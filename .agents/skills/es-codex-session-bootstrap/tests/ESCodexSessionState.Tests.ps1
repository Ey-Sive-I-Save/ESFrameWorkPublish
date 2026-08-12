$skillRoot = Split-Path -Parent $PSScriptRoot
$scriptsRoot = Join-Path $skillRoot 'scripts'
. (Join-Path $scriptsRoot 'ESCodexSessionState.ps1')
. (Join-Path $scriptsRoot 'ESCodexLaunchReadiness.ps1')

Describe 'ES Codex authoritative session registry' {
    It 'normalizes legacy records with a deterministic record ID' {
        $legacy = [pscustomobject]@{
            sessionId = '11111111-1111-1111-1111-111111111111'
            taskKey = 'task-a'
            tabTitle = 'ES-Test'
        }
        $first = ConvertTo-ESCodexSessionRecord $legacy
        $second = ConvertTo-ESCodexSessionRecord $legacy
        $first.recordId | Should Be $second.recordId
        $first.identityVersion | Should Be 1
    }

    It 'preserves an external CMD binding without fabricating a Codex SessionId' {
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        $bindingId = '12121212-1212-1212-1212-121212121212'
        $record = AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                taskKey = 'external-claim:' + $bindingId
                tabTitle = '已有 CMD'
                terminalMode = 'ExternalClaim'
                lifecycleStatus = 'ClaimedExternal'
                externalClaimId = '13131313-1313-1313-1313-131313131313'
                externalClaimBindingId = $bindingId
                externalClaimState = 'ClaimedExternal'
                externalClaimProcessId = 4321
                externalClaimProcessStartedAtUtc = '2026-08-12T00:00:00.0000000Z'
                externalClaimExpectedCmdProcessId = 4321
                externalClaimExpectedCmdProcessStartedAtUtc = '2026-08-12T00:00:00.0000000Z'
            })
        $record.sessionId | Should Be ''
        $record.lifecycleStatus | Should Be 'ClaimedExternal'
        $record.externalClaimBindingId | Should Be $bindingId
        $record.externalClaimExpectedCmdProcessId | Should Be 4321
        @($registry.sessions).Count | Should Be 1
    }

    It 'upserts a pending launch by launch token and later resolves its session ID' {
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        $pending = AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                launchToken = 'CodexLaunch:test-token'
                taskKey = 'task-a'
                lifecycleStatus = 'PendingRegistration'
            })
        $registered = AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                launchToken = 'CodexLaunch:test-token'
                sessionId = '22222222-2222-2222-2222-222222222222'
                taskKey = 'task-a'
                lifecycleStatus = 'Registered'
            })
        @($registry.sessions).Count | Should Be 1
        $registry.sessions[0].sessionId | Should Be '22222222-2222-2222-2222-222222222222'
        $registered.recordId | Should Be $pending.recordId
        $registry.sessions[0].recordId | Should Be $registered.recordId
    }

    It 'merges a pending launch-token record with an older exact-session record' {
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                recordId = 'old-session-record'
                sessionId = '29292929-2929-2929-2929-292929292929'
                taskKey = 'old-task'
                registeredAtUtc = '2026-08-01T00:00:00Z'
            }) | Out-Null
        AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                recordId = 'pending-launch-record'
                launchToken = 'CodexLaunch:merge-test'
                taskKey = 'new-task'
                registeredAtUtc = '2026-08-02T00:00:00Z'
            }) | Out-Null
        AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                launchToken = 'CodexLaunch:merge-test'
                sessionId = '29292929-2929-2929-2929-292929292929'
                taskKey = 'new-task'
                lifecycleStatus = 'Registered'
            }) | Out-Null
        @($registry.sessions).Count | Should Be 1
        $registry.sessions[0].recordId | Should Be 'old-session-record'
        $registry.sessions[0].launchToken | Should Be 'CodexLaunch:merge-test'
        $registry.sessions[0].sessionId | Should Be '29292929-2929-2929-2929-292929292929'
    }

    It 'writes schema v2 atomically and reads it back' {
        $path = Join-Path $TestDrive 'sessions.json'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                sessionId = '33333333-3333-3333-3333-333333333333'
                responsibilityKey = 'engineering-acceptance'
                taskKey = 'task-b'
                lifecycleStatus = 'Registered'
            }) | Out-Null
        Save-ESCodexSessionRegistry $path $registry
        $restored = Read-ESCodexSessionRegistry $path
        $restored.schemaVersion | Should Be 2
        $restored.revision | Should Be 1
        @($restored.sessions).Count | Should Be 1
    }

    It 'retries a short external registry file lock without losing the current record' {
        $path = Join-Path $TestDrive 'short-lock-sessions.json'
        $readyPath = Join-Path $TestDrive 'short-lock-ready.txt'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                sessionId = '34343434-3434-3434-3434-343434343434'
                taskKey = 'before-lock'
                lifecycleStatus = 'Registered'
            }) | Out-Null
        Save-ESCodexSessionRegistry $path $registry
        $job = Start-Job -ScriptBlock {
            param($registryPath, $signalPath)
            $stream = [IO.File]::Open($registryPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
            try {
                [IO.File]::WriteAllText($signalPath, 'locked', [Text.UTF8Encoding]::new($false))
                Start-Sleep -Milliseconds 350
            }
            finally { $stream.Dispose() }
        } -ArgumentList $path, $readyPath
        try {
            for ($attempt = 0; $attempt -lt 20 -and -not (Test-Path -LiteralPath $readyPath -PathType Leaf); $attempt++) {
                Start-Sleep -Milliseconds 50
            }
            Test-Path -LiteralPath $readyPath -PathType Leaf | Should Be $true
            Invoke-ESCodexRegistryUpdate -Path $path -Update {
                param($currentRegistry, $unused)
                AddOrUpdate-ESCodexSessionRecord $currentRegistry ([pscustomobject]@{
                        sessionId = '35353535-3535-3535-3535-353535353535'
                        taskKey = 'after-lock'
                        lifecycleStatus = 'Registered'
                    })
            } | Out-Null
            Wait-Job $job -Timeout 5 | Out-Null
            @($job.ChildJobs[0].JobStateInfo.State) -contains 'Completed' | Should Be $true
            $restored = Read-ESCodexSessionRegistry $path
            $restored.revision | Should Be 2
            @($restored.sessions).Count | Should Be 2
            @($restored.sessions | Where-Object sessionId -eq '34343434-3434-3434-3434-343434343434').Count | Should Be 1
        }
        finally { Remove-Job $job -Force -ErrorAction SilentlyContinue }
    }

    It 'rejects duplicate launch tokens instead of guessing ownership' {
        $path = Join-Path $TestDrive 'duplicate.json'
        $payload = @{
            schemaVersion = 2
            sessions = @(
                @{ recordId = 'a'; launchToken = 'same'; sessionId = '44444444-4444-4444-4444-444444444444' },
                @{ recordId = 'b'; launchToken = 'same'; sessionId = '55555555-5555-5555-5555-555555555555' }
            )
        } | ConvertTo-Json -Depth 6
        [IO.File]::WriteAllText($path, $payload, [Text.UTF8Encoding]::new($false))
        { Read-ESCodexSessionRegistry $path } | Should Throw
    }

    It 'rejects duplicate session IDs instead of creating competing authority' {
        $path = Join-Path $TestDrive 'duplicate-session.json'
        $payload = @{
            schemaVersion = 2
            sessions = @(
                @{ recordId = 'c'; launchToken = 'one'; sessionId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb' },
                @{ recordId = 'd'; launchToken = 'two'; sessionId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb' }
            )
        } | ConvertTo-Json -Depth 6
        [IO.File]::WriteAllText($path, $payload, [Text.UTF8Encoding]::new($false))
        { Read-ESCodexSessionRegistry $path } | Should Throw
    }

    It 'rejects identity-empty records before they can corrupt authority' {
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        { AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{ responsibilityKey = 'default' }) } | Should Throw
        @($registry.sessions).Count | Should Be 0
    }

    It 'passes registry update arguments explicitly across function scope' {
        $path = Join-Path $TestDrive 'explicit-update-arguments.json'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        Save-ESCodexSessionRegistry $path $registry
        $explicitRegistryUpdateArgument = [pscustomobject]@{ entry = [pscustomobject]@{ launchToken = 'CodexLaunch:explicit-argument'; taskKey = 'explicit-argument'; responsibilityKey = 'test'; lifecycleStatus = 'PendingRegistration' } }
        Invoke-ESCodexRegistryUpdate -Path $path -Update {
            param($currentRegistry, $explicitContext)
            AddOrUpdate-ESCodexSessionRecord $currentRegistry $explicitContext.entry
        } -Argument $explicitRegistryUpdateArgument | Out-Null
        $restored = Read-ESCodexSessionRegistry $path
        @($restored.sessions).Count | Should Be 1
        $restored.sessions[0].launchToken | Should Be 'CodexLaunch:explicit-argument'
    }
}

