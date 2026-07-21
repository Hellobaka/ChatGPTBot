using ChatGPTv3.UI.ViewModels;

namespace ChatGPTv3.UI.Views;

public partial class MiscManagementView
{
    public MiscManagementView()
    {
        InitializeComponent();
        DataContext = new MiscManagementViewModel();
    }
}
