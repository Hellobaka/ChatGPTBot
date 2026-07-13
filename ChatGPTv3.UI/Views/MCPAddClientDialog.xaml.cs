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
}
