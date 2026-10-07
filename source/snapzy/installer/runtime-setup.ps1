. (Join-Path $PSScriptRoot 'runtime-policy.ps1')

function Get-RegisteredDotNetRoot {
    foreach ($view in [Microsoft.Win32.RegistryView]::Registry32,[Microsoft.Win32.RegistryView]::Registry64) {
        $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine,$view)
        try {
            $key = $base.OpenSubKey('SOFTWARE\dotnet\Setup\InstalledVersions\x64')
            if ($key) {
                try { $location = [string]$key.GetValue('InstallLocation'); if ($location) { return $location } }
                finally { $key.Dispose() }
            }
        } finally { $base.Dispose() }
    }
    $programFiles = if ($env:ProgramW6432) { $env:ProgramW6432 } else { [Environment]::GetFolderPath('ProgramFiles') }
    return Join-Path $programFiles 'dotnet'
}

function Get-PeArchitecture([string]$Path) {
    $stream = $null; $reader = $null
    try {
        $stream = [IO.File]::OpenRead($Path); $reader = [IO.BinaryReader]::new($stream)
        if ($stream.Length -lt 64 -or $reader.ReadUInt16() -ne 0x5a4d) { return '' }
        $stream.Position=60; $offset=$reader.ReadInt32()
        if ($offset -lt 64 -or $offset -gt $stream.Length-6) { return '' }
        $stream.Position=$offset
        if ($reader.ReadUInt32() -ne 0x4550) { return '' }
        switch ($reader.ReadUInt16()) { 0x8664 {return 'x64'} 0x14c {return 'x86'} default {return ''} }
    } catch { return '' }
    finally { if ($reader) { $reader.Dispose() } elseif ($stream) { $stream.Dispose() } }
}

function Test-MicrosoftBinary([string]$Path) {
    try {
        $signature=Get-AuthenticodeSignature -LiteralPath $Path
        return $signature.Status -eq 'Valid' -and $signature.SignerCertificate -and
            $signature.SignerCertificate.Subject -match '(?:^|,\s*)O=Microsoft Corporation(?:,|$)'
    } catch { return $false }
}

function Get-DotNetRuntimeStatus($Requirements) {
    $process=$null
    try {
        $root=Get-RegisteredDotNetRoot
        $hostFile=Join-Path $root 'dotnet.exe'
        if ((Get-PeArchitecture $hostFile) -cne 'x64' -or -not (Test-MicrosoftBinary $hostFile)) { return $false }
        # Own the process handle: Start-Process can lose a quick exit code on PowerShell 5.1.
        $start=[Diagnostics.ProcessStartInfo]::new()
        $start.FileName=$hostFile; $start.Arguments='--list-runtimes'
        $start.UseShellExecute=$false; $start.CreateNoWindow=$true
        $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
        $start.StandardOutputEncoding=[Text.Encoding]::UTF8
        $process=[Diagnostics.Process]::new(); $process.StartInfo=$start
        if (-not $process.Start()) { return $false }
        $output=$process.StandardOutput.ReadToEndAsync()
        $errors=$process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(5000)) { $process.Kill(); $process.WaitForExit(); return $false }
        if ($process.ExitCode -ne 0 -or -not $output.Wait(1000) -or -not $errors.Wait(1000)) { return $false }
        $inventory=$output.GetAwaiter().GetResult()
        if ([Text.Encoding]::UTF8.GetByteCount($inventory) -gt 1MB) { return $false }
        return Test-DotNetRuntimeInventory $Requirements @($inventory -split '\r?\n') $root 'x64'
    } catch { return $false }
    finally {
        if ($process) { $process.Dispose() }
    }
}

function Remove-RuntimeTemporaryDirectory([string]$Directory) {
    $path=[IO.Path]::GetFullPath($Directory)
    $parent=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if (-not $path.StartsWith($parent,[StringComparison]::OrdinalIgnoreCase) -or
        (Split-Path $path -Leaf) -notmatch '^(dotnet-probe|dotnet-download)-[0-9a-f]{32}$') { throw 'Unsafe runtime temporary directory.' }
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
}

function Get-RuntimeText([string]$Language,[string]$English,[string]$Thai) {
    if ($Language -eq 'th') { return $Thai }; return $English
}

