using me.cqp.luohuaming.ChatGPT.UI.Model;
using me.cqp.luohuaming.ChatGPT.UI.ViewModel;
using System.Windows;
using System.Windows.Controls;

namespace me.cqp.luohuaming.ChatGPT.UI.Controls
{
    /// <summary>
    /// MCPClientEdit.xaml 的交互逻辑
    /// </summary>
    public partial class MCPClientEdit : UserControl
    {
        public MCPClientEdit()
        {
            InitializeComponent();
        }

        public MCPClientEditViewModel ViewModel
        {
            get { return DataContext as MCPClientEditViewModel; }
            set { DataContext = value; }
        }

        public MCPClientModel MCPClientModel
        {
            get { return (MCPClientModel)GetValue(MCPClientModelProperty); }
            set { SetValue(MCPClientModelProperty, value); }
        }

        public static readonly DependencyProperty MCPClientModelProperty =
            DependencyProperty.Register("MCPClientModel", typeof(MCPClientModel), typeof(MCPClientEdit), new PropertyMetadata(default));

        public MCPClientModel GetResult()
        {
            return ViewModel.Build();
        }

        public void Cancel()
        {
            ViewModel = new MCPClientEditViewModel(MCPClientModel);
        }

        private void MCPClientTypeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            STDIOSettings.Visibility = ViewModel.ToolType == "STDIO" ? Visibility.Visible : Visibility.Collapsed;
            HttpSettings.Visibility = ViewModel.ToolType == "Http" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel = new MCPClientEditViewModel(MCPClientModel);
        }
    }
}
