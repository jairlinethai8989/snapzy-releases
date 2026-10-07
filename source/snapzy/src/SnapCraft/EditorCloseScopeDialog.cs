namespace SnapCraft;

internal sealed class EditorCloseScopeDialog : Form
{
    public EditorCloseScopeDialog(int imageCount)
    {
        Name = "closeEditorScopeDialog";
        Text = AppInfo.ProductName;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(420, 194);
        BackColor = Color.White; Font = new Font("Segoe UI", 10);
        Controls.Add(new Label { Text = string.Format(Localization.Translate("มีภาพเปิดอยู่ {0} ภาพ ต้องการปิดแบบใด?"), imageCount), Location = new Point(16, 16), Size = new Size(388, 44) });
        Controls.Add(new Label { Text = Localization.Translate("ภาพที่ยังไม่ได้เก็บจะถามก่อนปิด"), ForeColor = Color.DimGray, Location = new Point(16, 62), Size = new Size(388, 28) });
        var current = Choice("closeCurrentWindow", "ปิดหน้าต่างนี้", DialogResult.Yes, 16, 98, 188);
        Choice("closeAllImages", "ปิดภาพทั้งหมด", DialogResult.No, 216, 98, 188);
        var cancel = Choice("cancelScope", "ยกเลิก", DialogResult.Cancel, 288, 146, 116);
        AcceptButton = current; CancelButton = cancel;
    }
    private Button Choice(string name, string label, DialogResult result, int left, int top, int width)
    {
        var button = new Button { Name = name, Text = Localization.Translate(label), DialogResult = result, Location = new Point(left, top), Size = new Size(width, 32), FlatStyle = FlatStyle.Flat, BackColor = Color.White };
        button.FlatAppearance.BorderColor = Color.FromArgb(215, 225, 239);
        Controls.Add(button); return button;
    }
}
