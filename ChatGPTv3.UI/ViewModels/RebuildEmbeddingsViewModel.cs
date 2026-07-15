using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ChatGPTv3.UI.ViewModels;

public partial class RebuildEmbeddingsViewModel : ObservableObject
{
    [ObservableProperty]
    private int _total;

    [ObservableProperty]
    private int _current;

    [ObservableProperty]
    private int _successCount;

    [ObservableProperty]
    private int _failCount;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isCompleted;

    public double Progress => Total == 0 ? 0 : (double)Current / Total;

    [RelayCommand]
    private async Task StartAsync()
    {
        if (MemoryManager.Qdrant == null)
        {
            Growl.Error("Qdrant 未配置或未连接，无法重建向量索引。请在系统设置中启用 Qdrant。");
            return;
        }

        IsRunning = true;
        IsCompleted = false;
        StatusText = "正在查询图片...";
        Current = 0;
        SuccessCount = 0;
        FailCount = 0;
        OnPropertyChanged(nameof(Progress));

        List<Picture> pictures;
        try
        {
            using var db = SQLiteManager.GetInstance();
            pictures = db.Queryable<Picture>()
                .Where(p => p.IsEmoji && !p.IsDeleted && !string.IsNullOrEmpty(p.Description))
                .ToList();
        }
        catch (Exception ex)
        {
            Growl.Error($"查询图片失败: {ex.Message}");
            IsRunning = false;
            return;
        }

        Total = pictures.Count;
        OnPropertyChanged(nameof(Progress));

        if (Total == 0)
        {
            StatusText = "没有需要重建的图片";
            Growl.Info("没有找到符合条件的图片");
            IsRunning = false;
            IsCompleted = true;
            return;
        }

        StatusText = $"共 {Total} 张图片，开始重建...";

        foreach (var picture in pictures)
        {
            if (!IsRunning)
            {
                break;
            }

            try
            {
                var ok = await MemoryManager.Qdrant!.InsertWithIdAsync(
                    picture.Description,
                    QdrantService.ImageCollectionName,
                    picture.Md5.ToUpper());

                if (ok)
                {
                    SuccessCount++;
                }
                else
                {
                    FailCount++;
                }
            }
            catch
            {
                FailCount++;
            }

            Current++;
            var currentCopy = Current;
            var successCopy = SuccessCount;
            var failCopy = FailCount;
            var totalCopy = Total;
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                OnPropertyChanged(nameof(Current));
                OnPropertyChanged(nameof(Progress));
                StatusText = $"已处理 {currentCopy}/{totalCopy}（成功 {successCopy}，失败 {failCopy}）";
                OnPropertyChanged(nameof(StatusText));
            });
        }

        IsRunning = false;
        IsCompleted = true;
        StatusText = $"完成：成功 {SuccessCount}，失败 {FailCount}，共 {Total}";
        Growl.Success($"图片向量索引重建完成：成功 {SuccessCount}，失败 {FailCount}");
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsRunning)
        {
            IsRunning = false;
            StatusText = $"已取消：成功 {SuccessCount}，失败 {FailCount}，共处理 {Current}/{Total}";
            IsCompleted = true;
        }
    }
}
