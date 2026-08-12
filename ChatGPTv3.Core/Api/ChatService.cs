using ChatGPTv3.AnthropicClient;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;
using ChatGPTv3.ResponsesClient;
using System.Text;

namespace ChatGPTv3.Core.Api;

/// <summary>
/// Core LLM interaction service. Routes to the local zero-dependency HTTP/SSE
/// clients (OpenAI Chat Completions / Anthropic Messages / OpenAI Responses)
/// based on the API key's ApiFormat. Implements the shared tool-call loop.
/// </summary>
public class ChatService
{
    public enum Purpose
    {
        聊天, 图片描述, 日程获取, 分段, 表情包推荐, 回复意愿, 记忆提取, 工具总结, 日记
    }

    public const string ErrorMessage = "连接发生问题，查看日志排查问题";

    /// <summary>Non-null when the LLM response ended with a non-normal finish reason.</summary>
    public string? LastAbnormalFinishReason { get; private set; }

    private readonly ToolCallService _toolCallService = new();

    /// <summary>Fires when an audio chunk is received (GPT-4o-audio streaming).</summary>
    public static event Action<string, byte[], string?>? OnAudioChunk;
    /// <summary>Fires when an inline image is received (generated image output).</summary>
    public static event Action<string, string>? OnImageChunk;

    /// <summary>Collects tool call data for post-turn summarization.</summary>
    public List<(string callId, string name, string args, string result, bool success)> ToolCallLog { get; } = [];

    /// <summary>Per-instance response processor — avoids reasoning leakage across concurrent sessions.</summary>
    public readonly ResponseProcessor ResponseProcessor = new();

    /// <summary>Last accumulated reasoning, exposed for external consumers (e.g. pipeline trace, ChatTest).</summary>
    public string? LastReasoning => ResponseProcessor.LastReasoning;

    /// <summary>
    /// Main chat completion entry point. Picks a random bound key and routes by its ApiFormat.
    /// </summary>
    public async Task<string> GetChatResultAsync(
        List<APIKeyPurpose>? keyList,
        List<ChatMessage> chatMessages,
        Purpose purpose,
        bool jsonMode = false,
        int timeout = 30000,
        ToolExecutor? toolExecutor = null,
        string? identity = null,
        CancellationToken cancellationToken = default,
        Func<string, Task>? onIntermediateText = null)
    {
        // Randomly select a key if multiple available
        var key = keyList?.OrderBy(_ => Guid.NewGuid()).FirstOrDefault();
        if (key?.Key == null || key.Model == null)
        {
            CommonHelper.LogWarning?.Invoke("ChatService", "Key 为 null");
            return ErrorMessage;
        }

        return await GetChatResultAsync(
            key.Key.EndPoint,
            key.Key.Key,
            key.Model.Name,
            chatMessages,
            purpose,
            jsonMode,
            timeout,
            toolExecutor,
            identity,
            onIntermediateText: onIntermediateText,
            format: key.Key.ApiFormat,
            enableWebSearch: key.Key.EnableWebSearch);
    }

