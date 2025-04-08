using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.PublicInfos;
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
    /// Chat.xaml 的交互逻辑
    /// </summary>
    public partial class Chat : Page
    {
        public Chat()
        {
            InitializeComponent();
        }
        private void ChatBubble_OnRetry(ChatBubble bubble)
        {
        }

        private void ChatBubble_OnCopy(string message)
        {
            ChatContainer.Dispatcher.Invoke(() =>
            {
                try
                {
                    Clipboard.SetText(message);
                }
                catch
                {
                    MainWindow.ShowError("复制文本失败");
                }
            });
        }

        private void AddChatBlock(string msg, bool leftAlign, int pos = -1)
        {
            if (pos >= 0)
            {
                ChatContainer.Children.Insert(pos, new ChatBubble(msg, leftAlign));
            }
            else
            {
                ChatContainer.Children.Add(new ChatBubble(msg, leftAlign));
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ChatBubble.OnCopy += ChatBubble_OnCopy;
            ChatBubble.OnRetry += ChatBubble_OnRetry;
        }
        private void ChatSendButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private async Task ShowAssistantMessage(string response)
        {
            if (EnableSpliter.IsChecked ?? false)
            {
                var lines = await Task.Run(() => new Spliter(response).Split());
                foreach (var line in lines.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    if (AppConfig.EnableSpliterRandomDelay)
                    {
                        double typeSpeed = AppConfig.SpliterSimulateTypeSpeed / 60;
                        double typeTime = line.Length * typeSpeed;
                        int randomSleep = MainSave.Random.Next(AppConfig.SpliterRandomDelayMin, AppConfig.SpliterRandomDelayMax);
                        await Task.Delay(TimeSpan.FromMilliseconds(typeTime + randomSleep));
                    }
                    AddChatBlock(line, true);
                }
            }
            else
            {
                AddChatBlock(response, true);
            }
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
        }

        private void ChatPromptList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

    }
}
