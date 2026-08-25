[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$ProjectRoot,
    [string]$OutputPath = 'ES/Output/Governance/commercial-coherence.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# Read-only validator contract: all inputs and report paths are project-relative;
# this aggregate never mutates source assets or starts runtime behavior.
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path

function Convert-ValidatorJson([string]$Raw, [string]$Path) {
    $raw = $Raw
    $match = [regex]::Match($raw, '(?s)\{.*\}\s*$')
    if (-not $match.Success) {
        return [pscustomobject]@{ status = 'blocked'; blockedCount = 1; reviewCount = 0; failureClass = 'validator-error'; error = "Validator did not return JSON: $Path" }
    }
    return ($match.Value | ConvertFrom-Json)
}
function Capture-ValidatorOutput([scriptblock]$Invocation) {
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        return (& $Invocation 2>&1 | Out-String)
    } catch { return ($_ | Out-String) }
    finally { $ErrorActionPreference = $previousPreference }
}
function Prefer-ValidatorReport($Fallback, [string]$RelativePath) {
    $full = Join-Path $root ($RelativePath.Replace('/', '\'))
    if (Test-Path -LiteralPath $full -PathType Leaf) {
        try { return (Get-Content -LiteralPath $full -Raw -Encoding UTF8 | ConvertFrom-Json) } catch { return $Fallback }
    }
    return $Fallback
}
function Get-CoherenceSnapshot([string]$Root) {
    $paths = @(
        '.agents/SKILL_DISCOVERY_POLICY.json',
        '.agents/SKILL_CATALOG.yaml',
        '.agents/SKILL_REGISTRY.manifest.json',
        'Assets/Plugins/ES/AICommands/AICommandCatalog.json',
        'Documentation/AIKnowledge/AIBRAIN_ENTRY.md',
        'Documentation/AIKnowledge/KnowledgeIndex.yaml'
    )
    $items = [ordered]@{}
    foreach ($relative in $paths) {
        $full = Join-Path $Root ($relative.Replace('/', '\'))
        if (Test-Path -LiteralPath $full -PathType Leaf) {
            $items[$relative] = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant()
        } else { $items[$relative] = 'missing' }
    }
    $canonical = ($items.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join "`n"
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hash = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical)))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
    [pscustomobject]@{ snapshotHash = $hash; files = $items }
}
function Get-ReceiptEvidence([string]$RelativePath) {
    $full = Join-Path $root ($RelativePath.Replace('/', '\'))
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { return [pscustomobject]@{ reportExists = $false; reportHash = 'missing' } }
    return [pscustomobject]@{ reportExists = $true; reportHash = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant() }
}
function Test-TrackedArtifact([string]$RelativePath) {
    $previous = $ErrorActionPreference
    try { $ErrorActionPreference = 'Continue'; $null = & git -C $root ls-files --error-unmatch -- $RelativePath 2>$null; return ($LASTEXITCODE -eq 0) }
    catch { return $false }
    finally { $ErrorActionPreference = $previous }
}

$architecturePath = Join-Path $root '.agents/skills/es-skill-governance/scripts/Test-ESSkillArchitecture.ps1'
$compatibilityPath = Join-Path $root '.agents/skills/es-skill-governance/scripts/Test-ESAutomationCompatibility.ps1'
$knowledgePath = Join-Path $root '.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1'
$refreshPlanPath = Join-Path $root '.agents/skills/es-knowledge-validator/scripts/Export-ESKnowledgeRefreshPlan.ps1'
$commandPath = Join-Path $root '.agents/skills/es-use-ai-command/scripts/Test-ESAICommands.ps1'
$coveragePath = Join-Path $root '.agents/skills/es-skill-governance/scripts/Test-ESStaticAcceptanceCoverage.ps1'
$snapshotBefore = Get-CoherenceSnapshot $root

$architecture = Convert-ValidatorJson -Raw (Capture-ValidatorOutput { & $architecturePath -ProjectRoot $root }) -Path $architecturePath
$compatibility = Convert-ValidatorJson -Raw (Capture-ValidatorOutput { & $compatibilityPath -ProjectRoot $root }) -Path $compatibilityPath
$knowledgeReportPath = 'ES/Output/Governance/commercial-coherence-knowledge.json'
$knowledge = Convert-ValidatorJson -Raw (Capture-ValidatorOutput { & $knowledgePath -ProjectRoot $root -Mode All -ReportPath $knowledgeReportPath }) -Path $knowledgePath
$architecture = Prefer-ValidatorReport $architecture 'ES/Output/SkillArchitecture/architecture.json'
$compatibility = Prefer-ValidatorReport $compatibility 'ES/Output/AutomationGovernance/compatibility.json'
$knowledge = Prefer-ValidatorReport $knowledge $knowledgeReportPath
$refreshPlan = Convert-ValidatorJson -Raw (Capture-ValidatorOutput { & $refreshPlanPath -ProjectRoot $root -OutputPath 'ES/Output/KnowledgeValidation/commercial-refresh-plan.json' -SampleDelayMilliseconds 20 }) -Path $refreshPlanPath
$refreshPlan = Prefer-ValidatorReport $refreshPlan 'ES/Output/KnowledgeValidation/commercial-refresh-plan.json'
$coverageRaw = Capture-ValidatorOutput { & $coveragePath -ProjectRoot $root }
$coverage = Convert-ValidatorJson -Raw $coverageRaw -Path $coveragePath
$coverage = Prefer-ValidatorReport $coverage 'ES/Output/Governance/static-acceptance-coverage.json'
$commandRaw = & $commandPath -ProjectRoot $root 2>&1 | Out-String
$commandMatch = [regex]::Match($commandRaw, 'AICommands:\s*(\d+),\s*navigation:\s*(\d+),\s*catalog:\s*(\d+),\s*invalid:\s*(\d+)')
if (-not $commandMatch.Success) { throw 'AICommand validator did not return its summary.' }
$commandSummary = [ordered]@{
    catalogPath = 'Assets/Plugins/ES/AICommands/AICommandCatalog.json'
    total = [int]$commandMatch.Groups[1].Value
    navigation = [int]$commandMatch.Groups[2].Value
    catalog = [int]$commandMatch.Groups[3].Value
    invalid = [int]$commandMatch.Groups[4].Value
    status = if ([int]$commandMatch.Groups[4].Value -eq 0) { 'passed' } else { 'blocked' }
}
$snapshotAfter = Get-CoherenceSnapshot $root
$architectureEvidence = Get-ReceiptEvidence 'ES/Output/SkillArchitecture/architecture.json'
$compatibilityEvidence = Get-ReceiptEvidence 'ES/Output/AutomationGovernance/compatibility.json'
$knowledgeEvidence = Get-ReceiptEvidence $knowledgeReportPath
$coverageEvidence = Get-ReceiptEvidence 'ES/Output/Governance/static-acceptance-coverage.json'
$catalogEvidence = Get-ReceiptEvidence 'Assets/Plugins/ES/AICommands/AICommandCatalog.json'
$deliveryArtifacts = @(
    '.agents/skills/es-skill-governance/scripts/Test-ESCommercialCoherence.ps1',
    '.agents/skills/es-skill-governance/scripts/Test-ESStaticAcceptanceCoverage.ps1',
    '.agents/skills/es-skill-governance/scripts/Test-ESRuntimeAuthorizationContract.ps1',
    '.agents/skills/es-knowledge-validator/scripts/Export-ESKnowledgeRefreshPlan.ps1',
    '.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeStableRefresh.ps1',
    '.agents/skills/es-skill-governance/references/commercial-coherence-contract.md'
)
$untrackedDeliveryArtifacts = @($deliveryArtifacts | Where-Object { -not (Test-TrackedArtifact $_) })
$snapshotStable = [string]::Equals($snapshotBefore.snapshotHash, $snapshotAfter.snapshotHash, [StringComparison]::OrdinalIgnoreCase)
$knowledgeStaticStatus = if ($knowledge.PSObject.Properties['staticStatus']) { [string]$knowledge.staticStatus } else { 'validator-error' }
$knowledgeRuntimeStatus = if ($knowledge.PSObject.Properties['runtimeStatus']) { [string]$knowledge.runtimeStatus } else { 'not-run' }
$knowledgeFindingCount = if ($knowledge.PSObject.Properties['findingCount']) { [int]$knowledge.findingCount } else { 1 }
$knowledgeFindingCodes = if ($knowledge.PSObject.Properties['findings']) { @($knowledge.findings | ForEach-Object { [string]$_.code }) } else { @('validator-error') }
$freshnessOnlyCodes = @('SOURCE_HASH_DRIFT', 'CONTENT_HASH_MISMATCH')
$knowledgeFreshnessOnly = ($knowledgeFindingCodes.Count -gt 0 -and @($knowledgeFindingCodes | Where-Object { $_ -notin $freshnessOnlyCodes }).Count -eq 0)
$knowledgeAggregateStatus = if ($knowledgeFreshnessOnly) { 'review' } else { [string]$knowledge.status }
$compatibilityClaims = if ($compatibility.PSObject.Properties['claimsNotProven']) { @($compatibility.claimsNotProven) } else { @('compatibility-validator-error') }

$checks = [ordered]@{
    snapshotStability = [ordered]@{ status = if ($snapshotStable) { 'passed' } else { 'blocked' }; before = $snapshotBefore.snapshotHash; after = $snapshotAfter.snapshotHash; detail = if ($snapshotStable) { 'Governance surfaces remained unchanged during this audit.' } else { 'Governance surfaces changed during this audit; sub-results belong to different source generations.' } }
    skillArchitecture = [ordered]@{ status = [string]$architecture.status; reportPath = 'ES/Output/SkillArchitecture/architecture.json'; reportExists = $architectureEvidence.reportExists; reportHash = $architectureEvidence.reportHash; blockedCount = if ($architecture.PSObject.Properties['blockedCount']) { [int]$architecture.blockedCount } else { 1 }; reviewCount = if ($architecture.PSObject.Properties['reviewCount']) { [int]$architecture.reviewCount } else { 0 }; failureClass = if ($architecture.PSObject.Properties['failureClass']) { [string]$architecture.failureClass } else { '' } }
    aiCommands = [ordered]@{ catalogPath = $commandSummary.catalogPath; catalogExists = $catalogEvidence.reportExists; catalogHash = $catalogEvidence.reportHash; total = $commandSummary.total; navigation = $commandSummary.navigation; catalog = $commandSummary.catalog; invalid = $commandSummary.invalid; status = $commandSummary.status }
    esAutomationCompatibility = [ordered]@{ status = [string]$compatibility.status; reportPath = 'ES/Output/AutomationGovernance/compatibility.json'; reportExists = $compatibilityEvidence.reportExists; reportHash = $compatibilityEvidence.reportHash; claimsNotProven = $compatibilityClaims }
    aiKnowledge = [ordered]@{ status = $knowledgeAggregateStatus; rawStatus = [string]$knowledge.status; reportPath = $knowledgeReportPath; reportExists = $knowledgeEvidence.reportExists; reportHash = $knowledgeEvidence.reportHash; staticStatus = $knowledgeStaticStatus; runtimeStatus = $knowledgeRuntimeStatus; findingCount = $knowledgeFindingCount; findingCodes = $knowledgeFindingCodes; freshnessOnly = $knowledgeFreshnessOnly; failureClass = if ($knowledge.PSObject.Properties['failureClass']) { [string]$knowledge.failureClass } else { '' } }
    knowledgeSourceFreshness = [ordered]@{ status = if ([int]$refreshPlan.findingCount -eq 0) { 'passed' } elseif ([int]$refreshPlan.unstableFindingCount -gt 0) { 'review' } else { 'review' }; reportPath = 'ES/Output/KnowledgeValidation/commercial-refresh-plan.json'; findingCount = [int]$refreshPlan.findingCount; unstableFindingCount = [int]$refreshPlan.unstableFindingCount; nextAction = [string]$refreshPlan.nextAction }
    staticAcceptanceCoverage = [ordered]@{ status = [string]$coverage.status; reportPath = 'ES/Output/Governance/static-acceptance-coverage.json'; reportExists = $coverageEvidence.reportExists; reportHash = $coverageEvidence.reportHash; skillCount = [int]$coverage.skillCount; coveredSkillCount = [int]$coverage.coveredSkillCount; blockedSkillCount = [int]$coverage.blockedSkillCount }
    deliveryTracking = [ordered]@{ status = if ($untrackedDeliveryArtifacts.Count -eq 0) { 'passed' } else { 'review' }; requiredArtifactCount = $deliveryArtifacts.Count; untrackedArtifactCount = $untrackedDeliveryArtifacts.Count; untrackedArtifacts = $untrackedDeliveryArtifacts; nextAction = if ($untrackedDeliveryArtifacts.Count -eq 0) { 'Commercial governance artifacts are tracked by the repository.' } else { 'Review and add the listed governance artifacts to version control before release.' } }
}
$blocked = @($checks.Values | Where-Object { $_.status -eq 'blocked' }).Count
$review = @($checks.Values | Where-Object { $_.status -eq 'review' }).Count
$overall = if ($blocked -eq 0 -and $review -eq 0) { 'static-coherent' } else { 'static-review-required' }
$report = [ordered]@{
    schemaVersion = 1
    toolId = 'es-commercial-coherence'
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    snapshotHash = $snapshotBefore.snapshotHash
    snapshotStable = $snapshotStable
    profile = 'StaticReview'
    mutatesSources = $false
    startsRuntime = $false
    overallVerdict = $overall
    checks = $checks
    blockedCheckCount = $blocked
    reviewCheckCount = $review
    claimsProven = @(
        'Skill responsibility-specific static acceptance coverage',
        'AICommand catalog and risk metadata consistency',
        'ES AIBrain/Facade/Bridge/TaskContract compatibility',
        'CapabilityEnvelope, PlanHash, idempotency and CompletionDecision source contracts',
        'Runtime authorization contract fixture boundaries without starting Runtime',
        'Knowledge SourceRef and ContentHash validation semantics',
        'Governance receipt existence and report hashes',
        'Critical governance artifact version-control tracking'
    )
    claimsNotProven = @('Unity/editor runtime behavior', 'external process/network behavior', 'Profiler/Player/IL2CPP/release behavior')
    nextAction = if ($blocked -eq 0) { 'Static governance surfaces are coherent; runtime requires separate authorized evidence.' } else { 'Review each check and resolve its own contract findings; do not treat this aggregate as a replacement for the underlying receipt.' }
}
$output = Join-Path $root ($OutputPath.Replace('/', '\'))
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $output) | Out-Null
$temporary = "$output.tmp-$([Guid]::NewGuid().ToString('N'))"
try {
    [IO.File]::WriteAllText($temporary, ($report | ConvertTo-Json -Depth 12), (New-Object Text.UTF8Encoding($false)))
    Move-Item -LiteralPath $temporary -Destination $output -Force
} finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force } }
$report | ConvertTo-Json -Depth 12
