using System.Text.Json;

namespace ChatGPTv3.Core.Config;

/// <summary>
/// JSON configuration manager with FileSystemWatcher-based hot reload.
/// Uses System.Text.Json exclusively. Thread-safe reads/writes.
/// </summary>
public static class ConfigManager
{
    private static string _configPath = string.Empty;
    private static JsonDocument? _config;
    private static readonly object ReadLock = new();
    private static readonly object WriteLock = new();
    private static FileSystemWatcher? _watcher;
    private static bool _hotReloadEnabled;

    /// <summary>
    /// Initializes the config system. Called once at startup with the app directory.
    /// </summary>
    public static void Initialize(string appDirectory)
    {
        _configPath = Path.Combine(appDirectory, "Config.json");
    }

    /// <summary>
    /// Loads configuration from disk. Returns false if the file is missing or invalid.
    /// </summary>
    public static bool Load()
    {
        lock (ReadLock)
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    File.WriteAllText(_configPath, "{}");
                }

                var json = File.ReadAllText(_configPath);
                _config?.Dispose();
                _config = JsonDocument.Parse(json);
                return true;
            }
            catch (Exception ex)
            {
                LogWarning?.Invoke("配置加载", $"Load failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Gets a configuration value by key. Returns defaultValue if the key is not found.
    /// </summary>
    public static T GetConfig<T>(string key, T defaultValue = default!) where T : notnull
    {
        lock (ReadLock)
        {
            if (_config != null && _config.RootElement.TryGetProperty(key, out var element))
            {
                try
                {
                    var result = JsonSerializer.Deserialize<T>(element.GetRawText());
                    if (result != null) return result;
                }
                catch { }
            }

            // Return default if not found or deserialization fails
            if (defaultValue != null)
            {
                SetConfig(key, defaultValue);
                return defaultValue;
            }

            return GetDefaultForType<T>();
        }
    }

    /// <summary>
    /// Sets a configuration value and persists to disk.
    /// </summary>
    public static void SetConfig<T>(string key, T value)
    {
        lock (WriteLock)
        {
            DisableHotReload();
            try
            {
                var json = File.Exists(_configPath)
                    ? File.ReadAllText(_configPath)
                    : "{}";

                using var doc = JsonDocument.Parse(json);
                var root = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? [];

                var valueJson = JsonSerializer.Serialize(value);
                root[key] = JsonDocument.Parse(valueJson).RootElement;

                var newJson = JsonSerializer.Serialize(root, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                File.WriteAllText(_configPath, newJson);

                // Reload into memory
                _config?.Dispose();
                _config = JsonDocument.Parse(newJson);
            }
            catch (Exception ex)
            {
                LogWarning?.Invoke("配置保存", $"Failed to save {key}: {ex.Message}");
            }
            finally
            {
                EnableHotReload();
            }
        }
    }

    /// <summary>
    /// Enables FileSystemWatcher-based hot reload.
    /// </summary>
    public static void EnableHotReload()
    {
        if (_hotReloadEnabled) return;
        if (string.IsNullOrEmpty(_configPath)) return;

        try
        {
            _watcher = new FileSystemWatcher(
                Path.GetDirectoryName(_configPath)!,
                Path.GetFileName(_configPath))
            {
                NotifyFilter = NotifyFilters.LastWrite
            };
            _watcher.Changed += OnConfigFileChanged;
            _watcher.EnableRaisingEvents = true;
            _hotReloadEnabled = true;
        }
        catch (Exception ex)
        {
            LogWarning?.Invoke("配置热重载", $"Failed to enable: {ex.Message}");
        }
    }

    /// <summary>
    /// Temporarily disables hot reload (used during config writes).
    /// </summary>
    public static void DisableHotReload()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
        }
        _hotReloadEnabled = false;
    }

    private static void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType == WatcherChangeTypes.Changed)
        {
            if (Load())
            {
                OnConfigReloaded?.Invoke();
            }
        }
    }

    /// <summary>
    /// Fires when config is hot-reloaded. Subscribe to re-initialize dependent state.
    /// </summary>
    public static event Action? OnConfigReloaded;

    /// <summary>
    /// Logger function. Set by Entry during initialization.
    /// </summary>
    public static Action<string, string>? LogWarning;

    private static T GetDefaultForType<T>()
    {
        if (typeof(T) == typeof(string)) return (T)(object)"";
        if (typeof(T) == typeof(int)) return (T)(object)0;
        if (typeof(T) == typeof(long)) return (T)(object)0L;
        if (typeof(T) == typeof(bool)) return (T)(object)false;
        if (typeof(T) == typeof(double)) return (T)(object)0.0;
        if (typeof(T) == typeof(float)) return (T)(object)0f;
        if (typeof(T) == typeof(ushort)) return (T)(object)(ushort)0;
        if (typeof(T) == typeof(List<long>)) return (T)(object)new List<long>();
        if (typeof(T) == typeof(List<string>)) return (T)(object)new List<string>();
        throw new InvalidOperationException($"Cannot create default for type {typeof(T)}");
    }
}
