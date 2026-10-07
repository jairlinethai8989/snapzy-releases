using ScreenRecorderLib;

namespace SnapCraft;

internal sealed class CaptureBackend
{
    private RecorderApi preferredDisplayApi = RecorderApi.WindowsGraphicsCapture;

    public async Task<string> CaptureWindowAsync(IntPtr handle, CancellationToken cancellationToken = default, Rectangle? desktopRegion = null)
    {
        if (handle == IntPtr.Zero) throw new ArgumentException("ไม่ได้เลือกหน้าต่าง");
        cancellationToken.ThrowIfCancellationRequested();
        // Shell surfaces are not GPU application windows. Capture the selected monitor as displayed.
        if (NativeInput.IsDesktopWindow(handle))
        {
            using var timing = PerformanceTrace.Measure("capture.desktop");
            using var desktop = await CaptureRegionAsync(desktopRegion ?? Screen.FromHandle(handle).Bounds, cancellationToken);
            var path = WebAssets.NewCapturePath();
            try { await Task.Run(() => desktop.Save(path, System.Drawing.Imaging.ImageFormat.Png), cancellationToken); }
            catch { File.Delete(path); throw; }
            return path;
        }
        if (!NativeInput.IsCaptureTargetVisible(handle)) throw new InvalidOperationException("หน้าต่างเป้าหมายถูกย่อหรือไม่มีพื้นที่ที่มองเห็น");
        try
        {
            using var timing = PerformanceTrace.Measure("capture.window");
            var bounds = NativeInput.VisibleWindowBounds(handle);
            Bitmap? captured = null;
            if (SystemInformation.VirtualScreen.Contains(bounds) && NativeInput.IsWindowUnobstructed(handle, bounds))
            {
                captured = await CaptureRegionAsync(bounds, cancellationToken);
                if (!NativeInput.IsWindowUnobstructed(handle, bounds) || NativeInput.VisibleWindowBounds(handle) != bounds)
                { captured.Dispose(); captured = null; }
            }
            captured ??= await WindowSurfaceCapture.TryRenderAsync(handle, bounds, cancellationToken);
            using var image = captured ?? throw new InvalidOperationException("The target window cannot be copied safely from the desktop.");
            var path = WebAssets.NewCapturePath();
            try { await Task.Run(() => image.Save(path, System.Drawing.Imaging.ImageFormat.Png), cancellationToken); }
            catch { File.Delete(path); throw; }
            return path;
        }
        catch (Exception firstError) when (firstError is not OperationCanceledException)
        {
            try { return await CaptureVerifiedAsync(() => new WindowRecordingSource(handle) { IsCursorCaptureEnabled = false }, cancellationToken); }
            catch (Exception fallbackError) when (fallbackError is not OperationCanceledException)
            { throw new InvalidOperationException($"จับหน้าต่างไม่ได้: {firstError.Message}; {fallbackError.Message}", fallbackError); }
        }
    }