    /// <summary>
    /// Core chat completion with full tool call loop. The ApiFormat parameter
    /// selects OpenAI Chat Completions, Anthropic Messages, or OpenAI Responses.
    /// </summary>
    public async Task<string> GetChatResultAsync(
        string baseUrl,
        string apiKey,
        string model,
        List<ChatMessage> chatMessages,
        Purpose purpose,
        bool jsonMode = false,
        int timeout = 30000,
        ToolExecutor? toolExecutor = null,
        string? identity = null,
        Func<string, Task>? onIntermediateText = null,
        ApiFormat format = ApiFormat.OpenAI,
        bool enableWebSearch = false)
    {
        // Normalize URL
        baseUrl = baseUrl.Replace("/chat/completions", "").TrimEnd('/');

        try
        {
            return format switch
            {
                ApiFormat.Anthropic => await GetAnthropicChatResultAsync(
                    baseUrl, apiKey, model, chatMessages, purpose, jsonMode, timeout,
                    toolExecutor, identity, onIntermediateText, enableWebSearch),
                ApiFormat.Responses => await GetResponsesChatResultAsync(
                    baseUrl, apiKey, model, chatMessages, purpose, jsonMode, timeout,
                    toolExecutor, identity, onIntermediateText, enableWebSearch),
                _ => await GetOpenAiChatResultAsync(
                    baseUrl, apiKey, model, chatMessages, purpose, jsonMode, timeout,
                    toolExecutor, identity, onIntermediateText)
            };
        }
        catch (OpenAiApiException ex)
        {
            CommonHelper.LogError?.Invoke("ChatService", $"API error {ex.StatusCode}: {ex.Message}");
            return ErrorMessage;
        }
        catch (AnthropicApiException ex)
        {
            CommonHelper.LogError?.Invoke("ChatService", $"Anthropic API error {ex.StatusCode}: {ex.Message}");
            return ErrorMessage;
        }
        catch (ResponsesApiException ex)
        {
            CommonHelper.LogError?.Invoke("ChatService", $"Responses API error {ex.StatusCode}: {ex.Message}");
            return ErrorMessage;
        }
        catch (Exception ex)
        {
            CommonHelper.LogError?.Invoke("ChatService", $"Exception: {ex.Message}");
            return ErrorMessage;
        }
        finally
        {
            _toolCallService.CleanupToolCallState(identity ?? string.Empty);
        }
    }

    // ── OpenAI Chat Completions ────────────────────────────────

    private async Task<string> GetOpenAiChatResultAsync(
        string baseUrl,
        string apiKey,
        string model,
        List<ChatMessage> chatMessages,
        Purpose purpose,
        bool jsonMode,
        int timeout,
        ToolExecutor? toolExecutor,
        string? identity,
        Func<string, Task>? onIntermediateText)
    {
        var options = new OpenAiChatClientOptions
        {
            BaseUrl = baseUrl,
            ApiKey = apiKey,
            TimeoutMs = timeout
        };

        using var client = new OpenAiChatClient(options);

        var request = new ChatCompletionRequest
        {
            Model = model,
            Messages = chatMessages,
            MaxTokens = AppConfig.ChatMaxTokens,
            Temperature = AppConfig.ChatTemperature,
            Stream = AppConfig.StreamMode
        };

        if (jsonMode)
        {
            request.EnableJsonMode();
        }

        bool includeTools = toolExecutor != null && AppConfig.EnableMCP;
        if (includeTools)
        {
            request.Tools = toolExecutor!.GetToolDefinitions();
            request.EnableAutoToolChoice();
        }

        var msg = await RunToolLoopAsync(
            chatMessages, purpose, baseUrl, model, apiKey, toolExecutor, identity, onIntermediateText,
            streaming => streaming
                ? ProcessOpenAiStreamingAsync(client, request, identity)
                : ProcessOpenAiNonStreamingAsync(client, request),
            () =>
            {
                request.Tools = null;
                request.ToolChoice = null;
            });

        return ResponseProcessor.ProcessResponse(msg);
    }

    // ── Anthropic Messages ─────────────────────────────────────

