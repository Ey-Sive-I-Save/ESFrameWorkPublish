[CmdletBinding()]
param([string]$ProjectRoot)
$ErrorActionPreference='Stop'
if([string]::IsNullOrWhiteSpace($ProjectRoot)){$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path}
$test=Join-Path $ProjectRoot 'ES/Automation/TaskContextRuntime/Test-ESTaskContextRuntimeIntegrationPolicy.ps1'
if(-not(Test-Path -LiteralPath $test -PathType Leaf)){throw 'TaskContextRuntime integration policy validator is missing.'}
& $test
if($LASTEXITCODE-ne0){exit $LASTEXITCODE}
