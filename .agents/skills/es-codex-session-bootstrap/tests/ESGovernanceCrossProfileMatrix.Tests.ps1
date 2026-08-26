$skillRoot = Split-Path -Parent $PSScriptRoot
$projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $skillRoot))
$validator = Join-Path $skillRoot 'scripts/Test-ESGovernanceDecisionScope.ps1'
$reasonCodes = Join-Path $skillRoot 'references/governance-reason-codes.json'
$schema = Join-Path $skillRoot 'references/governance-decision.schema.json'
. (Join-Path $skillRoot 'scripts/Get-ESGovernanceDecisionId.ps1')

function Set-MatrixDecisionId($Decision) {
    $Decision.decisionId = Get-ESGovernanceDecisionId $Decision
    return $Decision
}
function New-MatrixDecision {
    param(
        [string]$Profile, [string]$Scope, [string]$ReasonCode, [string]$RouteState,
        [string]$EvidenceState, [string]$Effect, [string]$RequestedScope = $Scope
    )
    $scopeKind = if ($Scope -eq 'project:global') { 'project-global' } elseif ($Scope -match '^project:object:') { 'project-object' } elseif ($Scope -match '^context:') { 'context-object' } else { 'task-object' }
    $registryPaths = @(
        '.agents/skills/es-codex-session-bootstrap/references/governance-reason-codes.json',
        '.agents/skills/es-codex-session-bootstrap/references/governance-profile-scope.registry.json',
        '.agents/skills/es-codex-session-bootstrap/references/governance-depth.registry.json',
        '.agents/skills/es-codex-session-bootstrap/references/governance-effect-override.registry.json',
        '.agents/skills/es-codex-session-bootstrap/references/governance-route-decision-table.json',
        '.agents/SKILL_CATALOG.yaml', '.agents/SKILL_REGISTRY.manifest.json'
    )
    $sourceRefs = @('.agents/skills/es-codex-session-bootstrap/references/governance-reason-codes.json', '.agents/skills/es-codex-session-bootstrap/references/governance-decision.schema.json') | ForEach-Object {
        [pscustomobject][ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath (Join-Path $projectRoot $_) -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
    $sourceHash = Get-ESGovernanceCanonicalHash (@($sourceRefs | Sort-Object path -Unique))
    $registryMaterial = @($registryPaths | ForEach-Object { [pscustomobject][ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath (Join-Path $projectRoot $_) -Algorithm SHA256).Hash.ToLowerInvariant() } } | Sort-Object path)
    $registryHash = Get-ESGovernanceCanonicalHash $registryMaterial
    $head = (& git -C $projectRoot rev-parse HEAD | Select-Object -First 1).Trim()
    $decision = [pscustomobject][ordered]@{
        decisionId = ''; object = 'matrix-target'; field = 'evidence'; profile = $Profile; scope = $Scope; scopeKind = $scopeKind
        routeState = $RouteState; evidenceState = $EvidenceState; effect = $Effect; reasonCode = $ReasonCode
        predicate = 'cross-profile matrix predicate'; evidence = @('snapshot:matrix'); alternativePath = 'continue only within the accepted scope'
        recovery = 're-evaluate the affected Profile from a fresh bounded snapshot'; rollback = 'discard the decision and restore the prior bounded route'
        routeDepth = 0; depthReasonCode = $null
        authorization = [pscustomobject]@{ mode = 'CurrentUserDirect'; requestedAction = 'bounded matrix validation'; requestedScope = $RequestedScope }
        snapshot = [pscustomobject]@{ head = $head; sourceRefs = @($sourceRefs); sourceRefsHash = $sourceHash; registryHash = $registryHash; coverage = [pscustomobject]@{ normalizationVersion = 'path-sha256-v1'; sourceRefsHash = 'sorted unique canonical JSON of {path,sha256}'; registryHash = 'sorted canonical JSON of {path,sha256} for registryFiles'; head = 'current Git commit SHA' } }
    }
    return Set-MatrixDecisionId $decision
}
function Write-MatrixFixture($Value, [string]$Name) {
    $path = Join-Path $TestDrive $Name
    [IO.File]::WriteAllText($path, ($Value | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
    return $path
}

Describe 'Cross-profile governance decision matrix' {
    It 'accepts task-object and context-object core routes without project projection' {
        $task = & $validator -ProjectRoot $projectRoot -DecisionPath (Write-MatrixFixture (New-MatrixDecision 'StaticDevelopment' 'task:read-only' 'RUNTIME.NOT_RUN' 'core' 'runtime-not-run' 'claim-cap') 'task-runtime.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema
        $context = & $validator -ProjectRoot $projectRoot -DecisionPath (Write-MatrixFixture (New-MatrixDecision 'StaticDevelopment' 'context:read-only' 'RUNTIME.NOT_RUN' 'core' 'runtime-not-run' 'claim-cap') 'context-runtime.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema
        $task.acceptanceScope.scopeKind | Should Be 'task-object'
        $context.acceptanceScope.scopeKind | Should Be 'context-object'
        $task.effect | Should Be 'claim-cap'
        $context.effect | Should Be 'claim-cap'
    }

    It 'accepts contract and project-object scopes while keeping project-global optional evidence out' {
        $contract = & $validator -ProjectRoot $projectRoot -DecisionPath (Write-MatrixFixture (New-MatrixDecision 'ContractValidation' 'context:contract:block-A' 'CONTRACT.INVALID_SCOPE' 'blocked' 'unknown' 'hard-block') 'contract-context.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema
        $projectObject = & $validator -ProjectRoot $projectRoot -DecisionPath (Write-MatrixFixture (New-MatrixDecision 'ProjectAcceptance' 'project:object:acceptance' 'SOURCE.STALE_OPTIONAL' 'core' 'stale' 'review') 'project-object-stale.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema
        $contract.acceptanceScope.scopeKind | Should Be 'context-object'
        $projectObject.acceptanceScope.scopeKind | Should Be 'project-object'
        $projectObject.effect | Should Be 'review'
        $global = New-MatrixDecision 'ProjectAcceptance' 'project:global' 'SOURCE.STALE_OPTIONAL' 'core' 'stale' 'review'
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-MatrixFixture $global 'optional-stale-global.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
    }

    It 'keeps runtime-not-run and budget stop scoped and non-blocking' {
        $runtime = & $validator -ProjectRoot $projectRoot -DecisionPath (Write-MatrixFixture (New-MatrixDecision 'ProjectAcceptance' 'project:object:runtime' 'RUNTIME.NOT_RUN' 'core' 'runtime-not-run' 'claim-cap') 'runtime-project-object.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema
        $budget = & $validator -ProjectRoot $projectRoot -DecisionPath (Write-MatrixFixture (New-MatrixDecision 'ProjectAcceptance' 'project:object:budget' 'BUDGET.NEXT_READ_EXCEEDED' 'core' 'pending' 'stop-next-read') 'budget-project-object.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema
        $runtime.effect | Should Be 'claim-cap'
        $budget.effect | Should Be 'stop-next-read'
        $globalRuntime = New-MatrixDecision 'ProjectAcceptance' 'project:global' 'RUNTIME.NOT_RUN' 'core' 'runtime-not-run' 'claim-cap'
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-MatrixFixture $globalRuntime 'runtime-global.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
    }

    It 'preserves true P0 hard-block and rejects authorization narrowing' {
        $p0 = & $validator -ProjectRoot $projectRoot -DecisionPath (Write-MatrixFixture (New-MatrixDecision 'StaticDevelopment' 'task:read-only' 'P0.DIRECT_VIOLATION' 'blocked' 'unknown' 'hard-block') 'p0-direct.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema
        $p0.effect | Should Be 'hard-block'
        $p0.routeState | Should Be 'blocked'
        $narrowed = New-MatrixDecision 'StaticDevelopment' 'task:read-only' 'AUTH.SCOPE_UNRESOLVED' 'blocked' 'unknown' 'hard-block' 'task:full-authorized-scope'
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-MatrixFixture $narrowed 'authorization-narrowed.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
        $downgraded = New-MatrixDecision 'StaticDevelopment' 'task:read-only' 'P0.DIRECT_VIOLATION' 'blocked' 'unknown' 'review'
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-MatrixFixture $downgraded 'p0-downgraded.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
    }
}
