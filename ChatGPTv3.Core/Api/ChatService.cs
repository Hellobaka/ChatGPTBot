using System.Text;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.Core.Api;

/// <summary>
/// Core LLM interaction service. Uses the HttpSse OpenAiChatClient instead of
/// Microsoft.Extensions.AI + OpenAI SDK. Implements prompt caching strategy.
/// </summary>
public class ChatService
{
    public enum Purpose
    {
        聊天, 图片描述, 日程获取, 分段, 表情包推荐, 回复意愿, 记忆提取, 工具总结
    }

    public const string ErrorMessage = "连接发生问题，查看日志排查问题";
    /// <summary>Non-null when the LLM response ended with a non-"stop" finish_reason.</summary>
    public string? LastAbnormalFinishReason { get; private set; }

    private readonly ToolCallService _toolCallService = new();

    /// <summary>Fires when an audio chunk is received (GPT-4o-audio streaming).</summary>
    public static event Action<string, byte[], string?>? OnAudioChunk;
    /// <summary>Fires when an inline image is received (generated image output).</summary>
    public static event Action<string, string>? OnImageChunk;
    /// <summary>Collects tool call data for post-turn summarization.</summary>
    public List<(string name, string args, string result, bool success)> ToolCallLog { get; } = [];

    /// <summary>
    /// Main chat completion entry point.
    /// </summary>
    public async Task<string> GetChatResultAsync(
        List<APIKeyPurpose>? keyList,
        List<ChatMessage> chatMessages,
        Purpose purpose,
        bool jsonMode = false,
        int timeout = 30000,
        ToolExecutor? toolExecutor = null,
        string? identity = null)
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
            identity);
    }

    /// <summary>
    /// Core chat completion with full tool call loop.
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
        string? identity = null)
    {
        // Normalize URL
        baseUrl = baseUrl.Replace("/chat/completions", "").TrimEnd('/');

        string msg = "";

        try
        {
            var options = new OpenAiChatClientOptions
            {
                BaseUrl = baseUrl,
                ApiKey = apiKey,
                TimeoutMs = timeout
            };

            using var client = new OpenAiChatClient(options);

            // Build the request
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

            // Add tools if available
            if (toolExecutor != null && AppConfig.EnableMCP)
            {
                request.Tools = toolExecutor.GetToolDefinitions();
                request.EnableAutoToolChoice();
            }

            // Reset tool call state for this conversation
            _toolCallService.ResetToolCallState(identity ?? string.Empty);

            // ── Tool call loop ──
            int toolCallRound = 0;
            const int maxToolCallRounds = 10;

            while (toolCallRound < maxToolCallRounds)
            {
                toolCallRound++;

                if (AppConfig.StreamMode)
                {
                    var (streamMsg, usage) = await ProcessStreamingAsync(client, request, identity);
                    msg = streamMsg;
                    TrackUsage(baseUrl, model, purpose, usage, apiKey);
                }
                else
                {
                    var (nonStreamMsg, usage) = await ProcessNonStreamingAsync(client, request);
                    msg = nonStreamMsg;
                    TrackUsage(baseUrl, model, purpose, usage, apiKey);
                }

                // Check if there are tool calls to execute
                var pendingToolCalls = _pendingToolCalls;
                _pendingToolCalls = null;

                if (pendingToolCalls == null || pendingToolCalls.Count == 0 || toolExecutor == null)
                {
                    break; // No tool calls, done
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
                var assistantMsg = new ChatMessage
                {
                    Role = "assistant",
                    Content = null,
                    ToolCalls = pendingToolCalls
                };
                chatMessages.Add(assistantMsg);

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
                request.Tools = null;
                request.ToolChoice = null;
            }

            // Add assistant response to context
            chatMessages.Add(ChatMessage.Assistant(msg));

            // Post-process
            msg = ResponseProcessor.ProcessResponse(msg);

            return msg;
        }
        catch (OpenAiApiException ex)
        {
            CommonHelper.LogError?.Invoke("ChatService", $"API error {ex.StatusCode}: {ex.Message}");
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

    // ── Streaming ────────────────────────────────────────

    private List<ToolCallRequest>? _pendingToolCalls;

    private async Task<(string msg, TokenUsageInfo? usage)> ProcessStreamingAsync(
        OpenAiChatClient client,
        ChatCompletionRequest request,
        string? identity)
    {
        var msg = new StringBuilder();
        TokenUsageInfo? usage = null;

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
                usage = update.Usage;

            // ── Accumulate text content ──
            var delta = update.GetDeltaContent();
            if (!string.IsNullOrEmpty(delta))
                msg.Append(delta);

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
                // Content might be a data URL for generated images
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
                var toolCalls = update.GetToolCalls();
                if (toolCalls != null && toolCalls.Count > 0)
                    _pendingToolCalls = toolCalls;

                if (msg.Length > 0)
                    CommonHelper.DebugLog("ToolCall", $"中间内容: {msg}");
            }
        }

        return (msg.ToString(), usage);
    }

    // ── Non-streaming ────────────────────────────────────

    private async Task<(string msg, TokenUsageInfo? usage)> ProcessNonStreamingAsync(
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

        if (toolCalls != null && toolCalls.Count > 0)
        {
            _pendingToolCalls = toolCalls;
        }

        return (msg, response.Usage);
    }

    // ── Helpers ──────────────────────────────────────────

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
    private readonly Func<ToolCallRequest, CancellationToken, Task<object?>> _executor;
    private readonly Func<List<ToolDefinition>> _getDefinitions;

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
