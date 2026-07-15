using ChatGPTv3.UI.ViewModels;
using System.Windows;

namespace ChatGPTv3.UI.Views;

public partial class RebuildEmbeddingsView : HandyControl.Controls.Window
{
    private readonly RebuildEmbeddingsViewModel _vm;

    public RebuildEmbeddingsView()
    {
        _vm = new RebuildEmbeddingsViewModel();
        DataContext = _vm;
        InitializeComponent();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Prevent closing while running — user should click Cancel first
        if (_vm.IsRunning)
        {
            e.Cancel = true;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        if (_vm.IsRunning)
        {
            _vm.CancelCommand.Execute(null);
        }
        Close();
    }
}
