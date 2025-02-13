using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

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
        }

        private ChatFlow ChatFlow { get; set; }

        public static void ShowError(string message)
        {
            MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static void ShowInfo(string message)
        {
            MessageBox.Show(message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static bool ShowConfirm(string message)
        {
            return MessageBox.Show(message, "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //MainSave.RecordDirectory = "";
            //ConfigHelper.ConfigFileName = "Config.json";
            //ConfigHelper.Load();
            //AppConfig.Init();

            RefreshTTSStatus();
            ChatFlow = new ChatFlow()
            { 
                IsGroup = true,
            };
            ChatFlow.Init();
            AddChatBlock(CommonHelper.TextTemplateParse(AppConfig.GroupPrompt, 0), false);

            ChatBubble.OnCopy += ChatBubble_OnCopy;
            ChatBubble.OnRetry += ChatBubble_OnRetry;
        }

        private async void ChatBubble_OnRetry(ChatBubble bubble)
        {
            int index = ChatContainer.Children.IndexOf(bubble);
            if (index == -1)
            {
                return;
            }
            if (!bubble.LeftAlign)
            {
                index++;
            }
            int count = ChatContainer.Children.Count;
            for (int i = index; i < count; i++)
            {
                ChatContainer.Children.RemoveAt(index);
                ChatFlow.Conversations.RemoveAt(index);
            }
            ChatStatus.Visibility = Visibility.Visible;
            ChatSendButton.IsEnabled = false;
            ChatResetButton.IsEnabled = false;
            var response = await Task.Run(() => Chat.GetChatResult(ChatFlow));

            AddChatBlock(response, true);
            ChatScroller.ScrollToBottom();
            ChatFlow.Conversations.Add(new ChatFlow.ConversationItem
            {
                Role = "assistant",
                Content = response
            });
            ChatStatus.Visibility = Visibility.Hidden;
            ChatSendButton.IsEnabled = true;
            ChatResetButton.IsEnabled = true;
        }

        private void ChatBubble_OnCopy(string message)
        {
            Clipboard.SetText(message);
        }

        private void AddChatBlock(string msg, bool leftAlign)
        {
            ChatContainer.Children.Add(new ChatBubble(msg, leftAlign));
        }

        private void RefreshTTSStatus()
        {
            TTSStatus.Text = TTSHelper.Enabled ? "启用中" : "已禁用";
            TTSStatus.Foreground = TTSHelper.Enabled ? Brushes.Green : Brushes.Red;
        }

        private void SettingButton_Click(object sender, RoutedEventArgs e)
        {
            var form = new Settings();
            form.ShowDialog();
            form.Close();
        }

        private void TTSSwitchStatusButton_Click(object sender, RoutedEventArgs e)
        {
            TTSHelper.Enabled = !TTSHelper.Enabled;
            RefreshTTSStatus();
        }

        private async void TTSReinitButton_Click(object sender, RoutedEventArgs e)
        {
            if (AppConfig.EnableTTS is false)
            {
                ShowError("配置已禁用TTS，请启用后重新进行检查");
                return;
            }
            TestTTSStatus.Visibility = Visibility.Visible;
            var check = await Task.Run<bool>(() =>
            {
                TTSHelper.CheckTTS();
                return TTSHelper.Enabled;
            });
            RefreshTTSStatus();
            TestTTSStatus.Visibility = Visibility.Collapsed;
            ShowInfo($"TTS服务检查结果为：{check}");
        }

        private async void TTSTestButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TTSInput.Text))
            {
                ShowError("合成的文本不可为空");
                return;
            }
            TestTTSStatus.Visibility = Visibility.Visible;
            string dir = Path.Combine(MainSave.RecordDirectory, "ChatGPT-TTS");
            Directory.CreateDirectory(dir);
            string fileName = $"{DateTime.Now:yyyyMMddHHmmss}.mp3";
            string testText = TTSInput.Text;
            var ttsResult = await Task.Run<bool>(() =>
            {
                return TTSHelper.TTS(testText, Path.Combine(dir, fileName), AppConfig.TTSVoice);
            });
            TestTTSStatus.Visibility = Visibility.Collapsed;
            if (ttsResult)
            {
                if (ShowConfirm("TTS 成功，点击\"是\"打开音频"))
                {
                    Process.Start(Path.Combine(dir, fileName));
                }
            }
            else
            {
                ShowError("音频合成失败，查看日志排查问题");
            }
        }

        private async void ChatSendButton_Click(object sender, RoutedEventArgs e)
        {
            ChatStatus.Visibility = Visibility.Visible;
            ChatSendButton.IsEnabled = false;
            ChatResetButton.IsEnabled = false;
            ChatFlow.Conversations.Add(new ChatFlow.ConversationItem
            {
                Role = "user",
                Content = ChatTestInput.Text,
            });
            AddChatBlock(ChatTestInput.Text, false);
            ChatTestInput.Text = "";
            var response = await Task.Run(() => Chat.GetChatResult(ChatFlow));
            AddChatBlock(response, true);
            ChatScroller.ScrollToBottom();
            ChatFlow.Conversations.Add(new ChatFlow.ConversationItem
            {
                Role = "assistant",
                Content = response
            });
            ChatStatus.Visibility = Visibility.Hidden;
            ChatSendButton.IsEnabled = true;
            ChatResetButton.IsEnabled = true;
        }

        private void ChatTestInput_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                ChatSendButton_Click(sender, e);
            }
        }

        private void ChatResetButton_Click(object sender, RoutedEventArgs e)
        {
            ChatFlow.Conversations.Clear();
            ChatFlow.Init();
            ChatContainer.Children.Clear();
            AddChatBlock(CommonHelper.TextTemplateParse(AppConfig.GroupPrompt, 0), false);
        }
    }
}
