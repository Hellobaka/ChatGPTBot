using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace me.cqp.luohuaming.ChatGPT.UI.Controls
{
    /// <summary>
    /// EditableListBox_Int.xaml 的交互逻辑
    /// </summary>
    public partial class EditableListBox_String : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {
        public EditableListBox_String()
        {
            InitializeComponent();
        }

        public IList<string> ItemSource
        {
            get { return (IList<string>)GetValue(ItemSourceProperty); }
            set { SetValue(ItemSourceProperty, value); RerenderListBox(); }
        }

        public static readonly DependencyProperty ItemSourceProperty =
            DependencyProperty.Register("ItemSource", typeof(IList<string>), typeof(EditableListBox_String), new PropertyMetadata(default, OnItemSourceSet));

        private static void OnItemSourceSet(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EditableListBox_String control)
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

        public string SelectedItem { get; set; }


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
            if (!string.IsNullOrEmpty(AddItemInput))
            {
                if (!CanDuplicate && ItemSource.Contains(AddItemInput))
                {
                    MainWindow.ShowError("已存在相同项");
                    return;
                }
                ItemSource.Add(AddItemInput);
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
