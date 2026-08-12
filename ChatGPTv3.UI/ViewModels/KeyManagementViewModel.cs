using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using ChatGPTv3.UI.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using System.Collections.ObjectModel;
using System.Windows;
using MessageBox = HandyControl.Controls.MessageBox;

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
    [ObservableProperty] private ModelCapability _capabilities = ModelCapability.Chat;

    public ProviderModelItem Clone() => new()
    {
        Id = Id,
        APIKeyId = APIKeyId,
        Name = Name,
        Enabled = Enabled,
        InputPricePer1M = InputPricePer1M,
        OutputPricePer1M = OutputPricePer1M,
        CachePricePer1M = CachePricePer1M,
        Capabilities = Capabilities
    };
}

public partial class ProviderItem : ObservableObject
{
    public int Id { get; set; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _endPoint = string.Empty;
    [ObservableProperty] private string _key = string.Empty;
    [ObservableProperty] private bool _useTencentSign;
    [ObservableProperty] private ApiFormat _apiFormat = ApiFormat.OpenAI;
    [ObservableProperty] private bool _enableWebSearch = true;
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
            ApiFormat = ApiFormat,
            EnableWebSearch = EnableWebSearch,
            TotalTokens = TotalTokens,
            TotalConsume = TotalConsume
        };
        foreach (var model in Models)
        {
            clone.Models.Add(model.Clone());
        }

        return clone;
    }
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
            var required = ParentGroup?.RequiredCapability ?? ModelCapability.Chat;
            foreach (var model in value.Models.Where(x => x.Enabled && (x.Capabilities & required) != 0))
            {
                AvailableModels.Add(model);
            }
        }

        if (SelectedModel != null && !AvailableModels.Any(x => x.Name == SelectedModel.Name))
        {
            SelectedModel = AvailableModels.FirstOrDefault();
        }
    }
}

public partial class PurposeBindingGroup : ObservableObject
{
    public string ConfigKey { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    /// <summary>Only models matching these capabilities can be bound to this purpose.</summary>
    public ModelCapability RequiredCapability { get; init; } = ModelCapability.Chat;

    public ObservableCollection<PurposeBindingItem> Items { get; } = [];
}

public partial class KeyManagementViewModel : ViewModelBase
{
    public ObservableCollection<ProviderItem> Providers { get; } = [];

    public ObservableCollection<PurposeBindingGroup> PurposeGroups { get; } = [];

    [ObservableProperty] private ProviderItem? _selectedProvider;

    public KeyManagementViewModel()
    {
        PurposeGroups.Add(new PurposeBindingGroup { ConfigKey = "ChatAPIKeyId", Title = "聊天模型", Description = "群聊 / 主对话使用的 Key 与模型", RequiredCapability = ModelCapability.Chat });
        PurposeGroups.Add(new PurposeBindingGroup { ConfigKey = "ReplyAPIKeyId", Title = "回复决策模型", Description = "边界回复判断使用的模型", RequiredCapability = ModelCapability.Chat });
        PurposeGroups.Add(new PurposeBindingGroup { ConfigKey = "SplitterApiKeyId", Title = "分段模型", Description = "分段和润色使用的模型", RequiredCapability = ModelCapability.Chat });
        PurposeGroups.Add(new PurposeBindingGroup { ConfigKey = "ImageDescriberApiKeyId", Title = "图像描述模型", Description = "视觉描述使用的模型", RequiredCapability = ModelCapability.Chat | ModelCapability.Image });
        PurposeGroups.Add(new PurposeBindingGroup { ConfigKey = "SummarizerApiKeyId", Title = "工具总结模型", Description = "工具结果总结使用的模型", RequiredCapability = ModelCapability.Chat });
        PurposeGroups.Add(new PurposeBindingGroup { ConfigKey = "DiaryAPIKeyId", Title = "日记模型", Description = "日记与回顾使用的模型", RequiredCapability = ModelCapability.Chat });
        PurposeGroups.Add(new PurposeBindingGroup { ConfigKey = "EmbeddingApiKeyId", Title = "Embedding 模型", Description = "向量嵌入使用的模型", RequiredCapability = ModelCapability.Embedding });
        PurposeGroups.Add(new PurposeBindingGroup { ConfigKey = "RerankApiKeyId", Title = "Rerank 模型", Description = "重排序使用的模型", RequiredCapability = ModelCapability.Rerank });

        LoadData();
    }

    [RelayCommand]
    private void Reload()
    {
        AppConfig.Init();
        LoadData();
        Growl.Success("接口配置已刷新");
    }

    [RelayCommand]
    private void AddProvider()
    {
        var dialogVm = new KeyEditDialogViewModel();
        var dialog = new KeyEditDialog(dialogVm) { Owner = GetActiveWindow() };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var item = dialogVm.ToProviderItem();
        Providers.Add(item);
        SelectedProvider = item;
        PersistProvider(item);
        LoadData();
        SelectedProvider = Providers.FirstOrDefault(x => x.Id == item.Id) ?? Providers.FirstOrDefault();
    }

