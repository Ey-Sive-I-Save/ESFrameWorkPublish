[CmdletBinding()]
param(
    [string]$PolicyPath,
    [string]$IntentPath
)

$ErrorActionPreference='Stop'
$scriptRoot=$PSScriptRoot
if([string]::IsNullOrWhiteSpace($PolicyPath)){$PolicyPath=Join-Path $scriptRoot '..\Contracts\es-task-context-runtime-integration-policy-v1.json'}
if([string]::IsNullOrWhiteSpace($IntentPath)){$IntentPath=Join-Path $scriptRoot '..\Contracts\es-task-context-runtime-intent-v1.json'}
$strictUtf8=[Text.UTF8Encoding]::new($false,$true)
$results=[Collections.Generic.List[object]]::new()

function Read-StrictJson([string]$Path){
    $resolved=(Resolve-Path -LiteralPath $Path).Path
    return $strictUtf8.GetString([IO.File]::ReadAllBytes($resolved))|ConvertFrom-Json -ErrorAction Stop
}

function Add-Case([string]$Name,[bool]$Passed,[string[]]$Findings){
    [void]$results.Add([pscustomobject]@{case=$Name;status=if($Passed){'passed'}else{'failed'};findings=@($Findings)})
}

function Test-ExactSet([string[]]$Actual,[string[]]$Expected){
    $actualValues=@($Actual|ForEach-Object{[string]$_}|Sort-Object -Unique)
    $expectedValues=@($Expected|Sort-Object -Unique)
    return $actualValues.Count-eq$expectedValues.Count-and(($actualValues-join'|')-ceq($expectedValues-join'|'))
}

$policy=Read-StrictJson $PolicyPath
$intent=Read-StrictJson $IntentPath
$coreProfiles=@('StaticReview','EngineeringReadiness')
$allProfiles=@('StaticReview','EngineeringReadiness','RuntimeAcceptance','ReleaseAcceptance')
$expectedProhibited=@(
    'skill.global-auto-wrap',
    'adapter.direct-lifecycle-ownership',
    'discovery.implies-business-execution',
    'automation.accepted-projects-completion',
    'profile.global-runtime-release-gate'
)
$expectedConditional=@(
    'aibrain.run-task',
    'automation-facade.task-endpoint',
    'worker.evidence-set-adapter',
    'codex-session.context-adapter',
    'semantic-archive.context-adapter',
    'unity-editor.task-creation-adapter'
)
$expectedForbiddenTerms=@(
    'AIBrain runTask',
    'ESAutomationFacade',
    'Worker EvidenceSet',
    'Codex Session adapter',
    'Semantic Archive adapter',
    'Unity Editor task creation',
    'all Skills in TaskContextRuntime'
)
$expectedAcceptanceExclusions=@(
    'prohibited-capability-absence',
    'unselected-conditional-capability-absence',
    'core-profile-runtime-not-run',
    'delivery-acceptance-pending',
    'automation-accepted-not-completion'
)

$inventoryFindings=[Collections.Generic.List[string]]::new()
if([int]$policy.schemaVersion-ne1){[void]$inventoryFindings.Add('schemaVersion must be 1.')}
if([string]$policy.policyId-cne'es.task-context-runtime.integration-acceptance.v1'){[void]$inventoryFindings.Add('policyId drifted.')}
if(-not(Test-ExactSet @($policy.coreAcceptanceExclusions) $expectedAcceptanceExclusions)){[void]$inventoryFindings.Add('Core acceptance exclusion inventory drifted.')}
$prohibitedIds=@($policy.prohibitedCapabilities|ForEach-Object{[string]$_.capabilityId})
$conditionalIds=@($policy.conditionalCapabilities|ForEach-Object{[string]$_.capabilityId})
$profileIds=@($policy.profileRules|ForEach-Object{[string]$_.profileId})
if(-not(Test-ExactSet $prohibitedIds $expectedProhibited)){[void]$inventoryFindings.Add('Prohibited capability inventory drifted.')}
if(-not(Test-ExactSet $conditionalIds $expectedConditional)){[void]$inventoryFindings.Add('Conditional capability inventory drifted.')}
if(-not(Test-ExactSet $profileIds $allProfiles)){[void]$inventoryFindings.Add('Verification profile inventory drifted.')}
if(-not(Test-ExactSet @($policy.coreAcceptanceForbiddenTerms) $expectedForbiddenTerms)){[void]$inventoryFindings.Add('Core acceptance forbidden-term inventory drifted.')}
if(@($prohibitedIds+$conditionalIds|Sort-Object -Unique).Count-ne($prohibitedIds.Count+$conditionalIds.Count)){[void]$inventoryFindings.Add('Capability IDs are not unique.')}
if(@($policy.prohibitedCapabilities|ForEach-Object{[string]$_.reasonCode}|Where-Object{$_-match'^[A-Z][A-Z0-9_]+$'}|Sort-Object -Unique).Count-ne$expectedProhibited.Count){[void]$inventoryFindings.Add('Prohibited capabilities require unique stable reasonCode values.')}
Add-Case 'stable-capability-inventory' ($inventoryFindings.Count-eq0) @($inventoryFindings)

