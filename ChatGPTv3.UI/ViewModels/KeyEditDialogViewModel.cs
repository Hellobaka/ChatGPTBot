using ChatGPTv3.Core.DB;
using ChatGPTv3.OpenAIClient;
using ChatGPTv3.UI.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using System.Collections.ObjectModel;
using System.Windows;

namespace ChatGPTv3.UI.ViewModels;

/// <summary>Display option for the API format combo box.</summary>
public sealed record ApiFormatOption(ApiFormat Value, string DisplayName);

public partial class KeyEditDialogViewModel : ObservableObject
{
    public string WindowTitle { get; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _endPoint = string.Empty;
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private bool _useTencentSign;
    [ObservableProperty] private ApiFormatOption? _selectedApiFormat;
    [ObservableProperty] private bool _enableWebSearch = true;
    [ObservableProperty] private long _totalTokens;
    [ObservableProperty] private decimal _totalConsume;

    public IReadOnlyList<ApiFormatOption> ApiFormatOptions { get; } =
    [
        new(ApiFormat.OpenAI, "OpenAI - Chat Completions"),
        new(ApiFormat.Anthropic, "Anthropic - Messages API"),
        new(ApiFormat.Responses, "OpenAI - Responses API")
    ];

    public ObservableCollection<ProviderModelItem> Models { get; } = [];

    [ObservableProperty] private ProviderModelItem? _selectedModel;

    public bool IsSaved { get; set; }

    /// <summary>Creates a dialog for a new provider.</summary>
    public KeyEditDialogViewModel()
    {
        WindowTitle = "新增服务商";
        SelectedApiFormat = ApiFormatOptions[0];
    }

    /// <summary>Creates a dialog for editing an existing provider.</summary>
    public KeyEditDialogViewModel(ProviderItem source)
    {
        WindowTitle = $"编辑服务商 - {source.DisplayName}";
        Name = source.Name;
        EndPoint = source.EndPoint;
        ApiKey = source.Key;
        UseTencentSign = source.UseTencentSign;
        SelectedApiFormat = ApiFormatOptions.FirstOrDefault(o => o.Value == source.ApiFormat)
            ?? ApiFormatOptions[0];
        EnableWebSearch = source.EnableWebSearch;
        TotalTokens = source.TotalTokens;
        TotalConsume = source.TotalConsume;
        foreach (var m in source.Models)
        {
            Models.Add(m.Clone());
        }
    }

    /// <summary>Apply dialog data back to a ProviderItem.</summary>
    public ProviderItem ToProviderItem()
    {
        var item = new ProviderItem
        {
            Name = Name.Trim(),
            EndPoint = EndPoint.Trim(),
            Key = ApiKey.Trim(),
            UseTencentSign = UseTencentSign,
            ApiFormat = SelectedApiFormat?.Value ?? ApiFormat.OpenAI,
            EnableWebSearch = EnableWebSearch,
            TotalTokens = TotalTokens,
            TotalConsume = TotalConsume
        };
        foreach (var m in Models)
        {
            item.Models.Add(m.Clone());
        }
        return item;
    }

    [RelayCommand]
    private void AddModel()
    {
        var dialogVm = new ModelEditDialogViewModel();
        var dialog = new ModelEditDialog(dialogVm) { Owner = GetActiveWindow() };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var model = new ProviderModelItem
        {
            Name = dialogVm.Name.Trim(),
            Enabled = dialogVm.Enabled,
            InputPricePer1M = dialogVm.InputPricePer1M,
            OutputPricePer1M = dialogVm.OutputPricePer1M,
            CachePricePer1M = dialogVm.CachePricePer1M,
            Capabilities = dialogVm.GetCapabilities()
        };
        Models.Add(model);
        SelectedModel = model;
    }

    [RelayCommand]
    private void EditModel(ProviderModelItem? model)
    {
        if (model == null)
        {
            return;
        }

        var dialogVm = new ModelEditDialogViewModel(model);
        var dialog = new ModelEditDialog(dialogVm) { Owner = GetActiveWindow() };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        model.Name = dialogVm.Name.Trim();
        model.Enabled = dialogVm.Enabled;
        model.InputPricePer1M = dialogVm.InputPricePer1M;
        model.OutputPricePer1M = dialogVm.OutputPricePer1M;
        model.CachePricePer1M = dialogVm.CachePricePer1M;
        model.Capabilities = dialogVm.GetCapabilities();
    }

    [RelayCommand]
    private void RemoveModel(ProviderModelItem? model)
    {
        if (model == null)
        {
            return;
        }
        Models.Remove(model);
        if (SelectedModel == model)
        {
            SelectedModel = Models.LastOrDefault();
        }
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

    [RelayCommand]
    private async Task TestModel(ProviderModelItem? model)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.Name))
        {
            Growl.Error("请先选择一个模型");
            return;
        }

