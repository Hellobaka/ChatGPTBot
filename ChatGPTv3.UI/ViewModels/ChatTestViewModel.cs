using Another_Mirai_Native.Abstractions.Context;
using Another_Mirai_Native.Abstractions.Enums;
using Another_Mirai_Native.Abstractions.Models;
using Another_Mirai_Native.Abstractions.Models.MessageItem;
using ChatGPTv3.Core;
using ChatGPTv3.Core.Api;
using ChatGPTv3.Core.Commands;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.UI.Mock;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Regex = System.Text.RegularExpressions.Regex;

namespace ChatGPTv3.UI.ViewModels;

public partial class ChatBubbleItem : ObservableObject
{
    [ObservableProperty]
    private string _content = "";

    public bool HasReasoning => !string.IsNullOrWhiteSpace(Reasoning);

    public bool IsSelf { get; init; }

    public string? Reasoning { get; init; }

    public string Sender { get; init; } = "";

    public DateTime Time { get; init; } = DateTime.Now;
}

/// <summary>Candidate item for QQ selection (friend or group member).</summary>
public partial class FriendItem : ObservableObject
{
    public string Display => string.IsNullOrEmpty(Name) ? Id.ToString() : $"{Name} ({Id})";

    public long Id { get; init; }

    public string Name { get; init; } = "";
}

/// <summary>Candidate item for group selection (name + id display).</summary>
public partial class GroupItem : ObservableObject
{
    public string Display => string.IsNullOrEmpty(Name) ? Id.ToString() : $"{Name} ({Id})";

    public long Id { get; init; }

    public string Name { get; init; } = "";
}

/// <summary>A pending image to send (local source path, computed hash, preview).</summary>
public partial class PendingImage : ObservableObject
{
    public string Hash { get; init; } = "";

    public bool IsEmoji { get; init; }

    public BitmapImage? Preview { get; init; }

    public string SourcePath { get; init; } = "";
}

