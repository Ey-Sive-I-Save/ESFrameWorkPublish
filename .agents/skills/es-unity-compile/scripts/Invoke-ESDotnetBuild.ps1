[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]]$Project,
    [string]$ProjectRoot,
    [switch]$Restore,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new()

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (& git rev-parse --show-toplevel 2>$null)
}
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    throw 'Cannot resolve the Git project root. Pass -ProjectRoot.'
}

$ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot.Trim())
$rootNormalized = $ProjectRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
$results = New-Object Collections.Generic.List[object]

foreach ($item in $Project) {
    if ([IO.Path]::IsPathRooted($item)) {
        $projectPath = [IO.Path]::GetFullPath($item)
    }
    else {
        if ($item -match '(^|[\\/])\.\.([\\/]|$)') { throw 'Project path cannot contain parent traversal.' }
        $projectPath = [IO.Path]::GetFullPath((Join-Path $ProjectRoot $item))
    }
    if (-not ($projectPath.Equals($rootNormalized, [StringComparison]::OrdinalIgnoreCase) -or $projectPath.StartsWith($rootNormalized + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))) {
        throw "Project path escapes ProjectRoot: $item"
    }
    if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
        $results.Add([pscustomobject]@{ project = $item; exitCode = -1; succeeded = $false; output = 'Project file does not exist.' })
        continue
    }

    $arguments = @('build', $projectPath)
    if (-not $Restore) { $arguments += '--no-restore' }
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $output = @(& dotnet @arguments 2>&1)
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = $previousErrorActionPreference
    $results.Add([pscustomobject]@{
        project = $projectPath
        exitCode = $exitCode
        succeeded = $exitCode -eq 0
        output = ($output -join [Environment]::NewLine)
    })
}

$failed = @($results | Where-Object { -not $_.succeeded })
$report = [pscustomobject]@{
    evidence = 'dotnet-build'
    warning = 'This result does not replace Unity Editor, Test Runner, PlayMode, Profiler, IL2CPP, or release validation.'
    projectRoot = $ProjectRoot
    succeeded = $failed.Count -eq 0
    results = $results.ToArray()
}

if ($Json) { $report | ConvertTo-Json -Depth 6 } else { $report.results | Format-List }
if (-not $report.succeeded) { exit 1 }
