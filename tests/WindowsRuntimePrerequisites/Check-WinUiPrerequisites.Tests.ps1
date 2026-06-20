$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$scriptPath = Join-Path $repoRoot 'scripts\windows-runtime\Check-WinUiPrerequisites.ps1'

if (-not (Test-Path -LiteralPath $scriptPath)) {
    throw "Missing prerequisite check script: $scriptPath"
}

& $scriptPath -SelfTest
if ($LASTEXITCODE -ne 0) {
    throw "Prerequisite check script self-test failed with exit code $LASTEXITCODE"
}
