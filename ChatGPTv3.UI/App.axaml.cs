using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace ChatGPTv3.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Determine app directory — use command-line arg or current directory
            var appDir = desktop.Args is { Length: > 0 } args
                ? args[0]
                : AppDomain.CurrentDomain.BaseDirectory;

            UIBootstrap.Initialize(appDir);

            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
