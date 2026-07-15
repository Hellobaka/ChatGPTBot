using System.Windows;
using ChatGPTv3.UI.ViewModels;

namespace ChatGPTv3.UI.Views;

/// <summary>
/// Dialog for importing knowledge text with a required source title.
/// </summary>
public partial class ImportKnowledgeDialog
{
    private readonly ImportKnowledgeViewModel _vm = new();

    public ImportKnowledgeDialog()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    public new bool? ShowDialog()
    {
        base.ShowDialog();
        return _vm.DialogResult ? true : false;
    }
}
