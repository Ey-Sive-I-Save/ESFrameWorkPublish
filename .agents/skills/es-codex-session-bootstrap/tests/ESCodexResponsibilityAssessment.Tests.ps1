$ErrorActionPreference = 'Stop'

Describe 'ES Codex responsibility assessment' {
    $projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
    $assessment = Join-Path $projectRoot '.agents\skills\es-codex-session-bootstrap\scripts\Get-ESCodexResponsibilityAssessment.ps1'
    $archive = (Get-ChildItem -LiteralPath (Join-Path $projectRoot 'ES') -Recurse -File -Filter '2026-08-23_AIBrain*.md' | Select-Object -First 1).FullName
    $session = 'C:\Users\asus\.codex\sessions\2026\08\21\rollout-2026-08-21T01-17-57-01a0202d-f1f9-7862-81b9-399610f6b1ed.jsonl'
    $orchestrator = (Get-ChildItem -LiteralPath (Join-Path $projectRoot 'ES') -Recurse -File -Filter 'Complete-ESCodexHandoff.ps1' | Select-Object -First 1).FullName

    It 'uses all formal timeline nodes and recommends the dominant editor responsibility' {
        $result = & $assessment -ArchivePath $archive | ConvertFrom-Json
        ([int]$result.nodeCount -ge 42) | Should Be $true
        $result.status | Should Be 'assessed'
        $result.recommendedResponsibilityKey | Should Be 'es-editor-foundation-governance'
        ([double]$result.confidence -ge 0.45) | Should Be $true
    }

    It 'rejects a recent-topic responsibility that contradicts the full history' {
        { & $orchestrator -SessionPath $session -ArchivePath $archive -ProjectPath $projectRoot -ResponsibilityKey 'es-aibrain-architecture' -DryRun } | Should Throw
    }

    It 'accepts the assessed responsibility in dry-run without opening a window' {
        $result = & $orchestrator -SessionPath $session -ArchivePath $archive -ProjectPath $projectRoot -ResponsibilityKey 'es-editor-foundation-governance' -DryRun
        $result.status | Should Be 'Prepared'
        $result.responsibilityAssessment.recommendedResponsibilityKey | Should Be 'es-editor-foundation-governance'
    }
}
