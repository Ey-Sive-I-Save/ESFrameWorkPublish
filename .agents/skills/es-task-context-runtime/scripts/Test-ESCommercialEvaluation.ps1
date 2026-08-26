[CmdletBinding()]
param([string]$ProjectRoot)
$ErrorActionPreference='Stop'
if([string]::IsNullOrWhiteSpace($ProjectRoot)){$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path}
$test=Join-Path $ProjectRoot 'ES/Automation/Evaluation/Test-ESCommercialEvaluation.ps1'
if(-not(Test-Path -LiteralPath $test -PathType Leaf)){throw 'Commercial evaluation validator is missing.'}
& $test -ProjectRoot $ProjectRoot
if($LASTEXITCODE-ne0){exit $LASTEXITCODE}
