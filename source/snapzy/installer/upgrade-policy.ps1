function Get-UpgradeAction([string]$InstalledVersion, [string]$IncomingVersion) {
    if (-not $InstalledVersion) { return 'Install' }
    $installed = [version]$InstalledVersion
    $incoming = [version]$IncomingVersion
    if ($installed -gt $incoming) { return 'Block' }
    if ($installed -eq $incoming) { return 'Repair' }
    return 'Update'
}
