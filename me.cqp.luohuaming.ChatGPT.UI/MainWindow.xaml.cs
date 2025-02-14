using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

        private string Prompt { get; set; } = AppConfig.GroupPrompt;

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
            if (App.Debug)
            {
                MainSave.AppDirectory = "";
                MainSave.RecordDirectory = "";
                ConfigHelper.ConfigFileName = "Config.json";
                ConfigHelper.Load();
                AppConfig.Init();
                BuildPromptList();
            }

            RefreshTTSStatus();
            ChatFlow = new ChatFlow()
            {
                IsGroup = true,
            };

            ChatBubble.OnCopy += ChatBubble_OnCopy;
            ChatBubble.OnRetry += ChatBubble_OnRetry;

            InitPromptList();
        }

        private void InitPromptList()
        {
            ChatPromptList.Items.Clear();
            PromptList.Items.Clear();

            ChatPromptList.Items.Add(new ComboBoxItem
            {
                Tag = AppConfig.GroupPrompt,
                Content = "群组 Prompt"
            });
            ChatPromptList.Items.Add(new ComboBoxItem
            {
                Tag = AppConfig.PrivatePrompt,
                Content = "私聊 Prompt"
            });

            PromptList.Items.Add(new ListBoxItem
            {
                Tag = AppConfig.GroupPrompt,
                Content = "群组 Prompt"
            });
            PromptList.Items.Add(new ListBoxItem
            {
                Tag = AppConfig.PrivatePrompt,
                Content = "私聊 Prompt"
            });

            foreach (var item in MainSave.Prompts)
            {
                ChatPromptList.Items.Add(new ComboBoxItem
                {
                    Tag = item.Value,
                    Content = item.Key
                });
                string prompt = File.ReadAllText(Path.Combine(MainSave.AppDirectory, item.Value));
                PromptList.Items.Add(new ListBoxItem
                {
                    Tag = prompt,
                    Content = item.Key
                });
            }

            ChatPromptList.SelectedIndex = 0;
            PromptList.SelectedIndex = 0;
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
            await ShowAssistantMessage(response);
            
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
            await ShowAssistantMessage(response);
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
            ChatFlow.Conversations.Clear();
            ChatContainer.Children.Clear();
            ChatPromptList_SelectionChanged(sender, null);
        }

        private void ChatPromptList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ChatPromptList.SelectedIndex < 0)
            {
                return;
            }
            if (ChatContainer.Children.Count > 0)
            {
                ChatContainer.Children.RemoveAt(0);
            }
            string prompt = "";
            string tag = (ChatPromptList.SelectedItem as ComboBoxItem).Tag.ToString();
            if (ChatPromptList.SelectedIndex > 1)
            {
                prompt = CommonHelper.TextTemplateParse(File.ReadAllText(Path.Combine(MainSave.AppDirectory, tag)), 0);
            }
            else
            {
                prompt = CommonHelper.TextTemplateParse(tag, 0);
            }
            AddChatBlock(prompt, false, 0);
            if (ChatFlow.Conversations.Count > 0)
            {
                ChatFlow.Conversations[0] = new ChatFlow.ConversationItem
                {
                    Role = "system",
                    Content = prompt
                };
            }
            else
            {
                ChatFlow.Conversations.Add(new ChatFlow.ConversationItem
                {
                    Role = "system",
                    Content = prompt
                });
            }
        }

        private void BuildPromptList()
        {
            MainSave.Prompts.Clear();
            string promptPath = Path.Combine(MainSave.AppDirectory, "Prompts");
            Directory.CreateDirectory(promptPath);
            foreach (var file in Directory.GetFiles(promptPath, "*.txt"))
            {
                MainSave.Prompts.Add(Path.GetFileNameWithoutExtension(file), file);
            }
        }

        private void PromptList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PromptList.SelectedIndex < 0)
            {
                return;
            }
            PromptRemoveButton.IsEnabled = PromptList.SelectedIndex > 1;

            string name = (PromptList.SelectedItem as ListBoxItem).Content.ToString();
            string prompt = (PromptList.SelectedItem as ListBoxItem).Tag.ToString();

            PromptPriview.Text = prompt;
            PromptName.Text = name;
            PromptName.IsEnabled = PromptList.SelectedIndex > 1;
        }

        private void PromptAddButton_Click(object sender, RoutedEventArgs e)
        {
            string name = "新建 Prompt";
            string content = AppConfig.PrivatePrompt;
            PromptList.Items.Add(new ListBoxItem
            {
                Tag = content,
                Content = name
            });
            PromptList.SelectedIndex = PromptList.Items.Count - 1;
        }

        private void PromptRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (PromptList.SelectedIndex <= 1)
            {
                return;
            }
            var item = PromptList.SelectedItem as ListBoxItem;

            if (ShowConfirm($"确认要删除 {item.Content} 预设吗？"))
            {
                int index = PromptList.SelectedIndex;
                PromptList.Items.RemoveAt(PromptList.SelectedIndex);
                index = Math.Max(0, index - 1);
                if (PromptList.Items.Count > index)
                {
                    PromptList.SelectedIndex = index;
                }
            }
        }

        private void PromptSaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (PromptList.Items.Count < 2)
            {
                ShowError("默认 Prompt 丢失，请重载插件以重启 UI");
                return;
            }
            string promptDir = Path.Combine(MainSave.AppDirectory, "Prompts");
            var invalid = Path.GetInvalidFileNameChars();
            List<string> files = new List<string>();
            for (int i = 2; i < PromptList.Items.Count; i++)
            {
                var item = PromptList.Items[i] as ListBoxItem;
                if (invalid.Any(item.Content.ToString().Contains))
                {
                    ShowError($"{item.Content} 含有无效文件名，无法保存");
                    return;
                }
                File.WriteAllText(Path.Combine(promptDir, item.Content.ToString() + ".txt"), item.Tag.ToString());
                files.Add(item.Content.ToString() + ".txt");
            }
            foreach (var file in Directory.GetFiles(promptDir, "*.txt"))
            {
                string name = Path.GetFileName(file);
                if (!files.Contains(name))
                {
                    File.Delete(file);
                }
            }
            AppConfig.GroupPrompt = (PromptList.Items[0] as ListBoxItem).Tag.ToString();
            AppConfig.PrivatePrompt = (PromptList.Items[1] as ListBoxItem).Tag.ToString();

            ConfigHelper.SetConfig("GroupPrompt", AppConfig.GroupPrompt);
            ConfigHelper.SetConfig("PrivatePrompt", AppConfig.PrivatePrompt);

            BuildPromptList();
            InitPromptList();

            ShowInfo("保存成功");
        }

        private void PromptAddVarible_Click(object sender, RoutedEventArgs e)
        {
            PromptPriview.AppendText((sender as Button).Tag.ToString());
        }

        private void PromptPriview_TextChanged(object sender, TextChangedEventArgs e)
        {
            var item = PromptList.SelectedItem as ListBoxItem;
            item.Tag = PromptPriview.Text;
        }

        private void PromptName_TextChanged(object sender, TextChangedEventArgs e)
        {
            var item = PromptList.SelectedItem as ListBoxItem;
            item.Content = PromptName.Text;
        }
    }
}
