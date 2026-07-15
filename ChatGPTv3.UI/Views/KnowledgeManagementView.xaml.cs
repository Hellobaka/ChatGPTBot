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
        if (DataContext is KnowledgeManagementViewModel vm && e.NewValue is SourceGroup group)
        {
            vm.SelectedSource = group;
        }
    }

    private async void TreeViewItem_RightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is TreeViewItem item && item.DataContext is SourceGroup group)
        {
            item.IsSelected = true;

            if (group.Name == "全部")
            {
                return;
            }

            var result = MessageBox.Show(
                $"确定要删除来源「{group.Name}」的全部 {group.Count} 条知识吗？\n此操作不可撤销。",
                "确认删除来源",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes && DataContext is KnowledgeManagementViewModel vm)
            {
                vm.DeleteSourceCommand.Execute(group);
            }
        }
    }
}
