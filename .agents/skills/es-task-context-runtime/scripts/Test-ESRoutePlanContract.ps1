[CmdletBinding()]
param([string]$ProjectRoot)
$ErrorActionPreference='Stop'
if([string]::IsNullOrWhiteSpace($ProjectRoot)){$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path}
& (Join-Path $ProjectRoot 'ES/Automation/RoutePlan/Test-ESRoutePlanContract.ps1') -ProjectRoot $ProjectRoot
exit $LASTEXITCODE
