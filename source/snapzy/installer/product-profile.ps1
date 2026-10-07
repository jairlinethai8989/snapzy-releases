function Get-ProductProfile([string]$Flavor) {
    if ($Flavor -and $Flavor -ne 'Snapzy') { throw "Unsupported product '$Flavor'. This source builds SnapZy only." }
    return [ordered]@{
        Flavor = 'Snapzy'; Name = 'SnapZy'; Version = '1.0.0'
        InstallFolder = 'Snapzy'; DataFolder = 'Snapzy'
        StartupValue = 'Snapzy'; UninstallKey = 'Snapzy'
        InstanceId = '6A2D75A0-73EE-4B83-9D4F-C3E14C7D5C10'
        Language = 'en'; IconName = 'Snapzy-1.0.0-A.ico'
        OutputFile = 'SnapZy-Setup-1.0.0.exe'
        PublishFolder = 'publish-Snapzy-1.0.0'; StagingFolder = 'setup-staging-Snapzy-1.0.0'
    }
}
