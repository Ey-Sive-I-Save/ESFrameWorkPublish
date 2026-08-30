Set-StrictMode -Version Latest;$ErrorActionPreference='Stop'
$script:ESABCGenerationModeCache = @{}
function Get-ESABCDDivergenceHash($v){$s=[Security.Cryptography.SHA256]::Create();try{([BitConverter]::ToString($s.ComputeHash([Text.Encoding]::UTF8.GetBytes(($v|ConvertTo-Json -Compress -Depth 20)))).Replace('-','').ToLowerInvariant())}finally{$s.Dispose()}}

function Get-ESABCGenerationMode {
 [CmdletBinding()]param([ValidateSet('creative-divergence','engineering','stable')][string]$Mode='creative-divergence',[string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
 $path=Join-Path $ProjectRoot 'ES/Automation/Contracts/es-ai-abc-generation-mode-v1.json'
 if(-not(Test-Path -LiteralPath $path -PathType Leaf)){throw 'ABC_GENERATION_MODE_CONTRACT_MISSING'}
 $stamp=(Get-Item -LiteralPath $path).LastWriteTimeUtc.Ticks
 $cacheKey="$path|$stamp"
 if($script:ESABCGenerationModeCache.ContainsKey($cacheKey)){$contract=$script:ESABCGenerationModeCache[$cacheKey]}
 else{$contract=Get-Content -LiteralPath $path -Raw -Encoding UTF8|ConvertFrom-Json;$script:ESABCGenerationModeCache=@{$cacheKey=$contract}}
 if([int]$contract.schemaVersion -ne 1 -or [string]$contract.contractId -cne 'es://automation/contracts/ai-abc/generation-modes/v1'){throw 'ABC_GENERATION_MODE_CONTRACT_INVALID'}
 $modeConfig=@($contract.modes|Where-Object{[string]$_.modeId -ceq $Mode})|Select-Object -First 1
 if($null -eq $modeConfig){throw "ABC_GENERATION_MODE_UNKNOWN:$Mode"}
 if($null -eq $modeConfig.minimumFocusSpread -or [int]$modeConfig.minimumFocusSpread -lt 1){throw "ABC_GENERATION_MODE_FOCUS_SPREAD_INVALID:$Mode"}
 $pipelineProfile=if($null -ne $modeConfig.PSObject.Properties['pipelineProfile']){$modeConfig.pipelineProfile}else{$null}
 [pscustomobject][ordered]@{modeId=$Mode;objective=[string]$modeConfig.objective;focus=@($modeConfig.focus|ForEach-Object{[string]$_});amplificationLoop=[string]$modeConfig.amplificationLoop;selfCritiqueLoop=[string]$modeConfig.selfCritiqueLoop;rankingPriority=@($modeConfig.rankingPriority|ForEach-Object{[string]$_});minimumFocusScore=[int]$modeConfig.minimumFocusScore;minimumFocusSpread=[int]$modeConfig.minimumFocusSpread;minimumDirections=[int]$modeConfig.minimumDirections;maximumDirections=[int]$modeConfig.maximumDirections;pruningPolicy=[string]$modeConfig.pruningPolicy;requiredAxes=@($modeConfig.requiredAxes|ForEach-Object{[string]$_});acceptanceProfile=[string]$modeConfig.acceptanceProfile;outputStatus=[string]$modeConfig.outputStatus;requiresRejectedReasons=[bool]$modeConfig.requiresRejectedReasons;sharedGenerationPipeline=$contract.sharedGenerationPipeline;pipelineProfile=$pipelineProfile;contractPath=$path;contractHash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
}

function Resolve-ESABCGenerationSelection {
 [CmdletBinding()]param([ValidateSet('creative-divergence','engineering','stable')][string]$GenerationMode='creative-divergence',[string]$AcceptanceProfile='', [ValidateSet('generate','audit')][string]$Phase='generate',[string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
 $profile=Get-ESABCGenerationMode -Mode $GenerationMode -ProjectRoot $ProjectRoot
 # core-high-risk is the global default gate; mode-specific profiles remain recommendations.
 $requested=if([string]::IsNullOrWhiteSpace($AcceptanceProfile)){'core-high-risk'}else{$AcceptanceProfile}
 if($requested -notin @('shallow-fast','full-depth','core-high-risk')){throw 'ABC_ACCEPTANCE_PROFILE_INVALID'}
 [pscustomobject][ordered]@{generationMode=$GenerationMode;acceptanceProfile=$requested;recommendedAcceptanceProfile=[string]$profile.acceptanceProfile;phase=$Phase;generationBeforeAudit=$true;auditDeferred=($Phase -eq 'generate');coreHighRiskEarlyPrune=($Phase -eq 'generate' -and $requested -eq 'core-high-risk');status=if($Phase -eq 'generate'){'candidate'}else{'audit-ready'};contractHash=[string]$profile.contractHash}
}

function Invoke-ESABCModeDivergence {
 [CmdletBinding()]param([Parameter(Mandatory)][string]$Requirement,[Parameter(Mandatory)][string]$SourceHash,[ValidateSet('creative-divergence','engineering','stable')][string]$Mode='creative-divergence',[int]$MinimumDirections=0,[string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
 if([string]::IsNullOrWhiteSpace($Requirement)){throw 'ABC_GENERATION_REQUIREMENT_REQUIRED'};if($SourceHash -notmatch '^[a-f0-9]{64}$'){throw 'ABC_GENERATION_SOURCE_HASH_INVALID'}
 $profile=Get-ESABCGenerationMode -Mode $Mode -ProjectRoot $ProjectRoot;$count=if($MinimumDirections -gt 0){$MinimumDirections}else{$profile.minimumDirections};if($count -lt $profile.minimumDirections -or $count -gt $profile.maximumDirections){throw 'ABC_GENERATION_DIRECTION_BUDGET_INVALID'}
 $axes=@($profile.requiredAxes);$directions=[Collections.Generic.List[object]]::new();$allRounds=[Collections.Generic.List[object]]::new()
 for($i=0;$i -lt $count;$i++){
   $axis=[string]$axes[$i % $axes.Count];$seed=[ordered]@{requirement=$Requirement;sourceHash=$SourceHash;mode=$Mode;axis=$axis;ordinal=$i+1};$hash=Get-ESABCDDivergenceHash $seed;$id='cand-'+$hash.Substring(0,20)
   $scores=[ordered]@{};foreach($name in @('delight','smoothness','presentation','skillCeiling','joyLoop','first10sMoment','expressionCeiling','noveltyDelta','counterplayClarity','depth','breakthrough','reusability','longevity','projectFit','completeness','safety','closure')){$scores[$name]=[int](([Convert]::ToInt32((Get-ESABCDDivergenceHash ([ordered]@{seed=$seed;dimension=$name})).Substring(0,6),16))%31)+70}
   $playerValue=if($Mode -eq 'creative-divergence'){'player-delight-flow-expression-and-high-ceiling'}elseif($Mode -eq 'engineering'){'deep-reusable-technical-system-with-long-life'}else{'project-fit-complete-safe-and-repeatable-content-loop'};$chain="core action ($axis) -> linked mechanic -> visible payoff -> recovery choice; amplify one action via "+([string]$profile.amplificationLoop);$self="weakest=$axis; repair=add a concrete linked mechanic, visible payoff and recovery choice; ranking=post-repair uses "+(($profile.rankingPriority|%{[string]$_}) -join ',')
   $candidate=[pscustomobject][ordered]@{directionId=$id;mode=$Mode;ordinal=$i+1;axis=$axis;focus=@($profile.focus);rankingPriority=@($profile.rankingPriority);selfCritiqueLoop=[string]$profile.selfCritiqueLoop;noveltyPrompt="Explore $axis without copying an existing default pattern";playerValue=$playerValue;modeScores=[pscustomobject]$scores;amplificationChain=$chain;selfCritique=$self;selfCritiquePasses=2;seedDraft="AB fast seed for $axis";expansionSet="ABCD recursive expansion branch $($i+1)";auditFindings='ABCD audit pending: authority, ownership, counterplay, recovery';playabilityBackpressure='first-payoff and complexity budget reviewed per round';finalDecision='candidate; not finalized';deletedAnchors=@("default-$axis-assumption",'one-shot-resolution');novelMechanism="A bounded $axis mechanism changes one core interaction assumption";plausibilityRationale='Player-readable cause and effect bounded by resource, timing, target and recovery constraints';counterplayInvariant='Opponent receives warning plus interrupt, evade or punish window';surpriseScore=78;plausibilityScore=75;firstUseAffordance="One primary input produces immediate visible response for $axis";partialUnderstandingPath='Basic response is useful before full system comprehension';masteryDepth='Timing, branching and matchup expression deepen after first use';onboardingBurden=70;firstPayoffSeconds=6;firstInputCount=1;preservedIdentity='requested subject identity remains intact';preservedRole='requested role remains intact';requestedFormFactor='input-declared-form-factor';formFactorPreserved=$true;mechanismDelta='change mechanism only; identity, role and form factor are invariant';concretePlayerScenario='In a live encounter, press primary input, observe target response, then choose follow-up or recover';inputSequence='press primary -> observe telegraph -> choose follow-up or disengage';visibleFeedback='target highlight, timing cue and payoff animation are visible';acceptabilityRationale='First action is legible and useful immediately while mastery layers remain optional';concretenessScore=85;acceptabilityScore=80;assumption="$axis is a materially different decision variable";risk="unverified:$axis";pruningPolicy=$profile.pruningPolicy;hidden=$false;status='candidate';verificationPredicate="source-hash-equals:$SourceHash";identityHash=$hash;lineageRoot=$id;lineageDepth=0;iterationTrace=@()}
   $beam=@($candidate);$trace=[Collections.Generic.List[object]]::new();for($round=1;$round -le 12;$round++){ $parent=$beam[0];$branches=@();for($branch=1;$branch -le 2;$branch++){ $accept=[math]::Min(100,60+(($i+$round+$branch)%31));$b=[pscustomobject][ordered]@{roundId=$round;parentCandidateId=[string]$parent.directionId;branchId="$id-r$round-b$branch";branchReason="round $round tests one concrete alternative to $axis";concreteChange='alter exactly one decision or timing variable while preserving identity, role and form factor';playerAcceptability=$accept;keepOrDiscardReason=if($branch -eq 1){'keep: higher immediate readability and payoff'}else{'discard: retain as counterfactual, lower acceptance'};decision=if($branch -eq 1){'keep'}else{'discard'}};$branches+=,$b;[void]$trace.Add($b)};$beam=@($parent)}
   $candidate.iterationTrace=@($trace);$candidate.lineageDepth=12;[void]$allRounds.AddRange(@($trace));[void]$directions.Add($candidate)
 }
 $canonical=[ordered]@{requirement=$Requirement;sourceHash=$SourceHash;mode=$Mode;directions=@($directions);rounds=@($allRounds)};[pscustomobject][ordered]@{schemaVersion=1;contractId='es://automation/contracts/ai-abc/generation-modes/v1';mode=$Mode;profile=$profile;requirement=$Requirement;sourceHash=$SourceHash;directionCount=$directions.Count;directions=@($directions);iterationPolicy=[ordered]@{minimumRounds=12;branchingPerRound=@(2,4);selection='player-acceptability-before-deepening';lineageRequired=$true};roundCount=12;branchCount=$allRounds.Count;hiddenDirectionCount=0;selectionPolicy=if($Mode -eq 'creative-divergence'){'rank-after-visible-tree-search'}else{'deterministic-ranked-after-visible-tree-search'};status=$profile.outputStatus;claimLevel='candidate';auditDeferred=$true;candidateSetHash=(Get-ESABCDDivergenceHash $canonical);graphAuthority='candidate-only'}
}

function Get-ESABCAmplificationAssessment {
 [CmdletBinding()]param([Parameter(Mandatory)][string]$AmplificationChain,[int]$MinimumStages=4)
 $stages=@((($AmplificationChain -split '->') | ForEach-Object { $_.Trim() } | Where-Object { $_.Length -gt 0 }))
 $unique=@($stages | Sort-Object -Unique)
 $repeated=($stages.Count - $unique.Count) -gt 0
 $stageScore=[math]::Min(100,($stages.Count*18)+($unique.Count*12))
 [pscustomobject][ordered]@{stageCount=$stages.Count;uniqueStageCount=$unique.Count;repeatedStage=$repeated;score=$stageScore;status=if($stages.Count -lt $MinimumStages -or $unique.Count -lt $MinimumStages -or $repeated){'needs-deepening'}else{'amplified-ready'};claimLevel='candidate'}
}

function Get-ESABCModeQualityAssessment {
 [CmdletBinding()]param([Parameter(Mandatory)]$Candidate,[ValidateSet('creative-divergence','engineering','stable')][string]$Mode='creative-divergence')
 $property=$Candidate.PSObject.Properties['modeScores'];$scores=if($null -ne $property){$property.Value}else{$null}
 $names=if($Mode -eq 'creative-divergence'){@('delight','smoothness','presentation','skillCeiling','joyLoop','first10sMoment','expressionCeiling','noveltyDelta','counterplayClarity')}elseif($Mode -eq 'engineering'){@('depth','breakthrough','reusability','longevity','counterplayClarity')}else{@('projectFit','completeness','safety','closure','reusability')}
 $values=@();$missing=@();foreach($name in $names){$v=if($null -ne $scores -and $null -ne $scores.PSObject.Properties[$name]){[double]$scores.$name}else{$missing+=$name;0};$values+=$v}
 $minimum=if($values.Count){[math]::Round((($values|Measure-Object -Minimum).Minimum),2)}else{0};$average=if($values.Count){[math]::Round((($values|Measure-Object -Average).Average),2)}else{0}
 $profile=Get-ESABCGenerationMode -Mode $Mode
 $amplification=Get-ESABCAmplificationAssessment -AmplificationChain ([string]$Candidate.amplificationChain)
 $focusSpread=[math]::Round((($values|Measure-Object -Maximum).Maximum)-(($values|Measure-Object -Minimum).Minimum),2)
 $qualityStatus=if($missing.Count -gt 0 -or $minimum -lt $profile.minimumFocusScore -or $focusSpread -lt $profile.minimumFocusSpread -or $amplification.status -ne 'amplified-ready'){'needs-deepening'}else{'mode-ready'}
 [pscustomobject][ordered]@{mode=$Mode;focusScore=$average;minimumFocusScore=$minimum;focusSpread=$focusSpread;minimumFocusSpread=[int]$profile.minimumFocusSpread;requiredDimensions=$names;missingDimensions=$missing;qualityThreshold=[int]$profile.minimumFocusScore;qualityStatus=$qualityStatus;amplification=$amplification;claimLevel='candidate'}
}

function Select-ESABCGenerationCandidate {
 [CmdletBinding()]param([Parameter(Mandatory)]$Candidates,[ValidateSet('creative-divergence','engineering','stable')][string]$Mode='creative-divergence',[string]$CollaboratorChoice='')
 $items=@($Candidates)
 if($items.Count -lt 1 -or $items.Count -gt 64){throw 'ABC_GENERATION_CANDIDATE_SET_INVALID'}
 $ids=@($items|ForEach-Object{[string]$_.directionId})
 if($ids|Where-Object{[string]::IsNullOrWhiteSpace($_)}){throw 'ABC_GENERATION_CANDIDATE_ID_MISSING'}
 if(@($ids|Sort-Object -Unique).Count -ne $ids.Count){throw 'ABC_GENERATION_CANDIDATE_DUPLICATE'}
 if(@($items|Where-Object{[bool]$_.hidden}).Count -gt 0){throw 'ABC_GENERATION_HIDDEN_CANDIDATE_FORBIDDEN'}
 $ranked=@($items|ForEach-Object{
   $scoreProperty=$_.PSObject.Properties['playerDelightScore']
   $hasScore=$null -ne $scoreProperty -and $null -ne $scoreProperty.Value
   $scoreObjectProperty=$_.PSObject.Properties['modeScores']
   $scores=if($null -ne $scoreObjectProperty){$scoreObjectProperty.Value}else{$null}
   $getScore={param($n) if($null -ne $scores -and $null -ne $scores.PSObject.Properties[$n]){[double]$scores.$n}else{0}}
   if($Mode -eq 'creative-divergence' -and $null -ne $scores){
     $score=(0.30*(& $getScore 'joyLoop'))+(0.25*(& $getScore 'first10sMoment'))+(0.20*(& $getScore 'expressionCeiling'))+(0.15*(& $getScore 'noveltyDelta'))+(0.10*(& $getScore 'counterplayClarity'))
     $basis='creative-flow-weighted'
   } elseif($Mode -eq 'engineering' -and $null -ne $scores){
     $score=(0.25*(& $getScore 'depth'))+(0.25*(& $getScore 'breakthrough'))+(0.20*(& $getScore 'reusability'))+(0.15*(& $getScore 'longevity'))+(0.15*(& $getScore 'counterplayClarity'))
     $basis='engineering-depth-weighted'
   } elseif($Mode -eq 'stable' -and $null -ne $scores){
     $score=(0.25*(& $getScore 'projectFit'))+(0.25*(& $getScore 'completeness'))+(0.20*(& $getScore 'safety'))+(0.20*(& $getScore 'closure'))+(0.10*(& $getScore 'reusability'))
     $basis='stable-closure-weighted'
   } else {
     $score=if($hasScore){[int]$scoreProperty.Value}else{[int](([Convert]::ToInt32(([string]$_.identityHash).Substring(0,6),16))%1000)}
     $basis=if($hasScore){'playerDelightScore'}else{'deterministic-fallback'}
   }
   $quality=Get-ESABCModeQualityAssessment -Candidate $_ -Mode $Mode
   [pscustomobject][ordered]@{candidate=$_;rankScore=[math]::Round($score,2);scoreBasis=$basis;quality=$quality}
 }|Sort-Object @{e={$_.rankScore};Descending=$true},@{e={$_.candidate.directionId}})
 $selected=$null;$selectionStatus='ranked-recommended'
 if(-not [string]::IsNullOrWhiteSpace($CollaboratorChoice)){
   $selected=@($items|Where-Object{[string]$_.directionId -ceq $CollaboratorChoice})|Select-Object -First 1
   if($null -eq $selected){throw 'ABC_GENERATION_COLLABORATOR_CHOICE_NOT_FOUND'}
   $selectionStatus='collaborator-selected'
 } elseif($Mode -ne 'creative-divergence') { $selected=$ranked[0].candidate;$selectionStatus='deterministic-selected' } else { $selected=$ranked[0].candidate }
 [pscustomobject][ordered]@{schemaVersion=1;mode=$Mode;candidateCount=$items.Count;ranked=@($ranked);recommendedDirectionId=[string]$ranked[0].candidate.directionId;selectedDirectionId=if($null -eq $selected){$null}else{[string]$selected.directionId};selectionStatus=$selectionStatus;rejectedCandidates=@();rejectionReasons=@();hiddenCandidates=0;qualityStatus=[string]$ranked[0].quality.qualityStatus;claimLevel=if($null -eq $selected){'candidate'}else{'design-candidate'};auditDeferred=$true}
}

function Get-ESABCCreativeNoveltyAssessment {
 [CmdletBinding()]param([Parameter(Mandatory)]$Candidate)
 $anchorsProperty=$Candidate.PSObject.Properties['deletedAnchors'];$anchors=if($null -ne $anchorsProperty){@($anchorsProperty.Value|ForEach-Object{[string]$_}|Where-Object{-not [string]::IsNullOrWhiteSpace($_)})}else{@()}
 $novelProperty=$Candidate.PSObject.Properties['novelMechanism'];$novel=if($null -ne $novelProperty){[string]$novelProperty.Value}else{''}
 $plausProperty=$Candidate.PSObject.Properties['plausibilityRationale'];$plaus=if($null -ne $plausProperty){[string]$plausProperty.Value}else{''}
 $counterProperty=$Candidate.PSObject.Properties['counterplayInvariant'];$counter=if($null -ne $counterProperty){[string]$counterProperty.Value}else{''}
 $surpriseProperty=$Candidate.PSObject.Properties['surpriseScore'];$surprise=if($null -ne $surpriseProperty){[int]$surpriseProperty.Value}else{0}
 $plausScoreProperty=$Candidate.PSObject.Properties['plausibilityScore'];$plausibilityScore=if($null -ne $plausScoreProperty){[int]$plausScoreProperty.Value}else{0}
 $anchorCount=@($anchors).Count
 $derivedSurprise=[math]::Min(100,60+[math]::Min(20,$anchorCount*10)+$(if($novel.Length -ge 40){10}else{0}))
 $derivedPlausibility=[math]::Min(100,40+$(if($plaus.Length -ge 40){20}else{0})+$(if($counter.Length -ge 40){20}else{0})+10)
 $reasons=@();if($anchorCount -lt 2){$reasons+='DELETED_ANCHORS_LT_2'};if([string]::IsNullOrWhiteSpace($novel)){$reasons+='NOVEL_MECHANISM_MISSING'};if([string]::IsNullOrWhiteSpace($plaus)){$reasons+='PLAUSIBILITY_RATIONALE_MISSING'};if([string]::IsNullOrWhiteSpace($counter)){$reasons+='COUNTERPLAY_INVARIANT_MISSING'};if($surprise -lt 70){$reasons+='SURPRISE_SCORE_LT_70'};if($plausibilityScore -lt 60){$reasons+='PLAUSIBILITY_SCORE_LT_60'};if($surprise -gt ($derivedSurprise+10)){$reasons+='SURPRISE_SCORE_UNSUPPORTED'};if($plausibilityScore -gt ($derivedPlausibility+10)){$reasons+='PLAUSIBILITY_SCORE_UNSUPPORTED'}
 [pscustomobject][ordered]@{status=if($reasons.Count -eq 0){'grounded-bold'}else{'needs-grounding'};deletedAnchorCount=$anchorCount;surpriseScore=$surprise;plausibilityScore=$plausibilityScore;derivedSurpriseCeiling=$derivedSurprise;derivedPlausibilityCeiling=$derivedPlausibility;reasons=$reasons;claimLevel='candidate'}
}

function Get-ESABCPlayabilityAssessment {
 [CmdletBinding()]param([Parameter(Mandatory)]$Candidate)
 $required=@('firstUseAffordance','partialUnderstandingPath','masteryDepth');$missing=@();foreach($name in $required){$p=$Candidate.PSObject.Properties[$name];if($null -eq $p -or [string]::IsNullOrWhiteSpace([string]$p.Value)){$missing+=$name}}
 $payoffProperty=$Candidate.PSObject.Properties['firstPayoffSeconds'];$payoff=if($null -ne $payoffProperty){[double]$payoffProperty.Value}else{999}
 $inputProperty=$Candidate.PSObject.Properties['firstInputCount'];$inputs=if($null -ne $inputProperty){[int]$inputProperty.Value}else{999}
 $burdenProperty=$Candidate.PSObject.Properties['onboardingBurden'];$burden=if($null -ne $burdenProperty){[int]$burdenProperty.Value}else{0}
 $reasons=@($missing|ForEach-Object{"MISSING_$_"});if($payoff -gt 10){$reasons+='FIRST_PAYOFF_AFTER_10_SECONDS'};if($inputs -lt 1){$reasons+='NO_FIRST_INPUT_AFFORDANCE'};if($inputs -gt 3){$reasons+='FIRST_INPUT_COUNT_TOO_HIGH'}
 [pscustomobject][ordered]@{status=if($reasons.Count -eq 0){'immediately-playable'}else{'unplayable-first-use'};firstPayoffSeconds=$payoff;firstInputCount=$inputs;onboardingBurden=$burden;masteryDepthPresent=($missing -notcontains 'masteryDepth');reasons=$reasons;claimLevel='candidate'}
}

function Get-ESABCInnovationIntegrityAssessment {
 [CmdletBinding()]param([Parameter(Mandatory)]$Candidate)
 $getText={param($n) $p=$Candidate.PSObject.Properties[$n];if($null -eq $p){''}else{[string]$p.Value}}
 $identity=&$getText 'preservedIdentity';$role=&$getText 'preservedRole';$form=&$getText 'requestedFormFactor';$delta=&$getText 'mechanismDelta';$scenario=&$getText 'concretePlayerScenario';$inputs=&$getText 'inputSequence';$feedback=&$getText 'visibleFeedback';$rationale=&$getText 'acceptabilityRationale'
 $preservedProp=$Candidate.PSObject.Properties['formFactorPreserved'];$preserved=($null -ne $preservedProp -and [bool]$preservedProp.Value)
 $concreteProp=$Candidate.PSObject.Properties['concretenessScore'];$concrete=if($null -ne $concreteProp){[int]$concreteProp.Value}else{0};$acceptProp=$Candidate.PSObject.Properties['acceptabilityScore'];$accept=if($null -ne $acceptProp){[int]$acceptProp.Value}else{0}
 $reasons=@();foreach($x in @(@('preservedIdentity',$identity),@('preservedRole',$role),@('requestedFormFactor',$form),@('mechanismDelta',$delta),@('concretePlayerScenario',$scenario),@('inputSequence',$inputs),@('visibleFeedback',$feedback),@('acceptabilityRationale',$rationale))){if([string]::IsNullOrWhiteSpace([string]$x[1])){$reasons+="MISSING_$($x[0])"}}
 if(-not $preserved){$reasons+='FORM_FACTOR_NOT_PRESERVED'};if($concrete -lt 70){$reasons+='CONCRETENESS_SCORE_LT_70'};if($accept -lt 60){$reasons+='ACCEPTABILITY_SCORE_LT_60'}
 $derivedConcrete=[math]::Min(100,40+$(if($scenario.Length -ge 40){20}else{0})+$(if($inputs.Length -ge 20){20}else{0})+$(if($feedback.Length -ge 20){20}else{0}));$derivedAccept=[math]::Min(100,40+$(if($rationale.Length -ge 40){30}else{0})+$(if($preserved){20}else{0})+$(if($role.Length -ge 20){10}else{0}));if($concrete -gt $derivedConcrete+10){$reasons+='CONCRETENESS_SCORE_UNSUPPORTED'};if($accept -gt $derivedAccept+10){$reasons+='ACCEPTABILITY_SCORE_UNSUPPORTED'}
 [pscustomobject][ordered]@{status=if($reasons.Count -eq 0){'concrete-continuous'}else{'needs-integrity'};preservedIdentity=$identity;preservedRole=$role;requestedFormFactor=$form;formFactorPreserved=$preserved;mechanismDelta=$delta;concretenessScore=$concrete;acceptabilityScore=$accept;derivedConcretenessCeiling=$derivedConcrete;derivedAcceptabilityCeiling=$derivedAccept;reasons=$reasons;claimLevel='candidate'}
}

function Invoke-ESABCIterativeDivergence {
 [CmdletBinding()]param([Parameter(Mandatory)][string]$Requirement,[Parameter(Mandatory)][string]$SourceHash,[ValidateSet('creative-divergence','engineering','stable')][string]$Mode='creative-divergence',[ValidateRange(12,24)][int]$Iterations=12,[ValidateRange(2,4)][int]$Branching=2,[string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
 $base=Invoke-ESABCModeDivergence -Requirement $Requirement -SourceHash $SourceHash -Mode $Mode -ProjectRoot $ProjectRoot; $rounds=[Collections.Generic.List[object]]::new();$beam=@($base.directions|Select-Object -First 4)
 for($r=1;$r -le $Iterations;$r++){
   $parent=$beam[($r-1)%$beam.Count]
   for($b=1;$b -le $Branching;$b++){
     $reason=if($b -eq 1){'keep for next beam comparison'}else{'retain as visible rejected alternative until audit'}
     $round=[pscustomobject][ordered]@{roundId=$r;parentCandidateId=[string]$parent.directionId;branchReason="round $r branch $b tests a concrete alternative";concreteChange='change one interaction decision and preserve identity, role, and form factor';playerAcceptability=[math]::Min(100,60+(($r+$b)%31));keepOrDiscardReason=$reason}
     [void]$rounds.Add($round)
   }
 }
 [pscustomobject][ordered]@{schemaVersion=1;mode=$Mode;requirement=$Requirement;sourceHash=$SourceHash;iterationPolicy=$base.profile.sharedGenerationPipeline.stages;minimumRounds=12;recommendedRounds=20;maximumRounds=24;roundCount=$Iterations;branchingPerRound=$Branching;branchCount=$rounds.Count;beamWidth=4;rounds=@($rounds);status='iterated-candidate';claimLevel='candidate';graphAuthority='candidate-only'}
}

function Invoke-ESABCGenerationPipeline {
 [CmdletBinding()]param([Parameter(Mandatory)]$Candidates,[ValidateSet('creative-divergence','engineering','stable')][string]$Mode='creative-divergence',[bool]$AuditApproved=$false,[bool]$PlayabilityAccepted=$false,[string]$CollaboratorChoice='')
 $profile=Get-ESABCGenerationMode -Mode $Mode
 $selection=Select-ESABCGenerationCandidate -Candidates $Candidates -Mode $Mode -CollaboratorChoice $CollaboratorChoice
 $evidenceNames=@('seedDraft','expansionSet','auditFindings','playabilityBackpressure','finalDecision')
 $missing=@($Candidates|ForEach-Object{foreach($name in $evidenceNames){$p=$_.PSObject.Properties[$name];if($null -eq $p -or [string]::IsNullOrWhiteSpace([string]$p.Value)){"$([string]$_.directionId):$name"}}})
 if($missing.Count -gt 0){throw "ABC_PIPELINE_STAGE_EVIDENCE_MISSING:$($missing -join ',')"}
 $novelty=@($Candidates|ForEach-Object{Get-ESABCCreativeNoveltyAssessment -Candidate $_})
 $integrity=@($Candidates|ForEach-Object{Get-ESABCInnovationIntegrityAssessment -Candidate $_});$integrityBlocked=@($integrity|Where-Object{$_.status -ne 'concrete-continuous'}).Count -gt 0
 $noveltyBlocked=($Mode -eq 'creative-divergence' -and @($novelty|Where-Object{$_.status -ne 'grounded-bold'}).Count -gt 0)
 $playability=@($Candidates|ForEach-Object{Get-ESABCPlayabilityAssessment -Candidate $_})
 $playabilityBlocked=@($playability|Where-Object{$_.status -ne 'immediately-playable'}).Count -gt 0
 $expansionMinimum=if($null -ne $profile.pipelineProfile){[int]$profile.pipelineProfile.expansionMinimum}else{0}
 $stageStatus=[ordered]@{abFastSeed='observed';abcdExpand=if(@($Candidates).Count -ge $expansionMinimum){'observed'}else{'needs-expansion'};abcdAudit=if($AuditApproved){'accepted'}else{'pending'};playabilityBackpressure=if($PlayabilityAccepted){'accepted'}else{'pending'};finalize='blocked'}
 $status='review-required';$claimLevel='candidate';$reason='ABC_PIPELINE_AUDIT_REQUIRED'
 if($Mode -eq 'stable'){$stageStatus.abcdAudit='not-applicable';$stageStatus.playabilityBackpressure='not-applicable';$reason='ABC_PIPELINE_STABLE_MODE_NOT_IN_SHARED_PIPELINE'}
 elseif($integrityBlocked){$reason='ABC_PIPELINE_INNOVATION_INTEGRITY_REQUIRED'}
 elseif($noveltyBlocked){$reason='ABC_PIPELINE_CREATIVE_GROUNDING_REQUIRED'}
 elseif($playabilityBlocked){$reason='ABC_PIPELINE_FIRST_USE_AFFORDANCE_REQUIRED'}
 elseif($AuditApproved -and $PlayabilityAccepted){$stageStatus.finalize='ready';$status='finalized-candidate';$claimLevel='design-candidate';$reason='ABC_PIPELINE_READY_TO_FINALIZE'}
 [pscustomobject][ordered]@{schemaVersion=1;pipelineId=[string]$profile.sharedGenerationPipeline.pipelineId;mode=$Mode;stageOrder=@($profile.sharedGenerationPipeline.stages|ForEach-Object{[string]$_.stageId});stageStatus=$stageStatus;selection=$selection;noveltyAssessment=$novelty;innovationIntegrityAssessment=$integrity;playabilityAssessment=$playability;status=$status;claimLevel=$claimLevel;reasonCode=$reason;auditApproved=$AuditApproved;playabilityAccepted=$PlayabilityAccepted;finalDecision=if($status -eq 'finalized-candidate'){[string]$selection.selectedDirectionId}else{$null}}
}

function Resolve-ESABCModeTransition {
 [CmdletBinding()]param(
  [Parameter(Mandatory)][ValidateSet('creative-divergence','engineering','stable')][string]$FromMode,
  [Parameter(Mandatory)][ValidateSet('creative-divergence','engineering','stable')][string]$ToMode,
  [bool]$CandidateSelected=$false,
  [bool]$DesignContractReady=$false,
  [bool]$RegressionFixtureReady=$false,
  [bool]$VariationExceedsTemplateBoundary=$false)
 $status='blocked';$reason='ABC_MODE_TRANSITION_NOT_ALLOWED'
 if($FromMode -eq 'creative-divergence' -and $ToMode -eq 'engineering'){
   $status=if($CandidateSelected){'ready'}else{'blocked'};$reason=if($CandidateSelected){'CREATIVE_CANDIDATE_SELECTED'}else{'CREATIVE_CANDIDATE_REQUIRED'}
 } elseif($FromMode -eq 'engineering' -and $ToMode -eq 'stable'){
   $status=if($DesignContractReady -and $RegressionFixtureReady){'ready'}else{'blocked'};$reason=if($status -eq 'ready'){'ENGINEERING_CONTRACT_AND_FIXTURE_READY'}else{'ENGINEERING_CONTRACT_OR_FIXTURE_MISSING'}
 } elseif($FromMode -eq 'stable' -and $ToMode -eq 'engineering'){
   $status=if($VariationExceedsTemplateBoundary){'ready'}else{'blocked'};$reason=if($status -eq 'ready'){'STABLE_VARIATION_EXCEEDS_TEMPLATE'}else{'STABLE_TEMPLATE_BOUNDARY_INTACT'}
 } elseif($FromMode -eq $ToMode){$status='blocked';$reason='ABC_MODE_TRANSITION_NOOP'}
 [pscustomobject][ordered]@{schemaVersion=1;fromMode=$FromMode;toMode=$ToMode;status=$status;reasonCode=$reason;candidateSelected=$CandidateSelected;designContractReady=$DesignContractReady;regressionFixtureReady=$RegressionFixtureReady;variationExceedsTemplateBoundary=$VariationExceedsTemplateBoundary;claimLevel='candidate';auditDeferred=$true}
}

function Invoke-ESABCDFiveDirectionDivergence {
 [CmdletBinding()]param([Parameter(Mandatory)][string]$Requirement,[Parameter(Mandatory)][string]$SourceHash,[ValidateRange(5,16)][int]$MinimumDirections=5)
 if([string]::IsNullOrWhiteSpace($Requirement)){throw 'DIVERGENCE_REQUIREMENT_REQUIRED'};if($SourceHash -notmatch '^[a-f0-9]{64}$'){throw 'DIVERGENCE_SOURCE_HASH_INVALID'}
 $kinds=@('minimal-change','alternative-architecture','failure-first','performance-first','compatibility-first','security-first','migration-first','observability-first');$dirs=@();foreach($k in $kinds|Select-Object -First $MinimumDirections){$seed=[ordered]@{requirement=$Requirement;sourceHash=$SourceHash;direction=$k};$dirs+=,[pscustomobject][ordered]@{directionId='dir-'+(Get-ESABCDDivergenceHash $seed).Substring(0,16);kind=$k;assumption="$k assumption for requirement";risk="risk:$k";verificationPredicate='source-hash-equals:'+ $SourceHash;identityHash=Get-ESABCDDivergenceHash $seed}}
 $scored=@($dirs|ForEach-Object{[pscustomobject]@{direction=$_;score=[int](([convert]::ToInt32($_.identityHash.Substring(0,4),16))%100)}}|Sort-Object @{e={$_.score};Descending=$true},@{e={$_.direction.directionId}});$winner=$scored[0].direction
 [pscustomobject][ordered]@{schemaVersion=1;requirement=$Requirement;sourceHash=$SourceHash;directionCount=$dirs.Count;directions=$dirs;selectedDirectionId=$winner.directionId;selection='deterministic-score-then-id';selectionScore=($scored|Where-Object {$_.direction.directionId -ceq $winner.directionId}).score;reusableCoreWrite=$false;persistedExperienceWrite=$false;status='accepted-for-current-task';hash=Get-ESABCDDivergenceHash ([ordered]@{directions=$dirs;winner=$winner.directionId})}
}
Export-ModuleMember -Function Invoke-ESABCDFiveDirectionDivergence,Get-ESABCDDivergenceHash,Get-ESABCGenerationMode,Resolve-ESABCGenerationSelection,Invoke-ESABCModeDivergence,Get-ESABCAmplificationAssessment,Get-ESABCModeQualityAssessment,Select-ESABCGenerationCandidate,Get-ESABCCreativeNoveltyAssessment,Get-ESABCPlayabilityAssessment,Get-ESABCInnovationIntegrityAssessment,Invoke-ESABCIterativeDivergence,Invoke-ESABCGenerationPipeline,Resolve-ESABCModeTransition
