using me.cqp.luohuaming.ChatGPT.UI.ViewModel;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace me.cqp.luohuaming.ChatGPT.UI.Pages
{
    /// <summary>
    /// KeyManage.xaml 的交互逻辑
    /// </summary>
    public partial class KeyManage : Page
    {
        public KeyManage()
        {
            InitializeComponent();
            DataContext = ViewModel;
        }

        public KeyManageViewModel ViewModel { get; } = new KeyManageViewModel();

        private async void ApiKeyEdit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is PublicInfos.DB.APIKeys apiKey)
            {
                KeyEdit keyEdit = new(apiKey.Clone());
                await keyEdit.ShowAsync();
                if (keyEdit.DialogResult == ModernWpf.Controls.ContentDialogResult.Primary)
                {
                    int index = ViewModel.APIKeys.IndexOf(apiKey);
                    if (index >= 0)
                    {
                        ViewModel.APIKeys[index] = keyEdit.ApiKey;
                        keyEdit.ApiKey.Save();
                    }
                }
            }
        }

        private async void ApiKeyDelete_Click(object sender, RoutedEventArgs e)
        {
            if (await MainWindow.ShowConfirmDialog("删除确认", "确认删除该 API Key 吗？")
                && sender is Button button
                && button.DataContext is PublicInfos.DB.APIKeys apiKey)
            {
                ViewModel.APIKeys.Remove(apiKey);
                apiKey.Delete();
            }
        }

        private async void CreateKeyButton_Click(object sender, RoutedEventArgs e)
        {
            KeyEdit keyEdit = new(new PublicInfos.DB.APIKeys());
            await keyEdit.ShowAsync();
            if (keyEdit.DialogResult == ModernWpf.Controls.ContentDialogResult.Primary)
            {
                ViewModel.APIKeys.Add(keyEdit.ApiKey);
                keyEdit.ApiKey.Save();
            }
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.LoadKeys();
        }
    }
}
