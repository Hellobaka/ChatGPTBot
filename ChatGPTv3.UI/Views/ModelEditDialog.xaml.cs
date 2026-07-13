using ChatGPTv3.UI.ViewModels;
using System.Windows;

namespace ChatGPTv3.UI.Views;

public partial class ModelEditDialog
{
    public ModelEditDialog(ModelEditDialogViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ModelEditDialogViewModel.IsSaved))
            {
                DialogResult = vm.IsSaved;
                Close();
            }
        };
    }
}
