using System.IO;

namespace LTTPEnhancementTools.Models;

public class LibraryEntry
{
    public string Name        { get; set; } = string.Empty;  // filename without extension
    public string SourcePath  { get; set; } = string.Empty;
    public string? CachedPcmPath { get; set; }               // null = not yet cached or source is newer

    public bool   IsPcm      => Path.GetExtension(SourcePath).Equals(".pcm", StringComparison.OrdinalIgnoreCase);
    public string FormatTag  => Path.GetExtension(SourcePath).TrimStart('.').ToUpperInvariant();
    public string AssignablePath  => CachedPcmPath ?? SourcePath;
    public bool   NeedsConversion => !IsPcm && CachedPcmPath is null;
    public bool   IsCached        => CachedPcmPath is not null;
}
