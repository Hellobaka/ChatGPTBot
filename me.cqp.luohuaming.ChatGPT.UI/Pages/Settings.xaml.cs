using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using me.cqp.luohuaming.ChatGPT.UI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace me.cqp.luohuaming.ChatGPT.UI.Pages
{
    /// <summary>
    /// Settings.xaml 的交互逻辑
    /// </summary>
    public partial class Settings : Page
    {
        // TODO: 更详细的用途管理
        public Settings()
        {
            InitializeComponent();
        }

        private ObservableCollection<APIKeyPurpose> KeyPurposes { get; set; } = new();

        private ObservableCollection<APIKeyPurpose> ChatPurposes { get; set; } = new();

        private ObservableCollection<APIKeyPurpose> ImageDescriptionPurposes { get; set; } = new();

        private ObservableCollection<APIKeyPurpose> EmbeddingPurposes { get; set; } = new();

        private ObservableCollection<APIKeyPurpose> RerankPurposes { get; set; } = new();

        private ObservableCollection<APIKeyPurpose> SplitterPurposes { get; set; } = new();

        private static bool TryParse(string input, Type type, out object value)
        {
            value = input;
            if (type.Name == "Int32")
            {
                if (int.TryParse(input, out int v))
                {
                    value = v;
                }
                else
                {
                    return false;
                }
            }
            else if (type.Name == "UInt16")
            {
                if (ushort.TryParse(input, out ushort v))
                {
                    value = v;
                }
                else
                {
                    return false;
                }
            }
            else if (type.Name == "Int64")
            {
                if (long.TryParse(input, out long v))
                {
                    value = v;
                }
                else
                {
                    return false;
                }
            }
            else if (type.Name == "Single")
            {
                if (float.TryParse(input, out float v))
                {
                    value = v;
                }
                else
                {
                    return false;
                }
            }
            else if (type.Name == "Double")
            {
                if (double.TryParse(input, out double v))
                {
                    value = v;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        private void GetAndSetConfigFromStackPanel(PropertyInfo[] properties, StackPanel container)
        {
            foreach (UIElement item in container.Children)
            {
                if (item is TextBox textBox)
                {
                    var property = properties.FirstOrDefault(x => x.Name == textBox.Name);
                    if (property != null && TryParse(textBox.Text, property.PropertyType, out object value))
                    {
                        property.SetValue(null, value);
                        ConfigHelper.SetConfig(textBox.Name, value);
                    }
                }
                else if (item is StackPanel stackPanel)
                {
                    foreach (UIElement child in stackPanel.Children)
                    {
                        if (child is ModernWpf.Controls.ToggleSwitch checkBox)
                        {
                            var property = properties.FirstOrDefault(x => x.Name == checkBox.Name);
                            property?.SetValue(null, checkBox.IsOn);
                            ConfigHelper.SetConfig(checkBox.Name, checkBox.IsOn);
                        }
                    }
                }
                else if (item is EditableListBox_String listBox_String)
                {
                    var property = properties.FirstOrDefault(x => x.Name == listBox_String.Name);
                    if (property == null)
                    {
                        Debugger.Break();
                        continue;
                    }
                    var list = property?.GetValue(null, null);
                    if (list is List<string> strings)
                    {
                        strings.Clear();
                        foreach (var i in listBox_String.ItemSource)
                        {
                            strings.Add(i.ToString());
                        }
                        ConfigHelper.SetConfig(listBox_String.Name, strings);
                    }
                }
                else if (item is EditableListBox_Int listBox_Int)
                {
                    var property = properties.FirstOrDefault(x => x.Name == listBox_Int.Name);
                    if (property == null)
                    {
                        Debugger.Break();
                        continue;
                    }
                    var list = property?.GetValue(null, null);
                    if (list is List<long> longs)
                    {
                        longs.Clear();
                        foreach (var i in listBox_Int.ItemSource)
                        {
                            longs.Add(i);
                        }
                        ConfigHelper.SetConfig(listBox_Int.Name, longs);
                    }
                }
                else if (item is ComboBox comboBox)
                {
                    var property = properties.FirstOrDefault(x => x.Name == comboBox.Name);
                    if (property == null)
                    {
                        Debugger.Break();
                        continue;
                    }
                    ConfigHelper.SetConfig(comboBox.Name, (comboBox.SelectedItem as ComboBoxItem).Tag.ToString());
                }
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            var uri = e.Uri;
            Process.Start(uri.ToString());
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var properties = typeof(AppConfig).GetProperties(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                if (VerifyInput(properties, APIContainer, out string err)
                    && VerifyInput(properties, ChatContainer, out err)
                    && VerifyInput(properties, MemoryContainer, out err)
                    && VerifyInput(properties, EmojiContainer, out err)
                    && VerifyInput(properties, ScheduleContainer, out err)
                    && VerifyInput(properties, ResponseContainer, out err)
                    && VerifyInput(properties, GroupContainer, out err))
                {
                    ConfigHelper.DisableHotReload();
                    GetAndSetConfigFromStackPanel(properties, APIContainer);
                    GetAndSetConfigFromStackPanel(properties, ResponseContainer);
                    GetAndSetConfigFromStackPanel(properties, GroupContainer);
                    GetAndSetConfigFromStackPanel(properties, ChatContainer);
                    GetAndSetConfigFromStackPanel(properties, MemoryContainer);
                    GetAndSetConfigFromStackPanel(properties, EmojiContainer);
                    GetAndSetConfigFromStackPanel(properties, ScheduleContainer);

                    AppConfig.ReloadAPIKey();
                    SavePurpose();
                    ConfigHelper.EnableHotReload();
                    MainWindow.ShowInfo("配置保存成功");
                }
                else
                {
                    MainWindow.ShowError(err);
                }
            }
            catch (Exception ex) 
            {
                MainSave.CQLog?.Info("配置保存", $"{ex.Message}\n{ex.StackTrace}");
                MainWindow.ShowError("配置保存失败，查看日志排查问题");
            }
            finally
            {
                ConfigHelper.EnableHotReload();
            }
        }

        private void SetConfigToStackPanel(PropertyInfo[] properties, StackPanel container)
        {
            try
            {
                foreach (UIElement item in container.Children)
                {
                    if (item is TextBox textBox)
                    {
                        var property = properties.FirstOrDefault(x => x.Name == textBox.Name);
                        if (property != null)
                        {
                            textBox.Text = property.GetValue(null).ToString();
                        }
                    }
                    else if (item is StackPanel stackPanel)
                    {
                        foreach (UIElement child in stackPanel.Children)
                        {
                            if (child is ModernWpf.Controls.ToggleSwitch checkBox)
                            {
                                var property = properties.FirstOrDefault(x => x.Name == checkBox.Name);
                                if (property != null)
                                {
                                    checkBox.IsOn = (bool)property.GetValue(null);
                                }
                            }
                        }
                    }
                    else if (item is EditableListBox_String listBox_String)
                    {
                        var property = properties.FirstOrDefault(x => x.Name == listBox_String.Name);
                        if (property == null)
                        {
                            Debugger.Break();
                            continue;
                        }
                        var list = property?.GetValue(null, null);
                        if (list is List<string> strings)
                        {
                            listBox_String.ItemSource = new ObservableCollection<string>(strings);
                        }
                    }
                    else if (item is EditableListBox_Int listBox_Int)
                    {
                        var property = properties.FirstOrDefault(x => x.Name == listBox_Int.Name);
                        if (property == null)
                        {
                            Debugger.Break();
                            continue;
                        }
                        var list = property?.GetValue(null, null);
                        if (list is List<long> longs)
                        {
                            listBox_Int.ItemSource = new ObservableCollection<long>(longs);
                        }
                    }
                    else if (item is ComboBox combobox)
                    {
                        var property = properties.FirstOrDefault(x => x.Name == combobox.Name);
                        if (property == null)
                        {
                            Debugger.Break();
                            continue;
                        }
                        var v = property?.GetValue(null, null);
                        combobox.SelectedIndex = (int)v;
                    }
                }
            }
            catch
            { }
        }

        private bool VerifyInput(PropertyInfo[] properties, StackPanel container, out string err)
        {
            err = "";
            foreach (UIElement item in container.Children)
            {
                if (item is TextBox textBox)
                {
                    var property = properties.FirstOrDefault(x => x.Name == textBox.Name);
                    if (property != null && !TryParse(textBox.Text, property.PropertyType, out _))
                    {
                        err = $"{textBox.Name} 的 {textBox.Text} 输入无法转换为有效配置";
                        return false;
                    }
                }
            }
            return true;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var properties = typeof(AppConfig).GetProperties(BindingFlags.Static | BindingFlags.Public);
            SetConfigToStackPanel(properties, APIContainer);
            SetConfigToStackPanel(properties, ResponseContainer);
            SetConfigToStackPanel(properties, GroupContainer);
            SetConfigToStackPanel(properties, ChatContainer);
            SetConfigToStackPanel(properties, MemoryContainer);
            SetConfigToStackPanel(properties, EmojiContainer);
            SetConfigToStackPanel(properties, ScheduleContainer);

            ReloadPurpose();
        }

        private async void KeyPurposeEditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is APIKeyPurpose purpose)
            {
                KeyPurposeEdit purposeEdit = new(purpose.Clone());
                await purposeEdit.ShowAsync();
                if (purposeEdit.DialogResult == ModernWpf.Controls.ContentDialogResult.Primary)
                {
                    int index = KeyPurposes.IndexOf(purpose);
                    if (index >= 0)
                    {
                        KeyPurposes[index] = purposeEdit.APIKeyPurpose;
                    }
                }
            }
        }

        private async void KeyPurposeDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (await MainWindow.ShowConfirmDialog("删除确认", "确认删除该 API Key 吗？")
                && sender is Button button
                && button.DataContext is APIKeyPurpose purpose)
            {
                KeyPurposes.Remove(purpose);
            }
        }

        private void KeyPurposeTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch ((KeyPurposeTabControl.SelectedItem as TabItem).Tag.ToString())
            {
                case "Chat":
                    KeyPurposes = ChatPurposes;
                    break;

                case "Splitter":
                    KeyPurposes = SplitterPurposes;
                    break;

                case "ImageDescriber":
                    KeyPurposes = ImageDescriptionPurposes;
                    break;

                case "Embedding":
                    KeyPurposes = EmbeddingPurposes;
                    break;

                case "Rerank":
                    KeyPurposes = RerankPurposes;
                    break;

                default:
                    MainWindow.ShowError("未知的数据源选项");
                    break;
            }
            SetTimeoutBoxVisible();
            KeyPurposeDataGrid.ItemsSource = KeyPurposes;
        }

        private void SetTimeoutBoxVisible()
        {
            string tag = (KeyPurposeTabControl.SelectedItem as TabItem).Tag.ToString();
            ChatTimeout.Visibility = tag == "Chat" ? Visibility.Visible : Visibility.Collapsed;
            RerankTimeout.Visibility = tag == "Rerank" ? Visibility.Visible : Visibility.Collapsed;
            EmbeddingTimeout.Visibility = tag == "Embedding" ? Visibility.Visible : Visibility.Collapsed;
            SplitterTimeout.Visibility = tag == "Splitter" ? Visibility.Visible : Visibility.Collapsed;
            ImageDescriberTimeout.Visibility = tag == "ImageDescriber" ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void CreateKeyButton_Click(object sender, RoutedEventArgs e)
        {
            KeyPurposeEdit purposeEdit = new(new APIKeyPurpose());
            await purposeEdit.ShowAsync();
            if (purposeEdit.DialogResult == ModernWpf.Controls.ContentDialogResult.Primary)
            {
                KeyPurposes.Add(purposeEdit.APIKeyPurpose);
            }
        }

        private async void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (!await MainWindow.ShowConfirmDialog("刷新确认", "刷新可能导致未保存的更改丢失，确定要刷新吗？"))
            {
                return;
            }
            ReloadPurpose();
        }

        private void ReloadPurpose()
        {
            ChatPurposes = AppConfig.ChatAPIKeyId.ToObservableCollection();
            SplitterPurposes = AppConfig.SplitterApiKeyId.ToObservableCollection();
            ImageDescriptionPurposes = AppConfig.ImageDescriberApiKeyId.ToObservableCollection();
            EmbeddingPurposes = AppConfig.EmbeddingApiKeyId.ToObservableCollection();
            RerankPurposes = AppConfig.RerankApiKeyId.ToObservableCollection();
            KeyPurposeTabControl_SelectionChanged(null, null);
        }

        private void SavePurpose()
        {
            AppConfig.ChatAPIKeyId = ChatPurposes.ToList();
            AppConfig.SplitterApiKeyId = SplitterPurposes.ToList();
            AppConfig.ImageDescriberApiKeyId = ImageDescriptionPurposes.ToList();
            AppConfig.EmbeddingApiKeyId = EmbeddingPurposes.ToList();
            AppConfig.RerankApiKeyId = RerankPurposes.ToList();

            ConfigHelper.SetConfig("ChatAPIKeyId", AppConfig.ChatAPIKeyId);
            ConfigHelper.SetConfig("SplitterApiKeyId", AppConfig.SplitterApiKeyId);
            ConfigHelper.SetConfig("ImageDescriberApiKeyId", AppConfig.ImageDescriberApiKeyId);
            ConfigHelper.SetConfig("EmbeddingApiKeyId", AppConfig.EmbeddingApiKeyId);
            ConfigHelper.SetConfig("RerankApiKeyId", AppConfig.RerankApiKeyId);
        }
    }
}