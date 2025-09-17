using me.cqp.luohuaming.ChatGPT.UI.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace me.cqp.luohuaming.ChatGPT.UI.Pages
{
    /// <summary>
    /// MCP.xaml 的交互逻辑
    /// </summary>
    public partial class MCP : Page
    {
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

        private void RebuildMCPButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void AddMCPButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void MCPClientContainer_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            ViewModel.SelectedMCPItem = [e.NewValue];
        }
    }
}
