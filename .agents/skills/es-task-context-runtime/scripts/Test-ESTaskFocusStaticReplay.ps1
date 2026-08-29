[CmdletBinding()]
param([string]$ProjectRoot,[string]$ReportPath='ES/Output/StaticReplay/task-focus-context.json')
$ErrorActionPreference='Stop'
if([string]::IsNullOrWhiteSpace($ProjectRoot)){$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path}
$runner=Join-Path $ProjectRoot 'ES/Automation/TaskFocusContext/Test-ESTaskFocusStaticReplay.ps1'
if(-not(Test-Path -LiteralPath $runner -PathType Leaf)){throw 'TaskFocusContext StaticReplay runner is missing.'}
& $runner -ReportPath $ReportPath
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
