using ScreenRecorderLib;

namespace SnapCraft;

internal sealed record VideoAudio(bool System = false, bool Microphone = false);

internal sealed class VideoSession : IDisposable
{
    private sealed class Attempt : IDisposable
    {
        public Recorder Recorder { get; }
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<string> Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Attempt(RecordingSourceBase source, VideoAudio audio, string logPath)
        {
            var options = new RecorderOptions
            {
                SourceOptions = new SourceOptions { RecordingSources = new List<RecordingSourceBase> { source } },
                OutputOptions = new OutputOptions { RecorderMode = RecorderMode.Video },
                VideoEncoderOptions = new VideoEncoderOptions
                {
                    Encoder = new H264VideoEncoder(),
                    Framerate = 30,
                    Bitrate = 5_000_000,
                    IsHardwareEncodingEnabled = false
                },
                AudioOptions = CreateAudioOptions(audio),
                LogOptions = new LogOptions { IsLogEnabled = true, LogFilePath = logPath },
                MouseOptions = new MouseOptions { IsMousePointerEnabled = true }
            };
            Recorder = Recorder.CreateRecorder(options);
            Recorder.OnStatusChanged += (_, args) =>
            {
                if (args.Status == RecorderStatus.Recording) Started.TrySetResult(true);
            };
            Recorder.OnRecordingComplete += (_, args) =>
            {
                Completed.TrySetResult(args.FilePath);
                Started.TrySetException(new InvalidOperationException("วิดีโอหยุดก่อนเริ่มอัด"));
            };
            Recorder.OnRecordingFailed += (_, args) =>
            {
                var error = new InvalidOperationException(args.Error);
                Started.TrySetException(error);
                Completed.TrySetException(error);
            };
        }

        public void Dispose() => Recorder.Dispose();
    }

    private readonly IntPtr windowHandle;
    private readonly string path;
    private readonly VideoAudio audio;
    private Attempt? active;
    private FfmpegVideoSession? cpu;
    private static bool preferCpu;
    private readonly bool forceCpu;
    public string BackendName { get; private set; } = "";
    public string DiagnosticPath { get; }

    public DateTime StartedAt { get; private set; }
    public string? FailureMessage => cpu?.FailureMessage ?? (active?.Completed.Task.IsFaulted == true
        ? active.Completed.Task.Exception?.GetBaseException().Message : null);

    public VideoSession(IntPtr windowHandle, string outputPath, VideoAudio? audio = null, bool forceCpu = false)
    {
        this.windowHandle = windowHandle;
        path = outputPath;
        this.audio = audio ?? new VideoAudio();
        this.forceCpu = forceCpu;
        DiagnosticPath = Path.Combine(WebAssets.DataRoot, "Logs", $"video-{Guid.NewGuid():N}.log");
    }

    internal static AudioOptions CreateAudioOptions(VideoAudio audio)
    {
        var options = new AudioOptions { IsAudioEnabled = audio.System || audio.Microphone };
        if (audio.System) options.AudioSources.Add(LoopbackAudioSource.Default);
        if (audio.Microphone) options.AudioSources.Add(CaptureAudioSource.Default);
        return options;
    }

    public async Task StartAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Directory.CreateDirectory(Path.GetDirectoryName(DiagnosticPath)!);
        File.AppendAllText(DiagnosticPath, $"SnapZy {AppInfo.Version}; audio: system={audio.System}, microphone={audio.Microphone}\n");
        var sources = windowHandle != IntPtr.Zero
            ? new (string Name, Func<RecordingSourceBase> Create)[]
            {
                ("Windows Graphics Capture", () => new WindowRecordingSource(windowHandle))
            }
            : new (string Name, Func<RecordingSourceBase> Create)[]
            {
                ("Desktop Duplication", () => new DisplayRecordingSource(DisplayRecordingSource.MainMonitor.DeviceName) { RecorderApi = RecorderApi.DesktopDuplication }),
                ("Windows Graphics Capture", () => new DisplayRecordingSource(DisplayRecordingSource.MainMonitor.DeviceName) { RecorderApi = RecorderApi.WindowsGraphicsCapture })
            };
        var errors = new List<string>();
        foreach (var source in forceCpu || preferCpu ? Array.Empty<(string Name, Func<RecordingSourceBase> Create)>() : sources)
        {
            Attempt? attempt = null;
            var attemptPath = path + $".native-{errors.Count}.mp4";
            try
            {
                attempt = new Attempt(source.Create(), audio, DiagnosticPath + $".{errors.Count}.native.log");
                attempt.Recorder.Record(attemptPath);
                await attempt.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
                active = attempt;
                BackendName = source.Name;
                StartedAt = DateTime.Now;
                return;
            }
            catch (Exception error)
            {
                errors.Add($"{source.Name}: {error.Message}");
                File.AppendAllText(DiagnosticPath, $"{source.Name}: {error}\n");
                if (attempt is not null)
                {
                    try { attempt.Recorder.Stop(); } catch (Exception cleanup) { File.AppendAllText(DiagnosticPath, $"Native stop cleanup: {cleanup.Message}\n"); }
                    try { attempt.Dispose(); } catch (Exception cleanup) { File.AppendAllText(DiagnosticPath, $"Native dispose cleanup: {cleanup.Message}\n"); }
                    _ = attempt.Completed.Task.Exception;
                }
                try { File.Delete(attemptPath); } catch (IOException) { }
            }
        }
        try
        {
            cpu = new FfmpegVideoSession(windowHandle, path, audio);
            await cpu.StartAsync();
            preferCpu = true; BackendName = "CPU / GDI"; StartedAt = cpu.StartedAt;
            File.AppendAllText(DiagnosticPath, "CPU / GDI started successfully\n");
        }
        catch (Exception error)
        {
            cpu?.Dispose(); cpu = null;
            File.AppendAllText(DiagnosticPath, $"CPU / GDI: {error}\n");
            throw new InvalidOperationException($"วิธีจับภาพหลักเริ่มไม่ได้ และวิธีสำรอง CPU ไม่สำเร็จ: {error.Message}\nรายละเอียด: {DiagnosticPath}", error);
        }
    }

    public async Task<string?> StopAsync(bool save)
    {
        if (cpu is not null)
        {
            try { return await cpu.StopAsync(save); }
            catch (Exception error) { File.AppendAllText(DiagnosticPath, $"CPU stop failed: {error}\n"); throw; }
        }
        if (active is null) throw new InvalidOperationException("ยังไม่ได้เริ่มบันทึกวิดีโอ");
        var attempt = active;
        active = null;
        string completedPath;
        try
        {
            if (!attempt.Completed.Task.IsCompleted) attempt.Recorder.Stop();
            completedPath = await attempt.Completed.Task.WaitAsync(TimeSpan.FromMinutes(5));
        }
        finally { attempt.Dispose(); }
        if (!save) { File.Delete(completedPath); return null; }
        return completedPath;
    }

    public void Dispose() { active?.Dispose(); cpu?.Dispose(); }
}
