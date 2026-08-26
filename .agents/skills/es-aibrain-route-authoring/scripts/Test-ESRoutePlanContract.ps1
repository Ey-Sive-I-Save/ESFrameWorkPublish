[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path)
$ErrorActionPreference='Stop'
& (Join-Path $ProjectRoot 'ES/Automation/RoutePlan/Test-ESRoutePlanContract.ps1') -ProjectRoot $ProjectRoot
exit $LASTEXITCODE
