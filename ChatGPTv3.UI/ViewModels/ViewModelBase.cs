using CommunityToolkit.Mvvm.ComponentModel;

namespace ChatGPTv3.UI.ViewModels;

public partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isLoading;
}