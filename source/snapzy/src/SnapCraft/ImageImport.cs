using System.Drawing.Imaging;

namespace SnapCraft;

internal static class ImageImport
{
    public static Task<string> CopyAsync(string source) => Task.Run(() =>
    {
        var temporary = WebAssets.NewCapturePath();
        try
        {
            using var stream = File.OpenRead(source);
            using var image = Image.FromStream(stream, false, true);
            if (image.Width > 32767 || image.Height > 32767 || (long)image.Width * image.Height > 80_000_000)
                throw new InvalidDataException("ภาพใหญ่เกินไป กรุณาแบ่งเป็นหลายภาพ");
            image.Save(temporary, ImageFormat.Png);
            return temporary;
        }
        catch (Exception error)
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            throw new InvalidDataException("เปิดภาพไม่ได้ กรุณาเลือกภาพ PNG, JPEG, BMP, GIF หรือ TIFF ที่ถูกต้อง", error);
        }
    });
}
