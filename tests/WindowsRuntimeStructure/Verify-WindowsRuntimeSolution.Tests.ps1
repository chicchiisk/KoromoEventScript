param()

$ErrorActionPreference = 'Stop'

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$solutionPath = Join-Path $repositoryRoot 'KoromoEventScript.slnx'
$coreProject = Join-Path $repositoryRoot 'source\runtime\KoromoEventScript.Runtime.Core\KoromoEventScript.Runtime.Core.csproj'
$windowsProject = Join-Path $repositoryRoot 'source\runtime\KoromoEventScript.Runtime.Windows\KoromoEventScript.Runtime.Windows.csproj'
$coreTestsProject = Join-Path $repositoryRoot 'tests\KoromoEventScript.Runtime.Core.Tests\KoromoEventScript.Runtime.Core.Tests.csproj'
$windowsTestsProject = Join-Path $repositoryRoot 'tests\KoromoEventScript.Runtime.Windows.Tests\KoromoEventScript.Runtime.Windows.Tests.csproj'

function Assert-FileExists {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Missing $Description at $Path"
    }
}

function Assert-XmlValue {
    param(
        [Parameter(Mandatory = $true)][xml]$Project,
        [Parameter(Mandatory = $true)][string]$XPath,
        [Parameter(Mandatory = $true)][string]$Expected,
        [Parameter(Mandatory = $true)][string]$Description
    )

    $node = $Project.SelectSingleNode($XPath)
    $actual = if ($node) { $node.InnerText } else { $null }
    if ($actual -ne $Expected) {
        throw "$Description expected '$Expected' but found '$actual'"
    }
}

Assert-FileExists -Path $solutionPath -Description 'solution'
Assert-FileExists -Path $coreProject -Description 'runtime core project'
Assert-FileExists -Path $windowsProject -Description 'Windows runtime project'
Assert-FileExists -Path $coreTestsProject -Description 'runtime core tests project'
Assert-FileExists -Path $windowsTestsProject -Description 'Windows runtime tests project'

$solution = Get-Content -Raw -LiteralPath $solutionPath
foreach ($path in @(
        'source/runtime/KoromoEventScript.Runtime.Core/KoromoEventScript.Runtime.Core.csproj',
        'source/runtime/KoromoEventScript.Runtime.Windows/KoromoEventScript.Runtime.Windows.csproj',
        'tests/KoromoEventScript.Runtime.Core.Tests/KoromoEventScript.Runtime.Core.Tests.csproj',
        'tests/KoromoEventScript.Runtime.Windows.Tests/KoromoEventScript.Runtime.Windows.Tests.csproj')) {
    if ($solution -notmatch [regex]::Escape($path)) {
        throw "Solution does not include $path"
    }
}

[xml]$coreXml = Get-Content -Raw -LiteralPath $coreProject
Assert-XmlValue -Project $coreXml -XPath '/Project/PropertyGroup/TargetFramework' -Expected 'net10.0' -Description 'Runtime Core TargetFramework'

[xml]$windowsXml = Get-Content -Raw -LiteralPath $windowsProject
$platformsNode = $windowsXml.SelectSingleNode('/Project/PropertyGroup/Platforms')
$platforms = if ($platformsNode) { $platformsNode.InnerText } else { $null }
if ($platforms -notmatch 'x64' -or $platforms -notmatch 'ARM64') {
    throw "Windows runtime Platforms must include x64 and ARM64, but found '$platforms'"
}

$runtimeIdentifiersNode = $windowsXml.SelectSingleNode('/Project/PropertyGroup/RuntimeIdentifiers')
$runtimeIdentifiers = if ($runtimeIdentifiersNode) { $runtimeIdentifiersNode.InnerText } else { $null }
if ($runtimeIdentifiers -notmatch 'win-x64' -or $runtimeIdentifiers -notmatch 'win-arm64') {
    throw "Windows runtime RuntimeIdentifiers must include win-x64 and win-arm64, but found '$runtimeIdentifiers'"
}

$anyCpuRuntimeIdentifier = $windowsXml.Project.PropertyGroup.RuntimeIdentifier |
    Where-Object { $_.Condition -eq "'`$(RuntimeIdentifier)' == '' and '`$(Platform)' == 'AnyCPU'" } |
    Select-Object -First 1
if (-not $anyCpuRuntimeIdentifier -or $anyCpuRuntimeIdentifier.InnerText -ne 'win-x64') {
    throw 'Windows runtime must map the solution AnyCPU configuration to win-x64 explicitly'
}

$anyCpuPlatformTarget = $windowsXml.Project.PropertyGroup.PlatformTarget |
    Where-Object { $_.Condition -eq "'`$(Platform)' == 'AnyCPU'" } |
    Select-Object -First 1
if (-not $anyCpuPlatformTarget -or $anyCpuPlatformTarget.InnerText -ne 'x64') {
    throw 'Windows runtime must map the solution AnyCPU configuration to x64 explicitly'
}

[xml]$coreTestsXml = Get-Content -Raw -LiteralPath $coreTestsProject
if (-not ($coreTestsXml.Project.ItemGroup.ProjectReference.Include -match 'KoromoEventScript.Runtime.Core.csproj')) {
    throw 'Runtime Core tests must reference Runtime Core project'
}

[xml]$windowsTestsXml = Get-Content -Raw -LiteralPath $windowsTestsProject
$windowsTestsTargetFramework = $windowsTestsXml.SelectSingleNode('/Project/PropertyGroup/TargetFramework').InnerText
if ($windowsTestsTargetFramework -ne 'net10.0-windows10.0.26100.0') {
    throw "Windows Runtime tests must target net10.0-windows10.0.26100.0, but found '$windowsTestsTargetFramework'"
}

if (-not ($windowsTestsXml.Project.ItemGroup.ProjectReference.Include -match 'KoromoEventScript.Runtime.Core.csproj')) {
    throw 'Windows Runtime tests must reference Runtime Core project'
}

Write-Host 'Windows runtime solution structure test passed.'
