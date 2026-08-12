using ChatGPTv3.Core.Api;
using ChatGPTv3.UI.ViewModels;
using HandyControl.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ChatGPTv3.UI.Views;

public partial class ChatTestView : UserControl
{
    private ChatTestViewModel VM => (ChatTestViewModel)DataContext!;

    public ChatTestView()
    {
        InitializeComponent();
        DataContext = new ChatTestViewModel();
        ChatService.OnToolCallStarted += OnToolCallStarted;
        ChatService.OnRoundReasoning += OnRoundReasoning;
        ChatService.OnIntermediateText += OnIntermediateText;
        Unloaded += (_, _) => ChatService.OnToolCallStarted -= OnToolCallStarted;
        Unloaded += (_, _) => ChatService.OnRoundReasoning -= OnRoundReasoning;
        Unloaded += (_, _) => ChatService.OnIntermediateText -= OnIntermediateText;
        Loaded += (_, _) => ScrollChatToEnd();
        VM.Messages.CollectionChanged += (_, _) => ScrollAndLayout();
    }

    private void OnToolCallStarted(string identity, string toolName, string arguments)
    {
        Dispatcher.BeginInvoke(() => VM.AddToolCallBubble(identity, toolName, arguments), DispatcherPriority.Send);
    }

    private void OnRoundReasoning(string identity, string reasoning)
    {
        Dispatcher.BeginInvoke(() => VM.SetPendingReasoning(identity, reasoning), DispatcherPriority.Send);
    }

    private void OnIntermediateText(string identity, string text)
    {
        Dispatcher.BeginInvoke(() => VM.AddIntermediateTextBubble(identity, text), DispatcherPriority.Send);
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        var vm = VM;
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            vm.SendCommand.Execute(null);
        }
        else if (e.Key == Key.Up)
        {
            e.Handled = true;
            vm.HistoryUp();
            InputBox.CaretIndex = InputBox.Text.Length;
        }
        else if (e.Key == Key.Down)
        {
            e.Handled = true;
            vm.HistoryDown();
            InputBox.CaretIndex = InputBox.Text.Length;
        }
    }

    private void ScrollChatToEnd()
    {
        ChatScroll.ScrollToEnd();
    }

    /// <summary>
    /// Forces immediate layout and scroll after every bubble so each segment is
    /// visible before the next typing delay starts (no end-of-send batching).
    /// Runs on the UI thread because bubbles are added via the dispatcher.
    /// </summary>
    private void ScrollAndLayout()
    {
        ChatScroll.ScrollToEnd();
        ChatScroll.UpdateLayout();
    }
}
