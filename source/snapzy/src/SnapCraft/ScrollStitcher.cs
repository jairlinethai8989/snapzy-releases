using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SnapCraft;

internal enum TileResult { Added, Unchanged, Ambiguous, Limit }

internal sealed class ScrollStitcher : IDisposable
{
    private sealed record Tile(Bitmap Image, int Shift);
    private readonly List<Tile> tiles = new();
    private byte[]? previousPixels;
    private int width;
    private int height;
    private int footerHeight;
    private long retainedPixels;
    public int Count => tiles.Count;
    public int TotalHeight { get; private set; }
    internal int FooterHeight => footerHeight;

    public TileResult Add(Bitmap input)
    {
        using var normalized = new Bitmap(input.Width, input.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(normalized)) graphics.DrawImageUnscaled(input, 0, 0);
        var pixels = ReadPixels(normalized);
        if (tiles.Count == 0)
        {
            width = normalized.Width;
            height = normalized.Height;
            TotalHeight = height;
            previousPixels = pixels;
            retainedPixels = (long)width * height;
            tiles.Add(new Tile((Bitmap)normalized.Clone(), 0));
            return TileResult.Added;
        }
        if (normalized.Width != width || normalized.Height != height)
            throw new InvalidOperationException("ขนาดพื้นที่จับภาพเปลี่ยนระหว่างเลื่อน กรุณาเลือกพื้นที่ใหม่");
        var shift = FindShift(previousPixels!, pixels, width, height);
        if (shift == 0) return TileResult.Unchanged;
        if (shift < 0) return TileResult.Ambiguous;
        if (TotalHeight + shift > 30000 || (long)width * (TotalHeight + shift) > 100_000_000)
            return TileResult.Limit;
        var detected = DetectFooter(previousPixels!, pixels, width, height, shift);
        var candidate = Math.Max(footerHeight, detected);
        if (shift >= height - candidate || tiles.Any(tile => tile.Shift >= height - candidate))
            return TileResult.Ambiguous;
        // An initially animating scrollbar may only become detectable later. Retain
        // bounded look-behind rows so its earlier joins can then be cropped losslessly.
        var lookBehind = Math.Min(160, height / 3);
        var stripHeight = Math.Min(height, shift + lookBehind);
        if (retainedPixels + (long)width * stripHeight > 100_000_000) return TileResult.Limit;
        footerHeight = candidate;
        tiles.Add(new Tile(normalized.Clone(new Rectangle(0, height - stripHeight, width, stripHeight), PixelFormat.Format32bppArgb), shift));
        retainedPixels += (long)width * stripHeight;
        TotalHeight += shift;
        previousPixels = pixels;
        return TileResult.Added;
    }

    public void Save(string path)
    {
        if (tiles.Count == 0) throw new InvalidOperationException("ยังไม่มีภาพสำหรับต่อ");
        using var output = new Bitmap(width, TotalHeight, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(output))
        {
            var first = tiles[0].Image;
            using var firstBody = first.Clone(new Rectangle(0, 0, width, height - footerHeight), PixelFormat.Format32bppArgb);
            graphics.DrawImageUnscaled(firstBody, 0, 0);
            var bottom = height - footerHeight;
            foreach (var tile in tiles.Skip(1))
            {
                using var body = tile.Image.Clone(new Rectangle(0, tile.Image.Height - footerHeight - tile.Shift, width, tile.Shift), PixelFormat.Format32bppArgb);
                graphics.DrawImageUnscaled(body, 0, bottom);
                bottom += tile.Shift;
            }
            if (footerHeight > 0)
            {
                var last = tiles[^1].Image;
                using var footer = last.Clone(new Rectangle(0, last.Height - footerHeight, width, footerHeight), PixelFormat.Format32bppArgb);
                graphics.DrawImageUnscaled(footer, 0, bottom);
            }
        }
        output.Save(path, ImageFormat.Png);
    }