        if (string.IsNullOrWhiteSpace(EndPoint) || string.IsNullOrWhiteSpace(ApiKey))
        {
            Growl.Error("请先填写 EndPoint 和 API Key");
            return;
        }

        try
        {
            var format = SelectedApiFormat?.Value ?? ApiFormat.OpenAI;
            switch (format)
            {
                case ApiFormat.Anthropic:
                    {
                        var anthropicOptions = new ChatGPTv3.AnthropicClient.AnthropicChatClientOptions
                        {
                            BaseUrl = EndPoint.Trim(),
                            ApiKey = ApiKey.Trim(),
                            TimeoutMs = 15000
                        };
                        using var anthropicClient = new ChatGPTv3.AnthropicClient.AnthropicChatClient(anthropicOptions);
                        var anthropicRequest = new ChatGPTv3.AnthropicClient.AnthropicChatRequest
                        {
                            Model = model.Name.Trim(),
                            Messages =
                            [
                                new ChatGPTv3.AnthropicClient.AnthropicMessage
                            {
                                Role = "user",
                                Content =
                                [
                                    new ChatGPTv3.AnthropicClient.AnthropicContentBlock
                                    {
                                        Type = "text",
                                        Text = "hello"
                                    }
                                ]
                            }
                            ],
                            MaxTokens = 64
                        };
                        var anthropicResponse = await anthropicClient.CompleteAsync(anthropicRequest);
                        _ = anthropicResponse.GetText() ?? "(no content)";
                        break;
                    }

                case ApiFormat.Responses:
                    {
                        var responsesOptions = new ChatGPTv3.ResponsesClient.ResponsesChatClientOptions
                        {
                            BaseUrl = EndPoint.Trim(),
                            ApiKey = ApiKey.Trim(),
                            TimeoutMs = 15000
                        };
                        using var responsesClient = new ChatGPTv3.ResponsesClient.ResponsesChatClient(responsesOptions);
                        var responsesRequest = new ChatGPTv3.ResponsesClient.ResponsesCreateRequest
                        {
                            Model = model.Name.Trim(),
                            Input = "hello",
                            MaxOutputTokens = 64
                        };
                        var responsesResponse = await responsesClient.CompleteAsync(responsesRequest);
                        _ = responsesResponse.GetText() ?? "(no content)";
                        break;
                    }

                default:
                    {
                        var options = new OpenAiChatClientOptions
                        {
                            BaseUrl = EndPoint.Trim(),
                            ApiKey = ApiKey.Trim(),
                            TimeoutMs = 15000
                        };

                        using var client = new OpenAiChatClient(options);
                        var request = new ChatCompletionRequest
                        {
                            Model = model.Name.Trim(),
                            Messages = [ChatMessage.User("hello")]
                        };

                        var response = await client.CompleteAsync(request);
                        _ = response.GetFirstChoiceText() ?? "(no content)";
                        break;
                    }
            }

            Growl.Success($"模型 {model.Name} 测试成功");
        }
        catch (Exception ex)
        {
            Growl.Error($"模型 {model.Name} 测试失败: {ex.Message}");
        }
    }

    private static System.Windows.Window? GetActiveWindow() =>
        System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive);
}
