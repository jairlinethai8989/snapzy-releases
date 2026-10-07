function Get-StableDotNetVersion([string]$Text) {
    if ($Text -notmatch '^10\.0\.(0|[1-9][0-9]*)$') { return $null }
    return [version]$Text
}

function Assert-DotNetRequirements($Requirements) {
    if ($Requirements.schema -ne 1 -or $Requirements.architecture -cne 'x64' -or $Requirements.rollForward -cne 'LatestPatch') {
        throw 'Unsupported .NET prerequisite policy.'
    }
    if (@($Requirements.frameworks).Count -ne 2) { throw 'Both .NET desktop and core frameworks are required.' }
    foreach ($name in 'Microsoft.NETCore.App','Microsoft.WindowsDesktop.App') {
        $framework = @($Requirements.frameworks | Where-Object { $_.name -ceq $name })
        if ($framework.Count -ne 1 -or $null -eq (Get-StableDotNetVersion $framework[0].version)) {
            throw "Invalid .NET framework requirement: $name"
        }
    }
}

function Get-DotNetRequirements($Configuration) {
    $options = $Configuration.runtimeOptions
    if ($options.tfm -cne 'net10.0' -or $options.includedFrameworks -or $options.rollForward -cne 'LatestPatch') {
        throw 'The application must use framework-dependent .NET 10 with LatestPatch roll-forward.'
    }
    $requirements = [pscustomobject]@{ schema=1; architecture='x64'; rollForward='LatestPatch'; frameworks=@($options.frameworks) }
    Assert-DotNetRequirements $requirements
    return $requirements
}

function Test-DotNetRuntimeInventory($Requirements, [string[]]$Inventory, [string]$Root, [string]$Architecture) {
    Assert-DotNetRequirements $Requirements
    if ($Architecture -cne 'x64' -or -not $Root) { return $false }
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    $selected = @{}
    foreach ($framework in $Requirements.frameworks) {
        $found = $false
        $expected = Join-Path $rootPath ("shared\{0}" -f $framework.name)
        $sentinel = if ($framework.name -ceq 'Microsoft.NETCore.App') { 'coreclr.dll' } else { 'System.Windows.Forms.dll' }
        foreach ($line in $Inventory) {
            if ($line -notmatch '^(Microsoft\.[A-Za-z.]+) ([^ ]+) \[(.+)\]$' -or $Matches[1] -cne $framework.name) { continue }
            $version = Get-StableDotNetVersion $Matches[2]
            $location = $Matches[3]
            if ($null -eq $version -or $version -lt [version]$framework.version) { continue }
            if ([IO.Path]::GetFullPath($location).TrimEnd('\') -ine $expected.TrimEnd('\')) { continue }
            $file = Get-Item -LiteralPath (Join-Path $expected ("{0}\{1}" -f $version,$sentinel)) -ErrorAction SilentlyContinue
            if ($file -and -not $file.PSIsContainer -and $file.Length -gt 0) {
                $found = $true
                if (-not $selected[$framework.name] -or $version -gt $selected[$framework.name]) { $selected[$framework.name] = $version }
            }
        }
        if (-not $found) { return $false }
    }
    try {
        $desktop = Join-Path $rootPath ("shared\Microsoft.WindowsDesktop.App\{0}\Microsoft.WindowsDesktop.App.runtimeconfig.json" -f $selected['Microsoft.WindowsDesktop.App'])
        $dependency = (Get-Content -LiteralPath $desktop -Raw | ConvertFrom-Json).runtimeOptions.framework
        $minimum = Get-StableDotNetVersion $dependency.version
        return $dependency.name -ceq 'Microsoft.NETCore.App' -and $null -ne $minimum -and $selected['Microsoft.NETCore.App'] -ge $minimum
    } catch { return $false }
}

function Test-DotNetDownloadUri([string]$Url, [string]$Version) {
    $uri = $null
    if (-not [Uri]::TryCreate($Url,[UriKind]::Absolute,[ref]$uri)) { return $false }
    return $uri.Scheme -ceq 'https' -and $uri.Host -ieq 'builds.dotnet.microsoft.com' -and $uri.IsDefaultPort -and
        -not $uri.UserInfo -and -not $uri.Query -and -not $uri.Fragment -and
        $uri.AbsolutePath -ceq "/dotnet/WindowsDesktop/$Version/windowsdesktop-runtime-$Version-win-x64.exe"
}

function Get-DotNetInstallerRelease($Requirements, $Metadata) {
    Assert-DotNetRequirements $Requirements
    $minimum = ($Requirements.frameworks | ForEach-Object { [version]$_.version } | Sort-Object -Descending | Select-Object -First 1)
    $candidates = @()
    foreach ($release in $Metadata.releases) {
        $version = Get-StableDotNetVersion $release.windowsdesktop.version
        $core = Get-StableDotNetVersion $release.runtime.version
        if ($null -eq $version -or $null -eq $core -or $version -lt $minimum -or $core -lt $minimum) { continue }
        $files = @($release.windowsdesktop.files | Where-Object { $_.rid -ceq 'win-x64' -and $_.name -ceq 'windowsdesktop-runtime-win-x64.exe' })
        if ($files.Count -ne 1) { continue }
        $file = $files[0]
        if (-not (Test-DotNetDownloadUri $file.url $version.ToString()) -or $file.hash -notmatch '^[0-9a-fA-F]{128}$') { continue }
        $candidates += [pscustomobject]@{ version=$version.ToString(); url=$file.url; hash=$file.hash }
    }
    $chosen = $candidates | Sort-Object { [version]$_.version } -Descending | Select-Object -First 1
    if ($null -eq $chosen) { throw 'No verified compatible .NET Desktop Runtime download was found.' }
    return $chosen
}

function Invoke-DotNetPrerequisite($Requirements, [hashtable]$Operations) {
    Assert-DotNetRequirements $Requirements
    foreach ($name in 'Detect','Consent','Acquire','Signature','Install','Cleanup') {
        if ($Operations[$name] -isnot [scriptblock]) { throw "Missing prerequisite operation: $name" }
    }
    if (& $Operations.Detect) { return 'Ready' }
    if (-not (& $Operations.Consent)) { return 'Canceled' }
    $artifact = $null
    try {
        $artifact = & $Operations.Acquire
        if (-not $artifact.path -or $artifact.hash -notmatch '^[0-9a-fA-F]{128}$' -or
            (Get-FileHash -LiteralPath $artifact.path -Algorithm SHA512).Hash -ine $artifact.hash) {
            throw 'The downloaded .NET installer failed its SHA-512 integrity check.'
        }
        if (-not (& $Operations.Signature $artifact.path)) { throw 'The .NET installer does not have a trusted Microsoft signature.' }
        try { $code = & $Operations.Install $artifact.path }
        catch {
            $error = $_.Exception
            while ($error) {
                if ($error -is [ComponentModel.Win32Exception] -and $error.NativeErrorCode -eq 1223) { return 'Canceled' }
                $error = $error.InnerException
            }
            throw
        }
        if ($code -eq 1602 -or $code -eq 1223) { return 'Canceled' }
        if ($code -eq 3010) { return 'RestartRequired' }
        if ($code -ne 0) { throw ".NET Desktop Runtime installation failed (code $code)." }
        if (-not (& $Operations.Detect)) { throw 'The required .NET Desktop Runtime is still unavailable after installation.' }
        return 'Ready'
    } finally {
        if ($null -ne $artifact) { & $Operations.Cleanup $artifact }
    }
}
