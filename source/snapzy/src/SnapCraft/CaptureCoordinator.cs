using System.Drawing.Imaging;

namespace SnapCraft;

internal enum CaptureKind { Area, Window, Scroll }

internal sealed class CaptureCoordinator
{
    private readonly CaptureBackend backend = new();

    public async Task<string?> CaptureAsync(CaptureKind kind, int delayMs)
    {
        using var totalTiming = PerformanceTrace.Measure("capture.total-including-selection");
        var selection = await SelectionOverlay.ChooseAsync(kind switch { CaptureKind.Window => SelectionMode.Window, CaptureKind.Scroll => SelectionMode.Scroll, _ => SelectionMode.Region });
        if (selection is null) return null;
        if (kind == CaptureKind.Scroll && selection.WindowHandle == IntPtr.Zero)
            throw new InvalidOperationException("ไม่พบหน้าต่างใต้กรอบที่เลือก");
        if (delayMs > 0)
        {
            using var delayTiming = PerformanceTrace.Measure("capture.configured-delay");
            await Task.Delay(Math.Min(delayMs, 10000));
        }
        await Task.Delay(120);
        using var captureTiming = PerformanceTrace.Measure("capture.after-selection");
        return kind switch
        {
            CaptureKind.Area => await CaptureAreaAsync(selection.Region),
            CaptureKind.Window => await backend.CaptureWindowAsync(selection.WindowHandle, desktopRegion: selection.Region),
            CaptureKind.Scroll => await CaptureScrollAsync(selection),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private async Task<string> CaptureAreaAsync(Rectangle region)
    {
        using var image = await CaptureVisibleRegionAsync(region);
        var path = WebAssets.NewCapturePath();
        using var timing = PerformanceTrace.Measure("capture.png");
        await Task.Run(() => image.Save(path, ImageFormat.Png));
        return path;
    }

    internal async Task<string?> CaptureScrollAsync(CaptureSelection selection, Action<Bitmap, int, TileResult, int>? observed = null)
    {
        using var stitcher = new ScrollStitcher();
        using var progress = new ScrollProgressForm(selection.Region);
        var finish = false;
        using var escape = new EscapeHook(() => finish = true);
        progress.Show();
        NativeInput.FocusWindow(selection.WindowHandle);
        progress.UpdateProgress(0, 0);
        var point = new Point(selection.Region.Left + selection.Region.Width / 2, selection.Region.Top + selection.Region.Height * 4 / 5);
        var unchanged = 0;
        var ambiguous = 0;
        var useAutomation = false;
        var started = DateTime.UtcNow;
        var lastAdded = started;
        try
        {
            var attempt = 0;
            while (stitcher.Count < 80)
            {
                if (progress.CancelRequested) return null;
                if (finish || progress.FinishRequested) break;
                if (attempt > 0)
                {
                    if (!progress.Manual)
                    {
                        NativeInput.FocusWindow(selection.WindowHandle);
                        var moved = false;
                        if (useAutomation)
                        {
                            try { moved = await Task.Run(() => NativeInput.TryAutomationScroll(point)).WaitAsync(TimeSpan.FromMilliseconds(600)); }
                            catch (TimeoutException)
                            {
                                progress.SetManual(true);
                                progress.UpdateProgress(stitcher.Count, stitcher.TotalHeight, "ระบบเลื่อนไม่ตอบสนอง: เลื่อนเองหรือ Esc เพื่อจบ");
                            }
                        }
                        if (!progress.Manual && !moved) NativeInput.WheelDown(point, 3);
                    }
                    await Task.Delay(progress.Manual ? 550 : 350);
                }
                using var tile = await CaptureVisibleRegionAsync(selection.Region);
                var result = await Task.Run(() => stitcher.Add(tile));
                observed?.Invoke(tile, stitcher.Count, result, stitcher.TotalHeight);
                if (result == TileResult.Added)
                {
                    lastAdded = DateTime.UtcNow;
                    unchanged = 0;
                    ambiguous = 0;
                    useAutomation = false;
                    progress.UpdateProgress(stitcher.Count, stitcher.TotalHeight);
                }
                else if (result == TileResult.Unchanged)
                {
                    unchanged++;
                    if (unchanged >= 2) useAutomation = true;
                    if (unchanged >= 4 && !progress.Manual)
                    {
                        if (stitcher.Count > 1) break;
                        progress.UpdateProgress(stitcher.Count, stitcher.TotalHeight, "ยังไม่เลื่อน: ลองเลื่อนเอง แล้วกด Esc เพื่อจบ");
                        progress.SetManual(true);
                    }
                }
                else if (result == TileResult.Ambiguous)
                {
                    ambiguous++;
                    if (ambiguous >= 2 && !progress.Manual)
                    {
                        progress.UpdateProgress(stitcher.Count, stitcher.TotalHeight, "เลื่อนเอง แล้วกด เสร็จ หรือ Esc");
                        progress.SetManual(true);
                    }
                }
                else break;
                if (!progress.Manual && DateTime.UtcNow - started > TimeSpan.FromMinutes(2)) break;
                if (progress.Manual && DateTime.UtcNow - started > TimeSpan.FromMinutes(7)) break;
                if (progress.Manual && DateTime.UtcNow - lastAdded > TimeSpan.FromSeconds(30)) break;
                attempt++;
            }
            if (progress.CancelRequested || stitcher.Count == 0) return null;
            var output = WebAssets.NewCapturePath();
            await Task.Run(() => stitcher.Save(output));
            return output;
        }
        finally { progress.Close(); }
    }

    private Task<Bitmap> CaptureVisibleRegionAsync(Rectangle region) => backend.CaptureRegionAsync(region);
}
