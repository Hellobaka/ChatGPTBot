using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChatGPTv3.Core.Config;

namespace ChatGPTv3.UI.ViewModels;

/// <summary>
/// Single config entry bound to a UI row. Computed type flags determine which control renders.
/// </summary>
public partial class ConfigEntry : ObservableObject
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public object? DefaultValue { get; init; }
    public bool IsMultiline { get; init; }
    public bool IsList { get; init; }

    [ObservableProperty]
    private object? _value;

    // Control type flags — stable after construction (DefaultValue doesn't change)
    public bool IsSwitch => DefaultValue is bool;
    public bool IsNumber => DefaultValue is int or double or float or ushort;
    public bool IsText => !IsSwitch && !IsNumber && !IsMultiline && !IsList;

    /// <summary>
    /// Editable text for list-type values. One item per line.
    /// Fires property change when Value changes so the UI rebinds.
    /// </summary>
    public string ListText
    {
        get => Value switch
        {
            List<string> sl => string.Join("\n", sl),
            List<long> ll => string.Join("\n", ll),
            _ => Value?.ToString() ?? ""
        };
        set
        {
            if (DefaultValue is List<long>)
            {
                var list = (value ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => long.TryParse(l.Trim(), out var n) ? n : 0)
                    .ToList();
                Value = list;
            }
            else
            {
                Value = (value ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim()).ToList();
            }
        }
    }

    // Keep ListText binding current when Value changes
    partial void OnValueChanged(object? value) => OnPropertyChanged(nameof(ListText));

    /// <summary>Whether list items are long integers (QQ numbers, group IDs).</summary>
    public bool IsLongList => DefaultValue is List<long>;

    /// <summary>Returns current list items as an observable string collection for the list editor.</summary>
    public System.Collections.ObjectModel.ObservableCollection<string> GetListItems()
    {
        var items = new System.Collections.ObjectModel.ObservableCollection<string>();
        if (Value is List<string> sl) foreach (var s in sl) items.Add(s);
        else if (Value is List<long> ll) foreach (var l in ll) items.Add(l.ToString());
        return items;
    }

    /// <summary>Saves edited string items back to the typed list value.</summary>
    public void SaveListItems(System.Collections.ObjectModel.ObservableCollection<string> items)
    {
        if (IsLongList)
        {
            var list = new List<long>();
            foreach (var s in items)
                if (long.TryParse(s.Trim(), out var n)) list.Add(n);
            Value = list;
        }
        else
        {
            Value = items.Select(s => s.Trim()).ToList();
        }
    }

    public void Load<T>(T fallback)
    {
        Value = ConfigManager.GetConfig(Key, fallback);
    }

    public void Save()
    {
        switch (Value)
        {
            case bool b: ConfigManager.SetConfig(Key, b); break;
            case int i: ConfigManager.SetConfig(Key, i); break;
            case double d: ConfigManager.SetConfig(Key, d); break;
            case float f: ConfigManager.SetConfig(Key, f); break;
            case ushort u: ConfigManager.SetConfig(Key, u); break;
            case List<string> sl: ConfigManager.SetConfig(Key, sl); break;
            case List<long> ll: ConfigManager.SetConfig(Key, ll); break;
            default: ConfigManager.SetConfig(Key, Value as string ?? ""); break;
        }
    }

    public void Reset()
    {
        Value = DefaultValue;
        Save();
    }
}

/// <summary>
/// Grouped section within a tab.
/// </summary>
public class ConfigSection(string title, ObservableCollection<ConfigEntry> items)
{
    public string Title { get; } = title;
    public ObservableCollection<ConfigEntry> Items { get; } = items;
}

/// <summary>
/// A tab in the config UI, containing one or more sections.
/// </summary>
public class ConfigTab(string name, string icon, List<ConfigSection> sections)
{
    public string Name { get; } = name;
    public string Icon { get; } = icon;
    public List<ConfigSection> Sections { get; } = sections;
}

public partial class ConfigurationViewModel : ViewModelBase
{
    public ObservableCollection<ConfigTab> Tabs { get; } = [];

