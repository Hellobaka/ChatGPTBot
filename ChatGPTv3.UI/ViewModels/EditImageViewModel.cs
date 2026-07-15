using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Model;
using ChatGPTv3.Core.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using System.IO;
using System.Linq;
using System.Windows;

namespace ChatGPTv3.UI.ViewModels;

public partial class EditImageViewModel : ViewModelBase
{
    private readonly Picture _picture;
    private readonly string _absoluteFilePath;
    private string _originalDescription = string.Empty;

    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _extraPrompt = string.Empty;
    [ObservableProperty] private bool _isRedescribing;
    [ObservableProperty] private bool _isSaving;

    /// <summary>Set to true by the view when save completes successfully.</summary>
    public bool IsSaved { get; private set; }

    public EditImageViewModel(Picture picture)
    {
        _picture = picture;
        Description = picture.Description;
        _originalDescription = picture.Description;

        // Resolve absolute file path
        if (Path.IsPathRooted(picture.FilePath))
        {
            _absoluteFilePath = picture.FilePath;
        }
        else
        {
            _absoluteFilePath = Path.Combine(CommonHelper.GetAppImageDirectory(), picture.FilePath);
        }
    }

    [RelayCommand]
    private async Task RedescribeAsync()
    {
        if (string.IsNullOrEmpty(_absoluteFilePath) || !File.Exists(_absoluteFilePath))
        {
            ErrorMessage = $"图片文件不存在，无法重新描述：{_absoluteFilePath}";
            return;
        }

        try
        {
            IsRedescribing = true;
            ErrorMessage = null;

            var result = await ImageScraper.RedescribeAsync(_absoluteFilePath, ExtraPrompt);
            if (result == null)
            {
                ErrorMessage = "重新描述失败：视觉模型未返回结果";
                return;
            }

            Description = result;
            Growl.Success("图片描述已更新");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"重新描述失败: {ex.Message}";
        }
        finally
        {
            IsRedescribing = false;
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        IsSaving = true;
        try
        {
            // If description changed, re-index in Qdrant before saving
            if (Description != _originalDescription)
            {
                if (string.IsNullOrWhiteSpace(Description))
                {
                    ErrorMessage = "描述不能为空";
                    return;
                }

                // Re-index in Qdrant
                var qdrantOk = await (MemoryManager.Qdrant?.InsertWithIdAsync(
                    Description, QdrantService.ImageCollectionName, _picture.Md5) ?? Task.FromResult(false));

                if (!qdrantOk)
                {
                    ErrorMessage = "向量索引失败，无法保存。请检查 Qdrant 配置是否正确。";
                    return;
                }

                // Update the picture's description
                _picture.Description = Description;
            }

            Picture.Upsert(_picture);
            IsSaved = true;
            Growl.Success("保存成功");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"保存失败: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        // Signal close without saving
        if (Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.DataContext == this) is System.Windows.Window window)
        {
            window.DialogResult = false;
            window.Close();
        }
    }
}
