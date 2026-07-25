[CmdletBinding()]
param(
    [string]$PackageReference,
    [string]$UnityCommand = "unity.exe",
    [string]$WorkRoot = "artifacts/unity-release-validation"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.."))
$resolvedWorkRoot = if ([System.IO.Path]::IsPathRooted($WorkRoot)) {
    [System.IO.Path]::GetFullPath($WorkRoot)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $WorkRoot))
}
$repositoryPrefix = $repositoryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (!$resolvedWorkRoot.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "WorkRoot must stay inside the repository: $resolvedWorkRoot"
}

if ([string]::IsNullOrWhiteSpace($PackageReference)) {
    $packagePath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "source/extension/unity/Package"))
    $PackageReference = "file:" + $packagePath.Replace('\', '/')
}

if (Test-Path -LiteralPath $resolvedWorkRoot) {
    Remove-Item -LiteralPath $resolvedWorkRoot -Recurse -Force
}

$assetsEditor = Join-Path $resolvedWorkRoot "Assets/Editor"
$packagesRoot = Join-Path $resolvedWorkRoot "Packages"
$projectSettingsRoot = Join-Path $resolvedWorkRoot "ProjectSettings"
New-Item -ItemType Directory -Force -Path $assetsEditor, $packagesRoot, $projectSettingsRoot | Out-Null
Copy-Item `
    -LiteralPath (Join-Path $PSScriptRoot "ReleaseValidation/KesReleaseValidation.cs") `
    -Destination (Join-Path $assetsEditor "KesReleaseValidation.cs")

$manifest = [ordered]@{
    dependencies = [ordered]@{
        "com.koromosoft.koromo-event-script" = $PackageReference
    }
}
$manifestJson = $manifest | ConvertTo-Json -Depth 5
[System.IO.File]::WriteAllText(
    (Join-Path $packagesRoot "manifest.json"),
    $manifestJson,
    [System.Text.UTF8Encoding]::new($false))
[System.IO.File]::WriteAllText(
    (Join-Path $projectSettingsRoot "ProjectVersion.txt"),
    "m_EditorVersion: 6000.5.3f1`nm_EditorVersionWithRevision: 6000.5.3f1 (c2eb47b3a2a9)`n",
    [System.Text.UTF8Encoding]::new($false))

& $UnityCommand --non-interactive run $resolvedWorkRoot --timeout 600 -- -executeMethod KesReleaseValidation.ImportSample -nographics
if ($LASTEXITCODE -ne 0) {
    throw "Clean project package/sample import failed."
}

& $UnityCommand --non-interactive run $resolvedWorkRoot --timeout 900 -- -executeMethod KesReleaseValidation.ValidateAndBuild -nographics
if ($LASTEXITCODE -ne 0) {
    throw "Clean project validation or Windows Player build failed."
}

$resultPath = Join-Path $resolvedWorkRoot "release-validation.json"
if (!(Test-Path -LiteralPath $resultPath)) {
    throw "Release validation result was not generated."
}

Get-Content -Raw -LiteralPath $resultPath
