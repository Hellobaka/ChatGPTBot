using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

namespace me.cqp.luohuaming.ChatGPT.UI.Controls
{
    /// <summary>
    /// EditableListBox_Dict_String_String.xaml 的交互逻辑
    /// </summary>
    public partial class EditableListBox_Dict_String_String : UserControl, INotifyPropertyChanged
    {
        public EditableListBox_Dict_String_String()
        {
            InitializeComponent();
        }

        public IDictionary<string, string> ItemSource
        {
            get { return (IDictionary<string, string>)GetValue(ItemSourceProperty); }
            set { SetValue(ItemSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemSourceProperty =
            DependencyProperty.Register("ItemSource", typeof(IDictionary<string, string>), typeof(EditableListBox_Dict_String_String), new PropertyMetadata(default, OnItemSourceSet));

        private static void OnItemSourceSet(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EditableListBox_Dict_String_String control)
            {
                control.RerenderListBox();
            }
        }

        public string AddItemKeyInput { get; set; }

        public string AddItemValueInput { get; set; }

        public ObservableCollection<KeyValuePair<string, string>> ItemDisplaySource { get; set; } = [];

        public KeyValuePair<string, string> SelectedItem { get; set; }


        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private void EditableListBoxRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem.Value == default || SelectedItem.Key == default)
            {
                MainWindow.ShowError("请选择一项");
                return;
            }

            ItemSource.Remove(SelectedItem.Key);
            ItemDisplaySource.Remove(SelectedItem);
        }

        private void EditableListBoxAddButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(AddItemKeyInput) && !string.IsNullOrEmpty(AddItemValueInput))
            {
                bool hasDuplicate = ItemSource.ContainsKey(AddItemKeyInput);
                if (hasDuplicate && !MainWindow.ShowConfirm("已存在相同键，是否替换？"))
                {
                    return;
                }
                if (!hasDuplicate)
                {
                    ItemSource.Add(AddItemKeyInput, AddItemValueInput);
                    ItemDisplaySource.Add(new(AddItemKeyInput, AddItemValueInput));
                }
                else
                {
                    var item = ItemDisplaySource.FirstOrDefault(x => x.Key == AddItemKeyInput);
                    ItemSource[AddItemKeyInput] = AddItemValueInput;
                    ItemDisplaySource[ItemDisplaySource.IndexOf(item)] = new(AddItemKeyInput, AddItemValueInput);
                }
                EditableListBoxKey.Clear();
                EditableListBoxValue.Clear();

                RerenderListBox();
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
            ItemDisplaySource.Clear();
            if (ItemSource != null)
            {
                foreach (var item in ItemSource)
                {
                    ItemDisplaySource.Add(item);
                }
            }
            OnPropertyChanged(nameof(ItemDisplaySource));
        }

        private void EditableListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectedItem.Value == default || SelectedItem.Key == default)
            {
                return;
            }
            AddItemKeyInput = SelectedItem.Key;
            AddItemValueInput = SelectedItem.Value;
        }
    }
}
