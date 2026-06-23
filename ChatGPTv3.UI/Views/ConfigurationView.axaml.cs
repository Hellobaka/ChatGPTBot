using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ChatGPTv3.UI.ViewModels;

namespace ChatGPTv3.UI.Views;

public partial class ConfigurationView : UserControl
{
    public ConfigurationView()
    {
        InitializeComponent();
        DataContext = new ConfigurationViewModel();
    }

    /// <summary>
    /// Replaces a config row placeholder with the correct editor control.
    /// Each entry type gets its own control — no overlapping hidden bindings.
    /// </summary>
    private void OnConfigRowLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not ContentControl host || host.DataContext is not ConfigEntry entry) return;
        if (host.Content != null) return; // already built

        host.Content = BuildEditor(entry);
    }

    private static Control BuildEditor(ConfigEntry entry)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(180, GridUnitType.Pixel),
                new ColumnDefinition(1, GridUnitType.Star)
            },
            Margin = new Avalonia.Thickness(0, 3)
        };

        var label = new TextBlock
        {
            Text = entry.Label,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(205, 214, 244)),
            FontSize = 13
        };
        grid.Children.Add(label);

        Control editor = entry switch
        {
            { IsList: true } => BuildListEditor(entry),
            { IsMultiline: true } => BuildMultilineBox(entry),
            { IsSwitch: true } => BuildSwitch(entry),
            { IsNumber: true } => BuildNumber(entry),
            _ => BuildTextBox(entry),
        };

        Grid.SetColumn(editor, 1);
        grid.Children.Add(editor);
        return grid;
    }

    private static TextBox BuildTextBox(ConfigEntry entry) => new()
    {
        [!TextBox.TextProperty] = new Avalonia.Data.Binding("Value"),
        Background = new SolidColorBrush(Color.FromRgb(49, 50, 68)),
        Foreground = new SolidColorBrush(Color.FromRgb(205, 214, 244)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(69, 71, 90)),
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    private static TextBox BuildMultilineBox(ConfigEntry entry) => new()
    {
        [!TextBox.TextProperty] = new Avalonia.Data.Binding("Value"),
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        MinHeight = 80,
        Background = new SolidColorBrush(Color.FromRgb(49, 50, 68)),
        Foreground = new SolidColorBrush(Color.FromRgb(205, 214, 244)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(69, 71, 90)),
        VerticalAlignment = VerticalAlignment.Top,
    };

    private static ToggleSwitch BuildSwitch(ConfigEntry entry) => new()
    {
        [!ToggleSwitch.IsCheckedProperty] = new Avalonia.Data.Binding("Value"),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static NumericUpDown BuildNumber(ConfigEntry entry)
    {
        var nud = new NumericUpDown
        {
            [!NumericUpDown.ValueProperty] = new Avalonia.Data.Binding("Value"),
            Minimum = 0,
            Maximum = 999999,
            Increment = entry.DefaultValue is double or float ? 0.1m : 1m,
            VerticalAlignment = VerticalAlignment.Center,
        };
        return nud;
    }

    /// <summary>
    /// List editor: compact "编辑 (N个)" button that opens a Flyout with the full editor.
    /// Matches the Vue Settings page pattern: list stays hidden until the user clicks edit.
    /// </summary>
    private static Control BuildListEditor(ConfigEntry entry)
    {
        var btn = new Button
        {
            Content = $"编辑列表 ({entry.GetListItems().Count}个)",
            Background = new SolidColorBrush(Color.FromRgb(69, 71, 90)),
            Foreground = new SolidColorBrush(Color.FromRgb(205, 214, 244)),
            Padding = new Avalonia.Thickness(12, 5),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var flyout = new Flyout
        {
            Placement = PlacementMode.BottomEdgeAlignedRight,
            ShowMode = FlyoutShowMode.Transient,
        };

        btn.Flyout = flyout;
        btn.Click += (_, _) =>
        {
            flyout.Content = BuildFlyoutContent(entry, flyout, btn);
            flyout.ShowAt(btn);
        };

        return btn;
    }

    private static Control BuildFlyoutContent(ConfigEntry entry, Flyout flyout, Button triggerBtn)
    {
        var card = new Border
        {
            Width = 360,
            MaxHeight = 420,
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 46)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(69, 71, 90)),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(8),
            Padding = new Avalonia.Thickness(14),
            Child = new StackPanel { Spacing = 10 }
        };
        var root = (StackPanel)card.Child;

        // ── Title bar ──
        var titleBar = new DockPanel();
        var title = new TextBlock
        {
            Text = entry.Label,
            FontSize = 15,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(205, 214, 244)),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var closeBtn = new Button
        {
            Content = "✕",
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 160)),
            FontSize = 14,
            Padding = new Avalonia.Thickness(6, 2),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
        };
        closeBtn.Click += (_, _) => flyout.Hide();
        DockPanel.SetDock(closeBtn, Dock.Right);
        titleBar.Children.Add(closeBtn);
        titleBar.Children.Add(title);
        root.Children.Add(titleBar);

        // ── Empty / items area ──
        var empty = new TextBlock
        {
            Text = "列表为空",
            Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 120)),
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 8),
        };
        var listBox = new ListBox
        {
            MaxHeight = 200,
            BorderThickness = new Avalonia.Thickness(0),
            Background = Brushes.Transparent,
        };
        listBox.ItemTemplate = new FuncDataTemplate<object>((item, _) =>
        {
            var row = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 58)),
                CornerRadius = new Avalonia.CornerRadius(4),
                Margin = new Avalonia.Thickness(0, 1),
                Padding = new Avalonia.Thickness(10, 5),
            };
            var dock = new DockPanel();
            var txt = new TextBlock
            {
                [!TextBlock.TextProperty] = new Avalonia.Data.Binding("."),
                Foreground = new SolidColorBrush(Color.FromRgb(205, 214, 244)),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13,
            };
            var delBtn = new Button
            {
                Content = "🗑",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 80, 80)),
                FontSize = 13,
                Padding = new Avalonia.Thickness(4, 1),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            };
            var text = item?.ToString() ?? "";
            delBtn.Click += (_, _) =>
            {
                var items = entry.GetListItems();
                if (items.Contains(text))
                {
                    items.Remove(text);
                    entry.SaveListItems(items);
                    RefreshFlyoutList(listBox, empty, entry, triggerBtn);
                }
            };
            DockPanel.SetDock(delBtn, Dock.Right);
            dock.Children.Add(delBtn);
            dock.Children.Add(txt);
            row.Child = dock;
            return row;
        });
        root.Children.Add(empty);
        root.Children.Add(listBox);

        // ── Error label ──
        var error = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(243, 139, 168)),
            FontSize = 12,
            IsVisible = false,
        };
        root.Children.Add(error);

        // ── Add bar ──
        var addBar = new DockPanel();
        var addBtn = new Button
        {
            Content = "添加",
            Background = new SolidColorBrush(Color.FromRgb(137, 180, 250)),
            Foreground = new SolidColorBrush(Color.FromRgb(17, 17, 27)),
            Padding = new Avalonia.Thickness(14, 5),
            FontSize = 12,
            FontWeight = FontWeight.Medium,
        };
        var addBox = new TextBox
        {
            Background = new SolidColorBrush(Color.FromRgb(49, 50, 68)),
            Foreground = new SolidColorBrush(Color.FromRgb(205, 214, 244)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(69, 71, 90)),
            Watermark = "输入新项，回车添加",
            FontSize = 12,
        };
        DockPanel.SetDock(addBtn, Dock.Right);
        addBar.Children.Add(addBtn);
        addBar.Children.Add(addBox);
        root.Children.Add(addBar);

        // ── Logic ──
        void DoAdd()
        {
            error.IsVisible = false;
            var text = addBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text)) return;

            var items = entry.GetListItems();
            if (items.Contains(text))
            {
                error.Text = "该项已存在";
                error.IsVisible = true;
                return;
            }
            items.Add(text);
            entry.SaveListItems(items);
            addBox.Text = "";
            RefreshFlyoutList(listBox, empty, entry, triggerBtn);
        }
        addBtn.Click += (_, _) => DoAdd();
        addBox.KeyDown += (_, args) =>
        {
            if (args.Key == Avalonia.Input.Key.Enter) DoAdd();
        };

        RefreshFlyoutList(listBox, empty, entry, triggerBtn);
        return card;
    }

    private static void RefreshFlyoutList(ListBox listBox, TextBlock empty, ConfigEntry entry, Button triggerBtn)
    {
        var items = entry.GetListItems();
        listBox.ItemsSource = items;
        listBox.IsVisible = items.Count > 0;
        empty.IsVisible = items.Count == 0;
        triggerBtn.Content = $"编辑列表 ({items.Count}个)";
    }
}
