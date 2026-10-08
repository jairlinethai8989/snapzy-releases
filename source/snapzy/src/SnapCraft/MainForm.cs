using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace SnapCraft;

internal sealed partial class MainForm : Form
{
    [DllImport("user32.dll")] private static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
    private readonly WebView2 web = new() { Dock = DockStyle.Fill };
    private readonly CaptureCoordinator capture = new();
    private readonly AppSettings settings = AppSettings.Load();
    private EditorHubForm? tabHub;
    private VideoSession? video;
    private readonly System.Windows.Forms.Timer recordingTimer = new() { Interval = 250 };
    private readonly NotifyIcon tray = new();
    private readonly ContextMenuStrip trayMenu = new();
    private ToolStripMenuItem startupMenuItem = null!;
    private readonly Label loading = new() { Dock = DockStyle.Fill, Text = "กำลังเตรียม SnapZy…", TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.White };
    private Task<bool>? cpuAvailability;
    private bool busy;
    private bool videoStarting;
    private bool exitRequested;
    private ShortcutManager? shortcuts;
    private RecordingStatusForm? recordingStatus;
    private CancellationTokenSource? videoCountdown;
    private bool videoStopping;
    private readonly HashSet<EditorHubForm> editors = new();
    private readonly HashSet<VideoPreviewForm> videoPreviews = new();
    private bool exitInProgress;
    private bool exitApproved;
    private readonly Task assetsPrepared;
    private readonly System.Diagnostics.Stopwatch startupClock = System.Diagnostics.Stopwatch.StartNew();

    private bool suppressInitialShow;

    protected override void SetVisibleCore(bool value)
    {
        if (suppressInitialShow && value)
        {
            suppressInitialShow = false;
            _ = Handle;
            base.SetVisibleCore(false);
            return;
        }
        base.SetVisibleCore(value);
    }

