namespace SnapCraft;

internal static class CaptureHistory
{
    internal static string DirectoryPath => Path.Combine(WebAssets.DataRoot, "History");
    internal static bool Enabled => AppSettings.Load().HistoryEnabled;
    private static readonly SemaphoreSlim gate = new(1, 1);
    internal const long MaximumBytes = 512L * 1024 * 1024;
    internal static async Task SaveAsync(string id, byte[] bytes, string extension)
    {
        if (!Enabled || bytes.Length > 180_000_000) return;
        if (!Guid.TryParseExact(id, "N", out _) || extension is not (".png" or ".neosnap")) throw new ArgumentException("Invalid history identity");
        await gate.WaitAsync();
        try
        {
            if (!Enabled) return;
            Directory.CreateDirectory(DirectoryPath);
            var path = Path.Combine(DirectoryPath, id + extension);
            await EditorHubForm.WritePngAsync(path, bytes);
            foreach (var old in Entries().Where(e => Path.GetFileNameWithoutExtension(e.FullName) == id && e.FullName != path)) old.Delete();
            Cleanup(AppSettings.Load().HistoryDays);
        }
        finally { gate.Release(); }
    }
    internal static FileInfo[] Entries() => Directory.Exists(DirectoryPath)
        ? new DirectoryInfo(DirectoryPath).GetFiles().Where(f => (f.Extension is ".png" or ".neosnap") && Guid.TryParseExact(Path.GetFileNameWithoutExtension(f.Name), "N", out _)).OrderByDescending(f => f.LastWriteTimeUtc).ToArray() : Array.Empty<FileInfo>();
    internal static void Cleanup(int days)
    {
        var retained = 0L; var index = 0;
        foreach (var entry in Entries())
        {
            retained += entry.Length; index++;
            if (entry.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-days) || index > 50 || retained > MaximumBytes) entry.Delete();
        }
    }
    internal static async Task ClearAsync()
    {
        await gate.WaitAsync();
        try { foreach (var entry in Entries()) entry.Delete(); }
        finally { gate.Release(); }
    }
    internal static async Task<string> RestoreAsync(string path)
    {
        await gate.WaitAsync();
        try
        {
            var entry = Entries().SingleOrDefault(e => e.FullName == Path.GetFullPath(path)) ?? throw new IOException("History item no longer exists");
            var target = Path.ChangeExtension(WebAssets.NewCapturePath(), entry.Extension);
            await EditorHubForm.WritePngAsync(target, await File.ReadAllBytesAsync(entry.FullName)); return target;
        }
        finally { gate.Release(); }
    }
    internal static async Task DeleteAsync(string path)
    {
        await gate.WaitAsync();
        try { Entries().SingleOrDefault(e => e.FullName == Path.GetFullPath(path))?.Delete(); }
        finally { gate.Release(); }
    }
}
