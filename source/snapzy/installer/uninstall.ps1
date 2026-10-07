param([ValidateSet('Snapzy')][string]$ProductFlavor = 'Snapzy')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
. (Join-Path $PSScriptRoot 'product-profile.ps1')
$product = Get-ProductProfile $ProductFlavor
$productName = $product.Name
try {
    $base = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs'))
    $target = [IO.Path]::GetFullPath((Join-Path $base $product.InstallFolder))
    if (-not $target.StartsWith(($base.TrimEnd('\') + '\'), [StringComparison]::OrdinalIgnoreCase)) { throw 'The installation path is invalid.' }
    if (@(Get-Process SnapCraft -ErrorAction SilentlyContinue | Where-Object { try { [IO.Path]::GetFullPath($_.Path) -eq [IO.Path]::GetFullPath((Join-Path $target 'SnapCraft.exe')) } catch { $false } }).Count) { throw "Close $productName before uninstalling it." }
    $shell = New-Object -ComObject WScript.Shell
    foreach ($directory in @((Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'), [Environment]::GetFolderPath('Desktop'))) {
        foreach ($name in @("$($product.InstallFolder).lnk", "$productName.lnk")) {
            $link = Join-Path $directory $name
            if ((Test-Path -LiteralPath $link) -and $shell.CreateShortcut($link).TargetPath -eq (Join-Path $target 'SnapCraft.exe')) { Remove-Item -LiteralPath $link -Force }
        }
    }
    Remove-Item -LiteralPath "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$($product.UninstallKey)" -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name $product.StartupValue -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
    [System.Windows.Forms.MessageBox]::Show("$productName was removed. Your settings, exported images, and videos were not deleted.", $productName, [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
} catch {
    [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, "$productName uninstall failed", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
    exit 1
}
