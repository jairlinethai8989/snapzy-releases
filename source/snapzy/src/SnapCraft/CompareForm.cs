using System.Drawing.Imaging;

namespace SnapCraft;

internal sealed class CompareForm : Form
{
    private readonly Bitmap before;
    private readonly Bitmap after;
    private readonly Bitmap backdrop;
    private Bitmap? difference;
    private bool processing;
    public CompareForm(byte[] beforePng, byte[] afterPng)
    {
        before = Decode(beforePng); after = Decode(afterPng);
        Font = new Font("Segoe UI", 10); BackColor = Color.White;
        backdrop = new Bitmap(Math.Max(before.Width, after.Width), Math.Max(before.Height, after.Height));
        using (var g = Graphics.FromImage(backdrop)) { g.Clear(Color.White); g.DrawImageUnscaled(before, 0, 0); }
        Text = AppInfo.ProductName + " | " + (Localization.CurrentLanguage == "th" ? "เปรียบเทียบก่อน / หลัง" : "Before / after"); Size = new Size(1120, 760); MinimumSize = new Size(700, 450); StartPosition = FormStartPosition.CenterParent;
        var canvas = new ImageReviewCanvas(backdrop) { Second = after };
        var slider = new TrackBar { Minimum = 0, Maximum = 100, Value = 50, Width = 220, Height = 30, TickStyle = TickStyle.None };
        var diff = new CheckBox { Text = "Differences", AutoSize = true }; var threshold = new NumericUpDown { Minimum = 0, Maximum = 100, Value = 16, Width = 55 };
        var actual = new CheckBox { Text = "100%", AutoSize = true }; var save = new Button { Text = "Save PNG", AutoSize = true };
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, MinimumSize = new Size(0, 44), Padding = new Padding(6) };
        if (Localization.CurrentLanguage == "th") { diff.Text = "ส่วนที่เปลี่ยน"; save.Text = "บันทึก PNG"; }
        slider.ValueChanged += (_, _) => { canvas.Split = slider.Value / 100d; canvas.Invalidate(); };
        actual.CheckedChanged += (_, _) => { canvas.Fit = !actual.Checked; canvas.RefreshView(); };
        async Task RefreshDifference()
        {
            if (processing) return; processing = true; diff.Enabled = false; threshold.Enabled = false; save.Enabled = false;
            try
            {
                if (diff.Checked) { var limit = (int)threshold.Value; var next = await Task.Run(() => ImageDifference.Render(before, after, limit)); difference?.Dispose(); difference = next; canvas.Second = difference; canvas.Split = 1; slider.Enabled = false; }
                else { canvas.Second = after; canvas.Split = slider.Value / 100d; slider.Enabled = true; } canvas.Invalidate();
            }
            catch (Exception error) { diff.Checked = false; canvas.Second = after; canvas.Split = slider.Value / 100d; slider.Enabled = true; canvas.Invalidate(); MessageBox.Show(this, error.Message, AppInfo.ProductName); }
            finally { processing = false; diff.Enabled = true; threshold.Enabled = true; save.Enabled = true; }
        }
        diff.CheckedChanged += async (_, _) => await RefreshDifference(); threshold.ValueChanged += async (_, _) => { if (diff.Checked) await RefreshDifference(); };
        save.Click += async (_, _) =>
        {
            using var dialog = new SaveFileDialog { Filter = "PNG image|*.png", FileName = "comparison.png", AddExtension = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                using var result = new Bitmap(Math.Max(before.Width, after.Width), Math.Max(before.Height, after.Height));
                using (var g = Graphics.FromImage(result)) { g.Clear(Color.White); g.DrawImageUnscaled(before, 0, 0); g.SetClip(new Rectangle(0, 0, (int)(result.Width * canvas.Split), result.Height)); g.DrawImageUnscaled(diff.Checked ? difference! : after, 0, 0); }
                using var bytes = new MemoryStream(); result.Save(bytes, ImageFormat.Png); await EditorHubForm.WritePngAsync(dialog.FileName, bytes.ToArray());
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, AppInfo.ProductName); }
        };
        FormClosing += (_, e) => { if (processing) e.Cancel = true; };
        bar.Controls.AddRange(new Control[] { slider, diff, new Label { Text = Localization.CurrentLanguage == "th" ? "ค่าความต่าง" : "Threshold", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }, threshold, actual, save }); Controls.Add(canvas); Controls.Add(bar);
    }
    private static Bitmap Decode(byte[] png) { using var stream = new MemoryStream(png); using var image = new Bitmap(stream); return new Bitmap(image); }
    protected override void Dispose(bool disposing) { if (disposing) { before.Dispose(); after.Dispose(); backdrop.Dispose(); difference?.Dispose(); } base.Dispose(disposing); }
}
