namespace SnapCraft;

internal sealed partial class MainForm
{
    private void RefreshHistorySettings()
    {
        var current = AppSettings.Load(); settings.HistoryEnabled = current.HistoryEnabled; settings.HistoryDays = current.HistoryDays;
    }
    private static async Task RetainCaptureHistoryAsync(string path)
    {
        try { await CaptureHistory.SaveAsync(Guid.NewGuid().ToString("N"), await File.ReadAllBytesAsync(path), ".png"); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { PerformanceTrace.Record("history.failed", 0); }
    }
}
