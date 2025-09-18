using me.cqp.luohuaming.ChatGPT.UI.Model;
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

namespace me.cqp.luohuaming.ChatGPT.UI.Controls
{
    /// <summary>
    /// MCPToolTest.xaml 的交互逻辑
    /// </summary>
    public partial class MCPToolTest : UserControl
    {
        public MCPToolTest()
        {
            InitializeComponent();
        }

        public MCPToolModel MCPToolModel
        {
            get { return (MCPToolModel)GetValue(MCPToolModelProperty); }
            set { SetValue(MCPToolModelProperty, value); }
        }

        public static readonly DependencyProperty MCPToolModelProperty =
            DependencyProperty.Register("MCPToolModel", typeof(MCPToolModel), typeof(MCPToolTest), new PropertyMetadata(default, OnMCPToolSet));

        private static void OnMCPToolSet(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MCPToolTest control && e.NewValue is MCPToolModel model)
            {
                control.ViewModel = new MCPToolTestViewModel(model);
            }
        }

        public MCPToolTestViewModel ViewModel
        {
            get { return DataContext as MCPToolTestViewModel; }
            set { DataContext = value; }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            //ViewModel = new MCPToolTestViewModel(MCPToolModel);
        }

        private void SendRequestButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
