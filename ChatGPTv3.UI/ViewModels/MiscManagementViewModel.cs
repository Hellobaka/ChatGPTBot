using ChatGPTv3.Core;
using ChatGPTv3.Core.Model;
using ChatGPTv3.Core.DB;
using ChatGPTv3.UI.Mock;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using MessageBox = HandyControl.Controls.MessageBox;

namespace ChatGPTv3.UI.ViewModels;

// ═══════════════════════════════════════════════════════════
//  Diary types
// ═══════════════════════════════════════════════════════════

public partial class DiaryEntryItem : ObservableObject
{
    [ObservableProperty] private DateTime _time;
    [ObservableProperty] private string _text = string.Empty;

    public string DisplayText
    {
        get
        {
            var firstLine = (Text.IndexOf('\n') switch
            {
                -1 => Text,
                var idx => Text[..idx]
            }).Trim();
            return firstLine.Length > 80 ? firstLine[..80] + "…" : firstLine;
        }
    }

    partial void OnTextChanged(string value) => OnPropertyChanged(nameof(DisplayText));
}

public partial class DiaryGroupItem : ObservableObject
{
    [ObservableProperty] private long _groupId;
    [ObservableProperty] private string _groupName = string.Empty;
    [ObservableProperty] private ObservableCollection<DiaryEntryItem> _entries = [];
}

// ═══════════════════════════════════════════════════════════
//  Schedule types
// ═══════════════════════════════════════════════════════════

public partial class ScheduleEntryItem : ObservableObject
{
    [ObservableProperty] private DateTime _time;
    [ObservableProperty] private string _action = string.Empty;

    public string TimeString => Time.ToString("HH:mm");
}

// ═══════════════════════════════════════════════════════════
//  Relationship types
// ═══════════════════════════════════════════════════════════

public partial class RelationshipItem : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private long _groupId;
    [ObservableProperty] private long _userQq;
    [ObservableProperty] private string _nickName = string.Empty;
    [ObservableProperty] private int _favorability;
    [ObservableProperty] private int _interactionCount;
    [ObservableProperty] private DateTime _lastInteractionTime;

    public string GroupLabel => GroupId == 0 ? "私聊" : $"群 {GroupId}";
}

public partial class RelationshipGroupItem : ObservableObject
{
    [ObservableProperty] private long _groupId;
    [ObservableProperty] private string _groupName = string.Empty;
    [ObservableProperty] private ObservableCollection<RelationshipItem> _items = [];
}

// ═══════════════════════════════════════════════════════════
//  Main ViewModel
// ═══════════════════════════════════════════════════════════

public partial class MiscManagementViewModel : ViewModelBase
{
    // ─── Diary state ───────────────────────────────────

    [ObservableProperty] private ObservableCollection<DiaryGroupItem> _diaryGroups = [];
    [ObservableProperty] private DiaryGroupItem? _selectedDiaryGroup;
    [ObservableProperty] private DiaryEntryItem? _selectedDiaryEntry;
    [ObservableProperty] private string _editDiaryText = string.Empty;
    [ObservableProperty] private bool _isEditingDiary;
    [ObservableProperty] private DateTime? _diarySearchDate;

    // ─── Schedule state ────────────────────────────────

    [ObservableProperty] private ObservableCollection<ScheduleEntryItem> _schedules = [];
    [ObservableProperty] private ScheduleEntryItem? _selectedSchedule;
    [ObservableProperty] private string _editScheduleTime = string.Empty;
    [ObservableProperty] private string _editScheduleAction = string.Empty;
    [ObservableProperty] private bool _isEditingSchedule;

    // ─── Relationship state ────────────────────────────

    [ObservableProperty] private ObservableCollection<RelationshipGroupItem> _relationshipGroups = [];
    [ObservableProperty] private RelationshipItem? _selectedRelationship;
    [ObservableProperty] private int _editFavorability;
    [ObservableProperty] private bool _isEditingRelationship;

    // New relationship
    [ObservableProperty] private long _newRelationshipGroupId;
    [ObservableProperty] private long _newRelationshipQQ;
    [ObservableProperty] private string _newRelationshipNickName = string.Empty;
    [ObservableProperty] private int _newRelationshipFavorability = 50;

    // Group list for new relationship dropdown
    [ObservableProperty] private ObservableCollection<RelationshipGroupItem> _availableGroups = [];

    // ─── Commands ──────────────────────────────────────

