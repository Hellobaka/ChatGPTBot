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
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Regex = System.Text.RegularExpressions.Regex;

namespace ChatGPTv3.UI.ViewModels;

public partial class ChatBubbleItem : ObservableObject
{
    [ObservableProperty]
    private string _content = "";

    public bool IsSelf { get; init; }

    public string Sender { get; init; } = "";

    public DateTime Time { get; init; } = DateTime.Now;

    public string? Reasoning { get; init; }

    public bool HasReasoning => !string.IsNullOrWhiteSpace(Reasoning);
}

/// <summary>Candidate item for group selection (name + id display).</summary>
public partial class GroupItem : ObservableObject
{
    public long Id { get; init; }

    public string Name { get; init; } = "";

    public string Display => string.IsNullOrEmpty(Name) ? Id.ToString() : $"{Name} ({Id})";
}

/// <summary>Candidate item for QQ selection (friend or group member).</summary>
public partial class FriendItem : ObservableObject
{
    public long Id { get; init; }

    public string Name { get; init; } = "";

    public string Display => string.IsNullOrEmpty(Name) ? Id.ToString() : $"{Name} ({Id})";
}

/// <summary>A pending image to send (local source path, computed hash, preview).</summary>
public partial class PendingImage : ObservableObject
{
    public string SourcePath { get; init; } = "";

    public string Hash { get; init; } = "";

    public BitmapImage? Preview { get; init; }

    public bool IsEmoji { get; init; }
}

public partial class ChatTestViewModel : ViewModelBase
{
    private readonly List<string> _sendHistory = [];
    private readonly List<GroupItem> _allGroupCandidates = [];
    private readonly List<FriendItem> _allQQCandidates = [];
    private int _historyIndex = -1;
    private string? _historyDraft;
    private readonly Dispatcher _uiDispatcher;

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
        .Build();

    public ObservableCollection<ChatBubbleItem> Messages { get; } = [];

    // ── AutoComplete candidates (dynamic) ──────────────────
    public ObservableCollection<GroupItem> GroupCandidates { get; } = [];

    public ObservableCollection<FriendItem> QQCandidates { get; } = [];

    [ObservableProperty]
    private string _groupSearchText = string.Empty;

    [ObservableProperty]
    private string _qqSearchText = string.Empty;

    [ObservableProperty]
    private GroupItem? _selectedGroup;

    [ObservableProperty]
    private FriendItem? _selectedQQ;

    [ObservableProperty]
    private bool _isPrivateMode;

    // ── Images ─────────────────────────────────────────────
    public ObservableCollection<PendingImage> PendingImages { get; } = [];

    public bool HasPendingImages => PendingImages.Count > 0;

    [ObservableProperty]
    private string _messageText = "";

    [ObservableProperty]
    private bool _isSending;

    public ChatTestViewModel()
    {
        _uiDispatcher = System.Windows.Application.Current?.Dispatcher
                        ?? Dispatcher.CurrentDispatcher;
        LoadMessagesFromDb();
        LoadGroupCandidates();
        ReloadQQCandidates();
    }

    partial void OnIsPrivateModeChanged(bool value)
    {
        SelectedQQ = null;
        ReloadQQCandidates();
        AutoSelectDefaultQQ();
        RefreshHistory();
    }

    partial void OnSelectedGroupChanged(GroupItem? value)
    {
        SelectedQQ = null;
        if (!IsPrivateMode)
        {
            ReloadQQCandidates();
            AutoSelectDefaultQQ();
            RefreshHistory();
        }
    }

    partial void OnSelectedQQChanged(FriendItem? value)
    {
        RefreshHistory();
    }

    partial void OnGroupSearchTextChanged(string value)
    {
        FilterGroupCandidates(value);
    }

    partial void OnQqSearchTextChanged(string value)
    {
        FilterQQCandidates(value);
    }

    // ═══════════════════════════════════════════════════════════
    //  Candidate loading
    // ═══════════════════════════════════════════════════════════

