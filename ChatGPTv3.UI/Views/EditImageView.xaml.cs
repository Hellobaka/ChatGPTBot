using ChatGPTv3.Core.DB;
using ChatGPTv3.UI.ViewModels;
using System.Windows;

namespace ChatGPTv3.UI.Views;

public partial class EditImageView
{
    private readonly EditImageViewModel _vm;

    public EditImageView(Picture picture)
    {
        InitializeComponent();
        _vm = new EditImageViewModel(picture);
        DataContext = _vm;

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EditImageViewModel.IsSaved) && _vm.IsSaved)
            {
                DialogResult = true;
                Close();
            }
        };
    }
}
