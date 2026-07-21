using HandyControl.Controls;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ChatGPTv3.UI.ViewModels;
using MessageBox = HandyControl.Controls.MessageBox;

namespace ChatGPTv3.UI.Views;

public partial class KnowledgeManagementView : UserControl
{
    public KnowledgeManagementView()
    {
        InitializeComponent();
        DataContext = new KnowledgeManagementViewModel();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is KnowledgeManagementViewModel vm)
        {
            await vm.LoadCommand.ExecuteAsync(null);
        }
    }

    private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is KnowledgeManagementViewModel vm)
        {
            if (e.NewValue is SourceGroup group)
            {
                vm.SelectedSource = group;
            }
            else if (e.NewValue is KnowledgeItem item)
            {
                vm.SelectedItem = item;
            }
        }
    }

    private async void TreeViewItem_RightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is TreeViewItem item && item.DataContext is SourceGroup group)
        {
            item.IsSelected = true;

            if (group.Name == "全部" || DataContext is not KnowledgeManagementViewModel vm)
            {
                return;
            }

            vm.DeleteSourceCommand.Execute(group);
        }
    }
}
