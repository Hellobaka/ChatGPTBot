using ChatGPTv3.UI.Converters;
using ChatGPTv3.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using HC = HandyControl.Controls;

namespace ChatGPTv3.UI.Views;

public partial class ConfigurationView : UserControl
{
    public ConfigurationView()
    {
        InitializeComponent();
        DataContext = new ConfigurationViewModel();

        // Default to first tab
        ConfigTabs.SelectedIndex = 0;
    }

    private void OnConfigRowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ContentControl host || host.DataContext is not ConfigEntry entry)
        {
            return;
        }

        host.Content = BuildEditor(entry);
    }

    private static FrameworkElement BuildEditor(ConfigEntry entry)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(180, GridUnitType.Pixel) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            Margin = new Thickness(0, 6, 0, 6),
            MinHeight = 30,
        };

        var label = new TextBlock
        {
            Text = entry.Label,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        grid.Children.Add(label);

        FrameworkElement editor = entry switch
        {
            { IsPresetSelector: true } => BuildPresetSelector(entry),
            { IsList: true } => BuildListEditor(entry),
            { IsMultiline: true } => BuildMultilineBox(entry),
            { IsSwitch: true } => BuildToggleSwitch(entry),
            { IsNumber: true } => BuildNumeric(entry),
            _ => BuildTextBox(entry),
        };

        Grid.SetColumn(editor, 1);
        grid.Children.Add(editor);
        return grid;
    }

    // ── Basic editors ──

    private static FrameworkElement BuildPresetSelector(ConfigEntry entry)
    {
        var cb = new ComboBox
        {
            DataContext = entry,
            Width = 180,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Left,
            DisplayMemberPath = "Key",
            SelectedValuePath = "Key",
        };

        cb.SetBinding(Selector.SelectedValueProperty, new Binding("PresetValue")
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });

        cb.ItemsSource = new Dictionary<string, string>
        {
            ["默认"] = "默认",
            ["积极回复"] = "积极回复",
            ["仅@时回复"] = "仅@时回复",
            ["不积极回复"] = "不积极回复",
        };

        return cb;
    }

    private static TextBox BuildTextBox(ConfigEntry entry)
    {
        var tb = new TextBox
        {
            DataContext = entry,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = 13,
        };
        tb.SetBinding(TextBox.TextProperty, new Binding("Value")
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        return tb;
    }

    private static TextBox BuildMultilineBox(ConfigEntry entry)
    {
        var tb = new TextBox
        {
            DataContext = entry,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 80,
            VerticalAlignment = VerticalAlignment.Top,
            FontSize = 13,
        };
        tb.SetBinding(TextBox.TextProperty, new Binding("Value")
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        return tb;
    }

    private static FrameworkElement BuildToggleSwitch(ConfigEntry entry)
    {
        var tb = new ToggleButton
        {
            DataContext = entry,
            VerticalAlignment = VerticalAlignment.Center,
            Content = "",
            Style = (Style)Application.Current.TryFindResource("ToggleButtonSwitch"),
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 36)),
        };
        tb.SetBinding(ToggleButton.IsCheckedProperty, new Binding("Value")
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        return tb;
    }

    private static FrameworkElement BuildNumeric(ConfigEntry entry)
    {
        var nud = new HC.NumericUpDown
        {
            DataContext = entry,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13,
            Width = 140,
            HorizontalAlignment = HorizontalAlignment.Left,
            Minimum = 0,
            Maximum = 999999,
        };
        nud.SetBinding(HC.NumericUpDown.ValueProperty, new Binding("Value")
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            Converter = new NumericToDoubleConverter(),
        });
        return nud;
    }

    // ── List editor (Popup) ──

    private static FrameworkElement BuildListEditor(ConfigEntry entry)
    {
        var btn = new Button
        {
            DataContext = entry,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = Cursors.Hand,
        };
        btn.SetBinding(ContentControl.ContentProperty, new Binding("ListCountText")
        {
            Mode = BindingMode.OneWay
        });

        Popup? popup = null;

        btn.Click += (_, _) =>
        {
            if (popup == null)
            {
                popup = new Popup
                {
                    PlacementTarget = btn,
                    Placement = PlacementMode.Bottom,
                    StaysOpen = false,
                    AllowsTransparency = true,
                };
                popup.Closed += (_, _) => RefreshButtonText(btn, entry);
            }

            popup.Child = BuildPopupContent(entry, popup, btn);
            popup.IsOpen = true;
        };

        return btn;
    }

    private static readonly Brush PopupBg = new SolidColorBrush(Color.FromRgb(37, 37, 42));
    private static readonly Brush PopupBorder = new SolidColorBrush(Color.FromRgb(63, 63, 70));
    private static readonly Brush PopupText = new SolidColorBrush(Color.FromRgb(212, 212, 216));
    private static readonly Brush PopupSubtle = new SolidColorBrush(Color.FromRgb(130, 130, 140));
    private static readonly Brush PopupItemBg = new SolidColorBrush(Color.FromRgb(50, 50, 56));
    private static readonly Brush PopupInputBg = new SolidColorBrush(Color.FromRgb(30, 30, 36));
    private static readonly Brush PopupInputBorder = new SolidColorBrush(Color.FromRgb(82, 82, 91));

    private static FrameworkElement BuildPopupContent(ConfigEntry entry, Popup popup, Button triggerBtn)
    {
        var card = new Border
        {
            Width = 380,
            MaxHeight = 420,
            Background = PopupBg,
            BorderBrush = PopupBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
        };
        var root = new StackPanel();
        card.Child = root;

        // Title bar
        var titleBar = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var title = new TextBlock
        {
            Text = entry.Label,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = PopupText,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var closeBtn = new Button
        {
            Content = "✕",
            Background = Brushes.Transparent,
            Foreground = PopupSubtle,
            FontSize = 14,
            Padding = new Thickness(6, 2, 6, 2),
            Cursor = Cursors.Hand,
            BorderThickness = new Thickness(0),
        };
        closeBtn.Click += (_, _) => popup.IsOpen = false;
        DockPanel.SetDock(closeBtn, Dock.Right);
        titleBar.Children.Add(closeBtn);
        titleBar.Children.Add(title);
        root.Children.Add(titleBar);

        // Items list (ScrollViewer + StackPanel, no ListBoxItem hover)
        var empty = new TextBlock
        {
            Text = "列表为空",
            FontSize = 13,
            Foreground = PopupSubtle,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 8),
        };
        var scrollViewer = new ScrollViewer
        {
            MaxHeight = 200,
        };
        var itemPanel = new StackPanel();
        scrollViewer.Content = itemPanel;
        root.Children.Add(empty);
        root.Children.Add(scrollViewer);

        // Error label
        var error = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(243, 139, 168)),
            FontSize = 12,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 4, 0, 0),
        };
        root.Children.Add(error);

        // Add bar
        var addBar = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        var addBtn = new Button
        {
            Content = "添加",
            FontSize = 12,
            FontWeight = FontWeights.Medium,
            MinWidth = 50,
            Margin = new Thickness(8, 0, 0, 0),
            Background = (Brush)Application.Current.TryFindResource("PrimaryBrush"),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
        };
        var addBox = new TextBox
        {
            FontSize = 12,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = PopupInputBg,
            Foreground = PopupText,
            BorderBrush = PopupInputBorder,
            CaretBrush = PopupText,
        };
        DockPanel.SetDock(addBtn, Dock.Right);
        addBar.Children.Add(addBtn);
        addBar.Children.Add(addBox);
        root.Children.Add(addBar);

        void RefreshList()
        {
            var items = entry.GetListItems();
            itemPanel.Children.Clear();
            foreach (var itemText in items)
            {
                itemPanel.Children.Add(BuildListItemRow(itemText, entry, RefreshList, triggerBtn));
            }
            scrollViewer.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            empty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        void DoAdd()
        {
            error.Visibility = Visibility.Collapsed;
            var text = addBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var items = entry.GetListItems();
            if (items.Contains(text))
            {
                error.Text = "该项已存在";
                error.Visibility = Visibility.Visible;
                return;
            }
            items.Add(text);
            entry.SaveListItems(items);
            addBox.Text = "";
            RefreshList();
            RefreshButtonText(triggerBtn, entry);
        }

        addBtn.Click += (_, _) => DoAdd();
        addBox.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                DoAdd();
            }
        };

        RefreshList();
        return card;
    }

    private static FrameworkElement BuildListItemRow(string text, ConfigEntry entry,
        Action refresh, Button triggerBtn)
    {
        var row = new Border
        {
            Background = PopupItemBg,
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 1, 0, 1),
            Padding = new Thickness(10, 5, 10, 5),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var dock = new DockPanel();

        var txt = new TextBlock
        {
            Text = text,
            Foreground = PopupText,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13,
        };

        var delBtn = new Button
        {
            Content = "🗑",
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 80, 80)),
            FontSize = 13,
            Padding = new Thickness(4, 1, 4, 1),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
            BorderThickness = new Thickness(0),
        };
        delBtn.Click += (_, _) =>
        {
            var items = entry.GetListItems();
            if (items.Contains(text))
            {
                items.Remove(text);
                entry.SaveListItems(items);
                refresh();
                RefreshButtonText(triggerBtn, entry);
            }
        };

        DockPanel.SetDock(delBtn, Dock.Right);
        dock.Children.Add(delBtn);
        dock.Children.Add(txt);
        row.Child = dock;
        return row;
    }

    private static void RefreshButtonText(Button btn, ConfigEntry entry)
    {
        btn.Content = $"编辑列表 ({entry.GetListItems().Count}个)";
    }
}