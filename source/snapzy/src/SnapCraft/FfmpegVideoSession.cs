using System.Diagnostics;
using System.Globalization;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace SnapCraft;

internal sealed class FfmpegVideoSession : IDisposable
{
    private sealed class AudioTrack : IDisposable
    {
        private readonly WasapiCapture capture;
        private readonly WaveFileWriter writer;
        private readonly Stopwatch clock = new();
        private readonly object gate = new();
        private readonly TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool disposed;
        public string Path { get; }
        public string? Failure { get; private set; }

        public AudioTrack(bool loopback, string path)
        {
            Path = path;
            capture = loopback ? new WasapiLoopbackCapture() : new WasapiCapture();
            try { writer = new WaveFileWriter(path, capture.WaveFormat); }
            catch { capture.Dispose(); throw; }
            capture.DataAvailable += (_, args) =>
            {
                lock (gate)
                {
                    // Loopback sends no packets while the output device is silent; preserve those gaps.
                    var expected = AlignedBytes(clock.Elapsed.TotalSeconds) - args.BytesRecorded;
                    if (expected - writer.Length > capture.WaveFormat.AverageBytesPerSecond / 5) PadTo(expected);
                    writer.Write(args.Buffer, 0, args.BytesRecorded);
                }
            };
            capture.RecordingStopped += (_, args) => { Failure = args.Exception?.Message; stopped.TrySetResult(); };
        }

        private long AlignedBytes(double seconds) => (long)(seconds * capture.WaveFormat.AverageBytesPerSecond) / capture.WaveFormat.BlockAlign * capture.WaveFormat.BlockAlign;
        private void PadTo(long bytes)
        {
            var silence = new byte[8192];
            while (writer.Length < bytes) writer.Write(silence, 0, (int)Math.Min(silence.Length, bytes - writer.Length));
        }
        public void Start() { clock.Start(); capture.StartRecording(); }
        public async Task StopAsync()
        {
            capture.StopRecording();
            await stopped.Task.WaitAsync(TimeSpan.FromSeconds(10));
            lock (gate) { PadTo(AlignedBytes(clock.Elapsed.TotalSeconds)); writer.Flush(); }
            if (Failure is not null) throw new InvalidOperationException($"บันทึกเสียงไม่ได้: {Failure}");
        }
        public void Dispose() { if (disposed) return; disposed = true; capture.Dispose(); lock (gate) writer.Dispose(); }
    }

    private readonly IntPtr windowHandle;
    private readonly string path;
    private readonly VideoAudio audio;
    private readonly List<AudioTrack> tracks = new();
    private readonly Queue<string> errors = new();
    private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Process? process;
    private string? videoPath;
    private string? finalizedPath;
    private bool stopping;
    private bool processStarted;
    public DateTime StartedAt { get; private set; }
    public string? FailureMessage => tracks.Select(track => track.Failure).FirstOrDefault(error => error is not null)
        ?? (process is { HasExited: true } && !stopping ? $"ตัวบันทึก CPU หยุดทำงาน: {ErrorDetail()}" : null);

    public FfmpegVideoSession(IntPtr windowHandle, string path, VideoAudio audio)
    {
        this.windowHandle = windowHandle; this.path = path; this.audio = audio;
    }

