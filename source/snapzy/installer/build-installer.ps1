param(
    [ValidateSet('Snapzy')][string]$ProductFlavor = 'Snapzy',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\dist'),
    [switch]$PublishOnly
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'product-profile.ps1')
. (Join-Path $PSScriptRoot 'runtime-policy.ps1')
. (Join-Path $PSScriptRoot 'payload-install.ps1')
$product = Get-ProductProfile $ProductFlavor
$productName = $product.Name
$version = $product.Version
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dist = [IO.Path]::GetFullPath($OutputDirectory)
$publish = Join-Path $dist $product.PublishFolder
$staging = Join-Path $dist $product.StagingFolder
$output = Join-Path $dist $product.OutputFile
$env:APPDATA = Join-Path $root '.build-profile'
$env:DOTNET_CLI_HOME = $env:APPDATA
$env:NUGET_PACKAGES = Join-Path $env:USERPROFILE '.nuget\packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
New-Item -ItemType Directory -Force -Path $env:APPDATA, $dist, $staging | Out-Null

$project = Join-Path $root 'src\SnapCraft\SnapCraft.csproj'
if (-not $publish.StartsWith($dist.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -or (Split-Path $publish -Leaf) -ne $product.PublishFolder) { throw 'Invalid publish directory.' }
Get-SafePayloadFile $publish 'SnapCraft.exe' | Out-Null
if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
# Restore packs for license attribution and the reviewed legacy cleanup inventory,
# then restore the application in its actual framework-dependent deployment mode.
dotnet restore $project --configfile (Join-Path $root 'NuGet.Config') -p:Platform=x64 -p:SelfContained=true
if ($LASTEXITCODE -ne 0) { throw 'Runtime inventory restore failed.' }
dotnet restore $project --configfile (Join-Path $root 'NuGet.Config') -p:Platform=x64 -p:SelfContained=false -p:RollForward=LatestPatch -p:AppHostDotNetSearch=Global
if ($LASTEXITCODE -ne 0) { throw 'Package restore failed.' }
dotnet publish $project -c Release -p:Platform=x64 --no-restore --self-contained false -p:SelfContained=false -p:RollForward=LatestPatch -p:AppHostDotNetSearch=Global -t:Rebuild -p:TreatWarningsAsErrors=true -o $publish
if ($LASTEXITCODE -ne 0) { throw 'App publish failed.' }
# Debug symbols and API documentation are build artifacts, not runtime dependencies.
$publish = [IO.Path]::GetFullPath($publish)
if (-not $publish.StartsWith(([IO.Path]::GetFullPath($dist).TrimEnd('\') + '\'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid publish directory.' }
Get-ChildItem -LiteralPath $publish -Recurse -File | Where-Object { $_.Extension -in '.pdb', '.xml' } | Remove-Item -Force
Copy-Item (Join-Path $PSScriptRoot 'uninstall.ps1') -Destination $publish -Force
Copy-Item (Join-Path $PSScriptRoot 'launch-after-setup.ps1') -Destination $publish -Force
Copy-Item (Join-Path $PSScriptRoot 'product-profile.ps1') -Destination $publish -Force
$iconSource = 'snapzy.ico'
Copy-Item -LiteralPath (Join-Path $publish "Assets\icons\$iconSource") -Destination (Join-Path $publish (Join-Path 'Assets\icons' $product.IconName)) -Force
Copy-Item -LiteralPath (Join-Path $env:NUGET_PACKAGES 'naudio\2.2.1\license.txt') -Destination (Join-Path $publish 'LICENSE-NAudio.txt') -Force
$libraryLicense = Join-Path $env:NUGET_PACKAGES 'screenrecorderlib\7.0.1\LICENSE'
if (Test-Path -LiteralPath $libraryLicense) {
    Copy-Item -LiteralPath $libraryLicense -Destination (Join-Path $publish 'LICENSE-ScreenRecorderLib.txt') -Force
}
$runtimeConfig = Get-Content -LiteralPath (Join-Path $publish 'SnapCraft.runtimeconfig.json') -Raw | ConvertFrom-Json
$requirements=Get-DotNetRequirements $runtimeConfig
$requirements | Add-Member -NotePropertyName hostSearch -NotePropertyValue 'Global'
$requirements | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $publish 'runtime-requirements.json') -Encoding UTF8
$runtimeVersion = ($requirements.frameworks | Where-Object { $_.name -eq 'Microsoft.NETCore.App' }).version
$desktopVersion = ($requirements.frameworks | Where-Object { $_.name -eq 'Microsoft.WindowsDesktop.App' }).version
foreach ($license in @(
    @{ Package = "microsoft.netcore.app.runtime.win-x64\$runtimeVersion"; Source = 'LICENSE.TXT'; Output = 'LICENSE-DotNet.txt' },
    @{ Package = "microsoft.netcore.app.runtime.win-x64\$runtimeVersion"; Source = 'THIRD-PARTY-NOTICES.TXT'; Output = 'THIRD-PARTY-NOTICES-DotNet.txt' },
    @{ Package = "microsoft.windowsdesktop.app.runtime.win-x64\$desktopVersion"; Source = 'LICENSE'; Output = 'LICENSE-WindowsDesktop.txt' }
)) {
    $source = Join-Path (Join-Path $env:NUGET_PACKAGES $license.Package) $license.Source
    Copy-Item -LiteralPath $source -Destination (Join-Path $publish $license.Output) -Force
}

$legacyFiles=@('LICENSE-DotNet.txt','THIRD-PARTY-NOTICES-DotNet.txt','LICENSE-WindowsDesktop.txt')
foreach ($package in @("microsoft.netcore.app.runtime.win-x64\$runtimeVersion","microsoft.windowsdesktop.app.runtime.win-x64\$desktopVersion")) {
    foreach ($folder in @('runtimes\win-x64\native','runtimes\win-x64\lib\net10.0')) {
        $directory=Join-Path (Join-Path $env:NUGET_PACKAGES $package) $folder
        if (-not (Test-Path -LiteralPath $directory)) { throw "Legacy runtime inventory source missing: $directory" }
        $prefix=[IO.Path]::GetFullPath($directory).TrimEnd('\')+'\'
        $legacyFiles+=@(Get-ChildItem -LiteralPath $directory -Recurse -File | Where-Object { $_.Extension -in '.dll','.exe','.json','.dat','.txt' } | ForEach-Object { $_.FullName.Substring($prefix.Length).Replace('\','/') })
    }
}
[pscustomobject]@{schema=1;files=@($legacyFiles | Sort-Object -Unique)} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $publish 'legacy-runtime-files.json') -Encoding UTF8
foreach ($module in 'runtime-policy.ps1','runtime-setup.ps1','payload-install.ps1') { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $module) -Destination $publish -Force }
Get-ManagedPayloadManifest $publish | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $publish 'managed-files.json') -Encoding UTF8
Read-ManagedPayloadManifest $publish -Validate | Out-Null
if ($PublishOnly) { Write-Output "Published framework-dependent payload: $publish"; exit 0 }

# Use one installer flow so upgrade checks do not depend on tools installed on the build machine.

$archive = Join-Path $staging 'payload.zip'
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $archive -Force
Copy-Item (Join-Path $PSScriptRoot 'install.ps1') -Destination $staging -Force
Copy-Item (Join-Path $PSScriptRoot 'setup-options.ps1') -Destination $staging -Force
Copy-Item (Join-Path $PSScriptRoot 'upgrade-policy.ps1') -Destination $staging -Force
Copy-Item (Join-Path $PSScriptRoot 'product-profile.ps1') -Destination $staging -Force
foreach ($file in 'runtime-policy.ps1','runtime-setup.ps1','payload-install.ps1','runtime-requirements.json','legacy-runtime-files.json') { Copy-Item -LiteralPath (Join-Path $publish $file) -Destination $staging -Force }
$installCmd = "@echo off`r`npowershell.exe -NoProfile -ExecutionPolicy Bypass -File `"%~dp0install.ps1`" -ProductFlavor $ProductFlavor`r`nexit /b %errorlevel%`r`n"
[IO.File]::WriteAllText((Join-Path $staging 'install.cmd'), $installCmd, [Text.Encoding]::ASCII)
$sedPath = Join-Path $staging "$ProductFlavor.sed"
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }
$sed = @"
[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=0
HideExtractAnimation=0
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=%InstallPrompt%
DisplayLicense=%DisplayLicense%
FinishMessage=%FinishMessage%
TargetName=%TargetName%
FriendlyName=%FriendlyName%
AppLaunched=%AppLaunched%
PostInstallCmd=%PostInstallCmd%
AdminQuietInstCmd=
UserQuietInstCmd=
SourceFiles=SourceFiles
[SourceFiles]
SourceFiles0=$staging\
[SourceFiles0]
%FILE0%=
%FILE1%=
%FILE2%=
%FILE3%=
%FILE4%=
%FILE5%=
%FILE6%=
%FILE7%=
%FILE8%=
%FILE9%=
%FILE10%=
[Strings]
InstallPrompt=Install $productName for Windows?
DisplayLicense=
FinishMessage=
TargetName=$output
FriendlyName=$productName
AppLaunched=install.cmd
PostInstallCmd=<None>
FILE0=payload.zip
FILE1=install.cmd
FILE2=install.ps1
FILE3=setup-options.ps1
FILE4=upgrade-policy.ps1
FILE5=product-profile.ps1
FILE6=runtime-policy.ps1
FILE7=runtime-setup.ps1
FILE8=payload-install.ps1
FILE9=runtime-requirements.json
FILE10=legacy-runtime-files.json
"@
[IO.File]::WriteAllText($sedPath, (($sed -split '\r?\n') -join "`r`n"), [Text.Encoding]::ASCII)
# IExpress parses the SED argument itself and rejects enclosing quotes.
if ($sedPath.Contains(' ')) {
    $sedPath = (New-Object -ComObject Scripting.FileSystemObject).GetFile($sedPath).ShortPath
    if ($sedPath.Contains(' ')) { throw 'IExpress needs a build path without spaces when short filenames are disabled.' }
}
$package = Start-Process -FilePath (Join-Path $env:WINDIR 'System32\iexpress.exe') -ArgumentList "/N /Q $sedPath" -WindowStyle Hidden -PassThru
if (-not $package.WaitForExit(900000)) {
    $package.Kill()
    $package.WaitForExit()
    throw 'IExpress packaging exceeded 15 minutes.'
}
if ($package.ExitCode -ne 0) { throw "IExpress packaging failed: $($package.ExitCode)" }
$lastSize = -1
$stableChecks = 0
for ($attempt = 0; $attempt -lt 15; $attempt++) {
    Start-Sleep -Seconds 1
    $file = Get-Item -LiteralPath $output -ErrorAction SilentlyContinue
    if ($file -and $file.Length -gt 1MB -and $file.Length -eq $lastSize) { $stableChecks++ }
    else { $stableChecks = 0 }
    if ($stableChecks -ge 3) { break }
    $lastSize = if ($file) { $file.Length } else { -1 }
}
if ($stableChecks -lt 3) { throw 'IExpress did not finish creating the setup file.' }
Write-Output "Installer: $output"
