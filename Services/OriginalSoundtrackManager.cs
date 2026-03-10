using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.RegularExpressions;
using LTTPEnhancementTools.Models;

namespace LTTPEnhancementTools.Services;

/// <summary>
/// Manages importing, matching, converting, and caching original ALttP soundtrack files.
/// Users provide MP3/WAV files which are converted to MSU-1 PCM and cached locally.
/// </summary>
public static partial class OriginalSoundtrackManager
{
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LTTPEnhancementTools", "OriginalAudio");

    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav", ".wma", ".aac", ".m4a", ".aiff", ".aif", ".flac" };

    /// <summary>
    /// Common OST name aliases → MSU slot number. Handles the Internet Archive OST
    /// and other common soundtrack releases that use different names than MSU slots.
    /// </summary>
    private static readonly Dictionary<string, int> OstAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // Disc 1 of the common Internet Archive / official OST release
        ["title"]                    = 1,
        ["link to the past"]         = 1,
        ["beginning of the journey"] = 6,  // Prologue
        ["seal of seven maidens"]    = 3,  // Rainy Intro
        ["time of the falling rain"] = 3,  // Rainy Intro (alt name)
        ["majestic castle"]          = 16, // Hyrule Castle
        ["princess zeldas rescue"]   = 25, // Zelda Rescued
        ["safety in the sanctuary"]  = 20, // Sanctuary
        ["hyrule field main theme"]  = 2,  // Light World
        ["hyrule field"]             = 2,
        ["kakariko village"]         = 7,  // Kakariko
        ["guessing game house"]      = 14, // Minigame
        ["fortune teller"]           = 23, // Shop
        ["soldiers of kakariko"]     = 12, // Guards Appear
        ["dank dungeons"]            = 17, // Pendant Dungeon
        ["lost ancient ruins"]       = 18, // Cave
        ["anger of the guardians"]   = 21, // Boss Battle
        ["great victory"]            = 19, // Boss Victory
        ["silly pink rabbit"]        = 4,  // Bunny Theme
        ["forest of mystery"]        = 5,  // Lost Woods
        ["unsealing the master sword"] = 10, // Pedestal Pull
        ["priest of the dark order"] = 28, // Agahnim Floor
        ["dark golden land"]         = 9,  // Dark World
        ["black mist"]               = 13, // Dark Death Mtn.
        ["dungeon of shadows"]       = 22, // Crystal Dungeon
        ["meeting the maidens"]      = 26, // Crystal Retrieved
        ["goddess appears"]          = 27, // Fairy
        ["release of ganon"]         = 29, // Ganon Reveal
        ["ganons message"]           = 30, // Ganon Dropdown
        ["prince of darkness"]       = 31, // Ganon Battle
        ["power of the gods"]        = 32, // Triforce
        ["epilogue"]                 = 33, // Epilogue
        ["beautiful hyrule"]         = 33, // Epilogue (alt name)
        ["staff roll"]               = 34, // Credits
        ["credits"]                  = 34,
        // Additional common aliases
        ["overworld"]                = 2,
        ["dark world theme"]         = 9,
        ["file select"]              = 11,
        ["game over"]                = 11,
        ["sanctuary"]                = 20,
        ["lost woods"]               = 5,
        ["kakariko"]                 = 7,
        ["ganon battle"]             = 31,
        ["ganon fight"]              = 31,
        ["triforce"]                 = 32,
        ["fairy fountain"]           = 27,
        ["boss battle"]              = 21,
        ["boss victory"]             = 19,
        ["cave"]                     = 18,
        ["shop"]                     = 23,
        ["minigame"]                 = 14,
        ["skull woods"]              = 15,
        ["dimensional shift"]        = 8,  // Portal Sound
        ["teleport"]                 = 8,
    };

    // ── Cache loading / clearing ────────────────────────────────────────────

    /// <summary>
    /// Checks the cache directory for previously converted originals and sets
    /// OriginalPcmPath on any matching track slots.
    /// </summary>
    public static void LoadCachedOriginals(IReadOnlyList<TrackSlot> tracks)
    {
        if (!Directory.Exists(CacheDir)) return;

        foreach (var track in tracks)
        {
            string cached = Path.Combine(CacheDir, $"{track.SlotNumber:D2}.pcm");
            if (File.Exists(cached))
                track.OriginalPcmPath = cached;
        }
    }

    /// <summary>
    /// Deletes all cached original PCM files and clears OriginalPcmPath on all tracks.
    /// </summary>
    public static void ClearCache(IReadOnlyList<TrackSlot> tracks)
    {
        foreach (var track in tracks)
            track.OriginalPcmPath = null;

        if (!Directory.Exists(CacheDir)) return;

        try { Directory.Delete(CacheDir, true); } catch { }
    }

    // ── Import from folder ─────────────────────────────────────────────────

    /// <summary>
    /// Imports audio files from a folder, matches them to track slots, converts to MSU-1 PCM,
    /// and caches the results. Returns null on success or an error message.
    /// </summary>
    public static async Task<string?> ImportFromFolderAsync(
        string folderPath,
        IReadOnlyList<TrackSlot> tracks,
        IProgress<(int current, int total, string trackName)>? progress = null,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(folderPath))
            return "Folder does not exist.";

        var audioFiles = Directory.GetFiles(folderPath)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        if (audioFiles.Count == 0)
            return "No supported audio files found in folder. Supported formats: MP3, WAV, WMA, AAC, M4A, AIFF, FLAC.";

        var matches = MatchFilesToSlots(audioFiles, tracks);
        if (matches.Count == 0)
            return "Could not match any files to track slots. Files should be numbered (e.g., '01 - Opening.mp3') or named to match track names.";

        return await ConvertAndCacheAsync(matches, tracks, progress, ct);
    }

    // ── Import from ZIP ────────────────────────────────────────────────────

    /// <summary>
    /// Imports audio files from a ZIP archive. Extracts to a temp directory, matches,
    /// converts, and caches. Returns null on success or an error message.
    /// </summary>
    public static async Task<string?> ImportFromZipAsync(
        string zipPath,
        IReadOnlyList<TrackSlot> tracks,
        IProgress<(int current, int total, string trackName)>? progress = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(zipPath))
            return "ZIP file does not exist.";

        string tempDir = Path.Combine(Path.GetTempPath(), "LTTPOriginalImport_" + Guid.NewGuid().ToString("N")[..8]);

        try
        {
            Directory.CreateDirectory(tempDir);
            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, tempDir), ct);

            // Collect all audio files recursively (ZIP may have subdirectories)
            var audioFiles = Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)))
                .ToList();

            if (audioFiles.Count == 0)
                return "No supported audio files found in ZIP. Supported formats: MP3, WAV, WMA, AAC, M4A, AIFF, FLAC.";

            var matches = MatchFilesToSlots(audioFiles, tracks);
            if (matches.Count == 0)
                return "Could not match any files to track slots. Files should be numbered (e.g., '01 - Opening.mp3') or named to match track names.";

            return await ConvertAndCacheAsync(matches, tracks, progress, ct);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    // ── Import from URL ────────────────────────────────────────────────────

    /// <summary>
    /// Downloads a ZIP from the given URL, then imports it. Returns null on success or an error message.
    /// </summary>
    public static async Task<string?> ImportFromUrlAsync(
        string url,
        IReadOnlyList<TrackSlot> tracks,
        System.Net.Http.HttpClient http,
        IProgress<(int current, int total, string trackName)>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "URL cannot be empty.";

        string tempZip = Path.Combine(Path.GetTempPath(), "LTTPOriginal_" + Guid.NewGuid().ToString("N")[..8] + ".zip");

        try
        {
            progress?.Report((0, 1, "Downloading…"));

            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                return $"Download failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}";

            await using (var stream = await response.Content.ReadAsStreamAsync(ct))
            await using (var file = File.Create(tempZip))
            {
                await stream.CopyToAsync(file, ct);
            }

            return await ImportFromZipAsync(tempZip, tracks, progress, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return $"Download failed: {ex.Message}";
        }
        finally
        {
            try { File.Delete(tempZip); } catch { }
        }
    }

    // ── File-to-slot matching ──────────────────────────────────────────────

    /// <summary>
    /// Matches a list of audio file paths to track slots by number or name.
    /// Returns a dictionary of slotNumber → filePath.
    /// </summary>
    public static Dictionary<int, string> MatchFilesToSlots(
        IReadOnlyList<string> filePaths,
        IReadOnlyList<TrackSlot> tracks)
    {
        var result = new Dictionary<int, string>();
        var slotLookup = tracks.ToDictionary(t => t.SlotNumber);
        var usedSlots = new HashSet<int>();

        // Pass 1: match by OST alias table (most precise — handles Internet Archive and other common releases)
        foreach (var filePath in filePaths)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            // Strip leading numbers and common separators: "01. Title ~ Link to the Past" → "Title ~ Link to the Past"
            string cleaned = LeadingNumberRegex().Replace(fileName, "")
                .Replace('_', ' ').Replace('-', ' ').Replace('~', ' ')
                .Replace(".", " ").Replace("'", "").Replace("\u2019", "").Trim();

            foreach (var (alias, slot) in OstAliases)
            {
                if (usedSlots.Contains(slot)) continue;
                if (!slotLookup.ContainsKey(slot)) continue;

                if (cleaned.Contains(alias, StringComparison.OrdinalIgnoreCase))
                {
                    result[slot] = filePath;
                    usedSlots.Add(slot);
                    break;
                }
            }
        }

        // Pass 2: match by leading number in filename (for files not matched by alias)
        foreach (var filePath in filePaths)
        {
            if (result.ContainsValue(filePath)) continue;

            string fileName = Path.GetFileNameWithoutExtension(filePath);
            var match = LeadingNumberRegex().Match(fileName);
            if (match.Success && int.TryParse(match.Value, out int slot) && slot >= 1 && slot <= 61 && slotLookup.ContainsKey(slot))
            {
                if (usedSlots.Add(slot))
                    result[slot] = filePath;
            }
        }

        // Pass 3: match by number anywhere in filename
        foreach (var filePath in filePaths)
        {
            if (result.ContainsValue(filePath)) continue;

            string fileName = Path.GetFileNameWithoutExtension(filePath);
            var matches = AnyNumberRegex().Matches(fileName);
            foreach (Match m in matches)
            {
                if (int.TryParse(m.Value, out int slot) && slot >= 1 && slot <= 61 && slotLookup.ContainsKey(slot) && usedSlots.Add(slot))
                {
                    result[slot] = filePath;
                    break;
                }
            }
        }

        // Pass 4: fuzzy name match for remaining unmatched files
        foreach (var filePath in filePaths)
        {
            if (result.ContainsValue(filePath)) continue;

            string fileName = Path.GetFileNameWithoutExtension(filePath)
                .Replace('_', ' ').Replace('-', ' ').Trim();

            foreach (var track in tracks)
            {
                if (usedSlots.Contains(track.SlotNumber)) continue;

                // Case-insensitive substring match in either direction
                if (fileName.Contains(track.Name, StringComparison.OrdinalIgnoreCase) ||
                    track.Name.Contains(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    result[track.SlotNumber] = filePath;
                    usedSlots.Add(track.SlotNumber);
                    break;
                }
            }
        }

        return result;
    }

    // ── Convert and cache ──────────────────────────────────────────────────

    private static async Task<string?> ConvertAndCacheAsync(
        Dictionary<int, string> matches,
        IReadOnlyList<TrackSlot> tracks,
        IProgress<(int current, int total, string trackName)>? progress,
        CancellationToken ct)
    {
        Directory.CreateDirectory(CacheDir);

        var slotLookup = tracks.ToDictionary(t => t.SlotNumber);
        var sorted = matches.OrderBy(kv => kv.Key).ToList();
        int completed = 0;
        int failed = 0;

        foreach (var (slot, sourcePath) in sorted)
        {
            ct.ThrowIfCancellationRequested();

            string trackName = slotLookup.TryGetValue(slot, out var ts) ? ts.Name : $"Track {slot}";
            progress?.Report((completed + 1, sorted.Count, trackName));

            string destPath = Path.Combine(CacheDir, $"{slot:D2}.pcm");

            // Skip if already cached
            if (!File.Exists(destPath))
            {
                string? error = await PcmConverter.ConvertAsync(sourcePath, destPath, ct: ct);
                if (error is not null)
                {
                    failed++;
                    completed++;
                    continue;
                }
            }

            // Set the path on the track slot
            if (slotLookup.TryGetValue(slot, out var trackSlot))
                trackSlot.OriginalPcmPath = destPath;

            completed++;
        }

        if (failed > 0 && failed == sorted.Count)
            return "All conversions failed. Ensure the files are valid audio files.";

        if (failed > 0)
            return $"{sorted.Count - failed} of {sorted.Count} tracks imported. {failed} file(s) failed to convert.";

        return null; // success
    }

    // ── Regex patterns ─────────────────────────────────────────────────────

    [GeneratedRegex(@"^\d+")]
    private static partial Regex LeadingNumberRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex AnyNumberRegex();
}
