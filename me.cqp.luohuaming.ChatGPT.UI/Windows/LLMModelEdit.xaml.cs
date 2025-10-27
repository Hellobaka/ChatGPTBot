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
using System.Windows.Shapes;

namespace me.cqp.luohuaming.ChatGPT.UI.Windows
{
    /// <summary>
    /// LLMModelEdit.xaml 的交互逻辑
    /// </summary>
    public partial class LLMModelEdit : Window
    {
        public LLMModelEdit(LLMModel model)
        {
            LLMModel = model;
            InitializeComponent();
            DataContext = LLMModel;
        }

        public new ContentDialogResult DialogResult { get; set; } = ContentDialogResult.None;

        public LLMModel LLMModel { get; set; }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(LLMModel.Name.Trim()))
            {
                MainWindow.ShowError("模型名称不可为空");
                return;
            }
            DialogResult = ContentDialogResult.Primary;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
