Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '..\TaskCollaboration\ESTaskCollaborationContracts.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'ESWebUiPersistentLeaseStore.psm1') -Force

function New-ESWebUiSubAgentScheduler {
    param([Parameter(Mandatory)]$Schedule,[int]$TaskRevision=1,[int]$ContextVersion=1,[int]$MaxAttempts=2,[int]$RetryBackoffSeconds=0,[object]$PersistentStore=$null)
    if ([string]$Schedule.dispatch -ne 'external-managed-worker-required') { throw 'SCHEDULER_DISPATCH_BOUNDARY_INVALID' }
    if ([string]$Schedule.admissionGate -ne 'web-ui-sub-agent-admission') { throw 'SCHEDULER_ADMISSION_GATE_REQUIRED' }
    if ([int]$Schedule.maxParallel -lt 1 -or [int]$Schedule.maxParallel -gt 256) { throw 'SCHEDULER_BUDGET_INVALID' }
    if ($MaxAttempts -lt 1 -or $MaxAttempts -gt 16) { throw 'SCHEDULER_ATTEMPTS_INVALID' }
    if ($RetryBackoffSeconds -lt 0 -or $RetryBackoffSeconds -gt 3600) { throw 'SCHEDULER_BACKOFF_INVALID' }
    [pscustomobject]@{
        schemaVersion=1; recordType='WebPageStudioSubAgentSchedulerState'; parentTaskId=[string]$Schedule.parentTaskId; planHash=[string]$Schedule.planHash; verificationHash=[string]$Schedule.verificationHash
        taskRevision=$TaskRevision; contextVersion=$ContextVersion; maxParallel=[int]$Schedule.maxParallel; maxAttempts=$MaxAttempts; retryBackoffSeconds=$RetryBackoffSeconds; persistentStore=$PersistentStore; retryReadyUtc=@{}; cancelled=@{}; waveIndex=0; active=@{}; attempts=@{}; terminal=@{}; maxObservedConcurrency=0; events=[System.Collections.Generic.List[object]]::new(); createdUtc=[DateTime]::UtcNow.ToString('o'); schedule=$Schedule
    }
}

function Get-ESWebUiSchedulerReadyTasks {
    param([Parameter(Mandatory)]$State)
    if ($State.waveIndex -ge @($State.schedule.waves).Count) { return @() }
    $wave=$State.schedule.waves[$State.waveIndex]
    if ($State.active.Count -ge $State.maxParallel) { return @() }
    $now=[DateTime]::UtcNow
    @($wave.taskIds | Where-Object { $id=[string]$_; -not $State.active.ContainsKey($id) -and -not $State.terminal.ContainsKey($id) -and -not $State.cancelled.ContainsKey($id) -and (-not $State.retryReadyUtc.ContainsKey($id) -or [int]$State.retryBackoffSeconds -eq 0 -or $now -ge [DateTime]$State.retryReadyUtc[$id]) })
}

function Start-ESWebUiSchedulerTask {
    param([Parameter(Mandatory)]$State,[Parameter(Mandatory)][string]$TaskId,[string]$WorkerId)
    if ($State.active.Count -ge $State.maxParallel) { throw 'SCHEDULER_BUDGET_EXCEEDED' }
    if ((Get-ESWebUiSchedulerReadyTasks -State $State) -notcontains $TaskId) { throw 'SCHEDULER_TASK_NOT_READY' }
    if (-not $WorkerId) { $WorkerId='web-ui-worker.' + $TaskId }
    $attempt=if($State.attempts.ContainsKey($TaskId)){[int]$State.attempts[$TaskId]+1}else{1}
    if($attempt -gt $State.maxAttempts){throw 'SCHEDULER_RETRY_EXHAUSTED'}
    $lease=if($null -ne $State.persistentStore){Claim-ESWebUiPersistentLease -Store $State.persistentStore -TaskId $TaskId -WorkerId $WorkerId -TaskRevision $State.taskRevision -ContextVersion $State.contextVersion}else{New-ESLeaseClaim -TaskId $TaskId -WorkerId $WorkerId -ExpectedTaskRevision $State.taskRevision -ExpectedContextVersion $State.contextVersion -IssuedUtc ([DateTime]::UtcNow)}
    $State.active[$TaskId]=[pscustomobject]@{taskId=$TaskId;attempt=$attempt;lease=$lease;startedUtc=[DateTime]::UtcNow.ToString('o')};$State.attempts[$TaskId]=$attempt
    if($State.active.Count -gt [int]$State.maxObservedConcurrency){$State.maxObservedConcurrency=$State.active.Count}
    $State.events.Add([ordered]@{event='claimed';taskId=$TaskId;attempt=$attempt;leaseId=$lease.leaseId;utc=[DateTime]::UtcNow.ToString('o')})
    $State.active[$TaskId]
}

