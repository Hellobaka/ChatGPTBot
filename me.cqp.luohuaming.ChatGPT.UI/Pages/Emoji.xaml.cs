using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using Microsoft.Win32;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        public int EmojiCount => SearchedEmojis.Count;

        public ObservableCollection<Model.Emoji> SearchedEmojis { get; set; } = [];

        public ObservableCollection<Model.Emoji> Emojis { get; set; } = [];

        [DoNotNotify]
        public bool PageLoaded { get; set; }

        public bool Querying { get; set; }

        public bool Inserting { get; set; }

        public int RecommendCount => RecommendEmojis.Count;

        public ObservableCollection<Model.Emoji> RecommendEmojis { get; set; } = [];

        public ObservableCollection<Model.Emoji> InsertEmojis { get; set; } = [];

        public int InsertTaskCount => InsertEmojis.Count;

        public int InsertTaskFinishedCount => InsertEmojis.Count(x => x.Finished);

        public int InsertTaskSucceedCount => InsertEmojis.Count(x => x.Success);

        public int EmojiInsertParallelCountValue => int.TryParse(EmojiInsertParallelCount.Text, out int value) ? value : -1;

        private Debouncer SearchDebouncer { get; set; } = new();

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void EmojiBatchDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.ShowConfirm("确定要删除这些图片吗？"))
            {
                foreach (var item in SearchedEmojis.Where(x => x.Checked))
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

        private async void EmojiRetryButton_Click(object sender, RoutedEventArgs e)
        {
            var emoji = (sender as Button).DataContext as Model.Emoji;
            if (emoji == null)
            {
                MainWindow.ShowError($"重试表情包插入失败，所选表情包为null");
            }
            try
            {
                Inserting = true;
                emoji.Finished = false;
                emoji.Duplicated = false;
                await InsertEmoji(emoji, true);
            }
            catch (Exception exc)
            {
                MainWindow.ShowError($"重试表情包插入失败，{exc}");
            }
            finally
            {
                emoji.Finished = true;
                Inserting = false;
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
            foreach (var item in SearchedEmojis)
            {
                item.Checked = true;
            }
        }

        private void EmojiSelectNoneButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in SearchedEmojis)
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
            SearchedEmojis.CollectionChanged += (s, e) =>
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
            RefilterEmoji();
        }

        private void Picture_OnPictureAdded(Picture picture)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (Emojis.Any(x => x.Hash == picture.Hash))
                {
                    Emojis.Remove(Emojis.First(x => x.Hash == picture.Hash));
                }
                if (picture.IsEmoji)
                {
                    Emojis.Add(Model.Emoji.ParseFromPicture(picture));
                }
                RefilterEmoji();
            });
        }

        private void Picture_OnPictureRemoved(Picture picture)
        {
            Dispatcher.BeginInvoke(() =>
            {
                var pic = Emojis.FirstOrDefault(x => x.Hash == picture.Hash);
                Emojis.Remove(pic);

                pic = RecommendEmojis.FirstOrDefault(x => x.Hash == picture.Hash);
                RecommendEmojis.Remove(pic);

                pic = InsertEmojis.FirstOrDefault(x => x.Hash == picture.Hash);
                InsertEmojis.Remove(pic);
                RefilterEmoji();
            });
        }

        private async void QueryButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(QueryText.Text))
            {
                MainWindow.ShowError("请输入要检索的文本");
                return;
            }
            Querying = true;
            try
            {
                RecommendEmojis.Clear();
                string query = QueryText.Text;
                bool embeddingFirst = QueryEmbeddingFirst.IsOn;
                List<(Picture emoji, double similarity)> pictures = [];
                await Task.Run(() =>
                {
                    if (embeddingFirst)
                    {
                        var embedding = Embedding.GetEmbedding(query);
                        pictures = Picture.GetRecommandEmoji(embedding, AppConfig.RecommendEmojiCount);
                    }
                    else
                    {
                        pictures = Picture.GetRecommandEmoji(query);
                    }
                    if (pictures.Count == 0)
                    {
                        MainWindow.ShowError("没有找到符合条件的图片");
                        return;
                    }
                });
                foreach (var item in pictures)
                {
                    var emoji = Model.Emoji.ParseFromPicture(item.emoji);
                    if (emoji != null)
                    {
                        emoji.CosineSimilarity = item.similarity;
                        RecommendEmojis.Add(emoji);
                    }
                }
                RecommendExpander.IsExpanded = true;
            }
            catch (Exception exc)
            {
                MainWindow.ShowError($"检索过程发生异常: {exc}");
            }
            finally
            {
                Querying = false;
            }
        }

        private void QueryResetButton_Click(object sender, RoutedEventArgs e)
        {
            QueryText.Text = string.Empty;
            RecommendEmojis.Clear();
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
                RefilterEmoji();
            }
        }

        private void RecommendSelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in RecommendEmojis)
            {
                item.Checked = true;
            }
        }

        private void RecommendSelectNoneButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in RecommendEmojis)
            {
                item.Checked = false;
            }
        }

        private void QueryText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                QueryButton_Click(sender, e);
            }
        }

        private async void SelectImagesButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog()
            {
                Multiselect = true,
                Filter = "图片文件|*.jpg;*.png;*.gif;*.jpeg;|所有文件|*.*",
                CheckFileExists = true,
            };
            if (!(dialog.ShowDialog() ?? false))
            {
                return;
            }
            try
            {
                Inserting = true;
                var files = dialog.FileNames;
                foreach (var item in files)
                {
                    string newPath = Path.Combine(PictureDescriber.GetPictureCachePath(), Path.GetFileName(item));
                    if (!File.Exists(newPath))
                    {
                        File.Copy(item, newPath, true);
                    }
                    var picture = new Model.Emoji
                    {
                        AddTime = DateTime.Now,
                        Hash = PictureDescriber.ComputeImageHash(newPath),
                        ImageAbsoultePath = newPath,
                        FilePath = CommonHelper.GetRelativePath(newPath, MainSave.ImageDirectory),
                    };
                    try
                    {
                        var exist = InsertEmojis.FirstOrDefault(x => x.Hash == picture.Hash);
                        if (exist != null)
                        {
                            InsertEmojis.Remove(exist);
                        }
                        InsertEmojis.Add(picture);
                        NoticeInsertChanged();
                    }
                    catch { }
                }
                var taskArray = InsertEmojis.Where(x => !x.Finished);
                if (EmojiInsertCanParallel.IsOn && EmojiInsertParallelCountValue > 0)
                {
                    var semaphore = new SemaphoreSlim(EmojiInsertParallelCountValue);
                    var tasks = taskArray.Select(async x =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            await InsertEmoji(x, false);
                            NoticeInsertChanged();
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });
                    await Task.WhenAll(tasks);
                }
                else
                {
                    foreach (var item in taskArray)
                    {
                        await InsertEmoji(item, false);
                        NoticeInsertChanged();
                    }
                }
            }
            catch (Exception exc)
            {
                MainWindow.ShowError($"插入表情包失败：{exc}");
            }
            finally
            {
                Inserting = false;
            }

            void NoticeInsertChanged()
            {
                OnPropertyChanged(nameof(InsertTaskFinishedCount));
                OnPropertyChanged(nameof(InsertTaskCount));
                OnPropertyChanged(nameof(InsertTaskSucceedCount));
                OnPropertyChanged(nameof(InsertEmojis));
            }
        }

        private async Task InsertEmoji(Model.Emoji item, bool force)
        {
            try
            {
                if (!force && Picture.Cache.TryGetValue(item.Hash, out Picture raw))
                {
                    item.Duplicated = true;
                    item.Raw = raw;
                    item.Description = item.Raw.Description;
                    item.EmbeddingDimensions = item.Raw.Embedding.Length;
                    item.Success = true;
                    return;
                }
                string description = await Task.Run(() => PictureDescriber.DescribeEmoji(item.ImageAbsoultePath));
                if (string.IsNullOrEmpty(description) || description == Chat.ErrorMessage)
                {
                    return;
                }
                item.Description = description;
                item.Raw = await Task.Run(() => Picture.InsertImageDescription(item.ImageAbsoultePath, item.Hash, true, description));
                if (item.Raw == null)
                {
                    return;
                }
                item.EmbeddingDimensions = item.Raw.Embedding.Length;

                item.Success = true;
            }
            catch (Exception exc)
            {
                CommonHelper.DebugLog("获取表情包描述", exc.ToString());
                item.Success = false;
            }
            finally
            {
                item.Finished = true;
            }
        }

        private void ClearInsertImagesButton_Click(object sender, RoutedEventArgs e)
        {
            InsertEmojis.Clear();
        }

        private void EmojiInsertParallelCount_LostFocus(object sender, RoutedEventArgs e)
        {
            if (EmojiInsertParallelCountValue <= 0)
            {
                MainWindow.ShowError("并行数量设置无效");
                EmojiInsertParallelCount.Text = "3";
            }
        }

        private void EmojiReloadButton_Click(object sender, RoutedEventArgs e)
        {
            Emojis.Clear();
            foreach (var item in Picture.Cache)
            {
                Emojis.Add(Model.Emoji.ParseFromPicture(item.Value));
            }
            RefilterEmoji();
        }

        private async void MaintRebuildEmbeddingButton_Click(object sender, RoutedEventArgs e)
        {
            if (!MainWindow.ShowConfirm("确认要重建Embedding吗？此操作应该只有在更换Embedding模型之后进行。"))
            {
                return;
            }
            try
            {
                int total = Picture.Cache.Count;
                int successCount = 0;

                UpdateMaintStatus(true, successCount, total);

                await Task.Run(() =>
                {
                    // 先尝试一个，测试Embedding接口是否正常
                    if (!UpdateEmbedding(Picture.Cache.FirstOrDefault().Value))
                    {
                        MainWindow.ShowError("Embedding接口异常，无法重建");
                        return;
                    }
                    successCount++;
                    UpdateMaintStatus(true, successCount, total);

                    var array = Picture.Cache.Values.Skip(1).ToArray();
                    List<Picture> failPictures = [];
                    Parallel.ForEach(array, new ParallelOptions { MaxDegreeOfParallelism = 3 }, (item) =>
                    {
                        if (UpdateEmbedding(item))
                        {
                            successCount++;
                            UpdateMaintStatus(true, successCount, total);
                        }
                        else
                        {
                            failPictures.Add(item);
                        }
                    });
                    foreach (var item in failPictures)
                    {
                        if (UpdateEmbedding(item))
                        {
                            successCount++;
                            UpdateMaintStatus(true, successCount, total);
                        }
                    }
                    if (successCount == total)
                    {
                        MainWindow.ShowInfo($"Embedding 重建完成，共计: {total} 个");
                    }
                    else
                    {
                        MainWindow.ShowError($"Embedding 重建完成，成功: {successCount} 个，失败: {total - successCount} 个");
                    }
                    Picture.InitCache();
                });
                EmojiReloadButton_Click(sender, e);
            }
            catch (Exception exc)
            {
                MainWindow.ShowError($"重建过程发生异常：{exc}");
            }
            finally
            {
                UpdateMaintStatus(true, -1, -1);
            }

            bool UpdateEmbedding(Picture emoji)
            {
                try
                {
                    var embedding = Embedding.GetEmbedding(emoji.Description);
                    if (embedding.Length == 0)
                    {
                        return false;
                    }
                    emoji.Embedding = embedding;
                    emoji.Update();
                    return true;
                }
                catch (Exception e)
                {
                    MainSave.CQLog?.Info("Embedding重建", $"Hash={emoji.Hash} 请求失败：{e}");
                    return false;
                }
            }
        }

        private async void MaintClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (!MainWindow.ShowConfirm("确认要清空图片数据库吗？此操作不可逆"))
            {
                return;
            }
            await Task.Run(() =>
            {
                try
                {
                    Picture.DropAndRebuildTable();
                    Picture.InitCache();

                    MainWindow.ShowInfo("图片数据库清空成功");
                }
                catch (Exception exc)
                {
                    MainWindow.ShowError($"清空图片数据库失败：{exc}");
                }
            });
            EmojiReloadButton_Click(sender, e);
        }

        private void UpdateMaintStatus(bool running, int current, int max)
        {
            Dispatcher.BeginInvoke(() =>
            {
                MaintanceRunningStatus.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
                if (current > 0 && max > 0)
                {
                    MaintanceStatus.Text = $"进度：{current}/{max}";
                    MaintanceProgress.Value = current;
                    MaintanceProgress.Maximum = max;
                }
            });
        }

        private void EmojiQueryInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchDebouncer.Debounce(RefilterEmoji, 500);
        }

        private void RefilterEmoji()
        {
            Dispatcher.BeginInvoke(() =>
            {
                string search = EmojiQueryInput.Text;

                if (string.IsNullOrWhiteSpace(search))
                {
                    SearchedEmojis = [.. Emojis];
                    return;
                }
                SearchedEmojis = [.. Emojis.Where(x => x.Hash.Contains(search) || x.Description.Contains(search))];
            });
        }
    }
}