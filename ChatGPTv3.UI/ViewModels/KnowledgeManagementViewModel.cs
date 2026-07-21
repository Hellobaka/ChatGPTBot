using ChatGPTv3.Core.Api;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using MessageBox = HandyControl.Controls.MessageBox;
using ChatGPTv3.UI.Views;

namespace ChatGPTv3.UI.ViewModels;

/// <summary>
/// Represents a knowledge item retrieved from Qdrant.
/// </summary>
public partial class KnowledgeItem : ObservableObject
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _source = string.Empty;
    [ObservableProperty] private string _content = string.Empty;
    [ObservableProperty] private DateTime _createTime;

    public string DisplayText
    {
        get
        {
            var firstLine = (Content.IndexOf('\n') switch
            {
                -1 => Content,
                var idx => Content[..idx]
            }).Trim();
            return firstLine.Length > 60 ? firstLine[..60] + "…" : firstLine;
        }
    }

    partial void OnContentChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayText));
    }
}

/// <summary>
/// Represents a source group for TreeView display, containing child knowledge items.
/// </summary>
public partial class SourceGroup : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private int _count;
    [ObservableProperty] private ObservableCollection<KnowledgeItem> _items = [];
}

public partial class KnowledgeManagementViewModel : ViewModelBase
{
    private const string CollectionName = QdrantService.KnowledgeCollectionName;

    // ─── State ────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<SourceGroup> _sources = [];
    [ObservableProperty] private SourceGroup? _selectedSource;
    [ObservableProperty] private KnowledgeItem? _selectedItem;

    [ObservableProperty] private string _searchText = string.Empty;

    // Edit state
    [ObservableProperty] private string _editContent = string.Empty;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _originalContent = string.Empty;

    [ObservableProperty] private string _loadingStatus = string.Empty;

    // ─── Load ─────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (MemoryManager.Qdrant == null)
        {
            Growl.Error("Qdrant 未连接，请在系统设置中配置。");
            return;
        }

        IsLoading = true;
        try
        {
            await LoadSourcesAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
        Growl.Success("已刷新");
    }