Describe 'ES Codex launch delivery evidence' {
    It 'distinguishes terminal start, prompt observation, and context acceptance' {
        $root = Join-Path $TestDrive 'readiness'
        $receiptRoot = Join-Path $root 'acceptance-receipts'
        [void][IO.Directory]::CreateDirectory($receiptRoot)
        $historyPath = Join-Path $root 'history.jsonl'
        $envelopePath = Join-Path $root 'envelope.json'
        $token = 'CodexLaunch:readiness-test'
        [IO.File]::WriteAllText($envelopePath, '{"schemaVersion":2}', [Text.UTF8Encoding]::new($false))

        $terminal = Get-ESCodexLaunchReadiness -LaunchToken $token -EnvelopePath $envelopePath -ProjectRoot $root -ReceiptRoot $receiptRoot -HistoryPath $historyPath -StartedAtUnix 100
        $terminal.launchPhase | Should Be 'TerminalStarted'
        $terminal.promptObserved | Should Be $false
        $terminal.contextAccepted | Should Be $false

        $history = @{ session_id = '30303030-3030-3030-3030-303030303030'; ts = 101; text = "initialize $token" } | ConvertTo-Json -Compress
        [IO.File]::WriteAllText($historyPath, $history, [Text.UTF8Encoding]::new($false))
        $prompt = Get-ESCodexLaunchReadiness -LaunchToken $token -EnvelopePath $envelopePath -ProjectRoot $root -ReceiptRoot $receiptRoot -HistoryPath $historyPath -StartedAtUnix 100
        $prompt.launchPhase | Should Be 'PromptObserved'
        $prompt.sessionId | Should Be '30303030-3030-3030-3030-303030303030'

        $receiptPath = Get-ESCodexLaunchReceiptPath $receiptRoot $token
        $receipt = [ordered]@{
            schemaVersion = 2
            launchToken = $token
            envelopePath = $envelopePath
            envelopeSha256 = Get-ESCodexLaunchFileSha256 $envelopePath
            projectRoot = $root
        } | ConvertTo-Json
        [IO.File]::WriteAllText($receiptPath, $receipt, [Text.UTF8Encoding]::new($false))
        $accepted = Get-ESCodexLaunchReadiness -LaunchToken $token -EnvelopePath $envelopePath -ProjectRoot $root -ReceiptRoot $receiptRoot -HistoryPath $historyPath -StartedAtUnix 100
        $accepted.launchPhase | Should Be 'ContextAccepted'
        $accepted.contextAccepted | Should Be $true
        $accepted.acceptanceReceiptPath | Should Be $receiptPath
    }

    It 'hard-fails conflicting acceptance receipts and early Codex exits' {
        $root = Join-Path $TestDrive 'failure-readiness'
        $receiptRoot = Join-Path $root 'acceptance-receipts'
        [void][IO.Directory]::CreateDirectory($receiptRoot)
        $envelopePath = Join-Path $root 'envelope.json'
        [IO.File]::WriteAllText($envelopePath, '{}', [Text.UTF8Encoding]::new($false))
        $badToken = 'CodexLaunch:bad-receipt'
        $badReceiptPath = Get-ESCodexLaunchReceiptPath $receiptRoot $badToken
        [IO.File]::WriteAllText($badReceiptPath, '{"schemaVersion":2,"launchToken":"wrong","envelopePath":"wrong","envelopeSha256":"wrong","projectRoot":"wrong"}', [Text.UTF8Encoding]::new($false))
        $bad = Get-ESCodexLaunchReadiness -LaunchToken $badToken -EnvelopePath $envelopePath -ProjectRoot $root -ReceiptRoot $receiptRoot
        $bad.launchPhase | Should Be 'Failed'
        $bad.startupFailed | Should Be $true

        $exitToken = 'CodexLaunch:early-exit'
        $exitMarker = Join-Path $root 'exit.json'
        [IO.File]::WriteAllText($exitMarker, (@{ schemaVersion = 1; launchToken = $exitToken; exitCode = 7 } | ConvertTo-Json -Compress), [Text.UTF8Encoding]::new($false))
        $exited = Get-ESCodexLaunchReadiness -LaunchToken $exitToken -EnvelopePath $envelopePath -ProjectRoot $root -ReceiptRoot $receiptRoot -ExitMarkerPath $exitMarker
        $exited.launchPhase | Should Be 'Failed'
        $exited.failureReason | Should Match 'exit=7'
    }

    It 'does not misclassify a just-created partial receipt as a permanent conflict' {
        $root = Join-Path $TestDrive 'partial-receipt'
        $receiptRoot = Join-Path $root 'acceptance-receipts'
        [void][IO.Directory]::CreateDirectory($receiptRoot)
        $envelopePath = Join-Path $root 'envelope.json'
        $token = 'CodexLaunch:partial-receipt'
        [IO.File]::WriteAllText($envelopePath, '{}', [Text.UTF8Encoding]::new($false))
        $receiptPath = Get-ESCodexLaunchReceiptPath $receiptRoot $token
        [IO.File]::WriteAllText($receiptPath, '{', [Text.UTF8Encoding]::new($false))
        $writing = Get-ESCodexLaunchReadiness -LaunchToken $token -EnvelopePath $envelopePath -ProjectRoot $root -ReceiptRoot $receiptRoot
        $writing.startupFailed | Should Be $false
        [IO.File]::SetLastWriteTimeUtc($receiptPath, [DateTime]::UtcNow.AddSeconds(-5))
        $stale = Get-ESCodexLaunchReadiness -LaunchToken $token -EnvelopePath $envelopePath -ProjectRoot $root -ReceiptRoot $receiptRoot
        $stale.startupFailed | Should Be $true
    }

    It 'rejects managed Resume and Fork picker launches and defaults to a sixty-second evidence wait' {
        $launcher = Join-Path $scriptsRoot 'Start-ESCodexSession.ps1'
        { & $launcher -Mode Resume -DryRun } | Should Throw
        { & $launcher -Mode Fork -DryRun } | Should Throw
        $source = Get-Content -LiteralPath $launcher -Raw -Encoding UTF8
        $source | Should Match '\[int\]\$StartupWaitSeconds = 60'
        $source | Should Match 'official picker cannot append the mandatory launch-envelope prompt'
    }

    It 'uses create-only flushed command wrappers rather than overwrite writes' {
        $launcher = Join-Path $scriptsRoot 'Start-ESCodexSession.ps1'
        $source = Get-Content -LiteralPath $launcher -Raw -Encoding UTF8
        $source | Should Match 'function Write-CreateOnlyUtf8File'
        $source | Should Match '\[IO\.FileMode\]::CreateNew'
        $source | Should Match '\$stream\.Flush\(\$true\)'
        $source | Should Match 'Write-CreateOnlyUtf8File \$commandWrapperPath \$commandWrapperContent'
        $source | Should Not Match '\[IO\.File\]::WriteAllText\(\$commandWrapperPath'
    }

    It 'creates an exit marker exactly once and rejects a collision' {
        $writer = Join-Path $scriptsRoot 'Write-ESCodexLaunchExitMarker.ps1'
        $root = Join-Path $TestDrive 'exit-marker-root'
        [void][IO.Directory]::CreateDirectory($root)
        $marker = Join-Path $root 'launch.exit.json'
        & $writer -Path $marker -ExpectedRoot $root -LaunchToken 'CodexLaunch:exit-marker-test' -ExitCode 7
        $first = Get-Content -LiteralPath $marker -Raw -Encoding UTF8 | ConvertFrom-Json
        $first.launchToken | Should Be 'CodexLaunch:exit-marker-test'
        $first.exitCode | Should Be 7
        { & $writer -Path $marker -ExpectedRoot $root -LaunchToken 'CodexLaunch:exit-marker-test' -ExitCode 0 } | Should Throw
        ($first | ConvertTo-Json -Compress) | Should Be ((Get-Content -LiteralPath $marker -Raw -Encoding UTF8 | ConvertFrom-Json) | ConvertTo-Json -Compress)
    }
}