    [RelayCommand]
    private async Task LoadDiariesAsync()
    {
        IsLoading = true;
        LoadingStatus = "正在加载日记...";
        try
        {
            await Task.Run(() =>
            {
                var allDiaries = DiaryMemoryManager.GetAllDiaries();
                var groups = Entry.ApiGroup?.GetGroupList() ?? MockGroupApi.Instance.GetGroupList();
                var groupDict = groups.ToDictionary(g => g.Group, g => g.Name ?? $"群 {g.Group}");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    DiaryGroups.Clear();
                    foreach (var (groupId, entries) in allDiaries.OrderByDescending(x => x.Key))
                    {
                        var groupName = groupDict.TryGetValue(groupId, out var name)
                            ? name
                            : $"群 {groupId}";

                        var groupItem = new DiaryGroupItem
                        {
                            GroupId = groupId,
                            GroupName = groupName
                        };

                        foreach (var (time, text) in entries.OrderByDescending(x => x.time))
                        {
                            groupItem.Entries.Add(new DiaryEntryItem { Time = time, Text = text });
                        }

                        DiaryGroups.Add(groupItem);
                    }
                });
            });
            LoadingStatus = "日记加载完成";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载日记失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectDiaryGroup(DiaryGroupItem? group)
    {
        SelectedDiaryEntry = null;
        IsEditingDiary = false;
    }

    [RelayCommand]
    private void SelectDiaryEntry(DiaryEntryItem? entry)
    {
        if (entry == null)
        {
            return;
        }
        EditDiaryText = entry.Text;
        IsEditingDiary = false;
    }

    [RelayCommand]
    private void StartEditDiary()
    {
        IsEditingDiary = true;
    }

