using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using me.cqp.luohuaming.ChatGPT.UI.Controls;
using me.cqp.luohuaming.ChatGPT.UI.Model;
using me.cqp.luohuaming.ChatGPT.UI.ViewModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace me.cqp.luohuaming.ChatGPT.UI.Pages
{
    /// <summary>
    /// MCP.xaml 的交互逻辑
    /// </summary>
    public partial class MCP : Page
    {
        // TODO: 通过对话框新建的无法保存
        // TODO: 不勾选显示内置工具保存时，内置工具会丢失设置
        // TODO: 内置工具提供一套默认启用

        public MCP()
        {
            InitializeComponent();
            ViewModel = new MCPViewModel();
        }

        public MCPViewModel ViewModel
        {
            get { return DataContext as MCPViewModel; }
            set { DataContext = value; }
        }

        public bool Rebuilt { get; set; }

        public bool HasChanged { get; set; }

        private async void RebuildMCPButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Rebuilding = true;
            await Task.Run(MCPClientManager.Rebuild);
            ViewModel.LoadMCPClients(ShowCustomTools.IsChecked ?? false);
            ViewModel.Rebuilding = false;
            Rebuilt = true;
        }

        private async void AddMCPButton_Click(object sender, RoutedEventArgs e)
        {
            MCPCreateClient dialog = new();
            _ = await dialog.ShowAsync();
            if (dialog.DialogResult == ModernWpf.Controls.ContentDialogResult.Primary)
            {
                ViewModel.MCPClients.Add(dialog.MCPClient);
            }
        }

        private void MCPClientContainer_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            ViewModel.SelectedMCPItem = e.NewValue;
            var item = ViewModel.SelectedMCPItem;
            if (item != null)
            {
                if (item is MCPClientModel clientModel)
                {
                    MCPClientEditControl.MCPClientModel = clientModel;
                    MCPClientEditControl.Visibility = Visibility.Visible;
                    MCPToolTestControl.Visibility = Visibility.Collapsed;
                    ActionContainer.Visibility = Visibility.Visible;
                }
                else if (item is MCPToolModel toolModel)
                {
                    MCPToolTestControl.MCPToolModel = toolModel;
                    MCPClientEditControl.Visibility = Visibility.Collapsed;
                    MCPToolTestControl.Visibility = Visibility.Visible;
                    ActionContainer.Visibility = Visibility.Collapsed;
                }
                else
                {
                    MCPClientEditControl.Visibility = Visibility.Collapsed;
                    MCPToolTestControl.Visibility = Visibility.Collapsed;
                    ActionContainer.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                MCPClientEditControl.Visibility = Visibility.Collapsed;
                MCPToolTestControl.Visibility = Visibility.Collapsed;
                ActionContainer.Visibility = Visibility.Collapsed;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            MCPClientEditControl.Cancel();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MCPClientEditControl.GetResult();
            if (result == null)
            {
                return;
            }
            var index = ViewModel.MCPClients.IndexOf(ViewModel.SelectedMCPItem as MCPClientModel);
            if (index >= 0)
            {
                ViewModel.MCPClients[index].MCPClientBase = result.MCPClientBase;
                MCPClientManager.Clients = ViewModel.MCPClients
                    .Where(x => x.MCPClientBase != null)
                    .Select(x => x.MCPClientBase).ToList();
                MCPClientManager.Save();
                Rebuilt = false;
                HasChanged = true;
                MainWindow.ShowInfo("保存成功");
                ViewModel.LoadMCPClients(ShowCustomTools.IsChecked ?? false);
            }
            else
            {
                MainWindow.ShowError("未找到选中项对应的元素，无法保存");
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.LoadMCPClients(ShowCustomTools.IsChecked ?? false);
            ViewModel.Rebuilding = false;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedMCPItem != null && ViewModel.SelectedMCPItem is MCPClientModel clientModel)
            {
                if (clientModel.MCPClientBase.ToolType == MCPClientType.Custom)
                {
                    MainWindow.ShowError("自定义工具无法删除，请通过配置来禁用此工具");
                    return;
                }
                if (MainWindow.ShowConfirm($"确认要删除客户端 {clientModel.Name} 吗？"))
                {
                    MCPClientManager.Clients.Remove(clientModel.MCPClientBase);
                    ViewModel.MCPClients.Remove(clientModel);
                    MCPClientManager.Save();
                }
            }
        }

        private void ShowCustomTools_Checked(object sender, RoutedEventArgs e)
        {
            ViewModel?.LoadMCPClients(ShowCustomTools.IsChecked ?? false);
        }
    }
}
