using System.Net.Http;
using System.Security.Cryptography;

namespace SnapCraft;

internal static class OcrModels
{
    internal static string DirectoryPath => Path.Combine(WebAssets.DataRoot, "OcrModels");
    private const string revision = "87416418657359cb625c412a48b6e1d6d41c29bd";
    private static readonly (string Code, int Size, string Hash)[] models =
    {
        ("eng", 4113088, "7D4322BD2A7749724879683FC3912CB542F19906C83BCC1A52132556427170B2"),
        ("tha", 1072600, "294227CC2D1292B0ACB28D61D4115C88252B96D466CA90B417CF4CF0C67BF07C")
    };
    private static readonly SemaphoreSlim gate = new(1, 1);
    internal static async Task<bool> ReadyAsync()
    {
        foreach (var model in models)
        {
            var path = Path.Combine(DirectoryPath, model.Code + ".traineddata");
            if (!File.Exists(path) || new FileInfo(path).Length != model.Size) return false;
            await using var input = File.OpenRead(path);
            if (Convert.ToHexString(await SHA256.HashDataAsync(input)) != model.Hash) return false;
        }
        return true;
    }
    internal static async Task<bool> EnsureAsync(Form owner, string language)
    {
        if (language != "th+en" || await ReadyAsync()) return true;
        var thai = Localization.CurrentLanguage == "th";
        if (MessageBox.Show(owner, thai ? "ดาวน์โหลดชุด OCR ไทยและอังกฤษประมาณ 5 MB จากโครงการ Tesseract? ภาพจะประมวลผลในเครื่องและไม่ถูกส่งออกไป" : "Download approximately 5 MB of Thai/English OCR models from Tesseract? Images stay on this device and are never uploaded.", AppInfo.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return false;
        await gate.WaitAsync();
        try
        {
            if (await ReadyAsync()) return true;
            Directory.CreateDirectory(DirectoryPath);
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            foreach (var model in models)
            {
                using var response = await client.GetAsync($"https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/{revision}/{model.Code}.traineddata", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync();
                using var data = new MemoryStream(); var buffer = new byte[65536]; int read;
                while ((read = await input.ReadAsync(buffer.AsMemory(), timeout.Token)) > 0) { if (data.Length + read > model.Size) throw new IOException("OCR model download exceeded its expected size."); data.Write(buffer, 0, read); }
                var bytes = data.ToArray();
                if (bytes.Length != model.Size || Convert.ToHexString(SHA256.HashData(bytes)) != model.Hash) throw new IOException("OCR model integrity check failed.");
                await EditorHubForm.WritePngAsync(Path.Combine(DirectoryPath, model.Code + ".traineddata"), bytes);
            }
            return true;
        }
        finally { gate.Release(); }
    }
}
