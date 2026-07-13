using ChatGPTv3.UI.ViewModels;
using System.Windows.Controls;

namespace ChatGPTv3.UI.Views;

public partial class MCPManagementView : UserControl
{
    public MCPManagementView()
    {
        InitializeComponent();
        DataContext = new MCPManagementViewModel();
    }
}