Describe 'ES Codex repair and close safety' {
    It 'plans and explicitly repairs duplicate identity-empty placeholders with a backup' {
        $stateRoot = Join-Path $TestDrive 'corrupt-placeholder-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $payload = [ordered]@{
            schemaVersion = 2
            revision = 3
            updatedUtc = [DateTime]::UtcNow.ToString('o')
            sessions = @(
                [ordered]@{ recordId = '45ca31c3315a5978f40438aab46040d7'; responsibilityKey = 'default' },
                [ordered]@{ recordId = '45ca31c3315a5978f40438aab46040d7'; responsibilityKey = 'default' },
                [ordered]@{ recordId = 'abababababababababababababababab'; sessionId = '23232323-2323-2323-2323-232323232323'; taskKey = 'valid'; responsibilityKey = 'valid'; lifecycleStatus = 'Registered' }
            )
        }
        [IO.File]::WriteAllText($registryPath, ($payload | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
        $before = [IO.File]::ReadAllBytes($registryPath)
        $plan = & (Join-Path $scriptsRoot 'Repair-ESCodexSessionState.ps1') -StateRoot $stateRoot
        $plan.corruptionMode | Should Be $true
        @($plan.proposedActions | Where-Object applicable).Count | Should Be 2
        [Convert]::ToBase64String($before) | Should Be ([Convert]::ToBase64String([IO.File]::ReadAllBytes($registryPath)))
        $applied = & (Join-Path $scriptsRoot 'Repair-ESCodexSessionState.ps1') -StateRoot $stateRoot -Apply
        $applied.appliedActions.Count | Should Be 2
        Test-Path -LiteralPath $applied.backupPath | Should Be $true
        $restored = Read-ESCodexSessionRegistry $registryPath
        @($restored.sessions).Count | Should Be 1
        $restored.revision | Should Be 4
    }

    It 'removes a single identity-empty placeholder only with Apply and preserves a backup' {
        $stateRoot = Join-Path $TestDrive 'single-placeholder-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $payload = [ordered]@{
            schemaVersion = 2; revision = 1; updatedUtc = [DateTime]::UtcNow.ToString('o'); sessions = @(
                [ordered]@{ recordId = '45ca31c3315a5978f40438aab46040d7'; responsibilityKey = 'default' },
                [ordered]@{ recordId = 'cdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcd'; sessionId = '24242424-2424-2424-2424-242424242424'; taskKey = 'valid'; responsibilityKey = 'valid'; lifecycleStatus = 'Registered' }
            )
        }
        [IO.File]::WriteAllText($registryPath, ($payload | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
        $plan = & (Join-Path $scriptsRoot 'Repair-ESCodexSessionState.ps1') -StateRoot $stateRoot
        @($plan.proposedActions | Where-Object action -eq 'RemoveIdentityEmptyPlaceholder').Count | Should Be 1
        @((Read-ESCodexSessionRegistry $registryPath).sessions).Count | Should Be 2
        $applied = & (Join-Path $scriptsRoot 'Repair-ESCodexSessionState.ps1') -StateRoot $stateRoot -Apply
        Test-Path -LiteralPath $applied.backupPath | Should Be $true
        @((Read-ESCodexSessionRegistry $registryPath).sessions).Count | Should Be 1
    }

    It 'keeps Repair read-only unless Apply is explicit' {
        $stateRoot = Join-Path $TestDrive 'repair-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                sessionId = '66666666-6666-6666-6666-666666666666'
                responsibilityKey = 'test'
                taskKey = 'lost-task'
                tabTitle = 'ES-Nonexistent-Test-Tab'
                processId = 999999
                lifecycleStatus = 'Registered'
            }) | Out-Null
        Save-ESCodexSessionRegistry $registryPath $registry
        $before = [IO.File]::ReadAllBytes($registryPath)
        $result = & (Join-Path $scriptsRoot 'Repair-ESCodexSessionState.ps1') -StateRoot $stateRoot
        $after = [IO.File]::ReadAllBytes($registryPath)
        $result.dryRun | Should Be $true
        [Convert]::ToBase64String($before) | Should Be ([Convert]::ToBase64String($after))
    }

    It 'blocks ambiguous registry matches unless AllMatches is explicit' {
        $stateRoot = Join-Path $TestDrive 'close-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        foreach ($id in @('77777777-7777-7777-7777-777777777777', '88888888-8888-8888-8888-888888888888')) {
            AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                    sessionId = $id
                    responsibilityKey = 'shared-test'
                    taskKey = $id
                    tabTitle = 'ES-Shared-Test'
                    lifecycleStatus = 'Registered'
                }) | Out-Null
        }
        Save-ESCodexSessionRegistry $registryPath $registry
        { & (Join-Path $scriptsRoot 'Close-ESCodexSession.ps1') -ResponsibilityKey 'shared-test' -DryRun -StateRoot $stateRoot } | Should Throw
    }

    It 'hydrates legacy authority fields only when Repair Apply is explicit' {
        $stateRoot = Join-Path $TestDrive 'hydrate-state'
        $launchStateRoot = Join-Path $stateRoot 'launch-state'
        [void][IO.Directory]::CreateDirectory($launchStateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $sessionId = '99999999-9999-9999-9999-999999999999'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                sessionId = $sessionId
                responsibilityKey = 'hydrate-test'
                taskKey = 'hydrate-task'
                tabTitle = 'ES-Hydrate-Test'
                lifecycleStatus = 'Registered'
            }) | Out-Null
        Save-ESCodexSessionRegistry $registryPath $registry
        $launchState = @{
            sessionId = $sessionId
            processId = $PID
            launchToken = 'CodexLaunch:hydrate-test'
            terminalMode = 'PlainCmd'
            handoffSnapshotDirectory = (Join-Path $stateRoot 'snapshot')
        } | ConvertTo-Json
        [IO.File]::WriteAllText((Join-Path $launchStateRoot 'hydrate.json'), $launchState, [Text.UTF8Encoding]::new($false))
        $result = & (Join-Path $scriptsRoot 'Repair-ESCodexSessionState.ps1') -StateRoot $stateRoot -Apply
        $restored = Read-ESCodexSessionRegistry $registryPath
        $result.dryRun | Should Be $false
        $restored.sessions[0].processId | Should Be $PID
        $restored.sessions[0].launchToken | Should Be 'CodexLaunch:hydrate-test'
    }

    It 'refuses Close when a live process has no uniquely located visual tab' {
        $stateRoot = Join-Path $TestDrive 'missing-tab-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                sessionId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
                responsibilityKey = 'missing-tab-test'
                taskKey = 'missing-tab-task'
                tabTitle = 'ES-Definitely-Not-A-Visible-Tab'
                processId = $PID
                terminalMode = 'ProjectWindow'
                lifecycleStatus = 'Registered'
            }) | Out-Null
        Save-ESCodexSessionRegistry $registryPath $registry
        { & (Join-Path $scriptsRoot 'Close-ESCodexSession.ps1') -SessionId 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' -DryRun -StateRoot $stateRoot } | Should Throw
    }
}

