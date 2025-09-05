using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using ModernWpf.Controls;
using OpenAI.Chat;
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
    /// KeyEdit.xaml 的交互逻辑
    /// </summary>
    public partial class KeyEdit
    {
        public KeyEdit(APIKeys apiKey)
        {
            InitializeComponent();
            ApiKey = apiKey;
            DataContext = apiKey;
        }

        public APIKeys ApiKey { get; }

        public bool Testing { get; set; }

        public ContentDialogResult DialogResult { get; set; }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = ContentDialogResult.Primary;
            Hide();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = ContentDialogResult.None;
            Hide();
        }

        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            if (Testing)
            {
                return;
            }
            Testing = true;
            TestButton.IsEnabled = false;
            TestingStatus.Visibility = Visibility.Visible;
            TestResultDisplay.Visibility = Visibility.Collapsed;
            string response = string.Empty;
            try
            {
                response = await Task.Run(() => Chat.GetChatResult(new APIKeyPurpose() { Key = ApiKey, ModelName = ApiKey.AvailableModels.FirstOrDefault() }, new List<ChatMessage>
                {
                    new UserChatMessage("Hello")
                }, Chat.Purpose.聊天));
            }
            catch 
            {
                response = Chat.ErrorMessage;
            }
            finally
            {
                Testing = false;
                TestButton.IsEnabled = true;
                TestingStatus.Visibility = Visibility.Collapsed;
            }
            if (response == Chat.ErrorMessage)
            {
                TestResultDisplay.Text = "测试失败，请检查接口配置或模型名称";
                TestResultDisplay.Foreground = Brushes.Red;
                TestResultDisplay.Visibility = Visibility.Visible;
            }
            else
            {
                TestResultDisplay.Text = "测试成功，此接口可用";
                TestResultDisplay.Foreground = Brushes.Green;
                TestResultDisplay.Visibility = Visibility.Visible;
            }
        }
    }
}
