using Microsoft.Web.WebView2.Core;

namespace SnapCraft;

internal static class BrowserEnvironment
{
    // WebView2 environments belong to their UI apartment; editor tabs share the launcher one.
    [ThreadStatic] private static Task<CoreWebView2Environment>? environment;

    public static Task<CoreWebView2Environment> GetAsync()
    {
        if (environment is null || environment.IsFaulted || environment.IsCanceled)
            environment = CreateAsync();
        return environment;
    }

    private static async Task<CoreWebView2Environment> CreateAsync()
    {
        using var timing = PerformanceTrace.Measure("browser.environment");
        return await CoreWebView2Environment.CreateAsync(userDataFolder: Path.Combine(WebAssets.DataRoot, "WebView2"));
    }
}
