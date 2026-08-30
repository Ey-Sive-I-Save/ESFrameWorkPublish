[CmdletBinding()]
param(
    [string]$ProjectionPath = '',
    [string]$AggregatePath = (Join-Path $PSScriptRoot 'fixtures/aggregate.valid.json'),
    [ValidateRange(1,8)][int]$ConcurrencyBudget = 4
)
$ErrorActionPreference = 'Stop'
$projection = if ($ProjectionPath) {
    if (-not (Test-Path -LiteralPath $ProjectionPath -PathType Leaf)) { throw 'PROJECTION_REQUIRED' }
    Get-Content -Raw -Encoding UTF8 -LiteralPath $ProjectionPath | ConvertFrom-Json
} else {
    & (Join-Path $PSScriptRoot 'Invoke-ESWebUiSubAgentProjection.ps1') -AggregatePath $AggregatePath -ConcurrencyBudget $ConcurrencyBudget | ConvertFrom-Json
}
$plan = $projection.executionPlan
$children = @($plan.parallelStage.childTaskIds | ForEach-Object { [string]$_ })
$budget = [int]$plan.parallelStage.concurrencyBudget
if ($budget -lt 1 -or $budget -gt [Math]::Max(1,$children.Count)) { throw 'CONCURRENCY_BUDGET_INVALID' }
if (@($children | Select-Object -Unique).Count -ne $children.Count) { throw 'CHILD_TASK_DUPLICATE' }
$waves = [System.Collections.Generic.List[object]]::new()
$waveNumber = 0
for ($offset = 0; $offset -lt $children.Count; $offset += $budget) {
    $end = [Math]::Min($offset + $budget - 1, $children.Count - 1)
    $waves.Add([ordered]@{ wave = $waveNumber; stage = 'layer-evidence'; dependsOnWaves = @(); maxParallel = $budget; taskIds = @($children[$offset..$end]) })
    $waveNumber++
}
$validatorTasks = @('web-ui.validate.network','web-ui.validate.preview','web-ui.validate.visual','web-ui.validate.release')
$waves.Add([ordered]@{ wave = $waveNumber; stage = 'layer-validation'; dependsOnWaves = @($waveNumber - 1); maxParallel = $budget; taskIds = $validatorTasks })
$waveNumber++
$waves.Add([ordered]@{ wave = $waveNumber; stage = 'evidence-aggregation'; dependsOnWaves = @($waveNumber - 1); maxParallel = 1; taskIds = @('web-ui.aggregate') })
$scheduleInput = [ordered]@{ projectionRecordType = [string]$projection.recordType; parentTaskId = [string]$projection.parentTaskId; planHash = [string]$projection.collaborationPlan.planHash; verificationHash = [string]$projection.verificationHash; maxParallel = $budget; waves = @($waves); dispatch = 'external-managed-worker-required'; cancellation = 'lease-cas'; admissionGate = 'web-ui-sub-agent-admission'; workerContractIds = @('es-task-collaboration-plan-v1','es-task-lease-cas-v1','es-task-result-envelope-v1','es-task-parent-aggregation-v1'); leafExecutor = 'ESAutomationProcessRunner'; nonClaims = @('schedule-only','does-not-start-workers','does-not-prove-runtime-speedup') }
$scheduleInput.scheduleHash = (Get-FileHash -InputStream ([IO.MemoryStream]::new([Text.Encoding]::UTF8.GetBytes(($scheduleInput | ConvertTo-Json -Depth 20 -Compress)))) -Algorithm SHA256).Hash.ToLowerInvariant()
$scheduleInput | ConvertTo-Json -Depth 20
