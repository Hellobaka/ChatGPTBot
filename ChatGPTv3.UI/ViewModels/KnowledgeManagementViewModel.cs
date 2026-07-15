using ChatGPTv3.Core.Api;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Model;
using ChatGPTv3.Core.Utilities;
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
}

/// <summary>
/// Represents a source group for TreeView display.
/// </summary>
public partial class SourceGroup : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private int _count;
}

public partial class KnowledgeManagementViewModel : ViewModelBase
{
    private const string CollectionName = QdrantService.KnowledgeCollectionName;

    // ─── State ────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<KnowledgeItem> _items = [];
    [ObservableProperty] private ObservableCollection<SourceGroup> _sources = [];
    [ObservableProperty] private SourceGroup? _selectedSource;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _pageSize = 50;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private int _totalCount;

    public bool CanGoFirst => CurrentPage > 1;
    public bool CanGoPrev => CurrentPage > 1;
    public bool CanGoNext => CurrentPage < TotalPages;
    public bool CanGoLast => CurrentPage < TotalPages;

    partial void OnCurrentPageChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoFirst));
        OnPropertyChanged(nameof(CanGoPrev));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoLast));
    }

    partial void OnTotalPagesChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoLast));
    }

    [ObservableProperty] private bool _isImporting;
    [ObservableProperty] private int _importTotal;
    [ObservableProperty] private int _importCurrent;
    [ObservableProperty] private int _importSuccess;
    [ObservableProperty] private int _importFailed;
    [ObservableProperty] private string _importStatus = string.Empty;

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
            await LoadPageAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadSourcesAsync()
    {
        var counts = await MemoryManager.Qdrant!.CountBySourceAsync(CollectionName);
        var groups = counts
            .Where(kv => !string.IsNullOrEmpty(kv.Key))
            .OrderBy(kv => kv.Key)
            .Select(kv => new SourceGroup { Name = kv.Key, Count = kv.Value })
            .ToList();

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            Sources.Clear();
            foreach (var g in groups)
            {
                Sources.Add(g);
            }

            // Add "All" as first item
            var allCount = counts.Values.Sum();
            Sources.Insert(0, new SourceGroup { Name = "全部", Count = allCount });

            if (SelectedSource == null && Sources.Count > 0)
            {
                SelectedSource = Sources[0];
            }
        });
    }

    private async Task LoadPageAsync()
    {
        var sourceFilter = SelectedSource?.Name == "全部" ? null : SelectedSource?.Name;

        // Scroll all matching items (client-side filter for search)
        var allItems = await MemoryManager.Qdrant!.ScrollAsync(
            CollectionName, sourceFilter, limit: 10000, offset: 0);

        // Client-side search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.ToLowerInvariant();
            allItems = allItems.Where(i =>
                i.text.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                i.source.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        TotalCount = allItems.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
        if (CurrentPage > TotalPages)
        {
            CurrentPage = TotalPages;
        }

        var page = allItems
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .Select(i => new KnowledgeItem
            {
                Id = i.id,
                Source = i.source,
                Content = i.text,
                CreateTime = i.time
            })
            .ToList();

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            Items.Clear();
            foreach (var item in page)
            {
                Items.Add(item);
            }

            OnPropertyChanged(nameof(Items));
        });
    }

    // ─── Search / Filter ──────────────────────────────────

    [RelayCommand]
    private async Task SearchAsync()
    {
        CurrentPage = 1;
        await LoadPageAsync();
    }

    partial void OnSelectedSourceChanged(SourceGroup? value)
    {
        if (value != null)
            _ = LoadPageAsync();
    }

    // ─── Pagination ───────────────────────────────────────

    [RelayCommand]
    private async Task FirstPageAsync()
    {
        if (CurrentPage == 1)
        {
            return;
        }
        CurrentPage = 1;
        await LoadPageAsync();
    }

    [RelayCommand]
    private async Task PrevPageAsync()
    {
        if (CurrentPage <= 1)
        {
            return;
        }
        CurrentPage--;
        await LoadPageAsync();
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (CurrentPage >= TotalPages)
        {
            return;
        }
        CurrentPage++;
        await LoadPageAsync();
    }

    [RelayCommand]
    private async Task LastPageAsync()
    {
        if (CurrentPage >= TotalPages)
        {
            return;
        }
        CurrentPage = TotalPages;
        await LoadPageAsync();
    }

    partial void OnPageSizeChanged(int value)
    {
        if (value > 0)
            _ = LoadPageAsync();
    }

    // ─── Delete ───────────────────────────────────────────

    [RelayCommand]
    private async Task DeleteAsync(KnowledgeItem? item)
    {
        if (item == null || MemoryManager.Qdrant == null)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"确定要删除这条知识吗？\n\n{item.Content[..Math.Min(item.Content.Length, 80)]}...",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        var ok = await MemoryManager.Qdrant.DeleteByIdAsync(CollectionName, item.Id);
        if (ok)
        {
            Items.Remove(item);
            await LoadSourcesAsync();
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
        if (source == null || source.Name == "全部" || MemoryManager.Qdrant == null)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"确定要删除来源「{source.Name}」的全部 {source.Count} 条知识吗？\n此操作不可撤销。",
            "确认删除来源",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        var ok = await MemoryManager.Qdrant.DeleteBySourceAsync(CollectionName, source.Name);
        if (ok)
        {
            await LoadSourcesAsync();
            await LoadPageAsync();
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
            await LoadPageAsync();
            Growl.Success("知识库导入完成");
        }
    }
}
