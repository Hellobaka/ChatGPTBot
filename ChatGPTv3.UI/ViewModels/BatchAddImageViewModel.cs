using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Model;
using ChatGPTv3.Core.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;

namespace ChatGPTv3.UI.ViewModels;

public partial class ImportImageItem : ObservableObject
{
    public string SourcePath { get; init; } = "";

    [ObservableProperty]
    private string _fileName = "";

    [ObservableProperty]
    private string _md5 = "";

    /// <summary>Whether this image is an emoji/sticker.</summary>
    [ObservableProperty]
    private bool _isEmoji;

    /// <summary>
    /// One of: 待处理 / 待导入 / 复制中 / 描述中 / 向量化中 / 已导入 / 重复 / 失败
    /// </summary>
    [ObservableProperty]
    private string _status = "待处理";

    [ObservableProperty]
    private string _statusMessage = "";
}

public partial class BatchAddImageViewModel : ViewModelBase
{
    public ObservableCollection<ImportImageItem> Items { get; } = [];

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _importedCount;

    [ObservableProperty]
    private int _skippedCount;

    [ObservableProperty]
    private int _failedCount;

    /// <summary>Number of concurrent imports (1-30).</summary>
    [ObservableProperty]
    private int _concurrency = 3;

    /// <summary>Default value for IsEmoji when adding new items.</summary>
    [ObservableProperty]
    private bool _defaultIsEmoji = true;

    // Thread-safe counters for concurrent processing
    private int _importedCounter;
    private int _skippedCounter;
    private int _failedCounter;

    [ObservableProperty]
    private string _statusMessage = "选择图片文件开始导入";

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _isSaved;

    [ObservableProperty]
    private bool _isClosing;

    [RelayCommand]
    private void Cancel()
    {
        IsSaved = false;
        IsClosing = true;
    }

    [RelayCommand]
    private void SelectFiles()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|所有文件|*.*",
            Title = "选择要导入的图片"
        };

        if (dlg.ShowDialog() != true)
        {
            return;
        }

        Items.Clear();
        foreach (var path in dlg.FileNames)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            byte[] hashBytes;
            try
            {
                hashBytes = ComputeMD5(path);
            }
            catch (Exception ex)
            {
                Items.Add(new ImportImageItem
                {
                    SourcePath = path,
                    FileName = Path.GetFileName(path),
                    Status = "失败",
                    StatusMessage = $"MD5 计算失败: {ex.Message}"
                });
                continue;
            }

            var md5 = Convert.ToHexString(hashBytes).Replace("-", "").ToUpperInvariant();
            var item = new ImportImageItem
            {
                SourcePath = path,
                FileName = Path.GetFileName(path),
                Md5 = md5,
                IsEmoji = DefaultIsEmoji
            };

            var existing = Picture.FindByHash(md5);
            if (existing != null)
            {
                item.Status = "重复";
                item.StatusMessage = "数据库中已存在相同图片";
            }
            else
            {
                item.Status = "待导入";
            }

            Items.Add(item);
        }

        TotalCount = Items.Count;
        ImportedCount = 0;
        SkippedCount = Items.Count(i => i.Status == "重复");
        FailedCount = 0;
        StatusMessage = Items.Count > 0
            ? $"已选择 {Items.Count} 个文件，准备导入"
            : "未选择任何文件";
    }

    [RelayCommand]
    private async Task Import()
    {
        if (Items.Count == 0)
        {
            return;
        }

        IsProcessing = true;
        _importedCounter = 0;
        _skippedCounter = (int)Items.Count(i => i.Status == "重复");
        _failedCounter = (int)Items.Count(i => i.Status == "失败");
        ImportedCount = 0;
        SkippedCount = _skippedCounter;
        FailedCount = _failedCounter;
        StatusMessage = "开始导入...";

        var imageDir = CommonHelper.GetAppImageDirectory();
        Directory.CreateDirectory(imageDir);

        var pendingItems = Items.Where(i => i.Status is "待导入").ToList();
        var semaphore = new SemaphoreSlim(Concurrency);
        var tasks = pendingItems.Select(async item =>
        {
            await semaphore.WaitAsync();
            try
            {
                await ProcessSingleItemAsync(item, imageDir);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        ImportedCount = _importedCounter;
        SkippedCount = _skippedCounter;
        FailedCount = _failedCounter;

        IsProcessing = false;
        StatusMessage = $"导入完成：成功 {ImportedCount}，跳过 {SkippedCount}，失败 {FailedCount}";

        if (ImportedCount + SkippedCount + FailedCount == TotalCount)
        {
            IsSaved = true;
        }
    }

    private async Task ProcessSingleItemAsync(ImportImageItem item, string imageDir)
    {
        try
        {
            // Step 1: Copy file
            item.Status = "复制中";
            var ext = Path.GetExtension(item.SourcePath);
            if (string.IsNullOrEmpty(ext))
            {
                ext = ".png";
            }

            var destPath = Path.Combine(imageDir, $"{item.Md5}{ext}");
            if (!File.Exists(destPath))
            {
                File.Copy(item.SourcePath, destPath, overwrite: false);
            }

            // Step 2: Insert DB record
            var picture = new Picture
            {
                Md5 = item.Md5,
                FilePath = $"{item.Md5}{ext}",
                IsEmoji = item.IsEmoji,
                Description = "",
                Url = "",
                UseCount = 0,
                IsDeleted = false,
                LastUsedAt = null,
                Time = DateTime.Now
            };
            Picture.Upsert(picture);

            // Step 3: Describe via vision model (also indexes in Qdrant automatically)
            item.Status = "描述中";
            var description = await ImageScraper.DescribeAsync(destPath, isEmoji: item.IsEmoji);
            if (!string.IsNullOrEmpty(description))
            {
                item.Status = "已导入";
                item.StatusMessage = description;
            }
            else
            {
                item.Status = "已导入";
                item.StatusMessage = "文件已保存，描述失败";
            }

            Interlocked.Increment(ref _importedCounter);
        }
        catch (Exception ex)
        {
            item.Status = "失败";
            item.StatusMessage = ex.Message;
            Interlocked.Increment(ref _failedCounter);
        }
        finally
        {
            ImportedCount = _importedCounter;
            SkippedCount = _skippedCounter;
            FailedCount = _failedCounter;
        }
    }

    private static byte[] ComputeMD5(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        return md5.ComputeHash(stream);
    }
}
