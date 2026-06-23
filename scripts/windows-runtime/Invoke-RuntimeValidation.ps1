param(
    [string]$OutputDirectory = "artifacts/windows-runtime-validation",

    [int]$AppPid = 0,

    [switch]$SelfTest
)

$ErrorActionPreference = 'Continue'

function New-StepResult {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Command,
        [Parameter(Mandatory = $true)][int]$ExitCode,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$LogPath,
        [string]$Status = $(if ($ExitCode -eq 0) { 'PASS' } else { 'FAIL' })
    )

    [pscustomobject]@{
        name = $Name
        command = $Command
        exitCode = $ExitCode
        status = $Status
        logPath = $LogPath
    }
}

function Invoke-LoggedCommand {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$LogFileName,
        [Parameter(Mandatory = $true)][scriptblock]$Script
    )

    $logPath = Join-Path $OutputDirectory $LogFileName
    $commandText = $Script.ToString().Trim()
    $output = & $Script 2>&1
    $exitCode = $LASTEXITCODE
    if ($null -eq $exitCode) {
        $exitCode = 0
    }

    $output | Out-File $logPath -Encoding utf8
    New-StepResult -Name $Name -Command $commandText -ExitCode $exitCode -LogPath $logPath
}

function Get-ValidationSummary {
    param([Parameter(Mandatory = $true)]$Steps)

    [pscustomobject]@{
        passed = @($Steps | Where-Object status -eq 'PASS').Count
        failed = @($Steps | Where-Object status -eq 'FAIL').Count
        skipped = @($Steps | Where-Object status -eq 'SKIP').Count
    }
}

function Invoke-SelfTest {
    $steps = @(
        New-StepResult -Name 'audio' -Command 'dotnet test audio' -ExitCode 0 -LogPath 'audio.log'
        New-StepResult -Name 'ui' -Command 'ui smoke' -ExitCode 0 -LogPath 'ui.log' -Status 'SKIP'
    )
    $summary = Get-ValidationSummary -Steps $steps
    if ($summary.passed -ne 1 -or $summary.failed -ne 0 -or $summary.skipped -ne 1) {
        throw 'Invoke-RuntimeValidation.ps1 self-test failed.'
    }

    Write-Host 'Invoke-RuntimeValidation.ps1 self-test passed.'
}

if ($SelfTest) {
    Invoke-SelfTest
    exit 0
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$steps = [System.Collections.Generic.List[object]]::new()

$steps.Add((Invoke-LoggedCommand `
    -Name 'Audio channel validation' `
    -LogFileName '01-audio-tests.log' `
    -Script { dotnet test tests\KoromoEventScript.Runtime.Windows.Tests\KoromoEventScript.Runtime.Windows.Tests.csproj --filter "FullyQualifiedName~AudioChannelServiceTests" --logger "console;verbosity=minimal" })) | Out-Null

$steps.Add((Invoke-LoggedCommand `
    -Name 'Full-command sample runtime package validation' `
    -LogFileName '02-runtime-package-tests.log' `
    -Script { dotnet test tests\KoromoEventScript.Runtime.Core.Tests\KoromoEventScript.Runtime.Core.Tests.csproj --filter "FullyQualifiedName~Resolve_WithFullCommandSampleBuildOutput" --logger "console;verbosity=minimal" })) | Out-Null

$steps.Add((Invoke-LoggedCommand `
    -Name 'CLI build run publish validation' `
    -LogFileName '03-cli-run-publish-tests.log' `
    -Script { dotnet test tests\KoromoEventScript.Cli.Tests\KoromoEventScript.Cli.Tests.csproj --filter "FullyQualifiedName~BuildRuntimeManifestTests|FullyQualifiedName~RunCommandTests|FullyQualifiedName~PublishCommandTests" --logger "console;verbosity=minimal" })) | Out-Null

if ($AppPid -gt 0) {
    $uiOutput = Join-Path $OutputDirectory 'ui-smoke'
    $steps.Add((Invoke-LoggedCommand `
        -Name 'WinUI smoke validation' `
        -LogFileName '04-winui-smoke.log' `
        -Script { powershell -NoProfile -ExecutionPolicy Bypass -File scripts\windows-runtime\Run-WinUiSmokeTests.ps1 -AppPid $AppPid -OutputDirectory $uiOutput })) | Out-Null
}
else {
    $steps.Add((New-StepResult `
        -Name 'WinUI smoke validation' `
        -Command 'Run-WinUiSmokeTests.ps1 -AppPid <running app pid>' `
        -ExitCode 0 `
        -LogPath '' `
        -Status 'SKIP')) | Out-Null
}

$summary = Get-ValidationSummary -Steps $steps
$report = [pscustomobject]@{
    outputDirectory = (Resolve-Path $OutputDirectory).Path
    summary = $summary
    steps = $steps
}

$report | ConvertTo-Json -Depth 5 | Out-File (Join-Path $OutputDirectory 'test-results.json') -Encoding utf8
Write-Host ("Passed: {0} | Failed: {1} | Skipped: {2}" -f $summary.passed, $summary.failed, $summary.skipped)

if ($summary.failed -gt 0) {
    exit 1
}

exit 0
