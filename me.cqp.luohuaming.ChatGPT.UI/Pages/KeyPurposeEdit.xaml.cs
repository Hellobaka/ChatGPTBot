using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace me.cqp.luohuaming.ChatGPT.UI.Pages
{
    /// <summary>
    /// KeyPurposeEdit.xaml 的交互逻辑
    /// </summary>
    public partial class KeyPurposeEdit : ContentDialog
    {
        public KeyPurposeEdit(APIKeyPurpose purpose)
        {
            InitializeComponent();
            KeyComboBox.ItemsSource = APIKeys.KeyCache.Select(x => x.Value).ToList();
            APIKeyPurpose = purpose;
            DataContext = purpose;
        }

        public APIKeyPurpose APIKeyPurpose { get; set; }

        public ContentDialogResult DialogResult { get; set; }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = ContentDialogResult.Primary;
            Hide();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = ContentDialogResult.None;
            Hide();
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            APIKeyPurpose.Id = APIKeyPurpose?.Key.Id ?? 0;
        }
    }
}
