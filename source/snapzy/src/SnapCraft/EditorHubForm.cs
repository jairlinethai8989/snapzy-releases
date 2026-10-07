using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Text.Json;

namespace SnapCraft;

internal sealed partial class EditorHubForm : Form
{
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill, DrawMode = TabDrawMode.OwnerDrawFixed, SizeMode = TabSizeMode.Fixed, ItemSize = new Size(152, 34), Padding = new Point(12, 5), Font = new Font("Segoe UI", 10) };
    private readonly Image closeIcon = Image.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "icons", "close.png"));
    private readonly Action showLauncher;
    private readonly Func<IEnumerable<EditorHubForm>>? getEditors;
    private readonly Action<string>? changeLanguage;
    private int hoveredClose = -1;
    private int captureNumber;
    private bool closeBusy;
    private bool closeApproved;
    private readonly TaskCompletionSource<bool> closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Dictionary<TabPage, TaskCompletionSource<bool>> saves = new();
    private readonly Dictionary<WebView2, Task> initializations = new();

    public EditorHubForm(Action showLauncher, Func<IEnumerable<EditorHubForm>>? getEditors = null, Action<string>? changeLanguage = null)
    {
        this.showLauncher = showLauncher;
        this.getEditors = getEditors;
        this.changeLanguage = changeLanguage;
        Text = $"{AppInfo.ProductName} {AppInfo.Version} | {Localization.Translate("แก้ไขภาพ")}";
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "icons", ProductProfile.Current.IconFile));
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(780, 540);
        Size = new Size(1300, 850);
        BackColor = Color.White;
        tabs.DrawItem += DrawTab;
        tabs.MouseMove += (_, e) => { var hovered = Enumerable.Range(0, tabs.TabCount).FirstOrDefault(index => CloseBounds(index).Contains(e.Location), -1); if (hovered != hoveredClose) { hoveredClose = hovered; tabs.Invalidate(); } };
        tabs.MouseLeave += (_, _) => { hoveredClose = -1; tabs.Invalidate(); };
        tabs.MouseDown += async (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            for (var index = 0; index < tabs.TabCount; index++) if (CloseBounds(index).Contains(e.Location)) { await ClosePageAsync(tabs.TabPages[index]); break; }
        };
        Controls.Add(tabs);
        Shown += OnFirstShown;
        FormClosing += async (_, e) =>
        {
            if (closeApproved) return;
            e.Cancel = true;
            await RequestUserCloseAsync();
        };
    }

    public void AddCapture(string path)
    {
        var page = new TabPage($"ภาพ {++captureNumber}") { Tag = path, BackColor = Color.White };
        var web = new WebView2 { Dock = DockStyle.Fill };
        page.Controls.Add(web);
        tabs.TabPages.Add(page);
        tabs.SelectedTab = page;
        if (Visible) StartEditorInitialization(web, path);
    }

    public void RefreshLanguage() => Text = $"{AppInfo.ProductName} {AppInfo.Version} | {Localization.Translate("แก้ไขภาพ")}";

    private void OnFirstShown(object? sender, EventArgs e)
    {
        Shown -= OnFirstShown;
        foreach (TabPage page in tabs.TabPages)
        {
            if (page.Controls[0] is WebView2 web && web.CoreWebView2 is null)
                StartEditorInitialization(web, (string)page.Tag!);
        }
    }

    private void StartEditorInitialization(WebView2 web, string path)
    {
        if (!initializations.ContainsKey(web)) initializations.Add(web, InitializeEditorAsync(web, path));
    }

    private async Task InitializeEditorAsync(WebView2 web, string path)
    {
        var readyClock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var env = await BrowserEnvironment.GetAsync();
            if (web.IsDisposed || IsDisposed) return;
            using (PerformanceTrace.Measure("editor.controller")) await web.EnsureCoreWebView2Async(env);
            if (web.IsDisposed || IsDisposed) return;
            web.CoreWebView2.SetVirtualHostNameToFolderMapping("snapcraft.local", WebAssets.WebRoot, CoreWebView2HostResourceAccessKind.DenyCors);
            web.CoreWebView2.WebMessageReceived += async (_, args) =>
            {
                using var message = JsonDocument.Parse(args.WebMessageAsJson);
                var action = message.RootElement.ValueKind == JsonValueKind.String
                    ? message.RootElement.GetString()
                    : message.RootElement.GetProperty("action").GetString();
                var page = tabs.TabPages.Cast<TabPage>().FirstOrDefault(page => page.Controls.Contains(web));
                if (action == "editorReady") PerformanceTrace.Record("editor.ready", readyClock.Elapsed.TotalMilliseconds);
                if (action == "language") changeLanguage?.Invoke(message.RootElement.GetProperty("language").GetString() ?? ProductProfile.Current.DefaultLanguage);
                if (action == "showLauncher") showLauncher();
                if (action == "closeEditorTab" && page is not null) await ClosePageAsync(page);
                if (action == "saveImage" && page is not null) await SavePageAsync(page);
                if (action == "saveProject" && page is not null) await SaveProjectAsync(page);
                if (action == "openProject") await OpenProjectAsync();
                if (action == "combineTabs" || action == "appendTabs") await CombineTabsAsync(web, action == "appendTabs");
                if (action == "projectOperationStarted" && !projectOperations.ContainsKey(web))
                    projectOperations.Add(web, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
                if (action == "projectOperationFinished" && projectOperations.TryGetValue(web, out var operation))
                {
                    operation.TrySetResult(message.RootElement.GetProperty("success").GetBoolean());
                    projectOperations.Remove(web);
                }
            };
            web.CoreWebView2.DownloadStarting += (_, args) =>
            {
                using var dialog = new SaveFileDialog
                {
                    Filter = "PNG image (*.png)|*.png",
                    FileName = $"snapzy-{DateTime.Now:yyyyMMdd-HHmmss}.png",
                    AddExtension = true
                };
                if (dialog.ShowDialog(this) == DialogResult.OK) args.ResultFilePath = dialog.FileName;
                else args.Cancel = true;
                args.Handled = true;
            };
            var parameter = Path.GetExtension(path).Equals(".neosnap", StringComparison.OrdinalIgnoreCase) ? "project" : "image";
            web.Source = new Uri($"https://snapcraft.local/editor.html?{parameter}={Uri.EscapeDataString(WebAssets.CaptureUrl(path))}&version={AppInfo.Version}&language={AppSettings.Load().Language}&product={Uri.EscapeDataString(AppInfo.ProductName)}");
        }
        catch (Exception error)
        {
            if (web.IsDisposed || IsDisposed) return;
            MessageBox.Show(this, Localization.Translate($"เปิดหน้าแก้ไขภาพไม่ได้: {error.Message}"), AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private Rectangle CloseBounds(int index)
    {
        var rectangle = tabs.GetTabRect(index);
        return new Rectangle(rectangle.Right - 30, rectangle.Top + (rectangle.Height - 24) / 2, 24, 24);
    }

    private void DrawTab(object? sender, DrawItemEventArgs e)
    {
        var selected = e.Index == tabs.SelectedIndex;
        using var fill = new SolidBrush(selected ? Color.FromArgb(237, 244, 255) : Color.FromArgb(247, 249, 252));
        e.Graphics.FillRectangle(fill, e.Bounds);
        if (selected) { using var pen = new Pen(Color.FromArgb(21, 94, 239), 2); e.Graphics.DrawLine(pen, e.Bounds.Left + 4, e.Bounds.Bottom - 2, e.Bounds.Right - 4, e.Bounds.Bottom - 2); }
        var label = new Rectangle(e.Bounds.Left + 12, e.Bounds.Top, e.Bounds.Width - 45, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, label, Color.FromArgb(40, 54, 73), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        var close = CloseBounds(e.Index);
        if (hoveredClose == e.Index) { using var hover = new SolidBrush(Color.FromArgb(219, 232, 253)); e.Graphics.FillRectangle(hover, close); }
        e.Graphics.DrawImage(closeIcon, new Rectangle(close.Left + 4, close.Top + 4, 16, 16));
    }

    private async Task RemovePageAsync(TabPage page)
    {
        var web = page.Controls.OfType<WebView2>().Single();
        if (initializations.TryGetValue(web, out var initialization)) await initialization;
        initializations.Remove(web);
        var path = page.Tag as string;
        tabs.TabPages.Remove(page);
        page.Dispose();
        if (path is not null) { try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    private async Task ClosePageAsync(TabPage page)
    {
        if (closeBusy || projectCommandBusy || !tabs.TabPages.Contains(page)) return;
        closeBusy = true;
        try
        {
            if (!await CanClosePageAsync(page)) return;
            await RemovePageAsync(page);
            if (tabs.TabCount == 0)
            {
                closeApproved = true;
                BeginInvoke(new Action(Close));
                await closed.Task;
            }
        }
        catch (Exception error) { ShowCloseFailure(error); }
        finally { closeBusy = false; }
    }

    internal async Task<bool> RequestCloseAsync()
    {
        if (IsDisposed) return true;
        if (closeBusy || projectCommandBusy) return false;
        closeBusy = true;
        try
        {
            while (tabs.TabCount > 0)
            {
                var page = tabs.TabPages[0];
                if (!await CanClosePageAsync(page)) return false;
                await RemovePageAsync(page);
            }
            closeApproved = true;
            // Complete the original closing/modal event before asking WinForms to close again.
            BeginInvoke(new Action(Close));
            return await closed.Task;
        }
        catch (Exception error) { ShowCloseFailure(error); return false; }
        finally { closeBusy = false; }
    }

    private void ShowCloseFailure(Exception error) => MessageBox.Show(this,
        Localization.Translate($"ยังปิดภาพไม่ได้ ภาพยังเปิดอยู่\n{error.Message}"), AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private async Task<bool> CanClosePageAsync(TabPage page)
    {
        var web = page.Controls.OfType<WebView2>().Single();
        if (!await HasUnkeptChangesAsync(page)) return true;
        tabs.SelectedTab = page;
        using var dialog = new EditorCloseDialog(page.Text);
        var decision = dialog.ShowDialog(this);
        if (decision == DialogResult.No) return true;
        if (decision != DialogResult.Yes || !await SavePageAsync(page, true)) return false;
        // A newer edit must never be discarded by a late export completion.
        return await web.ExecuteScriptAsync("neoSnapEditor.hasUnkeptChanges()") == "false";
    }

    private async Task<bool> HasUnkeptChangesAsync(TabPage page)
    {
        if (saves.TryGetValue(page, out var pending)) await pending.Task;
        var web = page.Controls.OfType<WebView2>().Single();
        if (projectOperations.TryGetValue(web, out var operation)) await operation.Task;
        try
        {
            if (web.CoreWebView2 is not null &&
                await web.ExecuteScriptAsync("window.neoSnapEditor ? neoSnapEditor.hasUnkeptChanges() : true") == "false") return false;
        }
        catch (Exception error) when (error is InvalidOperationException or System.Runtime.InteropServices.COMException) { }
        return true;
    }

    private async Task<bool> SavePageAsync(TabPage page, bool allowProject = false)
    {
        if (saves.TryGetValue(page, out var pending)) return await pending.Task;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        saves.Add(page, completion);
        var web = page.Controls.OfType<WebView2>().Single();
        try
        {
            if (web.CoreWebView2 is null) throw new InvalidOperationException("ภาพยังไม่พร้อมบันทึก กรุณารอให้ภาพเปิดเสร็จ");
            if (projectOperations.TryGetValue(web, out var operation)) await operation.Task;
            var project = allowProject && (Path.GetExtension(page.Tag as string) == ".neosnap" || await web.ExecuteScriptAsync("objects.some(o => o.tool === 'image')") == "true");
            using var dialog = new SaveFileDialog { Title = Localization.Translate(project ? "SnapZy | บันทึกงาน" : "SnapZy | PNG"), Filter = project ? $"{AppInfo.ProductName} project (*.neosnap)|*.neosnap|PNG image (*.png)|*.png" : "PNG image (*.png)|*.png", FileName = $"{(ProductProfile.Current.ApplicationFolder == "Snapzy" ? "snapzy" : "neo-snap")}-{DateTime.Now:yyyyMMdd-HHmmss}.{(project ? "neosnap" : "png")}", AddExtension = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) { completion.SetResult(false); return false; }
            web.Enabled = false;
            if (project && dialog.FilterIndex == 1)
            {
                var workspace = await ExportProjectAsync(web);
                var projectBytes = System.Text.Encoding.UTF8.GetBytes(workspace.GetProperty("project").GetRawText());
                if (projectBytes.Length > ProjectSizeLimit) throw new IOException("ไฟล์งานใหญ่เกินไป");
                await WritePngAsync(dialog.FileName, projectBytes);
                await web.ExecuteScriptAsync($"neoSnapEditor.markKept({JsonSerializer.Serialize(workspace.GetProperty("snapshot").GetString())}); showToast('บันทึกงานแล้ว')");
                completion.SetResult(true); return true;
            }
            using var exported = JsonDocument.Parse(await web.ExecuteScriptAsync("neoSnapEditor.exportImage()"));
            var snapshot = exported.RootElement.GetProperty("snapshot").GetString()!;
            var bytes = Convert.FromBase64String(exported.RootElement.GetProperty("base64").GetString()!);
            await WritePngAsync(dialog.FileName, bytes);
            await web.ExecuteScriptAsync($"neoSnapEditor.markKept({JsonSerializer.Serialize(snapshot)}); showToast('บันทึก PNG แล้ว')");
            completion.SetResult(true);
            return true;
        }
        catch (Exception error)
        {
            completion.TrySetResult(false);
            MessageBox.Show(this, Localization.Translate($"บันทึกภาพไม่สำเร็จ ภาพยังเปิดอยู่\n{error.Message}"), AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        finally { if (!web.IsDisposed) web.Enabled = true; saves.Remove(page); }
    }

    internal static async Task WritePngAsync(string path, byte[] bytes)
    {
        var temporary = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, $".snapzy-{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes);
            File.Move(temporary, path, true);
        }
        finally
        {
            try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        foreach (TabPage page in tabs.TabPages)
        {
            if (page.Tag is string path) { try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
        base.OnFormClosed(e);
        closed.TrySetResult(true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) closeIcon.Dispose();
        base.Dispose(disposing);
    }
}
