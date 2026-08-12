$skillRoot = Split-Path -Parent $PSScriptRoot
$scriptsRoot = Join-Path $skillRoot 'scripts'
$claimScript = Join-Path $scriptsRoot 'Invoke-ESCodexExternalClaim.ps1'
. (Join-Path $scriptsRoot 'ESCodexSessionState.ps1')

function New-ExternalClaimTestPrepare([string]$StateRoot, [string]$ClaimId = '') {
    $arguments = @{
        Action = 'Prepare'
        SessionId = [Guid]::NewGuid().ToString()
        StateRoot = $StateRoot
        TaskKey = 'external-claim-test'
        ResponsibilityKey = 'external-cmd-test'
        TabTitle = 'External Claim Test'
    }
    if (-not [string]::IsNullOrWhiteSpace($ClaimId)) { $arguments.ClaimId = $ClaimId }
    return & $claimScript @arguments
}

function Wait-ExternalClaimResponse([string]$Path) {
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        if (Test-Path -LiteralPath $Path -PathType Leaf) { return }
        Start-Sleep -Milliseconds 100
    }
    throw "Timed out waiting for external claim response: $Path"
}

function Get-ExternalClaimRequestCount([string]$StateRoot, [string]$SessionId) {
    $claimsRoot = Join-Path $StateRoot 'external-claims'
    if (-not (Test-Path -LiteralPath $claimsRoot -PathType Container)) { return 0 }
    return @(
        Get-ChildItem -LiteralPath $claimsRoot -Directory | Where-Object {
            $requestPath = Join-Path $_.FullName 'request.json'
            if (-not (Test-Path -LiteralPath $requestPath -PathType Leaf)) { return $false }
            try {
                $request = Get-Content -LiteralPath $requestPath -Raw -Encoding utf8 | ConvertFrom-Json
                return [string]$request.requestedSessionId -eq $SessionId
            }
            catch { return $false }
        }
    ).Count
}