    public ConfigurationViewModel()
    {
        // ── Tab 1: 基本设置 ──
        var bot = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "BotName", Label = "机器人名称", DefaultValue = "ChatGPT" },
            new() { Key = "BotNicknames", Label = "机器人昵称", DefaultValue = new List<string>{"ChatGPT"}, IsList = true },
            new() { Key = "MasterQQ", Label = "主人QQ", DefaultValue = new List<long>(), IsList = true },
        };
        var debug = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "DebugMode", Label = "调试模式", DefaultValue = false },
        };
        Tabs.Add(new ConfigTab("基本设置", "Cog",
        [
            new ConfigSection("机器人身份", bot),
            new ConfigSection("调试", debug),
        ]));

        // ── Tab 2: LLM 设置 ──
        var llm = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "ChatMaxTokens", Label = "最大 Token 数", DefaultValue = 3000 },
            new() { Key = "ChatTemperature", Label = "温度 (0-2)", DefaultValue = 1.0 },
            new() { Key = "ChatTimeout", Label = "超时时间 (ms)", DefaultValue = 30000 },
            new() { Key = "StreamMode", Label = "流式输出", DefaultValue = true },
            new() { Key = "GroupPrompt", Label = "群聊系统提示词", DefaultValue = "", IsMultiline = true },
            new() { Key = "PrivatePrompt", Label = "私聊系统提示词", DefaultValue = "", IsMultiline = true },
        };
        Tabs.Add(new ConfigTab("LLM 设置", "Brain",
        [
            new ConfigSection("模型与提示词", llm),
        ]));

        // ── Tab 3: 权限控制 ──
        var perm = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "IsGroupBlackList", Label = "群列表为黑名单模式", DefaultValue = false },
            new() { Key = "IsPersonBlackList", Label = "个人列表为黑名单模式", DefaultValue = false },
            new() { Key = "GroupList", Label = "群号列表", DefaultValue = new List<long>(), IsList = true },
            new() { Key = "PersonList", Label = "个人QQ列表", DefaultValue = new List<long>(), IsList = true },
            new() { Key = "Filters", Label = "消息过滤关键字", DefaultValue = new List<string>{"[CQ:", "&#"}, IsList = true },
        };
        Tabs.Add(new ConfigTab("权限控制", "Lock",
        [
            new ConfigSection("群与个人权限", perm),
        ]));

        // ── Tab 4: 对话行为 ──
        var splitter = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "EnableSplitter", Label = "启用消息分段", DefaultValue = false },
            new() { Key = "EnableSplitterRemoveMarkdown", Label = "移除 Markdown", DefaultValue = true },
            new() { Key = "SplitterMaxLines", Label = "最大行数", DefaultValue = 3 },
            new() { Key = "SplitterRegexFirst", Label = "优先正则分段", DefaultValue = false },
            new() { Key = "SplitterRegexRemovePunctuation", Label = "正则移除标点", DefaultValue = false },
            new() { Key = "SplitterSimulateTypeSpeed", Label = "打字速度 (ms)", DefaultValue = 100 },
            new() { Key = "EnableSplitterRandomDelay", Label = "随机延迟", DefaultValue = true },
            new() { Key = "SplitterRandomDelayMin", Label = "延迟最小 (ms)", DefaultValue = 1000 },
            new() { Key = "SplitterRandomDelayMax", Label = "延迟最大 (ms)", DefaultValue = 4500 },
            new() { Key = "SplitterMinLength", Label = "最小长度", DefaultValue = 20 },
            new() { Key = "ContextMaxLength", Label = "上下文最大消息数", DefaultValue = 20 },
            new() { Key = "RemoveThinkBlock", Label = "移除思考块", DefaultValue = true },
            new() { Key = "LogThinkBlock", Label = "将思考内容输出到日志", DefaultValue = false },
        };
        var emoji = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "EnableEmojiPassiveSend", Label = "被动表情包", DefaultValue = false },
            new() { Key = "EnableEmojiActiveSend", Label = "主动表情包", DefaultValue = false },
            new() { Key = "EmojiSendProbability", Label = "发送概率 (%)", DefaultValue = 10 },
            new() { Key = "IgnoreNotEmoji", Label = "忽略非表情图片", DefaultValue = true },
            new() { Key = "RandomSendEmoji", Label = "随机发送", DefaultValue = true },
            new() { Key = "RecommendEmojiCount", Label = "推荐数量", DefaultValue = 5 },
            new() { Key = "MinEmojiRecommendScore", Label = "最低分数", DefaultValue = 0.5 },
            new() { Key = "NonEmojiPictureSaveDays", Label = "非表情保存天数", DefaultValue = 7 },
        };
        var reply = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "BaseAttention", Label = "基础关注度", DefaultValue = 0.1 },
            new() { Key = "AttnMention", Label = "@提及权重", DefaultValue = 1.0 },
            new() { Key = "AttnReplyToBot", Label = "回复机器人权重", DefaultValue = 0.9 },
            new() { Key = "AttnNickname", Label = "昵称提及权重", DefaultValue = 0.8 },
            new() { Key = "AttnQuestion", Label = "问句权重", DefaultValue = 0.5 },
            new() { Key = "AttnContinuity", Label = "连续对话权重", DefaultValue = 0.4 },
            new() { Key = "AttnImageFactor", Label = "图片因子", DefaultValue = 0.1 },
            new() { Key = "TimingJustSent", Label = "刚发送后意愿", DefaultValue = 0.1 },
            new() { Key = "TimingBriefPause", Label = "短暂停顿意愿", DefaultValue = 0.3 },
            new() { Key = "TimingOptimal", Label = "最佳时机意愿", DefaultValue = 1.0 },
            new() { Key = "TimingStale", Label = "陈旧会话意愿", DefaultValue = 0.6 },
            new() { Key = "TimingVeryStale", Label = "非常陈旧意愿", DefaultValue = 0.3 },
            new() { Key = "ActivityFirst", Label = "首次发言系数", DefaultValue = 1.0 },
            new() { Key = "ActivitySecond", Label = "第二次发言系数", DefaultValue = 0.5 },
            new() { Key = "ActivityThird", Label = "第三次发言系数", DefaultValue = 0.2 },
            new() { Key = "ActivityThrottleSeconds", Label = "活跃节流 (秒)", DefaultValue = 60 },
            new() { Key = "ReplyWillingAmplifier", Label = "回复意愿放大器", DefaultValue = 1.0 },
            new() { Key = "EnableLLMCheckShouldResponse", Label = "LLM 辅助回复决策", DefaultValue = false },
        };
        Tabs.Add(new ConfigTab("对话行为", "MessageText",
        [
            new ConfigSection("消息分段", splitter),
            new ConfigSection("表情包", emoji),
            new ConfigSection("回复意愿", reply),
        ]));

        // ── Tab 5: 视觉与接口 ──
        var vision = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "EnableVision", Label = "启用视觉识别", DefaultValue = true },
            new() { Key = "EnableVisionWhenMentioned", Label = "仅@时触发", DefaultValue = true },
        };
        var qdrant = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "QdrantHost", Label = "主机地址", DefaultValue = "localhost" },
            new() { Key = "QdrantPort", Label = "端口", DefaultValue = (ushort)6333 },
            new() { Key = "QdrantAPIKey", Label = "API Key", DefaultValue = "" },
        };
        var rerank = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "EnableRerank", Label = "启用重排序", DefaultValue = true },
            new() { Key = "RerankUrl", Label = "API 地址", DefaultValue = "https://lkeap.tencentcloudapi.com" },
            new() { Key = "RerankModelName", Label = "模型名", DefaultValue = "lke-reranker-base" },
            new() { Key = "RerankTimeout", Label = "超时 (ms)", DefaultValue = 3000 },
        };
        Tabs.Add(new ConfigTab("视觉与接口", "Eye",
        [
            new ConfigSection("视觉识别", vision),
            new ConfigSection("Qdrant", qdrant),
            new ConfigSection("Rerank", rerank),
        ]));

        // ── Tab 6: 记忆与计划 ──
        var diary = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "EnableDiary", Label = "启用日记", DefaultValue = true },
            new() { Key = "DiaryMessageThreshold", Label = "消息阈值", DefaultValue = 50 },
            new() { Key = "DiaryIntervalMinutes", Label = "间隔 (分钟)", DefaultValue = 120 },
            new() { Key = "DiaryReviewHours", Label = "回顾时长 (小时)", DefaultValue = 24 },
            new() { Key = "DiaryMaxKeep", Label = "保留天数", DefaultValue = 7 },
            new() { Key = "DiaryTimeout", Label = "生成超时 (ms)", DefaultValue = 60000 },
        };
        var schedule = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "EnableSchedules", Label = "启用日程", DefaultValue = true },
            new() { Key = "SchedulePrompt", Label = "日程提示词", DefaultValue = "", IsMultiline = true },
            new() { Key = "DefaultSchedule", Label = "默认日程", DefaultValue = "摸鱼" },
            new() { Key = "MinCronIntervalMinutes", Label = "最小 Cron 间隔 (分钟)", DefaultValue = 5 },
        };
        var compress = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "EnableCompressByTime", Label = "按时间触发压缩", DefaultValue = true },
            new() { Key = "EnableCompressByCount", Label = "按条数触发压缩", DefaultValue = true },
            new() { Key = "CompressIntervalMinutes", Label = "时间间隔 (分钟)", DefaultValue = 60 },
            new() { Key = "CompressMessageThreshold", Label = "消息条数阈值", DefaultValue = 20 },
        };
        var ctx = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "EnableQdrant", Label = "启用 Qdrant", DefaultValue = true },
            new() { Key = "MinMemorySimilarity", Label = "最小记忆相似度", DefaultValue = 0.8 },
            new() { Key = "MaxMemoryCount", Label = "最大记忆条数", DefaultValue = 5 },
            new() { Key = "MemoryDimensions", Label = "向量维度", DefaultValue = 1024 },
        };
        Tabs.Add(new ConfigTab("记忆与计划", "Calendar",
        [
            new ConfigSection("Qdrant", ctx),
            new ConfigSection("日记", diary),
            new ConfigSection("日程", schedule),
            new ConfigSection("上下文压缩", compress),
        ]));

        // ── Tab 7: MCP 与过滤 ──
        var mcp = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "EnableMCP", Label = "启用 MCP", DefaultValue = true },
            new() { Key = "MaxToolCallCountEachTurn", Label = "每轮最大工具调用", DefaultValue = 5 },
            new() { Key = "AbortToolCallCountEachTurn", Label = "中止阈值", DefaultValue = 7 },
        };
        var filter = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "UseLLMContentFilterFallback", Label = "LLM 过滤回退", DefaultValue = false },
            new() { Key = "ContentFilterFallbacks", Label = "备用回复列表", DefaultValue = new List<string>(), IsList = true },
        };
        Tabs.Add(new ConfigTab("MCP 与过滤", "Shield",
        [
            new ConfigSection("MCP", mcp),
            new ConfigSection("内容过滤", filter),
        ]));

        LoadAll();
    }

    private void LoadAll()
    {
        foreach (var tab in Tabs)
        foreach (var section in tab.Sections)
        foreach (var entry in section.Items)
        {
            switch (entry.DefaultValue)
            {
                case bool b: entry.Load(b); break;
                case int i: entry.Load(i); break;
                case double d: entry.Load(d); break;
                case float f: entry.Load(f); break;
                case ushort u: entry.Load(u); break;
                case List<string> sl: entry.Load(sl); break;
                case List<long> ll: entry.Load(ll); break;
                default: entry.Load(entry.DefaultValue as string ?? ""); break;
            }
        }
    }

    [RelayCommand]
    private void SaveAll()
    {
        foreach (var tab in Tabs)
        foreach (var section in tab.Sections)
        foreach (var entry in section.Items)
            entry.Save();

        AppConfig.Init();
        ErrorMessage = null;

        HandyControl.Controls.Growl.Success("配置已保存");
    }

    [RelayCommand]
    private void ResetAll()
    {
        foreach (var tab in Tabs)
        foreach (var section in tab.Sections)
        foreach (var entry in section.Items)
            entry.Reset();

        AppConfig.Init();
    }
}