$coreFindings=[Collections.Generic.List[string]]::new()
foreach($profileId in $coreProfiles){
    $profile=@($policy.profileRules|Where-Object{[string]$_.profileId-ceq$profileId})
    if($profile.Count-ne1){[void]$coreFindings.Add("Profile rule must occur exactly once: $profileId");continue}
    if(@($profile[0].requiredCapabilityIds).Count-ne0){[void]$coreFindings.Add("Core profile must not require integration capabilities: $profileId")}
    if([string]$profile[0].missingConditionalCapability-cne'non-blocking'){[void]$coreFindings.Add("Conditional gaps must be non-blocking: $profileId")}
    if([string]$profile[0].runtimeNotRun-cne'non-blocking'){[void]$coreFindings.Add("runtime-not-run must be non-blocking: $profileId")}
}
foreach($capability in @($policy.prohibitedCapabilities)){
    if(-not(Test-ExactSet @($capability.nonBlockingProfiles) $allProfiles)){[void]$coreFindings.Add("Prohibited capability absence must be non-blocking in every profile: $($capability.capabilityId)")}
}
Add-Case 'core-profile-isolation' ($coreFindings.Count-eq0) @($coreFindings)

$selectionFindings=[Collections.Generic.List[string]]::new()
if([string]$policy.selectionContract.field-cne'requiredCapabilityIds'){[void]$selectionFindings.Add('Selection field must be requiredCapabilityIds.')}
if(@($policy.selectionContract.default).Count-ne0){[void]$selectionFindings.Add('Conditional capability selection must default to empty.')}
foreach($capability in @($policy.conditionalCapabilities)){
    if($capability.requiredOnlyWhenSelected-ne$true){[void]$selectionFindings.Add("Capability is not selection-gated: $($capability.capabilityId)")}
    if(-not(Test-ExactSet @($capability.nonBlockingProfiles) $coreProfiles)){[void]$selectionFindings.Add("Core non-blocking profiles drifted: $($capability.capabilityId)")}
    if(-not(Test-ExactSet @($capability.eligibleProfiles) @('RuntimeAcceptance','ReleaseAcceptance'))){[void]$selectionFindings.Add("Eligible profiles drifted: $($capability.capabilityId)")}
}
foreach($profileId in @('RuntimeAcceptance','ReleaseAcceptance')){
    $profile=@($policy.profileRules|Where-Object{[string]$_.profileId-ceq$profileId})
    if($profile.Count-ne1-or[string]$profile[0].missingConditionalCapability-cne'blocking-only-when-selected'){
        [void]$selectionFindings.Add("Runtime/Release profile is not selection-scoped: $profileId")
        continue
    }
    $selected=@($profile[0].requiredCapabilityIds|ForEach-Object{[string]$_})
    $unknown=@($selected|Where-Object{$conditionalIds-notcontains$_})
    if($unknown.Count){[void]$selectionFindings.Add("Profile selects unknown or prohibited capabilities: $profileId=$($unknown-join',')")}
    if($selected-contains'aibrain.run-task'-and$selected-notcontains'automation-facade.task-endpoint'){
        [void]$selectionFindings.Add("Profile selects aibrain.run-task without automation-facade.task-endpoint: $profileId")
    }
}
Add-Case 'conditional-selection' ($selectionFindings.Count-eq0) @($selectionFindings)

