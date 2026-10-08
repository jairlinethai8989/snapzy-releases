using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Text.Json;

namespace SnapCraft;

internal sealed class ReportForm : Form
{
    private readonly List<ReportImage> images;
    private readonly TextBox title = new() { Width = 230, Text = "Capture report" };
    private readonly TextBox summary = new() { Multiline = true, Width = 300, Height = 55 };
    private readonly TextBox heading = new() { Width = 220 };
    private readonly TextBox caption = new() { Multiline = true, Width = 300, Height = 55 };
    private readonly ListBox list = new() { Width = 230, Height = 140 };
    private readonly ComboBox template = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly WebView2 preview = new() { Dock = DockStyle.Fill };
    private readonly string directory = Path.Combine(WebAssets.WebRoot, "reports", Guid.NewGuid().ToString("N"));
    private bool writing;
    private bool updating;
    private bool preparing = true;
    private static readonly string[] templateKeys = { "Bug report", "Instructions", "Before / after" };
    public ReportForm(List<ReportImage> images)
    {
        if (images.Count == 0) throw new ArgumentException("Choose at least one image"); this.images = images;
        Font = new Font("Segoe UI", 10); BackColor = Color.White;
        Text = AppInfo.ProductName + " | " + (Localization.CurrentLanguage == "th" ? "สร้างรายงาน" : "Create report"); Size = new Size(1180, 850); MinimumSize = new Size(900, 650); StartPosition = FormStartPosition.CenterParent;
        template.Items.AddRange(Localization.CurrentLanguage == "th" ? new object[] { "รายงานปัญหา", "ขั้นตอนการทำงาน", "ก่อน / หลัง" } : templateKeys.Cast<object>().ToArray()); template.SelectedIndex = 0;
        if (Localization.CurrentLanguage == "th") title.Text = "รายงานภาพหน้าจอ";
        var options = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 330, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(10) };
        void Label(string en, string th) => options.Controls.Add(new Label { Text = Localization.CurrentLanguage == "th" ? th : en, AutoSize = true });
        Label("Template", "แม่แบบ"); options.Controls.Add(template); Label("Title", "หัวข้อ"); options.Controls.Add(title); Label("Summary / expected / actual", "สรุป / ผลที่คาด / ผลที่เกิด"); options.Controls.Add(summary); Label("Images", "ภาพ"); options.Controls.Add(list);
        var reorder = new FlowLayoutPanel { Width = 280, Height = 34 }; var up = new Button { Text = "↑", Width = 50 }; var down = new Button { Text = "↓", Width = 50 }; reorder.Controls.AddRange(new Control[] { up, down }); options.Controls.Add(reorder);
        Label("Image heading", "หัวข้อภาพ"); options.Controls.Add(heading); Label("Caption", "คำอธิบายภาพ"); options.Controls.Add(caption);
        var refresh = new Button { Text = Localization.CurrentLanguage == "th" ? "พรีวิว" : "Preview", Width = 140 };
        var pdf = new Button { Text = "Save PDF", Width = 140 }; var word = new Button { Text = "Save Word", Width = 140 };
        if (Localization.CurrentLanguage == "th") { pdf.Text = "บันทึก PDF"; word.Text = "บันทึก Word"; }
        options.Controls.AddRange(new Control[] { refresh, pdf, word }); Controls.Add(preview); Controls.Add(options);
        list.SelectedIndexChanged += (_, _) => { updating = true; if (list.SelectedIndex >= 0) { heading.Text = this.images[list.SelectedIndex].Title; caption.Text = this.images[list.SelectedIndex].Caption; } updating = false; };
        heading.TextChanged += (_, _) => SaveCaption(); caption.TextChanged += (_, _) => SaveCaption();
        void Move(int delta) { var index = list.SelectedIndex; var target = index + delta; if (index < 0 || target < 0 || target >= images.Count) return; (images[index], images[target]) = (images[target], images[index]); RefreshList(target); }
        up.Click += (_, _) => Move(-1); down.Click += (_, _) => Move(1);
        refresh.Click += async (_, _) =>
        {
            if (writing || preparing) return; preparing = true;
            try { await UpdatePreviewAsync(); } catch (Exception error) { MessageBox.Show(this, error.Message, AppInfo.ProductName); }
            finally { preparing = false; }
        };
        pdf.Click += async (_, _) => await SaveAsync(true); word.Click += async (_, _) => await SaveAsync(false);
        FormClosing += (_, e) => { if (writing || preparing) e.Cancel = true; };
        Shown += async (_, _) => { try { var environment = await BrowserEnvironment.GetAsync(); await preview.EnsureCoreWebView2Async(environment); preview.CoreWebView2.SetVirtualHostNameToFolderMapping("snapcraft.local", WebAssets.WebRoot, CoreWebView2HostResourceAccessKind.DenyCors); await UpdatePreviewAsync(); } catch (Exception error) { MessageBox.Show(this, error.Message); } finally { preparing = false; } };
        RefreshList(0);
    }
    private void SaveCaption() { var index = list.SelectedIndex; if (!updating && index >= 0) images[index] = images[index] with { Title = heading.Text, Caption = caption.Text }; }
    private void RefreshList(int selected) { list.Items.Clear(); foreach (var image in images) list.Items.Add(image.Title); list.SelectedIndex = selected; }
    private async Task UpdatePreviewAsync()
    {
        if (preview.CoreWebView2 is null) return;
        Directory.CreateDirectory(directory); for (var index = 0; index < images.Count; index++) await File.WriteAllBytesAsync(Path.Combine(directory, $"{index}.png"), images[index].Png);
        await File.WriteAllTextAsync(Path.Combine(directory, "report.html"), ReportDocument.Html(title.Text, summary.Text, templateKeys[template.SelectedIndex], images, (string)template.SelectedItem!));
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Navigated(object? sender, CoreWebView2NavigationCompletedEventArgs args) { if (args.IsSuccess) ready.TrySetResult(); else ready.TrySetException(new IOException("Report preview could not load")); }
        preview.CoreWebView2.NavigationCompleted += Navigated;
        try
        {
            preview.Source = new Uri($"https://snapcraft.local/reports/{Path.GetFileName(directory)}/report.html?refresh={Guid.NewGuid():N}"); await ready.Task.WaitAsync(TimeSpan.FromSeconds(12));
            for (var attempt = 0; ; attempt++) { if (await preview.ExecuteScriptAsync("document.fonts.status === 'loaded' && [...document.images].every(i=>i.complete && i.naturalWidth>0)") == "true") break; if (attempt >= 120) throw new IOException("Report images did not finish loading"); await Task.Delay(50); }
        }
        finally { preview.CoreWebView2.NavigationCompleted -= Navigated; }
    }
    private async Task SaveAsync(bool pdf)
    {
        if (writing || preparing || preview.CoreWebView2 is null) return;
        using var dialog = new SaveFileDialog { Filter = pdf ? "PDF report|*.pdf" : "Word document|*.docx", FileName = pdf ? "capture-report.pdf" : "capture-report.docx", AddExtension = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return; writing = true;
        try
        {
            if (!pdf) { var reportTitle = title.Text; var reportSummary = summary.Text; var reportImages = images.ToArray(); await EditorHubForm.WritePngAsync(dialog.FileName, await Task.Run(() => ReportDocument.Word(reportTitle, reportSummary, reportImages))); }
            else
            {
                await UpdatePreviewAsync(); var temporary = Path.Combine(Path.GetDirectoryName(dialog.FileName)!, $".report-{Guid.NewGuid():N}.pdf");
                try { var settings = preview.CoreWebView2.Environment.CreatePrintSettings(); settings.ShouldPrintBackgrounds = true; if (!await preview.CoreWebView2.PrintToPdfAsync(temporary, settings)) throw new IOException("PDF export failed"); File.Move(temporary, dialog.FileName, true); }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            MessageBox.Show(this, Localization.CurrentLanguage == "th" ? "บันทึกรายงานแล้ว" : "Report saved", AppInfo.ProductName);
        }
        catch (Exception error) { MessageBox.Show(this, error.Message, AppInfo.ProductName); }
        finally { writing = false; }
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(directory) && Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Path.Combine(WebAssets.WebRoot, "reports")) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            try { Directory.Delete(directory, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
