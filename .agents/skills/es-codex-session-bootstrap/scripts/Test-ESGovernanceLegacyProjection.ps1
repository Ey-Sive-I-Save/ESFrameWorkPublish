[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [Parameter(Mandatory = $true)][string]$ProjectionPath,
    [string]$LegacyMapPath = '.agents/skills/es-codex-session-bootstrap/references/governance-legacy-state-map.json',
    [string]$ProfileScopeRegistryPath = '.agents/skills/es-codex-session-bootstrap/references/governance-profile-scope.registry.json',
    [string]$SchemaPath = '.agents/skills/es-codex-session-bootstrap/references/governance-legacy-projection.schema.json'
)
$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
function Resolve-Relative([string]$Root, [string]$Path, [string]$Label) {
    if ([IO.Path]::IsPathRooted($Path)) { throw "ES-GOV-LEGACY-003: $Label must be project-relative" }
    $full = [IO.Path]::GetFullPath((Join-Path $Root $Path)); $prefix = $Root.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw "ES-GOV-LEGACY-003: $Label escapes ProjectRoot" }
    return $full
}
function Resolve-Input([string]$Root, [string]$Path, [string]$Label) {
    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    return Resolve-Relative $Root $Path $Label
}
function Read-Json([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "ES-GOV-LEGACY-001: $Label not found" }
    try { return [IO.File]::ReadAllText((Resolve-Path -LiteralPath $Path).Path, [Text.UTF8Encoding]::new($false, $true)) | ConvertFrom-Json } catch { throw "ES-GOV-LEGACY-001: $Label is not strict UTF-8 JSON" }
}
$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
$projection = Read-Json (Resolve-Input $root $ProjectionPath 'ProjectionPath') 'Projection'
$legacyMap = Read-Json (Resolve-Relative $root $LegacyMapPath 'LegacyMapPath') 'Legacy map'
$schema = Read-Json (Resolve-Relative $root $SchemaPath 'SchemaPath') 'Projection schema'
$profiles = Read-Json (Resolve-Relative $root $ProfileScopeRegistryPath 'ProfileScopeRegistryPath') 'Profile/scope registry'
$errors = [Collections.Generic.List[string]]::new()
if ([int]$projection.schemaVersion -ne 1 -or [int]$schema.schemaVersion -ne 1) { $errors.Add('projection and schema must use schemaVersion=1') }
$mapping = @($legacyMap.states)
$forbidden = @($legacyMap.forbiddenConversions | ForEach-Object { ([string]$_).Replace(' ','') })
$validKinds = @('project-object','project-global','task-object','context-object')
foreach ($entry in @($projection.entries)) {
    foreach ($name in @('legacyState','projectedState','source','object','field','profile','scope','scopeKind','routeState','evidenceState','effect','reasonCode')) {
        if ($null -eq $entry.PSObject.Properties[$name] -or [string]::IsNullOrWhiteSpace([string]$entry.$name)) { $errors.Add("entry missing $name") }
    }
    if ($null -eq $entry.source) { continue }
    foreach ($name in @('object','field','profile','scope','scopeKind')) {
        if ([string]$entry.$name -ne [string]$entry.source.$name) { $errors.Add("projection widened or lost source $name") }
    }
    if ($validKinds -notcontains [string]$entry.scopeKind) { $errors.Add("invalid scopeKind: $($entry.scopeKind)") }
    $profile = @($profiles.profiles | Where-Object { [string]$_.id -ceq [string]$entry.profile })
    if ($profile.Count -ne 1) { $errors.Add("profile is not exact and registered: $($entry.profile)") }
    else {
        if (@($profile[0].scopeKinds) -notcontains [string]$entry.scopeKind) { $errors.Add("scopeKind is not registered for profile: $($entry.profile)") }
        if (@($profile[0].scopePatterns | Where-Object { [string]$entry.scope -cmatch [string]$_ }).Count -ne 1) { $errors.Add("scope syntax is not registered for profile: $($entry.profile)") }
    }
    $expected = @($mapping | Where-Object { [string]$_.legacy -ceq [string]$entry.projectedState })
    if ($expected.Count -ne 1) { $errors.Add("unknown projected legacy state: $($entry.projectedState)") }
    else {
        if ([string]$entry.routeState -ne [string]$expected[0].routeState -or [string]$entry.evidenceState -ne [string]$expected[0].evidenceState) { $errors.Add("three-axis route/evidence state cannot be replayed for $($entry.projectedState)") }
        if ([string]$entry.effect -ne [string]$expected[0].defaultEffect) { $errors.Add("three-axis effect cannot be replayed for $($entry.projectedState)") }
    }
    $conversion = (([string]$entry.legacyState) + '->' + ([string]$entry.projectedState)).Replace(' ','')
    if ($forbidden -contains $conversion) { $errors.Add("forbidden legacy conversion: $conversion") }
    if ([string]$entry.projectedState -eq 'Accepted') {
        if ([string]$entry.evidenceState -ne 'closed' -or $null -eq $entry.evidenceRefs -or @($entry.evidenceRefs).Count -eq 0) { $errors.Add('Accepted projection requires closed evidence and evidenceRefs') }
        if ([string]$entry.scopeKind -eq 'project-global') { $errors.Add('Accepted projection cannot mean project-global acceptance') }
    }
    if ([string]$entry.scopeKind -eq 'project-global' -and [string]$entry.scope -ne 'project:global') { $errors.Add('project-global scope must remain exactly project:global') }
}
if ($errors.Count -gt 0) { throw "ES-GOV-LEGACY-002: $($errors -join '; ')" }
[pscustomobject][ordered]@{ validator = 'Test-ESGovernanceLegacyProjection'; projection = $ProjectionPath; projectionStatus = 'Accepted'; entryCount = @($projection.entries).Count; replay = 'routeState/evidenceState/effect'; runtimeStatus = 'runtime-not-run' }
