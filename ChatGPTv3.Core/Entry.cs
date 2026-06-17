using Another_Mirai_Native.Abstractions;
using Another_Mirai_Native.Abstractions.Attributes;
using ChatGPTv3.Core.Api;
using ChatGPTv3.Core.Commands;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Model;
using ChatGPTv3.Core.Model.MCP;
using ChatGPTv3.Core.Utilities;

namespace ChatGPTv3.Core;

[PluginInfo(
    appId: "me.cqp.luohuaming.ChatGPTv3",
    name: "ChatGPT 插件 v3",
    version: "3.0.0",
    description: "基于 AMN2 框架的 ChatGPT QQ 机器人插件 — 支持流式对话、记忆、心情、MCP工具调用",
    author: "落花茗"
)]
public class Entry : PluginBase
{
    private SchedulerManager? _schedulerManager;
    private ScheduledTaskRunner? _taskRunner;

    public override async Task OnEnableAsync(CancellationToken ct)
    {
        // ── Wire up logging ──
        CommonHelper.LogInfo = (tag, msg) => API.Logger.Info(tag, msg);
        CommonHelper.LogDebug = (tag, msg) => API.Logger.Debug(tag, msg);
        CommonHelper.LogWarning = (tag, msg) => API.Logger.Warn(tag, msg);
        CommonHelper.LogError = (tag, msg) => API.Logger.Error(tag, msg);
        ConfigManager.LogWarning = (tag, msg) => API.Logger.Warn(tag, msg);

        API.Logger.Info("ChatGPTv3", "插件正在启动...");

        // ── Capture bot identity ──
        PromptBuilder.CurrentBotQQ = API.AppApi.GetLoginQQ();

        // ── Initialize config ──
        string appDir = API.AppApi.GetAppDirectory();
        ConfigManager.Initialize(appDir);
        if (!ConfigManager.Load())
            API.Logger.Warn("ChatGPTv3", "配置文件加载失败，使用默认配置");

        // ── Initialize database ──
        SQLiteManager.AppDirectory = appDir;
        SQLiteManager.CreateDB();
        AppConfig.Init();

        // ── Validate minimum configuration ──
        if (AppConfig.ChatAPIKeyId.Count == 0)
        {
            API.Logger.Error("ChatGPTv3", "Chat API 未配置，插件无法使用");
            API.AppApi.DisablePlugin();
            return;
        }

        // ── Initialize subsystems (async, non-blocking startup) ──
        _ = Task.Run(async () =>
        {
            try
            {
                // Mood state (natural language, replaces old MoodManager)
                MoodState.Initialize(appDir);
                API.Logger.Info("ChatGPTv3", "心情状态已初始化");

                // Scheduler
                _schedulerManager = new SchedulerManager(appDir);
                if (AppConfig.EnableSchedules)
                    _schedulerManager.EnableTimer();
                API.Logger.Info("ChatGPTv3", "日程管理器已初始化");

                // Scheduled task runner
                _taskRunner = new ScheduledTaskRunner();
                _taskRunner.Start();
                API.Logger.Info("ChatGPTv3", "定时任务运行器已启动");

                // Validate API keys for sub-purposes
                if (AppConfig.ReplyAPIKeyId.Count == 0 && AppConfig.EnableLLMCheckShouldResponse)
                {
                    API.Logger.Warn("ChatGPTv3", "回复意愿 API 无效，已切换至内置方案");
                    AppConfig.EnableLLMCheckShouldResponse = false;
                }

                if (AppConfig.EnableQdrant && AppConfig.MemoryAPIKeyId.Count == 0)
                    API.Logger.Warn("ChatGPTv3", "记忆提取 API 无效，相关功能无法使用");

                if (AppConfig.EnableVision && AppConfig.ImageDescriberApiKeyId.Count == 0)
                {
                    API.Logger.Warn("ChatGPTv3", "图像描述 API 无效，已禁用");
                    AppConfig.EnableVision = false;
                }

                if (AppConfig.EnableRerank && AppConfig.RerankApiKeyId.Count == 0)
                {
                    API.Logger.Warn("ChatGPTv3", "重排序 API 无效，已禁用");
                    AppConfig.EnableRerank = false;
                }

                if (AppConfig.EnableSplitter && !AppConfig.SplitterRegexFirst
                    && AppConfig.SplitterApiKeyId.Count == 0)
                {
                    API.Logger.Warn("ChatGPTv3", "分段 API 无效，已切换至正则分段");
                    AppConfig.SplitterRegexFirst = true;
                }

                // Initialize memory
                MemoryManager.Initialize(appDir);
                API.Logger.Info("ChatGPTv3", "记忆管理器已初始化");

                // Initialize MCP
                if (AppConfig.EnableMCP)
                {
                    MCPClientManager.Load(appDir);
                    MCPClientManager.Rebuild();
                    API.Logger.Info("ChatGPTv3", $"MCP 已加载 {MCPClientManager.Clients.Count} 个客户端");
                }
                else
                {
                    API.Logger.Info("ChatGPTv3", "MCP 已禁用");
                }

                // TODO: Phase 7 completion
                // - Qdrant connection and memory loading
                // - Memory.LoadShortTermMemories() + LoadToDoItems()

                API.Logger.Info("ChatGPTv3", "所有子系统初始化完成");
            }
            catch (Exception ex)
            {
                API.Logger.Error("ChatGPTv3", $"初始化失败: {ex.Message}");
            }
        }, ct);

        // ── Subscribe to config hot-reload ──
        ConfigManager.OnConfigReloaded += () =>
        {
            API.Logger.Info("ChatGPTv3", "配置热重载完成");
            AppConfig.Init();
        };

        // ── Build chat pipelines ──
        ChatCommands.GroupPipeline = new ChatPipelineBuilder()
            .UseAccessControl()
            .UseMessageFilter()
            .UseConcurrencyGate()
            .UseReplyDecision()
            .UseChatHandler()
            .Build();

        ChatCommands.PrivatePipeline = new ChatPipelineBuilder()
            .UsePrivateAccessControl()
            .UseMessageFilter()
            .UsePrivateReplyDecision()
            .UseChatHandler()
            .Build();

        API.Logger.Info("ChatGPTv3", "插件启动完成");
    }
}
