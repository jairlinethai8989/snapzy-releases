Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

function New-SetupOptionsForm([System.Collections.IDictionary]$Product, [string]$Language = $Product.Language) {
    $form = New-Object System.Windows.Forms.Form
    $form.Text = "$($Product.Name) Setup"
    $form.ClientSize = New-Object System.Drawing.Size(460, 290)
    $form.StartPosition = 'CenterScreen'
    $form.FormBorderStyle = 'FixedDialog'
    $form.MaximizeBox = $false
    $form.MinimizeBox = $false
    $form.Font = New-Object System.Drawing.Font('Segoe UI', 10)
    $label = New-Object System.Windows.Forms.Label
    $label.Name = 'product-label'
    $label.Text = "Install $($Product.Name) for this Windows account"
    $label.Location = New-Object System.Drawing.Point(20, 20)
    $label.AutoSize = $true
    $form.Controls.Add($label)
    foreach ($option in @(@('shortcuts', 'Create Desktop and Start menu shortcuts', 92), @('launch', "Open $($Product.Name) after setup closes", 130), @('startup', 'Start with Windows in the system tray', 168))) {
        $check = New-Object System.Windows.Forms.CheckBox
        $check.Name = $option[0]
        $check.Text = $option[1]
        $check.Checked = $true
        $check.AutoSize = $true
        $check.Location = New-Object System.Drawing.Point(20, $option[2])
        $form.Controls.Add($check)
    }
    $languageLabel = New-Object System.Windows.Forms.Label
    $languageLabel.Text = 'Language / ภาษา'
    $languageLabel.Location = New-Object System.Drawing.Point(20, 57)
    $languageLabel.AutoSize = $true
    $form.Controls.Add($languageLabel)
    $languagePicker = New-Object System.Windows.Forms.ComboBox
    $languagePicker.Name = 'language'
    $languagePicker.DropDownStyle = 'DropDownList'
    [void]$languagePicker.Items.Add('English')
    [void]$languagePicker.Items.Add('ไทย')
    $languagePicker.SelectedIndex = if ($Language -eq 'th') { 1 } else { 0 }
    $languagePicker.Location = New-Object System.Drawing.Point(150, 53)
    $languagePicker.Size = New-Object System.Drawing.Size(190, 28)
    $form.Controls.Add($languagePicker)
    $install = New-Object System.Windows.Forms.Button
    $install.Name = 'install'
    $install.Text = 'Install'
    $install.Location = New-Object System.Drawing.Point(220, 230)
    $install.Size = New-Object System.Drawing.Size(85, 32)
    $install.DialogResult = 'OK'
    $cancel = New-Object System.Windows.Forms.Button
    $cancel.Name = 'cancel'
    $cancel.Text = 'Cancel'
    $cancel.Location = New-Object System.Drawing.Point(315, 230)
    $cancel.Size = $install.Size
    $cancel.DialogResult = 'Cancel'
    $form.Controls.AddRange(@($install, $cancel))
    $form.AcceptButton = $install
    $form.CancelButton = $cancel
    $applyLanguage = ({
        $isThai = $languagePicker.SelectedIndex -eq 1
        if ($isThai) {
            $form.Text = "ติดตั้ง $($Product.Name)"
            $label.Text = "ติดตั้ง $($Product.Name) สำหรับบัญชี Windows นี้"
            $form.Controls['shortcuts'].Text = 'สร้างทางลัดบนเดสก์ท็อปและเมนูเริ่ม'
            $form.Controls['launch'].Text = "เปิด $($Product.Name) หลังปิดหน้าติดตั้ง"
            $form.Controls['startup'].Text = 'เริ่มพร้อม Windows และพักในถาดระบบ'
            $install.Text = 'ติดตั้ง'
            $cancel.Text = 'ยกเลิก'
        } else {
            $form.Text = "$($Product.Name) Setup"
            $label.Text = "Install $($Product.Name) for this Windows account"
            $form.Controls['shortcuts'].Text = 'Create Desktop and Start menu shortcuts'
            $form.Controls['launch'].Text = "Open $($Product.Name) after setup closes"
            $form.Controls['startup'].Text = 'Start with Windows in the system tray'
            $install.Text = 'Install'
            $cancel.Text = 'Cancel'
        }
    }).GetNewClosure()
    $languagePicker.add_SelectedIndexChanged($applyLanguage)
    & $applyLanguage
    return $form
}