public partial class ChatTestViewModel : ViewModelBase
{
    private static readonly Regex AtRegex = new(@"\[CQ:at,qq=(?<qq>\d+)\]",
        System.Text.RegularExpressions.RegexOptions.Compiled |
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static string PersistPath => Path.Combine(
        MockAppApi.Instance.GetAppDirectory(), "chat_test_state.json");

    // Full pipelines for testing (same as production)
    private static readonly Func<ChatContext, Task> TestGroupPipeline = new ChatPipelineBuilder()
        .UseAccessControl()
        .UseMessageFilter()
        .UseConcurrencyGate()
        .UseMessageImageResolver()
        .UseMessageAtResolver()
        .UseMessageReferenceResolver()
        .UseMessageRecorder()
        .UseReplyDecision()
        .UseChatHandler()
        .UseBotMessageRecorder()
        .UseToolCallRecorder()
        .Build();

    private static readonly Func<ChatContext, Task> TestPrivatePipeline = new ChatPipelineBuilder()
        .UsePrivateAccessControl()
        .UseMessageFilter()
        .UseMessageImageResolver()
        .UseMessageAtResolver()
        .UseMessageReferenceResolver()
        .UseMessageRecorder()
        .UsePrivateReplyDecision()
        .UseChatHandler()
        .UseBotMessageRecorder()
        .UseToolCallRecorder()
        .Build();

    private readonly List<GroupItem> _allGroupCandidates = [];
    private readonly List<FriendItem> _allQQCandidates = [];
    private readonly List<string> _sendHistory = [];
    private readonly Dispatcher _uiDispatcher;

    /// <summary>
    /// Shared mock conversation history for the current test session.
    /// All targets share one history; cleared on restart or by RefreshHistory.
    /// </summary>
    private List<ChatRecord> _mockHistory = [];

    /// <summary>Latest reasoning per conversation, waiting to be attached to the next bubble.</summary>
    private readonly Dictionary<string, string> _pendingReasonings = new();

    /// <summary>Identity of the conversation currently being tested; filters static events from other sources.</summary>
    private string? _activeIdentity;

    /// <summary>Intermediate text already rendered as its own bubble, to avoid a duplicate Bot bubble via SendFunc.</summary>
    private (string Identity, string Text)? _pendingIntermediate;

    private string? _historyDraft;
    private int _historyIndex = -1;
    private long _lastGroupId;
    private long _lastQQ;

    [ObservableProperty]
    private string _groupIdText = string.Empty;

    [ObservableProperty]
    private string _qqText = string.Empty;

    [ObservableProperty]
    private bool _isPrivateMode;

    [ObservableProperty]
    private bool _isSending;

    [ObservableProperty]
    private string _messageText = "";

    [ObservableProperty]
    private GroupItem? _selectedGroup;

    [ObservableProperty]
    private FriendItem? _selectedQQ;

    public bool HasPendingImages => PendingImages.Count > 0;

    public bool HasQQSelected => SelectedQQ != null;

    public ObservableCollection<ChatBubbleItem> Messages { get; } = [];

    public ObservableCollection<PendingImage> PendingImages { get; } = [];

    /// <summary>
    /// Adds a "tool call" bubble to the conversation when ChatService reports
    /// that a tool is starting to execute.
    /// </summary>
    public void AddToolCallBubble(string identity, string toolName, string arguments)
    {
        if (identity != _activeIdentity)
        {
            return;
        }

        var roundReasoning = TakePendingReasoning(identity);
        var reasoningText = arguments;
        if (!string.IsNullOrWhiteSpace(roundReasoning))
        {
            reasoningText = $"[工具参数]\n{arguments}\n[本轮思考]\n{roundReasoning}";
        }

        Messages.Add(new ChatBubbleItem
        {
            Content = $"🔧 调用工具：{toolName}",
            IsSelf = false,
            Sender = "Tool",
            Time = DateTime.Now,
            Reasoning = reasoningText
        });
    }

    /// <summary>Stores the latest round reasoning for a conversation.</summary>
    public void SetPendingReasoning(string identity, string reasoning)
    {
        if (identity == _activeIdentity)
        {
            _pendingReasonings[identity] = reasoning;
        }
    }

    /// <summary>
    /// Renders intermediate text spoken during a tool-call round as its own bubble.
    /// The same text is also routed through SendFunc, which suppresses the duplicate.
    /// </summary>
    public void AddIntermediateTextBubble(string identity, string text)
    {
        if (identity != _activeIdentity)
        {
            return;
        }

        _pendingIntermediate = (identity, text);
        Messages.Add(new ChatBubbleItem
        {
            Content = text,
            IsSelf = false,
            Sender = "中间文本",
            Reasoning = TakePendingReasoning(identity),
            Time = DateTime.Now
        });
    }

    private string? TakePendingReasoning(string identity)
    {
        return _pendingReasonings.Remove(identity, out var reasoning) ? reasoning : null;
    }

    private static string GetConversationIdentity(ChatContext ctx) =>
        ctx.IsGroup ? $"group_{ctx.GroupId}" : $"private_{ctx.QQ}";

    public ChatTestViewModel()
    {
        _uiDispatcher = System.Windows.Application.Current?.Dispatcher
                        ?? Dispatcher.CurrentDispatcher;
        LoadPersistedState();
        LoadGroupCandidates();
        ReloadQQCandidates();
    }

    private static void AppendParsedTextAndMentions(Message message, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        int current = 0;
        foreach (System.Text.RegularExpressions.Match match in AtRegex.Matches(text))
        {
            if (match.Index > current)
            {
                var plain = text[current..match.Index];
                if (!string.IsNullOrEmpty(plain))
                {
                    message.MessageChain.Add(new Text(plain));
                }
            }

            if (long.TryParse(match.Groups["qq"].Value, out var qq))
            {
                message.MessageChain.Add(new At(qq, allTarget: false));
            }

            current = match.Index + match.Length;
        }

        if (current < text.Length)
        {
            var tail = text[current..];
            if (!string.IsNullOrEmpty(tail))
            {
                message.MessageChain.Add(new Text(tail));
            }
        }
    }

    private static string BuildPipelineTraceMessage(ChatContext ctx)
    {
        var failedReport = ctx.FormatTraceReport(failedOnly: true);
        return failedReport == "没有失败节点"
            ? "（消息未被处理，但没有记录到失败节点）"
            : $"（消息未被处理）\n{failedReport}";
    }

    private static byte[] ComputeMD5(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        return md5.ComputeHash(stream);
    }

    private static BitmapImage? LoadPreview(string path)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path);
            bmp.DecodePixelWidth = 120;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    [RelayCommand]
    private void AddEmoji()
    {
        AddFiles(isEmoji: true);
    }

