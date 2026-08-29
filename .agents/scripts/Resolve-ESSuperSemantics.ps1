[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PromptText,
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$SuppressionStatePath = '',
    [string]$ResponsibilityKey = 'semantic-routing-governance'
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)

if ([string]::IsNullOrWhiteSpace($PromptText)) { throw 'PromptText must not be empty.' }
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$indexPath = Join-Path $root '.agents\SUPER_SEMANTICS_REGISTRY.json'
$index = Get-Content -Raw -Encoding UTF8 -LiteralPath $indexPath | ConvertFrom-Json
$originalText = $PromptText
$cancelTokens = @($index.cancellation.tokens | ForEach-Object { [string]$_ })
$trimmedPrompt = $originalText.Trim()
if ([bool]$index.cancellation.exactTokenOnly -and ($cancelTokens -contains $trimmedPrompt)) {
    [ordered]@{
        schemaVersion = 1; registryId = [string]$index.registryId; status = 'cancelled'; isSuperSemantics = $false; isRegularRoute = $false
        displayLine = $null; selected = $null; candidates = @(); additionalMatches = @(); cancelled = $true
        cancellationToken = $trimmedPrompt; suppressionRounds = [int]$index.cancellation.defaultSuppressionRounds
        suppressionStatePath = $SuppressionStatePath; canExecute = $false; nonClaims = @($index.nonClaims)
    } | ConvertTo-Json -Depth 10
    exit 0
}
$maxChars = [int]$index.textSampling.shortTextMaxChars
$headChars = [int]$index.textSampling.longTextHeadChars
$tailChars = [int]$index.textSampling.longTextTailChars
if ($originalText.Length -le $maxChars) {
    $scanText = $originalText
    $scanMode = 'full-short-text'
} else {
    $head = $originalText.Substring(0, [Math]::Min($headChars, $originalText.Length))
    $tailStart = [Math]::Max(0, $originalText.Length - $tailChars)
    $tail = $originalText.Substring($tailStart)
    $scanText = "$head`n$([string]$index.textSampling.middleMarker)`n$tail"
    $scanMode = 'head-tail-long-text'
}
$matches = @()