    private async Task LoadSourcesAsync()
    {
        var counts = await MemoryManager.Qdrant!.CountBySourceAsync(CollectionName);
        var groups = counts
            .Where(kv => !string.IsNullOrEmpty(kv.Key))
            .OrderBy(kv => kv.Key)
            .Select(kv => new SourceGroup { Name = kv.Key, Count = kv.Value })
            .ToList();

        // Load items for each group (sorted by time descending)
        foreach (var group in groups)
        {
            var items = await LoadItemsForSourceAsync(group.Name);
            foreach (var item in items)
            {
                group.Items.Add(item);
            }
            group.Count = group.Items.Count;
        }

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            Sources.Clear();
            foreach (var g in groups)
            {
                Sources.Add(g);
            }

            if (SelectedSource == null && Sources.Count > 0)
            {
                SelectedSource = Sources[0];
            }
        });
    }

    private async Task<List<KnowledgeItem>> LoadItemsForSourceAsync(string source)
    {
        var allItems = await MemoryManager.Qdrant!.ScrollAsync(
            CollectionName, source, limit: 10000, offset: 0);

        // Trim content and sort by time descending
        var items = allItems
            .Select(i => new KnowledgeItem
            {
                Id = i.id,
                Source = i.source,
                Content = i.text.Trim(),
                CreateTime = i.time
            })
            .OrderBy(i => i.CreateTime)
            .ToList();

        return items;
    }

    // ─── Search ───────────────────────────────────────────

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (MemoryManager.Qdrant == null)
        {
            Growl.Error("Qdrant 未连接，请在系统设置中配置。");
            return;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            // Clear filter: reload all
            await LoadSourcesAsync();
            return;
        }

        IsLoading = true;
        try
        {
            // Semantic search: Qdrant with large recall
            int recallCount = AppConfig.EnableRerank ? 50 : AppConfig.MaxMemoryCount;

            LoadingStatus = "正在生成向量...";
            var results = await MemoryManager.Qdrant.SearchWithSourceAsync(
                SearchText, CollectionName, recallCount);

            if (results.Count == 0)
            {
                Growl.Info("未找到相关知识");
                return;
            }

            LoadingStatus = $"向量搜索完成，找到 {results.Count} 条结果...";

            // Rerank if enabled
            if (AppConfig.EnableRerank && results.Count > AppConfig.MaxMemoryCount)
            {
                LoadingStatus = "正在进行 Rerank 重排序...";
                var texts = results.Select(r => r.text).ToList();
                var reranked = await RerankService.RerankAsync(SearchText, texts);
                if (reranked.Count > 0)
                {
                    results = reranked.Select(r => results[r.index]).ToList();
                    LoadingStatus = "Rerank 完成，正在更新列表...";
                }
            }

            // Group by source
            var grouped = results
                .Select(r => new KnowledgeItem
                {
                    Id = r.id,
                    Source = r.source,
                    Content = r.text.Trim(),
                    CreateTime = r.time
                })
                .GroupBy(i => i.Source)
                .ToDictionary(g => g.Key, g => g.ToList());

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                // Update existing groups or create new ones
                foreach (var group in Sources.ToList())
                {
                    if (grouped.TryGetValue(group.Name, out var items))
                    {
                        group.Items.Clear();
                        foreach (var item in items)
                        {
                            group.Items.Add(item);
                        }
                        group.Count = group.Items.Count;
                    }
                    else
                    {
                        group.Items.Clear();
                        group.Count = 0;
                    }
                }
            });
        }
        finally
        {
            IsLoading = false;
            LoadingStatus = string.Empty;
        }
    }

    partial void OnSelectedSourceChanged(SourceGroup? value)
    {
        if (value != null)
        {
            SelectedItem = null;
            IsEditing = false;
        }
    }

    partial void OnSelectedItemChanged(KnowledgeItem? value)
    {
        if (value != null)
        {
            EditContent = value.Content;
            OriginalContent = value.Content;
            IsEditing = false;
        }
    }

    // ─── Edit ─────────────────────────────────────────────

    [RelayCommand]
    private void StartEdit()
    {
        if (SelectedItem == null) { return; }
        OriginalContent = SelectedItem.Content;
        EditContent = SelectedItem.Content;
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveEditAsync()
    {
        if (SelectedItem == null || MemoryManager.Qdrant == null) { return; }

        var confirm = MessageBox.Show(
            "修改内容后需要重新生成 Embedding 并更新到 Qdrant，这会产生 API 调用费用。\n\n是否继续？",
            "确认修改",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        var newContent = EditContent.Trim();
        var ok = await MemoryManager.Qdrant.InsertWithIdAsync(newContent, CollectionName, SelectedItem.Id);

        if (ok)
        {
            SelectedItem.Content = newContent;
            IsEditing = false;
            Growl.Success("已更新，Embedding 已重建");
        }
        else
        {
            Growl.Error("更新失败");
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        if (SelectedItem == null) { return; }
        EditContent = OriginalContent;
        IsEditing = false;
    }

    // ─── Delete ───────────────────────────────────────────

    [RelayCommand]
    private async Task DeleteAsync(KnowledgeItem? item)
    {
        if (item == null || MemoryManager.Qdrant == null) { return; }

        var confirm = MessageBox.Show(
            $"确定要删除这条知识吗？\n\n{item.Content[..Math.Min(item.Content.Length, 80)]}...",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) { return; }

        var ok = await MemoryManager.Qdrant.DeleteByIdAsync(CollectionName, item.Id);
        if (ok)
        {
            if (SelectedSource != null)
            {
                SelectedSource.Items.Remove(item);
                SelectedSource.Count = SelectedSource.Items.Count;
            }
            SelectedItem = null;
            Growl.Success("已删除");
        }
        else
        {
            Growl.Error("删除失败");
        }
    }

    [RelayCommand]
    private async Task DeleteSourceAsync(SourceGroup? source)
    {
        if (source == null || source.Name == "全部" || MemoryManager.Qdrant == null) { return; }

        var confirm = MessageBox.Show(
            $"确定要删除来源「{source.Name}」的全部 {source.Count} 条知识吗？\n此操作不可撤销。",
            "确认删除来源",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) { return; }

        var ok = await MemoryManager.Qdrant.DeleteBySourceAsync(CollectionName, source.Name);
        if (ok)
        {
            Sources.Remove(source);
            SelectedItem = null;
            Growl.Success($"已删除来源「{source.Name}」的全部知识");
        }
        else
        {
            Growl.Error("删除失败");
        }
    }

    // ─── Import ───────────────────────────────────────────

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (MemoryManager.Qdrant == null)
        {
            Growl.Error("Qdrant 未连接，请在系统设置中配置。");
            return;
        }

        var dialog = new ImportKnowledgeDialog
        {
            Owner = Application.Current.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(w => w.IsActive)
        };

        var result = dialog.ShowDialog();

        if (result == true)
        {
            await LoadSourcesAsync();
            Growl.Success("知识库导入完成");
        }
    }
}