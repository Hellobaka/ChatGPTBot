using System.Collections.ObjectModel;
using System.Text.Json;
using Another_Mirai_Native.Abstractions.Context;
using Another_Mirai_Native.Abstractions.Enums;
using Another_Mirai_Native.Abstractions.Models;
using Another_Mirai_Native.Abstractions.Models.MessageItem;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using ChatGPTv3.Core.Commands;
using ChatGPTv3.Core.DB;

namespace ChatGPTv3.UI.ViewModels;

public partial class ChatBubbleItem : ObservableObject
{
    public string Content { get; init; } = "";
    public bool IsSelf { get; init; }
    public string Sender { get; init; } = "";
}

public partial class ChatTestViewModel : ViewModelBase
{
    private readonly List<string> _sendHistory = [];
    private int _historyIndex = -1;
    private string? _historyDraft;

    // Full pipelines for testing (same as production)
    private static readonly Func<ChatContext, Task> TestGroupPipeline = new ChatPipelineBuilder()
        .UseAccessControl()
        .UseMessageFilter()
        .UseConcurrencyGate()
        .UseReplyDecision()
        .UseMessageImageResolver()
        .UseMessageReferenceResolver()
        .UseMessageRecorder()
        .UseChatHandler()
        .Build();

    private static readonly Func<ChatContext, Task> TestPrivatePipeline = new ChatPipelineBuilder()
        .UsePrivateAccessControl()
        .UseMessageFilter()
        .UsePrivateReplyDecision()
        .UseMessageImageResolver()
        .UseMessageReferenceResolver()
        .UseMessageRecorder()
        .UseChatHandler()
        .Build();

    public ObservableCollection<ChatBubbleItem> Messages { get; } = [];

    [ObservableProperty]
    private string _messageText = "";

    [ObservableProperty]
    private string _groupId = "";

    private string _qqSource = "";
    public string QQ
    {
        get => _qqSource;
        set
        {
            if (SetProperty(ref _qqSource, value))
                SaveSettings();
        }
    }

    [ObservableProperty]
    private bool _isSending;

    private static string SettingsPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_chat_settings.json");

    public ChatTestViewModel()
    {
        LoadSettings();
        LoadMessagesFromDb();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (settings != null)
                {
                    if (settings.TryGetValue("GroupId", out var gid)) GroupId = gid;
                    if (settings.TryGetValue("QQ", out var qq)) QQ = qq;
                }
            }
        }
        catch { /* ignore */ }
    }

    private void SaveSettings()
    {
        try
        {
            var settings = new Dictionary<string, string> { ["GroupId"] = GroupId, ["QQ"] = QQ };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings));
        }
        catch { /* ignore */ }
    }

    partial void OnGroupIdChanged(string value) => SaveSettings();

    [RelayCommand]
    private void RefreshHistory()
    {
        Messages.Clear();
        LoadMessagesFromDb();
    }

    /// <summary>Load chat history from DB to display multi-turn context.</summary>
    private void LoadMessagesFromDb()
    {
        if (!long.TryParse(QQ?.Trim(), out var qq) || qq <= 0) return;
        var isGroup = long.TryParse(GroupId?.Trim(), out var groupId) && groupId > 0;

        List<ChatRecord> records;
        if (isGroup)
            records = ChatRecord.GetGroupHistory(groupId, 50);
        else
            records = ChatRecord.GetPrivateHistory(qq, 50);

        var botQQ = ChatGPTv3.Core.Api.PromptBuilder.CurrentBotQQ;
        foreach (var r in records)
        {
            var isBot = r.QQ == botQQ || r.SenderType == SenderType.Assistant;
            Messages.Add(new ChatBubbleItem
            {
                Content = r.ParsedMessage,
                IsSelf = !isBot,
                Sender = isBot ? "Bot" : $"{r.QQ}"
            });
        }
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        var text = MessageText?.Trim();
        if (string.IsNullOrWhiteSpace(text) || IsSending) return;

        if (!long.TryParse(QQ?.Trim(), out var qq) || qq <= 0)
        {
            ErrorMessage = "请输入有效的来源QQ";
            return;
        }
        var isGroup = long.TryParse(GroupId?.Trim(), out var groupId) && groupId > 0;

        _sendHistory.Add(text);
        _historyIndex = -1;
        _historyDraft = null;

        Messages.Add(new ChatBubbleItem { Content = text, IsSelf = true, Sender = $"QQ: {qq}" });
        MessageText = "";
        IsSending = true;
        ErrorMessage = null;

        // ── Mock AMN2 Message with text in MessageChain ──
        var message = new Message(null!, Random.Shared.Next() * -1, text);
        message.MessageChain.Add(new Text(text));

        // ── Build context with mocked AMN2 objects ──
        ChatContext ctx;
        if (isGroup)
        {
            ctx = new ChatContext
            {
                MessageText = text,
                GroupCtx = new GroupMessageContext(null!,
                    new Group(null!, groupId),
                    new QQ(null!, qq),
                    message),
                CancellationToken = CancellationToken.None,
            };
        }
        else
        {
            ctx = new ChatContext
            {
                MessageText = text,
                PrivateCtx = new PrivateMessageContext(null!,
                    new QQ(null!, qq),
                    message),
                CancellationToken = CancellationToken.None,
            };
        }

        // Capture pipeline response via SendFunc
        var tcs = new TaskCompletionSource<string?>();
        ctx.SendFunc = async msg =>
        {
            tcs.TrySetResult(msg);
            await Dispatcher.CurrentDispatcher.InvokeAsync(() =>
            {
                Messages.Add(new ChatBubbleItem { Content = msg, IsSelf = false, Sender = "Bot" });
            });
            await Task.CompletedTask;
        };

        try
        {
            var pipeline = ctx.IsGroup ? TestGroupPipeline : TestPrivatePipeline;
            await pipeline(ctx);

            if (ctx.Result == EventHandleResult.Pass)
            {
                Messages.Add(new ChatBubbleItem { Content = "消息未处理", IsSelf = false, Sender = "Bot" });
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"失败: {ex.Message}";
        }
        finally
        {
            IsSending = false;
        }
    }

    public void HistoryUp()
    {
        if (_sendHistory.Count == 0) return;
        if (_historyIndex == -1) { _historyDraft = MessageText; _historyIndex = _sendHistory.Count - 1; }
        else if (_historyIndex > 0) _historyIndex--;
        MessageText = _sendHistory[_historyIndex];
    }

    public void HistoryDown()
    {
        if (_historyIndex == -1) return;
        if (_historyIndex < _sendHistory.Count - 1) { _historyIndex++; MessageText = _sendHistory[_historyIndex]; }
        else { _historyIndex = -1; MessageText = _historyDraft ?? ""; _historyDraft = null; }
    }
}
