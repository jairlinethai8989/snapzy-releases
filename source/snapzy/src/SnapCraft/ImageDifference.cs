using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SnapCraft;

internal static class ImageDifference
{
    internal static Bitmap Render(Bitmap before, Bitmap after, int threshold)
    {
        var width = Math.Max(before.Width, after.Width); var height = Math.Max(before.Height, after.Height);
        if ((long)width * height > 64_000_000) throw new InvalidOperationException("Crop the comparison to at most 64 million pixels.");
        using var a = new Bitmap(width, height, PixelFormat.Format32bppArgb); using var b = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(a)) { g.Clear(Color.White); g.DrawImageUnscaled(before, 0, 0); }
        using (var g = Graphics.FromImage(b)) { g.Clear(Color.White); g.DrawImageUnscaled(after, 0, 0); }
        var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var first = a.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var second = b.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var output = result.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            var rowA = new byte[width * 4]; var rowB = new byte[width * 4]; var row = new byte[width * 4];
            for (var y = 0; y < height; y++)
            {
                Marshal.Copy(first.Scan0 + y * first.Stride, rowA, 0, row.Length); Marshal.Copy(second.Scan0 + y * second.Stride, rowB, 0, row.Length);
                for (var x = 0; x < row.Length; x += 4)
                {
                    var different = Math.Max(Math.Abs(rowA[x] - rowB[x]), Math.Max(Math.Abs(rowA[x + 1] - rowB[x + 1]), Math.Abs(rowA[x + 2] - rowB[x + 2]))) > threshold;
                    if (different) { row[x] = 60; row[x + 1] = 40; row[x + 2] = 240; }
                    else { var gray = (byte)((rowB[x] + rowB[x + 1] + rowB[x + 2]) / 6 + 127); row[x] = row[x + 1] = row[x + 2] = gray; }
                    row[x + 3] = 255;
                }
                Marshal.Copy(row, 0, output.Scan0 + y * output.Stride, row.Length);
            }
        }
        finally { a.UnlockBits(first); b.UnlockBits(second); result.UnlockBits(output); }
        return result;
    }
}
