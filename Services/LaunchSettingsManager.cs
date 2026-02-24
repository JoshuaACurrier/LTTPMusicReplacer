using System.IO;
using System.Text.Json;

namespace LTTPEnhancementTools.Services;

public static class LaunchSettingsManager
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LTTPEnhancementTools", "launchSettings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Returns null if the file doesn't exist (triggers first-run wizard).</summary>
    public static LaunchSettings? TryLoad()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return null;
            string json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<LaunchSettings>(json, JsonOptions) ?? new LaunchSettings();
        }
        catch
        {
            return null;
        }
    }

    public static void Save(LaunchSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch { /* best-effort */ }
    }

    public static bool FileExists() => File.Exists(SettingsPath);
}
