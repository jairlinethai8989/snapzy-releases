namespace SnapCraft;

internal static class WebAssets
{
    public static readonly string DataRoot = Environment.GetEnvironmentVariable("SNAPCRAFT_DATA_DIR")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductProfile.Current.DataFolder);
    public static readonly string WebRoot = Path.Combine(DataRoot, "Web");
    public static readonly string Captures = Path.Combine(WebRoot, "captures");
    public static readonly string SettingsPath = Path.Combine(DataRoot, "settings.json");

    public static void Prepare()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Assets");
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException("ไม่พบไฟล์หน้าตาโปรแกรม SnapZy");
        Directory.CreateDirectory(WebRoot);
        Directory.CreateDirectory(Captures);
        CopyUpdatedFiles(source, WebRoot);
    }

    internal static int CopyUpdatedFiles(string source, string destinationRoot)
    {
        var copied = 0;
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var destination = Path.Combine(destinationRoot, relative);
            var input = new FileInfo(file);
            var output = new FileInfo(destination);
            if (output.Exists && input.Length == output.Length && input.LastWriteTimeUtc == output.LastWriteTimeUtc) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
            copied++;
        }
        return copied;
    }

    public static void CleanupCaptures()
    {
        foreach (var file in Directory.EnumerateFiles(Captures).Where(path => Path.GetExtension(path) is ".png" or ".neosnap"))
        {
            if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-2))
            {
                try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }

    public static string NewCapturePath() => Path.Combine(Captures, $"{Guid.NewGuid():N}.png");
    public static string CaptureUrl(string path) => $"https://snapcraft.local/captures/{Uri.EscapeDataString(Path.GetFileName(path))}";
}
