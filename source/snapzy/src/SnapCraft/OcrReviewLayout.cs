namespace SnapCraft;

internal sealed partial class OcrReviewForm
{
    private static readonly Color Ink = Color.FromArgb(23, 32, 29);
    private static readonly Color Muted = Color.FromArgb(104, 116, 111);
    private static readonly Color Line = Color.FromArgb(217, 225, 221);
    private static readonly Color Green = Color.FromArgb(18, 108, 83);
    private readonly List<IDisposable> visuals = new();
    private Image copyIcon = null!, copiedIcon = null!;

    private void BuildLayout()
    {
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        Text = AppInfo.ProductName + " | OCR"; ClientSize = new Size(704, 480); MinimumSize = new Size(500, 380);
        StartPosition = FormStartPosition.CenterParent; BackColor = Color.FromArgb(242, 245, 243); ForeColor = Ink;
        Font = Own(new Font("Segoe UI", 10));
        Icon = Own(new Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "icons", ProductProfile.Current.IconFile)));
        copyIcon = LoadIcon("ocr-copy.png"); copiedIcon = LoadIcon("ocr-done.png");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 10, 16, 10), ColumnCount = 1, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var height in new[] { 62f, 72f, 44f }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.Controls.Add(new PictureBox { Name = "ocrScanIcon", Image = LoadIcon("ocr-scan.png"), SizeMode = PictureBoxSizeMode.CenterImage, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0) }, 0, 0);
        var titles = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 7, 0, 7) };
        titles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        titles.RowStyles.Add(new RowStyle(SizeType.Percent, 60)); titles.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        titles.Controls.Add(new Label { Name = "ocrHeading", Text = Ui("Scan Text", "อ่านข้อความ OCR"), Font = Own(new Font("Segoe UI", 14, FontStyle.Bold)), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty }, 0, 0);
        titles.Controls.Add(new Label { Text = $"{AppInfo.ProductName}  v{AppInfo.Version}", ForeColor = Muted, Font = Own(new Font("Segoe UI", 9)), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty }, 0, 1);
        header.Controls.Add(titles, 1, 0); layout.Controls.Add(header, 0, 0);

        var tools = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3, Margin = Padding.Empty };
        tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); tools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160)); tools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136));
        tools.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); tools.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        tools.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tools.Controls.Add(Caption(Ui("Language", "ภาษา")), 0, 0); tools.Controls.Add(Caption(Ui("Output", "รูปแบบ")), 1, 0);
        language.Margin = new Padding(0, 4, 12, 0); tools.Controls.Add(language, 0, 1);
        var modes = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 0, 8, 0) };
        StyleMode(plain, Ui("Text", "ข้อความ"), 66); StyleMode(table, Ui("Table", "ตาราง"), 82);
        modes.Controls.AddRange(new Control[] { plain, table }); tools.Controls.Add(modes, 1, 1);
        StyleButton(read, Ui("Read text", "อ่านข้อความ"), 132); read.Image = LoadIcon("ocr-read.png"); read.BackColor = Green; read.ForeColor = Color.White; read.FlatAppearance.BorderColor = Green;
        read.FlatAppearance.MouseOverBackColor = Color.FromArgb(14, 89, 68);
        read.Dock = DockStyle.Fill; read.Margin = new Padding(4, 0, 0, 4); tools.Controls.Add(read, 2, 1); layout.Controls.Add(tools, 0, 1);
        tips.SetToolTip(read, Ui("Read the selected image", "อ่านข้อความจากภาพที่เลือก"));
        tips.SetToolTip(table, Ui("Tab-separated table (TSV)", "ตารางแยกคอลัมน์ด้วยแท็บ (TSV)"));

        var result = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        result.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        result.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); result.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124));
        result.Controls.Add(Caption(Ui("Result", "ผลลัพธ์")), 0, 0);
        StyleButton(copy, Ui("Copy", "คัดลอก"), 120); copy.Image = copyIcon; copy.Anchor = AnchorStyles.Right; result.Controls.Add(copy, 1, 0);
        tips.SetToolTip(copy, Ui("Copy the edited result", "คัดลอกผลลัพธ์ที่แก้ไขแล้ว")); layout.Controls.Add(result, 0, 2);
        var output = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(10), Margin = Padding.Empty };
        text.Font = Own(new Font("Segoe UI", 11)); text.BackColor = Color.White; text.ForeColor = Ink; text.AccessibleName = Ui("OCR result", "ผลลัพธ์ OCR");
        output.Controls.Add(text); layout.Controls.Add(output, 0, 3);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = Padding.Empty };
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        progress.Anchor = AnchorStyles.Left; progress.Margin = new Padding(0, 0, 8, 0); footer.Controls.Add(progress, 0, 0);
        status.Font = Own(new Font("Segoe UI", 9)); status.Margin = Padding.Empty; footer.Controls.Add(status, 1, 0);
        count.Font = Own(new Font("Segoe UI", 9)); count.ForeColor = Muted; count.Margin = new Padding(8, 0, 10, 0); footer.Controls.Add(count, 2, 0);
        StyleButton(closeButton, Ui("Close", "ปิด"), 86); closeButton.Image = LoadIcon("close.png"); closeButton.Anchor = AnchorStyles.Right; footer.Controls.Add(closeButton, 3, 0);
        layout.Controls.Add(footer, 0, 4); Controls.Add(layout);
    }

    private Label Caption(string value) => new() { Text = value, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Muted, Margin = Padding.Empty };
    private static void StyleButton(Button button, string label, int width)
    {
        button.Text = label; button.Size = new Size(width, 34); button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Line; button.FlatAppearance.MouseOverBackColor = Color.FromArgb(226, 248, 239);
        button.BackColor = Color.White; button.ForeColor = Ink; button.TextImageRelation = TextImageRelation.ImageBeforeText; button.ImageAlign = ContentAlignment.MiddleLeft;
        button.Padding = new Padding(7, 0, 4, 0); button.Margin = Padding.Empty;
    }
    private static void StyleMode(RadioButton button, string label, int width)
    {
        button.Text = label; button.Size = new Size(width, 34); button.Margin = Padding.Empty; button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Line; button.TextAlign = ContentAlignment.MiddleCenter;
    }
    private void StyleModes()
    {
        foreach (var button in new[] { plain, table }) { button.BackColor = button.Checked ? Color.FromArgb(226, 248, 239) : Color.White; button.ForeColor = button.Checked ? Green : Muted; }
    }
    private T Own<T>(T value) where T : IDisposable { visuals.Add(value); return value; }
    private Image LoadIcon(string name) => Own(Image.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "icons", name)));
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { tips.Dispose(); foreach (var visual in visuals) visual.Dispose(); visuals.Clear(); }
    }
}
