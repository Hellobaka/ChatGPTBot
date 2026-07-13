using ChatGPTv3.Core.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChatGPTv3.UI.ViewModels;

public partial class ModelEditDialogViewModel : ObservableObject
{
    public string WindowTitle { get; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _enabled = true;
    [ObservableProperty] private decimal _inputPricePer1M;
    [ObservableProperty] private decimal _outputPricePer1M;
    [ObservableProperty] private decimal _cachePricePer1M;

    // Capability flags
    [ObservableProperty] private bool _isChat = true;
    [ObservableProperty] private bool _isImage;
    [ObservableProperty] private bool _isEmbedding;
    [ObservableProperty] private bool _isRerank;

    public bool IsSaved { get; set; }

    public ModelEditDialogViewModel()
    {
        WindowTitle = "新增模型";
    }

    public ModelEditDialogViewModel(ProviderModelItem source)
    {
        WindowTitle = $"编辑模型 - {source.Name}";
        Name = source.Name;
        Enabled = source.Enabled;
        InputPricePer1M = source.InputPricePer1M;
        OutputPricePer1M = source.OutputPricePer1M;
        CachePricePer1M = source.CachePricePer1M;
        IsChat = (source.Capabilities & ModelCapability.Chat) != 0;
        IsImage = (source.Capabilities & ModelCapability.Image) != 0;
        IsEmbedding = (source.Capabilities & ModelCapability.Embedding) != 0;
        IsRerank = (source.Capabilities & ModelCapability.Rerank) != 0;
    }

    public ModelCapability GetCapabilities()
    {
        var caps = (ModelCapability)0;
        if (IsChat)
        {
            caps |= ModelCapability.Chat;
        }
        if (IsImage)
        {
            caps |= ModelCapability.Image;
        }
        if (IsEmbedding)
        {
            caps |= ModelCapability.Embedding;
        }
        if (IsRerank)
        {
            caps |= ModelCapability.Rerank;
        }
        return caps;
    }

    [RelayCommand]
    private void Save()
    {
        IsSaved = true;
        OnPropertyChanged(nameof(IsSaved));
    }

    [RelayCommand]
    private void Cancel()
    {
        IsSaved = false;
        OnPropertyChanged(nameof(IsSaved));
    }
}
