namespace ChatGPTv3.UI.Views;

public partial class OverrideConfigInputDialog
{
    public long? TargetId { get; private set; }
    public bool IsGroup { get; private set; } = true;

    public OverrideConfigInputDialog()
    {
        InitializeComponent();
    }

    private void TypeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        IsGroup = TypeComboBox.SelectedIndex == 0;
    }

    private void Confirm_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var text = TargetIdBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text) || !long.TryParse(text, out var id))
        {
            var label = IsGroup ? "群" : "QQ";
            HandyControl.Controls.Growl.Warning($"请输入有效的{label}号");
            return;
        }

        TargetId = id;
        Close();
    }

    private void Cancel_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        Close();
    }
}
