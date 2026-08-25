[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [Parameter(Mandatory = $true)][string[]]$AuthorizedPath,
    [Parameter(Mandatory = $true)][string[]]$Path,
    [ValidateSet(
        'create','modify','delete','rename',
        'git-stage','git-commit','git-push','git-reset','git-rebase','git-checkout','git-clean',
        'unity-runtime','external-process','network','release','credential-access'
    )][string]$Operation = 'modify',
    [ValidateSet('Exact','Subtree')][string]$ScopeMode = 'Exact',
    [switch]$ExplicitUserInstruction,
    [switch]$ExplicitAction,
    [switch]$InferredScopeExpansion,
    [switch]$AsObject
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-IsWithin([string]$Candidate, [string]$Container) {
    return [string]::Equals($Candidate, $Container, [StringComparison]::OrdinalIgnoreCase) -or
        $Candidate.StartsWith($Container + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}

function Resolve-ContainedPath([string]$Candidate, [string]$Root) {
    if ([string]::IsNullOrWhiteSpace($Candidate)) { throw 'path is empty' }
    $full = if ([IO.Path]::IsPathRooted($Candidate)) {
        [IO.Path]::GetFullPath($Candidate)
    } else {
        [IO.Path]::GetFullPath([IO.Path]::Combine($Root, $Candidate))
    }
    if (-not (Test-IsWithin $full $Root)) { throw "path escapes ProjectRoot: $Candidate" }

    $probe = $full
    while (-not (Test-Path -LiteralPath $probe) -and -not [string]::Equals($probe, $Root, [StringComparison]::OrdinalIgnoreCase)) {
        $parent = [IO.Path]::GetDirectoryName($probe)
        if ([string]::IsNullOrWhiteSpace($parent) -or [string]::Equals($parent, $probe, [StringComparison]::OrdinalIgnoreCase)) { break }
        $probe = $parent
    }
    if (Test-Path -LiteralPath $probe) {
        $resolvedProbe = (Resolve-Path -LiteralPath $probe).ProviderPath
        $suffix = $full.Substring($probe.Length).TrimStart('\','/')
        $resolvedFull = if ([string]::IsNullOrEmpty($suffix)) { $resolvedProbe } else { [IO.Path]::GetFullPath([IO.Path]::Combine($resolvedProbe, $suffix)) }
        if (-not (Test-IsWithin $resolvedFull $Root)) { throw "resolved path escapes ProjectRoot: $Candidate" }
    }

    $relative = if ([string]::Equals($full, $Root, [StringComparison]::OrdinalIgnoreCase)) { '.' } else { $full.Substring($Root.Length + 1).Replace('\','/') }
    return [pscustomobject]@{ Input = $Candidate; FullPath = $full; RelativePath = $relative }
}

function Test-Glob([string]$Value, [object[]]$Patterns) {
    foreach ($pattern in @($Patterns)) {
        if ($Value -like ([string]$pattern)) { return $true }
    }
    return $false
}

function Get-PathClasses([string]$Value, $Policy) {
    $classes = New-Object 'System.Collections.Generic.List[string]'
    foreach ($category in $Policy.nonDenyingPathClasses.psobject.Properties) {
        foreach ($marker in @($category.Value)) {
            $pattern = [string]$marker
            $matched = if ($pattern.Contains('*')) { $Value -like $pattern } else { $Value -like ('*' + $pattern + '*') }
            if ($matched) { [void]$classes.Add([string]$category.Name); break }
        }
    }
    return @($classes | Select-Object -Unique)
}

$root = (Resolve-Path -LiteralPath $ProjectRoot).ProviderPath
while ($root.Length -gt 3 -and ($root.EndsWith('\') -or $root.EndsWith('/'))) { $root = $root.Substring(0, $root.Length - 1) }
$policyPath = Join-Path $root '.agents/skills/es-skill-governance/references/user-directed-low-risk-policy.json'
if (-not (Test-Path -LiteralPath $policyPath -PathType Leaf)) { throw 'User-directed action policy is missing.' }
$policy = Get-Content -LiteralPath $policyPath -Raw -Encoding UTF8 | ConvertFrom-Json

$reasons = New-Object 'System.Collections.Generic.List[string]'
$reviewSignals = New-Object 'System.Collections.Generic.List[string]'
if (-not [bool]$policy.enabled) { [void]$reasons.Add('policy is disabled') }
if ([bool]$policy.requiresExplicitUserInstruction -and -not $ExplicitUserInstruction) { [void]$reasons.Add('missing-current-explicit-user-instruction') }
if ($InferredScopeExpansion) { [void]$reasons.Add('ai-inferred-scope-expansion') }

$authorized = New-Object 'System.Collections.Generic.List[object]'
foreach ($item in $AuthorizedPath) {
    try { [void]$authorized.Add((Resolve-ContainedPath ([string]$item) $root)) }
    catch { [void]$reasons.Add(('invalid-authorized-path: ' + $_.Exception.Message)) }
}

$actionSpecific = @($policy.actionSpecificOperations) -contains $Operation
if ($actionSpecific -and -not $ExplicitAction) { [void]$reasons.Add("action-specific-operation-not-explicitly-requested: $Operation") }

$records = New-Object 'System.Collections.Generic.List[object]'
$totalBytes = [long]0
foreach ($item in $Path) {
    try { $resolved = Resolve-ContainedPath ([string]$item) $root }
    catch { [void]$reasons.Add(('project-root-escape: ' + $_.Exception.Message)); continue }

    $scopeMatch = $false
    foreach ($allowed in $authorized) {
        if ($ScopeMode -eq 'Exact') {
            if ([string]::Equals($resolved.FullPath, $allowed.FullPath, [StringComparison]::OrdinalIgnoreCase)) { $scopeMatch = $true; break }
        } elseif (Test-IsWithin $resolved.FullPath $allowed.FullPath) { $scopeMatch = $true; break }
    }
    if (-not $scopeMatch) { [void]$reasons.Add("planned-target-outside-declared-user-scope: $($resolved.RelativePath)") }

    $exists = Test-Path -LiteralPath $resolved.FullPath -PathType Leaf
    if ($Operation -eq 'create' -and $exists) { [void]$reviewSignals.Add("create-target-already-exists: $($resolved.RelativePath)") }
    if ($Operation -eq 'modify' -and -not $exists) { [void]$reviewSignals.Add("modify-target-does-not-yet-exist: $($resolved.RelativePath)") }

    $credentialPath = Test-Glob $resolved.RelativePath $policy.actionSpecificPathGlobs.credentials
    if ($credentialPath -and -not $ExplicitAction) { [void]$reasons.Add("action-specific-operation-not-explicitly-requested: credentials: $($resolved.RelativePath)") }

    $bytes = if ($exists) { [long](Get-Item -LiteralPath $resolved.FullPath).Length } else { [long]0 }
    $totalBytes += $bytes
    [void]$records.Add([pscustomobject][ordered]@{
        input = [string]$item
        path = $resolved.RelativePath
        exists = $exists
        bytes = $bytes
        pathClasses = @(Get-PathClasses $resolved.RelativePath $policy)
        actionSpecific = [bool]$credentialPath
        scopeMatched = $scopeMatch
    })
}

if ($records.Count -gt [int]$policy.reviewThresholds.fileCount) { [void]$reviewSignals.Add('file-count-review-threshold-exceeded') }
if ($totalBytes -gt [long]$policy.reviewThresholds.existingInputBytes) { [void]$reviewSignals.Add('existing-input-bytes-review-threshold-exceeded') }

$status = if ($reasons.Count -eq 0) { 'allowed' } else { 'blocked' }
$result = [ordered]@{
    schemaVersion = 3
    validator = 'es-user-directed-action-authority'
    compatibilityName = 'Test-ESUserDirectedLowRiskPolicy.ps1'
    status = $status
    authority = [string]$policy.authority
    operation = $Operation
    scopeMode = $ScopeMode
    explicitUserInstruction = [bool]$ExplicitUserInstruction
    explicitAction = [bool]$ExplicitAction
    inferredScopeExpansion = [bool]$InferredScopeExpansion
    secondaryApprovalRequired = $false
    aibrainPlanRequired = $false
    aiCommandRequired = $false
    taskContractRequired = $false
    authorizedPaths = @($authorized | ForEach-Object { $_.RelativePath })
    files = $records.ToArray()
    totalBytes = $totalBytes
    reasons = $reasons.ToArray()
    reviewSignals = $reviewSignals.ToArray()
    resolution = if ($status -eq 'allowed') { 'proceed-with-quality-checks' } else { 'remove-inferred-expansion-or-obtain-current-user-clarification' }
}

if ($AsObject) { return [pscustomobject]$result }
$result | ConvertTo-Json -Depth 10
if ($status -ne 'allowed') { exit 1 }
