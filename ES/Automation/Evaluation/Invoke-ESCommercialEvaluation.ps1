[CmdletBinding()]
param(
    [string]$ProjectRoot='.',
    [string]$StoreRoot='ES/Output/TaskContextRuntime',
    [Parameter(Mandatory=$true)][string[]]$TaskId,
    [ValidateRange(2,100)][int]$MinimumStableRuns=2,
    [string]$ModulePath
)
$ErrorActionPreference='Stop'
if([string]::IsNullOrWhiteSpace($ModulePath)){$ModulePath=Join-Path $PSScriptRoot 'ESCommercialEvaluation.psm1'}
Import-Module (Resolve-Path -LiteralPath $ModulePath -ErrorAction Stop).Path -Force
New-ESCommercialEvaluationReport -ProjectRoot $ProjectRoot -StoreRoot $StoreRoot -TaskId $TaskId -MinimumStableRuns $MinimumStableRuns|ConvertTo-Json -Depth 40
