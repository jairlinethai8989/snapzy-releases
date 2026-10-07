namespace SnapCraft;

internal sealed class ScrollProgressForm : Form
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
    private readonly Label label = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Width = 224, Dock = DockStyle.Left };
    private readonly Button finish = new() { Text = "เสร็จ", Width = 57, Dock = DockStyle.Right };
    private readonly Button manual = new() { Text = "เลื่อนเอง", Width = 82, Dock = DockStyle.Right };
    private readonly Button cancel = new() { Text = "ยกเลิก", Width = 65, Dock = DockStyle.Right };
    public bool FinishRequested { get; private set; }
    public bool CancelRequested { get; private set; }
    public bool Manual { get; private set; }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x08000000;
            return parameters;
        }
    }

    public ScrollProgressForm(Rectangle target)
    {
        Text = "SnapZy | ภาพยาว";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(438, 48);
        BackColor = Color.FromArgb(24, 34, 31);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9);
        label.Padding = new Padding(10, 0, 0, 0);
        label.Text = "กำลังจับภาพยาว... Esc เพื่อจบ";
        foreach (var button in new[] { finish, manual, cancel })
        {
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = Color.White;
            button.ForeColor = Color.FromArgb(24, 34, 31);
            button.Margin = new Padding(3);
        }
        finish.Click += (_, _) => FinishRequested = true;
        cancel.Click += (_, _) => CancelRequested = true;
        manual.Click += (_, _) => SetManual(!Manual);
        Controls.Add(label);
        Controls.Add(cancel);
        Controls.Add(finish);
        Controls.Add(manual);
        var screen = Screen.FromRectangle(target).WorkingArea;
        StartPosition = FormStartPosition.Manual;
        var candidates = new[]
        {
            new Point(screen.Right - Width - 16, screen.Top + 16),
            new Point(screen.Right - Width - 16, screen.Bottom - Height - 16),
            new Point(screen.Left + 16, screen.Top + 16),
            new Point(screen.Left + 16, screen.Bottom - Height - 16)
        };
        Location = candidates.FirstOrDefault(point => !new Rectangle(point, Size).IntersectsWith(target), candidates[0]);
        Shown += (_, _) => SetWindowDisplayAffinity(Handle, 0x11);
    }

    public void UpdateProgress(int count, int height, string? note = null)
    {
        label.Text = note ?? $"{count} ภาพ • {height:N0} px • Esc จบ";
    }

    public void SetManual(bool value)
    {
        Manual = value;
        manual.Text = Manual ? "อัตโนมัติ" : "เลื่อนเอง";
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing) CancelRequested = true;
        base.OnFormClosing(e);
    }
}
