﻿using me.cqp.luohuaming.ChatGPT.PublicInfos;
using System;
using System.Collections.Generic;
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
        public Settings()
        {
            InitializeComponent();
        }

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

        private void BlackListAddButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(BlackListAdd.Text) && long.TryParse(BlackListAdd.Text, out _))
            {
                bool duplicate = false;
                foreach (var item in BlackList.Items)
                {
                    if (item.ToString() == BlackListAdd.Text)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate)
                {
                    MainWindow.ShowError("已存在相同项");
                    return;
                }
                BlackList.Items.Add(BlackListAdd.Text);
            }
            else
            {
                MainWindow.ShowError("输入内容格式错误");
            }
        }

        private void BlackListRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (BlackList.SelectedIndex < 0)
            {
                MainWindow.ShowError("请选择一项");
                return;
            }
            BlackList.Items.RemoveAt(BlackList.SelectedIndex);
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
                else if (item is ListBox listbox)
                {
                    var property = properties.FirstOrDefault(x => x.Name == listbox.Name);
                    if (property == null)
                    {
                        Debugger.Break();
                        continue;
                    }
                    var list = property?.GetValue(null, null);
                    if (list is List<long> l)
                    {
                        l.Clear();
                        foreach (var i in listbox.Items)
                        {
                            l.Add(long.Parse(i.ToString()));
                        }
                        ConfigHelper.SetConfig(listbox.Name, l);
                    }
                    else if (list is List<string> l2)
                    {
                        l2.Clear();
                        foreach (var i in listbox.Items)
                        {
                            l2.Add(i.ToString());
                        }
                        ConfigHelper.SetConfig(listbox.Name, l2);
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

        private void GroupListAddButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(GroupListAdd.Text) && long.TryParse(GroupListAdd.Text, out _))
            {
                bool duplicate = false;
                foreach (var item in GroupList.Items)
                {
                    if (item.ToString() == GroupListAdd.Text)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate)
                {
                    MainWindow.ShowError("已存在相同项");
                    return;
                }
                GroupList.Items.Add(GroupListAdd.Text);
            }
            else
            {
                MainWindow.ShowError("输入内容格式错误");
            }
        }

        private void GroupListRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (GroupList.SelectedIndex < 0)
            {
                MainWindow.ShowError("请选择一项");
                return;
            }
            GroupList.Items.RemoveAt(GroupList.SelectedIndex);
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            var uri = e.Uri;
            Process.Start(uri.ToString());
        }

        private void PersonListAddButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(PersonListAdd.Text) && long.TryParse(PersonListAdd.Text, out _))
            {
                bool duplicate = false;
                foreach (var item in PersonList.Items)
                {
                    if (item.ToString() == PersonListAdd.Text)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate)
                {
                    MainWindow.ShowError("已存在相同项");
                    return;
                }
                PersonList.Items.Add(PersonListAdd.Text);
            }
            else
            {
                MainWindow.ShowError("输入内容格式错误");
            }
        }

        private void PersonListRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (PersonList.SelectedIndex < 0)
            {
                MainWindow.ShowError("请选择一项");
                return;
            }
            PersonList.Items.RemoveAt(PersonList.SelectedIndex);
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
                    && VerifyInput(properties, CommandContainer, out err)
                    && VerifyInput(properties, ResponseContainer, out err)
                    && VerifyInput(properties, TTSContainer, out err)
                    && VerifyInput(properties, GroupContainer, out err))
                {
                    ConfigHelper.DisableHotReload();
                    GetAndSetConfigFromStackPanel(properties, APIContainer);
                    GetAndSetConfigFromStackPanel(properties, CommandContainer);
                    GetAndSetConfigFromStackPanel(properties, ResponseContainer);
                    GetAndSetConfigFromStackPanel(properties, TTSContainer);
                    GetAndSetConfigFromStackPanel(properties, GroupContainer);
                    GetAndSetConfigFromStackPanel(properties, ChatContainer);
                    GetAndSetConfigFromStackPanel(properties, MemoryContainer);
                    GetAndSetConfigFromStackPanel(properties, EmojiContainer);
                    GetAndSetConfigFromStackPanel(properties, ScheduleContainer);
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
                    else if (item is ListBox listbox)
                    {
                        var property = properties.FirstOrDefault(x => x.Name == listbox.Name);
                        if (property == null)
                        {
                            Debugger.Break();
                            continue;
                        }
                        listbox.Items.Clear();
                        var list = property?.GetValue(null, null);
                        if (list is List<long> l)
                        {
                            foreach (var i in l)
                            {
                                listbox.Items.Add(i);
                            }
                        }
                        else if (list is List<string> l2)
                        {
                            foreach (var i in l2)
                            {
                                listbox.Items.Add(i);
                            }
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

        private void TextTemplate_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                Clipboard.SetText((sender as TextBlock).Tag.ToString());
            }
            catch 
            {
                MainWindow.ShowError("复制文本失败");
            }
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
            SetConfigToStackPanel(properties, CommandContainer);
            SetConfigToStackPanel(properties, ResponseContainer);
            SetConfigToStackPanel(properties, TTSContainer);
            SetConfigToStackPanel(properties, GroupContainer);
            SetConfigToStackPanel(properties, ChatContainer);
            SetConfigToStackPanel(properties, MemoryContainer);
            SetConfigToStackPanel(properties, EmojiContainer);
            SetConfigToStackPanel(properties, ScheduleContainer);
        }

        private void BotNicknameRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (BotNicknames.SelectedIndex < 0)
            {
                MainWindow.ShowError("请选择一项");
                return;
            }
            BotNicknames.Items.RemoveAt(BotNicknames.SelectedIndex);
        }

        private void BotNicknameAddButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(BotNicknameAdd.Text))
            {
                bool duplicate = false;
                foreach (var item in BotNicknames.Items)
                {
                    if (item.ToString() == BotNicknameAdd.Text)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate)
                {
                    MainWindow.ShowError("已存在相同项");
                    return;
                }
                BotNicknames.Items.Add(BotNicknameAdd.Text);
            }
            else
            {
                MainWindow.ShowError("输入内容格式错误");
            }
        }
    }
}