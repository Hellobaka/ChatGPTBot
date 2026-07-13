using ChatGPTv3.UI.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace ChatGPTv3.UI.Views;

public partial class KeyEditDialog : HandyControl.Controls.Window
{
    private readonly KeyEditDialogViewModel _vm;

    public KeyEditDialog(KeyEditDialogViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        // Close the window when Save or Cancel is triggered
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(KeyEditDialogViewModel.IsSaved))
            {
                DialogResult = vm.IsSaved;
                Close();
            }
        };
    }
}
