[CmdletBinding()]
param(
    [string]$Version = "0.1.0",
    [string[]]$RuntimeIdentifiers = @("win-x64"),
    [string]$OutputRoot = "artifacts/release/cli"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.."))
$projectPath = Join-Path $repositoryRoot "source/cli/KoromoEventScript.Cli/KoromoEventScript.Cli.csproj"
$licensePath = Join-Path $repositoryRoot "LICENSE"
$readmePath = Join-Path $repositoryRoot "source/cli/README.md"
$repositoryPrefix = $repositoryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
$resolvedOutputRoot = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    [System.IO.Path]::GetFullPath($OutputRoot)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputRoot))
}
if (!$resolvedOutputRoot.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputRoot must stay inside the repository: $resolvedOutputRoot"
}
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "Version must be a SemVer release version: $Version"
}

New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null
$checksumLines = [System.Collections.Generic.List[string]]::new()

foreach ($runtimeIdentifier in $RuntimeIdentifiers) {
    if ($runtimeIdentifier -notmatch '^[a-z0-9]+-[a-z0-9]+$') {
        throw "Invalid runtime identifier: $runtimeIdentifier"
    }

    $packageName = "kes-$Version-$runtimeIdentifier"
    $packageRoot = Join-Path $resolvedOutputRoot $packageName
    $archivePath = Join-Path $resolvedOutputRoot "$packageName.zip"

    if (Test-Path -LiteralPath $packageRoot) {
        Remove-Item -LiteralPath $packageRoot -Recurse -Force
    }
    if (Test-Path -LiteralPath $archivePath) {
        Remove-Item -LiteralPath $archivePath -Force
    }

    dotnet publish $projectPath `
        --configuration Release `
        --runtime $runtimeIdentifier `
        --self-contained true `
        --output $packageRoot `
        -p:Version=$Version `
        -p:PublishSingleFile=true `
        -p:DebugType=None `
        -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $runtimeIdentifier."
    }

    Copy-Item -LiteralPath $readmePath -Destination (Join-Path $packageRoot "README.md")
    Copy-Item -LiteralPath $licensePath -Destination (Join-Path $packageRoot "LICENSE")
    Compress-Archive -LiteralPath $packageRoot -DestinationPath $archivePath -CompressionLevel Optimal

    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath
    $checksumLines.Add("$($hash.Hash.ToLowerInvariant())  $([System.IO.Path]::GetFileName($archivePath))")
}

$checksumPath = Join-Path $resolvedOutputRoot "SHA256SUMS.txt"
[System.IO.File]::WriteAllLines($checksumPath, $checksumLines, [System.Text.UTF8Encoding]::new($false))
Write-Host "KES CLI release artifacts: $resolvedOutputRoot"
