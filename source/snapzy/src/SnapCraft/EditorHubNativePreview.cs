namespace SnapCraft;

internal sealed partial class EditorHubForm
{
    private readonly HashSet<Microsoft.Web.WebView2.WinForms.WebView2> renderedViews = new();
    private async Task ShowNativePreviewAsync(TabPage page, string path)
    {
        if (!Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase)) return;
        using var timing = PerformanceTrace.Measure("editor.native-preview");
        Bitmap image;
        try { image = await Task.Run(() => { using var source = new Bitmap(path); return new Bitmap(source); }); }
        catch (Exception error) when (error is IOException or ArgumentException or OutOfMemoryException) { return; }
        if (page.IsDisposed || IsDisposed) { image.Dispose(); return; }
        var web = page.Controls.OfType<Microsoft.Web.WebView2.WinForms.WebView2>().Single();
        if (renderedViews.Contains(web)) { image.Dispose(); return; }
        var preview = new PictureBox { Name = "nativePreview", Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Image = image, BackColor = Color.FromArgb(239, 243, 247), Enabled = false };
        preview.Disposed += (_, _) => image.Dispose();
        page.Controls.Add(preview); preview.BringToFront();
    }
    private static void RemoveNativePreview(TabPage? page)
    {
        if (page is null) return;
        foreach (var preview in page.Controls.OfType<PictureBox>().Where(p => p.Name == "nativePreview").ToArray()) { page.Controls.Remove(preview); preview.Dispose(); }
    }
}