    [RelayCommand]
    private void EditProvider(ProviderItem? provider)
    {
        if (provider == null)
        {
            return;
        }

        var dialogVm = new KeyEditDialogViewModel(provider);
        var dialog = new KeyEditDialog(dialogVm) { Owner = GetActiveWindow() };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var edited = dialogVm.ToProviderItem();
        provider.Name = edited.Name;
        provider.EndPoint = edited.EndPoint;
        provider.Key = edited.Key;
        provider.UseTencentSign = edited.UseTencentSign;
        provider.ApiFormat = edited.ApiFormat;
        provider.EnableWebSearch = edited.EnableWebSearch;
        provider.TotalTokens = edited.TotalTokens;
        provider.TotalConsume = edited.TotalConsume;
        provider.Models.Clear();
        foreach (var m in edited.Models)
        {
            provider.Models.Add(m);
        }
        PersistProvider(provider);
    }

    private void PersistProvider(ProviderItem provider)
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
            ApiFormat = provider.ApiFormat,
            EnableWebSearch = provider.EnableWebSearch,
            AvailableModels = provider.Models.Select(model => new LLMModelConfig
            {
                Id = model.Id,
                APIKeyId = provider.Id,
                Name = model.Name.Trim(),
                Enabled = model.Enabled,
                InputPricePer1M = model.InputPricePer1M,
                OutputPricePer1M = model.OutputPricePer1M,
                CachePricePer1M = model.CachePricePer1M,
                Capabilities = model.Capabilities
            }).ToList()
        };

        var saved = APIKeyRepository.Save(entity);
        provider.Id = saved.Id;
        for (int i = 0; i < provider.Models.Count; i++)
        {
            provider.Models[i].APIKeyId = saved.Id;
        }
    }

    [RelayCommand]
    private void RemoveProvider(ProviderItem? provider)
    {
        if (provider == null)
        {
            return;
        }

        if (MessageBox.Show(
                $"确定要删除服务商「{provider.DisplayName}」吗？\n该操作不可撤销，并将移除所有对应的用途绑定。",
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        foreach (var group in PurposeGroups)
        {
            var stale = group.Items.Where(x => x.SelectedProvider == provider).ToList();
            foreach (var item in stale)
            {
                group.Items.Remove(item);
            }
        }

        if (provider.Id > 0)
        {
            APIKeyRepository.Delete(provider.Id);
        }

        Providers.Remove(provider);
        if (SelectedProvider == provider)
        {
            SelectedProvider = Providers.FirstOrDefault();
        }
    }

    [RelayCommand]
    private void AddPurposeBinding(PurposeBindingGroup? group)
    {
        if (group == null)
        {
            return;
        }

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
                    ApiFormat = provider.ApiFormat,
                    EnableWebSearch = provider.EnableWebSearch,
                    AvailableModels = provider.Models.Select(model => new LLMModelConfig
                    {
                        Id = model.Id,
                        APIKeyId = provider.Id,
                        Name = model.Name.Trim(),
                        Enabled = model.Enabled,
                        InputPricePer1M = model.InputPricePer1M,
                        OutputPricePer1M = model.OutputPricePer1M,
                        CachePricePer1M = model.CachePricePer1M,
                        Capabilities = model.Capabilities
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
                    .Select(item =>
                    {
                        if ((item.SelectedModel!.Capabilities & group.RequiredCapability) == 0)
                        {
                            throw new InvalidOperationException(
                                $"模型「{item.SelectedModel.Name}」不支持「{group.Title}」所需的能力。" +
                                $"模型能力: {item.SelectedModel.Capabilities}, 需要: {group.RequiredCapability}");
                        }
                        return new APIKeyPurpose
                        {
                            Id = item.SelectedProvider!.Id,
                            Key = new APIKey { Id = item.SelectedProvider.Id, Name = item.SelectedProvider.Name },
                            Model = new LLMModelConfig { Id = item.SelectedModel.Id, Name = item.SelectedModel.Name }
                        };
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

        foreach (var provider in APIKeyRepository.GetAllWithModels())
        {
            var item = new ProviderItem
            {
                Id = provider.Id,
                Name = provider.Name,
                EndPoint = provider.EndPoint,
                Key = provider.Key,
                UseTencentSign = provider.UseTencentSign,
                ApiFormat = provider.ApiFormat,
                EnableWebSearch = provider.EnableWebSearch,
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
                    CachePricePer1M = model.CachePricePer1M,
                    Capabilities = model.Capabilities == 0 ? ModelCapability.Chat : model.Capabilities
                });
            }
            Providers.Add(item);
        }

        LoadPurposeBindings();
        SelectedProvider ??= Providers.FirstOrDefault();
    }

    private static System.Windows.Window? GetActiveWindow() =>
        System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive);

    private void LoadPurposeBindings()
    {
        foreach (var group in PurposeGroups)
        {
            group.Items.Clear();
        }

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
            item.SelectedModel = item.AvailableModels.FirstOrDefault(x => x.Id == purpose.Model?.Id)
                ?? item.AvailableModels.FirstOrDefault(x => x.Name == purpose.Model?.Name);
            group.Items.Add(item);
        }
    }

}
