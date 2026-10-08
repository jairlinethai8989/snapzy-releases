using System.Drawing.Imaging;
using System.Text.RegularExpressions;

namespace SnapCraft;

internal static class SafeRedaction
{
    private static readonly Regex sensitive = new(@"[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}|(?<!\d)(?:\d[ -]?){10,16}(?!\d)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    internal static bool Suggest(string text) => sensitive.IsMatch(text);
    internal static byte[] Apply(Bitmap image, IEnumerable<Rectangle> regions)
    {
        using var result = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(result)) { graphics.DrawImageUnscaled(image, 0, 0); foreach (var region in regions) graphics.FillRectangle(Brushes.Black, Rectangle.Intersect(region, new Rectangle(Point.Empty, image.Size))); }
        using var output = new MemoryStream(); result.Save(output, ImageFormat.Png); return output.ToArray();
    }
}
internal sealed class RedactionForm : Form
{
    private readonly byte[] png;
    private readonly Bitmap image;
    private readonly ImageReviewCanvas canvas;
    private readonly CheckedListBox suggestions = new() { Dock = DockStyle.Right, Width = 260, CheckOnClick = true };
    private readonly List<Rectangle> manual = new();
    private readonly List<RecognizedLine> found = new();
    private bool reading;
    public byte[]? Result { get; private set; }
    public RedactionForm(byte[] png)
    {
        this.png = png; using var stream = new MemoryStream(png); using var decoded = new Bitmap(stream); image = new Bitmap(decoded);
        Font = new Font("Segoe UI", 10); BackColor = Color.White;
        Text = AppInfo.ProductName + " | " + (Localization.CurrentLanguage == "th" ? "ปิดข้อมูลก่อนแชร์" : "Safe sharing"); Size = new Size(1120, 740); MinimumSize = new Size(700, 450); StartPosition = FormStartPosition.CenterParent;
        canvas = new(image) { SelectRegions = true };
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, MinimumSize = new Size(0, 42), Padding = new Padding(6) };
        var language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 }; language.Items.AddRange(OcrService.Languages()); if (language.Items.Count > 0) language.SelectedIndex = 0;
        var scan = new Button { Text = "Suggest", AutoSize = true, Enabled = language.Items.Count > 0 };
        var undo = new Button { Text = "Undo region", AutoSize = true }; var create = new Button { Text = "Create safe image", AutoSize = true }; var actual = new CheckBox { Text = "100%", AutoSize = true };
        if (Localization.CurrentLanguage == "th") { scan.Text = "ค้นหาข้อมูล"; undo.Text = "ลบกรอบล่าสุด"; create.Text = "สร้างภาพปิดข้อมูล"; }
        canvas.RegionsChanged += () => { manual.Clear(); var suggested = suggestions.CheckedIndices.Cast<int>().Select(i => found[i].Bounds).ToHashSet(); manual.AddRange(canvas.Regions.Where(r => !suggested.Contains(r))); };
        actual.CheckedChanged += (_, _) => { canvas.Fit = !actual.Checked; canvas.RefreshView(); };
        undo.Click += (_, _) => { if (manual.Count > 0) manual.RemoveAt(manual.Count - 1); RefreshRegions(); };
        suggestions.ItemCheck += (_, _) => BeginInvoke(new Action(RefreshRegions));
        scan.Click += async (_, _) =>
        {
            reading = true; scan.Enabled = false; create.Enabled = false;
            try
            {
                if (!await OcrModels.EnsureAsync(this, (string)language.SelectedItem!)) return;
                var lines = await OcrService.ReadAsync(png, (string)language.SelectedItem!); found.Clear(); suggestions.Items.Clear();
                foreach (var line in lines.Where(l => SafeRedaction.Suggest(l.Text))) { var bounds = Rectangle.Inflate(line.Bounds, 3, 3); found.Add(line with { Bounds = bounds }); suggestions.Items.Add(line.Text, false); }
                if (found.Count == 0) MessageBox.Show(this, Localization.CurrentLanguage == "th" ? "ไม่พบรายการที่ระบบแนะนำ คุณยังลากกรอบปิดข้อมูลเองได้" : "No suggestions found. You can still mark regions manually.", AppInfo.ProductName);
                RefreshRegions();
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, AppInfo.ProductName); }
            finally { reading = false; scan.Enabled = true; create.Enabled = true; }
        };
        create.Click += (_, _) =>
        {
            if (canvas.Regions.Count == 0) return;
            if (MessageBox.Show(this, Localization.CurrentLanguage == "th" ? "สร้างภาพใหม่ที่ปิดข้อมูลตามกรอบทั้งหมด? กรุณาตรวจส่วนที่ต้องปิดให้ครบก่อนแชร์" : "Create a flattened image with all marked regions removed? Review the image before sharing.", AppInfo.ProductName, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            Result = SafeRedaction.Apply(image, canvas.Regions); DialogResult = DialogResult.OK; Close();
        };
        FormClosing += (_, e) => { if (reading) e.Cancel = true; };
        bar.Controls.AddRange(new Control[] { language, scan, undo, actual, create }); Controls.Add(canvas); Controls.Add(suggestions); Controls.Add(bar);
    }
    private void RefreshRegions() { canvas.Regions.Clear(); canvas.Regions.AddRange(manual); canvas.Regions.AddRange(suggestions.CheckedIndices.Cast<int>().Where(i => i < found.Count).Select(i => found[i].Bounds)); canvas.Invalidate(); }
    protected override void Dispose(bool disposing) { if (disposing) image.Dispose(); base.Dispose(disposing); }
}
