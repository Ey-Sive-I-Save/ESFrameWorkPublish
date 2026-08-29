[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$InputPath,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$ProjectRoot = 'F:\aaProject\ESFrameWorkPublish'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$inputFull = (Resolve-Path -LiteralPath $InputPath).Path
$outputFull = [IO.Path]::GetFullPath($OutputDirectory)
$runsRoot = [IO.Path]::GetFullPath((Join-Path $root 'ES\Automation\Runs\CodexMultiLaunch'))
if (-not $outputFull.StartsWith($runsRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'OutputDirectory must remain under the managed CodexMultiLaunch run root.' }
New-Item -ItemType Directory -Force -Path $outputFull | Out-Null
$request = Get-Content -LiteralPath $inputFull -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $request.batchId -or @($request.launches).Count -lt 1) { throw 'Invalid Codex MultiLaunch request.' }
$planPath = Join-Path $outputFull 'launch-plan.json'
$request | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $planPath -Encoding UTF8
$launcher = Join-Path $root '.agents\skills\es-codex-session-bootstrap\scripts\Invoke-ESCodexMultiLaunch.ps1'
if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) { throw 'MultiLaunch Skill entrypoint is missing.' }
$args = @{ PlanPath=$planPath; ProjectPath=$root; MaxParallel=[int]$request.maxParallel; DryRun=[bool]$request.dryRun }
if (-not [bool]$request.dryRun) { $args.Launch = $true }
if ([bool]$request.reissue) { $args.Reissue = $true }
$result = & $launcher @args
$resultText = if ($result -is [string]) { $result } else { $result | Out-String }
$resultPath = Join-Path $outputFull 'multilaunch-result.json'
$resultText | Set-Content -LiteralPath $resultPath -Encoding UTF8
$resultText
