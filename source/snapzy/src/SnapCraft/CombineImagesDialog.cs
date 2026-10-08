namespace SnapCraft;

internal sealed class CombineImagesDialog : Form
{
    private readonly CheckedListBox images = new() { Dock = DockStyle.Fill, CheckOnClick = true, BorderStyle = BorderStyle.FixedSingle };
    private readonly ComboBox layout = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly NumericUpDown gap = new() { Minimum = 0, Maximum = 500, Value = 16, Width = 64 };
    public int[] SelectedIndices => images.CheckedIndices.Cast<int>().ToArray();
    public string Arrangement => new[] { "vertical", "horizontal", "free" }[layout.SelectedIndex];
    public int Gap => (int)gap.Value;

    public CombineImagesDialog(string[] labels, bool append, bool selectionOnly = false)
    {
        var thai = Localization.CurrentLanguage == "th";
        string Ui(string en, string th) => thai ? th : en;
        Text = AppInfo.ProductName + " | " + (selectionOnly ? Ui("Select images", "เลือกภาพ") : append ? Ui("Add images from tabs", "เพิ่มภาพจากแท็บ") : Ui("Combine images", "รวมภาพ"));
        Font = new Font("Segoe UI", 10); BackColor = Color.White;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(420, 365); Padding = new Padding(16);
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, selectionOnly ? 0 : 52));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.Controls.Add(new Label { Text = Ui("Select images in tab order", "เลือกภาพตามลำดับแท็บ"), AutoSize = true }, 0, 0);
        for (var i = 0; i < labels.Length; i++) images.Items.Add(labels[i], true);
        table.Controls.Add(images, 0, 1);
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0) };
        layout.Items.AddRange(new object[] { Ui("Vertical", "แนวตั้ง"), Ui("Horizontal", "แนวนอน"), Ui("Free", "จัดเอง") }); layout.SelectedIndex = 0;
        options.Visible = !selectionOnly;
        options.Controls.Add(layout); options.Controls.Add(new Label { Text = Ui("Gap", "ระยะห่าง"), AutoSize = true, Padding = new Padding(8, 5, 0, 0) }); options.Controls.Add(gap);
        table.Controls.Add(options, 0, 2);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = Ui("Cancel", "ยกเลิก"), DialogResult = DialogResult.Cancel, AutoSize = true };
        var confirm = new Button { Text = selectionOnly ? Ui("Select", "เลือก") : append ? Ui("Add images", "เพิ่มภาพ") : Ui("Combine", "รวมภาพ"), AutoSize = true };
        confirm.Click += (_, _) =>
        {
            if (images.CheckedItems.Count == 0) { MessageBox.Show(this, Ui("Choose at least one image", "เลือกอย่างน้อยหนึ่งภาพ"), AppInfo.ProductName); return; }
            DialogResult = DialogResult.OK;
        };
        actions.Controls.Add(cancel); actions.Controls.Add(confirm); table.Controls.Add(actions, 0, 3);
        Controls.Add(table); AcceptButton = confirm; CancelButton = cancel;
    }
}