Describe 'ES Codex route query and presence' {
    It 'resolves one responsibility to stable binding and message IDs' {
        $stateRoot = Join-Path $TestDrive 'route-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                sessionId = 'cccccccc-cccc-cccc-cccc-cccccccccccc'
                responsibilityKey = 'route-test'
                taskKey = 'route-task'
                tabTitle = 'ES-Route-Test'
                processId = $PID
                terminalMode = 'PlainCmd'
                launchToken = 'CodexLaunch:route-test'
                lifecycleStatus = 'Registered'
                contextAccepted = $true
            }) | Out-Null
        Save-ESCodexSessionRegistry $registryPath $registry
        $result = & (Join-Path $scriptsRoot 'Resolve-ESCodexSessionRoute.ps1') -ResponsibilityKey 'route-test' -RequireUnique -StateRoot $stateRoot
        $result.unique | Should Be $true
        $result.route.bindingTargetId | Should Match '^record:'
        $result.route.messageTargetId | Should Be 'session:cccccccc-cccc-cccc-cccc-cccccccccccc'
        $result.route.waitState | Should Be 'UnknownAvailability'
    }

    It 'resolves Current from an explicit launch token without guessing' {
        $stateRoot = Join-Path $TestDrive 'current-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                sessionId = 'dddddddd-dddd-dddd-dddd-dddddddddddd'
                responsibilityKey = 'current-test'
                taskKey = 'current-task'
                tabTitle = 'ES-Current-Test'
                processId = $PID
                terminalMode = 'PlainCmd'
                launchToken = 'CodexLaunch:current-test'
                lifecycleStatus = 'Registered'
                contextAccepted = $true
            }) | Out-Null
        Save-ESCodexSessionRegistry $registryPath $registry
        $result = & (Join-Path $scriptsRoot 'Resolve-ESCodexSessionRoute.ps1') -Current -LaunchToken 'CodexLaunch:current-test' -RequireUnique -StateRoot $stateRoot
        $result.route.sessionId | Should Be 'dddddddd-dddd-dddd-dddd-dddddddddddd'
        @($result.resolutionEvidence) -contains 'launchToken' | Should Be $true
    }

    It 'does not invoke CIM ancestry discovery when a launch token already resolves Current' {
        Mock Get-CimInstance { throw 'CIM ancestry discovery must not run for a token-resolved session.' }
        $previousToken = $env:ES_CODEX_LAUNCH_TOKEN
        try {
            $env:ES_CODEX_LAUNCH_TOKEN = 'CodexLaunch:fast-current-test'
            $context = Get-ESCodexCurrentProcessContext
            @($context.launchTokens) -contains 'CodexLaunch:fast-current-test' | Should Be $true
            $context.processAncestryAttempted | Should Be $false
            Assert-MockCalled Get-CimInstance 0
        }
        finally {
            $env:ES_CODEX_LAUNCH_TOKEN = $previousToken
        }
    }

    It 'keeps route queries independent from Windows Terminal UI observation' {
        $source = Get-Content -LiteralPath (Join-Path $scriptsRoot 'Resolve-ESCodexSessionRoute.ps1') -Raw -Encoding UTF8
        $source | Should Match 'SkipUiObservation\s*=\s*\$true'
        $source | Should Match 'SkipReadinessRefresh\s*=\s*\$true'
    }

    It 'publishes Idle presence and Wait observes Ready without touching another session' {
        $stateRoot = Join-Path $TestDrive 'presence-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        $record = AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                sessionId = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee'
                responsibilityKey = 'presence-test'
                taskKey = 'presence-task'
                tabTitle = 'ES-Presence-Test'
                processId = $PID
                terminalMode = 'PlainCmd'
                lifecycleStatus = 'Registered'
                contextAccepted = $true
            })
        Save-ESCodexSessionRegistry $registryPath $registry
        $presence = & (Join-Path $scriptsRoot 'Set-ESCodexSessionPresence.ps1') -RecordId $record.recordId -Availability Idle -ActivityKey 'ready' -StateRoot $stateRoot
        $presence.route.waitState | Should Be 'Ready'
        $wait = & (Join-Path $scriptsRoot 'Wait-ESCodexSessionRoute.ps1') -RecordId $record.recordId -WaitFor Ready -TimeoutSeconds 0 -StateRoot $stateRoot
        $wait.completed | Should Be $true
        $wait.timedOut | Should Be $false
    }

    It 'binds one exact record and rejects a responsibility owned by another live record' {
        $stateRoot = Join-Path $TestDrive 'bind-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        $first = AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                sessionId = 'ffffffff-ffff-ffff-ffff-ffffffffffff'
                responsibilityKey = 'old-role'
                taskKey = 'first'
                tabTitle = 'ES-First'
                processId = $PID
                terminalMode = 'PlainCmd'
                lifecycleStatus = 'Registered'
                contextAccepted = $true
            })
        AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                sessionId = '12121212-1212-1212-1212-121212121212'
                responsibilityKey = 'owned-role'
                taskKey = 'second'
                tabTitle = 'ES-Second'
                processId = $PID
                terminalMode = 'PlainCmd'
                lifecycleStatus = 'Registered'
                contextAccepted = $true
            }) | Out-Null
        Save-ESCodexSessionRegistry $registryPath $registry
        $bound = & (Join-Path $scriptsRoot 'Set-ESCodexSessionResponsibility.ps1') -RecordId $first.recordId -NewResponsibilityKey 'new-role' -StateRoot $stateRoot
        $bound.route.responsibilityKey | Should Be 'new-role'
        { & (Join-Path $scriptsRoot 'Set-ESCodexSessionResponsibility.ps1') -RecordId $first.recordId -NewResponsibilityKey 'owned-role' -StateRoot $stateRoot } | Should Throw
    }

    It 'treats expired presence as Unknown without rewriting the registry' {
        $stateRoot = Join-Path $TestDrive 'expired-presence-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        $record = AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                sessionId = '13131313-1313-1313-1313-131313131313'
                responsibilityKey = 'expired-presence'
                processId = $PID
                terminalMode = 'PlainCmd'
                lifecycleStatus = 'Registered'
                contextAccepted = $true
                availability = 'Busy'
                availabilityUpdatedUtc = [DateTime]::UtcNow.AddMinutes(-2).ToString('o')
                availabilityExpiresUtc = [DateTime]::UtcNow.AddMinutes(-1).ToString('o')
            })
        Save-ESCodexSessionRegistry $registryPath $registry
        $before = [IO.File]::ReadAllBytes($registryPath)
        $result = & (Join-Path $scriptsRoot 'Resolve-ESCodexSessionRoute.ps1') -RecordId $record.recordId -RequireUnique -StateRoot $stateRoot
        $after = [IO.File]::ReadAllBytes($registryPath)
        $result.route.availabilityStale | Should Be $true
        $result.route.effectiveAvailability | Should Be 'Unknown'
        [Convert]::ToBase64String($before) | Should Be ([Convert]::ToBase64String($after))
    }

    It 'rejects a stale registry revision instead of overwriting newer presence' {
        $stateRoot = Join-Path $TestDrive 'registry-cas-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        $record = AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                sessionId = '14141414-1414-1414-1414-141414141414'
                responsibilityKey = 'registry-cas'
                processId = $PID
                terminalMode = 'PlainCmd'
                lifecycleStatus = 'Registered'
                contextAccepted = $true
            })
        Save-ESCodexSessionRegistry $registryPath $registry
        & (Join-Path $scriptsRoot 'Set-ESCodexSessionPresence.ps1') -RecordId $record.recordId -Availability Busy -ExpectedRegistryRevision 1 -StateRoot $stateRoot | Out-Null
        { & (Join-Path $scriptsRoot 'Set-ESCodexSessionPresence.ps1') -RecordId $record.recordId -Availability Idle -ExpectedRegistryRevision 1 -StateRoot $stateRoot } | Should Throw
        $resolved = & (Join-Path $scriptsRoot 'Resolve-ESCodexSessionRoute.ps1') -RecordId $record.recordId -RequireUnique -StateRoot $stateRoot
        $resolved.route.effectiveAvailability | Should Be 'Busy'
    }

    It 'wakes Wait after a real Busy to Idle update from another process' {
        $stateRoot = Join-Path $TestDrive 'wait-wakeup-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        $record = AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                sessionId = '17171717-1717-1717-1717-171717171717'
                responsibilityKey = 'wait-wakeup'
                processId = $PID
                terminalMode = 'PlainCmd'
                lifecycleStatus = 'Registered'
                contextAccepted = $true
                availability = 'Busy'
                availabilityUpdatedUtc = [DateTime]::UtcNow.ToString('o')
                availabilityExpiresUtc = [DateTime]::UtcNow.AddMinutes(5).ToString('o')
            })
        Save-ESCodexSessionRegistry $registryPath $registry
        $presenceScript = Join-Path $scriptsRoot 'Set-ESCodexSessionPresence.ps1'
        $job = Start-Job -ScriptBlock {
            param($scriptPath, $targetRoot, $targetRecordId)
            Start-Sleep -Milliseconds 600
            & $scriptPath -RecordId $targetRecordId -Availability Idle -StateRoot $targetRoot | Out-Null
        } -ArgumentList $presenceScript, $stateRoot, $record.recordId
        try {
            $wait = & (Join-Path $scriptsRoot 'Wait-ESCodexSessionRoute.ps1') -RecordId $record.recordId -WaitFor Ready -TimeoutSeconds 5 -PollMilliseconds 250 -StateRoot $stateRoot
            $wait.completed | Should Be $true
            $wait.query.route.effectiveAvailability | Should Be 'Idle'
            Wait-Job $job -Timeout 5 | Out-Null
            @($job.ChildJobs[0].JobStateInfo.State) -contains 'Completed' | Should Be $true
        }
        finally { Remove-Job $job -Force -ErrorAction SilentlyContinue }
    }

    It 'allows only one concurrent writer for the same expected registry revision' {
        $stateRoot = Join-Path $TestDrive 'concurrent-cas-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        $record = AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                sessionId = '18181818-1818-1818-1818-181818181818'
                responsibilityKey = 'concurrent-cas'
                processId = $PID
                terminalMode = 'PlainCmd'
                lifecycleStatus = 'Registered'
                contextAccepted = $true
            })
        Save-ESCodexSessionRegistry $registryPath $registry
        $presenceScript = Join-Path $scriptsRoot 'Set-ESCodexSessionPresence.ps1'
        $jobs = @('Busy', 'Idle') | ForEach-Object {
            Start-Job -ScriptBlock {
                param($scriptPath, $targetRoot, $targetRecordId, $availability)
                try {
                    & $scriptPath -RecordId $targetRecordId -Availability $availability -ExpectedRegistryRevision 1 -StateRoot $targetRoot | Out-Null
                    [pscustomobject]@{ succeeded = $true; availability = $availability }
                }
                catch { [pscustomobject]@{ succeeded = $false; availability = $availability; error = $_.Exception.Message } }
            } -ArgumentList $presenceScript, $stateRoot, $record.recordId, $_
        }
        try {
            $results = @($jobs | Wait-Job | Receive-Job)
            @($results | Where-Object succeeded).Count | Should Be 1
            @($results | Where-Object { -not $_.succeeded -and $_.error -like '*revision conflict*' }).Count | Should Be 1
            (Read-ESCodexSessionRegistry $registryPath).revision | Should Be 2
        }
        finally { $jobs | Remove-Job -Force -ErrorAction SilentlyContinue }
    }
}

