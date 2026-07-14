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

    private void SharedTree_SelectedItemChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MCPManagementViewModel vm)
        {
            vm.SelectedServer = e.NewValue as MCPServerNode;
            vm.SelectedTool = e.NewValue as MCPToolLeaf;
        }
    }
}
