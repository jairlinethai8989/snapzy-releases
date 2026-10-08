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
        if (Path.GetExtension(path).Equals(".neosnap", StringComparison.OrdinalIgnoreCase))
        {
            var parameter = Uri.EscapeDataString(WebAssets.CaptureUrl(path));
            view.Source = new Uri($"https://snapcraft.local/editor.html?project={parameter}&version={AppInfo.Version}&language={AppSettings.Load().Language}&product={Uri.EscapeDataString(AppInfo.ProductName)}");
        }
        else await view.ExecuteScriptAsync($"neoSnapEditor.openCapture({JsonSerializer.Serialize(WebAssets.CaptureUrl(path))})");
    }
}
