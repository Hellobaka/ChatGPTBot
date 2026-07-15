using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using MessageBox = HandyControl.Controls.MessageBox;

namespace ChatGPTv3.UI.ViewModels;

public partial class PictureItem : ObservableObject
{
    public int Id { get; set; }
    public string Md5 { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public bool IsEmoji { get; set; }
    public string Description { get; set; } = string.Empty;
    public int UseCount { get; set; }
    public DateTime Time { get; set; }
    public BitmapImage? Thumbnail { get; set; }
    public bool FileExists { get; set; }

    public string Md5Short => Md5.Length > 8 ? Md5[..8] : Md5;
    public string TimeDisplay => Time.ToString("yyyy-MM-dd HH:mm");
}

public partial class ImageManagementViewModel : ViewModelBase
{
    public ObservableCollection<PictureItem> Pictures { get; } = [];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _showEmojiOnly;
    [ObservableProperty] private bool _showDeleted;

    public ImageManagementViewModel()
    {
        _ = LoadPicturesAsync();
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        await LoadPicturesAsync();
        Growl.Success("图片列表已刷新");
    }

    [RelayCommand]
    private async Task DeleteAsync(PictureItem? item)
    {
        if (item == null)
        {
            return;
        }

        if (MessageBox.Show(
                $"确定要删除图片「{item.Md5Short}」吗？",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            IsLoading = true;
            Picture.Delete(item.Md5);
            Pictures.Remove(item);
            Growl.Success("图片已删除");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"删除失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanFilter))]
    private void ApplyFilter()
    {
        _ = LoadPicturesAsync();
    }

    private bool CanFilter() => true;

    [RelayCommand]
    private void OpenRebuildEmbeddings()
    {
        var owner = Application.Current?.Windows
            .OfType<System.Windows.Window>()
            .FirstOrDefault();
        var dialog = new Views.RebuildEmbeddingsView { Owner = owner };
        dialog.Show();
    }

    [RelayCommand]
    private void OpenBatchImport()
    {
        var owner = Application.Current?.Windows
            .OfType<System.Windows.Window>()
            .FirstOrDefault(w => w.IsActive || w.IsVisible);
        var dialog = new Views.BatchAddImageView { Owner = owner };
        dialog.ShowDialog();
        if (dialog.DialogResult == true)
        {
            Growl.Success("批量导入完成");
            _ = LoadPicturesAsync();
        }
    }

    [RelayCommand]
    private void Edit(PictureItem? item)
    {
        if (item == null)
        {
            return;
        }

        var picture = Picture.FindByHash(item.Md5);
        if (picture == null)
        {
            ErrorMessage = "未找到对应的图片记录";
            return;
        }

        var owner = Application.Current?.Windows
            .OfType<System.Windows.Window>()
            .FirstOrDefault(w => w.IsActive || w.IsVisible);
        var dialog = new Views.EditImageView(picture) { Owner = owner };
        var result = dialog.ShowDialog();

        if (result == true)
        {
            // Refresh the item in the list from the database
            item.Description = picture.Description;
            Growl.Success("图片信息已更新");
        }
    }

    private async Task LoadPicturesAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var pictures = Picture.GetActive();

            // Apply filters in memory (list is typically small)
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.ToLowerInvariant();
                pictures = pictures.Where(p =>
                    p.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    p.Md5.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (ShowEmojiOnly)
            {
                pictures = pictures.Where(p => p.IsEmoji).ToList();
            }

            var imageDir = CommonHelper.GetAppImageDirectory();
            var items = new List<PictureItem>(pictures.Count);

            await Task.Run(() =>
            {
                foreach (var p in pictures)
                {
                    var item = new PictureItem
                    {
                        Id = p.Id,
                        Md5 = p.Md5,
                        FilePath = p.FilePath,
                        IsEmoji = p.IsEmoji,
                        Description = p.Description,
                        UseCount = p.UseCount,
                        Time = p.Time,
                    };

                    // Resolve file path: FilePath may be relative or absolute
                    string fullPath = item.FilePath;
                    if (!Path.IsPathRooted(fullPath))
                    {
                        fullPath = Path.Combine(imageDir, fullPath);
                    }

                    item.FileExists = File.Exists(fullPath);

                    if (item.FileExists)
                    {
                        try
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.DecodePixelWidth = 80;
                            bmp.DecodePixelHeight = 80;
                            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                            bmp.StreamSource = stream;
                            bmp.EndInit();
                            bmp.Freeze();
                            item.Thumbnail = bmp;
                        }
                        catch
                        {
                            // If thumbnail fails, show placeholder
                            item.Thumbnail = null;
                        }
                    }

                    items.Add(item);
                }
            });

            Application.Current.Dispatcher.Invoke(() =>
            {
                Pictures.Clear();
                foreach (var item in items)
                {
                    Pictures.Add(item);
                }
            });
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
}