function Complete-ESWebUiSchedulerTask {
    param([Parameter(Mandatory)]$State,[Parameter(Mandatory)][string]$TaskId,[ValidateSet('candidate','failed','cancelled')][string]$Status='candidate',[string]$ReasonCode)
    if (-not $State.active.ContainsKey($TaskId)) { throw 'SCHEDULER_TASK_NOT_ACTIVE' }
    $entry=$State.active[$TaskId];$cas=Test-ESLeaseCas -LeaseClaim $entry.lease -CurrentTaskRevision $State.taskRevision -CurrentContextVersion $State.contextVersion
    if(-not $cas.canSubmitResult){throw 'SCHEDULER_LEASE_REJECTED'}
    if($null -ne $State.persistentStore){Complete-ESWebUiPersistentLease -Store $State.persistentStore -Lease $entry.lease -Status $Status -TaskRevision $State.taskRevision -ContextVersion $State.contextVersion|Out-Null}
    $State.active.Remove($TaskId);$State.terminal[$TaskId]=[pscustomobject]@{status=$Status;attempt=$entry.attempt;reasonCode=$ReasonCode;completedUtc=[DateTime]::UtcNow.ToString('o')}
    $State.events.Add([ordered]@{event='completed';taskId=$TaskId;attempt=$entry.attempt;status=$Status;reasonCode=$ReasonCode;utc=[DateTime]::UtcNow.ToString('o')})
    if($Status -eq 'failed' -and $entry.attempt -lt $State.maxAttempts){
        $State.terminal.Remove($TaskId)
        $readyAt=[DateTime]::UtcNow.AddSeconds([int]$State.retryBackoffSeconds)
        $State.retryReadyUtc[$TaskId]=$readyAt.ToString('o')
        $State.events.Add([ordered]@{event='retry-waiting';taskId=$TaskId;attempt=$entry.attempt;readyUtc=$readyAt.ToString('o');utc=[DateTime]::UtcNow.ToString('o')})
    }
    if($State.active.Count -eq 0 -and @((Get-ESWebUiSchedulerReadyTasks -State $State)).Count -eq 0 -and $State.waveIndex -lt @($State.schedule.waves).Count){$State.waveIndex++}
    $State.terminal[$TaskId]
}

function Cancel-ESWebUiSchedulerTask {
    param([Parameter(Mandatory)]$State,[Parameter(Mandatory)][string]$TaskId,[string]$ReasonCode='SCHEDULER_CANCELLED')
    $result=Complete-ESWebUiSchedulerTask -State $State -TaskId $TaskId -Status cancelled -ReasonCode $ReasonCode
    $State.cancelled[$TaskId]=$true
    $State.retryReadyUtc.Remove($TaskId)
    $result
}

function Cancel-ESWebUiSchedulerTree {
    param([Parameter(Mandatory)]$State,[string]$ReasonCode='SCHEDULER_PARENT_CANCELLED')
    $ids=@($State.active.Keys)+@($State.schedule.waves.taskIds)
    foreach($id in ($ids | ForEach-Object {[string]$_} | Sort-Object -Unique)) {
        if($State.active.ContainsKey($id)){ Cancel-ESWebUiSchedulerTask -State $State -TaskId $id -ReasonCode $ReasonCode | Out-Null }
        elseif(-not $State.terminal.ContainsKey($id)){ $State.cancelled[$id]=$true; $State.events.Add([ordered]@{event='cancel-propagated';taskId=$id;reasonCode=$ReasonCode;utc=[DateTime]::UtcNow.ToString('o')}) }
    }
    $true
}

function Get-ESWebUiSchedulerReceipt {
    param([Parameter(Mandatory)]$State)
    $durationEvents=@($State.events|Where-Object event -eq 'completed')
    [ordered]@{schemaVersion=1;recordType='WebPageStudioSubAgentSchedulerReceipt';parentTaskId=$State.parentTaskId;planHash=$State.planHash;verificationHash=$State.verificationHash;status=if($State.waveIndex -ge @($State.schedule.waves).Count -and $State.active.Count -eq 0){'completed'}else{'partial'};waveIndex=$State.waveIndex;activeCount=$State.active.Count;maxObservedConcurrency=[int]$State.maxObservedConcurrency;completedCount=$durationEvents.Count;retryBackoffSeconds=[int]$State.retryBackoffSeconds;cancelledCount=$State.cancelled.Count;persistence=if($null -ne $State.persistentStore){'file-backed-adapter-bound'}else{'in-memory'};events=@($State.events);runtimeStatus='runtime-not-run';nonClaims=@('scheduler-kernel-replay','does-not-start-external-worker','does-not-prove-process-speedup','persistent-adapter-does-not-prove-cross-process-atomicity','does-not-prove-crash-recovery')}
}

Export-ModuleMember -Function New-ESWebUiSubAgentScheduler,Get-ESWebUiSchedulerReadyTasks,Start-ESWebUiSchedulerTask,Complete-ESWebUiSchedulerTask,Cancel-ESWebUiSchedulerTask,Cancel-ESWebUiSchedulerTree,Get-ESWebUiSchedulerReceipt
