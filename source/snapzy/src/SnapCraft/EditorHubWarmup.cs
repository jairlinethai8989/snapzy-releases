using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;

namespace SnapCraft;

internal sealed partial class EditorHubForm
{
    private Task? warmup;
    private TabPage? warmPage;
    private TaskCompletionSource? warmReady;
    internal bool IsWarm => warmPage is not null && warmup is { IsCompletedSuccessfully: true };
    internal Task PrepareWarmAsync() => warmup ??= PrepareCoreAsync();

    private async Task PrepareCoreAsync()
    {
        using var timing = PerformanceTrace.Measure("editor.warmup");
        _ = Handle;
        warmReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        warmPage = new TabPage { BackColor = Color.White };
        var view = new WebView2 { Dock = DockStyle.Fill };
        warmPage.Controls.Add(view); tabs.TabPages.Add(warmPage);
        _ = view.Handle;
        StartEditorInitialization(view, "");
        await initializations[view];
        await warmReady.Task.WaitAsync(TimeSpan.FromSeconds(12));
    }

    private async Task LoadWarmCaptureAsync(WebView2 view, string path)
    {
        using var timing = PerformanceTrace.Measure("editor.prepared-to-image");
        try { await warmup!; }
        catch (Exception error) when (error is InvalidOperationException or ObjectDisposedException or System.Runtime.InteropServices.COMException or TimeoutException)
        {
            if (view.IsDisposed || IsDisposed) return;
            PerformanceTrace.Record("editor.warmup-fallback", 0);
            if (view.CoreWebView2 is null) { await InitializeEditorAsync(view, path); return; }
            var key = Path.GetExtension(path).Equals(".neosnap", StringComparison.OrdinalIgnoreCase) ? "project" : "image";
            view.Source = new Uri($"https://snapcraft.local/editor.html?{key}={Uri.EscapeDataString(WebAssets.CaptureUrl(path))}&version={AppInfo.Version}&language={AppSettings.Load().Language}&product={Uri.EscapeDataString(AppInfo.ProductName)}");
            return;
        }
        if (view.IsDisposed || IsDisposed) return;
        if (Path.GetExtension(path).Equals(".neosnap", StringComparison.OrdinalIgnoreCase))
        {
            var parameter = Uri.EscapeDataString(WebAssets.CaptureUrl(path));
            view.Source = new Uri($"https://snapcraft.local/editor.html?project={parameter}&version={AppInfo.Version}&language={AppSettings.Load().Language}&product={Uri.EscapeDataString(AppInfo.ProductName)}");
        }
        else await view.ExecuteScriptAsync($"neoSnapEditor.openCapture({JsonSerializer.Serialize(WebAssets.CaptureUrl(path))})");
    }
}
