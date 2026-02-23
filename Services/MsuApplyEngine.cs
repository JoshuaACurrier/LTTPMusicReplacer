using System.IO;

namespace LTTPMusicReplacer.Services;

public record ApplyRequest(
    string RomSourcePath,
    string OutputDir,
    IReadOnlyDictionary<string, string> Tracks, // slot -> pcm path
    OverwriteMode OverwriteMode
);

public enum OverwriteMode { Ask, Overwrite, Skip }

public record ApplyConflict(string FileName, string DestPath);

public record ApplySuccess(IReadOnlyList<string> FilesWritten);

public class ApplyEngine
{
    /// <summary>
    /// Raised when conflicts are detected and OverwriteMode is Ask.
    /// The caller must set e.Resolution before the task can continue.
    /// </summary>
    public event EventHandler<ConflictsDetectedEventArgs>? ConflictsDetected;

    public async Task<ApplySuccess> RunAsync(
        ApplyRequest req,
        IProgress<(string step, int current, int total)> progress,
        CancellationToken ct = default)
    {
        var sortedSlots = req.Tracks
            .OrderBy(kv => int.Parse(kv.Key))
            .ToList();

        int totalSteps = 4 + sortedSlots.Count; // validate, mkdir, rom, msu, pcms

        // STEP 1 — Validate
        progress.Report(("Validating inputs...", 0, totalSteps));
        ct.ThrowIfCancellationRequested();

        if (!File.Exists(req.RomSourcePath))
            throw new FileNotFoundException($"ROM file not found: {req.RomSourcePath}");

        foreach (var (slot, pcmPath) in sortedSlots)
        {
            if (!File.Exists(pcmPath))
                throw new FileNotFoundException($"PCM file for slot {slot} not found: {pcmPath}");
        }

        // STEP 2 — Compute output names
        string romExt = Path.GetExtension(req.RomSourcePath);
        string baseName = Path.GetFileNameWithoutExtension(req.RomSourcePath);
        string romDest = Path.Combine(req.OutputDir, baseName + romExt);
        string msuDest = Path.Combine(req.OutputDir, baseName + ".msu");
        var pcmDests = sortedSlots
            .Select(kv => (slot: kv.Key, src: kv.Value,
                           dest: Path.Combine(req.OutputDir, $"{baseName}-{kv.Key}.pcm")))
            .ToList();

        // STEP 3 — Conflict detection
        progress.Report(("Checking for conflicts...", 1, totalSteps));
        var allDests = new List<ApplyConflict>();
        allDests.Add(new(Path.GetFileName(romDest), romDest));
        allDests.Add(new(Path.GetFileName(msuDest), msuDest));
        allDests.AddRange(pcmDests.Select(p => new ApplyConflict(Path.GetFileName(p.dest), p.dest)));

        var conflicts = allDests.Where(d => File.Exists(d.DestPath)).ToList();

        OverwriteMode effectiveMode = req.OverwriteMode;
        if (conflicts.Count > 0 && req.OverwriteMode == OverwriteMode.Ask)
        {
            var args = new ConflictsDetectedEventArgs(conflicts);
            ConflictsDetected?.Invoke(this, args);
            await args.ResolutionTask; // wait for UI to respond
            effectiveMode = args.Resolution;

            if (effectiveMode == OverwriteMode.Ask) // user cancelled
                throw new OperationCanceledException("Apply cancelled by user.");
        }

        var skipPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (effectiveMode == OverwriteMode.Skip)
        {
            foreach (var c in conflicts) skipPaths.Add(c.DestPath);
        }

        // STEP 4 — Create directory
        progress.Report(("Creating output directory...", 2, totalSteps));
        Directory.CreateDirectory(req.OutputDir);

        var filesWritten = new List<string>();

        // STEP 5 — Copy ROM
        progress.Report(("Copying ROM...", 3, totalSteps));
        if (!skipPaths.Contains(romDest))
        {
            await Task.Run(() => File.Copy(req.RomSourcePath, romDest, overwrite: effectiveMode == OverwriteMode.Overwrite), ct);
            filesWritten.Add(romDest);
        }

        // STEP 6 — Write 0-byte .msu
        progress.Report(("Writing .msu marker...", 4, totalSteps));
        if (!skipPaths.Contains(msuDest))
        {
            await Task.Run(() => File.WriteAllBytes(msuDest, Array.Empty<byte>()), ct);
            filesWritten.Add(msuDest);
        }

        // STEP 7 — Copy PCMs
        for (int i = 0; i < pcmDests.Count; i++)
        {
            var (slot, src, dest) = pcmDests[i];
            progress.Report(($"Copying track {slot}...", 5 + i, totalSteps));
            ct.ThrowIfCancellationRequested();

            if (!skipPaths.Contains(dest))
            {
                await Task.Run(() => File.Copy(src, dest, overwrite: effectiveMode == OverwriteMode.Overwrite), ct);
                filesWritten.Add(dest);
            }
        }

        progress.Report(("Done.", totalSteps, totalSteps));
        return new ApplySuccess(filesWritten);
    }
}

public class ConflictsDetectedEventArgs : EventArgs
{
    public IReadOnlyList<ApplyConflict> Conflicts { get; }
    public OverwriteMode Resolution { get; set; } = OverwriteMode.Ask; // Ask = cancelled

    private readonly TaskCompletionSource _tcs = new();
    public Task ResolutionTask => _tcs.Task;

    public ConflictsDetectedEventArgs(IReadOnlyList<ApplyConflict> conflicts)
        => Conflicts = conflicts;

    public void Complete() => _tcs.TrySetResult();
}
