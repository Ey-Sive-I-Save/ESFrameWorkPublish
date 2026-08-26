[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [Parameter(Mandatory = $true)][string]$DecisionPath,
    [string]$ReasonCodesPath = '.agents/skills/es-codex-session-bootstrap/references/governance-reason-codes.json',
    [string]$SchemaPath = '.agents/skills/es-codex-session-bootstrap/references/governance-decision.schema.json',
    [string]$TransitionPath = '.agents/skills/es-codex-session-bootstrap/references/governance-reason-transition.contract.json',
    [string]$TransitionSchemaPath = '.agents/skills/es-codex-session-bootstrap/references/governance-reason-transition.schema.json',
    [string]$ProfileScopeRegistryPath = '.agents/skills/es-codex-session-bootstrap/references/governance-profile-scope.registry.json',
    [string]$DepthRegistryPath = '.agents/skills/es-codex-session-bootstrap/references/governance-depth.registry.json',
    [string]$EffectOverrideRegistryPath = '.agents/skills/es-codex-session-bootstrap/references/governance-effect-override.registry.json',
    [string]$RouteTablePath = '.agents/skills/es-codex-session-bootstrap/references/governance-route-decision-table.json',
    [string]$SnapshotContractPath = '.agents/skills/es-codex-session-bootstrap/references/governance-snapshot.contract.json'
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'Get-ESGovernanceDecisionId.ps1')

function Resolve-ProjectPath([string]$Root, [string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    return [IO.Path]::GetFullPath((Join-Path $Root $Path))
}
function Resolve-ProjectRelativePath([string]$Root, [string]$Path, [string]$Label) {
    if ([IO.Path]::IsPathRooted($Path)) { throw "ES-GOV-DECISION-003: $Label must be project-relative: $Path" }
    $full = [IO.Path]::GetFullPath((Join-Path $Root $Path))
    $prefix = $Root.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw "ES-GOV-DECISION-003: $Label escapes ProjectRoot: $Path" }
    return $full
}

$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
$decisionFull = Resolve-ProjectPath $root $DecisionPath
$reasonFull = Resolve-ProjectPath $root $ReasonCodesPath
$schemaFull = Resolve-ProjectPath $root $SchemaPath
$transitionFull = Resolve-ProjectRelativePath $root $TransitionPath 'TransitionPath'
$transitionSchemaFull = Resolve-ProjectRelativePath $root $TransitionSchemaPath 'TransitionSchemaPath'
$profileScopeFull = Resolve-ProjectRelativePath $root $ProfileScopeRegistryPath 'ProfileScopeRegistryPath'
$depthFull = Resolve-ProjectRelativePath $root $DepthRegistryPath 'DepthRegistryPath'
$effectOverrideFull = Resolve-ProjectRelativePath $root $EffectOverrideRegistryPath 'EffectOverrideRegistryPath'
$routeTableFull = Resolve-ProjectRelativePath $root $RouteTablePath 'RouteTablePath'
$snapshotContractFull = Resolve-ProjectRelativePath $root $SnapshotContractPath 'SnapshotContractPath'

function Read-StrictJson([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "ES-GOV-DECISION-001: $Label not found: $Path" }
    try {
        $utf8 = [Text.UTF8Encoding]::new($false, $true)
        return [IO.File]::ReadAllText((Resolve-Path -LiteralPath $Path).Path, $utf8) | ConvertFrom-Json
    } catch { throw "ES-GOV-DECISION-001: $Label is not strict UTF-8 JSON: $Path" }
}

