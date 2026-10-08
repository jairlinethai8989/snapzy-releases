namespace SnapCraft;

internal static class VideoClipStore
{
    internal static readonly string DirectoryPath = Path.Combine(WebAssets.DataRoot, "Recordings", "Clips");

    internal static string Retain(string source)
    {
        if (!File.Exists(source) || new FileInfo(source).Length == 0) throw new IOException("ไม่พบไฟล์วิดีโอที่บันทึกเสร็จ");
        Directory.CreateDirectory(DirectoryPath);
        var destination = Path.Combine(DirectoryPath, $"neo-snap-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.mp4");
        File.Move(source, destination);
        File.SetLastWriteTimeUtc(destination, DateTime.UtcNow);
        return destination;
    }

    internal static async Task SaveAsync(string source, string destination)
    {
        if (Path.GetFullPath(source).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase)) return;
        var staging = destination + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            await using (var output = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                await input.CopyToAsync(output);
            File.Move(staging, destination, true);
        }
        finally
        {
            try { File.Delete(staging); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    internal static void Cleanup(IEnumerable<string> protectedPaths)
    {
        if (!Directory.Exists(DirectoryPath)) return;
        var protectedSet = new HashSet<string>(protectedPaths.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(DirectoryPath, "*.mp4"))
        {
            try
            {
                if (!protectedSet.Contains(Path.GetFullPath(file)) && File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-7)) File.Delete(file);
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
