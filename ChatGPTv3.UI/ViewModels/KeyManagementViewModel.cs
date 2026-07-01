using System.Collections.ObjectModel;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;

namespace ChatGPTv3.UI.ViewModels;

public partial class ProviderModelItem : ObservableObject
{
    public int Id { get; set; }
    public int APIKeyId { get; set; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _enabled = true;
    [ObservableProperty] private decimal _inputPricePer1M;
    [ObservableProperty] private decimal _outputPricePer1M;
    [ObservableProperty] private decimal _cachePricePer1M;

    public ProviderModelItem Clone() => new()
    {
        Id = Id,
        APIKeyId = APIKeyId,
        Name = Name,
        Enabled = Enabled,
        InputPricePer1M = InputPricePer1M,
        OutputPricePer1M = OutputPricePer1M,
        CachePricePer1M = CachePricePer1M
    };
}

public partial class ProviderItem : ObservableObject
{
    public int Id { get; set; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _endPoint = string.Empty;
    [ObservableProperty] private string _key = string.Empty;
    [ObservableProperty] private bool _useTencentSign;
    [ObservableProperty] private long _totalTokens;
    [ObservableProperty] private decimal _totalConsume;

    public ObservableCollection<ProviderModelItem> Models { get; } = [];

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"Provider #{Id}" : Name;

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(DisplayName));

    public ProviderItem Clone()
    {
        var clone = new ProviderItem
        {
            Id = Id,
            Name = Name,
            EndPoint = EndPoint,
            Key = Key,
            UseTencentSign = UseTencentSign,
            TotalTokens = TotalTokens,
            TotalConsume = TotalConsume
        };
        foreach (var model in Models)
            clone.Models.Add(model.Clone());
        return clone;
    }
}

public partial class ModelSpendSummaryItem : ObservableObject
{
    public string ModelName { get; init; } = string.Empty;
    public int CallCount { get; init; }
    public int PromptTokens { get; init; }
    public int CachedPromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public int TotalTokens { get; init; }
    public decimal EstimatedConsume { get; init; }
    public string ConsumeText => EstimatedConsume.ToString("F4");
}

public partial class PurposeBindingItem : ObservableObject
{
    public PurposeBindingGroup? ParentGroup { get; set; }
    [ObservableProperty] private ProviderItem? _selectedProvider;
    [ObservableProperty] private ProviderModelItem? _selectedModel;

    public ObservableCollection<ProviderModelItem> AvailableModels { get; } = [];

    partial void OnSelectedProviderChanged(ProviderItem? value)
    {
        AvailableModels.Clear();
        if (value != null)
        {
            foreach (var model in value.Models.Where(x => x.Enabled))
                AvailableModels.Add(model);
        }

        if (SelectedModel != null && !AvailableModels.Any(x => x.Name == SelectedModel.Name))
            SelectedModel = AvailableModels.FirstOrDefault();
    }
}

public partial class PurposeBindingGroup : ObservableObject
{
    public string ConfigKey { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public ObservableCollection<PurposeBindingItem> Items { get; } = [];
}

public partial class KeyManagementViewModel : ViewModelBase
{
    public ObservableCollection<ProviderItem> Providers { get; } = [];
    public ObservableCollection<ModelSpendSummaryItem> SelectedProviderSpend { get; } = [];
    public ObservableCollection<PurposeBindingGroup> PurposeGroups { get; } = [];

    [ObservableProperty] private ProviderItem? _selectedProvider;
    [ObservableProperty] private ProviderModelItem? _selectedModel;

    public KeyManagementViewModel()
    {
        PurposeGroups.Add(new PurposeBindingGroup { ConfigKey = "ChatAPIKeyId", Title = "聊天模型", Description = "群聊 / 主对话使用的 Key 与模型" });
        PurposeGroups.Add(new PurposeBindingGroup { ConfigKey = "ReplyAPIKeyId", Title = "回复决策模型", Description = "边界回复判断使用的模型" });
        PurposeGroups.Add(new PurposeBindingGroup { ConfigKey = "SplitterApiKeyId", Title = "分段模型", Description = "分段和润色使用的模型" });
        PurposeGroups.Add(new PurposeBindingGroup { ConfigKey = "ImageDescriberApiKeyId", Title = "图像描述模型", Description = "视觉描述使用的模型" });
        PurposeGroups.Add(new PurposeBindingGroup { ConfigKey = "SummarizerApiKeyId", Title = "工具总结模型", Description = "工具结果总结使用的模型" });
        PurposeGroups.Add(new PurposeBindingGroup { ConfigKey = "DiaryAPIKeyId", Title = "日记模型", Description = "日记与回顾使用的模型" });
        PurposeGroups.Add(new PurposeBindingGroup { ConfigKey = "EmbeddingApiKeyId", Title = "Embedding 模型", Description = "向量嵌入使用的模型" });
        PurposeGroups.Add(new PurposeBindingGroup { ConfigKey = "RerankApiKeyId", Title = "Rerank 模型", Description = "重排序使用的模型" });

        LoadData();
    }