Describe 'ES Codex cooperative message mailbox' {
    . (Join-Path $scriptsRoot 'ESCodexSessionMessageState.ps1')

    It 'queues idempotently and exposes exact non-delivery status' {
        $stateRoot = Join-Path $TestDrive 'message-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        $record = AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                sessionId = '15151515-1515-1515-1515-151515151515'
                responsibilityKey = 'message-target'
                processId = $PID
                terminalMode = 'PlainCmd'
                lifecycleStatus = 'Registered'
                availability = 'Idle'
                availabilityUpdatedUtc = [DateTime]::UtcNow.ToString('o')
                availabilityExpiresUtc = [DateTime]::UtcNow.AddMinutes(5).ToString('o')
                contextAccepted = $true
            })
        Save-ESCodexSessionRegistry $registryPath $registry
        $first = & (Join-Path $scriptsRoot 'Publish-ESCodexSessionMessage.ps1') -RecordId $record.recordId -Body 'validate feature' -IdempotencyKey 'acceptance-1' -RequireReady -StateRoot $stateRoot
        $second = & (Join-Path $scriptsRoot 'Publish-ESCodexSessionMessage.ps1') -RecordId $record.recordId -Body 'validate feature' -IdempotencyKey 'acceptance-1' -RequireReady -StateRoot $stateRoot
        $first.messageId | Should Be $second.messageId
        $first.effectiveStatus | Should Be 'queued'
        $status = & (Join-Path $scriptsRoot 'Get-ESCodexSessionMessage.ps1') -MessageId $first.messageId -StateRoot $stateRoot
        $status.directCodexInjectionSupported | Should Be $false
        $status.matchedCount | Should Be 1
        $sessionStatus = & (Join-Path $scriptsRoot 'Get-ESCodexSessionStatus.ps1') -ResponsibilityKey 'message-target' -StateRoot $stateRoot
        $sessionStatus.totalMessages | Should Be 1
        $sessionStatus.pendingMessages | Should Be 1
        $sessionStatus.sessions[0].pendingMessageCount | Should Be 1
    }

    It 'uses CAS for receipts and rejects invalid or stale transitions' {
        $stateRoot = Join-Path $TestDrive 'message-receipt-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        $record = AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{
                sessionId = '16161616-1616-1616-1616-161616161616'
                responsibilityKey = 'receipt-target'
                processId = $PID
                terminalMode = 'PlainCmd'
                lifecycleStatus = 'Registered'
            })
        Save-ESCodexSessionRegistry $registryPath $registry
        $message = & (Join-Path $scriptsRoot 'Publish-ESCodexSessionMessage.ps1') -RecordId $record.recordId -Body 'receipt test' -StateRoot $stateRoot
        $accepted = & (Join-Path $scriptsRoot 'Set-ESCodexSessionMessageStatus.ps1') -MessageId $message.messageId -Status accepted -AcceptedByRecordId $record.recordId -ExpectedStateRevision 1 -StateRoot $stateRoot
        $accepted.stateRevision | Should Be 2
        { & (Join-Path $scriptsRoot 'Set-ESCodexSessionMessageStatus.ps1') -MessageId $message.messageId -Status completed -ExpectedStateRevision 1 -StateRoot $stateRoot } | Should Throw
        $completed = & (Join-Path $scriptsRoot 'Set-ESCodexSessionMessageStatus.ps1') -MessageId $message.messageId -Status completed -ExpectedStateRevision 2 -StateRoot $stateRoot
        $completed.effectiveStatus | Should Be 'completed'
        { & (Join-Path $scriptsRoot 'Set-ESCodexSessionMessageStatus.ps1') -MessageId $message.messageId -Status accepted -StateRoot $stateRoot } | Should Throw
    }

    It 'reports TTL expiry without claiming delivery' {
        $stateRoot = Join-Path $TestDrive 'message-expiry-state'
        . (Join-Path $scriptsRoot 'ESCodexSessionMessageState.ps1')
        $messageId = [Guid]::NewGuid().ToString()
        $paths = Get-ESCodexMessagePaths $stateRoot $messageId
        Write-ESCodexCreateOnlyJson $paths.requestPath ([ordered]@{ schemaVersion = 1; messageId = $messageId; idempotencyKey = ''; targetRecordId = 'target'; targetSessionId = ''; targetResponsibilityKey = 'test'; priority = 'normal'; body = 'expired'; createdUtc = [DateTime]::UtcNow.AddMinutes(-2).ToString('o'); expiresUtc = [DateTime]::UtcNow.AddMinutes(-1).ToString('o'); deliveryMode = 'cooperative-mailbox' })
        Write-ESCodexCreateOnlyJson $paths.statePath ([ordered]@{ schemaVersion = 1; messageId = $messageId; revision = 1; status = 'queued'; updatedUtc = [DateTime]::UtcNow.AddMinutes(-2).ToString('o'); acceptedByRecordId = ''; note = '' })
        $message = Read-ESCodexMessage $stateRoot $messageId
        $message.status | Should Be 'queued'
        $message.effectiveStatus | Should Be 'expired'
        $message.expired | Should Be $true
    }

    It 'keeps message repair read-only by default and applies expiry explicitly' {
        $stateRoot = Join-Path $TestDrive 'message-repair-state'
        . (Join-Path $scriptsRoot 'ESCodexSessionMessageState.ps1')
        $messageId = [Guid]::NewGuid().ToString()
        $paths = Get-ESCodexMessagePaths $stateRoot $messageId
        Write-ESCodexCreateOnlyJson $paths.requestPath ([ordered]@{ schemaVersion = 1; messageId = $messageId; idempotencyKey = ''; targetRecordId = 'target'; targetSessionId = ''; targetResponsibilityKey = 'test'; priority = 'normal'; body = 'expired'; createdUtc = [DateTime]::UtcNow.AddMinutes(-2).ToString('o'); expiresUtc = [DateTime]::UtcNow.AddMinutes(-1).ToString('o'); deliveryMode = 'cooperative-mailbox' })
        Write-ESCodexCreateOnlyJson $paths.statePath ([ordered]@{ schemaVersion = 1; messageId = $messageId; revision = 1; status = 'queued'; updatedUtc = [DateTime]::UtcNow.AddMinutes(-2).ToString('o'); acceptedByRecordId = ''; note = '' })
        $before = [IO.File]::ReadAllBytes($paths.statePath)
        $plan = & (Join-Path $scriptsRoot 'Repair-ESCodexSessionMessages.ps1') -StateRoot $stateRoot
        $afterPlan = [IO.File]::ReadAllBytes($paths.statePath)
        $plan.dryRun | Should Be $true
        $plan.plannedCount | Should Be 1
        [Convert]::ToBase64String($before) | Should Be ([Convert]::ToBase64String($afterPlan))
        $applied = & (Join-Path $scriptsRoot 'Repair-ESCodexSessionMessages.ps1') -StateRoot $stateRoot -Apply
        $applied.appliedCount | Should Be 1
        (Read-ESCodexMessage $stateRoot $messageId).status | Should Be 'expired'
    }

    It 'deletes only explicitly requested old terminal message pairs' {
        $stateRoot = Join-Path $TestDrive 'message-cleanup-state'
        . (Join-Path $scriptsRoot 'ESCodexSessionMessageState.ps1')
        $messageId = [Guid]::NewGuid().ToString()
        $paths = Get-ESCodexMessagePaths $stateRoot $messageId
        $old = [DateTime]::UtcNow.AddDays(-40).ToString('o')
        Write-ESCodexCreateOnlyJson $paths.requestPath ([ordered]@{ schemaVersion = 1; messageId = $messageId; idempotencyKey = ''; targetRecordId = 'target'; targetSessionId = ''; targetResponsibilityKey = 'test'; priority = 'normal'; body = 'done'; createdUtc = $old; expiresUtc = [DateTime]::UtcNow.AddDays(1).ToString('o'); deliveryMode = 'cooperative-mailbox' })
        Write-ESCodexCreateOnlyJson $paths.statePath ([ordered]@{ schemaVersion = 1; messageId = $messageId; revision = 2; status = 'completed'; updatedUtc = $old; acceptedByRecordId = 'target'; note = '' })
        $plan = & (Join-Path $scriptsRoot 'Repair-ESCodexSessionMessages.ps1') -StateRoot $stateRoot -RetentionDays 30 -DeleteTerminalMessages
        $plan.destructivePlannedCount | Should Be 1
        Test-Path -LiteralPath $paths.requestPath | Should Be $true
        $applied = & (Join-Path $scriptsRoot 'Repair-ESCodexSessionMessages.ps1') -StateRoot $stateRoot -RetentionDays 30 -DeleteTerminalMessages -Apply
        $applied.appliedCount | Should Be 1
        Test-Path -LiteralPath $paths.requestPath | Should Be $false
        Test-Path -LiteralPath $paths.statePath | Should Be $false
    }

    It 'selects Stop-hook delivery for a busy target with observed hook activation' {
        $stateRoot = Join-Path $TestDrive 'send-message-plan-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $sessionId = '25252525-2525-2525-2525-252525252525'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        $record = AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{ sessionId = $sessionId; responsibilityKey = 'send-plan'; processId = $PID; terminalMode = 'PlainCmd'; lifecycleStatus = 'Registered'; contextAccepted = $true; availability = 'Busy'; availabilityUpdatedUtc = [DateTime]::UtcNow.ToString('o'); availabilityExpiresUtc = [DateTime]::UtcNow.AddMinutes(5).ToString('o') })
        Save-ESCodexSessionRegistry $registryPath $registry
        $projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $skillRoot))
        Write-ESCodexHookActivation $stateRoot $record 'Stop' (Join-Path $projectRoot '.codex\hooks.json') (Join-Path $scriptsRoot 'Receive-ESCodexSessionMessageHook.ps1') | Out-Null
        $sent = & (Join-Path $scriptsRoot 'Send-ESCodexSessionMessage.ps1') -RecordId $record.recordId -Body 'process after busy turn' -StateRoot $stateRoot
        $sent.deliveryPlan | Should Be 'StopHookAtBusyCompletion'
        $sent.externalWakeRequired | Should Be $false
        $sent.queued | Should Be $true
        $sent.directTuiInjectionAttempted | Should Be $false
    }
}

