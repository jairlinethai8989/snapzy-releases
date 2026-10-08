using Tesseract;

namespace SnapCraft;

internal static class TesseractOcr
{
    internal static async Task<IReadOnlyList<RecognizedLine>> ReadAsync(byte[] png)
    {
        if (!await OcrModels.ReadyAsync()) throw new InvalidOperationException("Download the Thai/English OCR models first.");
        return await Task.Run<IReadOnlyList<RecognizedLine>>(() =>
        {
            using var stream = new MemoryStream(png); using var bitmap = new Bitmap(stream);
            if ((long)bitmap.Width * bitmap.Height > 64_000_000) throw new InvalidOperationException("Select a smaller OCR region (maximum 64 million pixels).");
            using var engine = new TesseractEngine(OcrModels.DirectoryPath, "tha+eng", EngineMode.LstmOnly);
            using var image = Pix.LoadFromMemory(png); using var page = engine.Process(image);
            using var iterator = page.GetIterator(); iterator.Begin();
            var lines = new List<RecognizedLine>(); var words = new List<RecognizedWord>();
            void FinishLine()
            {
                if (words.Count == 0) return;
                lines.Add(new(JoinWords(words), words.Select(w => w.Bounds).Aggregate(Rectangle.Union), words.ToArray())); words.Clear();
            }
            do
            {
                if (iterator.IsAtBeginningOf(PageIteratorLevel.TextLine)) FinishLine();
                var text = iterator.GetText(PageIteratorLevel.Word)?.Trim();
                if (!string.IsNullOrEmpty(text) && iterator.TryGetBoundingBox(PageIteratorLevel.Word, out var bounds)) words.Add(new(text, new Rectangle(bounds.X1, bounds.Y1, bounds.Width, bounds.Height)));
            } while (iterator.Next(PageIteratorLevel.Word));
            FinishLine(); return lines;
        });
    }

    internal static string JoinWords(IReadOnlyList<RecognizedWord> words)
    {
        if (words.Count == 0) return string.Empty;
        var result = new System.Text.StringBuilder(words.Sum(word => word.Text.Length + 1));
        result.Append(words[0].Text);
        for (var index = 1; index < words.Count; index++)
        {
            var previous = words[index - 1]; var current = words[index];
            if (NeedsSpace(previous, current)) result.Append(' ');
            result.Append(current.Text);
        }
        return result.ToString();
    }

    internal static bool NeedsSpace(RecognizedWord previous, RecognizedWord current)
    {
        if (!ContainsThai(previous.Text) || !ContainsThai(current.Text)) return true;
        var gap = current.Bounds.Left - previous.Bounds.Right;
        var glyphHeight = Math.Min(previous.Bounds.Height, current.Bounds.Height);
        return gap > Math.Max(3, (int)Math.Round(glyphHeight * 0.55, MidpointRounding.AwayFromZero));
    }

    private static bool ContainsThai(string value) => value.Any(character => character is >= '\u0E00' and <= '\u0E7F');
}