function New-DotNetConsentForm([string]$ProductName,[string]$Language,[string]$Minimum) {
    Add-Type -AssemblyName System.Windows.Forms
    $form=[Windows.Forms.Form]::new()
    $form.Text="$ProductName - .NET"; $form.ClientSize=[Drawing.Size]::new(460,200)
    $form.StartPosition='CenterScreen'; $form.FormBorderStyle='FixedDialog'; $form.MaximizeBox=$false; $form.MinimizeBox=$false
    $form.Font=[Drawing.Font]::new('Segoe UI',10)
    $message=[Windows.Forms.Label]::new(); $message.Name='message'; $message.Bounds=[Drawing.Rectangle]::new(20,18,420,120)
    $message.Text=Get-RuntimeText $Language "Microsoft .NET Desktop Runtime $Minimum or a compatible newer 10.0 patch (x64) is required.`n`nDownload and install it now? Administrator approval and internet access may be needed." "ต้องใช้ Microsoft .NET Desktop Runtime $Minimum หรือแพตช์ 10.0 ที่ใหม่กว่า (x64)`n`nต้องการดาวน์โหลดและติดตั้งตอนนี้หรือไม่? อาจต้องใช้อินเทอร์เน็ตและสิทธิ์ผู้ดูแลระบบ"
    $accept=[Windows.Forms.Button]::new(); $accept.Name='accept'; $accept.Bounds=[Drawing.Rectangle]::new(185,152,125,32)
    $accept.Text=Get-RuntimeText $Language 'Install .NET' 'ติดตั้ง .NET'; $accept.DialogResult='OK'
    $cancel=[Windows.Forms.Button]::new(); $cancel.Name='cancel'; $cancel.Bounds=[Drawing.Rectangle]::new(320,152,120,32)
    $cancel.Text=Get-RuntimeText $Language 'Cancel setup' 'ยกเลิกติดตั้ง'; $cancel.DialogResult='Cancel'
    $form.Controls.AddRange(@($message,$accept,$cancel)); $form.AcceptButton=$accept; $form.CancelButton=$cancel
    return $form
}

function Wait-RuntimeNetworkTask($Task,$State) {
    while (-not $Task.IsCompleted) {
        [Windows.Forms.Application]::DoEvents()
        $State.Token.Token.ThrowIfCancellationRequested()
        Start-Sleep -Milliseconds 20
    }
    return $Task.GetAwaiter().GetResult()
}

