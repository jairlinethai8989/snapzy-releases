using Microsoft.Web.WebView2.WinForms;
using System.Text.Json;

namespace SnapCraft;

internal sealed partial class EditorHubForm
{
    private static string Ui(string en, string th) => Localization.CurrentLanguage == "th" ? th : en;
    private void ShowProductivityMenu(WebView2 view)
    {
        var menu = new ContextMenuStrip();
        foreach (var entry in new[] { ("scrollReview", "Review scroll joins", "ตรวจรอยต่อภาพยาว"), ("report", "Create report", "สร้างรายงาน"), ("redact", "Safe sharing", "ปิดข้อมูลก่อนแชร์"), ("history", "History and recovery", "ประวัติและกู้คืนงาน"), ("compare", "Compare before / after", "เปรียบเทียบก่อน / หลัง") })
        {
            var action = entry.Item1;
            menu.Items.Add(Ui(entry.Item2, entry.Item3), null, async (_, _) => await RunProductivityAsync(action, view));
        }
        menu.Closed += (_, _) => BeginInvoke(new Action(menu.Dispose)); menu.Show(Cursor.Position);
    }
    private async Task<byte[]> RenderedImageAsync(WebView2 view, bool selection = false)
    {
        using var image = JsonDocument.Parse(await view.ExecuteScriptAsync(selection ? "neoSnapEditor.exportRegion()" : "neoSnapEditor.exportImage()"));
        if (image.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidOperationException(Ui("Image is still loading.", "ภาพยังโหลดไม่เสร็จ"));
        return Convert.FromBase64String(image.RootElement.GetProperty("base64").GetString()!);
    }
    private async Task RunProductivityAsync(string action, WebView2 view)
    {
        if (closeBusy || projectCommandBusy) return; projectCommandBusy = true;
        try
        {
            if (action == "history") { using var history = new HistoryForm(); if (history.ShowDialog(this) == DialogResult.OK && history.SelectedPath is not null) AddCapture(await CaptureHistory.RestoreAsync(history.SelectedPath)); return; }
            if (action == "ocr") { using var ocr = new OcrReviewForm(await RenderedImageAsync(view, true)); ocr.ShowDialog(this); return; }
            if (action == "redact")
            {
                using var redact = new RedactionForm(await RenderedImageAsync(view));
                if (redact.ShowDialog(this) == DialogResult.OK && redact.Result is not null) { var path = WebAssets.NewCapturePath(); await WritePngAsync(path, redact.Result); AddCapture(path); } return;
            }
            if (action == "scrollReview")
            {
                var source = tabs.TabPages.Cast<TabPage>().Single(p => p.Controls.Contains(view)).Tag as string;
                var audit = source is null ? null : ScrollCaptureAudit.Load(source);
                if (audit is null) { MessageBox.Show(this, Ui("This image has no scrolling capture trace.", "ภาพนี้ไม่มีข้อมูลการต่อภาพยาว"), AppInfo.ProductName); return; }
                using var review = new ScrollReviewForm(await File.ReadAllBytesAsync(source!), audit); review.ShowDialog(this); return;
            }
            var pages = (getEditors?.Invoke() ?? new[] { this }).Where(h => !h.IsDisposed).SelectMany(h => h.tabs.TabPages.Cast<TabPage>().Select(p => (hub: h, page: p))).ToArray();
            using var choose = new CombineImagesDialog(pages.Select((p, i) => $"{i + 1}: {p.page.Text}").ToArray(), true, selectionOnly: true);
            if (choose.ShowDialog(this) != DialogResult.OK) return;
            var selectedImages = new List<ReportImage>();
            foreach (var index in choose.SelectedIndices)
            {
                var source = pages[index]; var sourceView = source.page.Controls.OfType<WebView2>().Single();
                if (source.hub.IsDisposed || source.page.IsDisposed) throw new InvalidOperationException(Ui("An image was closed. Select images again.", "มีภาพถูกปิด กรุณาเลือกภาพอีกครั้ง"));
                if (source.hub.saves.TryGetValue(source.page, out var save)) await save.Task;
                if (source.hub.projectOperations.TryGetValue(sourceView, out var pending)) await pending.Task;
                selectedImages.Add(new ReportImage(source.page.Text, "", await RenderedImageAsync(sourceView)));
            }
            if (action == "compare")
            {
                if (selectedImages.Count != 2) throw new InvalidOperationException(Ui("Select exactly two images.", "เลือกภาพสองภาพเพื่อเปรียบเทียบ"));
                using var compare = new CompareForm(selectedImages[0].Png, selectedImages[1].Png); compare.ShowDialog(this);
            }
            else { using var report = new ReportForm(selectedImages); report.ShowDialog(this); }
        }
        catch (Exception error) { MessageBox.Show(this, error.Message, AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { projectCommandBusy = false; }
    }
}