$aibrain=@($policy.conditionalCapabilities|Where-Object{[string]$_.capabilityId-ceq'aibrain.run-task'})
$dependencyPassed=$aibrain.Count-eq1-and(Test-ExactSet @($aibrain[0].requiresCapabilities) @('automation-facade.task-endpoint'))
Add-Case 'aibrain-facade-dependency' $dependencyPassed $(if($dependencyPassed){@()}else{@('aibrain.run-task must require automation-facade.task-endpoint.')})

$acceptanceText=@($intent.acceptanceSignals|ForEach-Object{[string]$_})-join"`n"
$leakedIds=@($expectedProhibited+$expectedConditional|Where-Object{$acceptanceText.Contains($_)})
$leakedTerms=@($expectedForbiddenTerms|Where-Object{$acceptanceText.IndexOf($_,[StringComparison]::OrdinalIgnoreCase)-ge0})
$acceptanceFindings=@($leakedIds|ForEach-Object{"Integration capability leaked into Core acceptanceSignals: $_"})+@($leakedTerms|ForEach-Object{"Integration requirement term leaked into Core acceptanceSignals: $_"})
Add-Case 'core-acceptance-signal-separation' ($acceptanceFindings.Count-eq0) $acceptanceFindings

$forbiddenText=@($intent.forbiddenTransitions|ForEach-Object{[string]$_})-join"`n"
$automationSeparated=$forbiddenText.Contains('Automation run Accepted')-and$forbiddenText.Contains('global Skill auto-wrapping')
Add-Case 'forbidden-projection-and-auto-wrap' $automationSeparated $(if($automationSeparated){@()}else{@('Intent must forbid Automation Accepted projection and global Skill auto-wrapping.')})

$exclusionFindings=[Collections.Generic.List[string]]::new()
$intentNonGoalText=@($intent.nonGoals|ForEach-Object{[string]$_})-join"`n"
foreach($requiredNonGoal in @('deliveryAcceptance=pending','ESAutomationRunStatus.Accepted')){
    if(-not $intentNonGoalText.Contains($requiredNonGoal)){
        [void]$exclusionFindings.Add("Intent nonGoals must preserve: $requiredNonGoal")
    }
}
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\\..\\..')).Path
$manifestPath=Join-Path $repoRoot '.agents\\skills\\es-task-context-runtime\\static-replay.manifest.json'
if(Test-Path -LiteralPath $manifestPath){
    $manifest=Read-StrictJson $manifestPath
    if(-not(Test-ExactSet @($manifest.acceptanceExclusions) $expectedAcceptanceExclusions)){
        [void]$exclusionFindings.Add('StaticReplay acceptanceExclusions must match the policy inventory.')
    }
}else{[void]$exclusionFindings.Add('StaticReplay manifest is missing.')}
Add-Case 'acceptance-exclusion-lock' ($exclusionFindings.Count-eq0) @($exclusionFindings)

$failed=@($results|Where-Object{$_.status-eq'failed'})
[pscustomobject][ordered]@{
    schemaVersion=1
    validator='Test-ESTaskContextRuntimeIntegrationPolicy'
    status=if($failed.Count){'failed'}else{'passed'}
    caseCount=$results.Count
    passedCount=@($results|Where-Object{$_.status-eq'passed'}).Count
    failedCount=$failed.Count
    cases=@($results)
    policyPath=(Resolve-Path -LiteralPath $PolicyPath).Path
    intentPath=(Resolve-Path -LiteralPath $IntentPath).Path
    runtimeStatus='runtime-not-run'
    claimsNotProven=@('Any conditional adapter Runtime behavior','AIBrain runTask availability','Release acceptance')
}|ConvertTo-Json -Depth 10
if($failed.Count){exit 1}
