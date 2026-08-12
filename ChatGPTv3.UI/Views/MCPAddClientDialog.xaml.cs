using ChatGPTv3.Core.Model.MCP;
using ChatGPTv3.UI.ViewModels;

namespace ChatGPTv3.UI.Views;

public partial class MCPAddClientDialog
{
    public MCPAddClientDialog()
    {
        var vm = new MCPAddClientViewModel();
        vm.RequestClose = () => Close();
        DataContext = vm;
        InitializeComponent();
    }

    public MCPAddClientDialog(MCPClientBase client)
    {
        var vm = new MCPAddClientViewModel(client);
        vm.RequestClose = () => Close();
        DataContext = vm;
        InitializeComponent();
    }
}
