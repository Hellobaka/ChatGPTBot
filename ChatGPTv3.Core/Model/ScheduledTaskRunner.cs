using ChatGPTv3.Core.Api;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.Core.Model;

/// <summary>
/// Monitors ScheduledTask table and executes due tasks with a dedicated system prompt.
/// Uses the same MCP tool chain as normal conversations — tasks can do more than text.
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
        _ = CheckAndExecute(); // run immediately on start
    }

    public void Stop()
    {
        _running = false;
        _timer?.Dispose();
    }

    private async Task CheckAndExecute()
    {
        if (!AppConfig.EnableMCP) return;

        try
        {
            var dueTasks = ScheduledTask.GetDue(DateTime.Now);
            foreach (var task in dueTasks)
            {
                await ExecuteTask(task);
                // Re-schedule
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

    private async Task ExecuteTask(ScheduledTask task)
    {
        try
        {
            CommonHelper.LogInfo?.Invoke("ScheduledTask", $"执行: {task.TaskName}");

            // Build the task-mode system prompt (persona + task rules)
            var persona = string.Format(TaskSystemPrompt, AppConfig.GroupPrompt);

            // Build user message — make it the bot's own intention
            var userMsg = $"你想起来要做一件事：{task.ExtraPrompt}\n\n请自然地执行。";

            var messages = new List<ChatMessage>
            {
                ChatMessage.System(persona),
                ChatMessage.User(userMsg)
            };

            // TODO: Phase 7 — wire MCP tool chain to allow more than text for scheduled tasks

            var chatService = new ChatService();
            var response = await chatService.GetChatResultAsync(
                AppConfig.ChatAPIKeyId, messages,
                ChatService.Purpose.聊天,
                timeout: AppConfig.ChatTimeout,
                identity: $"task_{task.Id}");

            if (response == ChatService.ErrorMessage || string.IsNullOrWhiteSpace(response))
                return;

            CommonHelper.LogInfo?.Invoke("ScheduledTask", $"结果: {response[..Math.Min(response.Length, 100)]}");

            // TODO: send to target (group/private) — requires AppApi access
        }
        catch (Exception ex)
        {
            CommonHelper.LogError?.Invoke("ScheduledTask", $"执行失败: {ex.Message}");
        }
    }
}
