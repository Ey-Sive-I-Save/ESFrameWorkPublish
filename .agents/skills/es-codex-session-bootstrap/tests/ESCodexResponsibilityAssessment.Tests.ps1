$ErrorActionPreference = 'Stop'

Describe 'ES Codex responsibility assessment' {
    $projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
    $assessment = Join-Path $projectRoot '.agents\skills\es-codex-session-bootstrap\scripts\Get-ESCodexResponsibilityAssessment.ps1'
    $archive = (Get-ChildItem -LiteralPath (Join-Path $projectRoot 'ES') -Recurse -File -Filter '2026-08-23_AIBrain*.md' | Select-Object -First 1).FullName
    $orchestrator = (Get-ChildItem -LiteralPath (Join-Path $projectRoot 'ES') -Recurse -File -Filter 'Complete-ESCodexHandoff.ps1' | Select-Object -First 1).FullName
    $script:ESCodexResponsibilityFixtureRoot = Join-Path $projectRoot ('Temp\ESCodexResponsibilityAssessment-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $script:ESCodexResponsibilityFixtureRoot -Force | Out-Null
    $archiveIdPrefix = [regex]::Unescape('\u7a97\u53e3\u6863\u6848ID\uFF1A')
    $userRequestPrefix = [regex]::Unescape('- **\u7528\u6237\u8981\u6c42\uFF08\u539f\u6587\u8282\u9009\uFF09**\uFF1A')
    $coverageHeading = [regex]::Unescape('## \u8986\u76d6\u5ba1\u8ba1')
    function Expand-ResponsibilityFixture([string]$Value) {
        return $Value.Replace('__ARCHIVE_ID_PREFIX__', $archiveIdPrefix).
            Replace('__USER_REQUEST_PREFIX__', $userRequestPrefix).
            Replace('__COVERAGE_HEADING__', $coverageHeading)
    }
    $editorFixtureArchive = Join-Path $script:ESCodexResponsibilityFixtureRoot 'editor-history.md'
    $editorFixtureSession = Join-Path $script:ESCodexResponsibilityFixtureRoot 'editor-session.jsonl'
    $editorHistory = @'
# Editor responsibility fixture

__ARCHIVE_ID_PREFIX__`ES-CODEX-TEST-EDITOR`

Codex Session ID: `00000000-0000-7000-8000-000000000001`

### Stage S001: editor work

### T001: Editor foundation

__USER_REQUEST_PREFIX__Audit EditorWindow, Inspector, SerializedProperty, Undo, and Dirty behavior.
- **Task**: Audit.
- **Turn**: fixture-1.
- **Evidence**: Static fixture.
- **Remaining**: None.

### T002: Editor lifecycle

__USER_REQUEST_PREFIX__Test Workbench, Reload, and PlayMode lifecycle behavior.
- **Task**: Test.
- **Turn**: fixture-2.
- **Evidence**: Static fixture.
- **Remaining**: None.

__COVERAGE_HEADING__
'@
    [IO.File]::WriteAllText($editorFixtureArchive, (Expand-ResponsibilityFixture $editorHistory), [Text.UTF8Encoding]::new($false))
    $sessionRows = @(
        [ordered]@{ timestamp = '2026-08-23T00:00:00Z'; type = 'session_meta'; payload = [ordered]@{ id = '00000000-0000-7000-8000-000000000001'; cwd = $projectRoot } },
        [ordered]@{ timestamp = '2026-08-23T00:00:01Z'; type = 'event_msg'; payload = [ordered]@{ type = 'user_message'; message = 'Audit EditorWindow behavior.' } },
        [ordered]@{ timestamp = '2026-08-23T00:00:02Z'; type = 'event_msg'; payload = [ordered]@{ type = 'user_message'; message = 'Test Workbench lifecycle behavior.' } }
    )
    $sessionJsonl = (($sessionRows | ForEach-Object { $_ | ConvertTo-Json -Depth 6 -Compress }) -join "`n") + "`n"
    [IO.File]::WriteAllText($editorFixtureSession, $sessionJsonl, [Text.UTF8Encoding]::new($false))

    AfterAll {
        $root = $script:ESCodexResponsibilityFixtureRoot
        if (-not [string]::IsNullOrWhiteSpace($root) -and (Test-Path -LiteralPath $root -PathType Container)) {
            Get-ChildItem -LiteralPath $root -File | Remove-Item -Force
            Remove-Item -LiteralPath $root -Force
        }
        Remove-Variable -Name ESCodexResponsibilityFixtureRoot -Scope Script -ErrorAction SilentlyContinue
    }

    It 'uses all formal timeline nodes and recommends the dominant editor responsibility' {
        $result = & $assessment -ArchivePath $archive | ConvertFrom-Json
        ([int]$result.nodeCount -ge 42) | Should Be $true
        $result.status | Should Be 'assessed'
        $result.recommendedResponsibilityKey | Should Be 'es-editor-foundation-governance'
        ([double]$result.confidence -ge 0.45) | Should Be $true
    }

    It 'recognizes UI knowledge governance in mixed content and handoff history' {
        $mixedArchive = Join-Path $script:ESCodexResponsibilityFixtureRoot 'mixed-ui-knowledge-history.md'
        $mixedHistory = @'
### T001: knowledge inventory
__USER_REQUEST_PREFIX__Inventory the registered KnowledgeIndex entries and evidence levels.

### T002: categorized knowledge
__USER_REQUEST_PREFIX__Review Editor, Engineering, and UI entries including ScreenSpec and Materializer coverage.

### T003: required UI layers
__USER_REQUEST_PREFIX__Strengthen commercial game UI knowledge, visual design knowledge, responsive knowledge, and interaction state knowledge.

### T004: continuation
__USER_REQUEST_PREFIX__Continue strengthening.

### T005: auxiliary window
__USER_REQUEST_PREFIX__Open another window to review Skills capability.

### T006: correction
__USER_REQUEST_PREFIX__I asked you to open a new window.

### T007: ES support
__USER_REQUEST_PREFIX__Does ES support this?

### T008: handoff
__USER_REQUEST_PREFIX__Hand off to a new window and start the history.

__COVERAGE_HEADING__
'@
        [IO.File]::WriteAllText($mixedArchive, (Expand-ResponsibilityFixture $mixedHistory), [Text.UTF8Encoding]::new($false))

        $result = & $assessment -ArchivePath $mixedArchive -ResponsibilityKey 'es-ui-knowledge-governance' | ConvertFrom-Json
        $result.status | Should Be 'assessed'
        $result.recommendedResponsibilityKey | Should Be 'es-ui-knowledge-governance'
        $result.requestedMatchesRecommendation | Should Be $true
        ([int]$result.scores.'es-ui-knowledge-governance') | Should Be 2
        ([int]$result.assignedNodeCount) | Should Be 2
        ([double]$result.confidence -ge 0.45) | Should Be $true
    }

    It 'does not treat an ordinary Codex handoff as UI knowledge governance' {
        $sessionArchive = Join-Path $script:ESCodexResponsibilityFixtureRoot 'session-handoff-history.md'
        $sessionHistory = @'
### T001: Codex session handoff
__USER_REQUEST_PREFIX__Open a new Codex window, validate the launch envelope, and wait for the handoff receipt.

### T002: Session responsibility
__USER_REQUEST_PREFIX__Bind the receiving session responsibility and report context acceptance.

__COVERAGE_HEADING__
'@
        [IO.File]::WriteAllText($sessionArchive, (Expand-ResponsibilityFixture $sessionHistory), [Text.UTF8Encoding]::new($false))

        $result = & $assessment -ArchivePath $sessionArchive | ConvertFrom-Json
        $result.status | Should Be 'insufficient-history'
        $result.recommendedResponsibilityKey | Should Be ''
    }

    It 'does not treat bare Unity UI implementation terms as knowledge governance' {
        $runtimeUiArchive = Join-Path $script:ESCodexResponsibilityFixtureRoot 'runtime-ui-history.md'
        $runtimeUiHistory = @'
### T001: Runtime layout implementation
__USER_REQUEST_PREFIX__Implement Canvas, RectTransform, LayoutGroup, Safe Area, Canvas Scaler, TextMeshPro, and SpriteAtlas behavior.

__COVERAGE_HEADING__
'@
        [IO.File]::WriteAllText($runtimeUiArchive, (Expand-ResponsibilityFixture $runtimeUiHistory), [Text.UTF8Encoding]::new($false))

        $result = & $assessment -ArchivePath $runtimeUiArchive | ConvertFrom-Json
        $result.recommendedResponsibilityKey | Should Not Be 'es-ui-knowledge-governance'
    }

    It 'does not treat generic resource or engineering knowledge as UI knowledge' {
        $genericArchive = Join-Path $script:ESCodexResponsibilityFixtureRoot 'generic-knowledge-history.md'
        $genericHistory = @'
### T001: resource knowledge
__USER_REQUEST_PREFIX__Strengthen resource knowledge and license provenance.

### T002: engineering knowledge
__USER_REQUEST_PREFIX__Strengthen ES engineering knowledge and release evidence.

__COVERAGE_HEADING__
'@
        [IO.File]::WriteAllText($genericArchive, (Expand-ResponsibilityFixture $genericHistory), [Text.UTF8Encoding]::new($false))

        $result = & $assessment -ArchivePath $genericArchive | ConvertFrom-Json
        $result.status | Should Be 'insufficient-history'
        ([int]$result.scores.'es-ui-knowledge-governance') | Should Be 0
    }

    It 'caps repeated terms at one vote per node' {
        $repeatedArchive = Join-Path $script:ESCodexResponsibilityFixtureRoot 'repeated-ui-history.md'
        $repeatedHistory = @'
### T001: repeated UI knowledge
__USER_REQUEST_PREFIX__UI knowledge UI knowledge UI knowledge ScreenSpec ScreenSpec Materializer Materializer.

__COVERAGE_HEADING__
'@
        [IO.File]::WriteAllText($repeatedArchive, (Expand-ResponsibilityFixture $repeatedHistory), [Text.UTF8Encoding]::new($false))

        $result = & $assessment -ArchivePath $repeatedArchive | ConvertFrom-Json
        $result.status | Should Be 'ambiguous-history'
        ([int]$result.scores.'es-ui-knowledge-governance') | Should Be 1
        ([int]$result.assignedNodeCount) | Should Be 1
    }

    It 'recognizes actual session bootstrap maintenance' {
        $maintenanceArchive = Join-Path $script:ESCodexResponsibilityFixtureRoot 'session-maintenance-history.md'
        $maintenanceHistory = @'
### T001: bootstrap repair
__USER_REQUEST_PREFIX__Fix session bootstrap responsibility assessment and launch envelope handling.

### T002: registry audit
__USER_REQUEST_PREFIX__Audit the Codex session registry and repair handoff receipt validation.

__COVERAGE_HEADING__
'@
        [IO.File]::WriteAllText($maintenanceArchive, (Expand-ResponsibilityFixture $maintenanceHistory), [Text.UTF8Encoding]::new($false))

        $result = & $assessment -ArchivePath $maintenanceArchive | ConvertFrom-Json
        $result.status | Should Be 'assessed'
        $result.recommendedResponsibilityKey | Should Be 'es-session-bootstrap-maintenance'
    }

    It 'blocks equal dominant node votes' {
        $tieArchive = Join-Path $script:ESCodexResponsibilityFixtureRoot 'tie-history.md'
        $tieHistory = @'
### T001: UI contracts
__USER_REQUEST_PREFIX__Strengthen ScreenSpec and Materializer knowledge.

### T002: UI knowledge
__USER_REQUEST_PREFIX__Audit registered UI knowledge coverage.

### T003: editor foundation
__USER_REQUEST_PREFIX__Audit EditorWindow and Inspector behavior.

### T004: editor lifecycle
__USER_REQUEST_PREFIX__Test Workbench and Reload behavior.

__COVERAGE_HEADING__
'@
        [IO.File]::WriteAllText($tieArchive, (Expand-ResponsibilityFixture $tieHistory), [Text.UTF8Encoding]::new($false))

        $result = & $assessment -ArchivePath $tieArchive | ConvertFrom-Json
        $result.status | Should Be 'ambiguous-history'
        ([int]$result.scores.'es-ui-knowledge-governance') | Should Be 2
        ([int]$result.scores.'es-editor-foundation-governance') | Should Be 2
    }

    It 'blocks a unique winner below the confidence threshold' {
        $lowConfidenceArchive = Join-Path $script:ESCodexResponsibilityFixtureRoot 'low-confidence-history.md'
        $lowConfidenceHistory = @'
### T001: UI contracts
__USER_REQUEST_PREFIX__Strengthen ScreenSpec and Materializer knowledge.

### T002: UI knowledge
__USER_REQUEST_PREFIX__Audit registered UI knowledge coverage.

### T003: editor foundation
__USER_REQUEST_PREFIX__Audit EditorWindow and Inspector behavior.

### T004: AIBrain architecture
__USER_REQUEST_PREFIX__Design AIBrain architecture with AICommand and TaskContract routing.

### T005: session maintenance
__USER_REQUEST_PREFIX__Fix session bootstrap responsibility assessment.

__COVERAGE_HEADING__
'@
        [IO.File]::WriteAllText($lowConfidenceArchive, (Expand-ResponsibilityFixture $lowConfidenceHistory), [Text.UTF8Encoding]::new($false))

        $result = & $assessment -ArchivePath $lowConfidenceArchive | ConvertFrom-Json
        $result.status | Should Be 'ambiguous-history'
        ([double]$result.confidence) | Should Be 0.4
    }

    It 'rejects a recent-topic responsibility that contradicts the full history' {
        $message = ''
        try {
            & $orchestrator -SessionPath $editorFixtureSession -ArchivePath $editorFixtureArchive -ProjectPath $projectRoot -ResponsibilityKey 'es-aibrain-architecture' -DryRun
        }
        catch { $message = $_.Exception.Message }
        $message | Should Match 'ResponsibilityKey.*\u4e0d\u5339\u914d'
    }

    It 'accepts the assessed responsibility in dry-run without opening a window' {
        $result = & $orchestrator -SessionPath $editorFixtureSession -ArchivePath $editorFixtureArchive -ProjectPath $projectRoot -ResponsibilityKey 'es-editor-foundation-governance' -DryRun
        $result.status | Should Be 'Prepared'
        $result.responsibilityAssessment.recommendedResponsibilityKey | Should Be 'es-editor-foundation-governance'
    }
}