function Get-VerifiedRuntimeDownload($Requirements,$State,[string]$Language) {
    Add-Type -AssemblyName System.Net.Http
    $directory=Join-Path ([IO.Path]::GetTempPath()) ('dotnet-download-'+[guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $directory | Out-Null
    $handler=[Net.Http.HttpClientHandler]::new(); $handler.AllowAutoRedirect=$false
    $client=[Net.Http.HttpClient]::new($handler); $client.Timeout=[TimeSpan]::FromSeconds(30)
    $response=$null; $stream=$null; $output=$null
    try {
        [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
        $State.Token.CancelAfter([TimeSpan]::FromMinutes(5))
        $text=Wait-RuntimeNetworkTask ($client.GetStringAsync('https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json')) $State
        $release=Get-DotNetInstallerRelease $Requirements ($text | ConvertFrom-Json)
        $State.Label.Text=(Get-RuntimeText $Language 'Downloading .NET Desktop Runtime' 'กำลังดาวน์โหลด .NET Desktop Runtime')+" $($release.version)"
        $response=Wait-RuntimeNetworkTask ($client.GetAsync($release.url,[Net.Http.HttpCompletionOption]::ResponseHeadersRead,$State.Token.Token)) $State
        $response.EnsureSuccessStatusCode() | Out-Null
        $length=$response.Content.Headers.ContentLength
        if ($length -gt 200MB) { throw 'Unexpected runtime download size.' }
        $stream=Wait-RuntimeNetworkTask ($response.Content.ReadAsStreamAsync()) $State
        $path=Join-Path $directory 'windowsdesktop-runtime-win-x64.exe'
        $output=[IO.File]::Create($path); $buffer=New-Object byte[] 65536; $total=0L
        while ($true) {
            $read=Wait-RuntimeNetworkTask ($stream.ReadAsync($buffer,0,$buffer.Length,$State.Token.Token)) $State
            if ($read -eq 0) { break }
            $total+=$read; if ($total -gt 200MB) { throw 'Runtime download exceeded the size limit.' }
            $output.Write($buffer,0,$read)
            if ($length -gt 0) { $State.Progress.Style='Continuous'; $State.Progress.Value=[Math]::Min(100,[int]($total*100/$length)) }
        }
        $output.Dispose(); $output=$null
        if ($length -and $total -ne $length) { throw 'The runtime download is incomplete.' }
        return [pscustomobject]@{path=$path;hash=$release.hash;directory=$directory}
    } catch {
        if ($output) { $output.Dispose(); $output=$null }
        Remove-RuntimeTemporaryDirectory $directory
        throw
    } finally {
        if ($output) { $output.Dispose() }; if ($stream) { $stream.Dispose() }; if ($response) { $response.Dispose() }
        $client.Dispose(); $handler.Dispose()
    }
}

function Start-DotNetRuntimeInstaller([string]$Path,$State,[string]$Language) {
    $State.Installing=$true; $State.Cancel.Enabled=$false; $State.Progress.Style='Marquee'
    $State.Token.CancelAfter([Threading.Timeout]::Infinite)
    $State.Label.Text=Get-RuntimeText $Language 'Installing .NET. Approve the Windows prompt to continue.' 'กำลังติดตั้ง .NET กรุณายืนยันหน้าต่างขอสิทธิ์ของ Windows'
    $process=$null
    try {
        $process=Start-Process -FilePath $Path -ArgumentList '/install /passive /norestart' -Verb RunAs -PassThru
        $deadline=[DateTime]::UtcNow.AddMinutes(15)
        while (-not $process.HasExited) {
            [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 100
            if ([DateTime]::UtcNow -gt $deadline) { $State.KeepDownload=$true; throw 'The Microsoft runtime installer is still running. Complete it, then rerun setup.' }
        }
        return $process.ExitCode
    } finally { if ($process) { $process.Dispose() }; $State.Installing=$false; $State.Cancel.Enabled=$true }
}

function Install-RequiredDesktopRuntime([string]$RequirementsPath,[string]$ProductName,[string]$Language) {
    Add-Type -AssemblyName System.Windows.Forms
    $requirements=Get-Content -LiteralPath $RequirementsPath -Raw | ConvertFrom-Json
    Assert-DotNetRequirements $requirements
    $minimum=($requirements.frameworks | ForEach-Object { [version]$_.version } | Sort-Object -Descending | Select-Object -First 1).ToString()
    $progress=[Windows.Forms.Form]::new(); $progress.Text="$ProductName - .NET"; $progress.ClientSize=[Drawing.Size]::new(460,145)
    $progress.StartPosition='CenterScreen'; $progress.FormBorderStyle='FixedDialog'; $progress.MaximizeBox=$false; $progress.MinimizeBox=$false
    $label=[Windows.Forms.Label]::new(); $label.Bounds=[Drawing.Rectangle]::new(20,15,420,45)
    $bar=[Windows.Forms.ProgressBar]::new(); $bar.Bounds=[Drawing.Rectangle]::new(20,65,420,18); $bar.Style='Marquee'
    $cancel=[Windows.Forms.Button]::new(); $cancel.Bounds=[Drawing.Rectangle]::new(320,98,120,30); $cancel.Text=Get-RuntimeText $Language 'Cancel' 'ยกเลิก'
    $progress.Controls.AddRange(@($label,$bar,$cancel))
    $state=@{Token=[Threading.CancellationTokenSource]::new();Label=$label;Progress=$bar;Cancel=$cancel;Installing=$false;KeepDownload=$false;UserCanceled=$false}
    $cancel.Add_Click({ $state.UserCanceled=$true; $state.Token.Cancel() }.GetNewClosure())
    $progress.Add_FormClosing({param($sender,$event) $event.Cancel=$true; if (-not $state.Installing) {$state.UserCanceled=$true; $state.Token.Cancel()} }.GetNewClosure())
    $operations=@{
        Detect={
            [Windows.Forms.Application]::DoEvents()
            $state.Token.Token.ThrowIfCancellationRequested()
            $ready=Get-DotNetRuntimeStatus $requirements
            [Windows.Forms.Application]::DoEvents()
            $state.Token.Token.ThrowIfCancellationRequested()
            return $ready
        }
        Consent={
            $form=New-DotNetConsentForm $ProductName $Language $minimum
            try { $answer=$form.ShowDialog(); if ($answer -eq 'OK') { $progress.Show(); return $true }; return $false }
            finally { $form.Dispose() }
        }
        Acquire={ Get-VerifiedRuntimeDownload $requirements $state $Language }
        Signature={param($path)
            $state.Label.Text=Get-RuntimeText $Language 'Verifying the Microsoft runtime installer...' 'กำลังตรวจสอบตัวติดตั้ง Runtime ของ Microsoft...'
            [Windows.Forms.Application]::DoEvents()
            $state.Token.Token.ThrowIfCancellationRequested()
            Test-MicrosoftBinary $path
        }
        Install={param($path) $state.Token.Token.ThrowIfCancellationRequested(); Start-DotNetRuntimeInstaller $path $state $Language}
        Cleanup={param($artifact) if (-not $state.KeepDownload) { Remove-RuntimeTemporaryDirectory $artifact.directory } }
    }
    try { return Invoke-DotNetPrerequisite $requirements $operations }
    catch {
        if ($state.UserCanceled) { return 'Canceled' }
        $message=Get-RuntimeText $Language 'Unable to prepare .NET. No application files were changed. Install .NET Desktop Runtime 10 x64 manually and rerun setup.' 'ไม่สามารถเตรียม .NET ได้ ยังไม่ได้เปลี่ยนไฟล์โปรแกรม กรุณาติดตั้ง .NET Desktop Runtime 10 x64 แล้วเรียกตัวติดตั้งอีกครั้ง'
        throw "$message`n`n$($_.Exception.Message)`nhttps://dotnet.microsoft.com/en-us/download/dotnet/10.0"
    } finally { $progress.Dispose(); $state.Token.Dispose() }
}