    internal static string FindExecutable()
    {
        var configured = Environment.GetEnvironmentVariable("SNAPCRAFT_FFMPEG");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        var bundled = System.IO.Path.Combine(AppContext.BaseDirectory, "Tools", "ffmpeg.exe");
        if (File.Exists(bundled)) return bundled;
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            var file = System.IO.Path.Combine(directory.Trim('"'), "ffmpeg.exe");
            if (File.Exists(file)) return file;
        }
        var winget = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages");
        if (Directory.Exists(winget))
        {
            foreach (var package in Directory.EnumerateDirectories(winget, "Gyan.FFmpeg_*"))
                foreach (var file in Directory.EnumerateFiles(package, "ffmpeg.exe", SearchOption.AllDirectories)) return file;
        }
        throw new InvalidOperationException("โหมด CPU ต้องใช้ FFmpeg กรุณาติดตั้งจากปุ่ม ติดตั้งตัวบันทึก CPU แล้วลองอีกครั้ง");
    }

    internal static string[] RecordingArguments(Rectangle region, string output) => new[]
    {
        "-hide_banner", "-y", "-loglevel", "warning", "-progress", "pipe:1", "-stats_period", "0.2",
        "-f", "gdigrab", "-framerate", "20", "-draw_mouse", "1", "-probesize", "32",
        "-offset_x", region.Left.ToString(CultureInfo.InvariantCulture), "-offset_y", region.Top.ToString(CultureInfo.InvariantCulture),
        "-video_size", $"{region.Width}x{region.Height}", "-i", "desktop",
        "-an", "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2", "-c:v", "libx264", "-preset", "ultrafast", "-crf", "23", "-pix_fmt", "yuv420p",
        "-movflags", "+faststart", output
    };

    private static Process NewProcess(IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(FindExecutable()) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return new Process { StartInfo = info, EnableRaisingEvents = true };
    }

    public async Task StartAsync()
    {
        var region = windowHandle == IntPtr.Zero ? Screen.PrimaryScreen!.Bounds : Rectangle.Intersect(NativeInput.VisibleWindowBounds(windowHandle), SystemInformation.VirtualScreen);
        if (region.Width < 24 || region.Height < 24) throw new InvalidOperationException("หน้าต่างเป้าหมายถูกย่อหรือไม่มีพื้นที่ที่มองเห็น");
        videoPath = path + ".video.mp4";
        finalizedPath = path + $".ready-{Guid.NewGuid():N}.mp4";
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        process = NewProcess(RecordingArguments(region, videoPath));
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data?.StartsWith("frame=", StringComparison.Ordinal) == true && int.TryParse(args.Data.AsSpan(6), out var frame) && frame > 0) started.TrySetResult();
        };
        process.ErrorDataReceived += (_, args) => { if (!string.IsNullOrWhiteSpace(args.Data)) lock (errors) { if (errors.Count >= 15) errors.Dequeue(); errors.Enqueue(args.Data); } };
        process.Exited += (_, _) => { if (!stopping) started.TrySetException(new InvalidOperationException($"เริ่มตัวบันทึก CPU ไม่ได้: {ErrorDetail()}")); };
        try
        {
            if (audio.System) tracks.Add(new AudioTrack(true, path + ".system.wav"));
            if (audio.Microphone) tracks.Add(new AudioTrack(false, path + ".microphone.wav"));
            foreach (var track in tracks) track.Start();
            if (!process.Start()) throw new InvalidOperationException("เปิด FFmpeg ไม่ได้");
            processStarted = true;
            StartedAt = DateTime.Now;
            process.BeginOutputReadLine(); process.BeginErrorReadLine();
            await started.Task.WaitAsync(TimeSpan.FromSeconds(15));
            if (FailureMessage is string failure) throw new InvalidOperationException(failure);
        }
        catch { Dispose(); DeleteTemporaryFiles(); throw; }
    }

    private string ErrorDetail() { lock (errors) return string.Join(" | ", errors.TakeLast(4)); }

    public async Task<string?> StopAsync(bool save)
    {
        if (process is null || videoPath is null) throw new InvalidOperationException("ยังไม่ได้เริ่มอัด CPU");
        stopping = true;
        try
        {
            if (!process.HasExited)
            {
                await process.StandardInput.WriteLineAsync("q"); await process.StandardInput.FlushAsync();
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            }
            foreach (var track in tracks) await track.StopAsync();
            foreach (var track in tracks) track.Dispose();
            if (!save) { DeleteTemporaryFiles(); return null; }
            if (process.ExitCode != 0 || !File.Exists(videoPath)) throw new InvalidOperationException($"ปิดไฟล์ MP4 ไม่สำเร็จ: {ErrorDetail()}");
            // Native capture may still hold its failed output; finalize to a fresh file.
            if (tracks.Count == 0) File.Move(videoPath, finalizedPath!, false);
            else await MuxAudioAsync(videoPath, tracks.Select(track => track.Path).ToArray(), finalizedPath!);
            DeleteTemporaryFiles();
            return finalizedPath;
        }
        catch (Exception error)
        {
            if (!save) { Dispose(); DeleteTemporaryFiles(); return null; }
            throw new InvalidOperationException($"{error.Message}\nไฟล์สำรองอยู่ที่: {videoPath}", error);
        }
    }

    internal static async Task MuxAudioAsync(string video, string[] audioFiles, string output)
    {
        var args = new List<string> { "-hide_banner", "-y", "-loglevel", "error", "-i", video };
        foreach (var file in audioFiles) args.AddRange(new[] { "-i", file });
        var inputs = string.Concat(Enumerable.Range(1, audioFiles.Length).Select(index => $"[{index}:a]"));
        args.AddRange(new[] { "-filter_complex", $"{inputs}amix=inputs={audioFiles.Length}:duration=longest:normalize=1,apad[a]", "-map", "0:v", "-map", "[a]", "-c:v", "copy", "-c:a", "aac", "-b:a", "128k", "-shortest", "-movflags", "+faststart", output });
        using var mux = NewProcess(args);
        mux.Start();
        var stderr = mux.StandardError.ReadToEndAsync();
        var stdout = mux.StandardOutput.ReadToEndAsync();
        try { await mux.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(5)); }
        catch { if (!mux.HasExited) mux.Kill(true); throw; }
        await stdout;
        if (mux.ExitCode != 0) throw new InvalidOperationException($"รวมเสียง MP4 ไม่สำเร็จ: {await stderr}");
    }

    private void DeleteTemporaryFiles()
    {
        foreach (var file in tracks.Select(track => track.Path).Append(videoPath)) if (file is not null) { try { File.Delete(file); } catch (IOException) { } }
    }
    public void Dispose()
    {
        stopping = true;
        if (process is not null)
        {
            if (processStarted && !process.HasExited) process.Kill(true);
            process.Dispose(); process = null;
        }
        foreach (var track in tracks) track.Dispose();
    }
}
