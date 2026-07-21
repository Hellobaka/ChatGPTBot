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

        // Close the window when DialogResult is set (by Save or Cancel)
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ImportKnowledgeViewModel.Closing)
             && _vm.Closing)
            {
                DialogResult = _vm.DialogResult;
                Close();
            }
        };
    }

    public new bool? ShowDialog()
    {
        base.ShowDialog();
        return _vm.DialogResult ? true : false;
    }
}