    private void AddFiles(bool isEmoji)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|所有文件|*.*"
        };
        if (dlg.ShowDialog() != true)
        {
            return;
        }

        foreach (var path in dlg.FileNames)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var hashBytes = ComputeMD5(path);
            var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            var preview = LoadPreview(path);
            PendingImages.Add(new PendingImage
            {
                SourcePath = path,
                Hash = hash,
                Preview = preview,
                IsEmoji = isEmoji
            });
        }
        OnPropertyChanged(nameof(HasPendingImages));
    }

    [RelayCommand]
    private void AddImage()
    {
        AddFiles(isEmoji: false);
    }

    [RelayCommand]
    private void ClearImages()
    {
        PendingImages.Clear();
        OnPropertyChanged(nameof(HasPendingImages));
    }

    [RelayCommand]
    private void InsertAtBot()
    {
        var botQQ = PromptBuilder.CurrentBotQQ != 0
            ? PromptBuilder.CurrentBotQQ
            : MockAppApi.Instance.MockBotQQ;

        var atText = $"[CQ:at,qq={botQQ}]";
        if (string.IsNullOrWhiteSpace(MessageText))
        {
            MessageText = atText;
            return;
        }

        MessageText = MessageText.EndsWith(' ')
            ? MessageText + atText
            : MessageText + " " + atText;
    }

    private void LoadGroupCandidates()
    {
        _allGroupCandidates.Clear();
        try
        {
            var groups = Entry.ApiGroup?.GetGroupList() ?? MockGroupApi.Instance.GetGroupList();
            foreach (var g in groups)
            {
                _allGroupCandidates.Add(new GroupItem { Id = g.Group, Name = g.Name ?? "" });
            }

            if (!IsPrivateMode && _lastGroupId > 0)
            {
                var saved = _allGroupCandidates.FirstOrDefault(g => g.Id == _lastGroupId);
                if (saved != null)
                {
                    SelectedGroup = saved;
                }
            }
            else if (!IsPrivateMode && _allGroupCandidates.Count > 0 && SelectedGroup == null)
            {
                SelectedGroup = _allGroupCandidates[0];
            }

            if (_lastGroupId > 0)
            {
                GroupIdText = _lastGroupId.ToString();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载群列表失败: {ex.Message}";
        }
    }

    partial void OnIsPrivateModeChanged(bool value)
    {
        SelectedQQ = null;
        ReloadQQCandidates();
    }

    partial void OnSelectedGroupChanged(GroupItem? value)
    {
        SelectedQQ = null;
        if (!IsPrivateMode)
        {
            ReloadQQCandidates();
        }
    }

    partial void OnSelectedQQChanged(FriendItem? value)
    {
        OnPropertyChanged(nameof(HasQQSelected));
    }

    [RelayCommand]
    private void RefreshHistory()
    {
        Messages.Clear();
        _mockHistory = [];
        _pendingReasonings.Clear();
        _activeIdentity = null;
        _pendingIntermediate = null;
    }

    [RelayCommand]
    private void RefreshQQCandidates()
    {
        ReloadQQCandidates();
        RefreshHistory();
    }

    private void ReloadQQCandidates()
    {
        _allQQCandidates.Clear();
        try
        {
            if (IsPrivateMode)
            {
                var friends = Entry.ApiFriend?.GetFriendInfos() ?? MockFriendApi.Instance.GetFriendInfos();
                foreach (var f in friends)
                {
                    _allQQCandidates.Add(new FriendItem { Id = f.QQ, Name = f.Nick ?? "" });
                }
            }
            else if (SelectedGroup != null)
            {
                var members = Entry.ApiGroup?.GetGroupMembers(SelectedGroup.Id) ?? MockGroupApi.Instance.GetGroupMembers(SelectedGroup.Id);
                foreach (var m in members)
                {
                    var name = !string.IsNullOrWhiteSpace(m.Card) ? m.Card : (m.Nick ?? "");
                    _allQQCandidates.Add(new FriendItem { Id = m.QQ, Name = name });
                }
            }

            // Auto-select saved QQ
            if (_lastQQ > 0)
            {
                var saved = _allQQCandidates.FirstOrDefault(f => f.Id == _lastQQ);
                if (saved != null)
                {
                    SelectedQQ = saved;
                }
            }
            else if (_allQQCandidates.Count > 0 && SelectedQQ == null)
            {
                SelectedQQ = _allQQCandidates[0];
            }

            if (_lastQQ > 0)
            {
                QqText = _lastQQ.ToString();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载QQ列表失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RemoveImage(PendingImage? image)
    {
        if (image == null)
        {
            return;
        }

        PendingImages.Remove(image);
        OnPropertyChanged(nameof(HasPendingImages));
    }

    // ═══════════════════════════════════════════════════════════
    //  Input history (up/down arrow navigation)
    // ═══════════════════════════════════════════════════════════

    public void HistoryDown()
    {
        if (_historyIndex == -1) { return; }

        if (_historyIndex < _sendHistory.Count - 1)
        {
            _historyIndex++;
            MessageText = _sendHistory[_historyIndex];
        }
        else
        {
            _historyIndex = -1;
            MessageText = _historyDraft ?? "";
            _historyDraft = null;
        }
    }

    public void HistoryUp()
    {
        if (_sendHistory.Count == 0) { return; }

        if (_historyIndex == -1)
        {
            _historyDraft = MessageText;
            _historyIndex = _sendHistory.Count - 1;
        }
        else if (_historyIndex > 0)
        {
            _historyIndex--;
        }

        MessageText = _sendHistory[_historyIndex];
    }

    /// <summary>
    /// Adds an item to send history. Duplicate content is removed first so the
    /// same text only exists once and the latest send moves it to the end.
    /// </summary>
    private void AddToSendHistory(string text)
    {
        _sendHistory.RemoveAll(x => x == text);
        _sendHistory.Add(text);
    }

    // ═══════════════════════════════════════════════════════════
    //  Persistence
    // ═══════════════════════════════════════════════════════════

    private void LoadPersistedState()
    {
        try
        {
            if (File.Exists(PersistPath))
            {
                var json = File.ReadAllText(PersistPath);
                var state = JsonSerializer.Deserialize<ChatTestState>(json);
                if (state != null)
                {
                    _lastGroupId = state.LastGroupId;
                    _lastQQ = state.LastQQ;
                    if (state.SendHistory != null)
                    {
                        foreach (var item in state.SendHistory)
                        {
                            AddToSendHistory(item);
                        }
                    }
                }
            }
        }
        catch { }
    }

    private void SavePersistedState()
    {
        try
        {
            var state = new ChatTestState
            {
                LastGroupId = _lastGroupId,
                LastQQ = _lastQQ,
                SendHistory = _sendHistory
            };
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PersistPath, json);
        }
        catch { }
    }

    private record ChatTestState
    {
        public long LastGroupId { get; init; }
        public long LastQQ { get; init; }
        public List<string>? SendHistory { get; init; }
    }

    // ═══════════════════════════════════════════════════════════
    //  Image handling
    // ═══════════════════════════════════════════════════════════

    private List<Image> PrepareImagesForSend()
    {
        var images = new List<Image>();
        var imageDir = CommonHelper.GetAppImageDirectory();
        Directory.CreateDirectory(imageDir);

        foreach (var p in PendingImages)
        {
            if (!File.Exists(p.SourcePath))
            {
                continue;
            }

            var ext = Path.GetExtension(p.SourcePath);
            if (string.IsNullOrEmpty(ext))
            {
                ext = ".png";
            }

            var destPath = Path.Combine(imageDir, $"{p.Hash}{ext}");
            if (!File.Exists(destPath))
            {
                File.Copy(p.SourcePath, destPath, overwrite: false);
            }

            images.Add(new Image(destPath, p.Hash, isFlash: false, isEmoji: p.IsEmoji));
        }
        return images;
    }

    // ═══════════════════════════════════════════════════════════
    //  Send
    // ═══════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task SendAsync()
    {
        var text = MessageText?.Trim();
        var hasImages = PendingImages.Count > 0;
        if ((string.IsNullOrWhiteSpace(text) && !hasImages) || IsSending)
        {
            return;
        }

        // Parse QQ from text input
        if (!long.TryParse(QqText.Trim(), out var qq) || qq <= 0)
        {
            ErrorMessage = "请输入有效的 QQ 号";
            return;
        }

        // Parse Group ID from text input (for group mode)
        var isGroup = !IsPrivateMode;
        long groupId = 0;
        if (isGroup)
        {
            if (!long.TryParse(GroupIdText.Trim(), out groupId) || groupId <= 0)
            {
                ErrorMessage = "请输入有效的群号";
                return;
            }
        }

        // Save to history
        if (!string.IsNullOrWhiteSpace(text))
        {
            AddToSendHistory(text);
            _historyIndex = -1;
            _historyDraft = null;
        }

        // Update last selected
        _lastGroupId = groupId;
        _lastQQ = qq;
        SavePersistedState();

        var previewText = string.IsNullOrWhiteSpace(text) ? "[图片]" : text;
        var userBubble = new ChatBubbleItem { Content = previewText, IsSelf = true, Sender = $"QQ: {qq}" };
        Messages.Add(userBubble);
        MessageText = "";
        IsSending = true;
        ErrorMessage = null;

        // ── Build the AMN2 Message with the mock plugin API ──
        var api = MockPluginApi.Instance;
        var message = new Message(api, Random.Shared.Next() * -1, text ?? "");
        message.MessageChain.Clear();
        if (!string.IsNullOrWhiteSpace(text))
        {
            AppendParsedTextAndMentions(message, text);
        }

        foreach (var img in PrepareImagesForSend())
        {
            message.MessageChain.Add(img);
        }

        PendingImages.Clear();
        OnPropertyChanged(nameof(HasPendingImages));

        // ── Build context with mocked AMN2 objects ──
        ChatContext ctx;
        if (isGroup)
        {
            ctx = new ChatContext
            {
                MessageText = text ?? "",
                GroupCtx = new GroupMessageContext(api,
                    new Group(api, groupId),
                    new QQ(api, qq),
                    message),
                CancellationToken = CancellationToken.None,
                IsMockMode = true,
            };
        }
        else
        {
            ctx = new ChatContext
            {
                MessageText = text ?? "",
                PrivateCtx = new PrivateMessageContext(api,
                    new QQ(api, qq),
                    message),
                CancellationToken = CancellationToken.None,
                IsMockMode = true,
            };
        }

        // Use the shared in-memory mock history
        ctx.MockMessages = _mockHistory;
        _activeIdentity = GetConversationIdentity(ctx);

        // Capture pipeline response via SendFunc
        ctx.SendFunc = async msg =>
        {
            await _uiDispatcher.InvokeAsync(() =>
            {
                if (_pendingIntermediate is { } pending
                    && pending.Identity == GetConversationIdentity(ctx)
                    && pending.Text == msg)
                {
                    _pendingIntermediate = null;
                    return; // already shown as an intermediate-text bubble
                }

                Messages.Add(new ChatBubbleItem
                {
                    Content = msg,
                    IsSelf = false,
                    Sender = "Bot",
                    Reasoning = TakePendingReasoning(GetConversationIdentity(ctx)) ?? ctx.Reasoning
                });
            }, DispatcherPriority.Send);
            await Task.CompletedTask;
        };

        // ── Typing indicator ──
        var typingBubble = new ChatBubbleItem { Content = "Bot 正在输入...", IsSelf = false, Sender = "System" };
        await _uiDispatcher.InvokeAsync(() => Messages.Add(typingBubble));

        try
        {
            var pipeline = ctx.IsGroup ? TestGroupPipeline : TestPrivatePipeline;
            await Task.Run(() => pipeline(ctx));

            await _uiDispatcher.InvokeAsync(() => Messages.Remove(typingBubble));

            // Update user bubble with the pipeline-processed text (image descriptions etc.)
            if (!string.IsNullOrWhiteSpace(ctx.MessageText) && ctx.MessageText != userBubble.Content)
            {
                await _uiDispatcher.InvokeAsync(() => userBubble.Content = ctx.MessageText);
            }

            if (ctx.Result == EventHandleResult.Pass)
            {
                await _uiDispatcher.InvokeAsync(() =>
                {
                    Messages.Add(new ChatBubbleItem
                    {
                        Content = BuildPipelineTraceMessage(ctx),
                        IsSelf = false,
                        Sender = "System",
                        Reasoning = ctx.Reasoning
                    });
                });
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"失败: {ex.Message}";
            await _uiDispatcher.InvokeAsync(() => Messages.Remove(typingBubble));
        }
        finally
        {
            IsSending = false;
        }
    }
}
