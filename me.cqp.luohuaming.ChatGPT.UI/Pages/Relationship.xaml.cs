using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace me.cqp.luohuaming.ChatGPT.UI.Pages
{
    /// <summary>
    /// Relationship.xaml 的交互逻辑
    /// </summary>
    public partial class Relationship : Page
    {
        public Relationship()
        {
            InitializeComponent();
        }

        private List<long> RenderedGroups { get; set; } = [];

        private Dictionary<long, Expander> RenderGroupExpanders { get; set; } = [];

        private Dictionary<long, ListBox> RenderGroupListBoxs { get; set; } = [];

        private GroupInfoCollection? GroupInfos { get; set; }

        private object SelectedItem { get; set; }

        private PublicInfos.DB.Relationship SelectedRelationship { get; set; }

        private void EditSelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem == null)
            {
                MainWindow.ShowError("请先选中一项");
                return;
            }
            if (!double.TryParse(SelectRelationshipValue.Text, out double favor))
            {
                MainWindow.ShowError("输入的数值无效");
                return;
            }
            SelectedRelationship.Favorability = Math.Min(1000, Math.Max(-1000, favor));
            SelectedRelationship.Update();
            (SelectedItem as ListBoxItem).Content = $"{SelectedRelationship.Card ?? SelectedRelationship.NickName}[{SelectedRelationship.QQ}] - {SelectedRelationship.Favorability:f2}";
            SelectRelationshipDescription.Text = $"当前关系为：{SelectedRelationship}";
            MainWindow.ShowInfo("数值编辑成功");
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            GroupInfos = MainSave.CQApi?.GetGroupList();
            RebuildGroupList();

            SelectedItem = null;
            SelectedRelationship = null;
            SelectRelationshipDescription.Text = "当前关系为：";
            SelectRelationshipValue.Text = "";
        }

        private void DeleteSelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem == null)
            {
                MainWindow.ShowError("请先选中一项");
                return;
            }
            if (MainWindow.ShowConfirm("确认要删除此项目吗？"))
            {
                SelectedRelationship.Delete();
                if (RenderGroupListBoxs.TryGetValue(SelectedRelationship.GroupID, out ListBox listBox))
                {
                    listBox.Items.Remove(SelectedItem);
                }
                MainWindow.ShowInfo("删除成功");
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ReloadButton_Click(sender, e);
        }

        private void RebuildGroupList()
        {
            var groups = PublicInfos.DB.Relationship.GetRelationShipGroups();
            EmptyHint.Visibility = groups.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
            foreach (var item in RenderedGroups.ToList())
            {
                RenderedGroups.Remove(item);
                RemoveRenderGroup(item);
            }
            foreach (var item in groups)
            {
                RenderedGroups.Add(item);
                AddRenderGroup(item);
            }
        }

        private void RemoveRenderGroup(long groupId)
        {
            if (RenderGroupExpanders.TryGetValue(groupId, out var expander))
            {
                GroupsContainer.Children.Remove(expander);
                RenderGroupExpanders.Remove(groupId);
                expander.Expanded -= Expander_Expanded;
                if (RenderGroupListBoxs.TryGetValue(groupId, out var listbox))
                {
                    RenderGroupListBoxs.Remove(groupId);
                    listbox.MouseDoubleClick -= ListBox_MouseDoubleClick;
                }
            }
        }

        private void AddRenderGroup(long groupId)
        {
            string displayName = "";
            if (groupId == -1)
            {
                displayName = "私聊";
            }
            else
            {
                displayName = $"{GroupInfos?.FirstOrDefault(x => x.Group == groupId)?.Name}[{groupId}]";
            }
            Expander expander = new()
            {
                Header = displayName,
                Tag = groupId,
                Margin = new Thickness(0, 16, 16, 16)
            };
            expander.Expanded += Expander_Expanded;
            ListBox listBox = new();
            listBox.Margin = new Thickness(10);
            listBox.MouseDoubleClick += ListBox_MouseDoubleClick;
            expander.Content = listBox;

            RenderGroupExpanders.Add(groupId, expander);
            RenderGroupListBoxs.Add(groupId, listBox);

            GroupsContainer.Children.Add(expander);
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            var expander = sender as Expander;
            if (expander.Tag is long groupId && RenderGroupListBoxs.TryGetValue(groupId, out ListBox listBox)
                && listBox.Items.Count == 0)
            {
                var items = PublicInfos.DB.Relationship.GetRelationShipByGroup(groupId);
                foreach (var item in items)
                {
                    ListBoxItem listBoxItem = new()
                    {
                        Content = $"{item.Card ?? item.NickName}[{item.QQ}]: {item.Favorability:f2}",
                        Tag = item
                    };
                    listBox.Items.Add(listBoxItem);
                }
            }
        }

        private void ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var listbox = sender as ListBox;
            if (listbox.SelectedIndex >= 0)
            {
                if ((listbox.SelectedItem as ListBoxItem).Tag is PublicInfos.DB.Relationship item)
                {
                    SelectedItem = listbox.SelectedItem;
                    SelectedRelationship = item;

                    SelectRelationshipValue.Text = item.Favorability.ToString("f3");
                    SelectRelationshipDescription.Text = $"当前关系为：{item}";
                }
            }
        }
    }
}
