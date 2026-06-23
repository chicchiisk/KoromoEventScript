param(
    [Parameter(Mandatory = $true)]
    [int]$AppPid,

    [string]$OutputDirectory = "artifacts/windows-runtime-ui",

    [switch]$SelfTest
)

$ErrorActionPreference = 'Continue'

function New-Result {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Status,
        [string]$Detail = ''
    )

    [pscustomobject]@{
        name = $Name
        status = $Status
        detail = $Detail
    }
}

function Invoke-UiTest {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Script
    )

    try {
        $output = & $Script 2>&1
        if ($LASTEXITCODE -eq 0) {
            $script:Results.Add((New-Result -Name $Name -Status 'PASS')) | Out-Null
        }
        else {
            $script:Results.Add((New-Result -Name $Name -Status 'FAIL' -Detail ($output -join [Environment]::NewLine))) | Out-Null
        }
    }
    catch {
        $script:Results.Add((New-Result -Name $Name -Status 'FAIL' -Detail $_.Exception.Message)) | Out-Null
    }
}

function Invoke-OptionalUiTest {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Script
    )

    try {
        $output = & $Script 2>&1
        if ($LASTEXITCODE -eq 0) {
            $script:Results.Add((New-Result -Name $Name -Status 'PASS')) | Out-Null
        }
        else {
            $script:Results.Add((New-Result -Name $Name -Status 'SKIP' -Detail ($output -join [Environment]::NewLine))) | Out-Null
        }
    }
    catch {
        $script:Results.Add((New-Result -Name $Name -Status 'SKIP' -Detail $_.Exception.Message)) | Out-Null
    }
}

function Get-TestSummary {
    param([Parameter(Mandatory = $true)]$Results)

    [pscustomobject]@{
        passed = @($Results | Where-Object status -eq 'PASS').Count
        failed = @($Results | Where-Object status -eq 'FAIL').Count
        skipped = @($Results | Where-Object status -eq 'SKIP').Count
    }
}

function Invoke-SelfTest {
    $sample = [System.Collections.Generic.List[object]]::new()
    $sample.Add((New-Result -Name 'required' -Status 'PASS')) | Out-Null
    $sample.Add((New-Result -Name 'optional' -Status 'SKIP')) | Out-Null
    $summary = Get-TestSummary -Results $sample
    if ($summary.passed -ne 1 -or $summary.failed -ne 0 -or $summary.skipped -ne 1) {
        throw 'Run-WinUiSmokeTests.ps1 self-test failed.'
    }

    Write-Host 'Run-WinUiSmokeTests.ps1 self-test passed.'
}

if ($SelfTest) {
    Invoke-SelfTest
    exit 0
}

if (-not (Get-Command winapp -ErrorAction SilentlyContinue)) {
    Write-Error 'winapp CLI was not found. Run scripts/windows-runtime/Check-WinUiPrerequisites.ps1 first.'
    exit 1
}

$script:Results = [System.Collections.Generic.List[object]]::new()
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$screenshotDirectory = Join-Path $OutputDirectory 'screenshots'
New-Item -ItemType Directory -Force -Path $screenshotDirectory | Out-Null

Invoke-UiTest 'App exposes a main window' {
    $windows = winapp ui list-windows -a $AppPid --json 2>$null | ConvertFrom-Json
    $main = @($windows | Where-Object { $_.title -ne 'PopupHost' } | Select-Object -First 1)
    if ($main.Count -eq 0) {
        throw 'No non-PopupHost window was found.'
    }
}

Invoke-UiTest 'System menu button exists' {
    winapp ui wait-for 'RuntimeSystemMenuButton' -a $AppPid -t 3000
}

Invoke-UiTest 'Initial screenshot captured' {
    winapp ui screenshot -a $AppPid -o (Join-Path $screenshotDirectory '01-initial.png')
}

Invoke-UiTest 'System menu opens' {
    winapp ui invoke 'RuntimeSystemMenuButton' -a $AppPid
    Start-Sleep -Milliseconds 300
    winapp ui wait-for 'RuntimeMenuSave' -a $AppPid -t 3000
}

foreach ($menuId in @('RuntimeMenuSave', 'RuntimeMenuLoad', 'RuntimeMenuSettings', 'RuntimeMenuTitle', 'RuntimeMenuExit')) {
    $currentMenuId = $menuId
    Invoke-UiTest "$currentMenuId menu item exists" ({
        winapp ui wait-for $currentMenuId -a $AppPid -t 3000
    }.GetNewClosure())
}

Invoke-UiTest 'System menu screenshot captured' {
    winapp ui screenshot -a $AppPid -o (Join-Path $screenshotDirectory '02-system-menu.png')
}

Invoke-OptionalUiTest 'Message window visible when runtime state exposes text' {
    winapp ui wait-for 'RuntimeMessageWindow' -a $AppPid -t 500
}

Invoke-OptionalUiTest 'Choice list visible when runtime state exposes choices' {
    winapp ui wait-for 'RuntimeChoiceList' -a $AppPid -t 500
}

Invoke-OptionalUiTest 'Backlog panel visible when runtime state opens backlog' {
    winapp ui wait-for 'RuntimeBacklogPanel' -a $AppPid -t 500
}

Invoke-UiTest 'Interactive app controls expose AutomationId' {
    $inspection = winapp ui inspect -a $AppPid --interactive --json 2>$null | ConvertFrom-Json
    $controls = @($inspection.elements | Where-Object {
        $_.type -match 'Button|List|MenuItem|Text|Edit|ComboBox|CheckBox|ToggleSwitch' -and
        $_.name -notmatch 'Minimize|Maximize|Close|System' -and
        $_.className -notmatch 'PickerHost|#32770|CabinetWClass'
    })
    $missing = @($controls | Where-Object { -not $_.automationId })
    if ($missing.Count -gt 0) {
        $names = ($missing | ForEach-Object { "$($_.type) '$($_.name)'" }) -join ', '
        throw "Missing AutomationId: $names"
    }
}

$summary = Get-TestSummary -Results $script:Results
$report = [pscustomobject]@{
    appPid = $AppPid
    outputDirectory = (Resolve-Path $OutputDirectory).Path
    summary = $summary
    results = $script:Results
}

$report | ConvertTo-Json -Depth 6 | Out-File (Join-Path $OutputDirectory 'test-results.json') -Encoding utf8
Write-Host ("Passed: {0} | Failed: {1} | Skipped: {2}" -f $summary.passed, $summary.failed, $summary.skipped)

if ($summary.failed -gt 0) {
    exit 1
}

exit 0
