using ChatGPTv3.UI.ViewModels;
using System.Windows.Controls;

namespace ChatGPTv3.UI.Views;

public partial class ImageManagementView : UserControl
{
    public ImageManagementView()
    {
        InitializeComponent();
        DataContext = new ImageManagementViewModel();
    }
}
