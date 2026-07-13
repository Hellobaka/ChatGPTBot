using ChatGPTv3.UI.Models;
using ChatGPTv3.UI.Views;
using MahApps.Metro.IconPacks;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ChatGPTv3.UI;

public partial class MainWindow
{
    public static readonly DependencyProperty SidebarExpandedProperty =
        DependencyProperty.Register(nameof(SidebarExpanded), typeof(bool), typeof(MainWindow),
            new PropertyMetadata(true));

    public bool SidebarExpanded
    {
        get => (bool)GetValue(SidebarExpandedProperty);
        set => SetValue(SidebarExpandedProperty, value);
    }

    private const double ExpandedWidth = 220;
    private const double CollapsedWidth = 48;
    private bool _restored;

    private static readonly NavigationItem[] NavItems =
    [
        new() { Name = "聊天测试", IconKind = PackIconMaterialDesignKind.Chat, PageKey = "chat" },
        new() { Name = "接口密钥", IconKind = PackIconMaterialDesignKind.VpnKey, PageKey = "keys" },
        new() { Name = "Token统计", IconKind = PackIconMaterialDesignKind.BarChart, PageKey = "token" },
        new() { Name = "图片管理", IconKind = PackIconMaterialDesignKind.Image, PageKey = "image" },
        new() { Name = "关系管理", IconKind = PackIconMaterialDesignKind.Group, PageKey = "relationship" },
        new() { Name = "日记管理", IconKind = PackIconMaterialDesignKind.Book, PageKey = "diary" },
        new() { Name = "知识库", IconKind = PackIconMaterialDesignKind.LibraryBooks, PageKey = "knowledge" },
        new() { Name = "日程管理", IconKind = PackIconMaterialDesignKind.CalendarMonth, PageKey = "schedule" },
        new() { Name = "MCP管理", IconKind = PackIconMaterialDesignKind.Api, PageKey = "mcp" },
        new() { Name = "系统设置", IconKind = PackIconMaterialDesignKind.Settings, PageKey = "config" },
    ];

    private readonly Dictionary<string, UIElement> _pageCache = [];

    private static string SettingsPath => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "window_settings.json");

    public MainWindow()
    {
        InitializeComponent();

        HeaderIcon.Kind = PackIconMaterialDesignKind.Android;
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
            if (!File.Exists(SettingsPath))
            {
                goto defaults;
            }

            var json = File.ReadAllText(SettingsPath);
            var s = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (s == null)
            {
                goto defaults;
            }

            if (s.TryGetValue("Left", out var left))
            {
                Left = left.GetDouble();
            }

            if (s.TryGetValue("Top", out var top))
            {
                Top = top.GetDouble();
            }

            if (s.TryGetValue("Width", out var w) && w.GetDouble() > 400)
            {
                Width = w.GetDouble();
            }

            if (s.TryGetValue("Height", out var h) && h.GetDouble() > 300)
            {
                Height = h.GetDouble();
            }

            SidebarExpanded = !s.TryGetValue("SidebarExpanded", out var se) || se.GetBoolean();
            SetExpanded(SidebarExpanded);

            if (s.TryGetValue("LastTab", out var tab))
            {
                var pageKey = tab.GetString();
                var idx = Array.FindIndex(NavItems, n => n.PageKey == pageKey);
                if (idx >= 0)
                {
                    NavList.SelectedIndex = idx;
                }
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
        if (!_restored)
        {
            return;
        }

        try
        {
            var state = new Dictionary<string, object>
            {
                ["Left"] = Left,
                ["Top"] = Top,
                ["Width"] = Width,
                ["Height"] = Height,
                ["SidebarExpanded"] = SidebarExpanded,
            };
            if (NavList.SelectedItem is NavigationItem item)
            {
                state["LastTab"] = item.PageKey;
            }

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

    private void ToggleSidebar(object sender, RoutedEventArgs e)
    {
        SidebarExpanded = !SidebarExpanded;
        SetExpanded(SidebarExpanded);
        SaveWindowState();
    }

    private static readonly System.Windows.Media.Animation.IEasingFunction Ease
        = new System.Windows.Media.Animation.SineEase
        {
            EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut
        };

    private void SetExpanded(bool expanded)
    {
        var duration = new Duration(TimeSpan.FromMilliseconds(250));

        // ── Animate sidebar width ──
        Sidebar.BeginAnimation(FrameworkElement.WidthProperty,
            new System.Windows.Media.Animation.DoubleAnimation(
                expanded ? ExpandedWidth : CollapsedWidth, duration)
            { EasingFunction = Ease });

        SidebarExpanded = expanded;
        HeaderExpanded.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;

        ToggleIcon.Kind = expanded
            ? PackIconMaterialDesignKind.ChevronLeft
            : PackIconMaterialDesignKind.Menu;

        ToggleBtn.HorizontalAlignment = expanded
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Center;

        // ── Animate item left margins: expanded=14, collapsed=center icon ──
        var targetMargin = expanded
            ? new Thickness(14, 12, 14, 12)
            : new Thickness(5, 12, 0, 12); // slight nudge right when collapsed

        foreach (var item in NavList.Items)
        {
            var container = NavList.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
            if (container == null)
            {
                continue;
            }

            var sp = FindVisualChild<StackPanel>(container);
            sp?.BeginAnimation(FrameworkElement.MarginProperty,
                new System.Windows.Media.Animation.ThicknessAnimation(targetMargin, duration)
                { EasingFunction = Ease });
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t)
            {
                return t;
            }

            var result = FindVisualChild<T>(child);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }

    // ═══════════════════════════════════════════════════
    //  Navigation
    // ═══════════════════════════════════════════════════

    private void OnNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is not NavigationItem item)
        {
            return;
        }

        ContentArea.Content = item.PageKey switch
        {
            "config" => GetOrCreate("config", () => new ConfigurationView()),
            "chat" => GetOrCreate("chat", () => new ChatTestView()),
            "keys" => GetOrCreate("keys", () => new KeyManagementView()),
            "token" => GetOrCreate("token", () => new TokenUsageView()),
            "mcp" => GetOrCreate("mcp", () => new MCPManagementView()),
            _ => new TextBlock
            {
                Text = $"{item.Name} — 待实现",
                FontSize = 16
            }
        };

        if (!SidebarExpanded)
        {
            Dispatcher.BeginInvoke(() => SetExpanded(false));
        }

        SaveWindowState();
    }

    private UIElement GetOrCreate(string key, Func<UIElement> factory)
    {
        if (!_pageCache.TryGetValue(key, out var page))
        {
            page = factory();
            _pageCache[key] = page;
        }
        return page;
    }
}