    private async Task<string> GetAnthropicChatResultAsync(
        string baseUrl,
        string apiKey,
        string model,
        List<ChatMessage> chatMessages,
        Purpose purpose,
        bool jsonMode,
        int timeout,
        ToolExecutor? toolExecutor,
        string? identity,
        Func<string, Task>? onIntermediateText,
        bool enableWebSearch)
    {
        var options = new AnthropicChatClientOptions
        {
            BaseUrl = baseUrl,
            ApiKey = apiKey,
            TimeoutMs = timeout
        };

        using var client = new AnthropicChatClient(options);

        bool includeTools = toolExecutor != null && AppConfig.EnableMCP;
        List<AnthropicTool>? tools = null;
        if (includeTools)
        {
            tools = AnthropicMessageConverter.ToTools(toolExecutor!.GetToolDefinitions());
        }

        var msg = await RunToolLoopAsync(
            chatMessages, purpose, baseUrl, model, apiKey, toolExecutor, identity, onIntermediateText,
            streaming =>
            {
                var request = AnthropicMessageConverter.CreateRequest(
                    chatMessages, model, AppConfig.ChatMaxTokens, AppConfig.ChatTemperature,
                    streaming, includeTools ? tools : null, jsonMode, enableWebSearch);

                return streaming
                    ? ProcessAnthropicStreamingAsync(client, request, identity)
                    : ProcessAnthropicNonStreamingAsync(client, request);
            },
            () => includeTools = false);

        return ResponseProcessor.ProcessResponse(msg);
    }

    // ── OpenAI Responses ───────────────────────────────────────

    private async Task<string> GetResponsesChatResultAsync(
        string baseUrl,
        string apiKey,
        string model,
        List<ChatMessage> chatMessages,
        Purpose purpose,
        bool jsonMode,
        int timeout,
        ToolExecutor? toolExecutor,
        string? identity,
        Func<string, Task>? onIntermediateText,
        bool enableWebSearch)
    {
        var options = new ResponsesChatClientOptions
        {
            BaseUrl = baseUrl,
            ApiKey = apiKey,
            TimeoutMs = timeout
        };

        using var client = new ResponsesChatClient(options);

        bool includeTools = toolExecutor != null && AppConfig.EnableMCP;
        List<ResponsesTool>? tools = null;
        if (includeTools)
        {
            tools = ResponsesMessageConverter.ToTools(toolExecutor!.GetToolDefinitions());
        }

        var msg = await RunToolLoopAsync(
            chatMessages, purpose, baseUrl, model, apiKey, toolExecutor, identity, onIntermediateText,
            streaming =>
            {
                var request = ResponsesMessageConverter.CreateRequest(
                    chatMessages, model, AppConfig.ChatMaxTokens, AppConfig.ChatTemperature,
                    streaming, includeTools ? tools : null, jsonMode, enableWebSearch);

                return streaming
                    ? ProcessResponsesStreamingAsync(client, request, identity)
                    : ProcessResponsesNonStreamingAsync(client, request);
            },
            () => includeTools = false);

        return ResponseProcessor.ProcessResponse(msg);
    }

    // ── Shared tool loop ───────────────────────────────────────

    private async Task<string> RunToolLoopAsync(
        List<ChatMessage> chatMessages,
        Purpose purpose,
        string baseUrl,
        string model,
        string apiKey,
        ToolExecutor? toolExecutor,
        string? identity,
        Func<string, Task>? onIntermediateText,
        Func<bool, Task<(string msg, TokenUsageInfo? usage, List<ToolCallRequest>? toolCalls)>> sendRound,
        Action afterToolRound)
    {
        string msg = "";

        // Reset tool call state for this conversation
        _toolCallService.ResetToolCallState(identity ?? string.Empty);

        // ── Tool call loop ──
        int toolCallRound = 0;
        const int maxToolCallRounds = 10;

        while (toolCallRound < maxToolCallRounds)
        {
            toolCallRound++;

            var (roundMsg, usage, pendingToolCalls) = await sendRound(AppConfig.StreamMode);
            msg = roundMsg;
            TrackUsage(baseUrl, model, purpose, usage, apiKey);

            if (pendingToolCalls == null || pendingToolCalls.Count == 0 || toolExecutor == null)
            {
                break; // No tool calls, done
            }

            // Send intermediate text if LLM spoke during tool call round
            if (onIntermediateText != null && !string.IsNullOrWhiteSpace(msg))
            {
                await onIntermediateText(msg);
            }

            // Execute tool calls
            var toolResults = new List<ToolCallResult>();
            foreach (var tc in pendingToolCalls)
            {
                var (result, trackResult) = await _toolCallService.LogToolCallAsync(
                    tc,
                    async (toolCall, ct) =>
                    {
                        return await toolExecutor.ExecuteAsync(toolCall, ct);
                    },
                    identity ?? string.Empty,
                    CancellationToken.None);

                // Log for post-turn summarization
                ToolCallLog.Add((
                    tc.Id ?? "",
                    tc.Function.Name,
                    tc.Function.Arguments ?? "",
                    trackResult?.Result ?? result?.ToString() ?? "",
                    trackResult?.IsSuccess ?? false
                ));

                if (trackResult != null)
                {
                    toolResults.Add(trackResult);
                }
            }

            // Append assistant tool call message + tool results to conversation
            chatMessages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = null,
                ToolCalls = pendingToolCalls
            });

