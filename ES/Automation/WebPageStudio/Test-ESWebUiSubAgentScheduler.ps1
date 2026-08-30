[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'ESWebUiSubAgentScheduler.psm1') -Force
$schedule=& (Join-Path $PSScriptRoot 'Invoke-ESWebUiSubAgentSchedule.ps1') -ConcurrencyBudget 2 | ConvertFrom-Json
$checks=[System.Collections.Generic.List[object]]::new()
function Check([string]$Name,[bool]$Passed){$checks.Add([pscustomobject]@{check=$Name;passed=$Passed})}
$state=New-ESWebUiSubAgentScheduler -Schedule $schedule -MaxAttempts 2
$ready=@(Get-ESWebUiSchedulerReadyTasks -State $state)
Check 'initial-ready-bounded' ($ready.Count -eq 2)
$first=Start-ESWebUiSchedulerTask -State $state -TaskId ([string]$ready[0]);$second=Start-ESWebUiSchedulerTask -State $state -TaskId ([string]$ready[1])
Check 'active-budget' ($state.active.Count -eq 2 -and $state.maxObservedConcurrency -eq 2)
$budgetRejected=$false;try{Start-ESWebUiSchedulerTask -State $state -TaskId ([string]$ready[0])|Out-Null}catch{$budgetRejected=$_.Exception.Message -eq 'SCHEDULER_BUDGET_EXCEEDED'}
Check 'budget-overrun-rejected' $budgetRejected
Complete-ESWebUiSchedulerTask -State $state -TaskId $first.taskId -Status candidate|Out-Null;Complete-ESWebUiSchedulerTask -State $state -TaskId $second.taskId -Status candidate|Out-Null
$next=@(Get-ESWebUiSchedulerReadyTasks -State $state);Check 'next-wave-ready' ($next.Count -eq 2)
$retryState=New-ESWebUiSubAgentScheduler -Schedule $schedule -MaxAttempts 2;$retryTask=(Get-ESWebUiSchedulerReadyTasks -State $retryState)[0];Start-ESWebUiSchedulerTask -State $retryState -TaskId ([string]$retryTask)|Out-Null;Complete-ESWebUiSchedulerTask -State $retryState -TaskId ([string]$retryTask) -Status failed -ReasonCode 'TRANSIENT'|Out-Null;$retryReady=@(Get-ESWebUiSchedulerReadyTasks -State $retryState);Check 'failed-retry-ready' ($retryReady -contains [string]$retryTask)
Start-ESWebUiSchedulerTask -State $retryState -TaskId ([string]$retryTask)|Out-Null;Complete-ESWebUiSchedulerTask -State $retryState -TaskId ([string]$retryTask) -Status candidate|Out-Null
$cancelState=New-ESWebUiSubAgentScheduler -Schedule $schedule;$cancelTask=(Get-ESWebUiSchedulerReadyTasks -State $cancelState)[0];Start-ESWebUiSchedulerTask -State $cancelState -TaskId ([string]$cancelTask)|Out-Null;Cancel-ESWebUiSchedulerTask -State $cancelState -TaskId ([string]$cancelTask)|Out-Null;Check 'cancel-terminal' ([string]$cancelState.terminal[[string]$cancelTask].status -ceq 'cancelled' -and -not $cancelState.active.ContainsKey([string]$cancelTask))
$staleState=New-ESWebUiSubAgentScheduler -Schedule $schedule;$staleTask=(Get-ESWebUiSchedulerReadyTasks -State $staleState)[0];$staleEntry=Start-ESWebUiSchedulerTask -State $staleState -TaskId ([string]$staleTask);$staleEntry.lease.expiresUtc=[DateTime]::UtcNow.AddSeconds(-1).ToString('o');$staleRejected=$false;try{Complete-ESWebUiSchedulerTask -State $staleState -TaskId ([string]$staleTask)|Out-Null}catch{$staleRejected=$_.Exception.Message -eq 'SCHEDULER_LEASE_REJECTED'};Check 'expired-lease-rejected' $staleRejected
$receipt=Get-ESWebUiSchedulerReceipt -State $state;Check 'receipt-observes-concurrency' ([int]$receipt.maxObservedConcurrency -eq 2 -and @($receipt.events).Count -gt 0)
$backoffState=New-ESWebUiSubAgentScheduler -Schedule $schedule -MaxAttempts 2 -RetryBackoffSeconds 30;$backoffTask=(Get-ESWebUiSchedulerReadyTasks -State $backoffState)[0];Start-ESWebUiSchedulerTask -State $backoffState -TaskId ([string]$backoffTask)|Out-Null;Complete-ESWebUiSchedulerTask -State $backoffState -TaskId ([string]$backoffTask) -Status failed -ReasonCode 'TRANSIENT'|Out-Null;Check 'retry-backoff-gates-ready' (-not ((Get-ESWebUiSchedulerReadyTasks -State $backoffState) -contains [string]$backoffTask))
$treeState=New-ESWebUiSubAgentScheduler -Schedule $schedule;$treeTask=(Get-ESWebUiSchedulerReadyTasks -State $treeState)[0];Start-ESWebUiSchedulerTask -State $treeState -TaskId ([string]$treeTask)|Out-Null;Cancel-ESWebUiSchedulerTree -State $treeState|Out-Null;Check 'parent-cancel-propagates' ($treeState.cancelled.Count -ge 1 -and $treeState.active.Count -eq 0)
$backoffReceipt=Get-ESWebUiSchedulerReceipt -State $backoffState;Check 'receipt-exposes-retry-policy' ([int]$backoffReceipt.retryBackoffSeconds -eq 30)
$failed=@($checks|Where-Object {-not $_.passed})
[ordered]@{validator='web-ui-sub-agent-scheduler';status=if($failed.Count){'failed'}else{'passed'};checks=@($checks);runtimeStatus='runtime-not-run';nonClaims=@('in-memory-kernel-replay','does-not-start-external-worker','does-not-prove-cross-process-speedup','lease-is-not-persistent-CAS')}|ConvertTo-Json -Depth 12
if($failed.Count){exit 1}
