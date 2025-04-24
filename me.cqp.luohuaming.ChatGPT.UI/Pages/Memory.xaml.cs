using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using me.cqp.luohuaming.ChatGPT.UI.Model;
using PropertyChanged;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace me.cqp.luohuaming.ChatGPT.UI.Pages
{
    [AddINotifyPropertyChangedInterface]
    public partial class Memory : Page
    {
        public ObservableCollection<MemoryNode> Memories { get; } = [];

        public bool ReloadBusy { get; set; }

        public bool QueryBusy { get; set; }

        public bool RebuildBusy { get; set; }

        public string ErrorContent { get; set; }

        public bool ShowError { get; set; }

        public Memory()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void UpdateCollectionCount()
        {
            try
            {
                ReloadBusy = true;
                var qdrant = Qdrant.Instance;
                int count = qdrant?.GetCollectionCount() ?? 0;
                Dispatcher.Invoke(() => CollectionCountTextBlock.Text = $"Qdrant节点数量：{count}");
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MainWindow.ShowError($"Qdrant节点数量获取失败: {ex.Message}"));
            }
            finally
            {
                ReloadBusy = false;
            }
        }

        private async void QueryButton_Click(object sender, RoutedEventArgs e)
        {
            Memories.Clear();
            var input = QueryTextBox.Text.Trim();
            var group = GroupIdTextBox.IsEnabled ? GroupIdTextBox.Text : "";
            var qqText = QqTextBox.IsEnabled ? QqTextBox.Text : "";
            QueryBusy = true;
            await Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrEmpty(input))
                    {
                        Dispatcher.Invoke(() => MainWindow.ShowError("请输入查询内容"));
                        return;
                    }
                    if (!long.TryParse(group, out long groupId))
                    {
                        groupId = -1;
                    }
                    if (!long.TryParse(qqText, out long qq))
                    {
                        qq = -1;
                    }
                    var record = new ChatRecord
                    {
                        GroupID = groupId,
                        QQ = qq,
                        Message_NoAppendInfo = input,
                        Time = DateTime.Now
                    };
                    var result = PublicInfos.DB.Memory.GetMemories(record).OrderByDescending(x => x.score);
                    Dispatcher.Invoke(() =>
                    {
                        foreach (var item in result)
                        {
                            Memories.Add(new MemoryNode
                            {
                                Id = item.record.Id,
                                GroupId = item.record.GroupID,
                                Message = item.record.ParsedMessage,
                                MessageId = item.record.MessageID,
                                Time = item.record.Time,
                                QQ = item.record.QQ,
                                Score = item.score * 100
                            });
                        }
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => MainWindow.ShowError($"检索失败: {ex.Message}"));
                }
            });
            QueryBusy = false;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!MainWindow.ShowConfirm("是否删除此记忆节点？本操作不可逆。"))
            {
                return;
            }
            if ((sender as Button).DataContext is MemoryNode memory)
            {
                try
                {
                    if (DeleteMemory(memory))
                    {
                        Memories.Remove(memory);
                        MainWindow.ShowInfo("删除成功");
                    }
                    else
                    {
                        MainWindow.ShowError("删除失败");
                    }
                }
                catch (Exception ex)
                {
                    MainWindow.ShowError($"删除异常: {ex.Message}");
                }
                finally
                {
                    UpdateCollectionCount();
                }
            }
            else
            {
                MainWindow.ShowError("删除失败：未找到对应的记忆节点");
            }
        }

        private bool DeleteMemory(MemoryNode node)
        {
            // 调用Qdrant删除API，返回是否成功
            return Qdrant.Instance?.Delete(node.Id) ?? false;
        }

        private async void RebuildButton_Click(object sender, RoutedEventArgs e)
        {
            if (!MainWindow.ShowConfirm("是否确认重建记忆库？本操作不可逆。"))
            {
                return;
            }
            RebuildBusy = true;
            await Task.Run(() =>
            {
                try
                {
                    var qdrant = Qdrant.Instance;
                    bool dropOk = qdrant != null && qdrant.DropCollection();
                    if (!qdrant.GetCollections())
                    {
                        throw new Exception("Qdrant 获取集合失败");
                    }
                    bool createOk = qdrant != null && qdrant.CreateCollection();
                    Dispatcher.Invoke(() =>
                    {
                        if (dropOk && createOk)
                        {
                            MainWindow.ShowInfo("重建成功");
                            UpdateCollectionCount();
                        }
                        else if (!dropOk)
                        {
                            MainWindow.ShowError("重建失败：集合删除失败");
                        }
                        else
                        {
                            MainWindow.ShowError("重建失败：集合创建失败");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => MainWindow.ShowError($"重建异常: {ex.Message}"));
                }
            });
            RebuildBusy = false;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Run(UpdateCollectionCount);
            GroupSelector.IsOn = true;
        }

        private void ReloadQdrantButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(UpdateCollectionCount);
        }

        private void GroupSelector_Toggled(object sender, RoutedEventArgs e)
        {
            GroupIdTextBox.IsEnabled = GroupSelector.IsOn;
            QqTextBox.IsEnabled = true;
            if (GroupSelector.IsOn && !AppConfig.QdrantSearchOnlyPerson)
            {
                QqTextBox.IsEnabled = false;
                ShowError = true;
                ErrorContent = "注意：由于限制了记忆不检索个人，所以不能关联QQ进行检索";
            }
            else
            {
                ShowError = false;
                ErrorContent = "";
            }
        }
    }
}