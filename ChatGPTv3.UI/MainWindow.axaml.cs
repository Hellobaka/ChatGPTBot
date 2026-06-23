using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ChatGPTv3.UI.Models;
using ChatGPTv3.UI.Views;
using IconPacks.Avalonia.MaterialDesign;

namespace ChatGPTv3.UI;

public partial class MainWindow : Window
{
    private bool _isExpanded = true;
    private const double ExpandedWidth = 220;
    private const double CollapsedWidth = 48;
    private bool _restored;

    private static readonly NavigationItem[] NavItems =
    [
        new() { Name = "聊天测试", IconKind = PackIconMaterialDesignKind.Chat, PageKey = "chat" },
        new() { Name = "Token统计", IconKind = PackIconMaterialDesignKind.BarChart, PageKey = "token" },
        new() { Name = "图片管理", IconKind = PackIconMaterialDesignKind.Image, PageKey = "image" },
        new() { Name = "关系管理", IconKind = PackIconMaterialDesignKind.Group, PageKey = "relationship" },
        new() { Name = "日记管理", IconKind = PackIconMaterialDesignKind.Book, PageKey = "diary" },
        new() { Name = "知识库", IconKind = PackIconMaterialDesignKind.LibraryBooks, PageKey = "knowledge" },
        new() { Name = "日程管理", IconKind = PackIconMaterialDesignKind.CalendarMonth, PageKey = "schedule" },
        new() { Name = "MCP管理", IconKind = PackIconMaterialDesignKind.Api, PageKey = "mcp" },
        new() { Name = "系统设置", IconKind = PackIconMaterialDesignKind.Settings, PageKey = "config" },
    ];

    private static string SettingsPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window_settings.json");

    public MainWindow()
    {
        InitializeComponent();

        HeaderIcon.Kind = PackIconMaterialDesignKind.SmartToy;
        NavList.ItemsSource = NavItems;

        RestoreWindowState();
    }

    // ═══════════════════════════════════════════════════
    //  Window state persistence
    // ═══════════════════════════════════════════════════

    private void RestoreWindowState()
    {
        try
        {
            if (!File.Exists(SettingsPath)) goto defaults;
            var json = File.ReadAllText(SettingsPath);
            var s = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (s == null) goto defaults;

            if (s.TryGetValue("Left", out var left)) Position = Position.WithX(left.GetInt32());
            if (s.TryGetValue("Top", out var top)) Position = Position.WithY(top.GetInt32());
            if (s.TryGetValue("Width", out var w) && w.GetInt32() > 400) Width = w.GetInt32();
            if (s.TryGetValue("Height", out var h) && h.GetInt32() > 300) Height = h.GetInt32();

            _isExpanded = !s.TryGetValue("SidebarExpanded", out var se) || se.GetBoolean();
            SetExpanded(_isExpanded);

            if (s.TryGetValue("LastTab", out var tab))
            {
                var pageKey = tab.GetString();
                var idx = Array.FindIndex(NavItems, n => n.PageKey == pageKey);
                if (idx >= 0) NavList.SelectedIndex = idx;
            }

            _restored = true;
            return;
        }
        catch { /* ignore */ }
    defaults:
        NavList.SelectedIndex = 0;
        SetExpanded(true);
        _restored = true;
    }

    private void SaveWindowState()
    {
        if (!_restored) return;
        try
        {
            var state = new Dictionary<string, object>
            {
                ["Left"] = Position.X,
                ["Top"] = Position.Y,
                ["Width"] = Width,
                ["Height"] = Height,
                ["SidebarExpanded"] = _isExpanded,
            };
            if (NavList.SelectedItem is NavigationItem item)
                state["LastTab"] = item.PageKey;

            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(state));
        }
        catch { /* ignore */ }
    }

    protected override void OnClosed(EventArgs e)
    {
        SaveWindowState();
        base.OnClosed(e);
    }

    // ═══════════════════════════════════════════════════
    //  Sidebar toggle
    // ═══════════════════════════════════════════════════

    private void ToggleSidebar(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _isExpanded = !_isExpanded;
        SetExpanded(_isExpanded);
        SaveWindowState();
    }

    private void SetExpanded(bool expanded)
    {
        Sidebar.Width = expanded ? ExpandedWidth : CollapsedWidth;
        HeaderExpanded.IsVisible = expanded;

        ToggleIcon.Kind = expanded
            ? PackIconMaterialDesignKind.ChevronLeft
            : PackIconMaterialDesignKind.Menu;

        ToggleBtn.HorizontalAlignment = expanded
            ? Avalonia.Layout.HorizontalAlignment.Right
            : Avalonia.Layout.HorizontalAlignment.Center;

        foreach (var container in NavList.GetRealizedContainers())
        {
            var label = FindVisualChild<TextBlock>(container, "ItemLabel");
            if (label != null)
                label.IsVisible = expanded;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ListBox.SelectedIndexProperty && !_isExpanded)
        {
            Dispatcher.UIThread.Post(() => SetExpanded(false), DispatcherPriority.Background);
        }
    }

    private static T? FindVisualChild<T>(Visual parent, string name) where T : Control
    {
        if (parent is T t && (parent as Control)?.Name == name) return t;
        foreach (var child in parent.GetVisualChildren())
        {
            if (child is Visual v)
            {
                var found = FindVisualChild<T>(v, name);
                if (found != null) return found;
            }
        }
        return default;
    }

    // ═══════════════════════════════════════════════════
    //  Navigation
    // ═══════════════════════════════════════════════════

    private void OnNavSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is not NavigationItem item) return;

        ContentArea.Content = item.PageKey switch
        {
            "config" => new ConfigurationView(),
            "chat" => new ChatTestView(),
            _ => new TextBlock
            {
                Text = $"{item.Name} — 待实现",
                Foreground = new SolidColorBrush(Color.FromRgb(205, 214, 244)),
                FontSize = 16
            }
        };

        if (!_isExpanded)
            Dispatcher.UIThread.Post(() => SetExpanded(false), DispatcherPriority.Background);

        SaveWindowState();
    }
}
