[CmdletBinding()]param()
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'ESABCInnovationRun.psm1') -Force
$h=('b'*64)
$generator={param($ctx)
 if($ctx.phase -eq 'seed-selection'){return @([pscustomobject]@{content='axis-a';changedVariable='timing';playerAcceptability=80;novelty=90},[pscustomobject]@{content='axis-b';changedVariable='space';playerAcceptability=75;novelty=85},[pscustomobject]@{content='axis-c';changedVariable='resource';playerAcceptability=70;novelty=80})}
 return @([pscustomobject]@{content="round-$($ctx.round)-a";changedVariable='timing';playerAcceptability=82;novelty=78;counterplay=80;complexity=35;clarity=85;roleFit=80},[pscustomobject]@{content="round-$($ctx.round)-b";changedVariable='space';playerAcceptability=74;novelty=88;counterplay=70;complexity=55;clarity=70;roleFit=65})
}
$runs=@();foreach($mode in @('creative-divergence','engineering','stable')){$runs+=Invoke-ESABCInnovationRun -Requirement 'mode-test' -GoalRevision ('r-'+$mode) -SourceHash $h -GenerationMode $mode -ModelInvoker $generator}
if(@($runs|% generationMode|Sort-Object -Unique).Count -ne 3){throw 'MODE_BINDING_FAILED'}
if(@($runs|% {$_.finalDecision.modeScore}|Sort-Object -Unique).Count -lt 2){throw 'MODE_SCORE_DIFFERENCE_FAILED'}
if(@($runs|? {$_.finalDecision.challengeLensCount -lt 10}).Count -gt 0){throw 'CHALLENGE_LENS_COUNT_FAILED'}
if(@($runs|? {$_.finalDecision.challengeScore -lt 0 -or $_.finalDecision.challengeScore -gt 100}).Count -gt 0){throw 'CHALLENGE_SCORE_RANGE_FAILED'}
$bad=[pscustomobject]@{playerAcceptability=20;novelty=20;counterplay=20;complexity=90;clarity=20;roleFit=20}
$challenge=Invoke-ESABCChallengeMatrix -Branch $bad -Mode 'engineering'
if($challenge.status -ne 'needs-rework' -or $challenge.score -ge 60){throw 'CHALLENGE_REWORK_FAILED'}
[pscustomobject]@{status='passed';modeCount=$runs.Count;distinctModeScores=@($runs|% {$_.finalDecision.modeScore}|Sort-Object -Unique).Count;minChallengeLenses=($runs|% {$_.finalDecision.challengeLensCount}|measure -Minimum).Minimum;weakCandidateStatus=$challenge.status}
$abcp=[pscustomobject]@{ownership=92;rollback=88;domainFit=84};$scored=Invoke-ESABCStableScore -Branch ([pscustomobject]@{playerValue=80;causalClarity=85;counterplay=78;complexityControl=75;roleFit=82;joyLoop=86;first10sMoment=80;expressionCeiling=88;noveltyDelta=90;surprise=87;smoothness=83}) -GenerationMode creative-divergence -AbcpScores $abcp -ReviewRounds 5;if($scored.reviewRounds -ne 5 -or $scored.abcpScore -le 50 -or $scored.maxScoreDrift -gt 1.0 -or $scored.status -ne 'stable'){throw 'ABCP_OR_DRIFT_REVIEW_FAILED'};[pscustomobject]@{status='passed';abcpDimensionCount=$scored.dimensionNames.abcp.Count;reviewRounds=$scored.reviewRounds;maxScoreDrift=$scored.maxScoreDrift;abcpScore=$scored.abcpScore}
$n100=Convert-ESABCScoreToCanonical -RawScore 84 -SourceScale 'score-0-100' -Provenance 'ordinary-ai-independent-review'
$n5=Convert-ESABCScoreToCanonical -RawScore 2.5 -SourceScale 'score-0-5' -Provenance 'abcd-stable-review'
$n130=Convert-ESABCScoreToCanonical -RawScore 58 -SourceScale 'score-0-130' -Provenance 'abcd-engineering-review'
if($n100.canonicalScore -ne 84 -or $n5.canonicalScore -ne 50 -or $n130.canonicalScore -ne 44.62){throw 'CANONICAL_SCORE_NORMALIZATION_FAILED'}
if(@($n100,$n5,$n130|?{[string]::IsNullOrWhiteSpace($_.provenance) -or $_.canonicalScale -ne 'score-0-100'}).Count -ne 0){throw 'CANONICAL_SCORE_PROVENANCE_FAILED'}
[pscustomobject]@{status='passed';canonicalScores=@($n100.canonicalScore,$n5.canonicalScore,$n130.canonicalScore);sourceScales=@($n100.sourceScale,$n5.sourceScale,$n130.sourceScale)}
$reg=Invoke-ESABCScoringRegression -Branch ([pscustomobject]@{playerValue=80;causalClarity=85;counterplay=78;complexityControl=75;roleFit=82;joyLoop=86;first10sMoment=80;expressionCeiling=88;noveltyDelta=90;surprise=87;smoothness=83;mechanismChangeEvidence=$true}) -GenerationMode creative-divergence
if($reg.status -ne 'passed' -or @($reg.levels).Count -ne 6){throw 'MULTI_LEVEL_SCORING_REGRESSION_FAILED'}
[pscustomobject]@{status='passed';regressionStatus=$reg.status;regressionLevels=@($reg.levels).Count;failedLevels=@($reg.failedLevels)}
$fusion=Invoke-ESABCCreativeInnovationFusion -Factors ([pscustomobject]@{supernaturalPhenomenon=70;mutationLimitBreak=75;reversalInformationGap=80;specialBurstPoint=65;oppressionDespair=60;masteryCeiling=85;foundationalLogicShift=72;trajectoryInfluence=78;controlledRandomness=64;suppressionDramaticExperience=68})
if(-not $fusion.passed -or $fusion.activeFactorCount -ne 10){throw 'INNOVATION_FACTOR_FUSION_FAILED'}
[pscustomobject]@{status='passed';innovationFactorCount=$fusion.activeFactorCount;innovationFusionScore=$fusion.score}
$cmp=Compare-ESABCReferenceBaseline -EvaluandDimensions @{joyLoop=90;noveltyDelta=85;counterplay=70} -ComparisonDimensions @{joyLoop=60;noveltyDelta=55;counterplay=72} -ReferenceScore 60
if($cmp.status -ne 'passed' -or $cmp.evaluandRole -ne 'only-formal-score-target' -or $cmp.comparisonRole -ne 'reference-only' -or $cmp.largeGapCount -ne 2){throw 'COMPARISON_BASELINE_REGRESSION_FAILED'}
[pscustomobject]@{status='passed';comparisonDimensionCount=$cmp.dimensionCount;largeGapCount=$cmp.largeGapCount;referenceScore=$cmp.referenceCanonicalScore}
$traceKeys=@('supernaturalPhenomenon','mutationLimitBreak','reversalInformationGap','specialBurstPoint','oppressionDespair','masteryCeiling','foundationalLogicShift','trajectoryInfluence','controlledRandomness','suppressionDramaticExperience');$etrace=@{};$btrace=@{};foreach($k in $traceKeys){$etrace[$k]=@(80,82,84);$btrace[$k]=@(60,60,60)}
$ce=Invoke-ESABCCreativeEvaluation -Branch ([pscustomobject]@{playerValue=80;causalClarity=85;counterplay=80;complexityControl=75;roleFit=82;joyLoop=82;first10sMoment=78;expressionCeiling=84;noveltyDelta=82;surprise=80;smoothness=82;mechanismChangeEvidence=$true}) -InnovationFactors ([pscustomobject]@{supernaturalPhenomenon=70;mutationLimitBreak=72;reversalInformationGap=75;specialBurstPoint=68;oppressionDespair=65;masteryCeiling=80;foundationalLogicShift=74;trajectoryInfluence=77;controlledRandomness=62;suppressionDramaticExperience=69}) -EvaluandTrace $etrace -BaselineTrace $btrace
if($ce.status -ne 'passed' -or $ce.innovationFusion.activeFactorCount -lt 3){throw 'CREATIVE_EVALUATION_PIPELINE_FAILED'}
[pscustomobject]@{status='passed';creativePipeline=$ce.status;sourceFusionScore=$ce.innovationFusion.score}
$bands=Invoke-ESABCGenerationDepthRegression -Generations @([pscustomobject]@{score=2},[pscustomobject]@{score=3},[pscustomobject]@{score=4}) -TraditionalBaseline @([pscustomobject]@{score=1},[pscustomobject]@{score=1.5},[pscustomobject]@{score=2}) -Mode creative-divergence
if($bands.status -ne 'passed' -or (Convert-ESABCStrictBandToScore 0) -ne -20 -or (Convert-ESABCStrictBandToScore 5) -ne 90){throw 'STRICT_RUBRIC_REGRESSION_FAILED'}
[pscustomobject]@{status='passed';strictGenerations=$bands.minimumGenerations;mappedAnchors=@((Convert-ESABCStrictBandToScore 0),(Convert-ESABCStrictBandToScore 1),(Convert-ESABCStrictBandToScore 2),(Convert-ESABCStrictBandToScore 3),(Convert-ESABCStrictBandToScore 4),(Convert-ESABCStrictBandToScore 5))}
$strict=Invoke-ESABCStrictModeScore -Mode creative-divergence -GeneralBands @(2,3,2.5) -ModeBands @(0,1,2) -AbcpBands @(3,2.5,3)
if($strict.status -ne 'strict-scored' -or -not $strict.formalAcceptance -or $strict.scale -ne '0-5'){throw 'STRICT_CORE_ENTRYPOINT_FAILED'}
[pscustomobject]@{status='passed';strictCoreTotal=$strict.totalScore;legacyAdvisoryOnly=$strict.legacyScoresAdvisoryOnly}
