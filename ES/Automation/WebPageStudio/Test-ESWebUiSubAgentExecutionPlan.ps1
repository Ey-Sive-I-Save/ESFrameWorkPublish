[CmdletBinding()]
param([string]$AggregatePath=(Join-Path $PSScriptRoot 'fixtures/aggregate.valid.json'))
$ErrorActionPreference='Stop'
$projection=& (Join-Path $PSScriptRoot 'Invoke-ESWebUiSubAgentProjection.ps1') -AggregatePath $AggregatePath | ConvertFrom-Json
$plan=$projection.executionPlan
$children=@($plan.parallelStage.childTaskIds)
$checks=@(
 [pscustomobject]@{check='parallel-layer-count';passed=($children.Count -eq 4)},
 [pscustomobject]@{check='concurrency-budget';passed=([int]$plan.parallelStage.concurrencyBudget -ge 1 -and [int]$plan.parallelStage.concurrencyBudget -le 8)},
 [pscustomobject]@{check='serial-aggregation-stage';passed=(@($plan.serialStages) -contains 'evidence-aggregation')},
 [pscustomobject]@{check='lease-cas-cancellation';passed=([string]$plan.parallelStage.cancellation -ceq 'lease-cas')},
 [pscustomobject]@{check='verification-stage';passed=([string]$plan.verificationStage.name -ceq 'layer-validation' -and @($plan.verificationStage.validatorKeys).Count -eq 4)},
 [pscustomobject]@{check='stage-dependencies';passed=(@($plan.dependencies | Where-Object { $_.stage -eq 'evidence-aggregation' -and $_.dependsOn -eq 'layer-validation' }).Count -eq 1)},
 [pscustomobject]@{check='not-run-preserved';passed=([string]$projection.parentAggregation.status -eq 'partial' -and @($projection.resultEnvelopes | Where-Object { $_.resultStatus -eq 'failed' -and $_.errorCode -eq 'WEB_UI_RUNTIME_NOT_RUN' }).Count -eq 4)},
 [pscustomobject]@{check='evidence-refs-array';passed=(@($projection.resultEnvelopes | Where-Object { $_.evidenceRefs -isnot [array] }).Count -eq 0)}
)
$failed=@($checks|Where-Object {-not $_.passed})
[ordered]@{validator='web-ui-sub-agent-execution-plan';status=if($failed.Count){'failed'}else{'passed'};checks=$checks;childCount=$children.Count;runtimeStatus='runtime-not-run';nonClaims=@('plan-only','does-not-dispatch-workers','does-not-prove-runtime-speedup')}|ConvertTo-Json -Depth 8
if($failed.Count){exit 1}
