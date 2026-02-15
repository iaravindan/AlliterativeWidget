using System.Reflection;
using System.Text.Json;
using AlliterativeWidget.Models;

namespace AlliterativeWidget.Services;

public class PersistenceService : IPersistenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _appDataPath;
    private readonly string _configPath;
    private readonly string _historyPath;

    public PersistenceService()
    {
        _appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AlliterativeWidget");
        _configPath = Path.Combine(_appDataPath, "config.json");
        _historyPath = Path.Combine(_appDataPath, "history.json");

        EnsureDirectoryExists();
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_appDataPath))
        {
            Directory.CreateDirectory(_appDataPath);
        }
    }

    public WidgetConfig LoadConfig()
    {
        var defaultConfig = LoadDefaultConfig();

        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<WidgetConfig>(json, JsonOptions);
                if (config != null && IsValidConfig(config))
                {
                    // Merge missing fields from defaults
                    MergeWithDefaults(config, defaultConfig);
                    return config;
                }
            }
        }
        catch
        {
            // Fall through to default config
        }

        return defaultConfig;
    }

    private static void MergeWithDefaults(WidgetConfig config, WidgetConfig defaults)
    {
        // Merge weekend prefixes if missing
        if (config.Content.Prefixes.Saturday.Count == 0)
            config.Content.Prefixes.Saturday = defaults.Content.Prefixes.Saturday;
        if (config.Content.Prefixes.Sunday.Count == 0)
            config.Content.Prefixes.Sunday = defaults.Content.Prefixes.Sunday;

        // Merge weekend punchlines if missing
        if (config.Content.WeekendPunchlines.Count == 0)
            config.Content.WeekendPunchlines = defaults.Content.WeekendPunchlines;
    }

    private static bool IsValidConfig(WidgetConfig config)
    {
        return config.Content?.Prefixes != null &&
               config.Content?.Punchlines?.Count > 0;
    }

    private static WidgetConfig LoadDefaultConfig()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "AlliterativeWidget.Resources.DefaultConfig.json";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                return JsonSerializer.Deserialize<WidgetConfig>(json, JsonOptions) ?? new WidgetConfig();
            }
        }
        catch
        {
            // Fall through to empty config
        }

        return new WidgetConfig();
    }

    public void SaveConfig(WidgetConfig config)
    {
        try
        {
            EnsureDirectoryExists();
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(_configPath, json);
        }
        catch
        {
            // Silently fail - config save is non-critical
        }
    }

    public WidgetHistory LoadHistory()
    {
        try
        {
            if (File.Exists(_historyPath))
            {
                var json = File.ReadAllText(_historyPath);
                return JsonSerializer.Deserialize<WidgetHistory>(json, JsonOptions) ?? new WidgetHistory();
            }
        }
        catch
        {
            // Fall through to new history
        }

        return new WidgetHistory();
    }

    public void SaveHistory(WidgetHistory history)
    {
        try
        {
            EnsureDirectoryExists();
            var json = JsonSerializer.Serialize(history, JsonOptions);
            File.WriteAllText(_historyPath, json);
        }
        catch
        {
            // Silently fail - history save is non-critical
        }
    }
}
