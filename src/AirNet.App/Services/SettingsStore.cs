using System.Text.Json;
using System.Text.Json.Serialization;

namespace AirNet.App.Services;

/// <summary>Mutable preferences written to settings.json. Defaults match the prototype's behaviour.</summary>
public sealed class AppSettings
{
    public bool AutoRefresh { get; set; }
    public bool DualProbe { get; set; } = true;
    public bool KeepHistory { get; set; } = true;
    public string Theme { get; set; } = "Light";
}

/// <summary>Source-generated JSON keeps the settings file trim-safe like the history store.</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext;

/// <summary>
/// Loads/saves the small preference file. A missing or corrupt file is not an error: the app
/// falls back to defaults and keeps working.
/// </summary>
public sealed class SettingsStore
{
    private readonly string _filePath;

    public SettingsStore(string? directory = null)
    {
        // The screenshot pipeline isolates both history and settings behind AIRNET_DATA_DIR.
        var root = directory
                   ?? Environment.GetEnvironmentVariable("AIRNET_DATA_DIR")
                   ?? Path.Combine(
                       Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                       "AirNet");
        _filePath = Path.Combine(root, "settings.json");
    }

    public string FilePath => _filePath;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new AppSettings();
            }

            var text = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize(text, AppSettingsJsonContext.Default.AppSettings)
                   ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                _filePath,
                JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.AppSettings));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Preferences are a convenience; failing to persist them must not break a scan.
        }
    }
}
