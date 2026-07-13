using ChatGPTv3.UI.ViewModels;

namespace ChatGPTv3.UI.Views;

public partial class MCPToolTestDialog
{
    public MCPToolTestDialog(string clientName, string toolName)
    {
        DataContext = new MCPToolTestViewModel(clientName, toolName);
        InitializeComponent();
    }
}