    [RelayCommand]
    private async Task SaveDiaryAsync()
    {
        if (SelectedDiaryGroup == null || SelectedDiaryEntry == null)
        {
            return;
        }

        var groupId = SelectedDiaryGroup.GroupId;
        var index = SelectedDiaryGroup.Entries.IndexOf(SelectedDiaryEntry);

        try
        {
            await Task.Run(() =>
            {
                DiaryMemoryManager.SaveDiaryEntry(groupId, index, EditDiaryText);
            });

            SelectedDiaryEntry.Text = EditDiaryText;
            OnPropertyChanged(nameof(SelectedDiaryEntry.DisplayText));
            IsEditingDiary = false;
            Growl.Success("日记已保存");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"保存失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CancelEditDiary()
    {
        if (SelectedDiaryEntry != null)
        {
            EditDiaryText = SelectedDiaryEntry.Text;
        }
        IsEditingDiary = false;
    }

    // ─── Schedule commands ─────────────────────────────

    [RelayCommand]
    private async Task LoadSchedulesAsync()
    {
        IsLoading = true;
        LoadingStatus = "正在加载日程...";
        try
        {
            var schedules = await Task.Run(() =>
            {
                return SchedulerManager.Instance?.GetAllSchedules() ?? [];
            });

            Application.Current.Dispatcher.Invoke(() =>
            {
                Schedules.Clear();
                foreach (var (time, action) in schedules)
                {
                    Schedules.Add(new ScheduleEntryItem { Time = time, Action = action });
                }
            });
            LoadingStatus = "日程加载完成";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载日程失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectSchedule(ScheduleEntryItem? item)
    {
        if (item == null)
        {
            return;
        }
        EditScheduleTime = item.Time.ToString("HH:mm");
        EditScheduleAction = item.Action;
        IsEditingSchedule = false;
    }

    [RelayCommand]
    private void StartEditSchedule()
    {
        IsEditingSchedule = true;
    }

    [RelayCommand]
    private async Task SaveScheduleAsync()
    {
        if (SelectedSchedule == null)
        {
            return;
        }

        try
        {
            await Task.Run(() =>
            {
                SchedulerManager.Instance?.UpdateSchedule(EditScheduleTime, EditScheduleAction);
            });

            // Refresh
            await LoadSchedulesAsync();
            IsEditingSchedule = false;
            Growl.Success("日程已保存");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"保存失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CancelEditSchedule()
    {
        if (SelectedSchedule != null)
        {
            EditScheduleTime = SelectedSchedule.Time.ToString("HH:mm");
            EditScheduleAction = SelectedSchedule.Action;
        }
        IsEditingSchedule = false;
    }

    [RelayCommand]
    private async Task RegenerateSchedulesAsync()
    {
        if (MessageBox.Show("确定要重新生成今日日程吗？", "确认",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        IsLoading = true;
        LoadingStatus = "正在调用 LLM 重新生成日程...";
        try
        {
            if (SchedulerManager.Instance != null)
            {
                await SchedulerManager.Instance.RegenerateAsync();
            }
            await LoadSchedulesAsync();
            Growl.Success("日程已重新生成");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"生成失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ─── Relationship commands ─────────────────────────

    [RelayCommand]
    private async Task LoadRelationshipsAsync()
    {
        IsLoading = true;
        LoadingStatus = "正在加载关系数据...";
        try
        {
            var relationships = await Task.Run(() => Relationship.GetAll());
            var groups = Entry.ApiGroup?.GetGroupList() ?? MockGroupApi.Instance.GetGroupList();
            var groupDict = groups.ToDictionary(g => g.Group, g => g.Name ?? $"群 {g.Group}");

            Application.Current.Dispatcher.Invoke(() =>
            {
                // Build groups
                var grouped = relationships.GroupBy(r => r.GroupID)
                    .OrderBy(g => g.Key == 0 ? -1 : g.Key);

                RelationshipGroups.Clear();
                AvailableGroups.Clear();

                foreach (var g in grouped)
                {
                    var groupId = g.Key;
                    var groupName = groupId == 0 ? "私聊" : (groupDict.TryGetValue(groupId, out var name) ? name : $"群 {groupId}");

                    var groupItem = new RelationshipGroupItem
                    {
                        GroupId = groupId,
                        GroupName = groupName
                    };

                    foreach (var r in g.OrderBy(x => x.QQ))
                    {
                        groupItem.Items.Add(new RelationshipItem
                        {
                            Id = r.Id,
                            GroupId = r.GroupID,
                            UserQq = r.QQ,
                            NickName = r.NickName,
                            Favorability = r.Favorability,
                            InteractionCount = r.InteractionCount,
                            LastInteractionTime = r.LastInteractionTime
                        });
                    }

                    RelationshipGroups.Add(groupItem);
                    AvailableGroups.Add(groupItem);
                }
            });
            LoadingStatus = "关系数据加载完成";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectRelationship(RelationshipItem? item)
    {
        if (item == null)
        {
            return;
        }
        EditFavorability = item.Favorability;
        IsEditingRelationship = false;
    }

    [RelayCommand]
    private void StartEditRelationship()
    {
        IsEditingRelationship = true;
    }

    [RelayCommand]
    private async Task SaveRelationshipAsync()
    {
        if (SelectedRelationship == null)
        {
            return;
        }

        try
        {
            await Task.Run(() =>
            {
                SelectedRelationship.Favorability = EditFavorability;
                Relationship.Update(new Relationship
                {
                    Id = SelectedRelationship.Id,
                    GroupID = SelectedRelationship.GroupId,
                    QQ = SelectedRelationship.UserQq,
                    NickName = SelectedRelationship.NickName,
                    Favorability = EditFavorability,
                    InteractionCount = SelectedRelationship.InteractionCount,
                    LastInteractionTime = SelectedRelationship.LastInteractionTime,
                    LastUpdateTime = DateTime.Now
                });
            });

            SelectedRelationship.Favorability = EditFavorability;
            IsEditingRelationship = false;
            Growl.Success("关系已保存");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"保存失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CancelEditRelationship()
    {
        if (SelectedRelationship != null)
        {
            EditFavorability = SelectedRelationship.Favorability;
        }
        IsEditingRelationship = false;
    }

    [RelayCommand]
    private async Task AddRelationshipAsync()
    {
        if (NewRelationshipQQ <= 0)
        {
            Growl.Error("请输入有效的 QQ 号");
            return;
        }

        try
        {
            await Task.Run(() =>
            {
                Relationship.Create(NewRelationshipGroupId, NewRelationshipQQ, NewRelationshipNickName.Trim());
            });

            NewRelationshipQQ = 0;
            NewRelationshipNickName = string.Empty;
            NewRelationshipFavorability = 50;
            await LoadRelationshipsAsync();
            Growl.Success("关系已添加");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"添加失败: {ex.Message}";
        }
    }

    // ─── Init ──────────────────────────────────────────

    public MiscManagementViewModel()
    {
        // Load all tabs on startup
        _ = LoadDiariesAsync();
        _ = LoadSchedulesAsync();
        _ = LoadRelationshipsAsync();
    }
}
