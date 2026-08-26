[CmdletBinding()]
param([string]$ProjectRoot)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) { $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path }
$test = Join-Path $ProjectRoot 'ES/Automation/TaskContextRuntime/Test-ESTaskContextEvaluationAdapter.ps1'
if (-not (Test-Path -LiteralPath $test -PathType Leaf)) { throw 'TaskContextRuntime evaluation adapter validator is missing.' }
& $test -ProjectRoot $ProjectRoot
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