    private static byte[] ReadPixels(Bitmap bitmap)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var buffer = new byte[bitmap.Width * bitmap.Height * 4];
            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);
            return buffer;
        }
        finally { bitmap.UnlockBits(data); }
    }

    private static int DetectFooter(byte[] previous, byte[] current, int width, int height, int shift)
    {
        var step = Math.Max(2, width / 160);
        var samples = (width - 5) / step + 1;
        var first = height;
        var stableRun = 0;
        var separatorRun = 0;
        for (var y = height - 1; y >= Math.Max(0, height - Math.Min(160, height / 3)); y--)
        {
            var changed = 0;
            var gray = 0;
            for (var x = 2; x < width - 2; x += step)
            {
                var at = (y * width + x) * 4;
                if (Math.Abs(previous[at] - current[at]) + Math.Abs(previous[at + 1] - current[at + 1]) + Math.Abs(previous[at + 2] - current[at + 2]) > 24) changed++;
                var low = Math.Min(current[at], Math.Min(current[at + 1], current[at + 2]));
                var high = Math.Max(current[at], Math.Max(current[at + 1], current[at + 2]));
                if (low >= 160 && high <= 240 && high - low <= 12) gray++;
            }
            if (changed < 2)
            {
                stableRun++;
                separatorRun = 0;
            }
            else
            {
                // Canvas colors can bleed through a 1-2px scrollbar separator. Require
                // stationary support below, a broad gray rule, and only sparse changes.
                if ((separatorRun == 0 && stableRun < 3) || separatorRun >= 2 ||
                    changed > samples * 0.15 || gray < samples * 0.85) break;
                separatorRun++;
                stableRun = 0;
            }
            first = y;
        }
        if (height - first < 5) return 0;
        for (var y = Math.Max(1, first); y < height - 4; y++)
        {
            var edges = 0;
            var notScrolled = 0;
            for (var x = 2; x < width - 2; x += step)
            {
                var a = (y * width + x) * 4;
                var b = a - width * 4;
                if (Math.Abs(current[a] - current[b]) + Math.Abs(current[a + 1] - current[b + 1]) + Math.Abs(current[a + 2] - current[b + 2]) > 36) edges++;
                if (y >= shift)
                {
                    var moved = a - shift * width * 4;
                    if (Math.Abs(previous[a] - current[moved]) + Math.Abs(previous[a + 1] - current[moved + 1]) + Math.Abs(previous[a + 2] - current[moved + 2]) > 24) notScrolled++;
                }
            }
            // A light scrollbar thumb need not span the whole viewport. Confirm its
            // edge stays fixed rather than following the body's measured displacement.
            if (edges <= samples * 0.35 || notScrolled <= samples * 0.20) continue;
            var boundary = y;
            if (edges <= samples * 0.75)
            {
                // Include flat padding above a partial-width thumb, stopping at the
                // grid's vertical borders or other content instead of its thumb edge.
                boundary--;
                while (boundary > first)
                {
                    var differences = 0;
                    for (var x = 0; x < width && differences < 2; x++)
                    {
                        var a = (boundary * width + x) * 4;
                        var b = a - width * 4;
                        if (Math.Abs(current[a] - current[b]) + Math.Abs(current[a + 1] - current[b + 1]) + Math.Abs(current[a + 2] - current[b + 2]) > 36) differences++;
                    }
                    if (differences >= 2) break;
                    boundary--;
                }
            }
            return height - boundary;
        }
        return 0;
    }

    private static int FindShift(byte[] oldPixels, byte[] newPixels, int width, int height)
    {
        // Stationary rows (frozen titles, toolbars) must not vote for a scroll offset.
        var moving = new bool[height];
        var changedRows = 0;
        for (var y = 0; y < height; y++)
        {
            var changed = 0;
            for (var x = 2; x < width - 2; x += Math.Max(2, width / 160))
            {
                var at = (y * width + x) * 4;
                if (Math.Abs(oldPixels[at] - newPixels[at]) +
                    Math.Abs(oldPixels[at + 1] - newPixels[at + 1]) +
                    Math.Abs(oldPixels[at + 2] - newPixels[at + 2]) > 24) changed++;
            }
            moving[y] = changed >= 2;
            if (moving[y]) changedRows++;
        }
        if (changedRows < 3) return 0;
        var movingEnd = height;
        while (movingEnd > 0 && !moving[movingEnd - 1]) movingEnd--;
        var same = Score(oldPixels, newPixels, width, height, 0, moving, movingEnd);
        if (same > 0.985) return 0;
        var maximum = Math.Max(1, Math.Min((int)(height * 0.86), height - Math.Max(120, height / 5)));
        var bestShift = -1;
        var bestScore = 0d;
        var scores = new double[maximum + 1];
        for (var shift = 1; shift <= maximum; shift++)
        {
            var score = Score(oldPixels, newPixels, width, height, shift, moving, movingEnd);
            scores[shift] = score;
            if (score > bestScore) { bestScore = score; bestShift = shift; }
        }
        if (bestScore < 0.88) return -1;
        var competingScore = 0d;
        for (var shift = 1; shift <= maximum; shift++)
        {
            if (Math.Abs(shift - bestShift) > Math.Max(5, height / 50))
                competingScore = Math.Max(competingScore, scores[shift]);
        }
        return bestScore - competingScore >= 0.001 ? bestShift : -1;
    }

    private static double Score(byte[] oldPixels, byte[] newPixels, int width, int height, int shift, bool[] moving, int movingEnd)
    {
        var overlap = height - shift;
        if (overlap < height * 0.12) return 0;
        var xStep = Math.Max(2, width / 160);
        var yStep = Math.Max(2, overlap / 140);
        double error = 0;
        double weightTotal = 0;
        var sampledRows = 0;
        for (var y = 2; y < overlap - 4; y += yStep)
        {
            // A stationary bottom band in the old frame must not vote for a large
            // displacement by matching its empty pixels to blank body space.
            if (!moving[y] || y + shift >= movingEnd) continue;
            sampledRows++;
            var oldRow = (y + shift) * width * 4;
            var newRow = y * width * 4;
            for (var x = 2; x < width - 2; x += xStep)
            {
                var a = oldRow + x * 4;
                var b = newRow + x * 4;
                var dark = oldPixels[a] < 235 || oldPixels[a + 1] < 235 || oldPixels[a + 2] < 235 ||
                           newPixels[b] < 235 || newPixels[b + 1] < 235 || newPixels[b + 2] < 235;
                var weight = dark ? 4 : 1;
                var difference = (Math.Abs(oldPixels[a] - newPixels[b]) +
                                  Math.Abs(oldPixels[a + 1] - newPixels[b + 1]) +
                                  Math.Abs(oldPixels[a + 2] - newPixels[b + 2])) / 3.0;
                error += weight * difference;
                weightTotal += weight;
            }
        }
        return sampledRows < 6 || weightTotal < 100 ? 0 : 1 - error / (255 * weightTotal);
    }

    public void Dispose()
    {
        foreach (var tile in tiles) tile.Image.Dispose();
        tiles.Clear();
    }
}
