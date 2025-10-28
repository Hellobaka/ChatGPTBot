using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using me.cqp.luohuaming.ChatGPT.UI.Windows;
using Microsoft.Extensions.AI;
using ModernWpf.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace me.cqp.luohuaming.ChatGPT.UI.Pages
{
    /// <summary>
    /// KeyEdit.xaml 的交互逻辑
    /// </summary>
    public partial class KeyEdit : INotifyPropertyChanged
    {
        public KeyEdit(APIKeys apiKey)
        {
            InitializeComponent();
            DataContext = this;
            ApiKey = apiKey;
            AvailableModels = new ObservableCollection<LLMModel>(apiKey.AvailableModels ?? []);
            OnPropertyChanged(nameof(ApiKey));
        }

        public APIKeys ApiKey { get; set; }

        public ObservableCollection<LLMModel> AvailableModels { get; set; }

        public bool Testing { get; set; }

        public ContentDialogResult DialogResult { get; set; }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ApiKey.AvailableModels = AvailableModels.ToList();
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
                response = await Task.Run(() => Chat.GetChatResult(new APIKeyPurpose() { Key = ApiKey, Model = AvailableModels.FirstOrDefault() }, new List<ChatMessage>
                {
                    new(ChatRole.User, "Hello")
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

        private void AddModel_Click(object sender, RoutedEventArgs e)
        {
            LLMModelEdit edit = new(new());
            edit.ShowDialog();
            if (edit.DialogResult == ContentDialogResult.Primary)
            {
                var model = edit.LLMModel;
                if (AvailableModels.Any(x => x.Name == model.Name))
                {
                    MainWindow.ShowError("模型名称重复");
                    return;
                }
                AvailableModels.Add(model);
            }
        }

        private void EditModel_Click(object sender, RoutedEventArgs e)
        {
            if (LLMModelList.SelectedItem != null && LLMModelList.SelectedItem is LLMModel model)
            {
                LLMModelEdit edit = new(model.Clone());
                edit.ShowDialog();
                if (edit.DialogResult == ContentDialogResult.Primary)
                {
                    var modelToRemove = AvailableModels.FirstOrDefault(x => model.Id != 0 ? x.Id == model.Id : x.Name == model.Name);
                    if (modelToRemove == null)
                    {
                        MainWindow.ShowError("不能确定要编辑的项目，请重启程序后再重新编辑");
                        return;
                    }
                    int index = AvailableModels.IndexOf(modelToRemove);
                    AvailableModels.RemoveAt(index);
                    AvailableModels.Insert(index, edit.LLMModel);
                }
            }
            else
            {
                MainWindow.ShowError("未选中模型，无法编辑");
            }
        }

        private void DeleteModel_Click(object sender, RoutedEventArgs e)
        {
            if (LLMModelList.SelectedItem != null && LLMModelList.SelectedItem is LLMModel model)
            {
                if (MainWindow.ShowConfirm($"确认要删除模型 {model.Name} 吗？"))
                {
                    AvailableModels.Remove(model);
                }
            }
            else
            {
                MainWindow.ShowError("未选中模型，无法删除");
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
