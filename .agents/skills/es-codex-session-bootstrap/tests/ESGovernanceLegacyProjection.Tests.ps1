$skillRoot = Split-Path -Parent $PSScriptRoot
$validator = Join-Path $skillRoot 'scripts/Test-ESGovernanceLegacyProjection.ps1'
$projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $skillRoot))
$map = '.agents/skills/es-codex-session-bootstrap/references/governance-legacy-state-map.json'
function Write-LegacyFixture($Entries, [string]$Name) {
    $path = Join-Path $TestDrive $Name
    $payload = [ordered]@{ schemaVersion = 1; projectionId = 'fixture'; entries = @($Entries) }
    [IO.File]::WriteAllText($path, ($payload | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false)); return $path
}
function New-LegacyEntry([string]$Legacy = 'runtime-not-run', [string]$Projected = 'runtime-not-run') {
    [pscustomobject][ordered]@{
        legacyState = $Legacy; projectedState = $Projected; source = [pscustomobject]@{ object = 'task-1'; field = 'evidence'; profile = 'StaticDevelopment'; scope = 'task:read-only'; scopeKind = 'task-object' }
        object = 'task-1'; field = 'evidence'; profile = 'StaticDevelopment'; scope = 'task:read-only'; scopeKind = 'task-object'
        routeState = 'core'; evidenceState = 'runtime-not-run'; effect = 'claim-cap'; reasonCode = 'RUNTIME.NOT_RUN'; evidenceRefs = @('snapshot:fixture')
    }
}
Describe 'Executable legacy projection validation' {
    It 'replays a scoped legacy state without widening' {
        (& $validator -ProjectRoot $projectRoot -ProjectionPath (Write-LegacyFixture (New-LegacyEntry) 'valid.json') -LegacyMapPath $map).projectionStatus | Should Be 'Accepted'
    }
    It 'accepts Accepted only with closed evidence and a bounded scope' {
        $e = New-LegacyEntry -Legacy 'Accepted' -Projected 'Accepted'; $e.evidenceState = 'closed'; $e.effect = 'review'; $e.reasonCode = 'EVIDENCE.RECEIPT_MISSING'; $e.scope = 'context:acceptance'; $e.scopeKind = 'context-object'; $e.source.scope = 'context:acceptance'; $e.source.scopeKind = 'context-object'
        (& $validator -ProjectRoot $projectRoot -ProjectionPath (Write-LegacyFixture $e 'accepted.json') -LegacyMapPath $map).entryCount | Should Be 1
    }
    It 'rejects lost object or widened scope' {
        $e = New-LegacyEntry; $e.object = 'project'; { & $validator -ProjectRoot $projectRoot -ProjectionPath (Write-LegacyFixture $e 'lost-object.json') -LegacyMapPath $map } | Should Throw
    }
    It 'rejects forbidden conversion and non-replayable axes' {
        $e = New-LegacyEntry -Legacy 'runtime-not-run' -Projected 'runtime-failed'; $e.evidenceState = 'partial'; $e.effect = 'claim-cap'; { & $validator -ProjectRoot $projectRoot -ProjectionPath (Write-LegacyFixture $e 'forbidden.json') -LegacyMapPath $map } | Should Throw
    }
    It 'rejects optional stale projection to project-global blocked' {
        $e = New-LegacyEntry -Legacy 'stale' -Projected 'blocked'; $e.routeState = 'blocked'; $e.evidenceState = 'unknown'; $e.effect = 'hard-block'; $e.reasonCode = 'SOURCE.STALE_OPTIONAL'; $e.scope = 'project:global'; $e.scopeKind = 'project-global'; $e.source.scope = 'task:read-only'; $e.source.scopeKind = 'task-object'
        { & $validator -ProjectRoot $projectRoot -ProjectionPath (Write-LegacyFixture $e 'optional-stale-global-block.json') -LegacyMapPath $map } | Should Throw
    }
    It 'rejects Accepted without evidence refs' {
        $e = New-LegacyEntry -Legacy 'Accepted' -Projected 'Accepted'; $e.evidenceState = 'closed'; $e.effect = 'review'; $e.evidenceRefs = @(); { & $validator -ProjectRoot $projectRoot -ProjectionPath (Write-LegacyFixture $e 'accepted-no-evidence.json') -LegacyMapPath $map } | Should Throw
    }
}
