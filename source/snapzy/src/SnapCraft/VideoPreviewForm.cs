using System.Collections.Specialized;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace SnapCraft;

internal sealed class VideoPreviewForm : Form
{
    private readonly WebView2 web = new() { Dock = DockStyle.Fill };
    private readonly TaskCompletionSource<bool> closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task initialization = Task.CompletedTask;
    private bool kept;
    private bool busy;
    private bool closeBusy;
    private bool closeApproved;
    private bool discard;
    internal string ClipPath { get; }

    public VideoPreviewForm(string path)
    {
        ClipPath = Path.GetFullPath(path);
        Text = $"{AppInfo.ProductName} {AppInfo.Version} | {Localization.Translate("พรีวิววิดีโอ")}";
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "icons", ProductProfile.Current.IconFile));
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(720, 480);
        MinimumSize = new Size(496, 399);
        BackColor = Color.White;
        Controls.Add(web);
        Shown += (_, _) => initialization = InitializeAsync();
        FormClosing += async (_, e) => { if (!closeApproved) { e.Cancel = true; await RequestCloseAsync(); } };
    }

    private async Task InitializeAsync()
    {
        try
        {
            var environment = await BrowserEnvironment.GetAsync();
            if (IsDisposed) return;
            await web.EnsureCoreWebView2Async(environment);
            if (IsDisposed) return;
            web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            web.CoreWebView2.SetVirtualHostNameToFolderMapping("snapcraft.local", WebAssets.WebRoot, CoreWebView2HostResourceAccessKind.DenyCors);
            web.CoreWebView2.SetVirtualHostNameToFolderMapping("clips.snapcraft.local", VideoClipStore.DirectoryPath, CoreWebView2HostResourceAccessKind.DenyCors);
            web.CoreWebView2.NavigationStarting += (_, args) =>
            {
                var uri = new Uri(args.Uri);
                if (uri.Host != "snapcraft.local" || uri.AbsolutePath != "/video-preview.html") args.Cancel = true;
            };
            web.CoreWebView2.WebMessageReceived += (_, args) =>
            {
                var source = new Uri(args.Source);
                if (source.Scheme != "https" || source.Host != "snapcraft.local" || source.AbsolutePath != "/video-preview.html") return;
                using var message = JsonDocument.Parse(args.WebMessageAsJson);
                var action = message.RootElement.GetProperty("action").GetString();
                // Native modal dialogs and drag loops must run after the WebView2 callback returns.
                BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        if (IsDisposed || busy || closeBusy) return;
                        if (action == "copyClip") CopyClip();
                        else if (action == "saveClip") await SaveClipAsync();
                        else if (action == "dragClip") DragClip();
                    }
                    catch (Exception error) { Report(action ?? "copyClip", false, error.Message); }
                }));
            };
            var clipUrl = $"https://clips.snapcraft.local/{Uri.EscapeDataString(Path.GetFileName(ClipPath))}";
            web.Source = new Uri($"https://snapcraft.local/video-preview.html?clip={Uri.EscapeDataString(clipUrl)}&product={Uri.EscapeDataString(AppInfo.ProductName)}&language={Localization.CurrentLanguage}");
        }
        catch (Exception error)
        {
            if (IsDisposed) return;
            web.Hide();
            var fallback = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), FlowDirection = FlowDirection.TopDown, WrapContents = false };
            fallback.Controls.Add(new Label { Text = Localization.Translate($"เปิดพรีวิวไม่ได้: {error.Message}\nคลิปยังอยู่ที่: {ClipPath}"), AutoSize = true, MaximumSize = new Size(650, 0) });
            var copy = new Button { Text = Localization.Translate("คัดลอกคลิป"), AutoSize = true }; copy.Click += (_, _) => CopyClip();
            var save = new Button { Text = Localization.Translate("บันทึก MP4"), AutoSize = true }; save.Click += async (_, _) => await SaveClipAsync();
            fallback.Controls.Add(copy); fallback.Controls.Add(save); Controls.Add(fallback); fallback.BringToFront();
        }
    }

    private StringCollection FileList()
    {
        if (!File.Exists(ClipPath)) throw new FileNotFoundException("ไม่พบคลิปที่บันทึกไว้", ClipPath);
        return new StringCollection { ClipPath };
    }

    private void CopyClip()
    {
        try
        {
            Clipboard.SetFileDropList(FileList());
            kept = true;
            File.SetLastWriteTimeUtc(ClipPath, DateTime.UtcNow);
            Report("copyClip", true, "คัดลอกคลิปแล้ว");
        }
        catch (Exception error) { Report("copyClip", false, $"คัดลอกไม่สำเร็จ: {error.Message}"); }
    }

    private void DragClip()
    {
        busy = true;
        try
        {
            var data = new DataObject(); data.SetFileDropList(FileList());
            if (DoDragDrop(data, DragDropEffects.Copy) == DragDropEffects.Copy)
            {
                kept = true; File.SetLastWriteTimeUtc(ClipPath, DateTime.UtcNow);
                Report("dragClip", true, "ส่งต่อคลิปแล้ว");
            }
        }
        catch (Exception error) { Report("dragClip", false, $"ลากคลิปไม่สำเร็จ: {error.Message}"); }
        finally { busy = false; }
    }

    private async Task<bool> SaveClipAsync()
    {
        if (busy) return false;
        busy = true;
        try
        {
            using var dialog = new SaveFileDialog { Title = $"{AppInfo.ProductName} | MP4", Filter = "MP4 video (*.mp4)|*.mp4", FileName = $"{(ProductProfile.Current.ApplicationFolder == "Snapzy" ? "snapzy" : "neo-snap")}-{DateTime.Now:yyyyMMdd-HHmmss}.mp4", AddExtension = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) { Report("saveClip", false, "ยกเลิกการบันทึก"); return false; }
            await VideoClipStore.SaveAsync(ClipPath, dialog.FileName);
            kept = true; Report("saveClip", true, "บันทึก MP4 แล้ว"); return true;
        }
        catch (Exception error) { Report("saveClip", false, $"บันทึกไม่สำเร็จ: {error.Message}"); return false; }
        finally { busy = false; }
    }

    private void Report(string action, bool success, string text)
    {
        text = Localization.Translate(text);
        if (web.CoreWebView2 is not null && !web.IsDisposed)
            web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "result", action, success, text }));
        else if (!success) MessageBox.Show(this, text, AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    public void RefreshLanguage()
    {
        Text = $"{AppInfo.ProductName} {AppInfo.Version} | {Localization.Translate("พรีวิววิดีโอ")}";
        if (web.CoreWebView2 is not null && !web.IsDisposed)
            web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "language", language = Localization.CurrentLanguage }));
    }

    public async Task<bool> RequestCloseAsync()
    {
        if (IsDisposed) return true;
        if (closeBusy || busy) return false;
        closeBusy = true;
        try
        {
            if (!kept)
            {
                using var prompt = new EditorCloseDialog("วิดีโอ", video: true);
                var choice = prompt.ShowDialog(this);
                if (choice != DialogResult.Yes && choice != DialogResult.No) return false;
                if (choice == DialogResult.Yes && !await SaveClipAsync()) return false;
                discard = choice == DialogResult.No;
            }
            await initialization;
            closeApproved = true;
            BeginInvoke(new Action(Close));
            return await closed.Task;
        }
        finally { closeBusy = false; }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        web.Dispose();
        if (discard)
        {
            try { File.Delete(ClipPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        base.OnFormClosed(e);
        closed.TrySetResult(true);
    }
}
