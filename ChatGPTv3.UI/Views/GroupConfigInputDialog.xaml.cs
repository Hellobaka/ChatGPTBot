namespace ChatGPTv3.UI.Views;

public partial class GroupConfigInputDialog
{
    public long? GroupId { get; private set; }

    public GroupConfigInputDialog()
    {
        InitializeComponent();
    }

    private void Confirm_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var text = GroupIdBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text) || !long.TryParse(text, out var id))
        {
            HandyControl.Controls.Growl.Warning("请输入有效的群号");
            return;
        }

        GroupId = id;
        Close();
    }

    private void Cancel_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        Close();
    }
}
