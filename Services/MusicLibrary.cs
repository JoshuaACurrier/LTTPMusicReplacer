using System.IO;
using LTTPEnhancementTools.Models;

namespace LTTPEnhancementTools.Services;

public class MusicLibrary
{
    private static readonly HashSet<string> SupportedExts =
        new(StringComparer.OrdinalIgnoreCase)
        { ".pcm", ".mp3", ".wav", ".wma", ".wmv", ".aac", ".m4a", ".mp4", ".aiff", ".aif" };

    public string? LibraryFolder { get; private set; }
    public IReadOnlyList<LibraryEntry> Entries { get; private set; } = Array.Empty<LibraryEntry>();

    public void SetFolder(string? folder)
    {
        LibraryFolder = string.IsNullOrWhiteSpace(folder) ? null : folder;
        Refresh();
    }

    public void Refresh()
    {
        if (LibraryFolder is null || !Directory.Exists(LibraryFolder))
        {
            Entries = Array.Empty<LibraryEntry>();
            return;
        }

        var entries = new List<LibraryEntry>();

        foreach (var file in Directory.EnumerateFiles(LibraryFolder)
                     .Where(f => SupportedExts.Contains(Path.GetExtension(f)))
                     .OrderBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            string ext  = Path.GetExtension(file).ToLowerInvariant();

            string? cachedPcm = null;
            if (ext == ".pcm")
            {
                cachedPcm = file; // PCM is already the assignable format
            }
            else
            {
                string cachePath = GetCacheTargetPath(file);
                if (File.Exists(cachePath)
                    && File.GetLastWriteTimeUtc(cachePath) >= File.GetLastWriteTimeUtc(file))
                {
                    cachedPcm = cachePath; // valid cache — source hasn't changed since conversion
                }
            }

            entries.Add(new LibraryEntry
            {
                Name         = name,
                SourcePath   = file,
                CachedPcmPath = cachedPcm
            });
        }

        Entries = entries;
    }

    /// <summary>Returns the path where a converted .pcm should be stored for the given source file.</summary>
    public string GetCacheTargetPath(string sourcePath) =>
        Path.Combine(LibraryFolder!, "_cache",
            Path.GetFileNameWithoutExtension(sourcePath) + ".pcm");
}
