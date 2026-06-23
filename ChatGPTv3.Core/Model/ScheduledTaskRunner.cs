using ChatGPTv3.Core.Api;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Model.MCP;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.Core.Model;

/// <summary>
/// Monitors ScheduledTask table (every 30s) and executes due cron tasks.
/// Each task runs with a dedicated system prompt (persona + task-mode rules)
/// and full MCP tool chain access — tasks can do more than just text.
/// </summary>
public class ScheduledTaskRunner
{
    private const string TaskSystemPrompt = """
{0}

你现在正在执行一个事先安排的任务。这是你主动发起的行动，不是被动回复。

【任务模式规则】
- 直接自然地执行任务，不要等待、不要沉默
- 你拥有完整的工具调用能力，按需使用
- 不要把任务本身说出来（不说"我在执行延迟任务"）
- 执行完毕自然结束，不延伸无关话题
""";

    private Timer? _timer;
    private bool _running;

    public static ScheduledTaskRunner? Instance { get; private set; }

    /// <summary>
    /// Set by Entry during startup. Sends a message to a target group/private chat.
    /// Parameters: (groupId, qq, message).
    /// For group messages, qq is ignored. For private, groupId is 0.
    /// </summary>
    public static Func<long, long, string, Task>? SendReply { get; set; }

    public ScheduledTaskRunner()
    {
        Instance = this;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _timer = new Timer(_ => CheckAndExecute(), null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        _ = CheckAndExecute();
    }

    public void Stop()
    {
        _running = false;
        _timer?.Dispose();
    }

    // ═══════════════════════════════════════════════════════════
    //  Poll & execute
    // ═══════════════════════════════════════════════════════════

    private async Task CheckAndExecute()
    {
        if (!_running) return;

        try
        {
            var dueTasks = ScheduledTask.GetDue(DateTime.Now);
            foreach (var task in dueTasks)
            {
                if (!_running) break;
                await ExecuteTask(task);

                if (!_running) break;
                var next = CronHelper.GetNextFireTime(task.CronExpr, DateTime.Now);
                if (next.HasValue)
                {
                    task.NextFireAt = next.Value;
                    task.LastFiredAt = DateTime.Now;
                    ScheduledTask.Update(task);
                }
            }
        }
        catch (Exception ex)
        {
            CommonHelper.LogError?.Invoke("ScheduledTask", ex.Message);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Single task execution
    // ═══════════════════════════════════════════════════════════

    private async Task ExecuteTask(ScheduledTask task)
    {
        if (!_running) return;

        try
        {
            CommonHelper.LogInfo?.Invoke("ScheduledTask",
                $"执行: {task.TaskName} → {(task.TargetType == 0 ? "群" : "私聊")} {task.TargetId}");

            // ── Build task-mode system prompt (persona + rules) ──
            var persona = string.Format(TaskSystemPrompt, AppConfig.GroupPrompt);
            var userMsg = $"你想起来要做一件事：{task.ExtraPrompt}\n\n请自然地执行。";

            var messages = new List<ChatMessage>
            {
                ChatMessage.System(persona),
                ChatMessage.User(userMsg)
            };

            // ── MCP tool setup ──
            ToolExecutor? toolExecutor = null;
            if (AppConfig.EnableMCP)
            {
                var mcpCtx = new MCPToolContext
                {
                    GroupId = task.TargetType == 0 ? task.TargetId : 0,
                    QQ = task.TargetType == 1 ? task.TargetId : 0,
                    ChatIdentity = $"task_{task.Id}"
                };

                foreach (var c in MCPClientManager.Clients.OfType<MCPCustomClient>())
                    c.Context = mcpCtx;

                toolExecutor = new ToolExecutor(
                    () => MCPClientManager.GetToolsForConversation(mcpCtx).ToList(),
                    async (tc, ct2) => await MCPClientManager.ExecuteToolAsync(tc, ct2, mcpCtx));
            }

            // ── Call LLM ──
            var chatService = new ChatService();
            var response = await chatService.GetChatResultAsync(
                AppConfig.ChatAPIKeyId, messages,
                ChatService.Purpose.聊天,
                timeout: AppConfig.ChatTimeout,
                toolExecutor: toolExecutor,
                identity: $"task_{task.Id}");

            if (!_running) return;

            if (response == ChatService.ErrorMessage || string.IsNullOrWhiteSpace(response))
            {
                CommonHelper.LogWarning?.Invoke("ScheduledTask", $"{task.TaskName}: LLM 返回空");
                return;
            }

            CommonHelper.LogInfo?.Invoke("ScheduledTask",
                $"结果: {response[..Math.Min(response.Length, 100)]}");

            // ── Send to target ──
            if (_running)
                await SendToTarget(task, response);
        }
        catch (Exception ex)
        {
            CommonHelper.LogError?.Invoke("ScheduledTask", $"执行失败: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Message sending
    // ═══════════════════════════════════════════════════════════

    private static async Task SendToTarget(ScheduledTask task, string response)
    {
        if (SendReply == null) return;

        try
        {
            long groupId = task.TargetType == 0 ? task.TargetId : 0;
            long qq = task.TargetType == 1 ? task.TargetId : 0;
            await SendReply(groupId, qq, response);
        }
        catch (Exception ex)
        {
            CommonHelper.LogError?.Invoke("ScheduledTask", $"发送失败: {ex.Message}");
        }
    }
}
