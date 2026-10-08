namespace SnapCraft;

internal sealed class HistoryForm : Form
{
    private readonly ListBox list = new() { Dock = DockStyle.Fill, DisplayMember = "Label" };
    private readonly CheckBox enabled = new() { Text = "Keep history / recover edits", AutoSize = true };
    private readonly ComboBox days = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
    private readonly AppSettings settings = AppSettings.Load();
    private bool restoring;
    private bool working;
    private sealed record Entry(string Label, string Path);
    public string? SelectedPath => (list.SelectedItem as Entry)?.Path;
    public HistoryForm()
    {
        Font = new Font("Segoe UI", 10); BackColor = Color.White;
        Text = AppInfo.ProductName + " | " + (Localization.CurrentLanguage == "th" ? "ประวัติและกู้คืนงาน" : "History and recovery");
        Size = new Size(670, 440); MinimumSize = new Size(470, 300); StartPosition = FormStartPosition.CenterParent;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };
        var open = new Button { Text = "Open", DialogResult = DialogResult.None, AutoSize = true };
        var delete = new Button { Text = "Delete", AutoSize = true }; var clear = new Button { Text = "Clear all", AutoSize = true };
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(6) };
        if (Localization.CurrentLanguage == "th") { enabled.Text = "เก็บประวัติ / กู้คืนงาน"; open.Text = "เปิด"; delete.Text = "ลบ"; clear.Text = "ลบทั้งหมด"; }
        days.Items.AddRange(Localization.CurrentLanguage == "th" ? new object[] { "1 วัน", "7 วัน", "30 วัน" } : new object[] { "1 day", "7 days", "30 days" }); days.SelectedIndex = settings.HistoryDays == 1 ? 0 : settings.HistoryDays == 30 ? 2 : 1;
        enabled.Checked = settings.HistoryEnabled;
        enabled.CheckedChanged += (_, _) =>
        {
            if (restoring) return;
            if (enabled.Checked && MessageBox.Show(this, Localization.CurrentLanguage == "th" ? "ประวัติจะเก็บภาพต้นฉบับและงานที่แก้ไขไว้ในเครื่อง รวมถึงข้อมูลที่ถูกเบลอหรือปิดทับ เปิดใช้งานหรือไม่?" : "History stores original images and editable work locally, including information under blur or covering shapes. Enable it?", AppInfo.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) { enabled.Checked = false; return; }
            settings.HistoryEnabled = enabled.Checked; SaveSettings();
        };
        days.SelectedIndexChanged += (_, _) => { if (restoring) return; settings.HistoryDays = new[] { 1, 7, 30 }[days.SelectedIndex]; SaveSettings(); };
        open.Click += (_, _) => { if (SelectedPath is not null) { DialogResult = DialogResult.OK; Close(); } };
        list.DoubleClick += (_, _) => open.PerformClick();
        delete.Click += async (_, _) => { if (SelectedPath is null || working) return; working = true; try { await CaptureHistory.DeleteAsync(SelectedPath); RefreshList(); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { MessageBox.Show(this, error.Message); } finally { working = false; } };
        clear.Click += async (_, _) =>
        {
            if (working) return;
            if (MessageBox.Show(this, Localization.CurrentLanguage == "th" ? "ลบประวัติทั้งหมดและปิดการเก็บประวัติ?" : "Clear all history and disable recording history?", AppInfo.ProductName, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            enabled.Checked = false; if (CaptureHistory.Enabled) return;
            working = true; try { await CaptureHistory.ClearAsync(); RefreshList(); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { MessageBox.Show(this, error.Message); } finally { working = false; }
        };
        FormClosing += (_, e) => { if (working) e.Cancel = true; };
        bar.Controls.AddRange(new Control[] { enabled, days }); bottom.Controls.AddRange(new Control[] { open, delete, clear });
        Controls.Add(list); Controls.Add(bar); Controls.Add(bottom); RefreshList();
    }
    private void SaveSettings()
    {
        try { var current = AppSettings.Load(); current.HistoryEnabled = settings.HistoryEnabled; current.HistoryDays = settings.HistoryDays; current.Save(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            var saved = AppSettings.Load(); restoring = true;
            try { settings.HistoryEnabled = saved.HistoryEnabled; settings.HistoryDays = saved.HistoryDays; enabled.Checked = saved.HistoryEnabled; days.SelectedIndex = saved.HistoryDays == 1 ? 0 : saved.HistoryDays == 30 ? 2 : 1; }
            finally { restoring = false; }
            MessageBox.Show(this, error.Message, AppInfo.ProductName);
        }
    }
    private void RefreshList()
    {
        list.Items.Clear();
        foreach (var entry in CaptureHistory.Entries()) list.Items.Add(new Entry($"{entry.LastWriteTime:yyyy-MM-dd HH:mm:ss}   {entry.Extension}   {entry.Length / 1048576d:F1} MB", entry.FullName));
    }
}
