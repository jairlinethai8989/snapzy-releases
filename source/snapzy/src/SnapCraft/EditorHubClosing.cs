using Microsoft.Web.WebView2.WinForms;

namespace SnapCraft;

internal sealed partial class EditorHubForm
{
    private bool closeScopeBusy;

    private async Task<bool> RequestUserCloseAsync()
    {
        if (IsDisposed) return true;
        if (closeBusy || closeScopeBusy || projectCommandBusy) return false;
        closeScopeBusy = true;
        try
        {
            // Return from FormClosing before entering another native modal loop.
            await Task.Yield();
            if (IsDisposed) return true;
            var editors = (getEditors?.Invoke() ?? new[] { this }).Append(this).Where(editor => !editor.IsDisposed).Distinct().ToArray();
            if (editors.Any(editor => editor != this && (editor.closeScopeBusy || editor.closeBusy))) return false;
            var imageCount = editors.Sum(editor => editor.tabs.TabCount);
            if (imageCount <= 1) return await RequestCloseAsync();
            using var scope = new EditorCloseScopeDialog(imageCount);
            var choice = scope.ShowDialog(this);
            if (choice == DialogResult.Yes) return await RequestCloseAsync();
            if (choice != DialogResult.No) return false;
            return await CloseEditorsAsync(editors);
        }
        finally { closeScopeBusy = false; }
    }

    private async Task<bool> CloseEditorsAsync(EditorHubForm[] editors)
    {
        if (editors.Any(editor => editor.closeBusy || editor.projectCommandBusy))
        {
            MessageBox.Show(this, Localization.Translate("บางภาพกำลังทำงาน กรุณารอแล้วลองปิดอีกครั้ง"), AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
        // Only close this snapshot. Captures added while awaiting a save must remain open.
        var pages = editors.SelectMany(editor => editor.tabs.TabPages.Cast<TabPage>().Select(page => (editor, page))).ToArray();
        var views = pages.Select(entry => entry.page.Controls.OfType<WebView2>().Single()).Select(web => (web, enabled: web.Enabled)).ToArray();
        foreach (var editor in editors) editor.closeBusy = true;
        foreach (var view in views) view.web.Enabled = false;
        try
        {
            var unkept = new List<(EditorHubForm editor, TabPage page)>();
            foreach (var (editor, page) in pages)
            {
                if (await editor.HasUnkeptChangesAsync(page)) unkept.Add((editor, page));
                page.Controls.OfType<WebView2>().Single().Enabled = false;
            }
            if (unkept.Count > 0)
            {
                using var dialog = new EditorCloseDialog("", imageCount: unkept.Count);
                var decision = dialog.ShowDialog(this);
                if (decision != DialogResult.Yes && decision != DialogResult.No) return false;
                if (decision == DialogResult.Yes)
                {
                    foreach (var (editor, page) in unkept)
                    {
                        editor.tabs.SelectedTab = page;
                        if (!await editor.SavePageAsync(page, true)) return false;
                        page.Controls.OfType<WebView2>().Single().Enabled = false;
                    }
                    foreach (var (editor, page) in pages)
                        if (await editor.HasUnkeptChangesAsync(page)) return false;
                }
            }
            // Finish every confirmation/save before removing any image.
            foreach (var (editor, page) in pages) await editor.RemovePageAsync(page);
            var closing = new List<Task<bool>>();
            foreach (var editor in editors.Where(editor => editor.tabs.TabCount == 0))
            {
                editor.closeApproved = true;
                editor.BeginInvoke(new Action(editor.Close));
                closing.Add(editor.closed.Task);
            }
            await Task.WhenAll(closing);
            return true;
        }
        catch (Exception error) { ShowCloseFailure(error); return false; }
        finally
        {
            foreach (var view in views) if (!view.web.IsDisposed) view.web.Enabled = view.enabled;
            foreach (var editor in editors) editor.closeBusy = false;
        }
    }
}