    private void LoadGroupCandidates()
    {
        GroupCandidates.Clear();
        _allGroupCandidates.Clear();
        try
        {
            var groups = Entry.ApiGroup?.GetGroupList() ?? MockGroupApi.Instance.GetGroupList();
            foreach (var g in groups)
            {
                _allGroupCandidates.Add(new GroupItem { Id = g.Group, Name = g.Name ?? "" });
            }

            FilterGroupCandidates(GroupSearchText);

            if (!IsPrivateMode && SelectedGroup == null && GroupCandidates.Count > 0)
            {
                SelectedGroup = GroupCandidates[0];
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载群列表失败: {ex.Message}";
        }
    }

    /// <summary>
    /// Reload QQ candidates based on current mode:
    /// private → friend list, group → members of selected group.
    /// </summary>
    private void ReloadQQCandidates()
    {
        QQCandidates.Clear();
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

            FilterQQCandidates(QqSearchText);
            AutoSelectDefaultQQ();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载QQ列表失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RefreshQQCandidates()
    {
        ReloadQQCandidates();
        RefreshHistory();
    }

    [RelayCommand]
    private void RefreshHistory()
    {
        LoadMessagesFromDb();
    }

    private void LoadMessagesFromDb()
    {
        Messages.Clear();

        // Load recent history for whichever context is currently selected.
        long qq = SelectedQQ?.Id ?? 0;
        long groupId = SelectedGroup?.Id ?? 0;
        if (qq <= 0)
        {
            return;
        }

        List<ChatRecord> records;
        if (!IsPrivateMode && groupId > 0)
        {
            records = ChatRecord.GetGroupHistory(groupId, 50);
        }
        else
        {
            records = ChatRecord.GetPrivateHistory(qq, 50);
        }

        var botQQ = PromptBuilder.CurrentBotQQ;
        foreach (var r in records)
        {
            var isBot = r.QQ == botQQ || r.SenderType == SenderType.Assistant;
            Messages.Add(new ChatBubbleItem
            {
                Content = r.ParsedMessage,
                IsSelf = !isBot,
                Sender = isBot ? "Bot" : $"{r.QQ}",
                Time = r.Time
            });
        }
    }

    private void AutoSelectDefaultQQ()
    {
        if (SelectedQQ == null && QQCandidates.Count > 0)
        {
            SelectedQQ = QQCandidates[0];
        }
    }

    private void FilterGroupCandidates(string? keyword)
    {
        ReplaceCollection(GroupCandidates, FilterItems(_allGroupCandidates, keyword, item =>
            item.Display.Contains(keyword ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            || item.Id.ToString().Contains(keyword ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            || item.Name.Contains(keyword ?? string.Empty, StringComparison.OrdinalIgnoreCase)));

        if (SelectedGroup != null && !GroupCandidates.Any(x => x.Id == SelectedGroup.Id))
        {
            SelectedGroup = GroupCandidates.FirstOrDefault();
        }
    }

    private void FilterQQCandidates(string? keyword)
    {
        ReplaceCollection(QQCandidates, FilterItems(_allQQCandidates, keyword, item =>
            item.Display.Contains(keyword ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            || item.Id.ToString().Contains(keyword ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            || item.Name.Contains(keyword ?? string.Empty, StringComparison.OrdinalIgnoreCase)));

        if (SelectedQQ != null && !QQCandidates.Any(x => x.Id == SelectedQQ.Id))
        {
            SelectedQQ = QQCandidates.FirstOrDefault();
        }
    }

    private static IEnumerable<T> FilterItems<T>(IEnumerable<T> source, string? keyword, Func<T, bool> predicate)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return source;
        }

        return source.Where(predicate);
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Image handling (does NOT insert Picture table — middleware does)
    // ═══════════════════════════════════════════════════════════

    [RelayCommand]
    private void AddImage()
    {
        AddFiles(isEmoji: false);
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

            var hash = ComputeMD5(path);
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
    private void RemoveImage(PendingImage? image)
    {
        if (image == null)
        {
            return;
        }

        PendingImages.Remove(image);
        OnPropertyChanged(nameof(HasPendingImages));
    }

    [RelayCommand]
    private void ClearImages()
    {
        PendingImages.Clear();
        OnPropertyChanged(nameof(HasPendingImages));
    }

    /// <summary>
    /// Copy pending images into the framework image directory and build Image message items.
    /// The middleware (UseMessageImageResolver → ImageScraper) handles description + Picture caching.
    /// </summary>
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

    private static string ComputeMD5(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hash = md5.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
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

    private static readonly Regex AtRegex = new(@"\[CQ:at,qq=(?<qq>\d+)\]",
        System.Text.RegularExpressions.RegexOptions.Compiled |
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

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

        var qq = SelectedQQ?.Id ?? 0;
        if (qq <= 0)
        {
            ErrorMessage = "请选择来源QQ";
            return;
        }
        var isGroup = !IsPrivateMode && SelectedGroup != null;
        var groupId = isGroup ? SelectedGroup!.Id : 0;
        if (!string.IsNullOrWhiteSpace(text))
        {
            _sendHistory.Add(text);
            _historyIndex = -1;
            _historyDraft = null;
        }

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
            };
        }

        // Capture pipeline response via SendFunc
        ctx.SendFunc = async msg =>
        {
            await _uiDispatcher.InvokeAsync(() =>
            {
                Messages.Add(new ChatBubbleItem
                {
                    Content = msg,
                    IsSelf = false,
                    Sender = "Bot",
                    Reasoning = ctx.Reasoning
                });
            });
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

    public void HistoryUp()
    {
        if (_sendHistory.Count == 0)
        {
            return;
        }

        if (_historyIndex == -1) { _historyDraft = MessageText; _historyIndex = _sendHistory.Count - 1; }
        else if (_historyIndex > 0)
        {
            _historyIndex--;
        }

        MessageText = _sendHistory[_historyIndex];
    }

    public void HistoryDown()
    {
        if (_historyIndex == -1)
        {
            return;
        }

        if (_historyIndex < _sendHistory.Count - 1) { _historyIndex++; MessageText = _sendHistory[_historyIndex]; }
        else { _historyIndex = -1; MessageText = _historyDraft ?? ""; _historyDraft = null; }
    }
}