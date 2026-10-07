namespace SnapCraft;

internal sealed class CombineImagesDialog : Form
{
    private readonly CheckedListBox images = new() { Dock = DockStyle.Fill, CheckOnClick = true, BorderStyle = BorderStyle.FixedSingle };
    private readonly ComboBox layout = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly NumericUpDown gap = new() { Minimum = 0, Maximum = 500, Value = 16, Width = 64 };
    public int[] SelectedIndices => images.CheckedIndices.Cast<int>().ToArray();
    public string Arrangement => new[] { "vertical", "horizontal", "free" }[layout.SelectedIndex];
    public int Gap => (int)gap.Value;

    public CombineImagesDialog(string[] labels, bool append)
    {
        Text = append ? "SnapZy | เพิ่มภาพจากแท็บ" : "SnapZy | รวมภาพ";
        Font = new Font("Segoe UI", 10); BackColor = Color.White;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(420, 365); Padding = new Padding(16);
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.Controls.Add(new Label { Text = "เลือกภาพตามลำดับแท็บ", AutoSize = true }, 0, 0);
        for (var i = 0; i < labels.Length; i++) images.Items.Add(labels[i], true);
        table.Controls.Add(images, 0, 1);
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0) };
        layout.Items.AddRange(new object[] { "แนวตั้ง", "แนวนอน", "จัดเอง" }); layout.SelectedIndex = 0;
        options.Controls.Add(layout); options.Controls.Add(new Label { Text = "ระยะห่าง", AutoSize = true, Padding = new Padding(8, 5, 0, 0) }); options.Controls.Add(gap);
        table.Controls.Add(options, 0, 2);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = "ยกเลิก", DialogResult = DialogResult.Cancel, AutoSize = true };
        var confirm = new Button { Text = append ? "เพิ่มภาพ" : "รวมภาพ", AutoSize = true };
        confirm.Click += (_, _) =>
        {
            if (images.CheckedItems.Count == 0) { MessageBox.Show(this, "เลือกอย่างน้อยหนึ่งภาพ", "SnapZy"); return; }
            DialogResult = DialogResult.OK;
        };
        actions.Controls.Add(cancel); actions.Controls.Add(confirm); table.Controls.Add(actions, 0, 3);
        Controls.Add(table); AcceptButton = confirm; CancelButton = cancel;
    }
}
