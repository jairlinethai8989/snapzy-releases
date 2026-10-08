function Get-SafePayloadFile([string]$Root,[string]$Relative) {
    if (-not $Relative -or $Relative -notmatch '^[A-Za-z0-9_. -]+(/[A-Za-z0-9_. -]+)*$') { throw 'Invalid managed file path.' }
    foreach ($segment in $Relative.Split('/')) { if ($segment -in '.','..' -or $segment.EndsWith('.') -or $segment.EndsWith(' ')) { throw 'Unsafe managed file path.' } }
    $base=[IO.Path]::GetFullPath($Root).TrimEnd('\')
    $path=[IO.Path]::GetFullPath((Join-Path $base $Relative))
    if (-not $path.StartsWith($base+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Managed file escapes installation directory.' }
    $ancestor=Split-Path $path
    while ($ancestor) {
        $item=Get-Item -LiteralPath $ancestor -Force -ErrorAction SilentlyContinue
        if ($item -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Installation through a junction or symlink is not allowed.' }
        $parent=Split-Path $ancestor
        if ($parent -eq $ancestor) { break }; $ancestor=$parent
    }
    $item=Get-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
    if ($item -and (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $item.PSIsContainer)) { throw 'Managed file path is a directory or symlink.' }
    return $path
}

function Get-ManagedPayloadManifest([string]$Directory) {
    $root=[IO.Path]::GetFullPath($Directory).TrimEnd('\')
    $files=@(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object { $_.FullName -ine (Join-Path $root 'managed-files.json') } | Sort-Object FullName | ForEach-Object {
        if (-not $_.FullName.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Payload file escapes its directory.' }
        $relative=$_.FullName.Substring($root.Length+1).Replace('\','/')
        $path=Get-SafePayloadFile $root $relative
        [pscustomobject]@{path=$relative;hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
    })
    return [pscustomobject]@{schema=1;files=$files}
}

function Read-ManagedPayloadManifest([string]$Directory,[switch]$Validate) {
    $manifest=Get-Content -LiteralPath (Join-Path $Directory 'managed-files.json') -Raw | ConvertFrom-Json
    if ($manifest.schema -ne 1 -or -not $manifest.files -or $manifest.files.Count -gt 20000) { throw 'Invalid managed payload manifest.' }
    $seen=@{}
    foreach ($file in $manifest.files) {
        $path=Get-SafePayloadFile $Directory $file.path
        if ($seen.ContainsKey($file.path) -or $file.path -ieq 'managed-files.json' -or $file.hash -notmatch '^[0-9a-fA-F]{64}$') { throw 'Invalid or duplicate managed payload entry.' }
        $seen[$file.path]=$true
        if ($Validate -and (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ine $file.hash) { throw "Payload integrity check failed: $($file.path)" }
    }
    return $manifest
}

function Remove-ManagedStagingDirectory([string]$Directory) {
    $path=[IO.Path]::GetFullPath($Directory)
    $parent=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if (-not $path.StartsWith($parent,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path $path -Leaf) -notmatch '^app-payload-[0-9a-f]{32}$') { throw 'Unsafe payload staging cleanup.' }
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
}

function Expand-ManagedPayload([string]$Archive) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $stage=Join-Path ([IO.Path]::GetTempPath()) ('app-payload-'+[guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage | Out-Null
    $zip=$null
    try {
        $zip=[IO.Compression.ZipFile]::OpenRead($Archive)
        foreach ($entry in $zip.Entries) {
            # Windows PowerShell Compress-Archive writes backslash-separated names.
            $relative=$entry.FullName.Replace('\','/').TrimEnd('/')
            if (-not $relative) { continue }
            Get-SafePayloadFile $stage $relative | Out-Null
        }
        $zip.Dispose(); $zip=$null
        Expand-Archive -LiteralPath $Archive -DestinationPath $stage
        Read-ManagedPayloadManifest $stage -Validate | Out-Null
        return $stage
    } catch { Remove-ManagedStagingDirectory $stage; throw }
    finally { if ($zip) { $zip.Dispose() } }
}

function Install-ManagedPayload([string]$Stage,[string]$Target,$Legacy) {
    $manifest=Read-ManagedPayloadManifest $Stage -Validate
    if ($Legacy.schema -ne 1) { throw 'Invalid legacy runtime inventory.' }
    $affected=@{}
    foreach ($file in $manifest.files) { $affected[$file.path]=$true }
    $affected['managed-files.json']=$true
    if (Test-Path -LiteralPath (Join-Path $Target 'managed-files.json')) {
        $old=Read-ManagedPayloadManifest $Target
        foreach ($file in $old.files) { $affected[$file.path]=$true }
    }
    try { $bundled=(Get-Content -LiteralPath (Join-Path $Target 'SnapCraft.runtimeconfig.json') -Raw | ConvertFrom-Json).runtimeOptions.includedFrameworks } catch { $bundled=$null }
    if ($bundled) { foreach ($relative in $Legacy.files) { Get-SafePayloadFile $Target $relative | Out-Null; $affected[$relative]=$true } }
    foreach ($relative in $affected.Keys) { Get-SafePayloadFile $Target $relative | Out-Null }
    $parent=Split-Path ([IO.Path]::GetFullPath($Target).TrimEnd('\'))
    $backup=Join-Path $parent ('.app-backup-'+[guid]::NewGuid().ToString('N'))
    $backupPath=[IO.Path]::GetFullPath($backup)
    if (-not $backupPath.StartsWith([IO.Path]::GetFullPath($parent).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe upgrade backup path.' }
    New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
    $moved=[Collections.Generic.List[string]]::new(); $written=[Collections.Generic.List[string]]::new(); $keepBackup=$false
    try {
        New-Item -ItemType Directory -Path $Target -Force | Out-Null
        foreach ($relative in ($affected.Keys | Sort-Object)) {
            $file=Get-SafePayloadFile $Target $relative
            if (Test-Path -LiteralPath $file) {
                $saved=Get-SafePayloadFile $backupPath $relative
                New-Item -ItemType Directory -Path (Split-Path $saved) -Force | Out-Null
                Move-Item -LiteralPath $file -Destination $saved
                $moved.Add($relative)
            }
        }
        foreach ($relative in @($manifest.files.path)+@('managed-files.json')) {
            $file=Get-SafePayloadFile $Target $relative
            New-Item -ItemType Directory -Path (Split-Path $file) -Force | Out-Null
            $written.Add($relative)
            Copy-Item -LiteralPath (Get-SafePayloadFile $Stage $relative) -Destination $file
        }
        Read-ManagedPayloadManifest $Target -Validate | Out-Null
    } catch {
        $original=$_
        try {
            foreach ($relative in $written) { $file=Get-SafePayloadFile $Target $relative; if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file -Force } }
            foreach ($relative in $moved) { Move-Item -LiteralPath (Get-SafePayloadFile $backupPath $relative) -Destination (Get-SafePayloadFile $Target $relative) }
        } catch { $keepBackup=$true; throw "Upgrade recovery needs attention. Original files remain in $backupPath. $($_.Exception.Message)" }
        throw $original
    } finally {
        if (-not $keepBackup) { Remove-Item -LiteralPath $backupPath -Recurse -Force }
    }
}

function Invoke-ApplicationPayload([string]$Archive,[string]$Target,$Legacy,[scriptblock]$PrepareRuntime,[scriptblock]$CheckClosed) {
    $stage=Expand-ManagedPayload $Archive
    try {
        foreach ($name in 'SnapCraft.exe','SnapCraft.dll','SnapCraft.runtimeconfig.json','runtime-requirements.json') {
            if (-not (Test-Path -LiteralPath (Join-Path $stage $name))) { throw "Incomplete application payload: $name" }
        }
        $expected=Get-DotNetRequirements (Get-Content -LiteralPath (Join-Path $stage 'SnapCraft.runtimeconfig.json') -Raw | ConvertFrom-Json)
        $requirementsPath=Join-Path $stage 'runtime-requirements.json'
        $requirements=Get-Content -LiteralPath $requirementsPath -Raw | ConvertFrom-Json
        Assert-DotNetRequirements $requirements
        if ($requirements.hostSearch -cne 'Global') { throw 'Unsupported runtime discovery policy.' }
        foreach ($framework in $expected.frameworks) {
            if (($requirements.frameworks | Where-Object { $_.name -ceq $framework.name }).version -cne $framework.version) { throw 'Prerequisites do not match the application.' }
        }
        $status=& $PrepareRuntime $requirementsPath
        if ($status -in 'Canceled','RestartRequired') { return $status }
        if ($status -ne 'Ready') { throw 'Unexpected prerequisite installation status.' }
        & $CheckClosed
        Install-ManagedPayload $stage $Target $Legacy
        return 'Ready'
    } finally { Remove-ManagedStagingDirectory $stage }
}