    public MainForm(bool startInTray = false)
    {
        Localization.SetLanguage(settings.Language);
        loading.Text = Localization.Translate(loading.Text);
        suppressInitialShow = startInTray;
        assetsPrepared = Task.Run(() => { using var timing = PerformanceTrace.Measure("startup.assets"); WebAssets.Prepare(); });
        Text = $"{AppInfo.ProductName} {AppInfo.Version}";
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "icons", ProductProfile.Current.IconFile));
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        TopMost = false;
        ClientSize = new Size(380, 156);
        Controls.Add(web);
        Controls.Add(loading);
        loading.BringToFront();
        CreateFastLauncher();
        Shown += async (_, _) =>
        {
            PerformanceTrace.Record("launcher.visible", startupClock.Elapsed.TotalMilliseconds);
            await EnsureBrowserAsync();
        };
        recordingTimer.Tick += (_, _) =>
        {
            if (video?.FailureMessage is string failure)
            {
                recordingTimer.Stop();
                _ = StopVideoAsync(true, failure);
            }
            else if (video is not null)
            {
                var elapsed = video.Elapsed;
                Send(new { type = "elapsed", text = RecordingPolicy.ElapsedText(elapsed) });
                recordingStatus?.UpdateElapsed(elapsed);
                if (!checkingRecordingSpace && elapsed - lastSpaceCheck > TimeSpan.FromSeconds(2)) _ = CheckRecordingSpaceAsync(video);
            }
        };
        trayMenu.Items.Add(Localization.Translate("เปิด SnapZy"), null, (_, _) => ShowLauncher());
        startupMenuItem = new ToolStripMenuItem(Localization.Translate("เริ่มพร้อม Windows")) { Checked = StartupRegistration.Enabled, CheckOnClick = true };
        startupMenuItem.Click += (_, _) =>
        {
            try { StartupRegistration.SetEnabled(startupMenuItem.Checked); }
            catch (Exception error) { startupMenuItem.Checked = StartupRegistration.Enabled; MessageBox.Show(Localization.Translate(error.Message), AppInfo.ProductName); }
        };
        trayMenu.Items.Add(startupMenuItem);
        trayMenu.Items.Add(Localization.Translate("หยุดและบันทึก MP4"), null, async (_, _) => await StopVideoAsync(true));
        trayMenu.Items.Add(Localization.Translate("ยกเลิกวิดีโอ"), null, async (_, _) => await StopVideoAsync(false));
        trayMenu.Items.Add(Localization.CurrentLanguage == "th" ? "ประวัติและกู้คืนงาน" : "History and recovery", null, async (_, _) =>
        {
            using var history = new HistoryForm();
            if (history.ShowDialog(this) == DialogResult.OK && history.SelectedPath is not null) OpenEditor(await CaptureHistory.RestoreAsync(history.SelectedPath));
            RefreshHistorySettings();
        });
        trayMenu.Items.Add(Localization.Translate("ออกจาก SnapZy"), null, (_, _) => { exitRequested = true; Close(); });
        tray.Icon = Icon;
        tray.Text = AppInfo.ProductName;
        tray.ContextMenuStrip = trayMenu;
        tray.Visible = true;
        tray.DoubleClick += (_, _) => ShowLauncher();
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowLauncher(); };
        FormClosing += async (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing && !exitRequested) { e.Cancel = true; Hide(); return; }
            if (!exitApproved)
            {
                e.Cancel = true;
                if (exitInProgress) return;
                if (busy || videoStarting || videoStopping) { videoCountdown?.Cancel(); exitRequested = false; return; }
                exitInProgress = true;
                try
                {
                    foreach (var editor in editors.ToArray())
                        if (!await editor.RequestCloseAsync()) { exitRequested = false; return; }
                    if (video is not null) await StopVideoAsync(true);
                    foreach (var preview in videoPreviews.ToArray())
                        if (!await preview.RequestCloseAsync()) { exitRequested = false; return; }
                    exitApproved = true;
                    Close();
                }
                finally { exitInProgress = false; }
                return;
            }
            warmEditor?.Dispose();
            warmEditor = null;
            tray.Visible = false;
            tray.Dispose();
        };
    }

    private async Task InitializeBrowserAsync()
    {
        try
        {
            var env = await BrowserEnvironment.GetAsync();
            if (IsDisposed) return;
            using (PerformanceTrace.Measure("launcher.controller")) await web.EnsureCoreWebView2Async(env);
            await assetsPrepared;
            if (IsDisposed) return;
            web.CoreWebView2.SetVirtualHostNameToFolderMapping("snapcraft.local", WebAssets.WebRoot, CoreWebView2HostResourceAccessKind.DenyCors);
            web.CoreWebView2.WebMessageReceived += async (_, args) => await HandleMessageAsync(args.WebMessageAsJson);
            web.CoreWebView2.NavigationCompleted += (_, args) => { if (args.IsSuccess) { loading.Hide(); fastLauncher.Hide(); } };
            web.Source = new Uri($"https://snapcraft.local/launcher.html?language={settings.Language}&product={Uri.EscapeDataString(AppInfo.ProductName)}");
            SetWindowDisplayAffinity(Handle, 0x11);
        }
        catch (Exception error)
        {
            if (IsDisposed) return;
            MessageBox.Show(this, Localization.Translate($"เปิดหน้าตา SnapZy ไม่ได้: {error.Message}\nโปรดติดตั้ง Microsoft Edge WebView2 Runtime"), AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task HandleMessageAsync(string message)
    {
        try
        {
            using var json = JsonDocument.Parse(message);
            var root = json.RootElement;
            var action = root.GetProperty("action").GetString();
            switch (action)
            {
                case "ready":
                    PerformanceTrace.Record("launcher.ready", startupClock.Elapsed.TotalMilliseconds);
                    SendSettings();
                    _ = RefreshCpuAvailabilityAsync();
                    var protectedCaptures = editors.SelectMany(editor => editor.SourcePaths).ToArray();
                    _ = Task.Run(() => { try { WebAssets.CleanupCaptures(protectedCaptures); CaptureHistory.Cleanup(AppSettings.Load().HistoryDays); } catch (IOException) { } catch (UnauthorizedAccessException) { } });
                    CleanupVideoClips();
                    _ = PrepareNextEditorAsync();
                    break;
                case "hotkey":
                    var modifiers = root.GetProperty("modifiers").GetUInt32();
                    var key = root.GetProperty("key").GetUInt32();
                    var mode = root.TryGetProperty("mode", out var modeValue) ? modeValue.GetString()! : "launcher";
                    var enabled = !root.TryGetProperty("enabled", out var enabledValue) || enabledValue.ValueKind == JsonValueKind.True;
                    var binding = new ShortcutBinding(modifiers, key, enabled);
                    RefreshHistorySettings(); shortcuts!.UpdateAndSave(settings, mode, binding);
                    SendSettings();
                    Send(new { type = "hotkeySaved", mode });
                    SetStatus("บันทึกคีย์ลัดแล้ว");
                    break;
                case "installCpu": await InstallCpuAsync(); break;
                case "layout":
                    ClientSize = new Size(380, root.GetProperty("expanded").GetBoolean() ? 320 : 156);
                    var workArea = Screen.FromControl(this).WorkingArea;
                    Location = new Point(Math.Clamp(Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - Width)),
                        Math.Clamp(Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - Height)));
                    break;
                case "settings":
                    RefreshHistorySettings();
                    settings.DelayMs = root.GetProperty("delayMs").GetInt32();
                    settings.OpenMode = root.GetProperty("openMode").GetString() == "tab" ? "tab" : "window";
                    settings.Save();
                    break;
                case "language":
                    ChangeLanguage(root.GetProperty("language").GetString() ?? ProductProfile.Current.DefaultLanguage);
                    break;
                case "area": await CaptureAsync(CaptureKind.Area); break;
                case "browseImages":
                    if (busy || exitInProgress || videoStarting || videoStopping || video is not null) break;
                    busy = true;
                    Send(new { type = "busy", value = true });
                    try
                    {
                        await assetsPrepared;
                        using var images = new OpenFileDialog { Title = Localization.Translate("SnapZy | เลือกภาพเพื่อแก้ไข"), Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff", Multiselect = true, CheckFileExists = true };
                        if (images.ShowDialog(this) != DialogResult.OK) break;
                        foreach (var source in images.FileNames)
                        {
                            var temporary = await ImageImport.CopyAsync(source);
                            try { TopMost = false; OpenEditor(temporary); }
                            catch { if (File.Exists(temporary)) File.Delete(temporary); throw; }
                        }
                    }
                    finally { busy = false; Send(new { type = "busy", value = false }); }
                    break;
                case "window": await CaptureAsync(CaptureKind.Window); break;
                case "scroll": await CaptureAsync(CaptureKind.Scroll); break;
                case "videoScreen": await StartVideoAsync(false, ReadAudio(root)); break;
                case "videoWindow": await StartVideoAsync(true, ReadAudio(root)); break;
                case "stopVideo": await StopVideoAsync(true); break;
                case "cancelVideo": await StopVideoAsync(false); break;
            }
        }
        catch (Exception error)
        {
            SetStatus(error.Message, true);
            MessageBox.Show(this, Localization.Translate(error.Message), AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task CaptureAsync(CaptureKind kind)
    {
        if (busy || exitInProgress || videoStarting || videoStopping || video is not null) return;
        busy = true;
        var mode = kind switch { CaptureKind.Area => "area", CaptureKind.Window => "window", _ => "scroll" };
        Send(new { type = "captureActivity", mode, value = true });
        Send(new { type = "busy", value = true });
        Hide();
        EditorHubForm? openedEditor = null;
        try
        {
            await assetsPrepared;
            var path = await capture.CaptureAsync(kind, settings.DelayMs);
            if (path is not null)
            {
                openedEditor = OpenEditor(path);
                if (CaptureHistory.Enabled) _ = RetainCaptureHistoryAsync(path);
            }
            else SetStatus("ยกเลิกการจับภาพ");
        }
        catch (Exception error)
        {
            SetStatus(error.Message, true);
            MessageBox.Show(Localization.Translate(error.Message), AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            Show();
            busy = false;
            Send(new { type = "captureActivity", mode, value = false });
            Send(new { type = "busy", value = false });
            if (openedEditor is not null) { TopMost = false; openedEditor.Activate(); }
        }
    }

    private EditorHubForm OpenEditor(string path)
    {
        EditorHubForm hub;
        if (settings.OpenMode == "tab")
        {
            if (tabHub is null || tabHub.IsDisposed) tabHub = TakePreparedEditor();
            hub = tabHub;
        }
        else hub = TakePreparedEditor();
        if (editors.Add(hub))
        {
            hub.FormClosed += (_, _) => editors.Remove(hub);
            hub.CaptureReady += () => _ = PrepareNextEditorAsync();
        }
        hub.AddCapture(path);
        hub.Show();
        hub.Activate();
        _ = PrepareNextEditorAsync();
        return hub;
    }

    private static VideoAudio ReadAudio(JsonElement root) => new(
        root.TryGetProperty("systemAudio", out var system) && system.ValueKind == JsonValueKind.True,
        root.TryGetProperty("microphoneAudio", out var microphone) && microphone.ValueKind == JsonValueKind.True);

    private void SendSettings()
    {
        var cpuAvailable = cpuAvailability is not { IsCompletedSuccessfully: true } || cpuAvailability.Result;
        var bindings = settings.GetShortcuts().Select(entry => new { mode = entry.Key, modifiers = entry.Value.Modifiers, key = entry.Value.Key, enabled = entry.Value.Enabled, error = shortcuts?.Errors.GetValueOrDefault(entry.Key) });
        Send(new { type = "settings", delayMs = settings.DelayMs, openMode = settings.OpenMode, language = settings.Language, productName = AppInfo.ProductName, version = AppInfo.Version, developer = AppInfo.Developer, cpuAvailable, shortcuts = bindings });
    }

    private void ChangeLanguage(string language)
    {
        RefreshHistorySettings();
        settings.Language = language;
        Localization.SetLanguage(language);
        settings.Save();
        RefreshNativeLanguage();
        foreach (var editor in editors) editor.RefreshLanguage();
        foreach (var preview in videoPreviews) preview.RefreshLanguage();
        SendSettings();
    }

    private void RefreshNativeLanguage()
    {
        Text = $"{AppInfo.ProductName} {AppInfo.Version}";
        loading.Text = Localization.Translate("กำลังเตรียม SnapZy…");
        tray.Text = AppInfo.ProductName;
        if (trayMenu.Items.Count != 6) return;
        trayMenu.Items[0].Text = Localization.Translate("เปิด SnapZy");
        startupMenuItem.Text = Localization.Translate("เริ่มพร้อม Windows");
        trayMenu.Items[2].Text = Localization.Translate("หยุดและบันทึก MP4");
        trayMenu.Items[3].Text = Localization.Translate("ยกเลิกวิดีโอ");
        trayMenu.Items[4].Text = Localization.CurrentLanguage == "th" ? "ประวัติและกู้คืนงาน" : "History and recovery";
        trayMenu.Items[5].Text = Localization.Translate("ออกจาก SnapZy");
    }

    private async Task RefreshCpuAvailabilityAsync()
    {
        cpuAvailability ??= Task.Run(() =>
        {
            try { FfmpegVideoSession.FindExecutable(); return true; }
            catch (Exception error) when (error is InvalidOperationException or IOException or UnauthorizedAccessException) { return false; }
        });
        var available = await cpuAvailability;
        if (!IsDisposed) Send(new { type = "cpuAvailability", value = available });
    }

    private async Task InstallCpuAsync()
    {
        if (busy || videoStarting || video is not null) return;
        if (MessageBox.Show(this, Localization.Translate("ดาวน์โหลดและติดตั้ง FFmpeg จากแพ็กเกจ Gyan.FFmpeg ผ่าน WinGet? ต้องใช้อินเทอร์เน็ตและพื้นที่ดิสก์เพิ่มเติม"), $"{AppInfo.ProductName} • CPU recorder", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        busy = true; Send(new { type = "busy", value = true }); SetStatus("กำลังติดตั้งตัวบันทึก CPU...");
        try
        {
            var info = new System.Diagnostics.ProcessStartInfo("winget.exe")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var arg in new[] { "install", "--id", "Gyan.FFmpeg", "--exact", "--source", "winget", "--silent", "--accept-package-agreements", "--accept-source-agreements" }) info.ArgumentList.Add(arg);
            using var installer = System.Diagnostics.Process.Start(info) ?? throw new InvalidOperationException("เปิด WinGet ไม่ได้");
            var output = installer.StandardOutput.ReadToEndAsync(); var error = installer.StandardError.ReadToEndAsync();
            await installer.WaitForExitAsync(); await output;
            if (installer.ExitCode != 0) throw new InvalidOperationException($"ติดตั้ง FFmpeg ไม่สำเร็จ: {await error}");
            cpuAvailability = null;
            await RefreshCpuAvailabilityAsync();
            if (cpuAvailability?.Result != true) throw new InvalidOperationException("ติดตั้งแล้วแต่ยังไม่พบ FFmpeg");
            SetStatus("ติดตั้งตัวบันทึก CPU แล้ว");
        }
        finally { busy = false; Send(new { type = "busy", value = false }); }
    }

    private async Task StartVideoAsync(bool window, VideoAudio audio)
    {
        if (busy || exitInProgress || videoStarting || videoStopping || video is not null) return;
        videoStarting = true;
        Send(new { type = "busy", value = true });
        IntPtr handle = IntPtr.Zero;
        var temporaryPath = Path.Combine(WebAssets.DataRoot, "Recordings", $"{Guid.NewGuid():N}.mp4");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(temporaryPath)!);
            if (RecordingPolicy.MustStop(await Task.Run(() => RecordingPolicy.FreeBytes(temporaryPath))))
                throw new IOException(Localization.CurrentLanguage == "th" ? "พื้นที่ดิสก์ไม่พอเริ่มบันทึก กรุณาเพิ่มพื้นที่ว่าง" : "Not enough free disk space to start recording.");
            lastSpaceCheck = TimeSpan.Zero;
            if (window)
            {
                Hide();
                var selected = await SelectionOverlay.ChooseAsync(SelectionMode.Window);
                if (selected is null) return;
                handle = selected.WindowHandle;
            }
            Hide();
            videoCountdown = new CancellationTokenSource();
            recordingStatus = new RecordingStatusForm(save =>
            {
                if (videoStarting) videoCountdown?.Cancel();
                else _ = StopVideoAsync(save);
            });
            recordingStatus.Show();
            await recordingStatus.CountdownAsync(videoCountdown.Token);
            videoCountdown.Token.ThrowIfCancellationRequested();
            if (handle != IntPtr.Zero) NativeInput.FocusWindow(handle);
            video = new VideoSession(handle, temporaryPath, audio);
            await video.StartAsync();
            if (videoCountdown.IsCancellationRequested)
            {
                await video.StopAsync(false);
                video.Dispose(); video = null;
                throw new OperationCanceledException();
            }
            recordingStatus.BeginRecording();
            recordingTimer.Start();
            Send(new { type = "recording", value = true });
            tray.Visible = true;
            SetStatus($"กำลังอัด {video.BackendName}");
            if (video.BackendName == "CPU / GDI")
            {
                tray.ShowBalloonTip(4000, $"{AppInfo.ProductName} • CPU", Localization.Translate(window ? "จับบริเวณหน้าต่างที่มองเห็น กรุณาอย่าย่อ ย้าย หรือให้หน้าต่างอื่นบัง" : "กำลังใช้โหมดสำรอง CPU เพื่อบันทึก MP4"), ToolTipIcon.Info);
            }
        }
        catch (OperationCanceledException) { SetStatus("ยกเลิกการเตรียมบันทึก"); }
        catch (Exception error)
        {
            video?.Dispose();
            video = null;
            try { File.Delete(temporaryPath); } catch (IOException) { }
            SetStatus($"บันทึก MP4 ไม่ได้: {error.Message}", true);
            MessageBox.Show(this, Localization.Translate($"เริ่มบันทึก MP4 ไม่ได้: {error.Message}"), AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            videoStarting = false;
            videoCountdown?.Dispose(); videoCountdown = null;
            if (video is null)
            {
                recordingStatus?.Dismiss(); recordingStatus?.Dispose(); recordingStatus = null;
                ShowLauncher();
                Send(new { type = "busy", value = false });
            }
        }
    }

    private async Task StopVideoAsync(bool save, string? failure = null)
    {
        if (video is null || videoStopping) return;
        videoStopping = true;
        Send(new { type = "busy", value = true });
        recordingStatus?.Finalizing();
        var active = video;
        video = null;
        recordingTimer.Stop();
        SetStatus("กำลังปิดไฟล์วิดีโอ...");
        string? recoveryPath = null;
        string? previewPath = null;
        try
        {
            var temporaryPath = await active.StopAsync(save);
            if (failure is not null) throw new InvalidOperationException(failure);
            if (temporaryPath is null) SetStatus("ยกเลิกวิดีโอ");
            else
            {
                recoveryPath = temporaryPath;
                previewPath = VideoClipStore.Retain(temporaryPath);
                recoveryPath = null;
            }
        }
        catch (Exception error)
        {
            var detail = recoveryPath is null ? error.Message : $"{error.Message}\nคลิปชั่วคราวยังอยู่ที่: {recoveryPath}";
            SetStatus($"บันทึก MP4 ไม่สำเร็จ: {error.Message}", true);
            MessageBox.Show(this, Localization.Translate(detail), AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            active.Dispose();
            recordingStatus?.Dismiss(); recordingStatus?.Dispose(); recordingStatus = null;
            videoStopping = false;
            ShowLauncher();
            Send(new { type = "recording", value = false });
            if (previewPath is not null) OpenVideoPreview(previewPath);
        }
    }

    private void OpenVideoPreview(string path)
    {
        var preview = new VideoPreviewForm(path);
        videoPreviews.Add(preview);
        preview.FormClosed += (_, _) => videoPreviews.Remove(preview);
        preview.Show(); preview.Activate();
    }

    private void CleanupVideoClips()
    {
        try
        {
            var protectedPaths = videoPreviews.Select(preview => preview.ClipPath).ToList();
            if (Clipboard.ContainsFileDropList()) protectedPaths.AddRange(Clipboard.GetFileDropList().Cast<string>());
            _ = Task.Run(() => { try { VideoClipStore.Cleanup(protectedPaths); } catch (IOException) { } catch (UnauthorizedAccessException) { } });
        }
        catch (ExternalException) { /* Skip cleanup when the clipboard cannot be inspected safely. */ }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        shortcuts = new ShortcutManager(Handle, settings.GetShortcuts());
        BeginInvoke(new Action(() => _ = EnsureBrowserAsync()));
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        shortcuts?.Dispose();
        shortcuts = null;
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message message)
    {
        var action = shortcuts?.ActionFor(message);
        if (action is not null && !busy && !exitInProgress && !videoStarting && !videoStopping) _ = ExecuteShortcutAsync(action);
        base.WndProc(ref message);
    }

    private async Task ExecuteShortcutAsync(string action)
    {
        if (action == "launcher") { ShowLauncher(); return; }
        if (video is not null) return;
        Send(new { type = "mode", value = action });
        if (action == "video")
        {
            ShowLauncher();
            if (web.CoreWebView2 is not null && !fastLauncher.Visible) Send(new { type = "videoPrompt" });
            else { using var options = new VideoOptionsDialog(); if (options.ShowDialog(this) == DialogResult.OK) await StartVideoAsync(options.CaptureWindow, options.Audio); }
        }
        else await CaptureAsync(action switch { "area" => CaptureKind.Area, "window" => CaptureKind.Window, _ => CaptureKind.Scroll });
    }

    internal void ShowLauncher()
    {
        if (IsDisposed) return;
        if (busy || videoStarting || videoStopping) { recordingStatus?.Activate(); return; }
        Show();
        WindowState = FormWindowState.Normal;
        TopMost = false;
        if (video is null) Send(new { type = "idle" });
        BringToFront();
        Activate();
        NativeInput.FocusWindow(Handle);
    }
    private void SetStatus(string text, bool error = false) => Send(new { type = "status", text = Localization.Translate(text), error });
    private void Send(object message)
    {
        if (web.CoreWebView2 is not null) web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message));
    }
}
