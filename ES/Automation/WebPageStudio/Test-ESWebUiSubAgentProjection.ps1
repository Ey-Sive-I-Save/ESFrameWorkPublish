[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '..\TaskCollaboration\ESTaskCollaborationContracts.psm1') -Force
$zero = '0' * 64; $one = ('1' * 64); $two = ('2' * 64)
$parent = 'web-ui-parent'; $children = @('web-ui.network','web-ui.preview','web-ui.visual','web-ui.release')
$plan = New-ESCollaborationPlan -ParentTaskId $parent -GoalRevisionHash $zero -RoutePlanHash $zero -ChildTaskIds $children -ConcurrencyBudget 4 -AggregationStrategy 'all-required'
$registry = New-ESChildTaskRegistry -ParentTaskId $parent -ParentTaskRevision 1 -CollaborationPlan $plan
$issued = [DateTime]::Parse('2026-01-01T00:00:00Z').ToUniversalTime()
function Envelope([string]$child,[string]$hash,[ValidateSet('candidate','failed','cancelled')][string]$status='candidate',[int]$attempt=1,[string]$planHash=$plan.planHash,[int]$offset=1) {
    $lease = New-ESLeaseClaim -TaskId $child -WorkerId ('worker.' + $child) -ExpectedTaskRevision 1 -ExpectedContextVersion 1 -IssuedUtc $issued
    New-ESResultEnvelope -ParentTaskId $parent -ChildTaskId $child -CollaborationPlanHash $planHash -TaskRevision 1 -ContextVersion 1 -Attempt $attempt -LeaseClaim $lease -ResultStatus $status -OutputHash $hash -EvidenceRefs @('web-ui/' + $child) -ErrorCode $(if($status -eq 'candidate'){$null}else{'TEST_TERMINAL'}) -CapturedUtc $issued.AddSeconds($offset)
}
$conflict = Invoke-ESParentAggregation -CollaborationPlan $plan -ChildTaskRegistry $registry -ResultEnvelopes @((Envelope $children[0] $one),(Envelope $children[0] $two),(Envelope $children[1] $one),(Envelope $children[2] $one),(Envelope $children[3] $one))
$stale = Envelope $children[0] $one 'candidate' 1 ('f' * 64)
$staleAgg = Invoke-ESParentAggregation -CollaborationPlan $plan -ChildTaskRegistry $registry -ResultEnvelopes @($stale,(Envelope $children[1] $one),(Envelope $children[2] $one),(Envelope $children[3] $one))
$cancelled = Envelope $children[0] $one 'cancelled' 1 $plan.planHash 1
$recovery = Envelope $children[0] $two 'candidate' 2 $plan.planHash 2
$cancelAgg = Invoke-ESParentAggregation -CollaborationPlan $plan -ChildTaskRegistry $registry -ResultEnvelopes @($cancelled,$recovery,(Envelope $children[1] $one),(Envelope $children[2] $one),(Envelope $children[3] $one))
$checks = @(
    [pscustomobject]@{case='same-attempt-conflict';passed=([string]$conflict.status -ceq 'conflict' -and @($conflict.conflictChildTaskIds) -contains $children[0])},
    [pscustomobject]@{case='stale-plan-quarantine';passed=([string]$staleAgg.children[0].disposition -ceq 'missing' -and @($staleAgg.children[0].quarantinedResultHashes) -contains $stale.resultHash)},
    [pscustomobject]@{case='cancel-terminal-immutable';passed=([string]$cancelAgg.children[0].disposition -ceq 'cancelled' -and @($cancelAgg.children[0].quarantinedResultHashes) -contains $recovery.resultHash)}
)
$failed=@($checks|Where-Object {-not $_.passed})
[ordered]@{validator='web-ui-sub-agent-projection';status=if($failed.Count){'failed'}else{'passed'};checks=$checks;runtimeStatus='runtime-not-run';nonClaims=@('static-race-replay','no-worker-dispatch','does-not-prove-runtime-parallelism')}|ConvertTo-Json -Depth 8
if($failed.Count){exit 1}
