using ChatGPTv3.UI.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace ChatGPTv3.UI.Views;

public partial class BatchAddImageView
{
    private readonly BatchAddImageViewModel _vm;

    public BatchAddImageView()
    {
        InitializeComponent();
        _vm = new BatchAddImageViewModel();
        DataContext = _vm;

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BatchAddImageViewModel.IsSaved) && _vm.IsSaved)
            {
                DialogResult = _vm.IsSaved;
                Close();
            }
            else if (e.PropertyName == nameof(BatchAddImageViewModel.IsClosing))
            {
                DialogResult = false;
                Close();
            }
        };
    }

    private void SelectFiles_Click(object sender, RoutedEventArgs e)
    {
        _vm.SelectFilesCommand.Execute(null);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _vm.CancelCommand.Execute(null);
    }
}
