$skillRoot = Split-Path -Parent $PSScriptRoot
$projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $skillRoot))
$validator = Join-Path $skillRoot 'scripts/Test-ESGovernanceBypassObservation.ps1'

function Write-BypassObservation($Value, [string]$Name) {
    $path = Join-Path $TestDrive $Name
    [IO.File]::WriteAllText($path, ($Value | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
    return $path
}
function New-BypassObservation {
    [pscustomobject][ordered]@{
        profile = 'StaticDevelopment'; scope = 'task:read-only'; scopeKind = 'task-object'; routeMode = 'read-only-bypass-observation'
        productionRouteIntegrated = $false; globalP0Integrated = $false
        decisionIdAlgorithm = 'es-governance-decision-id-v1'; helperPath = '.agents/skills/es-codex-session-bootstrap/scripts/Get-ESGovernanceDecisionId.ps1'
        decisionIdExpected = 'decision-0123456789abcdef01234567'; decisionIdObserved = 'decision-0123456789abcdef01234567'
        bypassDetected = $false; observationState = 'disabled'; rollbackState = 'available'
        observations = @(
            [pscustomobject][ordered]@{ code = 'BYPASS_CHECK_PASSED'; detail = 'no alternate decisionId implementation observed' },
            [pscustomobject][ordered]@{ code = 'DECISION_ID_MATCH'; detail = 'expected and observed IDs match' },
            [pscustomobject][ordered]@{ code = 'ROLLBACK_AVAILABLE'; detail = 'bypass remains disabled and reversible' },
            [pscustomobject][ordered]@{ code = 'PRODUCTION_ROUTE_DISABLED'; detail = 'production route remains disabled' },
            [pscustomobject][ordered]@{ code = 'GLOBAL_P0_DISABLED'; detail = 'global P0 remains disabled' }
        )
    }
}

Describe 'Scoped bypass observation contract' {
    It 'accepts the disabled single-scope observation baseline' {
        $result = & $validator -ProjectRoot $projectRoot -ObservationPath (Write-BypassObservation (New-BypassObservation) 'baseline.json')
        $result.observationStatus | Should Be 'Accepted'
        $result.acceptanceScope.profile | Should Be 'StaticDevelopment'
        $result.acceptanceScope.scope | Should Be 'task:read-only'
        $result.effect | Should Be 'review'
        $result.productionRouteIntegrated | Should Be $false
        $result.globalP0Integrated | Should Be $false
        $rolledBack = New-BypassObservation; $rolledBack.observationState = 'rolled-back'; $rolledBack.rollbackState = 'rolled-back'
        (& $validator -ProjectRoot $projectRoot -ObservationPath (Write-BypassObservation $rolledBack 'rolled-back.json')).observationStatus | Should Be 'Accepted'
    }

    It 'rejects helper bypass, decisionId mismatch, and unavailable rollback' {
        $bypass = New-BypassObservation; $bypass.bypassDetected = $true
        (& $validator -ProjectRoot $projectRoot -ObservationPath (Write-BypassObservation $bypass 'bypass-detected.json')).observationStatus | Should Be 'Rejected'
        $mismatch = New-BypassObservation; $mismatch.decisionIdObserved = 'decision-fedcba9876543210fedcba98'
        (& $validator -ProjectRoot $projectRoot -ObservationPath (Write-BypassObservation $mismatch 'id-mismatch.json')).decisionIdMatched | Should Be $false
        $rollback = New-BypassObservation; $rollback.rollbackState = 'unavailable'
        (& $validator -ProjectRoot $projectRoot -ObservationPath (Write-BypassObservation $rollback 'rollback-unavailable.json')).observationStatus | Should Be 'Rejected'
    }

    It 'rejects production or global P0 takeover and scope expansion' {
        $production = New-BypassObservation; $production.productionRouteIntegrated = $true
        (& $validator -ProjectRoot $projectRoot -ObservationPath (Write-BypassObservation $production 'production-enabled.json')).observationStatus | Should Be 'Rejected'
        $global = New-BypassObservation; $global.globalP0Integrated = $true
        (& $validator -ProjectRoot $projectRoot -ObservationPath (Write-BypassObservation $global 'global-p0-enabled.json')).observationStatus | Should Be 'Rejected'
        $expanded = New-BypassObservation; $expanded.scope = 'project:global'; $expanded.scopeKind = 'project-global'
        (& $validator -ProjectRoot $projectRoot -ObservationPath (Write-BypassObservation $expanded 'scope-expanded.json')).observationStatus | Should Be 'Rejected'
    }

    It 'rejects unregistered observation codes' {
        $fixture = New-BypassObservation
        $fixture.observations += [pscustomobject][ordered]@{ code = 'UNREGISTERED.BYPASS'; detail = 'invalid' }
        (& $validator -ProjectRoot $projectRoot -ObservationPath (Write-BypassObservation $fixture 'unknown-code.json')).observationStatus | Should Be 'Rejected'
    }

    It 'rejects Automation to governance cross-contract projections' {
        $cases = @(
            [pscustomobject]@{ Name = 'guid-as-governance-id'; Field = 'automationDecisionId'; Value = '0123456789abcdef0123456789abcdef' },
            [pscustomobject]@{ Name = 'accepted-as-closed'; Field = 'automationDecisionStatus'; Value = 'Accepted' },
            [pscustomobject]@{ Name = 'blocked-as-hard-block'; Field = 'projectedEffect'; Value = 'hard-block' },
            [pscustomobject]@{ Name = 'static-as-profile'; Field = 'automationEvidenceScope'; Value = 'Static' },
            [pscustomobject]@{ Name = 'runtime-as-scope'; Field = 'projectedScope'; Value = 'task:read-only' },
            [pscustomobject]@{ Name = 'governance-hash-as-decision'; Field = 'automationGovernanceHash'; Value = ('a' * 64) },
            [pscustomobject]@{ Name = 'governance-hash-as-snapshot'; Field = 'projectedSnapshotHash'; Value = ('b' * 64) },
            [pscustomobject]@{ Name = 'project-global-projection'; Field = 'projectedScope'; Value = 'project:global' }
        )
        foreach ($case in $cases) {
            $fixture = New-BypassObservation
            $fixture | Add-Member -MemberType NoteProperty -Name automationProjection -Value ([pscustomobject][ordered]@{ $case.Field = $case.Value })
            (& $validator -ProjectRoot $projectRoot -ObservationPath (Write-BypassObservation $fixture ($case.Name + '.json'))).observationStatus | Should Be 'Rejected'
        }
        $rootProjection = New-BypassObservation
        $rootProjection | Add-Member -MemberType NoteProperty -Name automationDecisionId -Value '0123456789abcdef0123456789abcdef'
        (& $validator -ProjectRoot $projectRoot -ObservationPath (Write-BypassObservation $rootProjection 'root-projection.json')).observationStatus | Should Be 'Rejected'
    }
}
