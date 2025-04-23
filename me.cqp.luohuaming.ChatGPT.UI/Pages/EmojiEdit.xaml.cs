using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using ModernWpf.Controls;
using PropertyChanged;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace me.cqp.luohuaming.ChatGPT.UI.Pages
{
    /// <summary>
    /// EmojiEdit.xaml 的交互逻辑
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class EmojiEdit : INotifyPropertyChanged
    {
        public EmojiEdit()
        {
            InitializeComponent();
            DataContext = this;
        }
        public event PropertyChangedEventHandler PropertyChanged;

        public Model.Emoji Picture { get; set; }

        public bool Requesting { get; set; }

        public string ExtraDescription { get; set; }

        public string PictureDescription { get; set; }

        public string EmbeddingDisplay { get; set; }

        public float[] Embedding { get; set; } = [];

        [DoNotNotify]
        public ContentDialogResult DialogResult { get; set; }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (Embedding.Length == 0)
            {
                MainWindow.ShowError("Embedding 结果为空，请先请求");
                return;
            }
            DialogResult = ContentDialogResult.Primary;
            Hide();
        }

        private void CancleButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = ContentDialogResult.Secondary;
            Hide();
        }

        private async void Redescribe_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ExtraDescription))
            {
                MainWindow.ShowError("请先输入描述");
                return;
            }
            string prompt = $"{PictureDescriber.EmojiPrompt}以下是对图片内包含情绪的额外补充：{ExtraDescription}";
            Requesting = true;
            try
            {
                string result = await Task.Run(() => PictureDescriber.Describe(prompt, Picture.ImageAbsoultePath));
                if (string.IsNullOrEmpty(result) || result == Chat.ErrorMessage)
                {
                    MainWindow.ShowError("请求描述失败，返回结果为空");
                }
                else
                {
                    PictureDescription = result;
                    Embedding = [];
                    EmbeddingDisplay = "[]";
                }
            }
            catch (Exception exc)
            {
                MainWindow.ShowError($"请求描述失败：{exc}");
            }
            finally
            {
                Requesting = false;
            }
        }

        private async void Embedding_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PictureDescription))
            {
                MainWindow.ShowError("请先输入描述");
                return;
            }
            Requesting = true;
            try
            {
                Embedding = await Task.Run(() => PublicInfos.API.Embedding.GetEmbedding(PictureDescription));
                if (Embedding.Length == 0)
                {
                    MainWindow.ShowError("请求Embedding失败，返回结果为空");
                }
                else
                {
                    UpdateEmbeddingDisplay();
                }
            }
            catch (Exception exc)
            {
                MainWindow.ShowError($"请求Embedding失败：{exc}");
            }
            finally
            {
                Requesting = false;
            }
        }

        private void UpdateEmbeddingDisplay()
        {
            if (Embedding.Length == 0)
            {
                EmbeddingDisplay = "[]";
            }
            else
            {
                EmbeddingDisplay = $"[{string.Join(",", Embedding.Take(3))}...{Embedding.Length}维]";
            }
        }

        private void ContentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if (Picture == null)
            {
                return;
            }
            Embedding = Picture.Raw.Embedding;
            PictureDescription = Picture.Description;
            UpdateEmbeddingDisplay();
        }
    }
}
