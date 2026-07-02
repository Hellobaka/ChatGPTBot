using Another_Mirai_Native.Abstractions.Attributes;
using Another_Mirai_Native.Abstractions.Context;
using Another_Mirai_Native.Abstractions.Handlers;
using System.Windows;

namespace ChatGPTv3.UI;

[Menu("ChatGPTv3 管理面板")]
public class MenuEntry : IMenuHandler
{
    private MainWindow? _window;
    private Thread? _uiThread;
    private Application? _wpfApp;

    public void OnMenu(MenuContext e)
    {
        if (_window != null)
        {
            // Window already exists — just show it
            _window.Dispatcher.Invoke(() =>
            {
                _window.Show();
                _window.Activate();
            });
            return;
        }

        // Create WPF window on dedicated STA thread (required for WPF input)
        using var ready = new ManualResetEventSlim(false);

        _uiThread = new Thread(() =>
        {
            // ── Create Application and load HandyControl themes ──
            _wpfApp = new Application();

            var skinDarkUri = new Uri(
                "pack://application:,,,/HandyControl;component/Themes/SkinDark.xaml",
                UriKind.Absolute);
            var themeUri = new Uri(
                "pack://application:,,,/HandyControl;component/Themes/Theme.xaml",
                UriKind.Absolute);

            // Application-level
            _wpfApp.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = skinDarkUri });
            _wpfApp.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = themeUri });

            // ── Create window and inject themes directly ──
            _window = new MainWindow();
            _window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = skinDarkUri });
            _window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = themeUri });

            _window.Closing += (_, ev) =>
            {
                ev.Cancel = true;
                _window.Hide();
            };

            ready.Set();

            // ── Start WPF dispatcher loop ──
            _wpfApp.Run();
        })
        {
            IsBackground = true,
        };
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();
        ready.Wait();

        // Show the window (must use Dispatcher — we're on the framework UI thread)
        _window?.Dispatcher.Invoke(() => _window.Show());
    }
}