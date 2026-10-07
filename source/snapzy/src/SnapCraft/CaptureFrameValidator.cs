namespace SnapCraft;

internal static class CaptureFrameValidator
{
    public static bool IsBlank(Bitmap image)
    {
        var samples = 0;
        var nearBlack = 0;
        var xStep = Math.Max(1, image.Width / 80);
        var yStep = Math.Max(1, image.Height / 80);
        for (var y = 0; y < image.Height; y += yStep)
        {
            for (var x = 0; x < image.Width; x += xStep)
            {
                var pixel = image.GetPixel(x, y);
                samples++;
                if (pixel.R <= 8 && pixel.G <= 8 && pixel.B <= 8) nearBlack++;
            }
        }
        return samples > 0 && nearBlack >= samples * 0.995;
    }

    public static bool IsBlank(string path)
    {
        using var image = new Bitmap(path);
        return IsBlank(image);
    }
}
