using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SnapCraft;

internal static class SheetViewportDetector
{
    // Canvas sheets may expose only a Document in UIA. Require both persistent column
    // borders and a broad colored header before trimming application chrome.
    public static Rectangle? Find(Bitmap input)
    {
        if (input.Width < 300 || input.Height < 250) return null;
        using var image = new Bitmap(input.Width, input.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(image)) graphics.DrawImageUnscaled(input, 0, 0);
        var data = image.LockBits(new Rectangle(Point.Empty, image.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var pixels = new byte[image.Width * image.Height * 4];
        try { Marshal.Copy(data.Scan0, pixels, 0, pixels.Length); }
        finally { image.UnlockBits(data); }
        var width = image.Width;
        var height = image.Height;
        bool ColumnAt(int x, int y, bool neutral = true)
        {
            var at = (y * width + x) * 4;
            var low = Math.Min(pixels[at], Math.Min(pixels[at + 1], pixels[at + 2]));
            var high = Math.Max(pixels[at], Math.Max(pixels[at + 1], pixels[at + 2]));
            if (high > 240 || neutral && high - low > 24) return false;
            var left = at - 3 * 4;
            var right = at + 3 * 4;
            return pixels[left] + pixels[left + 1] + pixels[left + 2] > pixels[at] + pixels[at + 1] + pixels[at + 2] + 30
                || pixels[right] + pixels[right + 1] + pixels[right + 2] > pixels[at] + pixels[at + 1] + pixels[at + 2] + 30;
        }
        var columns = new List<int>();
        var yStart = height * 2 / 5;
        var yEnd = height * 17 / 20;
        var samples = (yEnd - yStart + 2) / 3;
        for (var x = 4; x < width - 4; x++)
        {
            var votes = 0;
            for (var y = yStart; y < yEnd; y += 3) if (ColumnAt(x, y)) votes++;
            if (votes < samples * 0.60) continue;
            if (columns.Count == 0 || x - columns[^1] > 5) columns.Add(x);
        }
        if (columns.Count < 4 || columns[^1] - columns[0] < width * 0.50) return null;
        var top = -1;
        for (var y = 0; y < height * 7 / 10 - 18; y++)
        {
            var present = 0;
            foreach (var x in columns)
            {
                var votes = 0;
                for (var offset = 1; offset <= 16; offset += 3) if (ColumnAt(x, y + offset, false)) votes++;
                if (votes >= 4) present++;
            }
            if (present >= Math.Max(4, (int)Math.Ceiling(columns.Count * 0.60))) { top = y; break; }
        }
        if (top < 0) return null;
        var coloredRun = 0;
        var headerFound = false;
        var headerTop = top;
        for (var y = Math.Max(0, top - 250); y < Math.Min(height, top + 250); y++)
        {
            var colored = 0;
            var count = 0;
            for (var x = 4; x < width - 4; x += 7)
            {
                var at = (y * width + x) * 4;
                var high = Math.Max(pixels[at], Math.Max(pixels[at + 1], pixels[at + 2]));
                var low = Math.Min(pixels[at], Math.Min(pixels[at + 1], pixels[at + 2]));
                if (high - low > 45 && high > 60 && high < 245) colored++;
                count++;
            }
            coloredRun = colored > count * 0.45 ? coloredRun + 1 : 0;
            if (coloredRun >= 12) { headerFound = true; headerTop = y - coloredRun + 1; break; }
        }
        if (!headerFound) return null;
        // The support probe starts a few pixels before the first real vertical border.
        while (top < height - 20 && columns.Count(x => ColumnAt(x, top, false)) < Math.Max(3, columns.Count / 2)) top++;
        top = Math.Min(top, headerTop);
        if (top < 1 || top >= height * 7 / 10) return null;
        return new Rectangle(0, top, width, height - top);
    }
}
