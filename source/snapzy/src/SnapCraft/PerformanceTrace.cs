using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

namespace SnapCraft;

internal static class PerformanceTrace
{
    private static readonly ConcurrentQueue<string> pending = new();
    private static int writing;

    public static IDisposable Measure(string stage) => new Measurement(stage);

    public static void Record(string stage, double milliseconds)
    {
        pending.Enqueue($"{DateTime.UtcNow:O} pid={Environment.ProcessId} version={AppInfo.Version} {stage} {milliseconds.ToString("F1", CultureInfo.InvariantCulture)}ms");
        while (pending.Count > 256) pending.TryDequeue(out _);
        if (Interlocked.CompareExchange(ref writing, 1, 0) == 0) _ = Task.Run(FlushAsync);
    }

    private static async Task FlushAsync()
    {
        try
        {
            var directory = Path.Combine(WebAssets.DataRoot, "Logs");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "performance.log");
            if (File.Exists(path) && new FileInfo(path).Length > 512 * 1024)
                File.Move(path, path + ".previous", true);
            var lines = new List<string>();
            while (pending.TryDequeue(out var line)) lines.Add(line);
            if (lines.Count > 0)
            {
                for (var attempt = 0; ; attempt++)
                {
                    try { await File.AppendAllLinesAsync(path, lines); break; }
                    catch (IOException) when (attempt < 2) { await Task.Delay(50); }
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        finally
        {
            Interlocked.Exchange(ref writing, 0);
            if (!pending.IsEmpty && Interlocked.CompareExchange(ref writing, 1, 0) == 0) _ = Task.Run(FlushAsync);
        }
    }

    private sealed class Measurement(string stage) : IDisposable
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();
        public void Dispose() { clock.Stop(); Record(stage, clock.Elapsed.TotalMilliseconds); }
    }
}
