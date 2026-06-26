using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ChatGPTv3.UI.ViewModels;

namespace ChatGPTv3.UI.Views;

public partial class ChatTestView : UserControl
{
    private ChatTestViewModel VM => (ChatTestViewModel)DataContext!;

    public ChatTestView()
    {
        InitializeComponent();
        DataContext = new ChatTestViewModel();
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

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == DataContextProperty && DataContext is ChatTestViewModel vm)
        {
            vm.Messages.CollectionChanged += (_, _) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                    ChatScroll.ScrollToEnd()));
            };
        }
    }
}
