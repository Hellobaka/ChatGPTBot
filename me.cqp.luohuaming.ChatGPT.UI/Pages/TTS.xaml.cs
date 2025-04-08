using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace me.cqp.luohuaming.ChatGPT.UI.Pages
{
    /// <summary>
    /// TTS.xaml 的交互逻辑
    /// </summary>
    public partial class TTS : Page
    {
        public TTS()
        {
            InitializeComponent();
        }

        private void RefreshTTSStatus()
        {
            TTSStatus.Text = TTSHelper.Enabled ? "启用中" : "已禁用";
            TTSStatus.Foreground = TTSHelper.Enabled ? Brushes.Green : Brushes.Red;
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
                MainWindow.ShowError("配置已禁用TTS，请启用后重新进行检查");
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
            MainWindow.ShowInfo($"TTS服务检查结果为：{check}");
        }

        private async void TTSTestButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TTSInput.Text))
            {
                MainWindow.ShowError("合成的文本不可为空");
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
                if (MainWindow.ShowConfirm("TTS 成功，点击\"是\"打开音频"))
                {
                    Process.Start(Path.Combine(dir, fileName));
                }
            }
            else
            {
                MainWindow.ShowError("音频合成失败，查看日志排查问题");
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshTTSStatus();

        }
    }
}