    partial void OnSelectedProviderChanged(ProviderItem? value)
    {
        SelectedModel = value?.Models.FirstOrDefault();
        ReloadSelectedProviderSpend();
    }

    [RelayCommand]
    private void Reload()
    {
        LoadData();
        Growl.Success("接口配置已刷新");
    }

    [RelayCommand]
    private void AddProvider()
    {
        var provider = new ProviderItem
        {
            Name = "新服务商",
            EndPoint = "https://api.openai.com/v1"
        };
        provider.Models.Add(new ProviderModelItem { Name = "gpt-4o", Enabled = true });
        Providers.Add(provider);
        SelectedProvider = provider;
    }

    [RelayCommand]
    private void RemoveProvider(ProviderItem? provider)
    {
        if (provider == null) return;

        foreach (var group in PurposeGroups)
        {
            var stale = group.Items.Where(x => x.SelectedProvider == provider).ToList();
            foreach (var item in stale)
                group.Items.Remove(item);
        }

        if (provider.Id > 0)
            APIKeyRepository.Delete(provider.Id);

        Providers.Remove(provider);
        if (SelectedProvider == provider)
            SelectedProvider = Providers.FirstOrDefault();
    }

    [RelayCommand]
    private void AddModel()
    {
        if (SelectedProvider == null)
        {
            ErrorMessage = "请先在顶部选择一个服务商，或点击「新增服务商」创建";
            return;
        }
        var model = new ProviderModelItem { Name = "new-model", Enabled = true };
        SelectedProvider.Models.Add(model);
        SelectedModel = model;
        ErrorMessage = null;
    }

    [RelayCommand]
    private void RemoveModel(ProviderModelItem? model)
    {
        if (SelectedProvider == null || model == null) return;

        foreach (var group in PurposeGroups)
        {
            foreach (var item in group.Items.Where(x => x.SelectedProvider == SelectedProvider && x.SelectedModel?.Name == model.Name))
                item.SelectedModel = null;
        }

        SelectedProvider.Models.Remove(model);
        if (SelectedModel == model)
            SelectedModel = SelectedProvider.Models.FirstOrDefault();
    }

    [RelayCommand]
    private void AddPurposeBinding(PurposeBindingGroup? group)
    {
        if (group == null) return;
        var item = new PurposeBindingItem();
        item.ParentGroup = group;
        var provider = Providers.FirstOrDefault();
        if (provider != null)
        {
            item.SelectedProvider = provider;
            item.SelectedModel = item.AvailableModels.FirstOrDefault();
        }
        group.Items.Add(item);
    }

    [RelayCommand]
    private void RemovePurposeBinding(PurposeBindingItem? item)
    {
        item?.ParentGroup?.Items.Remove(item);
    }

    [RelayCommand]
    private void SaveAll()
    {
        try
        {
            foreach (var provider in Providers)
            {
                var entity = new APIKey
                {
                    Id = provider.Id,
                    Name = provider.Name.Trim(),
                    EndPoint = provider.EndPoint.Trim(),
                    Key = provider.Key.Trim(),
                    TotalTokens = provider.TotalTokens,
                    TotalConsume = provider.TotalConsume,
                    UseTencentSign = provider.UseTencentSign,
                    AvailableModels = provider.Models.Select(model => new LLMModelConfig
                    {
                        Id = model.Id,
                        APIKeyId = provider.Id,
                        Name = model.Name.Trim(),
                        Enabled = model.Enabled,
                        InputPricePer1M = model.InputPricePer1M,
                        OutputPricePer1M = model.OutputPricePer1M,
                        CachePricePer1M = model.CachePricePer1M
                    }).ToList()
                };

                var saved = APIKeyRepository.Save(entity);
                provider.Id = saved.Id;
                for (int i = 0; i < provider.Models.Count; i++)
                {
                    provider.Models[i].APIKeyId = saved.Id;
                }
            }

            var bindings = PurposeGroups.ToDictionary(
                group => group.ConfigKey,
                group => group.Items
                    .Where(item => item.SelectedProvider != null && item.SelectedModel != null)
                    .Select(item => new APIKeyPurpose
                    {
                        Id = item.SelectedProvider!.Id,
                        Key = new APIKey { Id = item.SelectedProvider.Id, Name = item.SelectedProvider.Name },
                        Model = new LLMModelConfig { Name = item.SelectedModel!.Name }
                    })
                    .ToList());

            APIKeyRepository.SavePurposeBindings(bindings);
            LoadData();
            Growl.Success("接口与模型配置已保存");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"保存失败: {ex.Message}";
        }
    }

