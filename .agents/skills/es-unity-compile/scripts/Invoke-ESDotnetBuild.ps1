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
$results = New-Object Collections.Generic.List[object]

foreach ($item in $Project) {
    $projectPath = if ([IO.Path]::IsPathRooted($item)) { $item } else { Join-Path $ProjectRoot $item }
    $projectPath = [IO.Path]::GetFullPath($projectPath)
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
