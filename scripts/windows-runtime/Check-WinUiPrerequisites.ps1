param(
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'

$MinimumDotNetMajor = 8
$MinimumWinAppVersion = [version]'0.3'

function New-PrerequisiteStatus {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][bool]$Ok,
        [Parameter(Mandatory = $true)][string]$Found,
        [Parameter(Mandatory = $true)][string]$Required,
        [Parameter(Mandatory = $true)][string]$Remediation,
        [bool]$BlocksImplementation = $true
    )

    [pscustomobject]@{
        Name = $Name
        Ok = $Ok
        Found = $Found
        Required = $Required
        Remediation = $Remediation
        BlocksImplementation = $BlocksImplementation
    }
}

function Get-DotNetSdkStatus {
    $sdks = @()
    try {
        $sdks = (& dotnet --list-sdks 2>$null) -replace ' \[.*$', ''
    }
    catch {
        $sdks = @()
    }

    $matchingSdk = $null
    foreach ($sdk in $sdks) {
        $versionText = ($sdk -split '-')[0]
        try {
            $version = [version]$versionText
            if ($version.Major -ge $MinimumDotNetMajor) {
                $matchingSdk = $sdk
                break
            }
        }
        catch {
        }
    }

    $found = if ($matchingSdk) { $matchingSdk } elseif ($sdks.Count -gt 0) { ($sdks -join ', ') } else { 'missing' }
    New-PrerequisiteStatus `
        -Name '.NET SDK' `
        -Ok ([bool]$matchingSdk) `
        -Found $found `
        -Required ".NET SDK >= $MinimumDotNetMajor.0" `
        -Remediation 'Run the WinUI setup flow to install Microsoft.DotNet.SDK.10 if no SDK >= 8.0 is present.'
}

function ConvertTo-WinAppCliVersion {
    param([string[]]$Output)

    foreach ($line in $Output) {
        $match = [regex]::Match($line, '(?<!\d)(\d+\.\d+(?:\.\d+)?(?:\.\d+)?)(?!\d)')
        if ($match.Success) {
            try {
                return [version]$match.Groups[1].Value
            }
            catch {
            }
        }
    }

    return $null
}

function Get-WinAppCliStatus {
    $command = Get-Command winapp -ErrorAction SilentlyContinue
    $version = $null
    $ok = $false

    if ($command) {
        try {
            $raw = @(& winapp --version 2>$null)
            $version = ConvertTo-WinAppCliVersion -Output $raw
            $ok = $version -and $version -ge $MinimumWinAppVersion
        }
        catch {
            $version = $null
        }
    }

    $found = if ($version) { $version.ToString() } elseif ($command) { 'installed, version unknown' } else { 'missing' }
    New-PrerequisiteStatus `
        -Name 'WinApp CLI' `
        -Ok $ok `
        -Found $found `
        -Required "winapp >= $MinimumWinAppVersion" `
        -Remediation 'Run the WinUI setup flow to install or upgrade Microsoft.WinAppCLI.'
}

function Get-WinUiTemplateStatus {
    $ok = $false
    try {
        $ok = [bool](dotnet new list winui 2>$null | Select-String 'winui-mvvm' -Quiet)
    }
    catch {
        $ok = $false
    }

    New-PrerequisiteStatus `
        -Name 'WinUI 3 templates' `
        -Ok $ok `
        -Found $(if ($ok) { 'winui-mvvm template found' } else { 'missing' }) `
        -Required 'Microsoft.WindowsAppSDK.WinUI.CSharp.Templates installed' `
        -Remediation 'Run the WinUI setup flow to install or update Microsoft.WindowsAppSDK.WinUI.CSharp.Templates.'
}

function Get-DeveloperModeStatus {
    $enabled = $false
    try {
        $value = (Get-ItemProperty `
            -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' `
            -Name AllowDevelopmentWithoutDevLicense `
            -ErrorAction SilentlyContinue).AllowDevelopmentWithoutDevLicense
        $enabled = $value -eq 1
    }
    catch {
        $enabled = $false
    }

    New-PrerequisiteStatus `
        -Name 'Developer Mode' `
        -Ok $enabled `
        -Found $(if ($enabled) { 'enabled' } else { 'disabled' }) `
        -Required 'AllowDevelopmentWithoutDevLicense = 1' `
        -Remediation 'Stop implementation and run the WinUI setup flow; enable Developer Mode before building or launching the Windows runtime.'
}

function Get-WinUiPrerequisiteStatuses {
    @(
        Get-DotNetSdkStatus
        Get-WinAppCliStatus
        Get-WinUiTemplateStatus
        Get-DeveloperModeStatus
    )
}

function Format-StatusLine {
    param([Parameter(Mandatory = $true)]$Status)

    $state = if ($Status.Ok) { '[OK]' } else { '[FAIL]' }
    $blocking = if (-not $Status.Ok -and $Status.BlocksImplementation) { 'BLOCKS' } else { 'ready' }
    '{0,-20} {1,-6} found: {2,-32} required: {3} ({4})' -f $Status.Name, $state, $Status.Found, $Status.Required, $blocking
}

