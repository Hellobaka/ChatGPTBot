using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using PropertyChanged;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace me.cqp.luohuaming.ChatGPT.UI.Pages
{
    /// <summary>
    /// Emoji.xaml 的交互逻辑
    /// </summary>
    public partial class Emoji : Page, INotifyPropertyChanged
    {
        public Emoji()
        {
            InitializeComponent();
            DataContext = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public int EmojiCount => Emojis.Count;

        public ObservableCollection<Model.Emoji> Emojis { get; set; } = [];

        [DoNotNotify]
        public bool PageLoaded { get; set; }

        public int RecommendCount => RecommendEmojis.Count;

        public ObservableCollection<Model.Emoji> RecommendEmojis { get; set; } = [];

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void EmojiBatchDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.ShowConfirm("确定要删除这些图片吗？"))
            {
                foreach (var item in Emojis.Where(x => x.Checked))
                {
                    item.Raw.Delete();
                    Picture.Remove(item.Raw);
                }
            }
        }

        private void EmojiDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var emoji = (sender as Button).DataContext as Model.Emoji;
            if (MainWindow.ShowConfirm("确定要删除图片吗？"))
            {
                emoji.Raw.Delete();
                Picture.Remove(emoji.Raw);
            }
        }

        private async void EmojiEditButton_Click(object sender, RoutedEventArgs e)
        {
            var emoji = (sender as Button).DataContext as Model.Emoji;

            EmojiEdit dialog = new();
            dialog.Picture = emoji;
            await dialog.ShowAsync();
            if (dialog.DialogResult == ModernWpf.Controls.ContentDialogResult.Primary)
            {
                emoji.Description = dialog.PictureDescription;
                emoji.EmbeddingDimensions = dialog.Embedding.Length;
                emoji.Raw.Description = emoji.Description;
                emoji.Raw.Embedding = dialog.Embedding;

                emoji.Raw.Update();
            }
        }

        private void EmojiSelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in Emojis)
            {
                item.Checked = true;
            }
        }

        private void EmojiSelectNoneButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in Emojis)
            {
                item.Checked = false;
            }
        }

        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var emoji = (sender as Image).DataContext as Model.Emoji;
            if (e.ClickCount == 2 && File.Exists(emoji.ImageAbsoultePath))
            {
                Process.Start(emoji.ImageAbsoultePath);
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (PageLoaded)
            {
                return;
            }
            Picture.OnPictureAdded += Picture_OnPictureAdded;
            Picture.OnPictureRemoved += Picture_OnPictureRemoved;
            Emojis.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(EmojiCount));
            };
            RecommendEmojis.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(RecommendCount));
            };

            Emojis.Clear();
            foreach (var item in Picture.Cache)
            {
                Emojis.Add(Model.Emoji.ParseFromPicture(item.Value));
            }
        }

        private void Picture_OnPictureAdded(Picture picture)
        {
            if (Emojis.Any(x => x.Hash == picture.Hash))
            {
                Emojis.Remove(Emojis.First(x => x.Hash == picture.Hash));
            }
            if (picture.IsEmoji)
            {
                Emojis.Add(Model.Emoji.ParseFromPicture(picture));
            }
        }

        private void Picture_OnPictureRemoved(Picture picture)
        {
            var pic = Emojis.FirstOrDefault(x => x.Hash == picture.Hash);
            Emojis.Remove(pic);

            pic = RecommendEmojis.FirstOrDefault(x => x.Hash == picture.Hash);
            RecommendEmojis.Remove(pic);
        }

        private void QueryButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private void QueryResetButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private void RecommendBatchDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.ShowConfirm("确定要删除这些图片吗？"))
            {
                foreach (var item in RecommendEmojis.Where(x => x.Checked))
                {
                    item.Raw.Delete();
                    Picture.Remove(item.Raw);
                }
            }
        }

        private void RecommendSelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in Emojis)
            {
                item.Checked = true;
            }
        }

        private void RecommendSelectNoneButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in Emojis)
            {
                item.Checked = false;
            }
        }
    }
}