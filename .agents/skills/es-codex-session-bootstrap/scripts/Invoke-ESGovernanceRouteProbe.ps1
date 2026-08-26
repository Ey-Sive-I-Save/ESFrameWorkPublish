[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [Parameter(Mandatory = $true)][string]$DecisionPath,
    [string]$ReasonCodesPath = '.agents/skills/es-codex-session-bootstrap/references/governance-reason-codes.json',
    [string]$ProfileScopeRegistryPath = '.agents/skills/es-codex-session-bootstrap/references/governance-profile-scope.registry.json',
    [string]$DepthRegistryPath = '.agents/skills/es-codex-session-bootstrap/references/governance-depth.registry.json',
    [string]$EffectOverrideRegistryPath = '.agents/skills/es-codex-session-bootstrap/references/governance-effect-override.registry.json',
    [string]$RouteTablePath = '.agents/skills/es-codex-session-bootstrap/references/governance-route-decision-table.json',
    [string]$TransitionPath = '.agents/skills/es-codex-session-bootstrap/references/governance-reason-transition.contract.json'
)
$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)

function Resolve-ProjectRelative([string]$Root, [string]$Path, [string]$Label) {
    if ([IO.Path]::IsPathRooted($Path)) { throw "ES-GOV-PROBE-003: $Label must be project-relative" }
    $full = [IO.Path]::GetFullPath((Join-Path $Root $Path))
    $prefix = $Root.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw "ES-GOV-PROBE-003: $Label escapes ProjectRoot" }
    return $full
}
function Resolve-Input([string]$Root, [string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    return Resolve-ProjectRelative $Root $Path 'DecisionPath'
}
function Read-StrictJson([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "ES-GOV-PROBE-001: $Label not found: $Path" }
    try { return [IO.File]::ReadAllText((Resolve-Path -LiteralPath $Path).Path, [Text.UTF8Encoding]::new($false, $true)) | ConvertFrom-Json } catch { throw "ES-GOV-PROBE-001: $Label is not strict UTF-8 JSON" }
}
function Has-Property($Object, [string]$Name) { return $null -ne $Object -and $null -ne $Object.PSObject.Properties[$Name] }
function Get-Sha256([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Same-State($Left, $Right) { return [string]$Left.routeState -eq [string]$Right.routeState -and [string]$Left.evidenceState -eq [string]$Right.evidenceState -and [string]$Left.effect -eq [string]$Right.effect }
function StateObject($Route, $Evidence, $Effect) { return [pscustomobject][ordered]@{ routeState = $Route; evidenceState = $Evidence; effect = $Effect } }

$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
$decisionInput = Read-StrictJson (Resolve-Input $root $DecisionPath) 'Decision probe input'
$registryDefinitions = @(
    [pscustomobject][ordered]@{ path = $ReasonCodesPath; purpose = 'reason defaults' },
    [pscustomobject][ordered]@{ path = $ProfileScopeRegistryPath; purpose = 'exact Profile and scope syntax' },
    [pscustomobject][ordered]@{ path = $DepthRegistryPath; purpose = 'depth-2 authorization' },
    [pscustomobject][ordered]@{ path = $EffectOverrideRegistryPath; purpose = 'directional effect allowlist' },
    [pscustomobject][ordered]@{ path = $RouteTablePath; purpose = 'route transitions and precedence' },
    [pscustomobject][ordered]@{ path = $TransitionPath; purpose = 'registered state transitions' }
)
$registryReads = @()
foreach ($definition in $registryDefinitions) {
    $relative = ([string]$definition.path).Replace('\','/').Trim()
    $full = Resolve-ProjectRelative $root $relative 'registry path'
    $value = Read-StrictJson $full $definition.purpose
    switch ([string]$definition.purpose) {
        'reason defaults' { $reasonRegistry = $value }
        'exact Profile and scope syntax' { $profileRegistry = $value }
        'depth-2 authorization' { $depthRegistry = $value }
        'directional effect allowlist' { $effectRegistry = $value }
        'route transitions and precedence' { $routeTable = $value }
        'registered state transitions' { $transitionContract = $value }
    }
    $registryReads += [pscustomobject][ordered]@{ path = $relative; sha256 = Get-Sha256 $full; purpose = [string]$definition.purpose }
}
$errors = [Collections.Generic.List[string]]::new()
$reason = @($reasonRegistry.codes | Where-Object { [string]$_.code -ceq [string]$decisionInput.reasonCode })
if ($reason.Count -ne 1) { $errors.Add("unknown reasonCode: $($decisionInput.reasonCode)") }
$profile = @($profileRegistry.profiles | Where-Object { [string]$_.id -ceq [string]$decisionInput.profile })
if ($profile.Count -ne 1) { $errors.Add("unknown exact Profile: $($decisionInput.profile)") }
if ($profile.Count -eq 1) {
    if (@($profile[0].scopeKinds) -notcontains [string]$decisionInput.scopeKind) { $errors.Add('scopeKind is not allowed for the exact Profile') }
    if (@($profile[0].scopePatterns | Where-Object { [string]$decisionInput.scope -cmatch [string]$_ }).Count -ne 1) { $errors.Add('scope syntax is not registered for the exact Profile') }
}
$defaults = if ($reason.Count -eq 1) { StateObject $reason[0].defaultRouteState $reason[0].defaultEvidenceState $reason[0].defaultEffect } else { StateObject $null $null $null }
$actual = StateObject $decisionInput.routeState $decisionInput.evidenceState $decisionInput.effect
$stateChanged = $reason.Count -eq 1 -and -not (Same-State $defaults $actual)
$matchingTransition = @()
if ($reason.Count -eq 1 -and $profile.Count -eq 1) {
    $matchingTransition = @($transitionContract.transitionExceptions | Where-Object {
        [string]$_.reasonCode -ceq [string]$decisionInput.reasonCode -and @($_.profileIds) -contains [string]$decisionInput.profile -and [string]$decisionInput.scope -cmatch [string]$_.scopePattern -and
        (Same-State $_.from $defaults) -and (Same-State $_.to $actual)
    })
}
if ($stateChanged -and $matchingTransition.Count -ne 1) { $errors.Add('state deviation has no unique registered transition') }
if (-not $stateChanged -and (Has-Property $decisionInput 'effectOverride') -and $null -ne $decisionInput.effectOverride) { $errors.Add('effectOverride is present without a state transition') }
if ($stateChanged -and $matchingTransition.Count -eq 1) {
    $transition = $matchingTransition[0]
    $override = if (Has-Property $decisionInput 'effectOverride') { $decisionInput.effectOverride } else { $null }
    if ($null -eq $override) { $errors.Add('state deviation is missing directional effectOverride') }
    else {
        $allow = @($effectRegistry.allowedTransitions | Where-Object { [string]$_.id -ceq [string]$transition.id -and [string]$_.profile -ceq [string]$override.profile -and [string]$decisionInput.scope -cmatch [string]$_.scopePattern -and [string]$_.fromEffect -ceq [string]$override.fromEffect -and [string]$_.toEffect -ceq [string]$override.toEffect })
        if ($allow.Count -ne 1) { $errors.Add('effectOverride is not registered for the exact transition/Profile/scope/direction') }
    }
}
$depth = if (Has-Property $decisionInput 'routeDepth') { [int]$decisionInput.routeDepth } else { 0 }
if ($depth -gt [int]$depthRegistry.maxDepth) { $errors.Add('routeDepth exceeds the registered maximum') }
if ($depth -eq 2) {
    $depthEntry = @($depthRegistry.entries | Where-Object { [string]$_.reasonCode -ceq [string]$decisionInput.depthReasonCode })
    if ($depthEntry.Count -ne 1 -or [int]$depthEntry[0].authorizesDepth -ne 2 -or @($depthEntry[0].allowedRouteStates) -notcontains [string]$decisionInput.routeState -or @($depthEntry[0].allowedProfiles) -notcontains [string]$decisionInput.profile) { $errors.Add('depth-2 input is not authorized by the depth registry') }
}
if ([string]$decisionInput.scopeKind -eq 'project-global' -and $reason.Count -eq 1 -and -not [bool]$reason[0].projectBlockAllowed) { $errors.Add('reason code cannot project to project-global scope') }
if ([string]$decisionInput.routeState -eq 'blocked' -and [string]$decisionInput.effect -ne 'hard-block') { $errors.Add('blocked route requires hard-block effect') }
if ([string]$decisionInput.effect -eq 'hard-block' -and [string]$decisionInput.routeState -ne 'blocked') { $errors.Add('hard-block effect requires blocked route') }
$decisionStatus = if ($errors.Count -eq 0) { 'Accepted' } else { 'Rejected' }
[pscustomobject][ordered]@{
    probe = 'Invoke-ESGovernanceRouteProbe'
    probeStatus = 'ReadOnly'
    decisionStatus = $decisionStatus
    productionRouteIntegrated = $false
    globalP0Integrated = $false
    registriesRead = @($registryReads)
    decision = [ordered]@{
        decisionId = [string]$decisionInput.decisionId
        object = [string]$decisionInput.object
        field = [string]$decisionInput.field
        profile = [string]$decisionInput.profile
        scope = [string]$decisionInput.scope
        scopeKind = [string]$decisionInput.scopeKind
        reasonCode = [string]$decisionInput.reasonCode
        defaultState = $defaults
        actualState = $actual
        stateChanged = $stateChanged
        changedBy = if ($matchingTransition.Count -eq 1) { [string]$matchingTransition[0].id } else { $null }
        routeDecision = [string]$decisionInput.routeState
        effectDecision = [string]$decisionInput.effect
    }
    errors = @($errors)
    runtimeStatus = 'runtime-not-run'
}