Describe 'ES Codex broker capability boundary' {
    It 'reports Windows daemon and existing TUI limitations without probing or mutation' {
        $status = & (Join-Path $scriptsRoot 'Get-ESCodexSessionBrokerStatus.ps1') -StateRoot (Join-Path $TestDrive 'broker-state')
        $status.registryReadable | Should Be $true
        $status.cooperativeMailboxSupported | Should Be $true
        $status.directExistingTuiInjectionSupported | Should Be $false
        $status.safestDeliveryMode | Should Be 'cooperative-mailbox'
    }
}

Describe 'ES Codex cooperative delivery hooks' {
    . (Join-Path $scriptsRoot 'ESCodexSessionMessageState.ps1')

    It 'claims one queued message at Stop and requests a bounded continuation' {
        $stateRoot = Join-Path $TestDrive 'stop-hook-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $sessionId = '19191919-1919-1919-1919-191919191919'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        $record = AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{ sessionId = $sessionId; responsibilityKey = 'hook-target'; processId = $PID; terminalMode = 'PlainCmd'; lifecycleStatus = 'Registered' })
        Save-ESCodexSessionRegistry $registryPath $registry
        $message = & (Join-Path $scriptsRoot 'Publish-ESCodexSessionMessage.ps1') -RecordId $record.recordId -Body 'run focused acceptance' -Priority high -StateRoot $stateRoot
        $hookInput = @{ hook_event_name = 'Stop'; session_id = $sessionId; turn_id = 'turn-test'; stop_hook_active = $false; last_assistant_message = 'done' } | ConvertTo-Json -Compress
        $output = & (Join-Path $scriptsRoot 'Receive-ESCodexSessionMessageHook.ps1') -InputJson $hookInput -StateRoot $stateRoot | ConvertFrom-Json
        $output.decision | Should Be 'block'
        $output.reason | Should Match $message.messageId
        $output.reason | Should Match 'run focused acceptance'
        (Read-ESCodexMessage $stateRoot $message.messageId).status | Should Be 'accepted'
        $projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $skillRoot))
        $activation = Test-ESCodexHookActivation $stateRoot $record.recordId $sessionId (Join-Path $projectRoot '.codex\hooks.json') (Join-Path $scriptsRoot 'Receive-ESCodexSessionMessageHook.ps1')
        $activation.valid | Should Be $true
        $activation.reason | Should Be 'LoadedAndObserved'
    }

    It 'adds queued messages to the next user prompt as developer context' {
        $stateRoot = Join-Path $TestDrive 'prompt-hook-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $sessionId = '20202020-2020-2020-2020-202020202020'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        $record = AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{ sessionId = $sessionId; responsibilityKey = 'prompt-hook-target'; processId = $PID; terminalMode = 'PlainCmd'; lifecycleStatus = 'Registered' })
        Save-ESCodexSessionRegistry $registryPath $registry
        $message = & (Join-Path $scriptsRoot 'Publish-ESCodexSessionMessage.ps1') -RecordId $record.recordId -Body 'review queued evidence' -StateRoot $stateRoot
        $hookInput = @{ hook_event_name = 'UserPromptSubmit'; session_id = $sessionId; turn_id = 'turn-test'; prompt = 'continue' } | ConvertTo-Json -Compress
        $output = & (Join-Path $scriptsRoot 'Receive-ESCodexSessionMessageHook.ps1') -InputJson $hookInput -StateRoot $stateRoot | ConvertFrom-Json
        $output.hookSpecificOutput.hookEventName | Should Be 'UserPromptSubmit'
        $output.hookSpecificOutput.additionalContext | Should Match $message.messageId
        (Read-ESCodexMessage $stateRoot $message.messageId).status | Should Be 'accepted'
    }

    It 'does not recursively continue an already active Stop hook' {
        $stateRoot = Join-Path $TestDrive 'recursive-hook-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $sessionId = '21212121-2121-2121-2121-212121212121'
        $registry = [pscustomobject]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        $record = AddOrUpdate-ESCodexSessionRecord $registry ([pscustomobject]@{ sessionId = $sessionId; responsibilityKey = 'recursive-hook'; processId = $PID; terminalMode = 'PlainCmd'; lifecycleStatus = 'Registered' })
        Save-ESCodexSessionRegistry $registryPath $registry
        $message = & (Join-Path $scriptsRoot 'Publish-ESCodexSessionMessage.ps1') -RecordId $record.recordId -Body 'leave queued' -StateRoot $stateRoot
        $hookInput = @{ hook_event_name = 'Stop'; session_id = $sessionId; turn_id = 'turn-test'; stop_hook_active = $true } | ConvertTo-Json -Compress
        $output = @(& (Join-Path $scriptsRoot 'Receive-ESCodexSessionMessageHook.ps1') -InputJson $hookInput -StateRoot $stateRoot)
        $output.Count | Should Be 0
        (Read-ESCodexMessage $stateRoot $message.messageId).status | Should Be 'queued'
    }

    It 'invalidates hook activation evidence after definition drift' {
        $stateRoot = Join-Path $TestDrive 'hook-drift-state'
        $configPath = Join-Path $TestDrive 'hooks.json'
        $scriptPath = Join-Path $TestDrive 'hook.ps1'
        [IO.File]::WriteAllText($configPath, '{"hooks":{}}', [Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText($scriptPath, 'exit 0', [Text.UTF8Encoding]::new($false))
        $record = [pscustomobject]@{ recordId = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'; sessionId = '22222222-2222-2222-2222-222222222222'; responsibilityKey = 'drift-test' }
        Write-ESCodexHookActivation $stateRoot $record 'Stop' $configPath $scriptPath | Out-Null
        (Test-ESCodexHookActivation $stateRoot $record.recordId $record.sessionId $configPath $scriptPath).valid | Should Be $true
        [IO.File]::WriteAllText($scriptPath, 'exit 1', [Text.UTF8Encoding]::new($false))
        $drifted = Test-ESCodexHookActivation $stateRoot $record.recordId $record.sessionId $configPath $scriptPath
        $drifted.valid | Should Be $false
        $drifted.reason | Should Be 'HookScriptDrift'
    }
}
