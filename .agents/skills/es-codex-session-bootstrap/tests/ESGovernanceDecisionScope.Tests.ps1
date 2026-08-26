$skillRoot = Split-Path -Parent $PSScriptRoot
$validator = Join-Path $skillRoot 'scripts/Test-ESGovernanceDecisionScope.ps1'
$projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $skillRoot))
$reasonCodes = Join-Path $skillRoot 'references/governance-reason-codes.json'
$schema = Join-Path $skillRoot 'references/governance-decision.schema.json'
$transition = Join-Path $skillRoot 'references/governance-reason-transition.contract.json'
. (Join-Path $skillRoot 'scripts/Get-ESGovernanceDecisionId.ps1')

function Get-CanonicalHash($Value) { return Get-ESGovernanceCanonicalHash $Value }
function Set-DecisionId($Decision) {
    $Decision.decisionId = Get-ESGovernanceDecisionId $Decision
    return $Decision
}

function New-DecisionFixture {
    param(
        [string]$ReasonCode = 'RUNTIME.NOT_RUN',
        [string]$RouteState = 'core',
        [string]$EvidenceState = 'runtime-not-run',
        [string]$Effect = 'claim-cap',
        [string]$Profile = 'StaticDevelopment',
        [string]$Scope = 'task:read-only',
        [string]$ScopeKind = '',
        [int]$RouteDepth = 0
    )
    if ([string]::IsNullOrWhiteSpace($ScopeKind)) {
        $ScopeKind = if ($Scope -eq 'project:global') { 'project-global' } elseif ($Scope -match '^project:object:') { 'project-object' } elseif ($Scope -match '^context:') { 'context-object' } else { 'task-object' }
    }
    $registryPaths = @(
        '.agents/skills/es-codex-session-bootstrap/references/governance-reason-codes.json',
        '.agents/skills/es-codex-session-bootstrap/references/governance-profile-scope.registry.json',
        '.agents/skills/es-codex-session-bootstrap/references/governance-depth.registry.json',
        '.agents/skills/es-codex-session-bootstrap/references/governance-effect-override.registry.json',
        '.agents/skills/es-codex-session-bootstrap/references/governance-route-decision-table.json',
        '.agents/SKILL_CATALOG.yaml',
        '.agents/SKILL_REGISTRY.manifest.json'
    )
    $sourceRefs = @('.agents/skills/es-codex-session-bootstrap/references/governance-reason-codes.json', '.agents/skills/es-codex-session-bootstrap/references/governance-decision.schema.json') | ForEach-Object {
        [pscustomobject][ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath (Join-Path $projectRoot $_) -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
    $sourceCanonical = ConvertTo-Json -InputObject (@($sourceRefs | Sort-Object path -Unique)) -Compress -Depth 20
    $sourceBytes = [Text.UTF8Encoding]::new($false).GetBytes($sourceCanonical); $sha = [Security.Cryptography.SHA256]::Create(); try { $sourceHash = ([BitConverter]::ToString($sha.ComputeHash($sourceBytes))).Replace('-','').ToLowerInvariant() } finally { $sha.Dispose() }
    $registryMaterial = ConvertTo-Json -InputObject (@($registryPaths | ForEach-Object { [pscustomobject][ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath (Join-Path $projectRoot $_) -Algorithm SHA256).Hash.ToLowerInvariant() } } | Sort-Object path)) -Compress -Depth 20
    $registryBytes = [Text.UTF8Encoding]::new($false).GetBytes($registryMaterial); $sha2 = [Security.Cryptography.SHA256]::Create(); try { $registryHash = ([BitConverter]::ToString($sha2.ComputeHash($registryBytes))).Replace('-','').ToLowerInvariant() } finally { $sha2.Dispose() }
    $head = (& git -C $projectRoot rev-parse HEAD | Select-Object -First 1).Trim()
    $fixture = [pscustomobject][ordered]@{
        decisionId = ''; object = 'task-1'; field = 'evidence'; profile = $Profile; scope = $Scope; scopeKind = $ScopeKind
        routeState = $RouteState; evidenceState = $EvidenceState; effect = $Effect; reasonCode = $ReasonCode
        predicate = 'fixture predicate'; evidence = @('snapshot:fixture'); alternativePath = 'continue with bounded claim'
        recovery = 'run the affected bounded Profile'; rollback = 'discard this decision and re-evaluate'
        routeDepth = $RouteDepth; depthReasonCode = $null
        authorization = [pscustomobject]@{ mode = 'CurrentUserDirect'; requestedAction = 'read-only task'; requestedScope = $Scope }
        snapshot = [pscustomobject]@{ head = $head; sourceRefs = @($sourceRefs); sourceRefsHash = $sourceHash; registryHash = $registryHash; coverage = [pscustomobject]@{ normalizationVersion = 'path-sha256-v1'; sourceRefsHash = 'sorted unique canonical JSON of {path,sha256}'; registryHash = 'sorted canonical JSON of {path,sha256} for registryFiles'; head = 'current Git commit SHA' } }
    }
    return Set-DecisionId $fixture
}

function Write-Fixture($Value, [string]$Name) {
    $path = Join-Path $TestDrive $Name
    [IO.File]::WriteAllText($path, ($Value | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
    return $path
}

Describe 'Scoped governance decision validation' {
    It 'accepts core runtime-not-run with claim-cap' {
        $path = Write-Fixture (New-DecisionFixture) 'runtime-not-run.json'
        $result = & $validator -ProjectRoot $projectRoot -DecisionPath $path -ReasonCodesPath $reasonCodes -SchemaPath $schema
        $result.decisionStatus | Should Be 'Accepted'
        $result.acceptanceScope.profile | Should Be 'StaticDevelopment'
        $result.effect | Should Be 'claim-cap'
    }

    It 'accepts optional capability review and budget stop-next-read' {
        $optional = New-DecisionFixture -ReasonCode 'CAPABILITY.OPTIONAL_UNAVAILABLE' -EvidenceState 'unknown' -Effect 'review'
        (& $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $optional 'optional.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema).effect | Should Be 'review'
        $budget = New-DecisionFixture -ReasonCode 'BUDGET.NEXT_READ_EXCEEDED' -EvidenceState 'pending' -Effect 'stop-next-read'
        (& $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $budget 'budget.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema).effect | Should Be 'stop-next-read'
    }

    It 'keeps missing H/I receipt as claim-cap for development' {
        $fixture = New-DecisionFixture -ReasonCode 'EVIDENCE.RECEIPT_MISSING' -EvidenceState 'pending' -Effect 'claim-cap'
        (& $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $fixture 'receipt-development.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema).decisionStatus | Should Be 'Accepted'
    }

    It 'allows H/I acceptance to block only its acceptance Profile' {
        $fixture = New-DecisionFixture -ReasonCode 'EVIDENCE.RECEIPT_MISSING' -RouteState 'blocked' -EvidenceState 'pending' -Effect 'hard-block' -Profile 'HIAcceptance' -Scope 'context:HIAcceptance'
        $fixture | Add-Member -MemberType NoteProperty -Name effectOverride -Value ([pscustomobject]@{
            fromEffect = 'claim-cap'; toEffect = 'hard-block'; profile = 'HIAcceptance'
            predicate = 'the H/I acceptance Profile directly targets the missing receipt'
            justification = 'acceptance is scoped to the missing H/I receipt'
            evidenceRefs = @('snapshot:decision','profile:HIAcceptance')
        })
        (& $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $fixture 'receipt-acceptance.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema).routeState | Should Be 'blocked'
    }

    It 'rejects runtime-not-run as hard-block' {
        $fixture = New-DecisionFixture -RouteState 'blocked' -Effect 'hard-block'
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $fixture 'runtime-blocked.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
    }

    It 'rejects optional capability hard-block' {
        $fixture = New-DecisionFixture -ReasonCode 'CAPABILITY.OPTIONAL_UNAVAILABLE' -EvidenceState 'unknown' -RouteState 'blocked' -Effect 'hard-block'
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $fixture 'optional-blocked.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
    }

    It 'rejects optional stale or any project-level hard-block, including overrides' {
        $stale = New-DecisionFixture -ReasonCode 'SOURCE.STALE_OPTIONAL' -EvidenceState 'stale' -RouteState 'blocked' -Effect 'hard-block'
        $stale | Add-Member -MemberType NoteProperty -Name effectOverride -Value ([pscustomobject]@{ fromEffect = 'review'; toEffect = 'hard-block'; profile = 'StaticDevelopment'; predicate = 'unsafe override'; justification = 'unsafe override'; evidenceRefs = @('fixture') })
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $stale 'optional-stale-blocked.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
        $project = New-DecisionFixture -ReasonCode 'ROUTE.TARGET_AMBIGUOUS' -RouteState 'blocked' -EvidenceState 'unknown' -Effect 'hard-block' -Scope 'project:global'
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $project 'project-blocked.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
    }

    It 'limits malformed contract to a contract validation Profile' {
        $valid = New-DecisionFixture -ReasonCode 'CONTRACT.INVALID_SCOPE' -RouteState 'blocked' -EvidenceState 'unknown' -Effect 'hard-block' -Profile 'ContractValidation' -Scope 'context:contract:block-A'
        (& $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $valid 'contract-scoped.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema).routeState | Should Be 'blocked'
        $invalid = New-DecisionFixture -ReasonCode 'CONTRACT.INVALID_SCOPE' -RouteState 'blocked' -EvidenceState 'unknown' -Effect 'hard-block' -Profile 'ProjectAcceptance' -Scope 'project:global'
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $invalid 'contract-global.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
    }

    It 'accepts depth one, requires a reason for depth two, and rejects unbounded depth' {
        $one = New-DecisionFixture -ReasonCode 'ROUTE.EXTENSION_TRIGGERED' -RouteState 'extension' -EvidenceState 'pending' -Effect 'review' -RouteDepth 1
        (& $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $one 'depth-one.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema).routeDepth | Should Be 1
        $two = New-DecisionFixture -ReasonCode 'ROUTE.DEPTH_2_AUTHORIZED' -RouteState 'extension' -EvidenceState 'pending' -Effect 'review' -Profile 'SpecializedExtension' -Scope 'task:depth-two' -RouteDepth 2
        $two.depthReasonCode = 'ROUTE.DEPTH_2_AUTHORIZED'
        Set-DecisionId $two | Out-Null
        (& $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $two 'depth-two.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema).routeDepth | Should Be 2
        $missing = New-DecisionFixture -ReasonCode 'ROUTE.EXTENSION_TRIGGERED' -RouteState 'extension' -EvidenceState 'pending' -Effect 'review' -RouteDepth 2
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $missing 'depth-two-missing-reason.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
        $unbounded = New-DecisionFixture -ReasonCode 'ROUTE.DEPTH_2_AUTHORIZED' -RouteState 'extension' -EvidenceState 'pending' -Effect 'review' -RouteDepth 3
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $unbounded 'depth-three.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
    }

    It 'rejects narrowing a CurrentUserDirect scope' {
        $fixture = New-DecisionFixture -Scope 'task:target-only'
        $fixture.authorization.requestedScope = 'task:full-authorized-scope'
        Set-DecisionId $fixture | Out-Null
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $fixture 'scope-narrowed.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
    }

    It 'rejects malformed or unknown decisions without project-level projection' {
        $fixture = New-DecisionFixture -ReasonCode 'UNKNOWN.CODE'
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $fixture 'unknown-reason.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
        $fixture = New-DecisionFixture
        $fixture.routeState = 'blocked'; $fixture.effect = 'review'
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $fixture 'blocked-review.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
        $fixture = New-DecisionFixture -ReasonCode 'EVIDENCE.RECEIPT_MISSING' -Effect 'hard-block'
        $fixture | Add-Member -MemberType NoteProperty -Name effectOverride -Value $null
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $fixture 'null-effect-override.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
    }

    It 'rejects a reason code whose route or evidence state deviates from its default' {
        $stale = New-DecisionFixture -ReasonCode 'SOURCE.STALE_REQUIRED' -RouteState 'blocked' -EvidenceState 'closed' -Effect 'hard-block'
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $stale 'required-stale-closed.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
        $extension = New-DecisionFixture -ReasonCode 'ROUTE.EXTENSION_TRIGGERED' -RouteState 'core' -EvidenceState 'pending' -Effect 'review'
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $extension 'extension-core.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
    }

    It 'rejects an unregistered directional override' {
        $fixture = New-DecisionFixture -ReasonCode 'SOURCE.STALE_REQUIRED' -RouteState 'blocked' -EvidenceState 'stale' -Effect 'review'
        $fixture | Add-Member -MemberType NoteProperty -Name effectOverride -Value ([pscustomobject]@{ fromEffect = 'hard-block'; toEffect = 'review'; profile = 'StaticDevelopment'; predicate = 'unregistered downgrade'; justification = 'unregistered downgrade'; evidenceRefs = @('snapshot:decision') })
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $fixture 'unregistered-override.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
    }

    It 'rejects an absolute or missing transition contract source' {
        $absolute = (Resolve-Path $transition).Path
        $fixture = Write-Fixture (New-DecisionFixture) 'transition-source-boundary.json'
        { & $validator -ProjectRoot $projectRoot -DecisionPath $fixture -TransitionPath $absolute } | Should Throw
        { & $validator -ProjectRoot $projectRoot -DecisionPath $fixture -TransitionPath 'does-not-exist.json' } | Should Throw
    }

    It 'rejects a non-Git snapshot head and mismatched scope kind' {
        $head = New-DecisionFixture; $head.snapshot.head = 'fixture'
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $head 'fake-head.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
        $scope = New-DecisionFixture; $scope.scopeKind = 'project-global'
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $scope 'scope-kind-mismatch.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
    }

    It 'rejects a legacy or malformed decisionId even when the other fields are valid' {
        $fixture = New-DecisionFixture
        $fixture.decisionId = 'decision-fixture-1'
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $fixture 'legacy-decision-id.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
    }

    It 'rejects a forged SourceRef hash or registry hash' {
        $source = New-DecisionFixture; $source.snapshot.sourceRefs[0].sha256 = ('0' * 64)
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $source 'forged-source-hash.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
        $registry = New-DecisionFixture; $registry.snapshot.registryHash = ('f' * 64)
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $registry 'forged-registry-hash.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
    }

    It 'rejects task-object to project-global scope expansion' {
        $fixture = New-DecisionFixture -ReasonCode 'SOURCE.STALE_OPTIONAL' -EvidenceState 'stale' -Effect 'review' -Profile 'ProjectAcceptance' -Scope 'project:global' -ScopeKind 'project-global'
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $fixture 'scope-expanded-project-global.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
    }

    It 'rejects depth two without registry membership and profile authorization' {
        $fixture = New-DecisionFixture -ReasonCode 'ROUTE.DEPTH_2_AUTHORIZED' -RouteState 'extension' -EvidenceState 'pending' -Effect 'review' -Profile 'StaticDevelopment' -Scope 'task:depth-two' -RouteDepth 2
        $fixture.depthReasonCode = 'ROUTE.DEPTH_2_AUTHORIZED'
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $fixture 'depth-two-profile-denied.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
    }

    It 'rejects reusing a decisionId after the snapshot changes and accepts a regenerated id' {
        $original = New-DecisionFixture
        $oldId = [string]$original.decisionId
        $changed = New-DecisionFixture
        $newRefPath = '.agents/skills/es-codex-session-bootstrap/references/governance-reason-transition.contract.json'
        $newRef = [pscustomobject][ordered]@{ path = $newRefPath; sha256 = (Get-FileHash -LiteralPath (Join-Path $projectRoot $newRefPath) -Algorithm SHA256).Hash.ToLowerInvariant() }
        $changed.snapshot.sourceRefs = @($changed.snapshot.sourceRefs) + $newRef
        $changed.snapshot.sourceRefsHash = Get-CanonicalHash (@($changed.snapshot.sourceRefs | Sort-Object path -Unique))
        $changed.decisionId = $oldId
        $reuseError = ''
        try {
            & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $changed 'reused-decision-id.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema
        } catch {
            $reuseError = $_.Exception.Message
        }
        $reuseError | Should Match 'decisionId is not bound to the decision fields and snapshot hashes'
        $newId = [string](Set-DecisionId $changed).decisionId
        $newId | Should Not Be $oldId
        (& $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $changed 'new-snapshot-decision-id.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema).decisionStatus | Should Be 'Accepted'
    }

    It 'accepts only the registered task, context, and project-object Profile boundaries' {
        $accepted = @(
            @{ Profile = 'StaticDevelopment'; Scope = 'task:matrix-static'; ScopeKind = 'task-object' },
            @{ Profile = 'StaticDevelopment'; Scope = 'context:matrix-static'; ScopeKind = 'context-object' },
            @{ Profile = 'HIAcceptance'; Scope = 'context:HIAcceptance'; ScopeKind = 'context-object' },
            @{ Profile = 'ContractValidation'; Scope = 'context:contract:matrix'; ScopeKind = 'context-object' },
            @{ Profile = 'ProjectAcceptance'; Scope = 'project:object:matrix'; ScopeKind = 'project-object' },
            @{ Profile = 'SpecializedExtension'; Scope = 'task:matrix-extension'; ScopeKind = 'task-object' },
            @{ Profile = 'SpecializedExtension'; Scope = 'context:matrix-extension'; ScopeKind = 'context-object' }
        )
        for ($i = 0; $i -lt $accepted.Count; $i++) {
            $case = $accepted[$i]
            $fixture = New-DecisionFixture -Profile $case.Profile -Scope $case.Scope -ScopeKind $case.ScopeKind
            $result = & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $fixture "profile-scope-accepted-$i.json") -ReasonCodesPath $reasonCodes -SchemaPath $schema
            $result.decisionStatus | Should Be 'Accepted'
            $result.acceptanceScope.profile | Should Be $case.Profile
            $result.acceptanceScope.scope | Should Be $case.Scope
        }

        $rejected = @(
            @{ Profile = 'StaticDevelopment'; Scope = 'project:object:matrix'; ScopeKind = 'project-object' },
            @{ Profile = 'HIAcceptance'; Scope = 'task:matrix'; ScopeKind = 'task-object' },
            @{ Profile = 'ContractValidation'; Scope = 'task:matrix'; ScopeKind = 'task-object' },
            @{ Profile = 'ProjectAcceptance'; Scope = 'context:matrix'; ScopeKind = 'context-object' },
            @{ Profile = 'SpecializedExtension'; Scope = 'project:object:matrix'; ScopeKind = 'project-object' }
        )
        for ($i = 0; $i -lt $rejected.Count; $i++) {
            $case = $rejected[$i]
            $fixture = New-DecisionFixture -Profile $case.Profile -Scope $case.Scope -ScopeKind $case.ScopeKind
            { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $fixture "profile-scope-rejected-$i.json") -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
        }
    }

    It 'keeps optional stale, runtime-not-run, and budget stops out of project-global hard-block' {
        $reasons = @(
            @{ Code = 'SOURCE.STALE_OPTIONAL'; EvidenceState = 'stale'; Effect = 'review' },
            @{ Code = 'RUNTIME.NOT_RUN'; EvidenceState = 'runtime-not-run'; Effect = 'claim-cap' },
            @{ Code = 'BUDGET.NEXT_READ_EXCEEDED'; EvidenceState = 'pending'; Effect = 'stop-next-read' }
        )
        $boundedScopes = @(
            @{ Profile = 'StaticDevelopment'; Scope = 'task:matrix' },
            @{ Profile = 'StaticDevelopment'; Scope = 'context:matrix' },
            @{ Profile = 'ProjectAcceptance'; Scope = 'project:object:matrix' }
        )
        $caseIndex = 0
        foreach ($reason in $reasons) {
            foreach ($bounded in $boundedScopes) {
                $fixture = New-DecisionFixture -ReasonCode $reason.Code -EvidenceState $reason.EvidenceState -Effect $reason.Effect -Profile $bounded.Profile -Scope $bounded.Scope
                $result = & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $fixture "non-global-bounded-$caseIndex.json") -ReasonCodesPath $reasonCodes -SchemaPath $schema
                $result.routeState | Should Be 'core'
                $result.effect | Should Be $reason.Effect
                $caseIndex++
            }

            $global = New-DecisionFixture -ReasonCode $reason.Code -RouteState 'blocked' -EvidenceState $reason.EvidenceState -Effect 'hard-block' -Profile 'ProjectAcceptance' -Scope 'project:global' -ScopeKind 'project-global'
            { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $global "non-global-hard-block-$($reason.Code).json") -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
        }
    }

    It 'preserves scoped P0 and authorization hard-blocks and rejects downgrade overrides' {
        $reasons = @('P0.DIRECT_VIOLATION', 'AUTH.SCOPE_UNRESOLVED')
        $scopes = @(
            @{ Profile = 'StaticDevelopment'; Scope = 'task:matrix' },
            @{ Profile = 'StaticDevelopment'; Scope = 'context:matrix' },
            @{ Profile = 'ProjectAcceptance'; Scope = 'project:object:matrix' }
        )
        $caseIndex = 0
        foreach ($reasonCode in $reasons) {
            foreach ($bounded in $scopes) {
                $blocked = New-DecisionFixture -ReasonCode $reasonCode -RouteState 'blocked' -EvidenceState 'unknown' -Effect 'hard-block' -Profile $bounded.Profile -Scope $bounded.Scope
                $result = & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $blocked "required-hard-block-$caseIndex.json") -ReasonCodesPath $reasonCodes -SchemaPath $schema
                $result.routeState | Should Be 'blocked'
                $result.effect | Should Be 'hard-block'

                $downgraded = New-DecisionFixture -ReasonCode $reasonCode -RouteState 'core' -EvidenceState 'unknown' -Effect 'review' -Profile $bounded.Profile -Scope $bounded.Scope
                $downgraded | Add-Member -MemberType NoteProperty -Name effectOverride -Value ([pscustomobject]@{
                    fromEffect = 'hard-block'; toEffect = 'review'; profile = $bounded.Profile
                    predicate = 'attempted downgrade'; justification = 'attempted downgrade'; evidenceRefs = @('snapshot:decision')
                })
                { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $downgraded "required-downgrade-$caseIndex.json") -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
                $caseIndex++
            }
        }
    }

    It 'binds ManagedAIBrain decisions to the exact invocation scope' {
        $exact = New-DecisionFixture -Scope 'task:managed-exact'
        $exact.authorization.mode = 'ManagedAIBrain'
        Set-DecisionId $exact | Out-Null
        (& $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $exact 'managed-scope-exact.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema).decisionStatus | Should Be 'Accepted'

        $substituted = New-DecisionFixture -Scope 'task:managed-target'
        $substituted.authorization.mode = 'ManagedAIBrain'
        $substituted.authorization.requestedScope = 'task:managed-other'
        Set-DecisionId $substituted | Out-Null
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $substituted 'managed-scope-substituted.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw

        $caseChanged = New-DecisionFixture -Scope 'task:managed-case'
        $caseChanged.authorization.mode = 'ManagedAIBrain'
        $caseChanged.authorization.requestedScope = 'task:Managed-case'
        Set-DecisionId $caseChanged | Out-Null
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $caseChanged 'managed-scope-case-substituted.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw

        $widened = New-DecisionFixture -Profile 'ProjectAcceptance' -Scope 'project:object:managed-target'
        $widened.authorization.mode = 'ManagedAIBrain'
        $widened.authorization.requestedScope = 'task:managed-target'
        Set-DecisionId $widened | Out-Null
        { & $validator -ProjectRoot $projectRoot -DecisionPath (Write-Fixture $widened 'managed-scope-widened.json') -ReasonCodesPath $reasonCodes -SchemaPath $schema } | Should Throw
    }
}
