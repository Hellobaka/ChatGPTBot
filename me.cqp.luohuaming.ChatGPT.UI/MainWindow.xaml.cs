using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using me.cqp.luohuaming.ChatGPT.UI.Pages;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace me.cqp.luohuaming.ChatGPT.UI
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Topmost = true;
            Instance = this;
        }

        public static event Action OnWindowClosing;

        public static MainWindow Instance { get; private set; }

        private string CurrentPage { get; set; } = string.Empty;

        private Dictionary<string, object> PageCache { get; set; } = new();

        public static void ShowError(string message)
        {
            Instance.Dispatcher.Invoke(() => MessageBox.Show(Instance, message, "错误", MessageBoxButton.OK, MessageBoxImage.Error));
        }

        public static void ShowInfo(string message)
        {
            Instance.Dispatcher.Invoke(() => MessageBox.Show(Instance, message, "提示", MessageBoxButton.OK, MessageBoxImage.Information));
        }

        public static bool ShowConfirm(string message)
        {
            return MessageBox.Show(Instance, message, "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        public static async Task<bool> ShowConfirmDialog(string title, string message)
        {
            ContentDialog dialog = new()
            {
                Title = title,
                Content = message,
                DefaultButton = ContentDialogButton.Primary,
                PrimaryButtonText = "确认",
                SecondaryButtonText = "取消",
            };
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (App.Debug)
            {
                MainSave.AppDirectory = AppDomain.CurrentDomain.BaseDirectory;
                MainSave.RecordDirectory = AppDomain.CurrentDomain.BaseDirectory;
                MainSave.CQApi = new(new Sdk.Cqp.Model.AppInfo("", 0, 0, "", "1.0.0", 0, "", "", 1));
                MainSave.ImageDirectory = @"D:\Code\Another-Mirai-Native2\Another-Mirai-Native\bin\x86\Debug\net8.0-windows\data\image";
                ConfigHelper.ConfigFileName = Path.Combine(MainSave.AppDirectory, "Config.json");
                if (ConfigHelper.Load() is false)
                {
                    ShowError("加载配置文件, 内容格式不正确，无法加载");
                }
                AppConfig.Init();
                SQLHelper.CreateDB();
                Picture.InitCache();
                _ = new MoodManager();
                _ = new SchedulerManager();
                Qdrant qdrant = new(AppConfig.QdrantHost, AppConfig.QdrantPort);
                MCPClientManager.Load();
                if (!qdrant.GetCollections())
                {
                    ShowError("Qdrant Connection Failed.");
                }
                else
                {
                    qdrant.CreateCollection();
                }
            }

            Topmost = false;
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var selectedItem = (NavigationViewItem)args.SelectedItem;
            if (selectedItem != null)
            {
                string selectedItemTag = (string)selectedItem.Tag;
                if (selectedItemTag != "MCP" && CurrentPage == "MCP"
                    && PageCache.TryGetValue("MCP", out var mcpPage) && mcpPage is MCP mcp
                    && mcp.HasChanged && !mcp.Rebuilt)
                {
                    ShowInfo("MCP 配置已保存但未重建工具列表，请在 MCP 页重建工具列表");
                    mcp.HasChanged = false;
                }
                if (PageCache.TryGetValue(selectedItemTag, out object? page))
                {
                    CurrentPage = selectedItemTag;
                    MainFrame.Navigate(page);
                }
                else
                {
                    Type? pageType = typeof(MainWindow).Assembly.GetType("me.cqp.luohuaming.ChatGPT.UI.Pages." + selectedItemTag);
                    if (pageType == null)
                    {
                        return;
                    }
                    var obj = Activator.CreateInstance(pageType);
                    if (obj == null)
                    {
                        return;
                    }
                    PageCache.Add(selectedItemTag, obj);
                    CurrentPage = selectedItemTag;
                    MainFrame.Navigate(obj);
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            OnWindowClosing?.Invoke();
        }
    }
}
