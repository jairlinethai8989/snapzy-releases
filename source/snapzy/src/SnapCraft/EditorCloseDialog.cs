namespace SnapCraft;

internal sealed class EditorCloseDialog : Form
{
    public EditorCloseDialog(string imageName, bool video = false, int? imageCount = null)
    {
        Name = imageCount.HasValue ? "closeImagesDialog" : video ? "closeVideoDialog" : "closeImageDialog";
        Text = AppInfo.ProductName;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(400, 160);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10);
        var title = imageCount.HasValue ? Localization.Translate("บันทึกภาพทั้งหมดก่อนปิดหรือไม่?") : string.Format(Localization.Translate("บันทึก {0} ก่อนปิดหรือไม่?"), imageName);
        var detail = imageCount.HasValue ? string.Format(Localization.Translate("มีภาพที่ยังไม่ได้เก็บหรือแก้ไขค้างอยู่ {0} ภาพ"), imageCount) : Localization.Translate(video ? "คลิปนี้ยังไม่ได้คัดลอกหรือบันทึก" : "ภาพนี้ยังไม่ได้เก็บ หรือมีการแก้ไขเพิ่มเติม");
        Controls.Add(new Label { Text = title, AutoSize = false, Location = new Point(16, 16), Size = new Size(368, 36) });
        Controls.Add(new Label { Text = detail, ForeColor = Color.DimGray, AutoSize = false, Location = new Point(16, 52), Size = new Size(368, 40) });
        var save = Choice("saveClose", imageCount.HasValue ? "บันทึกทั้งหมด" : "บันทึก", DialogResult.Yes, 16);
        save.BackColor = Color.FromArgb(21, 94, 239);
        save.ForeColor = Color.White;
        Choice("discardClose", imageCount.HasValue ? "ไม่บันทึกทั้งหมด" : "ไม่บันทึก", DialogResult.No, 142);
        var cancel = Choice("cancelClose", "ยกเลิก", DialogResult.Cancel, 268);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private Button Choice(string name, string label, DialogResult result, int left)
    {
        var button = new Button { Name = name, Text = Localization.Translate(label), DialogResult = result, Location = new Point(left, 108), Size = new Size(116, 32), FlatStyle = FlatStyle.Flat, BackColor = Color.White };
        button.FlatAppearance.BorderColor = Color.FromArgb(215, 225, 239);
        Controls.Add(button);
        return button;
    }
}
