using System.Text.Json;
using System.Text;
using Microsoft.Web.WebView2.WinForms;

namespace SnapCraft;

internal sealed partial class EditorHubForm
{
    private readonly System.Windows.Forms.Timer recoveryTimer = new() { Interval = 10000 };
    private readonly Dictionary<WebView2, string> historyIds = new();
    private readonly Dictionary<WebView2, string> lastRecoveryState = new();
    private bool recoveryBusy;
    internal IEnumerable<string> SourcePaths => tabs.TabPages.Cast<TabPage>().Select(p => p.Tag as string).OfType<string>().ToArray();
    private void InitializeRecovery()
    {
        recoveryTimer.Tick += async (_, _) => await SaveRecoveryAsync(); recoveryTimer.Start();
    }
    private async Task SaveRecoveryAsync()
    {
        if (recoveryBusy || closeBusy || projectCommandBusy || IsDisposed) return;
        if (!CaptureHistory.Enabled) { lastRecoveryState.Clear(); return; }
        recoveryBusy = true;
        try
        {
            foreach (var page in tabs.TabPages.Cast<TabPage>().ToArray())
            {
                if (page.Tag is not string || saves.ContainsKey(page)) continue;
                var view = page.Controls.OfType<WebView2>().Single();
                if (view.IsDisposed || view.CoreWebView2 is null || projectOperations.ContainsKey(view)) continue;
                var state = await view.ExecuteScriptAsync("window.neoSnapEditor && baseImage ? imageSnapshot() : null");
                if (state == "null" || lastRecoveryState.GetValueOrDefault(view) == state) continue;
                var exported = await ExportProjectAsync(view);
                var bytes = Encoding.UTF8.GetBytes(exported.GetProperty("project").GetRawText());
                if (!historyIds.TryGetValue(view, out var id)) historyIds[view] = id = Guid.NewGuid().ToString("N");
                await CaptureHistory.SaveAsync(id, bytes, ".neosnap"); lastRecoveryState[view] = state;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException or ObjectDisposedException) { PerformanceTrace.Record("recovery.failed", 0); }
        finally { recoveryBusy = false; }
    }
}
