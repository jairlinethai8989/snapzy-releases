namespace SnapCraft;

internal sealed partial class OcrReviewForm : Form
{
    private readonly byte[] image;
    private readonly ComboBox language = new() { Name = "ocrLanguage", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DropDownWidth = 240 };
    private readonly TextBox text = new() { Name = "ocrText", Multiline = true, AcceptsTab = true, AcceptsReturn = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, MaxLength = int.MaxValue };
    private readonly RadioButton plain = new() { Name = "ocrTextMode", Appearance = Appearance.Button, Checked = true };
    private readonly RadioButton table = new() { Name = "ocrTableMode", Appearance = Appearance.Button };
    private readonly Button read = new() { Name = "ocrRead" };
    private readonly Button copy = new() { Name = "ocrCopy", Enabled = false };
    private readonly Button closeButton = new() { Name = "ocrClose", DialogResult = DialogResult.Cancel };
    private readonly Label status = new() { Name = "ocrStatus", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    private readonly Label count = new() { Name = "ocrCount", AutoSize = true, Anchor = AnchorStyles.Right };
    private readonly ProgressBar progress = new() { Name = "ocrProgress", Width = 54, Height = 8, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25, Visible = false };
    private readonly ToolTip tips = new();
    private readonly bool thai = Localization.CurrentLanguage == "th";
    private string plainText = "", tableText = "";
    private bool reading, tableMode, rendering;
    public OcrReviewForm(byte[] image)
    {
        this.image = image;
        BuildLayout();
        language.DisplayMember = nameof(LanguageChoice.Label); language.ValueMember = nameof(LanguageChoice.Code);
        language.DataSource = OcrService.Languages().Select(code => new LanguageChoice(code,
            code == "th+en" ? Ui("Thai + English", "ไทย + อังกฤษ") : $"{(code.StartsWith("th") ? Ui("Thai", "ไทย") : Ui("English", "อังกฤษ"))} ({code})")).ToArray();
        read.Enabled = language.Items.Count > 0;
        SetStatus(read.Enabled ? Ui("Ready", "พร้อมใช้งาน") : Ui("OCR language unavailable", "ไม่มีชุดภาษา OCR"));
        language.SelectedIndexChanged += (_, _) => tips.SetToolTip(language, language.Text);
        read.Click += async (_, _) => await ReadAsync();
        plain.CheckedChanged += (_, _) => { if (plain.Checked) SwitchMode(false); StyleModes(); };
        table.CheckedChanged += (_, _) => { if (table.Checked) SwitchMode(true); StyleModes(); };
        text.TextChanged += (_, _) =>
        {
            if (!rendering) { if (tableMode) tableText = text.Text; else plainText = text.Text; }
            ResetCopy(); UpdateCount();
        };
        copy.Click += (_, _) => CopyResult();
        FormClosing += (_, e) => { if (reading) e.Cancel = true; };
        CancelButton = closeButton; AcceptButton = read;
        UpdateCount(); StyleModes();
    }

    private string Ui(string en, string th) => thai ? th : en;

    private async Task ReadAsync()
    {
        if (reading || language.SelectedItem is not LanguageChoice selected) return;
        SetBusy(true); SetStatus(Ui("Preparing OCR...", "กำลังเตรียม OCR..."));
        try
        {
            if (!await OcrModels.EnsureAsync(this, selected.Code)) { SetStatus(Ui("Canceled", "ยกเลิกแล้ว")); return; }
            SetStatus(Ui("Reading text...", "กำลังอ่านข้อความ..."));
            var lines = await OcrService.ReadAsync(image, selected.Code);
            plainText = string.Join(Environment.NewLine, lines.Select(line => line.Text));
            tableText = OcrService.TableText(lines);
            ShowOutput();
            SetStatus(lines.Count > 0 ? Ui("Read complete", "อ่านเรียบร้อย") : Ui("No text found", "ไม่พบข้อความ"));
        }
        catch (Exception error) { SetStatus(Ui("OCR failed", "อ่านไม่สำเร็จ"), error.Message); }
        finally { SetBusy(false); }
    }

    private void SwitchMode(bool next)
    {
        if (reading || tableMode == next) return;
        if (tableMode) tableText = text.Text; else plainText = text.Text;
        tableMode = next; ShowOutput();
    }

    private void ShowOutput()
    {
        rendering = true;
        try { text.Text = tableMode ? tableText : plainText; }
        finally { rendering = false; }
    }

    private void SetBusy(bool value)
    {
        reading = value; read.Enabled = !value && language.Items.Count > 0;
        language.Enabled = plain.Enabled = table.Enabled = closeButton.Enabled = !value;
        text.ReadOnly = value; progress.Visible = value;
        UseWaitCursor = value; ResetCopy();
    }

    private void SetStatus(string value, string? error = null)
    {
        status.Text = value; status.ForeColor = error is null ? Muted : Color.FromArgb(190, 42, 55);
        status.AccessibleDescription = error; tips.SetToolTip(status, error ?? value);
    }

    private void CopyResult()
    {
        if (reading || string.IsNullOrWhiteSpace(text.Text)) return;
        try
        {
            Clipboard.SetText(text.Text);
            copy.Text = Ui("Copied", "คัดลอกแล้ว"); copy.BackColor = Green; copy.ForeColor = Color.White; copy.Image = copiedIcon;
            copy.FlatAppearance.MouseOverBackColor = Color.FromArgb(14, 89, 68);
        }
        catch (System.Runtime.InteropServices.ExternalException error) { SetStatus(Ui("Copy failed", "คัดลอกไม่สำเร็จ"), error.Message); }
    }

    private void ResetCopy()
    {
        copy.Enabled = !reading && !string.IsNullOrWhiteSpace(text.Text);
        copy.Text = Ui("Copy", "คัดลอก"); copy.BackColor = Color.White; copy.ForeColor = Ink; copy.Image = copyIcon;
        copy.FlatAppearance.MouseOverBackColor = Color.FromArgb(226, 248, 239);
    }

    private void UpdateCount() => count.Text = Ui($"{text.TextLength:N0} characters", $"{text.TextLength:N0} ตัวอักษร");
    private sealed record LanguageChoice(string Code, string Label) { public override string ToString() => Label; }
}
