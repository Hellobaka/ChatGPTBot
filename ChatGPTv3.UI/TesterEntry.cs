using System.Threading;

namespace ChatGPTv3.UI;

/// <summary>
/// Direct debug entry point (WinExe). Bypasses AMN2 host entirely.
/// </summary>
public static class TesterEntry
{
    [STAThread]
    public static void Main()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        UIBootstrap.Initialize(appDir);

        var menuEntry = new MenuEntry();
        menuEntry.OnMenu(null!);

        // Keep process alive while WPF window is open
        new ManualResetEvent(false).WaitOne();
    }
}