function Get-WinUiPrerequisiteReport {
    param([Parameter(Mandatory = $true)]$Statuses)

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('==== Windows runtime WinUI prerequisites ====')
    foreach ($status in $Statuses) {
        $lines.Add((Format-StatusLine -Status $status))
    }
    $lines.Add('')
    $lines.Add('Developer Mode policy:')
    $lines.Add('  If Developer Mode is disabled, stop implementation and run the WinUI setup flow before continuing.')
    $lines.Add('')
    $lines.Add('Build and launch policy:')
    $lines.Add('  Build and launch the Windows runtime with winapp run or a BuildAndRun.ps1-equivalent script.')
    $lines.Add('  Do not run the packaged .exe directly; direct packaged exe launch can silently exit and hide WinUI diagnostics.')
    $lines.Add('')
    $lines.Add('Setup remediation:')
    $lines.Add('  Use the WinUI setup flow to install/upgrade .NET SDK, WinApp CLI, WinUI 3 templates, and enable Developer Mode.')
    $lines -join [Environment]::NewLine
}

function Get-PrerequisiteExitCode {
    param([Parameter(Mandatory = $true)]$Statuses)

    $blockingFailure = $Statuses | Where-Object { -not $_.Ok -and $_.BlocksImplementation } | Select-Object -First 1
    if ($blockingFailure) {
        return 1
    }

    return 0
}

function Assert-SelfTest {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) {
        throw "SelfTest failed: $Message"
    }
}

function Invoke-SelfTest {
    $allOkStatuses = @(
        New-PrerequisiteStatus -Name '.NET SDK' -Ok $true -Found '10.0.100' -Required '.NET SDK >= 8.0' -Remediation 'unused'
        New-PrerequisiteStatus -Name 'WinApp CLI' -Ok $true -Found '0.4.0' -Required 'winapp >= 0.3' -Remediation 'unused'
        New-PrerequisiteStatus -Name 'WinUI 3 templates' -Ok $true -Found 'winui-mvvm template found' -Required 'templates installed' -Remediation 'unused'
        New-PrerequisiteStatus -Name 'Developer Mode' -Ok $true -Found 'enabled' -Required 'AllowDevelopmentWithoutDevLicense = 1' -Remediation 'unused'
    )

    $report = Get-WinUiPrerequisiteReport -Statuses $allOkStatuses
    Assert-SelfTest ($report -match '\.NET SDK') 'report includes .NET SDK status'
    Assert-SelfTest ($report -match 'WinApp CLI') 'report includes WinApp CLI status'
    Assert-SelfTest ($report -match 'WinUI 3 templates') 'report includes WinUI 3 templates status'
    Assert-SelfTest ($report -match 'Developer Mode') 'report includes Developer Mode status'
    Assert-SelfTest ($report -match 'winapp run') 'report documents winapp run launch'
    Assert-SelfTest ($report -match 'BuildAndRun\.ps1') 'report documents BuildAndRun.ps1-equivalent launch'
    Assert-SelfTest ($report -match 'Do not run the packaged \.exe directly') 'report forbids packaged exe direct launch'
    Assert-SelfTest ((Get-PrerequisiteExitCode -Statuses $allOkStatuses) -eq 0) 'all-ok statuses exit with success'
    Assert-SelfTest ((ConvertTo-WinAppCliVersion -Output @('Windows App Development CLI - Version 0.3.2', '0.3.2')) -eq [version]'0.3.2') 'parses winapp banner version output'

    $developerModeOff = @(
        New-PrerequisiteStatus -Name '.NET SDK' -Ok $true -Found '10.0.100' -Required '.NET SDK >= 8.0' -Remediation 'unused'
        New-PrerequisiteStatus -Name 'WinApp CLI' -Ok $true -Found '0.4.0' -Required 'winapp >= 0.3' -Remediation 'unused'
        New-PrerequisiteStatus -Name 'WinUI 3 templates' -Ok $true -Found 'winui-mvvm template found' -Required 'templates installed' -Remediation 'unused'
        New-PrerequisiteStatus -Name 'Developer Mode' -Ok $false -Found 'disabled' -Required 'AllowDevelopmentWithoutDevLicense = 1' -Remediation 'Run /winui-setup'
    )

    $developerModeOffReport = Get-WinUiPrerequisiteReport -Statuses $developerModeOff
    Assert-SelfTest ($developerModeOffReport -match 'Developer Mode policy') 'report includes Developer Mode stop policy'
    Assert-SelfTest ((Get-PrerequisiteExitCode -Statuses $developerModeOff) -eq 1) 'disabled Developer Mode fails the check'

    Write-Host 'Check-WinUiPrerequisites.ps1 self-test passed.'
}

if ($SelfTest) {
    Invoke-SelfTest
    exit 0
}

$statuses = Get-WinUiPrerequisiteStatuses
Write-Host (Get-WinUiPrerequisiteReport -Statuses $statuses)

$exitCode = Get-PrerequisiteExitCode -Statuses $statuses
if ($exitCode -ne 0) {
    Write-Host ''
    Write-Host 'Prerequisite check failed. Do not continue Windows runtime implementation until the failed items are resolved.'
    foreach ($status in ($statuses | Where-Object { -not $_.Ok })) {
        Write-Host ("- {0}: {1}" -f $status.Name, $status.Remediation)
    }
}

exit $exitCode
