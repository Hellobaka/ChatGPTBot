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

    private readonly Dictionary<AutoCompleteTextBox, DateTime> _autoCompleteSuppressUntil = [];

    public ChatTestView()
    {
        InitializeComponent();
        DataContext = new ChatTestViewModel();
        Loaded += (_, _) => ScrollChatToEnd();
        VM.Messages.CollectionChanged += (_, _) =>
        {
            Dispatcher.BeginInvoke(ScrollChatToEnd, DispatcherPriority.Background);
        };
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

    private void OnAutoCompletePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not AutoCompleteTextBox autoComplete)
        {
            return;
        }

        if (!autoComplete.IsKeyboardFocusWithin)
        {
            autoComplete.Focus();
        }

        OpenAutoComplete(autoComplete);
    }

    private void OnAutoCompleteDropDownClosed(object sender, EventArgs e)
    {
        if (sender is AutoCompleteTextBox autoComplete)
        {
            _autoCompleteSuppressUntil[autoComplete] = DateTime.UtcNow.AddMilliseconds(250);
        }
    }

    private void OpenAutoComplete(object sender)
    {
        if (sender is AutoCompleteTextBox autoComplete && autoComplete.Items.Count > 0)
        {
            if (_autoCompleteSuppressUntil.TryGetValue(autoComplete, out var until)
                && DateTime.UtcNow < until)
            {
                return;
            }

            Dispatcher.BeginInvoke(() =>
            {
                autoComplete.IsDropDownOpen = true;
            }, DispatcherPriority.Input);
        }
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
    }

    private void ScrollChatToEnd()
    {
        ChatScroll.ScrollToEnd();
    }
}