using System.Runtime.InteropServices;

namespace SnapCraft;

internal sealed class RecordingStatusForm : Form
{
    [DllImport("user32.dll")] private static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
    private readonly Label status = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(25, 35, 48), Font = new Font("Segoe UI", 11, FontStyle.Bold) };
    private readonly Button stop = new() { Text = "หยุด", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(21, 94, 239), ForeColor = Color.White, Font = new Font("Segoe UI", 10) };
    private readonly Button cancel = new() { Text = "ยกเลิก", FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.FromArgb(62, 75, 90), Font = new Font("Segoe UI", 10) };
    private readonly Action<bool> finish;
    private bool closing;
    public string DisplayText => status.Text;
    public bool IsRecording { get; private set; }
    public event Action<int>? CountdownChanged;

    public RecordingStatusForm(Action<bool> finish)
    {
        this.finish = finish;
        Text = "SnapZy • บันทึก";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.White;
        ClientSize = new Size(328, 52);
        status.SetBounds(30, 8, 135, 34);
        stop.SetBounds(166, 10, 66, 30);
        cancel.SetBounds(238, 10, 80, 30);
        stop.FlatAppearance.BorderSize = 0;
        cancel.FlatAppearance.BorderColor = Color.FromArgb(221, 227, 234);
        stop.Enabled = false;
        Controls.AddRange(new Control[] { status, stop, cancel });
        stop.Click += (_, _) => finish(true);
        cancel.Click += (_, _) => finish(false);
        FormClosing += (_, e) => { if (!closing) { e.Cancel = true; finish(false); } };
        var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(screen.Left + (screen.Width - Width) / 2, screen.Top + 12);
    }

    protected override bool ShowWithoutActivation => true;
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        SetWindowDisplayAffinity(Handle, 0x11);
    }

    public async Task CountdownAsync(CancellationToken token)
    {
        for (var number = 3; number >= 1; number--)
        {
            token.ThrowIfCancellationRequested();
            status.Text = $"เตรียมบันทึก  {number}";
            CountdownChanged?.Invoke(number);
            await Task.Delay(1000, token);
        }
        status.Text = "กำลังเริ่มบันทึก…";
    }

    public void BeginRecording()
    {
        IsRecording = true;
        stop.Enabled = true;
        status.Text = "REC  00:00:00";
        Invalidate();
    }

    public void UpdateElapsed(TimeSpan elapsed) => status.Text = "REC  " + RecordingPolicy.ElapsedText(elapsed);
    public void Finalizing() { stop.Enabled = false; cancel.Enabled = false; status.Text = "กำลังบันทึก…"; }
    public void Dismiss() { closing = true; Close(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var brush = new SolidBrush(IsRecording ? Color.FromArgb(239, 51, 64) : Color.FromArgb(21, 94, 239));
        e.Graphics.FillEllipse(brush, 13, 23, 9, 9);
    }
}