    private void LoadData()
    {
        ErrorMessage = null;
        Providers.Clear();
        SelectedProviderSpend.Clear();

        foreach (var provider in APIKeyRepository.GetAllWithModels())
        {
            var item = new ProviderItem
            {
                Id = provider.Id,
                Name = provider.Name,
                EndPoint = provider.EndPoint,
                Key = provider.Key,
                UseTencentSign = provider.UseTencentSign,
                TotalTokens = provider.TotalTokens,
                TotalConsume = provider.TotalConsume
            };
            foreach (var model in provider.AvailableModels ?? [])
            {
                item.Models.Add(new ProviderModelItem
                {
                    Id = model.Id,
                    APIKeyId = model.APIKeyId,
                    Name = model.Name,
                    Enabled = model.Enabled,
                    InputPricePer1M = model.InputPricePer1M,
                    OutputPricePer1M = model.OutputPricePer1M,
                    CachePricePer1M = model.CachePricePer1M
                });
            }
            Providers.Add(item);
        }

        if (Providers.Count == 0)
        {
            AddProvider();
        }

        LoadPurposeBindings();
        SelectedProvider ??= Providers.FirstOrDefault();
    }

    private void LoadPurposeBindings()
    {
        foreach (var group in PurposeGroups)
            group.Items.Clear();

        LoadPurposeGroup("ChatAPIKeyId", AppConfig.ChatAPIKeyId);
        LoadPurposeGroup("ReplyAPIKeyId", AppConfig.ReplyAPIKeyId);
        LoadPurposeGroup("SplitterApiKeyId", AppConfig.SplitterApiKeyId);
        LoadPurposeGroup("ImageDescriberApiKeyId", AppConfig.ImageDescriberApiKeyId);
        LoadPurposeGroup("SummarizerApiKeyId", AppConfig.SummarizerApiKeyId);
        LoadPurposeGroup("DiaryAPIKeyId", AppConfig.DiaryAPIKeyId);
        LoadPurposeGroup("EmbeddingApiKeyId", AppConfig.EmbeddingApiKeyId);
        LoadPurposeGroup("RerankApiKeyId", AppConfig.RerankApiKeyId);
    }

    private void LoadPurposeGroup(string key, List<APIKeyPurpose> purposes)
    {
        var group = PurposeGroups.First(x => x.ConfigKey == key);
        foreach (var purpose in purposes)
        {
            var provider = Providers.FirstOrDefault(x => x.Id == purpose.Id);
            var item = new PurposeBindingItem
            {
                ParentGroup = group,
                SelectedProvider = provider
            };
            item.SelectedModel = item.AvailableModels.FirstOrDefault(x => x.Name == purpose.Model?.Name);
            group.Items.Add(item);
        }
    }

    private void ReloadSelectedProviderSpend()
    {
        SelectedProviderSpend.Clear();
        if (SelectedProvider == null)
            return;

        foreach (var summary in APIKeyRepository.GetModelSpendSummaries(new APIKey
        {
            Id = SelectedProvider.Id,
            Name = SelectedProvider.Name,
            EndPoint = SelectedProvider.EndPoint,
            Key = SelectedProvider.Key,
            AvailableModels = SelectedProvider.Models.Select(x => new LLMModelConfig
            {
                Id = x.Id,
                APIKeyId = x.APIKeyId,
                Name = x.Name,
                Enabled = x.Enabled,
                InputPricePer1M = x.InputPricePer1M,
                OutputPricePer1M = x.OutputPricePer1M,
                CachePricePer1M = x.CachePricePer1M
            }).ToList()
        }))
        {
            SelectedProviderSpend.Add(new ModelSpendSummaryItem
            {
                ModelName = summary.ModelName,
                CallCount = summary.CallCount,
                PromptTokens = summary.PromptTokens,
                CachedPromptTokens = summary.CachedPromptTokens,
                CompletionTokens = summary.CompletionTokens,
                TotalTokens = summary.TotalTokens,
                EstimatedConsume = summary.EstimatedConsume
            });
        }
    }
}