    public async Task<Bitmap> CaptureRegionAsync(Rectangle region, CancellationToken cancellationToken = default)
    {
        if (region.Width < 20 || region.Height < 20 || !SystemInformation.VirtualScreen.Contains(region))
            throw new InvalidOperationException("กรอบจับภาพต้องอยู่ภายในพื้นที่หน้าจอที่มองเห็น");
        try
        {
            return await Task.Run(() =>
            {
                using var timing = PerformanceTrace.Measure("capture.screen-copy");
                var image = new Bitmap(region.Width, region.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    using var graphics = Graphics.FromImage(image);
                    graphics.CopyFromScreen(region.Location, Point.Empty, region.Size);
                    // A successful screen copy can legitimately contain an entirely black region.
                    // Blank-frame retries are reserved for GPU recorder startup below.
                    return image;
                }
                catch { image.Dispose(); throw; }
            }, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            PerformanceTrace.Record("capture.gpu-fallback", 0);
            var screen = Screen.FromRectangle(region);
            if (!screen.Bounds.Contains(region)) throw;
            var raw = await CaptureDisplayAsync(screen.DeviceName, cancellationToken);
            try
            {
                using var display = new Bitmap(raw);
                // Only the GPU fallback needs coordinate mapping; normal capture copies exact screen pixels.
                var crop = MapDisplayRegion(region, screen.Bounds, display.Size);
                return display.Clone(crop, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            }
            finally { File.Delete(raw); }
        }
    }

    internal static Rectangle MapDisplayRegion(Rectangle region, Rectangle screen, Size display)
    {
        var left = (int)Math.Round((double)(region.Left - screen.Left) * display.Width / screen.Width);
        var top = (int)Math.Round((double)(region.Top - screen.Top) * display.Height / screen.Height);
        var right = (int)Math.Round((double)(region.Right - screen.Left) * display.Width / screen.Width);
        var bottom = (int)Math.Round((double)(region.Bottom - screen.Top) * display.Height / screen.Height);
        return Rectangle.Intersect(Rectangle.FromLTRB(left, top, right, bottom), new Rectangle(Point.Empty, display));
    }

    public async Task<string> CaptureDisplayAsync(string deviceName, CancellationToken cancellationToken = default)
    {
        var firstApi = preferredDisplayApi;
        var secondApi = firstApi == RecorderApi.WindowsGraphicsCapture
            ? RecorderApi.DesktopDuplication : RecorderApi.WindowsGraphicsCapture;
        try
        {
            return await CaptureVerifiedAsync(() => new DisplayRecordingSource(deviceName) { IsCursorCaptureEnabled = false, RecorderApi = firstApi }, cancellationToken);
        }
        catch (Exception firstError) when (firstError is not OperationCanceledException)
        {
            try
            {
                var path = await CaptureVerifiedAsync(() => new DisplayRecordingSource(deviceName) { IsCursorCaptureEnabled = false, RecorderApi = secondApi }, cancellationToken);
                preferredDisplayApi = secondApi;
                return path;
            }
            catch (Exception secondError) when (secondError is not OperationCanceledException)
            {
                throw new InvalidOperationException($"จับภาพหน้าจอไม่ได้ ({firstApi}: {firstError.Message}; {secondApi}: {secondError.Message})", secondError);
            }
        }
    }

    private static async Task<string> CaptureVerifiedAsync(Func<RecordingSourceBase> source, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (attempt > 0) await Task.Delay(180 * attempt, cancellationToken);
            var path = await TakeScreenshotAsync(source(), cancellationToken);
            bool blank;
            try { blank = CaptureFrameValidator.IsBlank(path); }
            catch
            {
                File.Delete(path);
                throw;
            }
            if (!blank) return path;
            File.Delete(path);
        }
        throw new InvalidOperationException("ภาพที่จับได้เป็นสีดำทั้งเฟรม กรุณาตรวจหน้าต่างเป้าหมายหรือไดรเวอร์จอภาพ");
    }

    private static async Task<string> TakeScreenshotAsync(RecordingSourceBase source, CancellationToken cancellationToken)
    {
        var path = WebAssets.NewCapturePath();
        var options = new RecorderOptions
        {
            SourceOptions = new SourceOptions { RecordingSources = new List<RecordingSourceBase> { source } },
            OutputOptions = new OutputOptions { RecorderMode = RecorderMode.Screenshot },
            SnapshotOptions = new SnapshotOptions { SnapshotFormat = ImageFormat.PNG },
            AudioOptions = new AudioOptions { IsAudioEnabled = false },
            MouseOptions = new MouseOptions { IsMousePointerEnabled = false }
        };
        using var recorder = Recorder.CreateRecorder(options);
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        recorder.OnRecordingComplete += (_, args) => completion.TrySetResult(args.FilePath);
        recorder.OnRecordingFailed += (_, args) => completion.TrySetException(new InvalidOperationException(args.Error));
        using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        try
        {
            recorder.Record(path);
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);
        }
        catch
        {
            try { File.Delete(path); } catch (IOException) { }
            throw;
        }
    }
}