$decision = Read-StrictJson $decisionFull 'Decision'
$reasonRegistry = Read-StrictJson $reasonFull 'Reason-code registry'
$schema = Read-StrictJson $schemaFull 'Decision schema'
$transitionContract = Read-StrictJson $transitionFull 'Reason transition contract'
$transitionSchema = Read-StrictJson $transitionSchemaFull 'Reason transition schema'
$profileScopeRegistry = Read-StrictJson $profileScopeFull 'Profile/scope registry'
$depthRegistry = Read-StrictJson $depthFull 'Depth registry'
$effectOverrideRegistry = Read-StrictJson $effectOverrideFull 'Effect override registry'
$routeTable = Read-StrictJson $routeTableFull 'Route decision table'
$snapshotContract = Read-StrictJson $snapshotContractFull 'Snapshot contract'
$errors = [System.Collections.Generic.List[string]]::new()

function Has-Property($Object, [string]$Name) {
    if ($null -eq $Object) { return $false }
    return $null -ne $Object.PSObject.Properties[$Name]
}
function Require-Text($Object, [string]$Name) {
    if (-not (Has-Property $Object $Name) -or [string]::IsNullOrWhiteSpace([string]$Object.$Name)) { $errors.Add("missing or empty $Name") }
}
function Add-Error([string]$Message) { $errors.Add($Message) }

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}
function Get-CanonicalHash($Value) { return Get-ESGovernanceCanonicalHash $Value }
function Normalize-Relative([string]$Path) { return $Path.Replace('\','/').Trim() }
function Get-DecisionFingerprint($Value) { return Get-ESGovernanceCanonicalHash (Get-ESGovernanceDecisionFingerprint $Value) }

function Test-StateEqual($Left, $Right) {
    return [string]$Left.routeState -eq [string]$Right.routeState -and [string]$Left.evidenceState -eq [string]$Right.evidenceState -and [string]$Left.effect -eq [string]$Right.effect
}

if ([int]$schema.schemaVersion -ne 1 -or $null -eq $schema.required) { Add-Error 'decision schema must declare schemaVersion=1 and required fields' }
foreach ($schemaField in @($schema.required)) {
    if (-not (Has-Property $decision ([string]$schemaField))) { Add-Error "schema-required field missing: $schemaField" }
}
if ([int]$transitionContract.schemaVersion -ne 1 -or [string]$transitionContract.decisionModel -ne 'routeState/evidenceState/effect') { Add-Error 'reason transition contract must declare schemaVersion=1 and the three-axis decision model' }
foreach ($axis in @('routeState','evidenceState','effect')) {
    if ([string]$transitionContract.defaultStatePolicy.$axis -ne 'exact') { Add-Error "reason transition default policy must be exact for $axis" }
}
if ([int]$transitionSchema.schemaVersion -ne 1 -or $null -eq $transitionSchema.required) { Add-Error 'reason transition schema must declare schemaVersion=1 and required fields' }
foreach ($required in @('contractId','decisionModel','defaultStatePolicy','transitionExceptions','forbiddenTransitions')) {
    if (-not (Has-Property $transitionContract $required)) { Add-Error "reason transition contract is missing $required" }
}
if ([int]$profileScopeRegistry.schemaVersion -ne 1 -or @($profileScopeRegistry.scopeKinds).Count -ne 4) { Add-Error 'profile/scope registry must declare schemaVersion=1 and four scope kinds' }
if ([int]$depthRegistry.schemaVersion -ne 1 -or [int]$depthRegistry.defaultMaxDepth -ne 1 -or [int]$depthRegistry.maxDepth -ne 2) { Add-Error 'depth registry must declare default depth=1 and maximum depth=2' }
if ([int]$effectOverrideRegistry.schemaVersion -ne 1) { Add-Error 'effect override registry must declare schemaVersion=1' }
if ([int]$routeTable.schemaVersion -ne 1 -or [int]$routeTable.defaultMaxExtensionDepth -ne 1 -or [int]$routeTable.maxExtensionDepth -ne 2) { Add-Error 'route decision table has invalid depth contract' }
if ([string]$routeTable.unknownState.routeState -ne 'core' -or [string]$routeTable.unknownState.effect -ne 'review') { Add-Error 'unknown state must remain core/review and scoped' }
if (@($routeTable.effectPrecedence | ForEach-Object { [string]$_ }) -join ',' -ne 'hard-block,stop-next-read,claim-cap,review') { Add-Error 'route decision effect precedence is not closed' }
foreach ($transitionRow in @($routeTable.transitions)) {
    foreach ($name in @('from','trigger','to','reasonCode')) { if (-not (Has-Property $transitionRow $name)) { Add-Error "route decision row missing $name" } }
    if (@('core','extension','blocked') -notcontains [string]$transitionRow.from -or @('core','extension','blocked') -notcontains [string]$transitionRow.to) { Add-Error 'route decision row has invalid route state' }
    if (@($reasonRegistry.codes | Where-Object { [string]$_.code -eq [string]$transitionRow.reasonCode }).Count -ne 1) { Add-Error "route decision reasonCode is not registered: $($transitionRow.reasonCode)" }
}
if ([int]$snapshotContract.schemaVersion -ne 1 -or [string]$snapshotContract.normalizationVersion -ne 'path-sha256-v1') { Add-Error 'snapshot contract must declare schemaVersion=1 and path-sha256-v1' }

$transitionExceptions = @($transitionContract.transitionExceptions)
$transitionIds = @($transitionExceptions | ForEach-Object { [string]$_.id })
if (@($transitionIds | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -gt 0) { Add-Error 'reason transition IDs must be non-empty' }
if (@($transitionIds | Sort-Object -Unique).Count -ne $transitionIds.Count) { Add-Error 'reason transition IDs must be unique' }
$validRoutes = @('core','extension','blocked')
$validEvidence = @('unknown','pending','partial','closed','stale','runtime-not-run')
$validEffects = @('hard-block','claim-cap','review','stop-next-read')
$effectRank = @{ 'review' = 1; 'claim-cap' = 2; 'stop-next-read' = 3; 'hard-block' = 4 }
foreach ($transition in $transitionExceptions) {
    foreach ($name in @('id','reasonCode','scopePattern','from','to','override','alternativePath','recovery','rollback')) { if (-not (Has-Property $transition $name)) { Add-Error "transition is missing $name" } }
    if ([string]$transition.id -notmatch '^[A-Z0-9_.-]+$') { Add-Error "transition ID has invalid format: $($transition.id)" }
    if (@($transition.profileIds).Count -eq 0) { Add-Error "transition has no profileIds: $($transition.id)" }
    if ([string]$transition.scopePattern -notmatch '^\^[^*]+\$$') { Add-Error "transition scopePattern must be anchored and bounded: $($transition.id)" }
    try { [regex]::new([string]$transition.scopePattern) | Out-Null } catch { Add-Error "transition scopePattern is invalid: $($transition.id)" }
    foreach ($state in @($transition.from,$transition.to)) {
        if ($validRoutes -notcontains [string]$state.routeState) { Add-Error "transition has invalid routeState: $($transition.id)" }
        if ($validEvidence -notcontains [string]$state.evidenceState) { Add-Error "transition has invalid evidenceState: $($transition.id)" }
        if ($validEffects -notcontains [string]$state.effect) { Add-Error "transition has invalid effect: $($transition.id)" }
    }
    if (Test-StateEqual $transition.from $transition.to) { Add-Error "transition cannot be a no-op: $($transition.id)" }
    if ([string]$transition.to.effect -eq 'hard-block' -and [string]$transition.to.routeState -ne 'blocked') { Add-Error "hard-block transition must target blocked route: $($transition.id)" }
    foreach ($name in @('fromEffect','toEffect','profile','predicate')) { if (-not (Has-Property $transition.override $name)) { Add-Error "transition override is missing ${name}: $($transition.id)" } }
    if ($null -eq $transition.override.requiredEvidenceRefs -or @($transition.override.requiredEvidenceRefs).Count -eq 0) { Add-Error "transition override requires evidence refs: $($transition.id)" }
    if ($effectRank[[string]$transition.override.toEffect] -lt $effectRank[[string]$transition.override.fromEffect]) { Add-Error "effect override may not weaken its source effect: $($transition.id)" }
    if (@($reasonRegistry.codes | Where-Object { [string]$_.code -eq [string]$transition.reasonCode }).Count -ne 1) { Add-Error "transition reasonCode is not registered: $($transition.id)" }
    foreach ($profileId in @($transition.profileIds)) {
        if (@($profileScopeRegistry.profiles | Where-Object { [string]$_.id -eq [string]$profileId }).Count -ne 1) { Add-Error "transition profile is not registered: $($transition.id)/$profileId" }
    }
    $overrideRegistration = @($effectOverrideRegistry.allowedTransitions | Where-Object { [string]$_.id -eq [string]$transition.id })
    if ($overrideRegistration.Count -ne 1) { Add-Error "transition override is not registered: $($transition.id)" }
    elseif ([string]$overrideRegistration[0].fromEffect -ne [string]$transition.override.fromEffect -or [string]$overrideRegistration[0].toEffect -ne [string]$transition.override.toEffect -or [string]$overrideRegistration[0].profile -ne [string]$transition.override.profile -or [string]$overrideRegistration[0].scopePattern -ne [string]$transition.scopePattern) { Add-Error "transition override registration mismatch: $($transition.id)" }
}
for ($i = 0; $i -lt $transitionExceptions.Count; $i++) {
    for ($j = $i + 1; $j -lt $transitionExceptions.Count; $j++) {
        $left = $transitionExceptions[$i]; $right = $transitionExceptions[$j]
        $profileOverlap = @($left.profileIds | Where-Object { @($right.profileIds) -contains [string]$_ }).Count -gt 0
        if ([string]$left.reasonCode -eq [string]$right.reasonCode -and $profileOverlap -and [string]$left.scopePattern -eq [string]$right.scopePattern -and (Test-StateEqual $left.from $right.to) -and (Test-StateEqual $left.to $right.from)) {
            Add-Error "reverse transitions are forbidden: $($left.id) and $($right.id)"
        }
    }
}

foreach ($field in @('decisionId','object','field','profile','scope','scopeKind','routeState','evidenceState','effect','reasonCode','predicate','evidence','alternativePath','recovery','rollback','authorization','snapshot')) {
    Require-Text $decision $field
}
if ([string]$decision.decisionId -notmatch '^decision-[0-9a-fA-F]{24,64}$') { Add-Error 'decisionId must be decision- followed by 24-64 hexadecimal characters' }
foreach ($field in @('decisionId','object','field','profile','scope','routeState','evidenceState','effect','reasonCode','predicate','alternativePath','recovery','rollback')) {
    if (-not (Has-Property $decision $field)) { continue }
}

$routeStates = @('core','extension','blocked')
$evidenceStates = @('unknown','pending','partial','closed','stale','runtime-not-run')
$effects = @('hard-block','claim-cap','review','stop-next-read')
if ($routeStates -notcontains [string]$decision.routeState) { Add-Error "invalid routeState: $($decision.routeState)" }
if ($evidenceStates -notcontains [string]$decision.evidenceState) { Add-Error "invalid evidenceState: $($decision.evidenceState)" }
if ($effects -notcontains [string]$decision.effect) { Add-Error "invalid effect: $($decision.effect)" }
if ($null -eq $decision.evidence -or $decision.evidence -isnot [Array]) { Add-Error 'evidence must be an array' }

$profileEntry = @($profileScopeRegistry.profiles | Where-Object { [string]$_.id -ceq [string]$decision.profile })
if ($profileEntry.Count -ne 1) {
    Add-Error "profile must be an exact registered Profile ID: $($decision.profile)"
} else {
    if (@($profileEntry[0].scopeKinds | ForEach-Object { [string]$_ }) -notcontains [string]$decision.scopeKind) { Add-Error "scopeKind is not allowed for profile $($decision.profile): $($decision.scopeKind)" }
    $scopeMatches = @($profileEntry[0].scopePatterns | Where-Object { [string]$decision.scope -cmatch [string]$_ })
    if ($scopeMatches.Count -ne 1) { Add-Error "scope syntax is not an exact match for profile $($decision.profile): $($decision.scope)" }
    if ([string]$decision.scopeKind -eq 'project-global' -and [string]$decision.scope -ne 'project:global') { Add-Error 'project-global scope must be exactly project:global' }
    if ([string]$decision.scope -match '^project:' -and [string]$decision.scope -notmatch '^project:(object:[A-Za-z0-9][A-Za-z0-9._/-]*|global)$') { Add-Error 'project scope must distinguish project-object from project-global' }
}

if (-not (Has-Property $decision.authorization 'mode') -or @('CurrentUserDirect','ManagedAIBrain') -notcontains [string]$decision.authorization.mode) { Add-Error 'authorization.mode must be CurrentUserDirect or ManagedAIBrain' }
foreach ($field in @('requestedAction','requestedScope')) { Require-Text $decision.authorization $field }
foreach ($field in @('head','sourceRefs','sourceRefsHash','registryHash','coverage')) { if (-not (Has-Property $decision.snapshot $field)) { Add-Error "snapshot missing $field" } }
if (Has-Property $decision.snapshot 'head') {
    if (([string]$decision.snapshot.head) -notmatch '^[0-9a-fA-F]{7,64}$') { Add-Error 'snapshot.head must be a 7-64 character hexadecimal Git SHA' }
}
if (Has-Property $decision.snapshot 'coverage') {
    foreach ($pair in @(@('normalizationVersion','path-sha256-v1'), @('sourceRefsHash','sorted unique canonical JSON of {path,sha256}'), @('registryHash','sorted canonical JSON of {path,sha256} for registryFiles'), @('head','current Git commit SHA'))) {
        if ([string]$decision.snapshot.coverage.($pair[0]) -ne [string]$pair[1]) { Add-Error "snapshot coverage mismatch: $($pair[0])" }
    }
}
if (([string]$decision.decisionId) -match '^decision-[0-9a-fA-F]{24,64}$' -and (Has-Property $decision.snapshot 'head') -and (Has-Property $decision.snapshot 'sourceRefsHash') -and (Has-Property $decision.snapshot 'registryHash')) {
    $expectedDecisionId = 'decision-' + (Get-DecisionFingerprint $decision).Substring(0, 24)
    if (-not [string]::Equals([string]$decision.decisionId, $expectedDecisionId, [StringComparison]::OrdinalIgnoreCase)) { Add-Error 'decisionId is not bound to the decision fields and snapshot hashes' }
}
if (Has-Property $decision.snapshot 'sourceRefs' -and $decision.snapshot.sourceRefs -is [Array]) {
    $normalizedRefs = @()
    foreach ($ref in @($decision.snapshot.sourceRefs)) {
        if (-not (Has-Property $ref 'path') -or -not (Has-Property $ref 'sha256')) { Add-Error 'snapshot sourceRefs entries require path and sha256'; continue }
        $relative = Normalize-Relative ([string]$ref.path)
        if ([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|/)\.\.(?:/|$)') { Add-Error "snapshot SourceRef must be project-relative: $relative"; continue }
        $sourceFull = Resolve-ProjectRelativePath $root $relative 'snapshot.sourceRefs.path'
        if (-not (Test-Path -LiteralPath $sourceFull -PathType Leaf)) { Add-Error "snapshot SourceRef missing: $relative"; continue }
        $actual = Get-Sha256 $sourceFull
        if ($actual -ne ([string]$ref.sha256).ToLowerInvariant()) { Add-Error "snapshot SourceRef hash mismatch: $relative" }
        $normalizedRefs += [pscustomobject][ordered]@{ path = $relative; sha256 = ([string]$ref.sha256).ToLowerInvariant() }
    }
    $canonicalRefs = @($normalizedRefs | Sort-Object path -Unique)
    $computedSourceRefsHash = Get-CanonicalHash $canonicalRefs
    Write-Verbose "snapshot sourceRefsHash computed=$computedSourceRefsHash expected=$([string]$decision.snapshot.sourceRefsHash) count=$($canonicalRefs.Count)"
    if ($computedSourceRefsHash -ne ([string]$decision.snapshot.sourceRefsHash).ToLowerInvariant()) { Add-Error 'snapshot.sourceRefsHash does not match normalized SourceRefs' }
    if ($canonicalRefs.Count -ne @($decision.snapshot.sourceRefs).Count) { Add-Error 'snapshot SourceRefs must be unique after normalization' }
}
$headOutput = (& git -C $root rev-parse HEAD 2>$null | Select-Object -First 1)
if ($LASTEXITCODE -eq 0 -and [string]$headOutput -and [string]$decision.snapshot.head -notlike "$headOutput*") { Add-Error 'snapshot.head is not bound to current Git HEAD' }
$registryRelativePaths = @($snapshotContract.registryFiles | ForEach-Object { Normalize-Relative ([string]$_) })
$registryMaterial = @()
foreach ($registryRelative in $registryRelativePaths) {
    $registryPath = Resolve-ProjectRelativePath $root $registryRelative 'snapshot.registryFiles'
    if (-not (Test-Path -LiteralPath $registryPath -PathType Leaf)) { Add-Error "snapshot registry file missing: $registryRelative"; continue }
    $registryMaterial += [pscustomobject][ordered]@{ path = $registryRelative; sha256 = Get-Sha256 $registryPath }
}
$computedRegistryHash = Get-CanonicalHash @($registryMaterial | Sort-Object path)
Write-Verbose "snapshot registryHash computed=$computedRegistryHash expected=$([string]$decision.snapshot.registryHash) count=$($registryMaterial.Count)"
if ($computedRegistryHash -ne ([string]$decision.snapshot.registryHash).ToLowerInvariant()) { Add-Error 'snapshot.registryHash does not match current reason/Profile/Skill registry content' }

$codes = @($reasonRegistry.codes)
$code = [string]$decision.reasonCode
$reason = @($codes | Where-Object { [string]$_.code -eq $code })[0]
if ($null -eq $reason) {
    Add-Error "unknown reasonCode: $code"
} else {
    $registeredTransitions = @($transitionContract.transitionExceptions | Where-Object {
        [string]$_.reasonCode -eq $code -and
        @($_.profileIds) -contains [string]$decision.profile -and
        [string]$decision.scope -match [string]$_.scopePattern -and
        [string]$_.from.routeState -eq [string]$reason.defaultRouteState -and
        [string]$_.from.evidenceState -eq [string]$reason.defaultEvidenceState -and
        [string]$_.from.effect -eq [string]$reason.defaultEffect -and
        [string]$_.to.routeState -eq [string]$decision.routeState -and
        [string]$_.to.evidenceState -eq [string]$decision.evidenceState -and
        [string]$_.to.effect -eq [string]$decision.effect
    })
    $stateMatchesDefault = [string]$decision.routeState -eq [string]$reason.defaultRouteState -and [string]$decision.evidenceState -eq [string]$reason.defaultEvidenceState -and [string]$decision.effect -eq [string]$reason.defaultEffect
    if (-not $stateMatchesDefault -and $registeredTransitions.Count -eq 0) { Add-Error "routeState/evidenceState/effect must match reasonCode defaults or a registered transition: $code" }
    if ($registeredTransitions.Count -gt 1) { Add-Error "multiple reason transitions match the same decision: $code" }
    if (-not [bool]$reason.projectBlockAllowed -and [string]$decision.scopeKind -eq 'project-global') {
        Add-Error "reasonCode does not permit project-global scope: $code"
    }

    $hasEffectOverride = (Has-Property $decision 'effectOverride') -and $null -ne $decision.effectOverride
    if (-not $stateMatchesDefault) {
        if (-not $hasEffectOverride) {
            Add-Error "state deviation requires a registered directional effectOverride: $code"
        } else {
            $override = $decision.effectOverride
            foreach ($name in @('fromEffect','toEffect','profile','predicate','justification')) { Require-Text $override $name }
            if ($null -eq $override.evidenceRefs -or $override.evidenceRefs -isnot [Array] -or @($override.evidenceRefs).Count -eq 0) { Add-Error 'effectOverride.evidenceRefs must contain evidence references' }
            if ($registeredTransitions.Count -gt 0) {
                $transition = $registeredTransitions[0]
                if ([string]$override.fromEffect -ne [string]$transition.override.fromEffect -or [string]$override.toEffect -ne [string]$transition.override.toEffect -or [string]$override.profile -ne [string]$transition.override.profile -or [string]$override.predicate -ne [string]$transition.override.predicate) { Add-Error "effectOverride direction/profile/predicate does not match the registered transition: $code" }
                $overrideRegistration = @($effectOverrideRegistry.allowedTransitions | Where-Object { [string]$_.id -eq [string]$transition.id -and [string]$_.profile -ceq [string]$override.profile -and [string]$decision.scope -cmatch [string]$_.scopePattern -and [string]$_.fromEffect -eq [string]$override.fromEffect -and [string]$_.toEffect -eq [string]$override.toEffect })
                if ($overrideRegistration.Count -ne 1) { Add-Error "effectOverride is not allowlisted for the exact Profile/scope/direction: $code" }
                if ($effectRank[[string]$override.toEffect] -lt $effectRank[[string]$override.fromEffect]) { Add-Error "effectOverride may not weaken the source effect: $code" }
                foreach ($requiredRef in @($transition.override.requiredEvidenceRefs)) {
                    if (@($override.evidenceRefs | ForEach-Object { [string]$_ }) -notcontains [string]$requiredRef) { Add-Error "effectOverride is missing registered evidenceRef: $requiredRef" }
                }
            }
        }
    } elseif ($hasEffectOverride) {
        Add-Error "effectOverride is forbidden unless a registered transition changes the decision state: $code"
    }
}

if ([string]$decision.routeState -eq 'blocked' -and [string]$decision.effect -ne 'hard-block') { Add-Error 'routeState=blocked requires effect=hard-block' }
if ([string]$decision.effect -eq 'hard-block' -and [string]$decision.routeState -ne 'blocked') { Add-Error 'effect=hard-block requires routeState=blocked' }
if ([string]$code -eq 'RUNTIME.NOT_RUN') {
    if ([string]$decision.evidenceState -ne 'runtime-not-run') { Add-Error 'RUNTIME.NOT_RUN requires evidenceState=runtime-not-run' }
    if ([string]$decision.effect -ne 'claim-cap') { Add-Error 'RUNTIME.NOT_RUN may only produce claim-cap' }
    if ([string]$decision.routeState -eq 'blocked') { Add-Error 'RUNTIME.NOT_RUN cannot produce project or route blocked' }
}
if ([string]$code -eq 'CAPABILITY.OPTIONAL_UNAVAILABLE' -and [string]$decision.effect -eq 'hard-block') { Add-Error 'optional capability absence cannot produce hard-block' }
if ([string]$code -eq 'SOURCE.STALE_OPTIONAL' -and [string]$decision.effect -eq 'hard-block') { Add-Error 'optional stale source cannot produce hard-block' }
if ([string]$code -eq 'BUDGET.NEXT_READ_EXCEEDED' -and [string]$decision.effect -ne 'stop-next-read') { Add-Error 'BUDGET.NEXT_READ_EXCEEDED requires stop-next-read' }
if ([string]$code -eq 'BUDGET.NEXT_READ_EXCEEDED' -and [string]$decision.routeState -eq 'blocked') { Add-Error 'budget overflow stops the next read; it does not block the route' }
if ([string]$code -eq 'CONTRACT.INVALID_SCOPE') {
    if ([string]$decision.profile -notmatch '(?i)contract') { Add-Error 'CONTRACT.INVALID_SCOPE is limited to a contract validation Profile' }
    if ([string]$decision.scopeKind -in @('project-object','project-global')) { Add-Error 'CONTRACT.INVALID_SCOPE cannot use project scope' }
}

$depth = if (Has-Property $decision 'routeDepth') { [int]$decision.routeDepth } else { 0 }
if ($depth -lt 0 -or $depth -gt [int]$depthRegistry.maxDepth) { Add-Error "routeDepth must be between 0 and $($depthRegistry.maxDepth): $depth" }
if ($depth -gt 0 -and [string]$decision.routeState -eq 'core') { Add-Error 'extension depth requires routeState=extension' }
if ($depth -eq 2) {
    $depthCode = [string]$decision.depthReasonCode
    $depthEntry = @($depthRegistry.entries | Where-Object { [string]$_.reasonCode -ceq $depthCode })
    $depthReason = @($codes | Where-Object { [string]$_.code -ceq $depthCode })
    if ($depthEntry.Count -ne 1 -or $depthReason.Count -ne 1) { Add-Error 'routeDepth=2 requires a depthReasonCode present in both registries' }
    else {
        if ([int]$depthEntry[0].authorizesDepth -ne 2 -or [int]$depthReason[0].authorizesDepth -ne 2) { Add-Error 'depthReasonCode does not authorize depth 2' }
        if (@($depthEntry[0].allowedRouteStates) -notcontains [string]$decision.routeState -or @($depthReason[0].allowedRouteStates) -notcontains [string]$decision.routeState) { Add-Error 'depthReasonCode does not authorize the current route state' }
        if (@($depthEntry[0].allowedProfiles) -notcontains [string]$decision.profile -or @($depthReason[0].allowedProfiles) -notcontains [string]$decision.profile) { Add-Error 'depthReasonCode does not authorize the current Profile' }
    }
} elseif (Has-Property $decision 'depthReasonCode') {
    if ($null -ne $decision.depthReasonCode -and -not [string]::IsNullOrWhiteSpace([string]$decision.depthReasonCode)) { Add-Error 'depthReasonCode is only valid when routeDepth=2' }
}

if ([string]$decision.authorization.mode -eq 'CurrentUserDirect' -and -not [string]::Equals([string]$decision.scope, [string]$decision.authorization.requestedScope, [StringComparison]::Ordinal)) {
    Add-Error 'CurrentUserDirect requested scope was narrowed by contract or Skill metadata'
}
if ([string]$decision.authorization.mode -eq 'ManagedAIBrain' -and -not [string]::Equals([string]$decision.scope, [string]$decision.authorization.requestedScope, [StringComparison]::Ordinal)) {
    Add-Error 'ManagedAIBrain decision scope must equal its invocation-bound requestedScope'
}

if ([string]$decision.effect -eq 'hard-block') {
    foreach ($field in @('alternativePath','recovery','rollback')) { Require-Text $decision $field }
}
if (Has-Property $decision 'effectOverride' -and $null -ne $decision.effectOverride) {
    Require-Text $decision.effectOverride 'justification'
}

if ($errors.Count -gt 0) {
    throw "ES-GOV-DECISION-002: $($errors -join '; ')"
}

[pscustomobject][ordered]@{
    validator = 'Test-ESGovernanceDecisionScope'
    decision = $DecisionPath
    decisionStatus = 'Accepted'
    acceptanceScope = [ordered]@{ object = [string]$decision.object; field = [string]$decision.field; profile = [string]$decision.profile; scope = [string]$decision.scope; scopeKind = [string]$decision.scopeKind }
    routeState = [string]$decision.routeState
    evidenceState = [string]$decision.evidenceState
    effect = [string]$decision.effect
    reasonCode = [string]$decision.reasonCode
    routeDepth = $depth
    p0Status = 'not-evaluated'
    runtimeStatus = 'runtime-not-run'
    evidenceLevel = 'S1-static'
}
