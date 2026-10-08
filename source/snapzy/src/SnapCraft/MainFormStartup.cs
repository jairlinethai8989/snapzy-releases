namespace SnapCraft;

internal sealed partial class MainForm
{
    private Panel fastLauncher = null!;
    private Task? browserInitialization;
    private EditorHubForm? warmEditor;

    private Task EnsureBrowserAsync() => browserInitialization ??= InitializeBrowserAsync();
    private EditorHubForm TakePreparedEditor()
    {
        if (warmEditor is null || warmEditor.IsDisposed) return new EditorHubForm(ShowLauncher, () => editors.ToArray(), ChangeLanguage);
        var editor = warmEditor; warmEditor = null; return editor;
    }

    private void CreateFastLauncher()
    {
        fastLauncher = new Panel { Name = "fastLauncher", Dock = DockStyle.Fill, BackColor = Color.White };
        fastLauncher.Controls.Add(new Label { Text = $"{AppInfo.ProductName}  v{AppInfo.Version}", AutoSize = true, Location = new Point(12, 12), Font = new Font("Segoe UI", 11, FontStyle.Bold) });
        var choices = new[] { ("Area", CaptureKind.Area), ("Window", CaptureKind.Window), ("Scrolling", CaptureKind.Scroll) };
        for (var index = 0; index < choices.Length; index++)
        {
            var choice = choices[index];
            var button = FastButton(choice.Item1, index);
            button.Disposed += (_, _) => button.Image?.Dispose();
            button.Click += async (_, _) => await CaptureAsync(choice.Item2);
            fastLauncher.Controls.Add(button);
        }
        var videoButton = FastButton("Video", 3);
        videoButton.Disposed += (_, _) => videoButton.Image?.Dispose();
        videoButton.Click += async (_, _) =>
        {
            using var options = new VideoOptionsDialog();
            if (options.ShowDialog(this) == DialogResult.OK) await StartVideoAsync(options.CaptureWindow, options.Audio);
        };
        fastLauncher.Controls.Add(videoButton);
        Controls.Add(fastLauncher); fastLauncher.BringToFront();
    }

    private static Button FastButton(string name, int index) => new()
    {
        Text = Localization.CurrentLanguage == "th" ? new[] { "พื้นที่", "หน้าต่าง", "ภาพยาว", "วิดีโอ" }[index] : name, Name = "fast" + name, Location = new Point(12 + index * 92, 50), Size = new Size(86, 66),
        FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.FromArgb(20, 35, 47),
        Font = new Font("Segoe UI", 10), ImageAlign = ContentAlignment.TopCenter, TextAlign = ContentAlignment.BottomCenter,
        Image = File.Exists(Path.Combine(AppContext.BaseDirectory, "Assets", "icons", "capture-" + index + ".png"))
            ? Image.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "icons", "capture-" + index + ".png")) : null
    };

    private async Task PrepareNextEditorAsync()
    {
        if (warmEditor is not null || IsDisposed || exitApproved || editors.Any(editor => editor.IsLoadingCapture)) return;
        await assetsPrepared;
        if (warmEditor is not null || IsDisposed || exitApproved || editors.Any(editor => editor.IsLoadingCapture)) return;
        var editor = new EditorHubForm(ShowLauncher, () => editors.ToArray(), ChangeLanguage);
        warmEditor = editor;
        try { await editor.PrepareWarmAsync(); }
        catch (Exception error) when (error is InvalidOperationException or ObjectDisposedException or System.Runtime.InteropServices.COMException or TimeoutException)
        {
            if (warmEditor == editor) { warmEditor = null; editor.Dispose(); }
        }
    }
}
