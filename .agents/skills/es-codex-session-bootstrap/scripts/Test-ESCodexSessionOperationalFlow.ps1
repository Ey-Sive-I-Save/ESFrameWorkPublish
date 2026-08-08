[CmdletBinding()]
param(
    [switch]$KeepArtifacts
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ESCodexSessionState.ps1')
. (Join-Path $PSScriptRoot 'ESCodexSessionMessageState.ps1')

$started = [DateTime]::UtcNow
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
$stateRoot = Join-Path $temporaryRoot ('ESCS-Smoke-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($stateRoot)
$evidence = [Collections.Generic.List[object]]::new()
$passed = $false
$errorMessage = ''
$messageId = ''

try {
    $sessionId = [Guid]::NewGuid().ToString()
    $registryPath = Join-Path $stateRoot 'sessions.json'
    $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
    $record = AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
            sessionId = $sessionId
            responsibilityKey = 'smoke-unbound'
            taskKey = 'commercial-operational-smoke'
            tabTitle = 'ES-Smoke'
            processId = $PID
            terminalMode = 'PlainCmd'
            lifecycleStatus = 'Registered'
            contextAccepted = $true
            availability = 'Busy'
            availabilityUpdatedUtc = [DateTime]::UtcNow.ToString('o')
            availabilityExpiresUtc = [DateTime]::UtcNow.AddMinutes(5).ToString('o')
        })
    Save-ESCodexSessionRegistry $registryPath $registry
    $evidence.Add([pscustomobject]@{ stage = 'registry_created'; passed = (Test-Path -LiteralPath $registryPath); detail = $record.recordId })

    $bound = & (Join-Path $PSScriptRoot 'Set-ESCodexSessionResponsibility.ps1') -RecordId $record.recordId -NewResponsibilityKey 'operational-smoke' -StateRoot $stateRoot
    $evidence.Add([pscustomobject]@{ stage = 'responsibility_bound'; passed = [string]$bound.route.responsibilityKey -eq 'operational-smoke'; detail = [string]$bound.route.responsibilityKey })

    & (Join-Path $PSScriptRoot 'Set-ESCodexSessionPresence.ps1') -RecordId $record.recordId -Availability Busy -ActivityKey 'smoke-wait' -StateRoot $stateRoot | Out-Null
    $presenceScript = Join-Path $PSScriptRoot 'Set-ESCodexSessionPresence.ps1'
    $job = Start-Job -ScriptBlock {
        param($scriptPath, $targetRoot, $targetRecordId)
        Start-Sleep -Milliseconds 400
        & $scriptPath -RecordId $targetRecordId -Availability Idle -ActivityKey 'smoke-ready' -StateRoot $targetRoot | Out-Null
    } -ArgumentList $presenceScript, $stateRoot, $record.recordId
    try {
        $wait = & (Join-Path $PSScriptRoot 'Wait-ESCodexSessionRoute.ps1') -RecordId $record.recordId -WaitFor Ready -TimeoutSeconds 5 -PollMilliseconds 250 -StateRoot $stateRoot
        Wait-Job $job -Timeout 5 | Out-Null
        $jobError = @($job.ChildJobs[0].Error)
        if ($jobError.Count -gt 0) { throw [string]$jobError[0] }
    }
    finally { Remove-Job $job -Force -ErrorAction SilentlyContinue }
    $evidence.Add([pscustomobject]@{ stage = 'busy_to_idle_wait'; passed = [bool]$wait.completed -and [string]$wait.query.route.effectiveAvailability -eq 'Idle'; detail = [string]$wait.query.route.waitState })

    & (Join-Path $PSScriptRoot 'Set-ESCodexSessionPresence.ps1') -RecordId $record.recordId -Availability Busy -ActivityKey 'smoke-message' -StateRoot $stateRoot | Out-Null
    $emptyHookInput = @{ hook_event_name = 'Stop'; session_id = $sessionId; turn_id = 'smoke-activation'; stop_hook_active = $false; last_assistant_message = 'ready' } | ConvertTo-Json -Compress
    & (Join-Path $PSScriptRoot 'Receive-ESCodexSessionMessageHook.ps1') -InputJson $emptyHookInput -StateRoot $stateRoot | Out-Null

    $sent = & (Join-Path $PSScriptRoot 'Send-ESCodexSessionMessage.ps1') -RecordId $record.recordId -Body 'isolated commercial operational smoke message' -IdempotencyKey 'operational-smoke-message' -Priority high -StateRoot $stateRoot
    $messageId = [string]$sent.message.messageId
    $messagePaths = Get-ESCodexMessagePaths $stateRoot $messageId
    $requestBefore = [Convert]::ToBase64String([IO.File]::ReadAllBytes($messagePaths.requestPath))
    $evidence.Add([pscustomobject]@{ stage = 'message_queued'; passed = [bool]$sent.queued -and [string]$sent.deliveryPlan -eq 'StopHookAtBusyCompletion'; detail = [string]$sent.deliveryPlan })

    $claimHookInput = @{ hook_event_name = 'Stop'; session_id = $sessionId; turn_id = 'smoke-claim'; stop_hook_active = $false; last_assistant_message = 'done' } | ConvertTo-Json -Compress
    $hookOutput = & (Join-Path $PSScriptRoot 'Receive-ESCodexSessionMessageHook.ps1') -InputJson $claimHookInput -StateRoot $stateRoot | ConvertFrom-Json
    $acceptedResult = & (Join-Path $PSScriptRoot 'Get-ESCodexSessionMessage.ps1') -MessageId $messageId -StateRoot $stateRoot
    $accepted = @($acceptedResult.messages)[0]
    $evidence.Add([pscustomobject]@{ stage = 'hook_accepted'; passed = [string]$hookOutput.decision -eq 'block' -and [string]$accepted.effectiveStatus -eq 'accepted'; detail = [string]$accepted.effectiveStatus })

    $completed = & (Join-Path $PSScriptRoot 'Set-ESCodexSessionMessageStatus.ps1') -MessageId $messageId -Status completed -AcceptedByRecordId $record.recordId -Note 'isolated smoke completed' -ExpectedStateRevision $accepted.stateRevision -StateRoot $stateRoot
    $requestAfter = [Convert]::ToBase64String([IO.File]::ReadAllBytes($messagePaths.requestPath))
    $evidence.Add([pscustomobject]@{ stage = 'message_completed'; passed = [string]$completed.effectiveStatus -eq 'completed' -and $requestBefore -eq $requestAfter; detail = [string]$completed.effectiveStatus })

    $reloaded = Read-ESCodexSessionRegistry $registryPath
    $reloadedRecord = @($reloaded.sessions | Where-Object recordId -eq $record.recordId)
    $evidence.Add([pscustomobject]@{ stage = 'restart_reload'; passed = $reloadedRecord.Count -eq 1 -and [string]$reloadedRecord[0].responsibilityKey -eq 'operational-smoke'; detail = "revision=$($reloaded.revision)" })

    $repair = & (Join-Path $PSScriptRoot 'Repair-ESCodexSessionState.ps1') -SkipUiObservation -SkipReadinessRefresh -StateRoot $stateRoot
    $evidence.Add([pscustomobject]@{ stage = 'repair_idempotent'; passed = @($repair.proposedActions | Where-Object applicable).Count -eq 0; detail = "planned=$(@($repair.proposedActions).Count)" })

    $failedStages = @($evidence | Where-Object { -not $_.passed })
    if ($failedStages.Count -gt 0) { throw 'Operational smoke stages failed: ' + (@($failedStages | ForEach-Object stage) -join ', ') }
    $passed = $true
}
catch {
    $errorMessage = $_.Exception.Message
}
finally {
    if ($passed -and -not $KeepArtifacts) {
        $resolvedStateRoot = [IO.Path]::GetFullPath($stateRoot).TrimEnd('\')
        $safePrefix = $temporaryRoot + '\ESCS-Smoke-'
        if (-not $resolvedStateRoot.StartsWith($safePrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clean an unexpected operational smoke path: $resolvedStateRoot"
        }
        if (Test-Path -LiteralPath $resolvedStateRoot -PathType Container) { Remove-Item -LiteralPath $resolvedStateRoot -Recurse -Force }
    }
}

[pscustomobject][ordered]@{
    operationalSmokeContractVersion = 1
    passed = $passed
    isolated = $true
    touchedAuthoritativeLocalState = $false
    stateRoot = if ($passed -and -not $KeepArtifacts) { '' } else { $stateRoot }
    artifactsRetained = -not $passed -or [bool]$KeepArtifacts
    messageId = $messageId
    durationMs = [int]([DateTime]::UtcNow - $started).TotalMilliseconds
    evidence = $evidence.ToArray()
    error = $errorMessage
}
