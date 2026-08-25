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
. (Join-Path $PSScriptRoot 'ESPathBoundary.Common.ps1')

function Resolve-ESCommercialOutputPath([string]$Candidate) {
    $target = Resolve-ESContainedRelativePath -Candidate $Candidate -ContainerRoot $root -Label 'OutputPath'
    if (-not $target.RelativePath.StartsWith('ES/Output/', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'OutputPath must remain below ES/Output.'
    }
    return $target
}

try {
    $outputTarget = Resolve-ESCommercialOutputPath -Candidate $OutputPath
} catch {
    Write-Error $_.Exception.Message -ErrorAction Continue
    exit 2
}

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
function Capture-IsolatedValidatorOutput([string]$Path, [hashtable]$Parameters) {
    $pipeline = [PowerShell]::Create()
    try {
        [void]$pipeline.AddCommand($Path)
        foreach ($entry in $Parameters.GetEnumerator()) {
            [void]$pipeline.AddParameter([string]$entry.Key, $entry.Value)
        }
        $result = $pipeline.Invoke()
        $output = ($result | Out-String)
        if ($pipeline.Streams.Error.Count -gt 0) {
            $errors = ($pipeline.Streams.Error | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
            return ('Validator error: ' + ($errors -replace '[{}]', ''))
        }
        return $output
    } catch { return ($_ | Out-String) }
    finally { $pipeline.Dispose() }
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
        'AGENTS.md',
        '.agents/SKILL_DISCOVERY_POLICY.json',
        '.agents/SKILL_CATALOG.yaml',
        '.agents/SKILL_REGISTRY.manifest.json',
        '.agents/skills/es-skill-governance/references/user-directed-low-risk-policy.json',
        '.agents/skills/es-skill-governance/scripts/Test-ESUserDirectedLowRiskPolicy.ps1',
        '.agents/skills/es-skill-governance/references/user-directed-action-authority.md',
        '.agents/tests/Test-ESUserDirectedActionAuthority.ps1',
        '.agents/skills/es-skill-governance/scripts/Test-ESCommercialCoherence.ps1',
        '.agents/skills/es-skill-governance/scripts/Test-ESStaticAcceptanceCoverage.ps1',
        '.agents/skills/es-skill-governance/scripts/Test-ESRuntimeAuthorizationContract.ps1',
        '.agents/skills/es-knowledge-validator/scripts/Export-ESKnowledgeRefreshPlan.ps1',
        '.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeStableRefresh.ps1',
        '.agents/skills/es-skill-governance/references/commercial-coherence-contract.md',
        '.agents/tests/Test-ESCommercialDeliveryTracking.ps1',
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
function Invoke-ESGitRead([string]$RepositoryRoot, [string[]]$Arguments) {
    $previous = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = (& git -C $RepositoryRoot @Arguments 2>$null | Out-String).Trim()
        return [pscustomobject]@{ exitCode = $LASTEXITCODE; output = $output; error = '' }
    } catch {
        return [pscustomobject]@{ exitCode = 127; output = ''; error = $_.Exception.Message }
    } finally { $ErrorActionPreference = $previous }
}
function Resolve-ESDeliveryArtifactVersionState {
    param(
        [bool]$GitAvailable,
        [bool]$WorktreeExists,
        [string]$WorktreeObjectId,
        [bool]$TrackedInIndex,
        [string]$IndexObjectId,
        [bool]$TrackedInHead,
        [string]$HeadObjectId
    )
    if (-not $GitAvailable) { return 'git-error' }
    if (-not $WorktreeExists) { return 'worktree-missing' }
    if (-not $TrackedInIndex) { return 'untracked' }
    if ([string]::IsNullOrWhiteSpace($WorktreeObjectId) -or [string]::IsNullOrWhiteSpace($IndexObjectId)) { return 'git-error' }
    if (-not [string]::Equals($WorktreeObjectId, $IndexObjectId, [StringComparison]::Ordinal)) { return 'worktree-differs-from-index' }
    if (-not $TrackedInHead) { return 'index-only-staged-new' }
    if ([string]::IsNullOrWhiteSpace($HeadObjectId)) { return 'git-error' }
    if (-not [string]::Equals($IndexObjectId, $HeadObjectId, [StringComparison]::Ordinal)) { return 'index-differs-from-head' }
    return 'committed-clean'
}
function Get-ESDeliveryArtifactVersionState([string]$RepositoryRoot, [string]$RelativePath) {
    $normalizedRoot = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\', '/')
    $invalidPath = [IO.Path]::IsPathRooted($RelativePath)
    $worktreePath = ''
    if (-not $invalidPath) {
        $worktreePath = [IO.Path]::GetFullPath((Join-Path $normalizedRoot ($RelativePath.Replace('/', '\'))))
        $invalidPath = -not $worktreePath.StartsWith($normalizedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
    }
    if ($invalidPath) {
        return [pscustomobject][ordered]@{
            path = $RelativePath; versionState = 'invalid-path'; worktreeExists = $false
            trackedInIndex = $false; trackedInHead = $false
            worktreeObjectId = ''; indexObjectId = ''; headObjectId = ''
            worktreeMatchesIndex = $false; indexMatchesHead = $false; currentCommitCarriesWorktree = $false
            repositoryProbeExitCode = -1; worktreeQueryExitCode = -1; indexQueryExitCode = -1; headQueryExitCode = -1
            gitError = 'Delivery artifact path is not a contained project-relative path.'
        }
    }

    $repositoryProbe = Invoke-ESGitRead -RepositoryRoot $normalizedRoot -Arguments @('rev-parse', '--is-inside-work-tree')
    $gitAvailable = ($repositoryProbe.exitCode -eq 0 -and [string]::Equals($repositoryProbe.output, 'true', [StringComparison]::OrdinalIgnoreCase))
    $worktreeExists = Test-Path -LiteralPath $worktreePath -PathType Leaf
    $worktreeQuery = if ($gitAvailable -and $worktreeExists) {
        Invoke-ESGitRead -RepositoryRoot $normalizedRoot -Arguments @('hash-object', "--path=$RelativePath", '--', $worktreePath)
    } else { [pscustomobject]@{ exitCode = if ($worktreeExists) { 127 } else { 1 }; output = ''; error = '' } }
    $indexQuery = if ($gitAvailable) {
        Invoke-ESGitRead -RepositoryRoot $normalizedRoot -Arguments @('rev-parse', '--verify', ":$RelativePath")
    } else { [pscustomobject]@{ exitCode = 127; output = ''; error = '' } }
    $headQuery = if ($gitAvailable) {
        Invoke-ESGitRead -RepositoryRoot $normalizedRoot -Arguments @('rev-parse', '--verify', "HEAD:$RelativePath")
    } else { [pscustomobject]@{ exitCode = 127; output = ''; error = '' } }
    $trackedInIndex = $indexQuery.exitCode -eq 0
    $trackedInHead = $headQuery.exitCode -eq 0
    $worktreeObjectId = if ($worktreeQuery.exitCode -eq 0) { [string]$worktreeQuery.output } else { '' }
    $indexObjectId = if ($trackedInIndex) { [string]$indexQuery.output } else { '' }
    $headObjectId = if ($trackedInHead) { [string]$headQuery.output } else { '' }
    $versionState = Resolve-ESDeliveryArtifactVersionState -GitAvailable $gitAvailable -WorktreeExists $worktreeExists -WorktreeObjectId $worktreeObjectId -TrackedInIndex $trackedInIndex -IndexObjectId $indexObjectId -TrackedInHead $trackedInHead -HeadObjectId $headObjectId
    $worktreeMatchesIndex = $worktreeExists -and $trackedInIndex -and [string]::Equals($worktreeObjectId, $indexObjectId, [StringComparison]::Ordinal)
    $indexMatchesHead = $trackedInIndex -and $trackedInHead -and [string]::Equals($indexObjectId, $headObjectId, [StringComparison]::Ordinal)
    $currentCommitCarriesWorktree = $worktreeExists -and $trackedInHead -and [string]::Equals($worktreeObjectId, $headObjectId, [StringComparison]::Ordinal)

    return [pscustomobject][ordered]@{
        path = $RelativePath
        versionState = $versionState
        worktreeExists = $worktreeExists
        trackedInIndex = $trackedInIndex
        trackedInHead = $trackedInHead
        worktreeObjectId = $worktreeObjectId
        indexObjectId = $indexObjectId
        headObjectId = $headObjectId
        worktreeMatchesIndex = $worktreeMatchesIndex
        indexMatchesHead = $indexMatchesHead
        currentCommitCarriesWorktree = $currentCommitCarriesWorktree
        repositoryProbeExitCode = [int]$repositoryProbe.exitCode
        worktreeQueryExitCode = [int]$worktreeQuery.exitCode
        indexQueryExitCode = [int]$indexQuery.exitCode
        headQueryExitCode = [int]$headQuery.exitCode
        gitError = [string]$repositoryProbe.error
    }
}

$architecturePath = Join-Path $root '.agents/skills/es-skill-governance/scripts/Test-ESSkillArchitecture.ps1'
$compatibilityPath = Join-Path $root '.agents/skills/es-skill-governance/scripts/Test-ESAutomationCompatibility.ps1'
$knowledgePath = Join-Path $root '.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1'
$refreshPlanPath = Join-Path $root '.agents/skills/es-knowledge-validator/scripts/Export-ESKnowledgeRefreshPlan.ps1'
$commandPath = Join-Path $root '.agents/skills/es-use-ai-command/scripts/Test-ESAICommands.ps1'
$coveragePath = Join-Path $root '.agents/skills/es-skill-governance/scripts/Test-ESStaticAcceptanceCoverage.ps1'
$authorityRegressionPath = Join-Path $root '.agents/tests/Test-ESUserDirectedActionAuthority.ps1'
$managedAuthorizationLane = 'ManagedAIBrain'
$snapshotBefore = Get-CoherenceSnapshot $root

$architectureInvocation = Convert-ValidatorJson -Raw (Capture-ValidatorOutput { & $architecturePath -ProjectRoot $root -ReportPath 'ES/Output/SkillArchitecture/architecture.json' -AuthorizationLane $managedAuthorizationLane }) -Path $architecturePath
$compatibility = Convert-ValidatorJson -Raw (Capture-ValidatorOutput { & $compatibilityPath -ProjectRoot $root }) -Path $compatibilityPath
$knowledgeReportPath = 'ES/Output/Governance/commercial-coherence-knowledge.json'
$knowledge = Convert-ValidatorJson -Raw (Capture-ValidatorOutput { & $knowledgePath -ProjectRoot $root -Mode All -ReportPath $knowledgeReportPath }) -Path $knowledgePath
$architecture = Prefer-ValidatorReport $architectureInvocation 'ES/Output/SkillArchitecture/architecture.json'
$compatibility = Prefer-ValidatorReport $compatibility 'ES/Output/AutomationGovernance/compatibility.json'
$knowledge = Prefer-ValidatorReport $knowledge $knowledgeReportPath
$refreshPlan = Convert-ValidatorJson -Raw (Capture-ValidatorOutput { & $refreshPlanPath -ProjectRoot $root -OutputPath 'ES/Output/KnowledgeValidation/commercial-refresh-plan.json' -SampleDelayMilliseconds 20 }) -Path $refreshPlanPath
$refreshPlan = Prefer-ValidatorReport $refreshPlan 'ES/Output/KnowledgeValidation/commercial-refresh-plan.json'
$coverageRaw = Capture-ValidatorOutput { & $coveragePath -ProjectRoot $root }
$coverage = Convert-ValidatorJson -Raw $coverageRaw -Path $coveragePath
$coverage = Prefer-ValidatorReport $coverage 'ES/Output/Governance/static-acceptance-coverage.json'
$authorityRegressionRaw = Capture-IsolatedValidatorOutput -Path $authorityRegressionPath -Parameters @{ ProjectRoot = $root }
$authorityRegression = Convert-ValidatorJson -Raw $authorityRegressionRaw -Path $authorityRegressionPath
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
$architectureEvidence = Get-ReceiptEvidence 'ES/Output/SkillArchitecture/architecture.json'
$compatibilityEvidence = Get-ReceiptEvidence 'ES/Output/AutomationGovernance/compatibility.json'
$knowledgeEvidence = Get-ReceiptEvidence $knowledgeReportPath
$coverageEvidence = Get-ReceiptEvidence 'ES/Output/Governance/static-acceptance-coverage.json'
$catalogEvidence = Get-ReceiptEvidence 'Assets/Plugins/ES/AICommands/AICommandCatalog.json'
$authorityArtifactPaths = @(
    'AGENTS.md',
    '.agents/skills/es-skill-governance/references/user-directed-low-risk-policy.json',
    '.agents/skills/es-skill-governance/scripts/Test-ESUserDirectedLowRiskPolicy.ps1',
    '.agents/skills/es-skill-governance/references/user-directed-action-authority.md',
    '.agents/tests/Test-ESUserDirectedActionAuthority.ps1'
)
$authorityArtifactEvidence = [ordered]@{}
foreach ($relative in $authorityArtifactPaths) {
    $authorityArtifactEvidence[$relative] = Get-ReceiptEvidence $relative
}
$missingAuthorityArtifacts = @($authorityArtifactPaths | Where-Object { -not $authorityArtifactEvidence[$_].reportExists })
$deliveryArtifacts = @(
    'AGENTS.md',
    '.agents/skills/es-skill-governance/references/user-directed-low-risk-policy.json',
    '.agents/skills/es-skill-governance/scripts/Test-ESUserDirectedLowRiskPolicy.ps1',
    '.agents/skills/es-skill-governance/references/user-directed-action-authority.md',
    '.agents/tests/Test-ESUserDirectedActionAuthority.ps1',
    '.agents/skills/es-skill-governance/scripts/Test-ESCommercialCoherence.ps1',
    '.agents/skills/es-skill-governance/scripts/Test-ESStaticAcceptanceCoverage.ps1',
    '.agents/skills/es-skill-governance/scripts/Test-ESRuntimeAuthorizationContract.ps1',
    '.agents/skills/es-knowledge-validator/scripts/Export-ESKnowledgeRefreshPlan.ps1',
    '.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeStableRefresh.ps1',
    '.agents/skills/es-skill-governance/references/commercial-coherence-contract.md',
    '.agents/tests/Test-ESCommercialDeliveryTracking.ps1'
)
$deliveryArtifactVersionStates = @($deliveryArtifacts | ForEach-Object { Get-ESDeliveryArtifactVersionState -RepositoryRoot $root -RelativePath $_ })
$deliveryReviewArtifacts = @($deliveryArtifactVersionStates | Where-Object { $_.versionState -ne 'committed-clean' })
$untrackedDeliveryArtifacts = @($deliveryArtifactVersionStates | Where-Object { $_.versionState -eq 'untracked' } | ForEach-Object { $_.path })
$deliveryStateCounts = [ordered]@{}
foreach ($stateGroup in @($deliveryArtifactVersionStates | Group-Object versionState | Sort-Object Name)) {
    $deliveryStateCounts[[string]$stateGroup.Name] = [int]$stateGroup.Count
}
$deliveryTrackingStatus = if ($deliveryReviewArtifacts.Count -eq 0) { 'passed' } else { 'review' }
$snapshotAfter = Get-CoherenceSnapshot $root
$snapshotStable = [string]::Equals($snapshotBefore.snapshotHash, $snapshotAfter.snapshotHash, [StringComparison]::OrdinalIgnoreCase)
$knowledgeStaticStatus = if ($knowledge.PSObject.Properties['staticStatus']) { [string]$knowledge.staticStatus } else { 'validator-error' }
$knowledgeRuntimeStatus = if ($knowledge.PSObject.Properties['runtimeStatus']) { [string]$knowledge.runtimeStatus } else { 'not-run' }
$knowledgeFindingCount = if ($knowledge.PSObject.Properties['findingCount']) { [int]$knowledge.findingCount } else { 1 }
$knowledgeFindingCodes = if ($knowledge.PSObject.Properties['findings']) { @($knowledge.findings | ForEach-Object { [string]$_.code }) } else { @('validator-error') }
$freshnessOnlyCodes = @('SOURCE_HASH_DRIFT', 'CONTENT_HASH_MISMATCH')
$knowledgeFreshnessOnly = ($knowledgeFindingCodes.Count -gt 0 -and @($knowledgeFindingCodes | Where-Object { $_ -notin $freshnessOnlyCodes }).Count -eq 0)
$knowledgeAggregateStatus = if ($knowledgeFreshnessOnly) { 'review' } else { [string]$knowledge.status }
$compatibilityClaims = if ($compatibility.PSObject.Properties['claimsNotProven']) { @($compatibility.claimsNotProven) } else { @('compatibility-validator-error') }
$architectureRawStatus = if ($architecture.PSObject.Properties['status']) { [string]$architecture.status } else { 'validator-error' }
$architectureReportedLane = if ($architecture.PSObject.Properties['authorizationLane']) { [string]$architecture.authorizationLane } else { '' }
$architectureInvocationStatus = if ($architectureInvocation.PSObject.Properties['status']) { [string]$architectureInvocation.status } else { 'validator-error' }
$architectureInvocationLane = if ($architectureInvocation.PSObject.Properties['authorizationLane']) { [string]$architectureInvocation.authorizationLane } else { '' }
$architectureLaneMatched = [string]::Equals($architectureReportedLane, $managedAuthorizationLane, [StringComparison]::OrdinalIgnoreCase) -and
    [string]::Equals($architectureInvocationLane, $managedAuthorizationLane, [StringComparison]::OrdinalIgnoreCase)
$architectureStatus = if ($architectureLaneMatched) { $architectureRawStatus } else { 'blocked' }
$architectureFailureClass = if (-not $architectureLaneMatched) { 'authorization-lane-mismatch' } elseif ($architecture.PSObject.Properties['failureClass']) { [string]$architecture.failureClass } else { '' }
$authorityRawStatus = if ($authorityRegression.PSObject.Properties['status']) { [string]$authorityRegression.status } else { 'validator-error' }
$authorityCaseCount = if ($authorityRegression.PSObject.Properties['caseCount']) { [int]$authorityRegression.caseCount } else { 0 }
$authorityFailedCount = if ($authorityRegression.PSObject.Properties['failedCount']) { [int]$authorityRegression.failedCount } else { 1 }
$authorityStatus = if ($authorityRawStatus -eq 'static-passed' -and $authorityFailedCount -eq 0 -and $missingAuthorityArtifacts.Count -eq 0) { 'passed' } else { 'blocked' }
$authorityClaimsNotProven = if ($authorityRegression.PSObject.Properties['claimsNotProven']) { @($authorityRegression.claimsNotProven) } else { @('authority-regression-validator-error') }

$checks = [ordered]@{
    snapshotStability = [ordered]@{ status = if ($snapshotStable) { 'passed' } else { 'blocked' }; before = $snapshotBefore.snapshotHash; after = $snapshotAfter.snapshotHash; detail = if ($snapshotStable) { 'Governance surfaces remained unchanged during this audit.' } else { 'Governance surfaces changed during this audit; sub-results belong to different source generations.' } }
    skillArchitecture = [ordered]@{ authorizationLane = $managedAuthorizationLane; invocationAuthorizationLane = $architectureInvocationLane; reportedAuthorizationLane = $architectureReportedLane; invocationStatus = $architectureInvocationStatus; laneMatched = $architectureLaneMatched; status = $architectureStatus; rawStatus = $architectureRawStatus; reportPath = 'ES/Output/SkillArchitecture/architecture.json'; reportExists = $architectureEvidence.reportExists; reportHash = $architectureEvidence.reportHash; blockedCount = if ($architecture.PSObject.Properties['blockedCount']) { [int]$architecture.blockedCount } else { 1 }; reviewCount = if ($architecture.PSObject.Properties['reviewCount']) { [int]$architecture.reviewCount } else { 0 }; failureClass = $architectureFailureClass }
    userDirectedActionAuthority = [ordered]@{ authorizationLane = 'CurrentUserDirect'; status = $authorityStatus; rawStatus = $authorityRawStatus; validatorPath = '.agents/tests/Test-ESUserDirectedActionAuthority.ps1'; caseCount = $authorityCaseCount; failedCount = $authorityFailedCount; missingArtifactCount = $missingAuthorityArtifacts.Count; missingArtifacts = $missingAuthorityArtifacts; artifactEvidence = $authorityArtifactEvidence; claimsNotProven = $authorityClaimsNotProven }
    aiCommands = [ordered]@{ authorizationLane = $managedAuthorizationLane; catalogPath = $commandSummary.catalogPath; catalogExists = $catalogEvidence.reportExists; catalogHash = $catalogEvidence.reportHash; total = $commandSummary.total; navigation = $commandSummary.navigation; catalog = $commandSummary.catalog; invalid = $commandSummary.invalid; status = $commandSummary.status }
    esAutomationCompatibility = [ordered]@{ authorizationLane = $managedAuthorizationLane; status = [string]$compatibility.status; reportPath = 'ES/Output/AutomationGovernance/compatibility.json'; reportExists = $compatibilityEvidence.reportExists; reportHash = $compatibilityEvidence.reportHash; claimsNotProven = $compatibilityClaims }
    aiKnowledge = [ordered]@{ status = $knowledgeAggregateStatus; rawStatus = [string]$knowledge.status; reportPath = $knowledgeReportPath; reportExists = $knowledgeEvidence.reportExists; reportHash = $knowledgeEvidence.reportHash; staticStatus = $knowledgeStaticStatus; runtimeStatus = $knowledgeRuntimeStatus; findingCount = $knowledgeFindingCount; findingCodes = $knowledgeFindingCodes; freshnessOnly = $knowledgeFreshnessOnly; failureClass = if ($knowledge.PSObject.Properties['failureClass']) { [string]$knowledge.failureClass } else { '' } }
    knowledgeSourceFreshness = [ordered]@{ status = if ([int]$refreshPlan.findingCount -eq 0) { 'passed' } elseif ([int]$refreshPlan.unstableFindingCount -gt 0) { 'review' } else { 'review' }; reportPath = 'ES/Output/KnowledgeValidation/commercial-refresh-plan.json'; findingCount = [int]$refreshPlan.findingCount; unstableFindingCount = [int]$refreshPlan.unstableFindingCount; nextAction = [string]$refreshPlan.nextAction }
    staticAcceptanceCoverage = [ordered]@{ status = [string]$coverage.status; reportPath = 'ES/Output/Governance/static-acceptance-coverage.json'; reportExists = $coverageEvidence.reportExists; reportHash = $coverageEvidence.reportHash; skillCount = [int]$coverage.skillCount; coveredSkillCount = [int]$coverage.coveredSkillCount; blockedSkillCount = [int]$coverage.blockedSkillCount }
    deliveryTracking = [ordered]@{
        status = $deliveryTrackingStatus
        requiredArtifactCount = $deliveryArtifacts.Count
        committedCleanArtifactCount = @($deliveryArtifactVersionStates | Where-Object { $_.versionState -eq 'committed-clean' }).Count
        reviewArtifactCount = $deliveryReviewArtifacts.Count
        reviewArtifacts = @($deliveryReviewArtifacts | ForEach-Object { $_.path })
        untrackedArtifactCount = $untrackedDeliveryArtifacts.Count
        untrackedArtifacts = $untrackedDeliveryArtifacts
        versionStateCounts = $deliveryStateCounts
        artifactVersionStates = $deliveryArtifactVersionStates
        nextAction = if ($deliveryTrackingStatus -eq 'passed') { 'Current worktree bytes for every commercial governance artifact are carried by the current local HEAD commit.' } else { 'Review each artifact versionState. Staging or index membership alone is insufficient; current worktree bytes must be carried unchanged by the current HEAD commit before a versioned-delivery claim.' }
    }
}
$blocked = @($checks.Values | Where-Object { $_.status -eq 'blocked' }).Count
$review = @($checks.Values | Where-Object { $_.status -eq 'review' }).Count
$overall = if ($blocked -eq 0 -and $review -eq 0) { 'static-coherent' } else { 'static-review-required' }
$claimsProven = @(
    'Skill responsibility-specific static acceptance coverage',
    'AICommand catalog and risk metadata consistency',
    'ES AIBrain/Facade/Bridge/TaskContract compatibility',
    'CapabilityEnvelope, PlanHash, idempotency and CompletionDecision source contracts',
    'Runtime authorization contract fixture boundaries without starting Runtime',
    'Knowledge SourceRef and ContentHash validation semantics',
    'Governance receipt existence and report hashes'
)
if ($authorityStatus -eq 'passed') {
    $claimsProven += 'Current-user-direct authorization scope closure, action-specific denial, and control-plane path neutrality'
}
if ($deliveryTrackingStatus -eq 'passed') {
    $claimsProven += 'Current worktree bytes for critical governance artifacts are carried by the current local HEAD commit'
}
$report = [ordered]@{
    schemaVersion = 2
    toolId = 'es-commercial-coherence'
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    snapshotHash = $snapshotBefore.snapshotHash
    snapshotStable = $snapshotStable
    snapshotFilesBefore = $snapshotBefore.files
    snapshotFilesAfter = $snapshotAfter.files
    profile = 'StaticReview'
    authorizationLane = $managedAuthorizationLane
    laneCoverage = [ordered]@{ aggregate = $managedAuthorizationLane; managedChecks = @('skillArchitecture', 'aiCommands', 'esAutomationCompatibility'); directChecks = @('userDirectedActionAuthority') }
    mutatesSources = $false
    startsRuntime = $false
    overallVerdict = $overall
    checks = $checks
    blockedCheckCount = $blocked
    reviewCheckCount = $review
    claimsProven = $claimsProven
    claimsNotProven = @('Unity/editor runtime behavior', 'external process/network behavior', 'Profiler/Player/IL2CPP/release behavior', 'Remote publication or cross-machine availability of the current local HEAD commit')
    nextAction = if ($overall -eq 'static-coherent') { 'Static governance surfaces are coherent; runtime requires separate authorized evidence.' } elseif ($blocked -eq 0) { 'Resolve each review finding before claiming static coherence; runtime remains a separate evidence axis.' } else { 'Review each blocked check and resolve its own contract findings; do not treat this aggregate as a replacement for the underlying receipt.' }
}
$output = $outputTarget.FullPath
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $output) | Out-Null
$output = (Resolve-ESCommercialOutputPath -Candidate $outputTarget.RelativePath).FullPath
$temporary = "$output.tmp-$([Guid]::NewGuid().ToString('N'))"
try {
    [IO.File]::WriteAllText($temporary, ($report | ConvertTo-Json -Depth 12), (New-Object Text.UTF8Encoding($false)))
    Move-Item -LiteralPath $temporary -Destination $output -Force
} finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force } }
$report | ConvertTo-Json -Depth 12