Describe 'ES Codex external CMD claim protocol' {
    It 'reuses one exact prepared claim instead of creating a competing request after reload' {
        $claimId = [Guid]::NewGuid().ToString()
        $sessionId = [Guid]::NewGuid().ToString()
        $first = & $claimScript -Action Prepare -ClaimId $claimId -SessionId $sessionId -StateRoot $TestDrive
        $second = & $claimScript -Action Prepare -ClaimId $claimId -SessionId $sessionId -StateRoot $TestDrive

        $first.claimId | Should Be $second.claimId
        $first.command | Should Be $second.command
        @((Get-ChildItem -LiteralPath (Join-Path $TestDrive 'external-claims') -Directory)).Count | Should Be 1
    }

    It 'rejects a prepared claim when a different SessionId tries to reuse its ClaimId' {
        $claimId = [Guid]::NewGuid().ToString()
        & $claimScript -Action Prepare -ClaimId $claimId -SessionId ([Guid]::NewGuid().ToString()) -StateRoot $TestDrive | Out-Null

        { & $claimScript -Action Prepare -ClaimId $claimId -SessionId ([Guid]::NewGuid().ToString()) -StateRoot $TestDrive } | Should Throw
    }

    It 'rejects a second ClaimId for one unexpired SessionId' {
        $sessionId = [Guid]::NewGuid().ToString()
        & $claimScript -Action Prepare -ClaimId ([Guid]::NewGuid().ToString()) -SessionId $sessionId -StateRoot $TestDrive | Out-Null

        { & $claimScript -Action Prepare -ClaimId ([Guid]::NewGuid().ToString()) -SessionId $sessionId -StateRoot $TestDrive } | Should Throw
        Get-ExternalClaimRequestCount $TestDrive $sessionId | Should Be 1
    }

    It 'allows a fresh ClaimId after an expired request for the same SessionId' {
        $sessionId = [Guid]::NewGuid().ToString()
        $expiredClaimId = [Guid]::NewGuid().ToString()
        $expiredDirectory = Join-Path (Join-Path $TestDrive 'external-claims') $expiredClaimId
        [void][IO.Directory]::CreateDirectory($expiredDirectory)
        $expiredRequest = [ordered]@{
            schemaVersion = 1
            claimId = $expiredClaimId
            claimToken = ('a' * 64)
            projectRoot = 'F:\aaProject\ESFrameWorkPublish'
            requestedSessionId = $sessionId
            createdUtc = [DateTime]::UtcNow.AddMinutes(-10).ToString('o')
            expiresAtUtc = [DateTime]::UtcNow.AddMinutes(-5).ToString('o')
        }
        [IO.File]::WriteAllText((Join-Path $expiredDirectory 'request.json'),
            ($expiredRequest | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))

        $cancelled = & $claimScript -Action Cancel -ClaimId $expiredClaimId -StateRoot $TestDrive
        $fresh = & $claimScript -Action Prepare -ClaimId ([Guid]::NewGuid().ToString()) -SessionId $sessionId -StateRoot $TestDrive

        $cancelled.claimState | Should Be 'Cancelled'
        $fresh.requestedSessionId | Should Be $sessionId
        $fresh.claimId | Should Not Be $expiredClaimId
    }

    It 'allows concurrent Prepare calls for one SessionId to create at most one live request' {
        $sessionId = [Guid]::NewGuid().ToString()
        $firstJob = Start-Job -ScriptBlock {
                param($path, $id, $stateRoot)
                try {
                    $result = & $path -Action Prepare -ClaimId ([Guid]::NewGuid().ToString()) -SessionId $id -StateRoot $stateRoot
                    [pscustomobject]@{ succeeded = $true; claimId = [string]$result.claimId; error = '' }
                }
                catch {
                    [pscustomobject]@{ succeeded = $false; claimId = ''; error = $_.Exception.Message }
                }
            } -ArgumentList $claimScript, $sessionId, $TestDrive
        $secondJob = Start-Job -ScriptBlock {
                param($path, $id, $stateRoot)
                try {
                    $result = & $path -Action Prepare -ClaimId ([Guid]::NewGuid().ToString()) -SessionId $id -StateRoot $stateRoot
                    [pscustomobject]@{ succeeded = $true; claimId = [string]$result.claimId; error = '' }
                }
                catch {
                    [pscustomobject]@{ succeeded = $false; claimId = ''; error = $_.Exception.Message }
                }
            } -ArgumentList $claimScript, $sessionId, $TestDrive
        $jobs = @($firstJob, $secondJob)
        $jobs | Wait-Job -Timeout 15 | Out-Null
        @($jobs | Where-Object State -ne 'Completed').Count | Should Be 0
        $results = @($jobs | Receive-Job -ErrorAction Stop)
        @($results | Where-Object succeeded).Count | Should Be 1
        @($results | Where-Object { -not $_.succeeded -and $_.error -match 'non-expired external claim' }).Count | Should Be 1
        Get-ExternalClaimRequestCount $TestDrive $sessionId | Should Be 1
    }

    It 'cancels an unfinalized claim atomically and blocks later finalization' {
        $prepared = New-ExternalClaimTestPrepare $TestDrive
        $cancelled = & $claimScript -Action Cancel -ClaimId $prepared.claimId -StateRoot $TestDrive

        $cancelled.claimState | Should Be 'Cancelled'
        (Test-Path -LiteralPath (Join-Path $prepared.claimDirectory 'cancel-receipt.json') -PathType Leaf) | Should Be $true
        { & $claimScript -Action Finalize -ClaimId $prepared.claimId -StateRoot $TestDrive } | Should Throw
    }

    It 'keeps cancellation idempotent before finalization' {
        $prepared = New-ExternalClaimTestPrepare $TestDrive
        $first = & $claimScript -Action Cancel -ClaimId $prepared.claimId -StateRoot $TestDrive
        $second = & $claimScript -Action Cancel -ClaimId $prepared.claimId -StateRoot $TestDrive

        $second.claimId | Should Be $first.claimId
        { & $claimScript -Action Cancel -ClaimId $prepared.claimId -StateRoot $TestDrive } | Should Not Throw
    }

    It 'allows only Cancel or Finalize to commit when they race after target CMD proof' {
        $prepared = New-ExternalClaimTestPrepare $TestDrive
        $shellCommand = $prepared.command + ' & timeout /t 8 /nobreak >nul'
        $cmd = Start-Process -FilePath 'cmd.exe' -ArgumentList @('/d', '/c', $shellCommand) -PassThru
        try {
            Wait-ExternalClaimResponse (Join-Path $prepared.claimDirectory 'response.json')
            $cancelJob = Start-Job -ScriptBlock {
                param($path, $id, $stateRoot)
                try { & $path -Action Cancel -ClaimId $id -StateRoot $stateRoot | Out-Null; 'cancelled' }
                catch { 'cancel-rejected' }
            } -ArgumentList $claimScript, $prepared.claimId, $TestDrive
            $finalizeJob = Start-Job -ScriptBlock {
                param($path, $id, $stateRoot)
                try { & $path -Action Finalize -ClaimId $id -StateRoot $stateRoot | Out-Null; 'finalized' }
                catch { 'finalize-rejected' }
            } -ArgumentList $claimScript, $prepared.claimId, $TestDrive
            $jobs = @($cancelJob, $finalizeJob)
            $jobs | Wait-Job -Timeout 15 | Out-Null
            @($jobs | Where-Object State -ne 'Completed').Count | Should Be 0
            $states = @($jobs | Receive-Job -ErrorAction Stop)
            @($states | Where-Object { $_ -eq 'cancelled' -or $_ -eq 'finalized' }).Count | Should Be 1
            @($states | Where-Object { $_ -eq 'cancel-rejected' -or $_ -eq 'finalize-rejected' }).Count | Should Be 1
            $cancelReceipt = Test-Path -LiteralPath (Join-Path $prepared.claimDirectory 'cancel-receipt.json') -PathType Leaf
            $finalizeReceipt = Test-Path -LiteralPath (Join-Path $prepared.claimDirectory 'finalize-receipt.json') -PathType Leaf
            ($cancelReceipt -xor $finalizeReceipt) | Should Be $true
        }
        finally {
            if ($null -ne $cmd) { $cmd.WaitForExit() }
        }
    }

    It 'rejects Finalize before the target CMD writes its create-only response' {
        $prepared = New-ExternalClaimTestPrepare $TestDrive

        { & $claimScript -Action Finalize -ClaimId $prepared.claimId -StateRoot $TestDrive } | Should Throw
    }

    It 'allows concurrent finalizers to converge on one Registry record after target CMD proof' {
        $prepared = New-ExternalClaimTestPrepare $TestDrive
        $shellCommand = $prepared.command + ' & timeout /t 8 /nobreak >nul'
        $cmd = Start-Process -FilePath 'cmd.exe' -ArgumentList @('/d', '/c', $shellCommand) -PassThru
        try {
            Wait-ExternalClaimResponse (Join-Path $prepared.claimDirectory 'response.json')
            $firstJob = Start-Job -ScriptBlock {
                param($path, $id, $stateRoot)
                & $path -Action Finalize -ClaimId $id -StateRoot $stateRoot
            } -ArgumentList $claimScript, $prepared.claimId, $TestDrive
            $secondJob = Start-Job -ScriptBlock {
                param($path, $id, $stateRoot)
                & $path -Action Finalize -ClaimId $id -StateRoot $stateRoot
            } -ArgumentList $claimScript, $prepared.claimId, $TestDrive
            $jobs = @($firstJob, $secondJob)
            $jobs | Wait-Job -Timeout 15 | Out-Null
            @($jobs | Where-Object State -ne 'Completed').Count | Should Be 0
            $results = @($jobs | Receive-Job -ErrorAction Stop)
            @($results).Count | Should Be 2
            @($results | Select-Object -ExpandProperty recordId -Unique).Count | Should Be 1
            $registry = Read-ESCodexSessionRegistry (Join-Path $TestDrive 'sessions.json')
            @($registry.sessions | Where-Object externalClaimId -eq $prepared.claimId).Count | Should Be 1
            [string]$registry.sessions[0].lifecycleStatus | Should Be 'ClaimedExternal'
        }
        finally {
            if ($null -ne $cmd) { $cmd.WaitForExit() }
        }
    }
}
