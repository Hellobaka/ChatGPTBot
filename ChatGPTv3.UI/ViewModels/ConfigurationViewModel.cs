using ChatGPTv3.Core;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using ChatGPTv3.UI.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using Another_Mirai_Native.Abstractions.Models;

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

    public bool IsPresetSelector { get; init; }

    /// <summary>
    /// When true, this entry is read-only in group config mode because the pipeline
    /// reads it from global AppConfig only (access control runs before group config is loaded).
    /// </summary>
    public bool IsGroupReadOnly { get; init; }

    /// <summary>
    /// Computed: true when a group config is selected AND this entry is group-read-only.
    /// Set by ConfigurationViewModel.LoadAll() each time the group selection changes.
    /// </summary>
    [ObservableProperty]
    private bool _isDisabled;

    [ObservableProperty]
    private string? _presetValue = null;

    public Action<string>? OnPresetApplied { get; set; }

    partial void OnPresetValueChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            OnPresetApplied?.Invoke(value);
        }
    }

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
    partial void OnValueChanged(object? value)
    {
        OnPropertyChanged(nameof(ListText));
        OnPropertyChanged(nameof(ListCountText));
    }

    public string ListCountText => $"编辑列表 ({GetListItems().Count}个)";

    /// <summary>Whether list items are long integers (QQ numbers, group IDs).</summary>
    public bool IsLongList => DefaultValue is List<long>;

    /// <summary>Returns current list items as an observable string collection for the list editor.</summary>
    public System.Collections.ObjectModel.ObservableCollection<string> GetListItems()
    {
        var items = new System.Collections.ObjectModel.ObservableCollection<string>();
        if (Value is List<string> sl)
        {
            foreach (var s in sl)
            {
                items.Add(s);
            }
        }
        else if (Value is List<long> ll)
        {
            foreach (var l in ll)
            {
                items.Add(l.ToString());
            }
        }

        return items;
    }

    /// <summary>Saves edited string items back to the typed list value.</summary>
    public void SaveListItems(System.Collections.ObjectModel.ObservableCollection<string> items)
    {
        if (IsLongList)
        {
            var list = new List<long>();
            foreach (var s in items)
            {
                if (long.TryParse(s.Trim(), out var n))
                {
                    list.Add(n);
                }
            }

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

public partial class ConfigTargetItem : ObservableObject
{
    public long TargetId { get; init; }
    public bool IsGroup { get; init; }

    private string _displayText = string.Empty;
    public string DisplayText
    {
        get => _displayText;
        set => SetProperty(ref _displayText, value);
    }

    public OverrideConfig? Config { get; set; }
}

public partial class ConfigurationViewModel : ViewModelBase
{
    public ObservableCollection<ConfigTab> Tabs { get; } = [];

    public ObservableCollection<ConfigTargetItem> Targets { get; } = [];

    [ObservableProperty]
    private ConfigTargetItem? _selectedTarget;

    [ObservableProperty]
    private int _configVersion;

    public bool IsTargetSelected => SelectedTarget is { TargetId: > 0 };

    public string TargetSelectorText => SelectedTarget?.DisplayText ?? "无配置";

    partial void OnSelectedTargetChanged(ConfigTargetItem? value)
    {
        LoadAll();
    }

    public ConfigurationViewModel()
    {
        // ── Tab 1: 基本设置 ──
        var bot = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "BotName", Label = "机器人名称", DefaultValue = "ChatGPT" },
            new() { Key = "BotNicknames", Label = "机器人昵称", DefaultValue = new List<string>{"ChatGPT"}, IsList = true },
            new() { Key = "MasterQQ", Label = "主人QQ", DefaultValue = new List<long>(), IsList = true, IsGroupReadOnly = true },
            new() { Key = "EnableGroupReply", Label = "启用群聊回复", DefaultValue = false, IsGroupReadOnly = true },
        };
        var general = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "MessageDebounceMs", Label = "消息防抖间隔 (ms)", DefaultValue = 3000 },
        };
        var debug = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "DebugMode", Label = "调试模式", DefaultValue = false, IsGroupReadOnly = true },
        };
        Tabs.Add(new ConfigTab("基本设置", "Cog",
        [
            new ConfigSection("机器人身份", bot),
            new ConfigSection("通用", general),
            new ConfigSection("调试", debug),
        ]));

        // ── Tab 2: LLM 设置 ──
        var llm = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "ChatMaxTokens", Label = "最大 Token 数", DefaultValue = 3000 },
            new() { Key = "ChatTemperature", Label = "温度 (0-2)", DefaultValue = 1.0 },
            new() { Key = "ChatTimeout", Label = "对话超时 (ms)", DefaultValue = 30000 },
            new() { Key = "StreamMode", Label = "流式输出", DefaultValue = true },
            new() { Key = "GroupPrompt", Label = "群聊系统提示词", DefaultValue = "", IsMultiline = true },
            new() { Key = "PrivatePrompt", Label = "私聊系统提示词", DefaultValue = "", IsMultiline = true, IsGroupReadOnly = true },
        };
        var timeouts = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "ReplyTimeout", Label = "回复决策超时 (ms)", DefaultValue = 5000 },
            new() { Key = "SplitterTimeout", Label = "消息分段超时 (ms)", DefaultValue = 30000 },
            new() { Key = "ImageDescriberTimeout", Label = "图片描述超时 (ms)", DefaultValue = 30000 },
            new() { Key = "EmbeddingTimeout", Label = "向量嵌入超时 (ms)", DefaultValue = 3000 },
        };
        Tabs.Add(new ConfigTab("LLM 设置", "Brain",
        [
            new ConfigSection("模型与提示词", llm),
            new ConfigSection("超时设置", timeouts),
        ]));

        // ── Tab 3: 权限控制 ──
        var perm = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "IsGroupBlackList", Label = "群列表为黑名单模式", DefaultValue = false, IsGroupReadOnly = true },
            new() { Key = "IsPersonBlackList", Label = "个人列表为黑名单模式", DefaultValue = false, IsGroupReadOnly = true },
            new() { Key = "GroupList", Label = "群号列表", DefaultValue = new List<long>(), IsList = true, IsGroupReadOnly = true },
            new() { Key = "PersonList", Label = "个人QQ列表", DefaultValue = new List<long>(), IsList = true, IsGroupReadOnly = true },
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
            new() { Key = "SplitterSimulateTypeSpeed", Label = "打字速度 (字/分钟)", DefaultValue = 100 },
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
            new() { Key = "__ReplyPreset", Label = "回复意愿预设", DefaultValue = "默认", IsPresetSelector = true, OnPresetApplied = ApplyPreset },
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
            new() { Key = "QdrantHost", Label = "Qdrant 主机地址", DefaultValue = "localhost", IsGroupReadOnly = true },
            new() { Key = "QdrantPort", Label = "Qdrant 端口", DefaultValue = (ushort)6333, IsGroupReadOnly = true },
            new() { Key = "QdrantAPIKey", Label = "Qdrant API Key", DefaultValue = "", IsGroupReadOnly = true },
        };
        var rerank = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "EnableRerank", Label = "启用重排序", DefaultValue = true },
            new() { Key = "RerankTimeout", Label = "超时 (ms)", DefaultValue = 3000 },
        };
        var tencent = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "TencentSecretId", Label = "腾讯云 SecretId", DefaultValue = "", IsGroupReadOnly = true },
            new() { Key = "TencentSecretKey", Label = "腾讯云 SecretKey", DefaultValue = "", IsGroupReadOnly = true },
        };
        Tabs.Add(new ConfigTab("视觉与接口", "Eye",
        [
            new ConfigSection("视觉识别", vision),
            new ConfigSection("向量检索 (Qdrant)", qdrant),
            new ConfigSection("Rerank 重排序", rerank),
            new ConfigSection("腾讯云签名", tencent),
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
            new() { Key = "SchedulePrompt", Label = "日程提示词", DefaultValue = "喜欢打各种游戏，为人热情积极向上，作息健康，10%概率熬夜", IsMultiline = true },
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
            new() { Key = "EnableQdrant", Label = "启用 Qdrant 知识检索", DefaultValue = true },
            new() { Key = "MinMemorySimilarity", Label = "最小知识相似度", DefaultValue = 0.8 },
            new() { Key = "MaxMemoryCount", Label = "最大知识条数", DefaultValue = 5 },
            new() { Key = "MemoryDimensions", Label = "向量维度", DefaultValue = 1024 },
        };
        var relation = new ObservableCollection<ConfigEntry>
        {
            new() { Key = "RelationshipUpdateTime", Label = "关系值更新间隔 (天)", DefaultValue = 7, IsGroupReadOnly = true },
        };
        Tabs.Add(new ConfigTab("记忆与计划", "Calendar",
        [
            new ConfigSection("Qdrant 知识检索", ctx),
            new ConfigSection("关系管理", relation),
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

        BuildTargets();
        SelectedTarget = Targets[0];
    }

    private void BuildTargets()
    {
        Targets.Clear();
        Targets.Add(new ConfigTargetItem { TargetId = 0, IsGroup = true, DisplayText = "全局默认配置" });

        var configured = OverrideConfig.GetAllConfigured().ToList();

        // Groups first, then private
        foreach (var oc in configured.Where(c => c.IsGroup))
        {
            var item = new ConfigTargetItem
            {
                TargetId = oc.TargetId,
                IsGroup = true,
                DisplayText = $"群 {oc.TargetId}",
                Config = oc
            };
            Targets.Add(item);
            _ = ResolveTargetNameAsync(item);
        }

        foreach (var oc in configured.Where(c => !c.IsGroup))
        {
            var item = new ConfigTargetItem
            {
                TargetId = oc.TargetId,
                IsGroup = false,
                DisplayText = $"QQ {oc.TargetId}",
                Config = oc
            };
            Targets.Add(item);
            _ = ResolveTargetNameAsync(item);
        }
    }

    private void LoadAll()
    {
        var isTargetMode = SelectedTarget is { TargetId: > 0 };
        Dictionary<string, object>? overrides = null;

        if (isTargetMode && SelectedTarget?.Config?.ConfigJson != null)
        {
            try
            {
                overrides = JsonSerializer.Deserialize<Dictionary<string, object>>(SelectedTarget.Config.ConfigJson);
            }
            catch { }
        }

        foreach (var tab in Tabs)
        {
            foreach (var section in tab.Sections)
            {
                foreach (var entry in section.Items)
                {
                    entry.IsDisabled = isTargetMode && entry.IsGroupReadOnly;

                    if (overrides != null && overrides.TryGetValue(entry.Key, out var v) && v is JsonElement je)
                    {
                        entry.Value = entry.DefaultValue switch
                        {
                            bool => je.GetBoolean(),
                            int => je.GetInt32(),
                            double => je.GetDouble(),
                            float => (float)je.GetDouble(),
                            ushort => (ushort)je.GetInt32(),
                            List<string> => je.Deserialize<List<string>>(),
                            List<long> => je.Deserialize<List<long>>(),
                            string => je.GetString(),
                            _ => je.GetRawText()
                        };
                    }
                    else
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
            }
        }
    }

    [RelayCommand]
    private void SaveAll()
    {
        if (SelectedTarget is { TargetId: > 0 })
        {
            SaveTargetConfig();
            return;
        }

        foreach (var tab in Tabs)
        {
            foreach (var section in tab.Sections)
            {
                foreach (var entry in section.Items)
                {
                    entry.Save();
                }
            }
        }

        AppConfig.Init();
        ErrorMessage = null;

        HandyControl.Controls.Growl.Success("配置已保存");
    }

    private void SaveTargetConfig()
    {
        if (SelectedTarget is not { TargetId: > 0 })
        {
            return;
        }

        var snapshot = new Dictionary<string, object>();
        foreach (var tab in Tabs)
        {
            foreach (var section in tab.Sections)
            {
                foreach (var entry in section.Items)
                {
                    if (entry.Value != null)
                    {
                        var v = CoerceToDefaultType(entry.Value, entry.DefaultValue);
                        if (v != null)
                        {
                            snapshot[entry.Key] = v;
                        }
                    }
                }
            }
        }

        var config = SelectedTarget.Config ?? new OverrideConfig
        {
            TargetId = SelectedTarget.TargetId,
            IsGroup = SelectedTarget.IsGroup
        };
        config.ConfigJson = JsonSerializer.Serialize(snapshot);
        OverrideConfig.Save(config);
        OverrideConfig.ClearCache(SelectedTarget.TargetId, SelectedTarget.IsGroup);
        SelectedTarget.Config = OverrideConfig.Get(SelectedTarget.TargetId, SelectedTarget.IsGroup) ?? config;
        LoadAll();

        var label = SelectedTarget.IsGroup ? "群" : "私聊";
        HandyControl.Controls.Growl.Success($"已保存{label} {SelectedTarget.TargetId} 的配置");
    }

    private static object? CoerceToDefaultType(object? value, object? defaultValue)
    {
        if (value == null || defaultValue == null)
        {
            return value;
        }

        if (value is string s)
        {
            if (defaultValue is bool)
            {
                return bool.TryParse(s, out var b) ? b : null;
            }

            if (defaultValue is int && int.TryParse(s, out var iv))
            {
                return iv;
            }

            if ((defaultValue is double || defaultValue is float) && double.TryParse(s, out var dv))
            {
                return dv;
            }

            if (defaultValue is ushort && ushort.TryParse(s, out var uv))
            {
                return uv;
            }
        }

        return value;
    }

    private void ApplyPreset(string key)
    {
        var replySection = Tabs
            .First(t => t.Name == "对话行为")
            .Sections
            .First(s => s.Title == "回复意愿");

        var values = Presets.GetValueOrDefault(key);
        if (values == null)
        {
            return;
        }

        foreach (var entry in replySection.Items)
        {
            if (entry.Key == "__ReplyPreset")
            {
                continue;
            }

            if (!values.TryGetValue(entry.Key, out var val))
            {
                continue;
            }

            entry.Value = val;
        }
    }

    [RelayCommand]
    private void AddTargetConfig()
    {
        var dialog = new OverrideConfigInputDialog { Owner = GetActiveWindow() };
        dialog.ShowDialog();
        if (dialog.TargetId == null)
        {
            return;
        }

        var targetId = dialog.TargetId.Value;
        if (Targets.Any(t => t.TargetId == targetId && t.IsGroup == dialog.IsGroup))
        {
            Growl.Warning("该目标已有配置");
            return;
        }

        // Clone current global config as starting point
        var snapshot = new Dictionary<string, object>();
        foreach (var tab in Tabs)
        {
            foreach (var section in tab.Sections)
            {
                foreach (var entry in section.Items)
                {
                    if (entry.Value != null)
                    {
                        snapshot[entry.Key] = entry.Value;
                    }
                }
            }
        }

        var config = new OverrideConfig
        {
            TargetId = targetId,
            IsGroup = dialog.IsGroup,
            ConfigJson = JsonSerializer.Serialize(snapshot)
        };
        OverrideConfig.Save(config);
        var item = new ConfigTargetItem
        {
            TargetId = targetId,
            IsGroup = dialog.IsGroup,
            DisplayText = dialog.IsGroup ? $"群 {targetId}" : $"QQ {targetId}",
            Config = config
        };
        Targets.Add(item);
        SelectedTarget = item;
        LoadAll();
        _ = ResolveTargetNameAsync(item);
        var label = dialog.IsGroup ? "群" : "私聊";
        Growl.Success($"已为{label} {targetId} 创建配置");
    }

    [RelayCommand]
    private void DeleteTargetConfig()
    {
        if (SelectedTarget == null)
        {
            return;
        }
        var label = SelectedTarget.IsGroup ? "群" : "私聊";
        var result = HandyControl.Controls.MessageBox.Show(
            $"确定要删除{label} {SelectedTarget.TargetId} 的配置吗？该目标将恢复使用全局默认配置。",
            "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var targetId = SelectedTarget.TargetId;
        OverrideConfig.Delete(targetId, SelectedTarget.IsGroup);
        Targets.Remove(SelectedTarget);
        SelectedTarget = null;
        LoadAll();
        Growl.Success($"已删除{label} {targetId} 的配置");
    }

    private static async Task ResolveTargetNameAsync(ConfigTargetItem item)
    {
        try
        {
            if (item.IsGroup)
            {
                var info = await Task.Run(() => Core.Entry.ApiGroup?.GetGroupInfo(item.TargetId));
                if (info != null && !string.IsNullOrEmpty(info.Name))
                {
                    item.DisplayText = $"{info.Name} ({item.TargetId})";
                }
            }
            else
            {
                var friends = await Task.Run(() => Core.Entry.ApiFriend?.GetFriendInfos());
                var info = friends?.FirstOrDefault(f => f.QQ == item.TargetId);
                if (info != null && !string.IsNullOrEmpty(info.Nick))
                {
                    item.DisplayText = $"{info.Nick} ({item.TargetId})";
                }
            }
        }
        catch
        {
            // Keep numeric fallback
        }
    }

    private static readonly Dictionary<string, Dictionary<string, object>> Presets = new()
    {
        ["默认"] = new()
        {
            ["BaseAttention"] = 0.1,
            ["AttnMention"] = 1.0,
            ["AttnReplyToBot"] = 0.9,
            ["AttnNickname"] = 0.8,
            ["AttnQuestion"] = 0.5,
            ["AttnContinuity"] = 0.4,
            ["AttnImageFactor"] = 0.1,
            ["TimingJustSent"] = 0.1,
            ["TimingBriefPause"] = 0.3,
            ["TimingOptimal"] = 1.0,
            ["TimingStale"] = 0.6,
            ["TimingVeryStale"] = 0.3,
            ["ActivityFirst"] = 1.0,
            ["ActivitySecond"] = 0.5,
            ["ActivityThird"] = 0.2,
            ["ActivityThrottleSeconds"] = 60,
            ["ReplyWillingAmplifier"] = 1.0,
            ["EnableLLMCheckShouldResponse"] = false,
        },
        ["积极回复"] = new()
        {
            ["BaseAttention"] = 0.2,
            ["AttnMention"] = 1.0,
            ["AttnReplyToBot"] = 0.95,
            ["AttnNickname"] = 0.85,
            ["AttnQuestion"] = 0.6,
            ["AttnContinuity"] = 0.5,
            ["AttnImageFactor"] = 0.15,
            ["TimingJustSent"] = 0.2,
            ["TimingBriefPause"] = 0.5,
            ["TimingOptimal"] = 1.0,
            ["TimingStale"] = 0.7,
            ["TimingVeryStale"] = 0.4,
            ["ActivityFirst"] = 1.0,
            ["ActivitySecond"] = 0.6,
            ["ActivityThird"] = 0.3,
            ["ActivityThrottleSeconds"] = 30,
            ["ReplyWillingAmplifier"] = 1.5,
            ["EnableLLMCheckShouldResponse"] = false,
        },
        ["仅@时回复"] = new()
        {
            ["BaseAttention"] = 0.0,
            ["AttnMention"] = 1.0,
            ["AttnReplyToBot"] = 0.0,
            ["AttnNickname"] = 0.0,
            ["AttnQuestion"] = 0.0,
            ["AttnContinuity"] = 0.0,
            ["AttnImageFactor"] = 0.0,
            ["TimingJustSent"] = 1.0,
            ["TimingBriefPause"] = 1.0,
            ["TimingOptimal"] = 1.0,
            ["TimingStale"] = 1.0,
            ["TimingVeryStale"] = 1.0,
            ["ActivityFirst"] = 1.0,
            ["ActivitySecond"] = 1.0,
            ["ActivityThird"] = 1.0,
            ["ActivityThrottleSeconds"] = 0,
            ["ReplyWillingAmplifier"] = 0.0,
            ["EnableLLMCheckShouldResponse"] = false,
        },
        ["不积极回复"] = new()
        {
            ["BaseAttention"] = 0.03,
            ["AttnMention"] = 1.0,
            ["AttnReplyToBot"] = 0.5,
            ["AttnNickname"] = 0.4,
            ["AttnQuestion"] = 0.2,
            ["AttnContinuity"] = 0.15,
            ["AttnImageFactor"] = 0.02,
            ["TimingJustSent"] = 0.05,
            ["TimingBriefPause"] = 0.1,
            ["TimingOptimal"] = 0.5,
            ["TimingStale"] = 0.3,
            ["TimingVeryStale"] = 0.1,
            ["ActivityFirst"] = 1.0,
            ["ActivitySecond"] = 0.3,
            ["ActivityThird"] = 0.1,
            ["ActivityThrottleSeconds"] = 120,
            ["ReplyWillingAmplifier"] = 0.5,
            ["EnableLLMCheckShouldResponse"] = false,
        },
    };

    [RelayCommand]
    private void ResetAll()
    {
        foreach (var tab in Tabs)
        {
            foreach (var section in tab.Sections)
            {
                foreach (var entry in section.Items)
                {
                    entry.Reset();
                }
            }
        }

        AppConfig.Init();
    }

    private static System.Windows.Window? GetActiveWindow()
    {
        return System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive);
    }
}