[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [ValidateRange(1, 8)][int]$MaxIterations = 8,
    [switch]$Apply,
    [string]$OutputDirectory = 'ES/Output/KnowledgeValidation'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$exporter = Join-Path $PSScriptRoot 'Export-ESKnowledgeRefreshPlan.ps1'
$refresher = Join-Path $PSScriptRoot 'Invoke-ESKnowledgeStableRefresh.ps1'
$outputRoot = Join-Path $root $OutputDirectory
[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$history = [Collections.Generic.List[object]]::new()
$converged = $false

for ($iteration = 1; $iteration -le $MaxIterations; $iteration++) {
    $planPath = Join-Path $OutputDirectory 'fixed-point-plan.json'
    & $exporter -ProjectRoot $root -OutputPath $planPath | Out-Null
    $plan = Get-Content (Join-Path $root $planPath) -Raw -Encoding UTF8 | ConvertFrom-Json
    $driftCount = @($plan.findings | Where-Object { $_.code -eq 'SOURCE_HASH_DRIFT' }).Count
    $history.Add([pscustomobject]@{ iteration = $iteration; status = $plan.planStatus; driftCount = $driftCount; blockerCount = $plan.blockerCount; planHash = $plan.planHash })
    if ($plan.planStatus -ne 'ready') { break }
    if ($driftCount -eq 0) { $converged = $true; break }
    if (-not $Apply) { break }
    if (-not $PSCmdlet.ShouldProcess("Knowledge fixed-point iteration $iteration", 'Apply stable refresh')) { break }
    $receiptPath = Join-Path $OutputDirectory ("fixed-point-receipt-$iteration.json")
    & $refresher -ProjectRoot $root -PlanPath $planPath -OutputPath $receiptPath -Apply | Out-Null
    $receipt = Get-Content (Join-Path $root $receiptPath) -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not [bool]$receipt.applied -or [int]$receipt.staleAtApplyCount -ne 0) { break }
}

$result = [ordered]@{
    schemaVersion = 1
    toolId = 'es-knowledge-fixed-point-refresh'
    algorithm = 'es-knowledge-stable-refresh-v2-source-normalized'
    applied = [bool]$Apply
    converged = $converged
    iterationCount = $history.Count
    maxIterations = $MaxIterations
    history = @($history)
    nextAction = if ($converged) { 'Validate KnowledgeIndex and all target entries.' } elseif (-not $Apply) { 'Review preview, then rerun with -Apply.' } else { 'Blocked: fixed-point refresh did not converge within the bounded iteration limit.' }
}
$json = $result | ConvertTo-Json -Depth 8
[Console]::Out.WriteLine($json)
if (-not $converged) { exit 1 }