foreach ($source in @($index.sources)) {
    $sourcePath = Join-Path $root ([string]$source)
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw "Super-semantics source missing: $source" }
    $registry = Get-Content -Raw -Encoding UTF8 -LiteralPath $sourcePath | ConvertFrom-Json
    foreach ($trigger in @($registry.triggers)) {
        foreach ($phrase in @($trigger.triggerPhrases)) {
            $phraseAllowed = $true
            foreach ($excludedPhrase in @($trigger.excludePhrases)) {
                if ([string]::IsNullOrWhiteSpace([string]$excludedPhrase)) { continue }
                if ($scanText.IndexOf([string]$excludedPhrase, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $phraseAllowed = $false; break }
            }
            if (-not $phraseAllowed) { continue }
            if ([string]$trigger.matchMode -eq 'contextual' -and [string]$phrase -eq '注意') {
                $trimmed = $originalText.Trim()
                $contextHit = ($trimmed -eq '注意')
                foreach ($contextPhrase in @($trigger.contextualPhrases)) {
                    if ($scanText.IndexOf([string]$contextPhrase, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $contextHit = $true; break }
                }
                if (-not $contextHit) { $phraseAllowed = $false }
            }
            if (-not $phraseAllowed) { continue }
            $matchIndex = $scanText.IndexOf([string]$phrase, [StringComparison]::OrdinalIgnoreCase)
            if ($matchIndex -ge 0) {
                $prefixStart = [Math]::Max(0, $matchIndex - 8)
                $prefix = $scanText.Substring($prefixStart, $matchIndex - $prefixStart)
                $negated = $false
                foreach ($marker in @($index.negationMarkers)) {
                    if ($prefix.Contains([string]$marker)) { $negated = $true; break }
                }
                if ($negated) { continue }
                $suffixStart = $matchIndex + ([string]$phrase).Length
                $suffix = if ($suffixStart -lt $scanText.Length) { $scanText.Substring($suffixStart, [Math]::Min(8, $scanText.Length - $suffixStart)) } else { '' }
                $comparisonBlocked = $false
                foreach ($comparisonMarker in @($index.comparisonMarkers)) {
                    if ($suffix.Contains([string]$comparisonMarker)) { $comparisonBlocked = $true; break }
                }
                if ($comparisonBlocked) { continue }
                $matches += [pscustomobject]@{
                    id = [string]$trigger.id
                    label = [string]$trigger.label
                    phrase = [string]$phrase
                    skillName = [string]$registry.skillName
                    operation = [string]$trigger.operation
                    executionPolicy = [string]$trigger.executionPolicy
                    responsibilityGate = [string]$trigger.responsibilityGate
                    allowedResponsibilities = @($trigger.allowedResponsibilities)
                    delegationMarkers = @($trigger.delegationMarkers)
                    bareTriggerPolicy = [string]$trigger.bareTriggerPolicy
                    longTextPolicy = if ($null -ne $trigger.longTextPolicy) { $trigger.longTextPolicy } else { $null }
                    routeKeys = @($trigger.routeKeys)
                    nextStage = [string]$trigger.nextStage
                    requiresDeepUserGuidance = [bool]$trigger.requiresDeepUserGuidance
                    allowAutonomousExpansion = [bool]$trigger.allowAutonomousExpansion
                    requiredQuestions = @($trigger.requiredQuestions)
                    evidencePolicy = [string]$trigger.evidencePolicy
                    focusAction = [string]$trigger.focusAction
                    focusActivation = if ($null -ne $trigger.focusActivation) { $trigger.focusActivation } else { $null }
                    priority = [int]$trigger.priority
                    composable = [bool]$trigger.composable
                    requiresUserChoice = [bool]$trigger.requiresUserChoice
                    activationMode = [string]$trigger.activationMode
                    defaultState = [string]$trigger.defaultState
                    source = [string]$source
                }
                break
            }
        }
        if ([string]$trigger.matchMode -eq 'explicit-or-contextual' -and @($trigger.contextualPhrases).Count -gt 0) {
            foreach ($contextPhrase in @($trigger.contextualPhrases)) {
                if ($trimmedPrompt -eq [string]$contextPhrase -and [string]$contextPhrase -eq '只读') { continue }
                if ($scanText.IndexOf([string]$contextPhrase, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    $matches += [pscustomobject]@{
                        id = [string]$trigger.id; label = [string]$trigger.label; phrase = [string]$contextPhrase
                        skillName = [string]$registry.skillName; operation = [string]$trigger.operation; routeKeys = @($trigger.routeKeys)
                        executionPolicy = [string]$trigger.executionPolicy; responsibilityGate = [string]$trigger.responsibilityGate; allowedResponsibilities = @($trigger.allowedResponsibilities)
                        delegationMarkers = @($trigger.delegationMarkers); bareTriggerPolicy = [string]$trigger.bareTriggerPolicy; longTextPolicy = if ($null -ne $trigger.longTextPolicy) { $trigger.longTextPolicy } else { $null }
                        nextStage = 'read-only-confirmation'; requiresDeepUserGuidance = [bool]$trigger.requiresDeepUserGuidance
                        allowAutonomousExpansion = [bool]$trigger.allowAutonomousExpansion; requiredQuestions = @($trigger.requiredQuestions)
                        evidencePolicy = [string]$trigger.evidencePolicy; focusAction = [string]$trigger.focusAction
                        priority = [int]$trigger.priority; composable = [bool]$trigger.composable; requiresUserChoice = $true
                        activationMode = 'confirm-once'; defaultState = 'normal'; source = [string]$source
                    }
                    break
                }
            }
        }
    }
}

$unique = @($matches | Sort-Object id -Unique)
$precedenceOverride = $null
$precedenceReason = $null
$highRiskConflict = $false
$rule = @($index.precedenceRules)[0]
if ($null -ne $rule) {
    $hasAny = $false
    foreach ($term in @($rule.matchAny)) { if ($scanText -like ('*' + [string]$term + '*')) { $hasAny = $true; break } }
    $hasRequired = $false
    foreach ($term in @($rule.requiresAny)) { if ($scanText -like ('*' + [string]$term + '*')) { $hasRequired = $true; break } }
    $negated = $false
    foreach ($marker in @($index.negationMarkers)) {
        foreach ($term in @($rule.matchAny)) {
            $termIndex = $scanText.IndexOf([string]$term, [StringComparison]::OrdinalIgnoreCase)
            if ($termIndex -ge 0) {
                $prefix = $scanText.Substring([Math]::Max(0, $termIndex - 12), [Math]::Min(12, $termIndex))
                if ($prefix.Contains([string]$marker)) { $negated = $true; break }
            }
        }
        if ($negated) { break }
    }
    $historical = $false
    foreach ($historyMarker in @($rule.historyMarkers)) { if ($scanText.IndexOf([string]$historyMarker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $historical = $true; break } }
    $comparison = $false
    foreach ($term in @($rule.matchAny)) {
        $termIndex = $scanText.IndexOf([string]$term, [StringComparison]::OrdinalIgnoreCase)
        if ($termIndex -ge 0) {
            $suffixStart = $termIndex + ([string]$term).Length
            $suffix = if ($suffixStart -lt $scanText.Length) { $scanText.Substring($suffixStart, [Math]::Min(8, $scanText.Length - $suffixStart)) } else { '' }
            foreach ($comparisonMarker in @($index.comparisonMarkers)) { if ($suffix.Contains([string]$comparisonMarker)) { $comparison = $true; break } }
        }
        if ($comparison) { break }
    }
    if ($hasAny -and $hasRequired -and -not $negated -and -not $historical -and -not $comparison) {
        $props = @{ id=[string]$rule.id; label=[string]$rule.label; phrase='P0-feedback'; skillName='es-ai-interaction-governance'; operation=[string]$rule.operation; routeKeys=@($rule.routeKeys); nextStage=[string]$rule.nextStage; requiresDeepUserGuidance=[bool]$rule.requiresDeepUserGuidance; allowAutonomousExpansion=[bool]$rule.allowAutonomousExpansion; requiredQuestions=@(); evidencePolicy=[string]$rule.evidencePolicy; focusAction='lock'; priority=[int]$rule.priority; composable=$false; requiresUserChoice=$true; activationMode='confirm-before-action'; defaultState='blocked'; authorityOverride=$true }
        $precedenceOverride = New-Object PSObject -Property $props
        $precedenceReason = 'p0-feedback-override'
    }
}
$ordinaryMatches = $unique
if (-not $precedenceOverride) {
    $highRiskRule = @($index.precedenceRules | Where-Object { [string]$_.id -eq 'high-risk-over-wrapper' })[0]
    if ($highRiskRule) {
        $highRiskOps = @($highRiskRule.whenCandidateOperations | ForEach-Object { [string]$_ })
        $overriddenOps = @($highRiskRule.overriddenOperations | ForEach-Object { [string]$_ })
        $highRisk = @($unique | Where-Object { $highRiskOps -contains [string]$_.operation } | Sort-Object priority -Descending)
        $wrapped = @($unique | Where-Object { $overriddenOps -contains [string]$_.operation })
        if ($highRisk.Count -gt 1 -and $wrapped.Count -gt 0) {
            $highRiskConflict = $true
        } elseif ($highRisk.Count -eq 1 -and $wrapped.Count -gt 0) {
            $precedenceOverride = $highRisk[0]
            $precedenceReason = [string]$highRiskRule.id
        }
    }
}
if ($precedenceOverride) { $unique = @($precedenceOverride) + @($ordinaryMatches) }
$suppression = $null
if (-not [string]::IsNullOrWhiteSpace($SuppressionStatePath) -and (Test-Path -LiteralPath $SuppressionStatePath -PathType Leaf)) {
    $suppression = Get-Content -LiteralPath $SuppressionStatePath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([int]$suppression.remainingRounds -gt 0) {
        $suppressedIds = @($suppression.semanticIds | ForEach-Object { [string]$_ })
        $unique = @($unique | Where-Object { $suppressedIds -notcontains [string]$_.id })
    }
}
$nonComposable = @($unique | Where-Object { -not $_.composable -and $_.id -ne 'show-me-evidence' })
$hasEvidencePresentation = (@($unique | Where-Object { $_.id -eq 'show-me-evidence' }).Count -gt 0)
$allComposable = ($unique.Count -gt 1 -and ($nonComposable.Count -eq 0 -or ($hasEvidencePresentation -and $nonComposable.Count -eq 1)))
$status = if ($highRiskConflict) { 'ambiguous' } elseif ($precedenceOverride) { 'triggered' } elseif ($unique.Count -eq 1 -or $allComposable) { 'triggered' } elseif ($unique.Count -gt 1) { 'ambiguous' } else { 'not-triggered' }
$selected = if ($status -eq 'triggered') {
    if ($precedenceOverride) { $precedenceOverride } elseif ($hasEvidencePresentation -and $nonComposable.Count -eq 1) { $nonComposable[0] } else { @($unique | Sort-Object priority -Descending)[0] }
} else { $null }
$executionIntent = 'none'
if ($selected -and [string]$selected.operation -eq 'PromptAutoWrapToggle') {
    $delegated = $false
    foreach ($marker in @($selected.delegationMarkers)) {
        if ($scanText -like ('*' + [string]$marker + '*')) { $delegated = $true; break }
    }
    $allowedResponsibilities = @($selected.allowedResponsibilities | ForEach-Object { [string]$_ })
    $inScope = ($allowedResponsibilities.Count -eq 0 -or $allowedResponsibilities -contains $ResponsibilityKey)
    $executionIntent = if (-not $inScope) { 'review-responsibility-out-of-scope' } elseif ($delegated) { 'delegated-current-window' } else { 'current-turn-context' }
    if (-not $inScope) { $status = 'review'; $selected | Add-Member -NotePropertyName reviewReason -NotePropertyValue 'responsibility-out-of-scope' -Force }
    if ($selected.longTextPolicy -and $originalText.Length -gt [int]$selected.longTextPolicy.maxCharsWithoutMarker) {
        $boundaryFound = $false
        foreach ($signal in @($selected.longTextPolicy.requiredBoundarySignal)) {
            if (-not [string]::IsNullOrWhiteSpace([string]$signal) -and $scanText.IndexOf([string]$signal, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $boundaryFound = $true; break }
        }
        if (-not $boundaryFound) { $status = 'review'; $selected | Add-Member -NotePropertyName reviewReason -NotePropertyValue 'long-text-boundary-signal-missing' -Force }
    }
    $selected | Add-Member -NotePropertyName executionIntent -NotePropertyValue $executionIntent -Force
}
$displayLine = $null
if ($selected -and $status -eq 'triggered') {
    $body = ([string]$index.presentation.template).Replace('{label}', [string]$selected.label)
    $displayLine = "$([string]$index.presentation.prefix)$body"
}

[ordered]@{
    schemaVersion = 1
    registryId = [string]$index.registryId
    status = $status
    finalDisposition = if ($highRiskConflict) { 'clarify-high-risk-conflict' } elseif ($status -eq 'triggered' -and $precedenceOverride -and $precedenceReason -eq 'p0-feedback-override') { 'p0-review-required' } elseif ($status -eq 'triggered' -and $precedenceOverride) { 'high-risk-review-required' } elseif ($status -eq 'triggered' -and $selected -and $selected.executionIntent -eq 'delegated-current-window') { 'delegated-current-window' } elseif ($status -eq 'triggered') { 'resolved-no-execution' } elseif ($status -eq 'review') { 'review' } elseif ($status -eq 'ambiguous') { 'clarify' } else { 'none' }
    input = [ordered]@{ rawPrompt = $originalText; responsibilityKey = $ResponsibilityKey }
    isSuperSemantics = ($status -eq 'triggered')
    isRegularRoute = $false
    displayLine = $displayLine
    selected = $selected
    additionalMatches = if ($selected) { @($unique | Where-Object { $_.id -ne $selected.id }) } else { @() }
    candidates = $unique
    precedence = [ordered]@{ applied = [bool]$precedenceOverride; ruleId = $precedenceReason; overriddenCandidateIds = if ($precedenceOverride) { @($ordinaryMatches | Where-Object { $_.id -ne $precedenceOverride.id } | ForEach-Object { [string]$_.id }) } else { @() } }
    presentation = $index.presentation
    scan = [ordered]@{ mode = $scanMode; originalChars = $originalText.Length; scannedChars = $scanText.Length; shortTextMaxChars = $maxChars; headChars = $headChars; tailChars = $tailChars }
    suppression = if ($suppression) { [ordered]@{ remainingRounds = [int]$suppression.remainingRounds; statePath = $SuppressionStatePath } } else { $null }
    requiresUserChoice = (($status -eq 'ambiguous') -or @($unique | Where-Object { $_.requiresUserChoice }).Count -gt 0)
    focusActivation = if ($selected -and $null -ne $selected.focusActivation) { $selected.focusActivation } else { $null }
    canExecute = $false
    executionIntent = $executionIntent
    authorityDecision = if ($precedenceOverride) { $precedenceReason } else { 'ordinary-semantic-resolution' }
    nonClaims = @($index.nonClaims)
} | ConvertTo-Json -Depth 10
