param([ValidateSet('Snapzy')][string]$ProductFlavor = 'Snapzy')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
. (Join-Path $PSScriptRoot 'product-profile.ps1')
. (Join-Path $PSScriptRoot 'setup-options.ps1')
. (Join-Path $PSScriptRoot 'upgrade-policy.ps1')
. (Join-Path $PSScriptRoot 'runtime-setup.ps1')
. (Join-Path $PSScriptRoot 'payload-install.ps1')
$product = Get-ProductProfile $ProductFlavor
$productName = $product.Name
$version = $product.Version
$app = Join-Path (Join-Path $env:LOCALAPPDATA 'Programs') (Join-Path $product.InstallFolder 'SnapCraft.exe')
$target = Split-Path $app
$reg = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$($product.UninstallKey)"
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$settingsFile = Join-Path (Join-Path $env:LOCALAPPDATA $product.DataFolder) 'settings.json'

function Show-Notice([string]$message, [string]$title = $productName) {
    [System.Windows.Forms.MessageBox]::Show($message, $title, [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
}

try {
    if (-not [Environment]::Is64BitOperatingSystem) { throw "$productName requires 64-bit Windows." }
    $build = [int](Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion').CurrentBuildNumber
    if ($build -lt 19041) { throw "$productName requires Windows 10 version 2004 or later." }
    $installedVersion = (Get-ItemProperty -Path $reg -ErrorAction SilentlyContinue).DisplayVersion
    $installedDll = Join-Path $target 'SnapCraft.dll'
    if (-not $installedVersion -and (Test-Path -LiteralPath $installedDll)) {
        $installedVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($installedDll).FileVersion
    }
    $action = Get-UpgradeAction $installedVersion $version
    if ($action -eq 'Block') { throw "$productName $installedVersion is newer than this installer ($version). Downgrade was blocked. No files were changed." }
    if ($action -ne 'Install') {
        $answer = [System.Windows.Forms.MessageBox]::Show("$productName $installedVersion is installed.`n$action to version $version?`nYour settings, shortcuts and saved work will be preserved.", "$productName update", 'OKCancel', 'Information')
        if ($answer -ne 'OK') { exit 0 }
    }
    while ($running = @(Get-Process SnapCraft -ErrorAction SilentlyContinue | Where-Object { try { [IO.Path]::GetFullPath($_.Path) -eq [IO.Path]::GetFullPath($app) } catch { $false } })) {
        $answer = [System.Windows.Forms.MessageBox]::Show("$productName is still running.`nSave your images, stop any recording, then choose Exit from the tray menu.`nClick Retry after closing $productName. Setup will not force-close your work.", "Close $productName before updating", 'RetryCancel', 'Warning')
        if ($answer -ne 'Retry') { exit 0 }
    }
    $initialLanguage = $product.Language
    try {
        $savedLanguage = (Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json).Language
        if ($savedLanguage -in @('en','th')) { $initialLanguage = $savedLanguage }
    } catch { }
    $options = New-SetupOptionsForm $product $initialLanguage
    if ($action -ne 'Install') {
        $startup = Get-ItemProperty -Path $runKey -ErrorAction SilentlyContinue
        $startupValue = $startup.PSObject.Properties[$product.StartupValue].Value
        $options.Controls['startup'].Checked = [bool]$startupValue
        $options.Controls['shortcuts'].Checked = Test-Path -LiteralPath (Join-Path ([Environment]::GetFolderPath('Desktop')) "$productName.lnk")
    }
    try {
        if ($options.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { exit 0 }
        $createShortcuts = $options.Controls['shortcuts'].Checked
        $launchAfterSetup = $options.Controls['launch'].Checked
        $startWithWindows = $options.Controls['startup'].Checked
        $language = if ($options.Controls['language'].SelectedIndex -eq 1) { 'th' } else { 'en' }
    } finally { $options.Dispose() }
    if (@(Get-Process SnapCraft -ErrorAction SilentlyContinue | Where-Object { try { [IO.Path]::GetFullPath($_.Path) -eq [IO.Path]::GetFullPath($app) } catch { $false } }).Count) { throw "$productName was reopened. Please close it and run setup again. No files were changed." }
    $archive = Join-Path $PSScriptRoot 'payload.zip'
    if (-not (Test-Path -LiteralPath $archive)) { throw 'The installer payload is missing.' }
    $legacy=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'legacy-runtime-files.json') -Raw | ConvertFrom-Json
    $runtimeStatus=Invoke-ApplicationPayload $archive $target $legacy {
        param($requirementsPath) Install-RequiredDesktopRuntime $requirementsPath $productName $language
    } {
        if (@(Get-Process SnapCraft -ErrorAction SilentlyContinue | Where-Object { try { [IO.Path]::GetFullPath($_.Path) -eq [IO.Path]::GetFullPath($app) } catch { $false } }).Count) {
            throw "$productName was reopened. Please close it and run setup again. No files were changed."
        }
    }
    if ($runtimeStatus -eq 'Canceled') { exit 0 }
    if ($runtimeStatus -eq 'RestartRequired') {
        Show-Notice (Get-RuntimeText $language 'The Microsoft runtime installer requires a Windows restart. Restart when convenient, then rerun setup. No application files were changed.' 'ตัวติดตั้ง Microsoft Runtime ต้องการให้เริ่ม Windows ใหม่ กรุณาเริ่มใหม่เมื่อสะดวก แล้วเรียกตัวติดตั้งอีกครั้ง ยังไม่ได้เปลี่ยนไฟล์โปรแกรม')
        exit 0
    }
    foreach ($obsolete in @('ScreenRecorderLib.pdb','SnapCraft.pdb','Microsoft.Web.WebView2.Core.xml','Microsoft.Web.WebView2.WinForms.xml','Microsoft.Web.WebView2.Wpf.xml','Assets\stitch.js')) {
        $file = Join-Path $target $obsolete
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file -Force }
    }
    $icon = Join-Path $target (Join-Path 'Assets\icons' $product.IconName)
    if (-not (Test-Path -LiteralPath $app)) { throw "$productName executable was not installed." }
    New-Item -ItemType Directory -Force -Path (Split-Path $settingsFile) | Out-Null
    $settings = @{}
    if (Test-Path -LiteralPath $settingsFile) {
        try {
            $savedSettings = Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json
            foreach ($property in $savedSettings.PSObject.Properties) { $settings[$property.Name] = $property.Value }
        } catch { $settings = @{} }
    }
    $settings['Language'] = $language
    $settings | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $settingsFile -Encoding UTF8
    if ($startWithWindows) {
        New-Item -Path $runKey -Force | Out-Null
        New-ItemProperty -Path $runKey -Name $product.StartupValue -Value ('"{0}" --tray' -f $app) -PropertyType String -Force | Out-Null
    } else {
        Remove-ItemProperty -Path $runKey -Name $product.StartupValue -ErrorAction SilentlyContinue
    }

    $shell = New-Object -ComObject WScript.Shell
    $startLink = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\$productName.lnk"
    $desktopLink = Join-Path ([Environment]::GetFolderPath('Desktop')) "$productName.lnk"
    foreach ($link in @($startLink, $desktopLink)) { if (Test-Path -LiteralPath $link) { Remove-Item -LiteralPath $link -Force } }
    foreach ($link in $(if ($createShortcuts) { @($startLink, $desktopLink) } else { @() })) {
        $shortcut = $shell.CreateShortcut($link)
        $shortcut.TargetPath = $app
        $shortcut.WorkingDirectory = $target
        $shortcut.IconLocation = "$icon,0"
        $shortcut.Save()
    }

    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class SnapzyShell {
    [DllImport("shell32.dll")]
    public static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}
'@ -ErrorAction SilentlyContinue
    [SnapzyShell]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
    foreach ($directory in @((Split-Path $startLink), (Split-Path $desktopLink))) {
        $oldLink = Join-Path $directory ("{0}.lnk" -f $product.InstallFolder)
        if ($oldLink -ne $startLink -and $oldLink -ne $desktopLink -and (Test-Path -LiteralPath $oldLink) -and $shell.CreateShortcut($oldLink).TargetPath -eq $app) { Remove-Item -LiteralPath $oldLink -Force }
    }

    $uninstall = Join-Path $target 'uninstall.ps1'
    $uninstallCommand = '"{0}" -NoProfile -ExecutionPolicy Bypass -File "{1}" -ProductFlavor {2}' -f (Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'), $uninstall, $ProductFlavor
    New-Item -Path $reg -Force | Out-Null
    New-ItemProperty -Path $reg -Name DisplayName -Value $productName -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $reg -Name DisplayVersion -Value $version -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $reg -Name Publisher -Value 'jairlinethai' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $reg -Name InstallLocation -Value $target -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $reg -Name UninstallString -Value $uninstallCommand -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $reg -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $reg -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null

    $missing = @()
    $webViewId = '{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}'
    $webViewKeys = @(
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\$webViewId",
        "HKCU:\Software\Microsoft\EdgeUpdate\Clients\$webViewId"
    )
    $webViewInstalled = $false
    foreach ($key in $webViewKeys) {
        $version = (Get-ItemProperty -Path $key -Name pv -ErrorAction SilentlyContinue).pv
        if ($version -and $version -ne '0.0.0.0') { $webViewInstalled = $true; break }
    }
    if (-not $webViewInstalled) {
        $missing += 'WebView2 Runtime: https://developer.microsoft.com/microsoft-edge/webview2/'
    }
    $vc = Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64' -ErrorAction SilentlyContinue
    if ($vc.Installed -ne 1) {
        $missing += 'Visual C++ Redistributable x64: https://aka.ms/vs/17/release/vc_redist.x64.exe'
    }
    if ($missing.Count) {
        Show-Notice ("$productName is installed. Install these Microsoft components before opening it:`n`n" + ($missing -join "`n`n"))
    } else {
        Show-Notice "$productName was installed successfully. Click OK to finish setup."
        if ($launchAfterSetup) {
            $helper = Join-Path $target 'launch-after-setup.ps1'
            $arguments = '-NoProfile -ExecutionPolicy Bypass -File "{0}" -SetupPid {1} -AppPath "{2}"' -f $helper, $PID, $app
            Start-Process -FilePath (Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe') -ArgumentList $arguments -WindowStyle Hidden
        }
    }
} catch {
    [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, "$productName setup failed", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
    exit 1
}
