using me.cqp.luohuaming.ChatGPT.UI.Model;
using me.cqp.luohuaming.ChatGPT.UI.ViewModel;
using System.Collections.Generic;
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
            DependencyProperty.Register("MCPClientModel", typeof(MCPClientModel), typeof(MCPClientEdit), new PropertyMetadata(default, OnMCPClientSet));

        private static void OnMCPClientSet(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MCPClientEdit control && e.NewValue is MCPClientModel model)
            {
                control.ViewModel = new MCPClientEditViewModel(model);
            }
        }

        public MCPClientModel GetResult()
        {
            if (ViewModel.IsReadOnly)
            {
                MainWindow.ShowInfo("内置工具无法修改保存");
                return null;
            }
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
        }

        private void AutoCreateToolNameConverterButton_Click(object sender, RoutedEventArgs e)
        {
            var tools = MCPClientModel.Tools;
            var converters = new Dictionary<string, string>();
            for (int i = 0; i < tools.Count; i++)
            {
                converters.Add(tools[i].Name, $"tool_{i + 1}");
            }
            ViewModel.ToolNameConverter = converters;
            //ToolNameConverterEditor.ItemSource = ViewModel.ToolNameConverter;
        }
    }
}
