using Another_Mirai_Native.Abstractions.Services;

namespace ChatGPTv3.UI.Mock;

/// <summary>
/// Mock ILogger — forwards all log calls to the debug output (System.Diagnostics.Debug).
/// Used by the standalone UI test environment where the AMN2 host logger is unavailable.
/// </summary>
public sealed class MockLogger : ILogger
{
    public static MockLogger Instance { get; } = new();

    private static void Write(string level, string tag, string message)
        => System.Diagnostics.Debug.WriteLine($"[{level}] [{tag}] {message}");

    public void Debug(string type, string message) => Write("DEBUG", type, message);

    public void Info(string type, string message) => Write("INFO", type, message);

    public void Warn(string type, string message) => Write("WARN", type, message);

    public void Error(string type, string message) => Write("ERROR", type, message);

    public void Fatal(string message) => Write("FATAL", "", message);
}