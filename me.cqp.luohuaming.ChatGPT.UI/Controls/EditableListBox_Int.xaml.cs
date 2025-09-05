using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace me.cqp.luohuaming.ChatGPT.UI.Controls
{
    /// <summary>
    /// EditableListBox_Int.xaml 的交互逻辑
    /// </summary>
    public partial class EditableListBox_Int : System.Windows.Controls.UserControl
    {
        public EditableListBox_Int()
        {
            InitializeComponent();
        }

        public IList<long> ItemSource
        {
            get { return (IList<long>)GetValue(ItemSourceProperty); }
            set { SetValue(ItemSourceProperty, value); RerenderListBox(); }
        }

        public static readonly DependencyProperty ItemSourceProperty =
            DependencyProperty.Register("ItemSource", typeof(IList<long>), typeof(EditableListBox_Int), new PropertyMetadata(default, OnItemSourceSet));

        private static void OnItemSourceSet(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EditableListBox_Int control)
            {
                control.RerenderListBox();
            }
        }

        public bool CanDuplicate
        {
            get { return (bool)GetValue(CanDuplicateProperty); }
            set { SetValue(CanDuplicateProperty, value); }
        }

        public static readonly DependencyProperty CanDuplicateProperty =
            DependencyProperty.Register("CanDuplicate", typeof(bool), typeof(EditableListBox_String), new PropertyMetadata(false));

        public string AddItemInput { get; set; }

        public int SelectedItem { get; set; }


        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private void EditableListBoxRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (EditableListBox.SelectedIndex < 0)
            {
                MainWindow.ShowError("请选择一项");
                return;
            }

            ItemSource.RemoveAt(EditableListBox.SelectedIndex);
            EditableListBox.Items.RemoveAt(EditableListBox.SelectedIndex);
        }

        private void EditableListBoxAddButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(AddItemInput) && long.TryParse(AddItemInput, out long value))
            {
                if (!CanDuplicate && ItemSource.Contains(value))
                {
                    MainWindow.ShowError("已存在相同项");
                    return;
                }
                ItemSource.Add(value);
                EditableListBox.Items.Add(AddItemInput);
                EditableListBoxAdd.Clear();
            }
            else
            {
                MainWindow.ShowError("输入内容格式错误");
            }
        }

        private void EditableListBoxAdd_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                EditableListBoxAddButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private void RerenderListBox()
        {
            EditableListBox.Items.Clear();
            foreach (var item in ItemSource)
            {
                EditableListBox.Items.Add(item);
            }
        }
    }
}
