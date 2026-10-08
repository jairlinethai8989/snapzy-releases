namespace SnapCraft;

internal sealed class VideoOptionsDialog : Form
{
    private readonly CheckBox system = new() { Text = "System audio", AutoSize = true };
    private readonly CheckBox microphone = new() { Text = "Microphone", AutoSize = true };
    private readonly CheckBox window = new() { Text = "Selected window", AutoSize = true };
    public bool CaptureWindow => window.Checked;
    public VideoAudio Audio => new(system.Checked, microphone.Checked);
    public VideoOptionsDialog()
    {
        Text = AppInfo.ProductName + " | Video"; ClientSize = new Size(290, 175); BackColor = Color.White;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterParent;
        system.Location = new Point(16, 16); microphone.Location = new Point(16, 48); window.Location = new Point(16, 80);
        if (Localization.CurrentLanguage == "th") { system.Text = "เสียงจากเครื่อง"; microphone.Text = "ไมโครโฟน"; window.Text = "เลือกหน้าต่าง"; }
        var start = new Button { Text = Localization.CurrentLanguage == "th" ? "เริ่ม" : "Start", DialogResult = DialogResult.OK, Location = new Point(105, 128), Size = new Size(76, 30) };
        var cancel = new Button { Text = Localization.CurrentLanguage == "th" ? "ยกเลิก" : "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(193, 128), Size = new Size(76, 30) };
        Controls.AddRange(new Control[] { system, microphone, window, start, cancel }); AcceptButton = start; CancelButton = cancel;
    }
}
