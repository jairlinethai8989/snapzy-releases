namespace SnapCraft;

internal sealed class ScrollReviewForm : Form
{
    private readonly Bitmap image;
    public ScrollReviewForm(byte[] png, ScrollCaptureAudit audit)
    {
        using var stream = new MemoryStream(png); using var source = new Bitmap(stream); image = new Bitmap(source);
        Font = new Font("Segoe UI", 10); BackColor = Color.White;
        Text = AppInfo.ProductName + " | " + (Localization.CurrentLanguage == "th" ? "ตรวจรอยต่อภาพยาว" : "Scroll join review"); Size = new Size(1050, 760); StartPosition = FormStartPosition.CenterParent;
        var canvas = new ImageReviewCanvas(image); var list = new ListBox { Dock = DockStyle.Left, Width = 170 };
        var thai = Localization.CurrentLanguage == "th";
        foreach (var y in audit.Joins) list.Items.Add($"{(thai ? "รอยต่อ" : "Join")} {list.Items.Count + 1}: {y:N0} px");
        list.SelectedIndexChanged += (_, _) => { if (list.SelectedIndex >= 0) { canvas.MarkerY = audit.Joins[list.SelectedIndex]; canvas.ScrollTo(canvas.MarkerY); } };
        var status = new Label { Dock = DockStyle.Bottom, Height = 28, Text = thai ? $"ภาพต้นฉบับ | เฟรมปฏิเสธ: {audit.RejectedFrames}   ไม่นิ่ง: {audit.UnstableFrames}   ถึงขีดจำกัด: {audit.ReachedLimit}" : $"Original capture | Rejected frames: {audit.RejectedFrames}   Unstable: {audit.UnstableFrames}   Limit: {audit.ReachedLimit}", TextAlign = ContentAlignment.MiddleLeft };
        var actual = new CheckBox { Text = "100%", Dock = DockStyle.Top, Height = 28 }; actual.CheckedChanged += (_, _) => { canvas.Fit = !actual.Checked; canvas.RefreshView(); };
        Controls.Add(canvas); Controls.Add(list); Controls.Add(status); Controls.Add(actual);
    }
    protected override void Dispose(bool disposing) { if (disposing) image.Dispose(); base.Dispose(disposing); }
}
