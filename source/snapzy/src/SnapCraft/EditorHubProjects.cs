using Microsoft.Web.WebView2.WinForms;
using System.Text;
using System.Text.Json;

namespace SnapCraft;

internal sealed partial class EditorHubForm
{
    private const long ProjectSizeLimit = 180_000_000;
    private readonly Dictionary<WebView2, TaskCompletionSource<bool>> projectOperations = new();
    private bool projectCommandBusy;

    private async Task<JsonElement> ExportProjectAsync(WebView2 web)
    {
        if (web.IsDisposed || web.CoreWebView2 is null) throw new InvalidOperationException("กรุณารอให้ภาพเปิดเสร็จ");
        using var exported = JsonDocument.Parse(await web.ExecuteScriptAsync("neoSnapEditor.exportProject()"));
        if (exported.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("ภาพยังไม่พร้อมรวม กรุณารอให้ภาพเปิดเสร็จ");
        return exported.RootElement.Clone();
    }

    private async Task SaveProjectAsync(TabPage page)
    {
        if (saves.ContainsKey(page) || closeBusy) return;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        saves.Add(page, completion);
        var web = page.Controls.OfType<WebView2>().Single();
        try
        {
            using var dialog = new SaveFileDialog { Title = "SnapZy | บันทึกงาน", Filter = "SnapZy project (*.neosnap)|*.neosnap", FileName = $"snapzy-{DateTime.Now:yyyyMMdd-HHmmss}.neosnap", AddExtension = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            web.Enabled = false;
            if (projectOperations.TryGetValue(web, out var operation)) await operation.Task;
            var exported = await ExportProjectAsync(web);
            var json = exported.GetProperty("project").GetRawText();
            var bytes = Encoding.UTF8.GetBytes(json);
            if (bytes.Length > ProjectSizeLimit) throw new IOException("ไฟล์งานใหญ่เกินไป กรุณาแบ่งเป็นหลายงาน");
            await WritePngAsync(dialog.FileName, bytes);
            var snapshot = exported.GetProperty("snapshot").GetString();
            await web.ExecuteScriptAsync($"neoSnapEditor.markKept({JsonSerializer.Serialize(snapshot)}); showToast('บันทึกงานแล้ว')");
            completion.TrySetResult(true);
        }
        catch (Exception error) { ProjectError(error); }
        finally { completion.TrySetResult(false); if (!web.IsDisposed) web.Enabled = true; saves.Remove(page); }
    }

    private async Task OpenProjectAsync()
    {
        if (projectCommandBusy || closeBusy) return;
        projectCommandBusy = true;
        string? temporary = null;
        try
        {
            temporary = await ChooseProjectAsync(this);
            if (temporary is null) return;
            AddCapture(temporary);
            tabs.SelectedTab!.Text = "งาน SnapZy";
            temporary = null;
        }
        catch (Exception error) { ProjectError(error); }
        finally { projectCommandBusy = false; DeleteProjectTemporary(temporary); }
    }

    private async Task CombineTabsAsync(WebView2 target, bool append)
    {
        if (projectCommandBusy || closeBusy) return;
        projectCommandBusy = true;
        string? temporary = null;
        try
        {
            var hubs = (getEditors?.Invoke() ?? new[] { this }).Where(h => !h.IsDisposed).ToArray();
            var sources = hubs.SelectMany((hub, index) => hub.tabs.TabPages.Cast<TabPage>().Select(page => (hub, page, label: hubs.Length > 1 ? $"{index + 1}: {page.Text}" : page.Text)))
                .Where(source => !append || !source.page.Controls.Contains(target)).ToArray();
            var pages = sources.Select(source => source.page).ToArray();
            if (pages.Length == 0) throw new InvalidOperationException("เปิดภาพอีกแท็บก่อนเพิ่มภาพจากแท็บ");
            using var dialog = new CombineImagesDialog(sources.Select(source => source.label).ToArray(), append);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            var projects = new List<JsonElement>();
            foreach (var index in dialog.SelectedIndices)
            {
                var web = pages[index].Controls.OfType<WebView2>().Single();
                if (sources[index].hub.saves.TryGetValue(pages[index], out var saving)) await saving.Task;
                if (sources[index].hub.projectOperations.TryGetValue(web, out var importing)) await importing.Task;
                projects.Add((await ExportProjectAsync(web)).GetProperty("project").Clone());
            }
            if (append)
            {
                var operation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                projectOperations.Add(target, operation);
                target.Enabled = false;
                try
                {
                    await target.ExecuteScriptAsync($"neoSnapEditor.appendTabs({JsonSerializer.Serialize(projects)}, {JsonSerializer.Serialize(dialog.Arrangement)}, {dialog.Gap})");
                    await operation.Task;
                }
                finally { projectOperations.Remove(target); if (!target.IsDisposed) target.Enabled = true; }
            }
            else
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(new { format = "snapzy-combine", layout = dialog.Arrangement, gap = dialog.Gap, projects });
                if (bytes.Length > ProjectSizeLimit) throw new IOException("งานรวมภาพใหญ่เกินไป");
                temporary = Path.ChangeExtension(WebAssets.NewCapturePath(), ".neosnap");
                await WritePngAsync(temporary, bytes);
                AddCapture(temporary); tabs.SelectedTab!.Text = "งานรวมภาพ";
                temporary = null;
            }
        }
        catch (Exception error) { ProjectError(error); }
        finally { projectCommandBusy = false; DeleteProjectTemporary(temporary); }
    }

    private void ProjectError(Exception error) => MessageBox.Show(this, $"ยังทำรายการไม่ได้ งานเดิมยังอยู่\n{error.Message}", "SnapZy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    internal static async Task<string?> ChooseProjectAsync(IWin32Window owner)
    {
        using var dialog = new OpenFileDialog { Title = "SnapZy | เปิดงาน", Filter = "SnapZy project (*.neosnap)|*.neosnap", CheckFileExists = true };
        if (dialog.ShowDialog(owner) != DialogResult.OK) return null;
        if (new FileInfo(dialog.FileName).Length > ProjectSizeLimit) throw new IOException("ไฟล์งานใหญ่เกินไป");
        var bytes = await File.ReadAllBytesAsync(dialog.FileName);
        using var json = JsonDocument.Parse(bytes);
        if (!json.RootElement.TryGetProperty("format", out var format) || format.GetString() != "neo-snap") throw new IOException("ไฟล์นี้ไม่ใช่งาน SnapZy");
        var temporary = Path.ChangeExtension(WebAssets.NewCapturePath(), ".neosnap");
        await WritePngAsync(temporary, bytes);
        return temporary;
    }
    private static void DeleteProjectTemporary(string? path)
    {
        if (path is null) return;
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
