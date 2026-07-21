using ChatGPTv3.Core.Api;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using System.IO;
using System.Text;

namespace ChatGPTv3.UI.ViewModels;

public partial class ImportKnowledgeViewModel : ObservableObject
{
    private const string CollectionName = QdrantService.KnowledgeCollectionName;

    [ObservableProperty] private string _sourceTitle = string.Empty;
    [ObservableProperty] private string _importText = string.Empty;
    [ObservableProperty] private bool _isImporting;
    [ObservableProperty] private int _importTotal;
    [ObservableProperty] private int _importCurrent;
    [ObservableProperty] private int _importSuccess;
    [ObservableProperty] private int _importFailed;
    [ObservableProperty] private string _importStatus = string.Empty;
    [ObservableProperty] private bool _canEdit = true;
    [ObservableProperty] private bool _closing = false;

    [ObservableProperty] private int _chunkSize = 300;
    [ObservableProperty] private int _chunkOverlap = 50;
    [ObservableProperty] private string _previewResult = string.Empty;
    [ObservableProperty] private int _previewCount;

    public bool HasPreviewResult => !string.IsNullOrWhiteSpace(PreviewResult);

    public bool DialogResult { get; private set; }

    [RelayCommand]
    private void BrowseFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "文本文件|*.txt;*.md;*.csv|所有文件|*.*",
            Title = "选择要导入的文本文件"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                ImportText = File.ReadAllText(dlg.FileName);
                if (string.IsNullOrWhiteSpace(SourceTitle))
                {
                    SourceTitle = Path.GetFileNameWithoutExtension(dlg.FileName);
                }
            }
            catch (Exception ex)
            {
                Growl.Error($"读取文件失败：{ex.Message}");
            }
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceTitle))
        {
            Growl.Error("请输入来源标题");
            return;
        }

        if (string.IsNullOrWhiteSpace(ImportText))
        {
            Growl.Error("请输入要导入的文本内容");
            return;
        }

        if (MemoryManager.Qdrant == null)
        {
            Growl.Error("Qdrant 未连接，请在系统设置中配置。");
            return;
        }

        IsImporting = true;
        CanEdit = false;
        ImportTotal = 0;
        ImportCurrent = 0;
        ImportSuccess = 0;
        ImportFailed = 0;
        ImportStatus = "正在切分文本...";

        try
        {
            var chunks = RecursiveChunker.Chunk(ImportText, ChunkSize, ChunkOverlap);

            ImportTotal = chunks.Length;
            ImportStatus = $"共 {chunks.Length} 段，开始导入...";

            foreach (var chunk in chunks)
            {
                if (string.IsNullOrWhiteSpace(chunk))
                {
                    continue;
                }

                var ok = await MemoryManager.Qdrant.InsertWithSourceAsync(chunk, CollectionName, SourceTitle);
                if (ok)
                {
                    ImportSuccess++;
                }
                else
                {
                    ImportFailed++;
                }

                ImportCurrent++;
                ImportStatus = $"已处理 {ImportCurrent}/{ImportTotal}（成功 {ImportSuccess}，失败 {ImportFailed}）";
            }

            ImportStatus = $"导入完成：成功 {ImportSuccess}，失败 {ImportFailed}";
            DialogResult = true;
        }
        catch (Exception ex)
        {
            ImportStatus = $"导入失败：{ex.Message}";
            DialogResult = false;
        }
        finally
        {
            IsImporting = false;
            CanEdit = true;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsImporting)
        {
            return;
        }

        Closing = true;
        DialogResult = false;
    }

    [RelayCommand]
    private void Preview()
    {
        if (string.IsNullOrWhiteSpace(ImportText))
        {
            PreviewResult = "请先输入或选择要导入的文本内容。";
            PreviewCount = 0;
            return;
        }

        var chunks = RecursiveChunker.Chunk(ImportText, ChunkSize, ChunkOverlap);
        PreviewCount = chunks.Length;
        var sb = new StringBuilder();
        sb.AppendLine($"共切分为 {chunks.Length} 段：");
        for (int i = 0; i < chunks.Length; i++)
        {
            var preview = chunks[i].Length > 80 ? chunks[i][..80] + "…" : chunks[i];
            sb.AppendLine($"[{i + 1}] ({chunks[i].Length} 字) {preview}");
        }
        PreviewResult = sb.ToString();
        OnPropertyChanged(nameof(HasPreviewResult));
    }
}
