$skillRoot = Split-Path -Parent $PSScriptRoot
$scriptsRoot = Join-Path $skillRoot 'scripts'
. (Join-Path $scriptsRoot 'ESCodexSessionState.ps1')
. (Join-Path $scriptsRoot 'ESCodexSessionMessageState.ps1')

Describe 'ES Codex acceptance request/reply protocol' {
    It 'waits through Busy, sends a correlated request, and receives a correlated reply' {
        $stateRoot = Join-Path $TestDrive 'acceptance-flow-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        $requester = AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                sessionId = '31313131-3131-3131-3131-313131313131'; responsibilityKey = 'feature-owner'; taskKey = 'acceptance-flow'; tabTitle = 'ES-Owner'; processId = $PID; terminalMode = 'PlainCmd'; lifecycleStatus = 'Registered'; contextAccepted = $true; availability = 'Idle'; availabilityUpdatedUtc = [DateTime]::UtcNow.ToString('o'); availabilityExpiresUtc = [DateTime]::UtcNow.AddMinutes(5).ToString('o')
            })
        $target = AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                sessionId = '32323232-3232-3232-3232-323232323232'; responsibilityKey = 'engineering-acceptance'; taskKey = 'acceptance-flow'; tabTitle = 'ES-Acceptance'; processId = $PID; terminalMode = 'PlainCmd'; lifecycleStatus = 'Registered'; contextAccepted = $true; availability = 'Busy'; availabilityUpdatedUtc = [DateTime]::UtcNow.ToString('o'); availabilityExpiresUtc = [DateTime]::UtcNow.AddMinutes(5).ToString('o')
            })
        Save-ESCodexSessionRegistry $registryPath $registry
        $projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $skillRoot))
        $hookInput = @{ hook_event_name = 'Stop'; session_id = $target.sessionId; turn_id = 'acceptance-activation'; stop_hook_active = $false } | ConvertTo-Json -Compress
        & (Join-Path $scriptsRoot 'Receive-ESCodexSessionMessageHook.ps1') -InputJson $hookInput -StateRoot $stateRoot | Out-Null
        $presenceScript = Join-Path $scriptsRoot 'Set-ESCodexSessionPresence.ps1'
        $job = Start-Job -ScriptBlock {
            param($scriptPath, $root, $recordId)
            Start-Sleep -Milliseconds 500
            & $scriptPath -RecordId $recordId -Availability Idle -StateRoot $root | Out-Null
        } -ArgumentList $presenceScript, $stateRoot, $target.recordId
        try {
            $result = & (Join-Path $scriptsRoot 'Request-ESCodexAcceptance.ps1') -AcceptanceResponsibilityKey 'engineering-acceptance' -RequesterRecordId $requester.recordId -Body 'Please accept the new feature.' -WaitSeconds 5 -PollMilliseconds 250 -StateRoot $stateRoot
            Wait-Job $job -Timeout 5 | Out-Null
        }
        finally { Remove-Job $job -Force -ErrorAction SilentlyContinue }
        $result.sent | Should Be $true
        $result.timedOut | Should Be $true
        $result.targetHistory.Count | Should BeGreaterThan 1
        $result.request.requestKind | Should Be 'acceptance-request'
        $result.request.expectsReply | Should Be $true
        $result.request.replyToRecordId | Should Be $requester.recordId
        $result.request.correlationId | Should Be $result.correlationId

        $reply = & (Join-Path $scriptsRoot 'Reply-ESCodexAcceptance.ps1') -RequestMessageId $result.requestMessageId -ResponderRecordId $target.recordId -Body 'Accepted; evidence is complete.' -StateRoot $stateRoot
        $reply.sent | Should Be $true
        $status = & (Join-Path $scriptsRoot 'Get-ESCodexAcceptanceStatus.ps1') -RequestMessageId $result.requestMessageId -RequesterRecordId $requester.recordId -StateRoot $stateRoot
        $status.replyCount | Should Be 1
        $status.reply.requestKind | Should Be 'acceptance-response'
        $status.reply.inReplyToMessageId | Should Be $result.requestMessageId
        $status.reply.body | Should Be 'Accepted; evidence is complete.'
        $status.completed | Should Be $true
    }

    It 'does not send while the acceptance window remains Busy' {
        $stateRoot = Join-Path $TestDrive 'acceptance-busy-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        $requester = AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{ sessionId = '33333333-3333-3333-3333-333333333333'; responsibilityKey = 'feature-owner'; taskKey = 'busy'; tabTitle = 'ES-Owner'; processId = $PID; terminalMode = 'PlainCmd'; lifecycleStatus = 'Registered'; contextAccepted = $true; availability = 'Idle'; availabilityUpdatedUtc = [DateTime]::UtcNow.ToString('o'); availabilityExpiresUtc = [DateTime]::UtcNow.AddMinutes(5).ToString('o') })
        AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{ sessionId = '34343434-3434-3434-3434-343434343434'; responsibilityKey = 'engineering-acceptance'; taskKey = 'busy'; tabTitle = 'ES-Acceptance'; processId = $PID; terminalMode = 'PlainCmd'; lifecycleStatus = 'Registered'; contextAccepted = $true; availability = 'Busy'; availabilityUpdatedUtc = [DateTime]::UtcNow.ToString('o'); availabilityExpiresUtc = [DateTime]::UtcNow.AddMinutes(5).ToString('o') }) | Out-Null
        Save-ESCodexSessionRegistry $registryPath $registry
        $result = & (Join-Path $scriptsRoot 'Request-ESCodexAcceptance.ps1') -AcceptanceResponsibilityKey 'engineering-acceptance' -RequesterRecordId $requester.recordId -WaitSeconds 1 -PollMilliseconds 250 -StateRoot $stateRoot
        $result.sent | Should Be $false
        $result.timedOut | Should Be $true
        @(Find-ESCodexMessages $stateRoot).Count | Should Be 0
    }

    It 'auto-sends once on Stop after an explicit acceptance binding' {
        $stateRoot = Join-Path $TestDrive 'acceptance-auto-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        $owner = AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{ sessionId = '35353535-3535-3535-3535-353535353535'; responsibilityKey = 'feature-owner'; taskKey = 'auto'; tabTitle = 'ES-Owner'; processId = $PID; terminalMode = 'PlainCmd'; lifecycleStatus = 'Registered'; contextAccepted = $true; availability = 'Idle'; availabilityUpdatedUtc = [DateTime]::UtcNow.ToString('o'); availabilityExpiresUtc = [DateTime]::UtcNow.AddMinutes(5).ToString('o') })
        AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{ sessionId = '36363636-3636-3636-3636-363636363636'; responsibilityKey = 'engineering-acceptance'; taskKey = 'auto'; tabTitle = 'ES-Acceptance'; processId = $PID; terminalMode = 'PlainCmd'; lifecycleStatus = 'Registered'; contextAccepted = $true; availability = 'Idle'; availabilityUpdatedUtc = [DateTime]::UtcNow.ToString('o'); availabilityExpiresUtc = [DateTime]::UtcNow.AddMinutes(5).ToString('o') }) | Out-Null
        Save-ESCodexSessionRegistry $registryPath $registry
        $binding = & (Join-Path $scriptsRoot 'Set-ESCodexAcceptanceBinding.ps1') -RecordId $owner.recordId -AcceptanceResponsibilityKey 'engineering-acceptance' -StateRoot $stateRoot
        $binding.enabled | Should Be $true
        $route = (& (Join-Path $scriptsRoot 'Resolve-ESCodexSessionRoute.ps1') -RecordId $owner.recordId -RequireUnique -StateRoot $stateRoot).route
        $route.acceptanceOnCompletion | Should Be $true
        $hookInput = @{ hook_event_name = 'Stop'; session_id = $owner.sessionId; turn_id = 'auto-turn-1'; stop_hook_active = $false; last_assistant_message = 'feature task completed' } | ConvertTo-Json -Compress
        & (Join-Path $scriptsRoot 'Receive-ESCodexSessionMessageHook.ps1') -InputJson $hookInput -StateRoot $stateRoot | Out-Null
        $messages = @(Find-ESCodexMessages $stateRoot | Where-Object requestKind -eq 'acceptance-request')
        $messages.Count | Should Be 1
        $messages[0].replyToRecordId | Should Be $owner.recordId
        & (Join-Path $scriptsRoot 'Receive-ESCodexSessionMessageHook.ps1') -InputJson $hookInput -StateRoot $stateRoot | Out-Null
        @(Find-ESCodexMessages $stateRoot | Where-Object requestKind -eq 'acceptance-request').Count | Should Be 1
        & (Join-Path $scriptsRoot 'Set-ESCodexAcceptanceBinding.ps1') -RecordId $owner.recordId -AcceptanceResponsibilityKey 'engineering-acceptance' -Disable -StateRoot $stateRoot | Out-Null
        $disabled = (& (Join-Path $scriptsRoot 'Resolve-ESCodexSessionRoute.ps1') -RecordId $owner.recordId -RequireUnique -StateRoot $stateRoot).route
        $disabled.acceptanceOnCompletion | Should Be $false
    }
}
