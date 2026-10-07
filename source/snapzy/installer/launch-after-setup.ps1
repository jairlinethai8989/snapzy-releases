param([int]$SetupPid, [string]$AppPath)
$ErrorActionPreference = 'Stop'
$parent = Get-Process -Id $SetupPid -ErrorAction SilentlyContinue
if ($parent) { Wait-Process -InputObject $parent -ErrorAction SilentlyContinue }
if (Test-Path -LiteralPath $AppPath) {
    Start-Process -FilePath $AppPath -WorkingDirectory (Split-Path $AppPath) -WindowStyle Normal
}
