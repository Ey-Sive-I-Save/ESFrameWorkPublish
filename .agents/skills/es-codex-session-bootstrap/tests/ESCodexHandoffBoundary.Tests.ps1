$ErrorActionPreference = 'Stop'

Describe 'ES Codex handoff boundary' {
    $projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
    $launcher = Join-Path $projectRoot '.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1'

    It 'rejects direct New with a handoff path even when prompt has no handoff wording' {
        { & $launcher -Mode New -ProjectPath $projectRoot -TaskPrompt 'continue' -HandoffPath 'ES\AI协作历程（Codex）\README.md' -DryRun } | Should Throw
    }

    It 'rejects a manually supplied HandoffMode without orchestrator capability' {
        { & $launcher -Mode Validate -ProjectPath $projectRoot -ResponsibilityKey 'content-audit' -HandoffMode -DryRun } | Should Throw
    }

    It 'keeps ordinary New validation available' {
        $result = & $launcher -Mode Validate -ProjectPath $projectRoot -TaskPrompt 'ordinary new session' -ResponsibilityKey 'resource-pipeline' -DryRun
        $result.requiredPathsValid | Should Be $true
        $result.launchPhase | Should Be 'Prepared'
    }

    It 'accepts the capability only for the matching orchestrator call' {
        $token = [Guid]::NewGuid().ToString('N')
        $previous = [string]$env:ES_CODEX_HANDOFF_AUTHORIZATION
        $env:ES_CODEX_HANDOFF_AUTHORIZATION = $token
        try {
            $result = & $launcher -Mode Validate -ProjectPath $projectRoot -ResponsibilityKey 'content-audit' -HandoffMode -HandoffAuthorization $token -DryRun
            $result.requiredPathsValid | Should Be $true
        }
        finally {
            if ([string]::IsNullOrWhiteSpace($previous)) { Remove-Item Env:ES_CODEX_HANDOFF_AUTHORIZATION -ErrorAction SilentlyContinue }
            else { $env:ES_CODEX_HANDOFF_AUTHORIZATION = $previous }
        }
    }
}
