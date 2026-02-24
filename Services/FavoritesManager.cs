using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace LTTPEnhancementTools.Services;

public static class FavoritesManager
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LTTPEnhancementTools", "sprite_favorites.json");

    public static HashSet<string> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<HashSet<string>>(json) ?? new();
        }
        catch { return new(); }
    }

    public static void Save(HashSet<string> favorites)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(favorites));
        }
        catch { }
    }
}
