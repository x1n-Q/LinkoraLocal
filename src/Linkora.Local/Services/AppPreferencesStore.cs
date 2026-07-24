using System.IO;
using System.Text.Json;

namespace Linkora.Local.Services;

internal sealed class AppPreferences
{
    public bool AlwaysOnEnabled { get; set; }
    public string? Hostname { get; set; }
    public string? ServiceUrl { get; set; }
}

internal static class AppPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string SettingsDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Linkora",
            "Local");

    private static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public static AppPreferences Read()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppPreferences();
            }

            return JsonSerializer.Deserialize<AppPreferences>(
                       File.ReadAllText(SettingsPath),
                       JsonOptions)
                   ?? new AppPreferences();
        }
        catch
        {
            return new AppPreferences();
        }
    }

    public static void Save(AppPreferences preferences)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var temporaryPath = SettingsPath + ".tmp";
        File.WriteAllText(
            temporaryPath,
            JsonSerializer.Serialize(preferences, JsonOptions));
        File.Move(temporaryPath, SettingsPath, overwrite: true);
    }
}
