using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using System.Drawing.Imaging;
using System.Text;

namespace SnapCraft;

internal sealed record RecognizedWord(string Text, Rectangle Bounds);
internal sealed record RecognizedLine(string Text, Rectangle Bounds, IReadOnlyList<RecognizedWord> Words);
internal static class OcrService
{
    internal static string[] Languages() => OcrEngine.AvailableRecognizerLanguages.Select(l => l.LanguageTag).Where(l => l.StartsWith("en", StringComparison.OrdinalIgnoreCase) || l.StartsWith("th", StringComparison.OrdinalIgnoreCase)).Append("th+en").ToArray();
    internal static async Task<IReadOnlyList<RecognizedLine>> ReadAsync(byte[] png, string language)
    {
        if (language == "th+en") return await TesseractOcr.ReadAsync(png);
        var engine = OcrEngine.TryCreateFromLanguage(new Language(language)) ?? throw new InvalidOperationException("This OCR language is not installed. Add its language pack in Windows Settings.");
        using var sourceStream = new MemoryStream(png); using var image = new Bitmap(sourceStream);
        var maximum = (int)OcrEngine.MaxImageDimension;
        if (image.Width > maximum) throw new InvalidOperationException($"Crop a region no wider than {maximum} pixels before reading text.");
        var lines = new List<RecognizedLine>();
        for (var y = 0; y < image.Height; y += maximum - 64)
        {
            using var tile = image.Clone(new Rectangle(0, y, image.Width, Math.Min(maximum, image.Height - y)), PixelFormat.Format32bppArgb);
            using var encoded = new MemoryStream(); tile.Save(encoded, ImageFormat.Png);
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream)) { writer.WriteBytes(encoded.ToArray()); await writer.StoreAsync(); writer.DetachStream(); }
            stream.Seek(0); var decoder = await BitmapDecoder.CreateAsync(stream);
            using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            var result = await engine.RecognizeAsync(bitmap);
            foreach (var line in result.Lines)
            {
                var words = line.Words.Select(word => new RecognizedWord(word.Text, new Rectangle((int)Math.Floor(word.BoundingRect.X), y + (int)Math.Floor(word.BoundingRect.Y), (int)Math.Ceiling(word.BoundingRect.Width), (int)Math.Ceiling(word.BoundingRect.Height)))).ToArray();
                if (words.Length == 0) continue;
                var bounds = words.Select(w => w.Bounds).Aggregate(Rectangle.Union);
                if (lines.Any(old => old.Text == line.Text && Math.Abs(old.Bounds.Y - bounds.Y) < 12)) continue;
                lines.Add(new(line.Text, bounds, words));
            }
            if (y + maximum >= image.Height) break;
        }
        return lines.OrderBy(l => l.Bounds.Y).ThenBy(l => l.Bounds.X).ToArray();
    }
    internal static string TableText(IReadOnlyList<RecognizedLine> lines)
    {
        var rows = new List<List<RecognizedWord>>();
        foreach (var word in lines.SelectMany(l => l.Words).OrderBy(w => w.Bounds.Y).ThenBy(w => w.Bounds.X))
        {
            var row = rows.LastOrDefault(r => Math.Abs(r[0].Bounds.Y - word.Bounds.Y) < Math.Max(5, Math.Min(r[0].Bounds.Height, word.Bounds.Height) / 2));
            if (row is null) rows.Add(new List<RecognizedWord> { word }); else row.Add(word);
        }
        return string.Join(Environment.NewLine, rows.Select(row =>
        {
            var text = new StringBuilder(); RecognizedWord? previous = null;
            foreach (var word in row.OrderBy(w => w.Bounds.X))
            {
                if (previous is not null)
                {
                    if (word.Bounds.Left - previous.Bounds.Right > Math.Max(20, previous.Bounds.Height * 2)) text.Append('\t');
                    else if (TesseractOcr.NeedsSpace(previous, word)) text.Append(' ');
                }
                text.Append(word.Text.Replace('\t', ' ').Replace('\n', ' ')); previous = word;
            }
            return text.ToString();
        }));
    }
}
