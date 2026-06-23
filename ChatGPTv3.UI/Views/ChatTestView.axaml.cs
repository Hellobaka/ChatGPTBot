using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
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

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        var vm = VM;
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            vm.SendCommand.Execute(null);
        }
        else if (e.Key == Key.Up)
        {
            e.Handled = true;
            vm.HistoryUp();
        }
        else if (e.Key == Key.Down)
        {
            e.Handled = true;
            vm.HistoryDown();
        }
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        VM.Messages.CollectionChanged += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
                ChatScroll.ScrollToEnd(), DispatcherPriority.Background);
        };
    }
}
