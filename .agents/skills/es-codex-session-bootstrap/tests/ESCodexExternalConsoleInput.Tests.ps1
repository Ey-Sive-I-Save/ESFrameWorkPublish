$skillRoot = Split-Path -Parent $PSScriptRoot
$scriptsRoot = Join-Path $skillRoot 'scripts'
$launcherScript = Join-Path $scriptsRoot 'Start-ESCodexSession.ps1'
$inputScript = Join-Path $scriptsRoot 'Invoke-ESCodexExternalConsoleInput.ps1'

function Start-ExternalConsoleInputTestCmd([string[]]$Arguments) {
    $process = Start-Process -FilePath (Join-Path $env:WINDIR 'System32\cmd.exe') `
        -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    Start-Sleep -Milliseconds 400
    return $process
}

function Stop-ExternalConsoleInputTestCmd([System.Diagnostics.Process]$Process, [string]$StartedAtUtc) {
    if ($null -eq $Process) { return }
    try {
        $live = Get-Process -Id $Process.Id -ErrorAction Stop
        $expectedTicks = [DateTime]::Parse($StartedAtUtc).ToUniversalTime().Ticks
        if ($live.ProcessName -eq 'cmd' -and $live.StartTime.ToUniversalTime().Ticks -eq $expectedTicks) {
            & (Join-Path $env:WINDIR 'System32\taskkill.exe') /PID $live.Id /T /F | Out-Null
        }
    }
    catch {
        # The test process may have already ended. Never target a reused PID.
    }
}

function New-ExternalConsoleInputClaim([string]$StateRoot, [System.Diagnostics.Process]$Cmd) {
    $startedAtUtc = $Cmd.StartTime.ToUniversalTime().ToString('o')
    return & $launcherScript `
        -Mode PrepareExternalClaim `
        -ExternalClaimBindingId ([Guid]::NewGuid().ToString()) `
        -ExternalClaimExpectedCmdProcessId $Cmd.Id `
        -ExternalClaimExpectedCmdProcessStartedAtUtc $startedAtUtc `
        -ExternalClaimId ([Guid]::NewGuid().ToString()) `
        -TaskKey ('external-console-input-test:' + [Guid]::NewGuid().ToString('N')) `
        -ResponsibilityKey 'external-console-input-test' `
        -TabTitle 'External Console Input Test' `
        -ExternalClaimStateRoot $StateRoot
}

Describe 'ES Codex external console input' {
    It 'submits one verified claim, observes its response, and rejects a duplicate submission' {
        $cmd = Start-ExternalConsoleInputTestCmd @('/k', 'prompt ES_INPUT_TEST$G')
        $startedAtUtc = $cmd.StartTime.ToUniversalTime().ToString('o')
        try {
            $prepared = New-ExternalConsoleInputClaim $TestDrive $cmd
            $submission = & $launcherScript `
                -Mode SubmitExternalClaimInput `
                -ExternalClaimId $prepared.claimId `
                -ExternalClaimExpectedCmdProcessId $cmd.Id `
                -ExternalClaimExpectedCmdProcessStartedAtUtc $startedAtUtc `
                -ExternalClaimStateRoot $TestDrive
            $finalized = & $launcherScript `
                -Mode FinalizeExternalClaim `
                -ExternalClaimId $prepared.claimId `
                -ExternalClaimStateRoot $TestDrive

            [bool]$submission.success | Should Be $true
            [bool]$submission.responseObserved | Should Be $true
            [string]$finalized.claimState | Should Be 'ClaimedExternal'
            [string]$finalized.requestedSessionId | Should Be ''
            { & $inputScript `
                    -ClaimId $prepared.claimId `
                    -ExpectedCmdProcessId $cmd.Id `
                    -ExpectedCmdProcessStartedAtUtc $startedAtUtc `
                    -StateRoot $TestDrive } | Should Throw
        }
        finally {
            Stop-ExternalConsoleInputTestCmd $cmd $startedAtUtc
        }
    }

    It 'refuses a CMD with an active child process and writes no response' {
        $cmd = Start-ExternalConsoleInputTestCmd @('/d', '/k', 'timeout /t 30 /nobreak >nul')
        $startedAtUtc = $cmd.StartTime.ToUniversalTime().ToString('o')
        try {
            $prepared = New-ExternalConsoleInputClaim $TestDrive $cmd
            { & $launcherScript `
                    -Mode SubmitExternalClaimInput `
                    -ExternalClaimId $prepared.claimId `
                    -ExternalClaimExpectedCmdProcessId $cmd.Id `
                    -ExternalClaimExpectedCmdProcessStartedAtUtc $startedAtUtc `
                    -ExternalClaimStateRoot $TestDrive } | Should Throw
            (Test-Path -LiteralPath (Join-Path $prepared.claimDirectory 'response.json') -PathType Leaf) | Should Be $false
        }
        finally {
            Stop-ExternalConsoleInputTestCmd $cmd $startedAtUtc
        }
    }
}