            // Only add successful tool results
            foreach (var tr in toolResults.Where(r => r.IsSuccess))
            {
                chatMessages.Add(ChatMessage.Tool(tr.ToolCallId, tr.Result));
            }

            // Check abort
            if (_toolCallService.ShouldAbortConversation(identity ?? string.Empty))
            {
                CommonHelper.LogWarning?.Invoke("ChatService", "工具调用次数超限，会话终止");
                break;
            }

            // Remove tools for subsequent calls (model should respond with final text)
            afterToolRound();
        }

        // Add assistant response to context
        chatMessages.Add(ChatMessage.Assistant(msg));

        return msg;
    }

    // ── OpenAI streaming ───────────────────────────────────────

    private async Task<(string msg, TokenUsageInfo? usage, List<ToolCallRequest>? toolCalls)>
        ProcessOpenAiStreamingAsync(
            OpenAiChatClient client,
            ChatCompletionRequest request,
            string? identity,
            CancellationToken ct = default)
    {
        var msg = new StringBuilder();
        TokenUsageInfo? usage = null;
        List<ToolCallRequest>? toolCalls = null;

        ResponseProcessor.Reset();

        await foreach (var update in client.StreamAsync(request))
        {
            // ── Handle stream_options usage chunk ──
            if (update.IsUsageChunk())
            {
                usage = update.Usage;
                continue;
            }

            if (update.Usage != null)
            {
                usage = update.Usage;
            }

            // ── Accumulate text content ──
            var delta = update.GetDeltaContent();
            if (!string.IsNullOrEmpty(delta))
            {
                msg.Append(delta);
            }

            // ── Accumulate reasoning ──
            ResponseProcessor.AppendReasoning(update.GetReasoningContent());

            // ── Handle audio data (GPT-4o-audio / TTS models) ──
            if (update.Choices?.FirstOrDefault()?.Delta?.Audio is AudioDelta audio
                && audio.GetAudioBytes() is byte[] audioBytes)
            {
                OnAudioChunk?.Invoke(identity ?? "", audioBytes, audio.Transcript);
            }

            // ── Handle inline image generation (DALL-E / GPT-4o image output) ──
            if (update.Choices?.FirstOrDefault()?.Delta?.Content != null)
            {
                var content = update.GetDeltaContent();
                if (!string.IsNullOrEmpty(content) && content.StartsWith("data:image/"))
                {
                    OnImageChunk?.Invoke(identity ?? "", content);
                }
            }

            // ── Check for abnormal finish_reason ──
            var reason = update.GetFinishReason();
            if (reason != null && reason != "stop"
                && reason != "tool_calls" && reason != "function_call")
            {
                LastAbnormalFinishReason = reason;
                CommonHelper.LogWarning?.Invoke("ChatService", $"异常结束原因: {reason}");
            }

            // ── Check for completed tool calls ──
            if (update.IsToolCallFinish())
            {
                var calls = update.GetToolCalls();
                if (calls != null && calls.Count > 0)
                {
                    toolCalls = calls;
                }

                if (msg.Length > 0)
                {
                    CommonHelper.DebugLog("ToolCall", $"中间内容: {msg}");
                }
            }
        }

        return (msg.ToString(), usage, toolCalls);
    }

    // ── OpenAI non-streaming ───────────────────────────────────

    private async Task<(string msg, TokenUsageInfo? usage, List<ToolCallRequest>? toolCalls)>
        ProcessOpenAiNonStreamingAsync(
            OpenAiChatClient client,
            ChatCompletionRequest request)
    {
        var response = await client.CompleteAsync(request);

        var finishReason = response.Choices.FirstOrDefault()?.FinishReason;
        if (finishReason != null && finishReason != "stop"
            && finishReason != "tool_calls" && finishReason != "function_call")
        {
            LastAbnormalFinishReason = finishReason;
            CommonHelper.LogWarning?.Invoke("ChatService", $"异常结束原因: {finishReason}");
        }

        var msg = response.GetFirstChoiceText() ?? string.Empty;
        var toolCalls = response.GetFirstChoiceToolCalls();

        return (msg, response.Usage, toolCalls);
    }

    // ── Anthropic streaming ────────────────────────────────────

    private async Task<(string msg, TokenUsageInfo? usage, List<ToolCallRequest>? toolCalls)>
        ProcessAnthropicStreamingAsync(
            AnthropicChatClient client,
            AnthropicChatRequest request,
            string? identity,
            CancellationToken ct = default)
    {
        var msg = new StringBuilder();
        TokenUsageInfo? usage = null;
        var toolCalls = new List<ToolCallRequest>();

        ResponseProcessor.Reset();

        await foreach (var update in client.StreamAsync(request))
        {
            if (!string.IsNullOrEmpty(update.TextDelta))
            {
                msg.Append(update.TextDelta);
            }

            ResponseProcessor.AppendReasoning(update.ThinkingDelta);

            if (update.IsToolUseFinish && update.ToolUseId != null)
            {
                toolCalls.Add(new ToolCallRequest
                {
                    Id = update.ToolUseId,
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = update.ToolUseName ?? string.Empty,
                        Arguments = update.ToolUseInputJson ?? "{}"
                    }
                });
            }

            if (update.StopReason != null)
            {
                var reason = update.StopReason;
                if (reason is not ("end_turn" or "tool_use" or "stop_sequence"))
                {
                    LastAbnormalFinishReason = reason;
                    CommonHelper.LogWarning?.Invoke("ChatService", $"异常结束原因: {reason}");
                }
            }

            if (update.Usage != null)
            {
                usage = update.Usage.ToTokenUsageInfo();
            }
        }

        return (msg.ToString(), usage, toolCalls.Count > 0 ? toolCalls : null);
    }

    // ── Anthropic non-streaming ────────────────────────────────

    private async Task<(string msg, TokenUsageInfo? usage, List<ToolCallRequest>? toolCalls)>
        ProcessAnthropicNonStreamingAsync(
            AnthropicChatClient client,
            AnthropicChatRequest request)
    {
        var response = await client.CompleteAsync(request);

        if (response.StopReason != null)
        {
            var reason = response.StopReason;
            if (reason is not ("end_turn" or "tool_use" or "stop_sequence"))
            {
                LastAbnormalFinishReason = reason;
                CommonHelper.LogWarning?.Invoke("ChatService", $"异常结束原因: {reason}");
            }
        }

        var msg = response.GetText() ?? string.Empty;
        var toolCalls = response.GetToolUse();

        return (msg, response.Usage?.ToTokenUsageInfo(), toolCalls);
    }

    // ── Responses streaming ────────────────────────────────────

    private async Task<(string msg, TokenUsageInfo? usage, List<ToolCallRequest>? toolCalls)>
        ProcessResponsesStreamingAsync(
            ResponsesChatClient client,
            ResponsesCreateRequest request,
            string? identity,
            CancellationToken ct = default)
    {
        var msg = new StringBuilder();
        TokenUsageInfo? usage = null;
        var toolCalls = new List<ToolCallRequest>();

        ResponseProcessor.Reset();

        await foreach (var update in client.StreamAsync(request))
        {
            if (!string.IsNullOrEmpty(update.TextDelta))
            {
                msg.Append(update.TextDelta);
            }

            ResponseProcessor.AppendReasoning(update.ReasoningDelta);

            if (update.ToolCalls != null)
            {
                foreach (var call in update.ToolCalls)
                {
                    if (!toolCalls.Any(x => x.Id == call.Id))
                    {
                        toolCalls.Add(call);
                    }
                }
            }

            if (update.Response != null)
            {
                if (update.Response.Usage != null)
                {
                    usage = update.Response.GetTokenUsage();
                }

                var responseCalls = update.Response.GetToolCalls();
                if (responseCalls != null)
                {
                    foreach (var call in responseCalls)
                    {
                        if (!toolCalls.Any(x => x.Id == call.Id))
                        {
                            toolCalls.Add(call);
                        }
                    }
                }

                var finishReason = update.Response.GetFinishReason();
                if (finishReason is not (null or "stop" or "tool_calls" or "function_call"
                    or "length" or "content_filter" or "incomplete"))
                {
                    LastAbnormalFinishReason = finishReason;
                    CommonHelper.LogWarning?.Invoke("ChatService", $"异常结束原因: {finishReason}");
                }

                if (update.Response.Error != null)
                {
                    throw new ResponsesApiException(
                        0,
                        update.Response.Error.Message ?? "Responses API stream error",
                        errorCode: update.Response.Error.Code);
                }
            }
        }

        return (msg.ToString(), usage, toolCalls.Count > 0 ? toolCalls : null);
    }

    // ── Responses non-streaming ────────────────────────────────

    private async Task<(string msg, TokenUsageInfo? usage, List<ToolCallRequest>? toolCalls)>
        ProcessResponsesNonStreamingAsync(
            ResponsesChatClient client,
            ResponsesCreateRequest request)
    {
        var response = await client.CompleteAsync(request);

        if (response.Error != null)
        {
            throw new ResponsesApiException(
                0,
                response.Error.Message ?? "Responses API error",
                errorCode: response.Error.Code);
        }

        var finishReason = response.GetFinishReason();
        if (finishReason is not (null or "stop" or "tool_calls" or "function_call"
            or "length" or "content_filter" or "incomplete"))
        {
            LastAbnormalFinishReason = finishReason;
            CommonHelper.LogWarning?.Invoke("ChatService", $"异常结束原因: {finishReason}");
        }

        var msg = response.GetText() ?? string.Empty;
        var toolCalls = response.GetToolCalls();

        return (msg, response.GetTokenUsage(), toolCalls);
    }

    // ── Helpers ────────────────────────────────────────────────

    private static void TrackUsage(string baseUrl, string model, Purpose purpose,
        TokenUsageInfo? usage, string apiKey)
    {
        if (usage != null)
        {
            UsageTracker.TrackUsage(baseUrl, model, purpose.ToString(), usage, apiKey);
        }
    }
}

/// <summary>
/// Abstraction for tool execution, allowing MCPClientManager or mock executors.
/// </summary>
public class ToolExecutor
{
    private readonly Func<List<ToolDefinition>> _getDefinitions;
    private readonly Func<ToolCallRequest, CancellationToken, Task<object?>> _executor;

    public ToolExecutor(
        Func<List<ToolDefinition>> getDefinitions,
        Func<ToolCallRequest, CancellationToken, Task<object?>> executor)
    {
        _getDefinitions = getDefinitions;
        _executor = executor;
    }

    public List<ToolDefinition> GetToolDefinitions() => _getDefinitions();

    public Task<object?> ExecuteAsync(ToolCallRequest toolCall, CancellationToken ct) =>
        _executor(toolCall, ct);
}
