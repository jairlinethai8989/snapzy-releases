using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace SnapCraft;

internal static class WindowSurfaceCapture
{
    private delegate bool EnumChildProc(IntPtr window, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int length);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr window, EnumChildProc callback, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    public static Task<Bitmap?> TryRenderAsync(IntPtr handle, Rectangle bounds, CancellationToken token) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        var name = new StringBuilder(256);
        GetClassName(handle, name, name.Capacity);
        if (!name.ToString().StartsWith("WindowsForms10.", StringComparison.Ordinal)) return null;
        var accelerated = false;
        EnumChildWindows(handle, (child, _) =>
        {
            name.Clear(); GetClassName(child, name, name.Capacity);
            var childClass = name.ToString();
            accelerated = childClass.StartsWith("Chrome_", StringComparison.Ordinal) || childClass.StartsWith("HwndWrapper", StringComparison.Ordinal);
            return !accelerated;
        }, IntPtr.Zero);
        if (accelerated) return null;
        var window = NativeInput.WindowBounds(handle);
        if (window.Width < 1 || window.Height < 1 || (long)window.Width * window.Height > 80_000_000) return null;
        using var image = new Bitmap(window.Width, window.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(image))
        {
            graphics.Clear(Color.Black);
            var dc = graphics.GetHdc();
            try
            {
                // Bound WM_PRINT so a hung target cannot block capture indefinitely.
                if (SendMessageTimeout(handle, 0x0317, dc, new IntPtr(0x1e), 0x22, 350, out _) == IntPtr.Zero) return null;
            }
            finally { graphics.ReleaseHdc(dc); }
        }
        token.ThrowIfCancellationRequested();
        if (NativeInput.WindowBounds(handle) != window || NativeInput.VisibleWindowBounds(handle) != bounds) return null;
        var crop = new Rectangle(bounds.X - window.X, bounds.Y - window.Y, bounds.Width, bounds.Height);
        if (!new Rectangle(Point.Empty, image.Size).Contains(crop) || CaptureFrameValidator.IsBlank(image)) return null;
        return image.Clone(crop, PixelFormat.Format32bppArgb);
    }, token);
}
