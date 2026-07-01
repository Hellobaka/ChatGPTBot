using System.Windows.Controls;
using ChatGPTv3.UI.ViewModels;

namespace ChatGPTv3.UI.Views;

public partial class KeyManagementView : UserControl
{
    public KeyManagementView()
    {
        InitializeComponent();
        DataContext = new KeyManagementViewModel();
    }
}
