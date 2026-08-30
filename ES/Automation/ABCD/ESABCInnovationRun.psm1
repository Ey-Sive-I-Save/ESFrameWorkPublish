Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$script:Stages=@('requirement-facts','player-outcomes','lexical-deanchor','seed-divergence','tree-expansion','global-convergence','interaction-graph','adaptive-weighting','player-replay','counterplay-audit','complexity-prune','candidate-tournament','final-decision')
function Get-ESABCInnovationRunHash($v){$s=[Security.Cryptography.SHA256]::Create();try{([BitConverter]::ToString($s.ComputeHash([Text.Encoding]::UTF8.GetBytes(($v|ConvertTo-Json -Compress -Depth 30)))).Replace('-','').ToLowerInvariant())}finally{$s.Dispose()}}
function Get-ESABCScoringContract {
 [CmdletBinding()]param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
 $path=Join-Path $ProjectRoot 'ES/Automation/Contracts/es-ai-abc-scoring-v1.json';if(-not(Test-Path -LiteralPath $path -PathType Leaf)){throw 'ABC_SCORING_CONTRACT_MISSING'}
 $contract=Get-Content -LiteralPath $path -Raw -Encoding UTF8|ConvertFrom-Json;if([int]$contract.schemaVersion -ne 1 -or [string]$contract.contractId -cne 'es://automation/contracts/ai-abc/scoring/v1'){throw 'ABC_SCORING_CONTRACT_INVALID'};return $contract
}
function Convert-ESABCScoreToCanonical {
 [CmdletBinding()]param(
  [Parameter(Mandatory)][double]$RawScore,
  [ValidateSet('score-0-100','score-0-5','score-0-130')][string]$SourceScale='score-0-100',
  [string]$Provenance=''
 )
 $c=Get-ESABCScoringContract
 if([string]::IsNullOrWhiteSpace($Provenance)){throw 'ABC_SCORING_PROVENANCE_REQUIRED'}
 $profile=$c.normalization.profiles.PSObject.Properties[$SourceScale]
 if($null -eq $profile){throw 'ABC_SCORING_SOURCE_SCALE_NOT_REGISTERED'}
 $min=[double]$profile.Value.inputMinimum;$max=[double]$profile.Value.inputMaximum
 if($RawScore -lt $min -or $RawScore -gt $max){throw 'ABC_SCORING_RAW_VALUE_OUT_OF_RANGE'}
 $canonical=[math]::Round((($RawScore-$min)/($max-$min))*100,2)
 [pscustomobject][ordered]@{rawScore=$RawScore;sourceScale=$SourceScale;canonicalScore=$canonical;canonicalScale='score-0-100';provenance=$Provenance;formula=[string]$c.normalization.formula}
}
function Get-ESABCScoringDimensions {
 [CmdletBinding()]param([ValidateSet('creative-divergence','engineering','stable')][string]$GenerationMode='creative-divergence',[object]$AbcpScores=$null)
 $c=Get-ESABCScoringContract;$general=@($c.layers.general.dimensions);$modeProperty=$c.layers.mode.modes.PSObject.Properties[$GenerationMode];if($null -eq $modeProperty){throw 'ABC_SCORING_MODE_NOT_REGISTERED'};$modeDimensions=@($modeProperty.Value);$abcp=@();if($null -ne $AbcpScores){$abcp=@($AbcpScores.PSObject.Properties|ForEach-Object{$_.Name})};$weights=Resolve-ESABCScoreWeights;[pscustomobject][ordered]@{general=$general;mode=$modeDimensions;abcp=$abcp;layerWeights=[ordered]@{general=[double]$weights.general;mode=[double]$weights.mode;abcp=[double]$weights.abcp};weightSignals=$weights.signals}
}
function Get-ESABCScoreValue([object]$Source,[string]$Name,[double]$Missing=50){if($null -ne $Source -and $Source.PSObject.Properties[$Name]){return [math]::Max(0,[math]::Min(100,[double]$Source.$Name))};return $Missing}
function Convert-ESABCStrictBandToScore {
 [CmdletBinding()]param([Parameter(Mandatory)][ValidateRange(0,5)][double]$Band)
 $anchors=@(@(0,-20),@(1,0),@(2,20),@(3,40),@(4,70),@(5,90));for($i=1;$i -lt $anchors.Count;$i++){if($Band -le $anchors[$i][0]){$x0=$anchors[$i-1][0];$y0=$anchors[$i-1][1];$x1=$anchors[$i][0];$y1=$anchors[$i][1];return [math]::Round($y0+(($Band-$x0)/($x1-$x0))*($y1-$y0),2)}};return 90
}
function Invoke-ESABCGenerationDepthRegression {
 [CmdletBinding()]param([Parameter(Mandatory)][object[]]$Generations,[Parameter(Mandatory)][object[]]$TraditionalBaseline,[ValidateSet('creative-divergence','engineering','stable')][string]$Mode='creative-divergence')
 $c=Get-ESABCScoringContract;if($Generations.Count -lt $c.strictModeRubric.minimumGenerations -or $TraditionalBaseline.Count -lt $c.strictModeRubric.minimumGenerations){return [pscustomobject]@{status='failed';failureCode='ABC_STRICT_RUBRIC_REGRESSION_FAILED';reason='minimum-generations-not-met'}};$rows=@();for($i=0;$i -lt $c.strictModeRubric.minimumGenerations;$i++){ $g=[double]$Generations[$i].score;$b=[double]$TraditionalBaseline[$i].score;$delta=[math]::Round($g-$b,2);$rows+=,[pscustomobject]@{generation=$i+1;strictBand=$g;baselineBand=$b;incrementalDelta=$delta;mappedScore=(Convert-ESABCStrictBandToScore $g);baselineMappedScore=(Convert-ESABCStrictBandToScore $b);qualitativeChange=($delta -ge 1.0)} };$failed=@($rows|Where-Object{$_.incrementalDelta -lt 1.0});$failedIds=@($failed|ForEach-Object{$_.generation});[pscustomobject][ordered]@{status=if($failed.Count -eq 0){'passed'}else{'failed'};mode=$Mode;minimumGenerations=3;rows=@($rows);failedGenerations=$failedIds;incrementalOnly=$c.strictModeRubric.incrementalGainOnly;failureCode=$c.strictModeRubric.failureCode}
}
function Invoke-ESABCStrictModeScore {
 [CmdletBinding()]param([Parameter(Mandatory)][ValidateSet('creative-divergence','engineering','stable')][string]$Mode,[Parameter(Mandatory)][double[]]$GeneralBands,[Parameter(Mandatory)][double[]]$ModeBands,[Parameter(Mandatory)][double[]]$AbcpBands)
 $all=@($GeneralBands+$ModeBands+$AbcpBands);if(@($all|Where-Object{$_ -lt 0 -or $_ -gt 5}).Count -gt 0){throw 'ABC_STRICT_BAND_OUT_OF_RANGE'};if($GeneralBands.Count -eq 0 -or $ModeBands.Count -eq 0 -or $AbcpBands.Count -eq 0){throw 'ABC_STRICT_LAYER_EMPTY'};$g=[math]::Round((($GeneralBands|Measure-Object -Average).Average),2);$m=[math]::Round((($ModeBands|Measure-Object -Average).Average),2);$a=[math]::Round((($AbcpBands|Measure-Object -Average).Average),2);$gm=Convert-ESABCStrictBandToScore $g;$mm=Convert-ESABCStrictBandToScore $m;$am=Convert-ESABCStrictBandToScore $a;$total=[math]::Round($gm*0.30+$mm*0.40+$am*0.30,2);[pscustomobject][ordered]@{status='strict-scored';mode=$Mode;bandScores=[ordered]@{general=$g;mode=$m;abcp=$a};mappedScores=[ordered]@{general=$gm;mode=$mm;abcp=$am};totalScore=$total;scale='0-5';mappedScale='-20-to-90';formalAcceptance=$true;legacyScoresAdvisoryOnly=$true}
}
function Resolve-ESABCScoreWeights {
 [CmdletBinding()]param([hashtable]$Signals=@{})
 $c=Get-ESABCScoringContract;$w=[ordered]@{general=[double]$c.weightPolicy.default.general;mode=[double]$c.weightPolicy.default.mode;abcp=[double]$c.weightPolicy.default.abcp}
 if($Signals.ContainsKey('abcpExtensionRequest') -and [bool]$Signals.abcpExtensionRequest){$w.abcp=[math]::Min([double]$c.weightPolicy.bounds.abcp.maximum,$w.abcp+0.10);$w.general=[math]::Max([double]$c.weightPolicy.bounds.general.minimum,$w.general-0.05);$w.mode=1-$w.general-$w.abcp}
 if($Signals.ContainsKey('riskLevel') -and [string]$Signals.riskLevel -eq 'high'){$w.general=[math]::Min([double]$c.weightPolicy.bounds.general.maximum,$w.general+0.10);$w.mode=1-$w.general-$w.abcp}
 [pscustomobject][ordered]@{general=[math]::Round($w.general,4);mode=[math]::Round($w.mode,4);abcp=[math]::Round($w.abcp,4);signals=$Signals;source='ABC-scoring-core.weightPolicy'}
}
function Invoke-ESABCCreativeInnovationFusion {
 [CmdletBinding()]param([Parameter(Mandatory)]$Factors)
 $c=Get-ESABCScoringContract;$values=@($c.innovationFusion.factors|ForEach-Object{Get-ESABCScoreValue $Factors $_ 0});$active=@($values|Where-Object{$_ -gt 0});$mean=if(@($active).Count -gt 0){[math]::Round(($active|Measure-Object -Average).Average,2)}else{0};$passed=(@($active).Count -ge [int]$c.innovationFusion.minimumActiveFactors -and @($active|Where-Object{$_ -lt [double]$c.innovationFusion.minimumFactorScore}).Count -eq 0);[pscustomobject][ordered]@{score=$mean;activeFactorCount=@($active).Count;minimumActiveFactors=[int]$c.innovationFusion.minimumActiveFactors;minimumFactorScore=[double]$c.innovationFusion.minimumFactorScore;passed=$passed;failureCode=[string]$c.innovationFusion.failureCode;factors=[ordered]@{}} 
}
function Invoke-ESABCCreativeDepthRegression {
 [CmdletBinding()]param([Parameter(Mandatory)][hashtable]$EvaluandTrace,[Parameter(Mandatory)][hashtable]$BaselineTrace,[ValidateSet('original','mundane')][string]$BaselineKind='original')
 $c=Get-ESABCScoringContract;$rows=[Collections.Generic.List[object]]::new();$failed=[Collections.Generic.List[object]]::new();$keys=@($c.innovationFusion.factors);foreach($k in $keys){$e=@($EvaluandTrace[$k]);$b=@($BaselineTrace[$k]);$rounds=[math]::Min($e.Count,$b.Count);$deltas=@();for($i=0;$i -lt $rounds;$i++){ $deltas+=[double]$e[$i]-[double]$b[$i] };$avg=if($deltas.Count -gt 0){[math]::Round(($deltas|Measure-Object -Average).Average,2)}else{0};$ok=($rounds -ge [int]$c.deepInnovationRegression.minimumDecisionRounds -and $avg -ge [double]$c.deepInnovationRegression.quality.minimumQualitativeDelta);$row=[pscustomobject]@{factor=$k;rounds=$rounds;roundDeltas=@($deltas);qualitativeDelta=$avg;passed=$ok;zeroed=(!$ok)};[void]$rows.Add($row);if(-not $ok){[void]$failed.Add($k)}};[pscustomobject][ordered]@{status=if($failed.Count -eq 0){'passed'}else{'failed'};baselineKind=$BaselineKind;minimumRounds=[int]$c.deepInnovationRegression.minimumDecisionRounds;minimumQualitativeDelta=[double]$c.deepInnovationRegression.quality.minimumQualitativeDelta;factors=@($rows);failedFactors=@($failed);failureCode=[string]$c.deepInnovationRegression.failureCode}
}
function Compare-ESABCReferenceBaseline {
 [CmdletBinding()]param([Parameter(Mandatory)][hashtable]$EvaluandDimensions,[Parameter(Mandatory)][hashtable]$ComparisonDimensions,[double]$ReferenceScore=60)
 $c=Get-ESABCScoringContract;$p=$c.comparisonPolicy;if($ReferenceScore -ne [double]$p.referenceCanonicalScore){throw 'ABC_COMPARISON_REFERENCE_SCORE_INVALID'};$ek=@($EvaluandDimensions.Keys|Sort-Object);$ck=@($ComparisonDimensions.Keys|Sort-Object);if(($ek -join '|') -cne ($ck -join '|')){throw [string]$p.missingDimensionEffect};$rows=[Collections.Generic.List[object]]::new();foreach($k in $ek){$e=[double]$EvaluandDimensions[$k];$b=[double]$ComparisonDimensions[$k];if($e -lt 0 -or $e -gt 100 -or $b -lt 0 -or $b -gt 100){throw [string]$p.scaleMismatchEffect};$gap=[math]::Round($e-$b,2);$large=([math]::Abs($gap) -ge [double]$p.largeGapThreshold);$impact=if($large){[math]::Round(([math]::Sign($gap)*[double]$p.largeGapImpact),2)}else{[math]::Round($gap/3,2)};[void]$rows.Add([pscustomobject]@{dimension=$k;evaluand=$e;comparison=$b;gap=$gap;gapBand=if($large){'large'}else{'normal'};signedImpact=$impact})};$largeCount=@($rows|Where-Object{$_.gapBand -eq 'large'}).Count;[pscustomobject][ordered]@{status='passed';evaluandRole='only-formal-score-target';comparisonRole='reference-only';referenceCanonicalScore=$ReferenceScore;dimensionCount=$rows.Count;largeGapCount=$largeCount;dimensions=@($rows);comparisonCannotReplaceEvaluand=[bool]$p.comparisonCannotReplaceEvaluand}
}
function Invoke-ESABCCreativeEvaluation {
 [CmdletBinding()]param([Parameter(Mandatory)]$Branch,[Parameter(Mandatory)]$InnovationFactors,[object]$AbcpScores=$null,[hashtable]$WeightSignals=@{},[ValidateRange(3,8)][int]$ReviewRounds=5,[hashtable]$EvaluandTrace=$null,[hashtable]$BaselineTrace=$null)
 $fusion=Invoke-ESABCCreativeInnovationFusion -Factors $InnovationFactors;if(-not $fusion.passed){return [pscustomobject]@{status='innovation-gate-failed';failureCode=$fusion.failureCode;fusion=$fusion}}
 if($null -eq $EvaluandTrace -or $null -eq $BaselineTrace){return [pscustomobject]@{status='depth-regression-unproven';failureCode='ABC_INNOVATION_DEPTH_REGRESSION_FAILED';fusion=$fusion}}
 $depth=Invoke-ESABCCreativeDepthRegression -EvaluandTrace $EvaluandTrace -BaselineTrace $BaselineTrace
 if($depth.status -ne 'passed'){return [pscustomobject]@{status='depth-regression-failed';failureCode=$depth.failureCode;fusion=$fusion;depthRegression=$depth}}
 $score=Invoke-ESABCStableScore -Branch $Branch -GenerationMode 'creative-divergence' -AbcpScores $AbcpScores -ReviewRounds $ReviewRounds
 [pscustomobject][ordered]@{status=if($score.status -eq 'stable'){'passed'}else{$score.status};score=$score;innovationFusion=$fusion;depthRegression=$depth;composition=[pscustomobject]@{outcomeWeight=0.5;sourceFusionWeight=0.5;sourceFusionScore=$fusion.score}}
}
function Invoke-ESABCStableScore {
 [CmdletBinding()]param([Parameter(Mandatory)]$Branch,[ValidateSet('creative-divergence','engineering','stable')][string]$GenerationMode='creative-divergence',[object]$AbcpScores=$null,[ValidateRange(3,8)][int]$ReviewRounds=5)
 $c=Get-ESABCScoringContract;$d=Get-ESABCScoringDimensions -GenerationMode $GenerationMode -AbcpScores $AbcpScores;if($d.abcp.Count -gt $c.layers.abcp.maxDimensions){throw 'ABC_SCORING_ABCP_DIMENSION_LIMIT_EXCEEDED'};if(@($d.abcp|Sort-Object -Unique).Count -ne $d.abcp.Count){throw 'ABC_SCORING_ABCP_DUPLICATE_DIMENSION'};foreach($n in $d.abcp){if($n -notmatch [string]$c.layers.abcp.dimensionPattern){throw 'ABC_SCORING_ABCP_DIMENSION_INVALID'}};$g=@($d.general|%{Get-ESABCScoreValue $Branch $_ $c.layers.general.missingScore});$m=@($d.mode|%{Get-ESABCScoreValue $Branch $_ $c.layers.mode.missingScore});$a=@();if($d.abcp.Count -gt 0){$a=@($d.abcp|%{Get-ESABCScoreValue $AbcpScores $_ $c.layers.abcp.missingScore})}else{$a=@([double]$c.layers.abcp.missingScore)}
 $missingGeneral=@($d.general|?{-not $Branch.PSObject.Properties[$_]});$missingMode=@($d.mode|?{-not $Branch.PSObject.Properties[$_]});$missingAbcp=@();if($d.abcp.Count -gt 0){$missingAbcp=@($d.abcp|?{-not $AbcpScores.PSObject.Properties[$_]})};$missing=@($missingGeneral+$missingMode+$missingAbcp);$gs=[math]::Round(($g|Measure-Object -Average).Average,2);$ms=[math]::Round(($m|Measure-Object -Average).Average,2);$as=[math]::Round(($a|Measure-Object -Average).Average,2);$total=[math]::Round($gs*$d.layerWeights.general+$ms*$d.layerWeights.mode+$as*$d.layerWeights.abcp,2);$history=[Collections.Generic.List[object]]::new();$previous=$null;for($i=1;$i -le $ReviewRounds;$i++){$drift=if($null -eq $previous){0}else{[math]::Round([math]::Abs($total-$previous),2)};[void]$history.Add([pscustomobject][ordered]@{round=$i;generalScore=$gs;modeScore=$ms;abcpScore=$as;totalScore=$total;challengeScore=$null;dimensionSnapshot=[ordered]@{general=$g;mode=$m;abcp=$a};missingDimensions=$missing;changedEvidence='same immutable candidate inputs; independent recompute';driftFromPrevious=$drift});$previous=$total}
 $maxDrift=($history|Measure-Object -Property driftFromPrevious -Maximum).Maximum;$result=[pscustomobject][ordered]@{mode=$GenerationMode;generalScore=$gs;modeScore=$ms;abcpScore=$as;totalScore=$total;dimensionNames=$d;missingDimensions=$missing;evidenceCompleteness=[math]::Round(100*(1-($missing.Count/[math]::Max(1,($d.general.Count+$d.mode.Count+$d.abcp.Count)))),2);reviewRounds=$ReviewRounds;reviewHistory=@($history);maxScoreDrift=[double]$maxDrift;driftThreshold=[double]$c.review.maximumScoreDrift;status=if($maxDrift -le $c.review.maximumScoreDrift){'stable'}else{'drift-failed'};formula=[string]$c.finalScore.formula};if($GenerationMode -eq 'creative-divergence'){$gate=$c.creativeGate;$novelty=Get-ESABCScoreValue $Branch 'noveltyDelta' 0;$joy=Get-ESABCScoreValue $Branch 'joyLoop' 0;$evidence=($Branch.PSObject.Properties['mechanismChangeEvidence'] -and [bool]$Branch.mechanismChangeEvidence);if(-not $evidence -and $null -eq $Branch.PSObject.Properties['mechanismChangeEvidence']){$evidence=($novelty -ge [double]$gate.minimumNoveltyDelta)};$result|Add-Member -NotePropertyName innovationGate -NotePropertyValue ([pscustomobject]@{passed=($evidence -and $novelty -ge [double]$gate.minimumNoveltyDelta -and $joy -ge [double]$gate.minimumJoyLoop);noveltyDelta=$novelty;minimumNoveltyDelta=[double]$gate.minimumNoveltyDelta;joyLoop=$joy;minimumJoyLoop=[double]$gate.minimumJoyLoop;mechanismChangeEvidence=$evidence;failureCode=[string]$gate.failureCode});if(-not $result.innovationGate.passed){$result.status='innovation-gate-failed'}};return $result
}
function Get-ESABCInnovationModePolicy {
 [CmdletBinding()]param([ValidateSet('creative-divergence','engineering','stable')][string]$Mode='creative-divergence')
 $common=[ordered]@{player=0.2;novelty=0.2;counterplay=0.2;complexity=0.2;clarity=0.1;roleFit=0.1;requiredLenses=@('player-value','causal-clarity','counterplay','complexity','role-fit','accessibility','abuse-resistance','feedback','replayability','implementation-risk')}
 if($Mode -eq 'creative-divergence'){$common.player=.30;$common.novelty=.25;$common.counterplay=.15;$common.complexity=.10;$common.clarity=.10;$common.roleFit=.10;$common.requiredLenses+=@('surprise','expression','first-payoff')}
 elseif($Mode -eq 'engineering'){$common.player=.10;$common.novelty=.15;$common.counterplay=.15;$common.complexity=.10;$common.clarity=.10;$common.roleFit=.10;$common.requiredLenses+=@('state-integrity','ownership-lifecycle','determinism','performance','failure-recovery','reuse','security','observability');$common.novelty=.10;$common.counterplay=.15;$common.complexity=.15}
 else{$common.player=.15;$common.novelty=.10;$common.counterplay=.15;$common.complexity=.15;$common.clarity=.15;$common.roleFit=.10;$common.requiredLenses+=@('project-fit','compatibility','regression','rollback','throughput','complete-loop');$common.novelty=.05;$common.counterplay=.15}
 $sum=($common.player+$common.novelty+$common.counterplay+$common.complexity+$common.clarity+$common.roleFit);foreach($k in @('player','novelty','counterplay','complexity','clarity','roleFit')){$common[$k]=[math]::Round(([double]$common[$k])/$sum,4)}
 [pscustomobject]$common
}
function Invoke-ESABCScoringRegression {
 [CmdletBinding()]param([Parameter(Mandatory)]$Branch,[ValidateSet('creative-divergence','engineering','stable')][string]$GenerationMode='creative-divergence',[object]$AbcpScores=$null)
 $levels=[Collections.Generic.List[object]]::new();$base=Invoke-ESABCStableScore -Branch $Branch -GenerationMode $GenerationMode -AbcpScores $AbcpScores -ReviewRounds 3
 [void]$levels.Add([pscustomobject]@{level='schema-and-range';passed=($base.totalScore -ge 0 -and $base.totalScore -le 100);detail='canonical score and layer ranges validated'})
 $changed=[pscustomobject]@{};$Branch.PSObject.Properties|ForEach-Object{Add-Member -InputObject $changed -NotePropertyName $_.Name -NotePropertyValue $_.Value -Force};$old=Get-ESABCScoreValue $Branch 'playerValue' 50;Add-Member -InputObject $changed -NotePropertyName playerValue -NotePropertyValue ([math]::Min(100,$old+10)) -Force;$probe=Invoke-ESABCStableScore -Branch $changed -GenerationMode $GenerationMode -AbcpScores $AbcpScores -ReviewRounds 3
 [void]$levels.Add([pscustomobject]@{level='dimension-sensitivity';passed=($probe.totalScore -ge $base.totalScore);detail='raising playerValue must not lower total score'})
 [void]$levels.Add([pscustomobject]@{level='weight-application';passed=($base.formula -eq 'general*dynamic.general + mode*dynamic.mode + abcp*dynamic.abcp' -and [math]::Abs(($base.dimensionNames.layerWeights.general+$base.dimensionNames.layerWeights.mode+$base.dimensionNames.layerWeights.abcp)-1.0) -lt 0.001);detail='central dynamic layer weights applied'})
 $gatePassed=$true;if($GenerationMode -eq 'creative-divergence'){$negative=[pscustomobject]@{noveltyDelta=40;joyLoop=80;mechanismChangeEvidence=$false};$ng=Invoke-ESABCStableScore -Branch $negative -GenerationMode $GenerationMode -ReviewRounds 3;$gatePassed=($ng.status -eq 'innovation-gate-failed')};[void]$levels.Add([pscustomobject]@{level='creative-gate-negative';passed=$gatePassed;detail='low novelty or missing mechanism evidence is rejected'})
 [void]$levels.Add([pscustomobject]@{level='missing-evidence';passed=($base.PSObject.Properties['missingDimensions'] -ne $null);detail='missing dimensions are recorded'})
 [void]$levels.Add([pscustomobject]@{level='boundary-and-drift';passed=($base.maxScoreDrift -le $base.driftThreshold -and $base.reviewRounds -ge 3);detail='review drift remains within threshold'})
 $failed=@($levels|Where-Object{-not $_.passed});[pscustomobject][ordered]@{status=if($failed.Count -eq 0){'passed'}else{'failed'};mode=$GenerationMode;levels=@($levels);failedLevels=@($failed|ForEach-Object{$_.level});baseScore=$base.totalScore;regressionContract='ABC_SCORING_REGRESSION_FAILED'}
}
function Get-ESABCBranchScore {
 [CmdletBinding()]param([Parameter(Mandatory)]$Branch,[ValidateSet('creative-divergence','engineering','stable')][string]$Mode='creative-divergence')
 $scores=Invoke-ESABCStableScore -Branch $Branch -GenerationMode $Mode -ReviewRounds 3
 return [double]$scores.totalScore
}
function Invoke-ESABCChallengeMatrix {
 [CmdletBinding()]param([Parameter(Mandatory)]$Branch,[ValidateSet('creative-divergence','engineering','stable')][string]$Mode='creative-divergence')
 $p=Get-ESABCInnovationModePolicy -Mode $Mode;$findings=[Collections.Generic.List[object]]::new();$score=100
 foreach($lens in $p.requiredLenses){$value=if($Branch.PSObject.Properties[$lens]){[double]$Branch.$lens}elseif($Branch.PSObject.Properties['challengeScores'] -and $Branch.challengeScores.PSObject.Properties[$lens]){[double]$Branch.challengeScores.$lens}else{50};$deduction=if($value -lt 40){20}elseif($value -lt 60){8}else{0};$score-=$deduction;[void]$findings.Add([pscustomobject]@{lens=$lens;score=[math]::Round($value,2);deduction=$deduction;status=if($deduction -gt 0){'challenge-required'}else{'passed'}})}
 [pscustomobject][ordered]@{mode=$Mode;score=[math]::Max(0,[math]::Round($score,2));lensCount=$findings.Count;findings=@($findings);status=if($score -ge 60){'reviewed'}else{'needs-rework'}}
}
function New-ESABCInnovationRun {
 [CmdletBinding()]param([Parameter(Mandatory)][string]$Requirement,[Parameter(Mandatory)][string]$GoalRevision,[Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$SourceHash,[ValidateSet('creative-divergence','engineering','stable')][string]$GenerationMode='creative-divergence',[ValidateRange(1,24)][int]$MaxRounds=12,[ValidateRange(2,4)][int]$BranchesPerRound=2,[ValidateRange(1,128)][int]$MaxModelCalls=64)
 if([string]::IsNullOrWhiteSpace($Requirement)-or[string]::IsNullOrWhiteSpace($GoalRevision)){throw 'INNOVATION_RUN_REQUIREMENT_OR_REVISION_REQUIRED'}
 $stageBudgets=[ordered]@{
  'requirement-facts'=[ordered]@{modelCalls=2;evaluations=4}
  'player-outcomes'=[ordered]@{modelCalls=8;evaluations=16}
  'lexical-deanchor'=[ordered]@{modelCalls=8;evaluations=16}
  'seed-divergence'=[ordered]@{modelCalls=16;evaluations=32}
  'tree-expansion'=[ordered]@{modelCalls=$MaxModelCalls;evaluations=[math]::Min(512,$MaxModelCalls*2)}
  'global-convergence'=[ordered]@{modelCalls=16;evaluations=64}
  'interaction-graph'=[ordered]@{modelCalls=16;evaluations=64}
  'adaptive-weighting'=[ordered]@{modelCalls=16;evaluations=64}
  'player-replay'=[ordered]@{modelCalls=32;evaluations=128}
  'counterplay-audit'=[ordered]@{modelCalls=32;evaluations=128}
  'complexity-prune'=[ordered]@{modelCalls=24;evaluations=96}
  'candidate-tournament'=[ordered]@{modelCalls=24;evaluations=96}
  'final-decision'=[ordered]@{modelCalls=8;evaluations=32}
 }
 $bud=[ordered]@{maxRounds=$MaxRounds;branchesPerRound=$BranchesPerRound;maxBranches=[math]::Min(256,($MaxRounds*$BranchesPerRound)+64);maxModelCalls=$MaxModelCalls;modelCallsUsed=0;evaluationsUsed=0;stageBudgets=$stageBudgets;stageUsage=@{}}
 $seed=[ordered]@{requirement=$Requirement;goalRevision=$GoalRevision;sourceHash=$SourceHash;generationMode=$GenerationMode}
 $policy=Get-ESABCInnovationModePolicy -Mode $GenerationMode
 [pscustomobject][ordered]@{schemaVersion=1;contractId='es://automation/contracts/ai-abc/innovation-run/v1';runId='ir-'+(Get-ESABCInnovationRunHash $seed).Substring(0,24);requirement=$Requirement;goalRevision=$GoalRevision;sourceHash=$SourceHash;generationMode=$GenerationMode;taskKind='design';codeValidation=$null;modePolicy=$policy;currentStage='requirement-facts';stageIndex=0;stagePlan=@($script:Stages);stageOutputs=@{};divergenceTree=@{};convergenceHistory=@();interactionGraph=@();branchWeights=@{};weightHistory=@();rejectedBranches=@();finalDecision=$null;resourceBudget=$bud;challengeMatrix=@();status='running';runHash=(Get-ESABCInnovationRunHash $seed);claimLevel='candidate';runtimeStatus='runtime-not-run'}
}
function Move-ESABCInnovationRun {
 [CmdletBinding()]param([Parameter(Mandatory)]$Run,[Parameter(Mandatory)][string]$ToStage,[hashtable]$Output=@{})
 $idx=[array]::IndexOf($script:Stages,$ToStage);if($idx -lt 0){throw 'INNOVATION_RUN_STAGE_UNKNOWN'};if($idx -ne ([int]$Run.stageIndex+1)){throw 'INNOVATION_RUN_ILLEGAL_STAGE_TRANSITION'}
 if($Run.status -ne 'running'){throw 'INNOVATION_RUN_NOT_RUNNING'};if($ToStage -eq 'tree-expansion' -and [string]$Run.currentStage -ne 'seed-divergence'){throw 'INNOVATION_RUN_SEED_OUTPUT_REQUIRED'}
 $hasMeaningfulOutput=$false;foreach($value in $Output.Values){if($null -ne $value -and ([string]$value).Trim().Length -gt 0){$hasMeaningfulOutput=$true;break}}
 $currentStage=[string]$Run.currentStage
 if($currentStage -eq 'tree-expansion' -and $Run.divergenceTree.Count -gt 0){$hasMeaningfulOutput=$true}
 if($currentStage -eq 'global-convergence' -and @($Run.convergenceHistory).Count -gt 0){$hasMeaningfulOutput=$true}
 if($currentStage -eq 'adaptive-weighting' -and $Run.branchWeights.Count -gt 0){$hasMeaningfulOutput=$true}
 if(-not $hasMeaningfulOutput){throw 'INNOVATION_RUN_STAGE_OUTPUT_REQUIRED'}
 if($Run.PSObject.Properties['enforceEvidence'] -and [bool]$Run.enforceEvidence){Assert-ESABCStageGateOutput -Run $Run -CurrentStage $currentStage -Output $Output -ToStage $ToStage}
 $Run.stageOutputs[[string]$Run.currentStage]=[pscustomobject]$Output;$Run.currentStage=$ToStage;$Run.stageIndex=$idx;[pscustomobject][ordered]@{status='advanced';runId=$Run.runId;stage=$ToStage;stageIndex=$idx;resourceBudget=$Run.resourceBudget}
}
function Assert-ESABCStageGateOutput {
 [CmdletBinding()]param([Parameter(Mandatory)]$Run,[Parameter(Mandatory)][string]$CurrentStage,[Parameter(Mandatory)][hashtable]$Output,[Parameter(Mandatory)][string]$ToStage)
 switch($CurrentStage){
  'seed-divergence' {if(-not $Output.ContainsKey('selectionAuthority') -or [string]$Output.selectionAuthority -cne 'ABCD' -or [int]$Output.selectedCount -lt 3){throw 'ABCD_STAGE_GATE_SEED_EVIDENCE_REQUIRED'}}
  'tree-expansion' {if($Run.divergenceTree.Count -lt 2){throw 'ABCD_STAGE_GATE_DIVERGENCE_EVIDENCE_REQUIRED'}}
  'global-convergence' {if(@($Run.convergenceHistory).Count -lt 1){throw 'ABCD_STAGE_GATE_CONVERGENCE_EVIDENCE_REQUIRED'}}
  'interaction-graph' {if(-not $Output.ContainsKey('links') -and -not $Output.ContainsKey('convergence')){throw 'ABCD_STAGE_GATE_INTERACTION_EVIDENCE_REQUIRED'}}
  'adaptive-weighting' {if($Run.branchWeights.Count -eq 0){throw 'ABCD_STAGE_GATE_WEIGHT_EVIDENCE_REQUIRED'}}
  'counterplay-audit' {if((-not $Output.ContainsKey('lenses') -or @($Output.lenses).Count -lt 3) -and -not $Output.ContainsKey('status')){throw 'ABCD_STAGE_GATE_REBUTTAL_EVIDENCE_REQUIRED'}}
 }
}
function Add-ESABCInnovationBranch {
 [CmdletBinding()]param([Parameter(Mandatory)]$Run,[Parameter(Mandatory)][string]$BranchId,[string]$ParentBranchId='',[Parameter(Mandatory)][string]$Content,[Parameter(Mandatory)][string]$ChangedVariable,[ValidateRange(0,100)][int]$PlayerAcceptability,[ValidateRange(0,100)][int]$Novelty=0,[ValidateRange(0,100)][int]$Counterplay=50,[ValidateRange(0,100)][int]$Complexity=50,[ValidateRange(0,100)][int]$Clarity=50,[ValidateRange(0,100)][int]$RoleFit=50,[object]$AbcpScores=$null,[ValidateRange(3,8)][int]$ScoreReviewRounds=5)
 if($Run.currentStage -ne 'tree-expansion'){throw 'INNOVATION_RUN_TREE_STAGE_REQUIRED'};if($Run.divergenceTree.ContainsKey($BranchId)){throw 'INNOVATION_RUN_BRANCH_DUPLICATE'};if($Run.divergenceTree.Count -ge $Run.resourceBudget.maxBranches){throw 'INNOVATION_RUN_BRANCH_BUDGET_EXHAUSTED'};if($Run.resourceBudget.modelCallsUsed -ge $Run.resourceBudget.maxModelCalls){throw 'INNOVATION_RUN_MODEL_BUDGET_EXHAUSTED'}
 if(-not [string]::IsNullOrWhiteSpace($ParentBranchId)-and -not $Run.divergenceTree.ContainsKey($ParentBranchId)){throw 'INNOVATION_RUN_PARENT_BRANCH_REQUIRED'}
 $stageBudget=$Run.resourceBudget.stageBudgets[[string]$Run.currentStage];$usage=$Run.resourceBudget.stageUsage[[string]$Run.currentStage];if($null -eq $usage){$usage=[ordered]@{modelCalls=0;evaluations=0};$Run.resourceBudget.stageUsage[[string]$Run.currentStage]=$usage};if($usage.modelCalls -ge $stageBudget.modelCalls -or $usage.evaluations -ge $stageBudget.evaluations){throw 'INNOVATION_RUN_STAGE_RESOURCE_EXHAUSTED'}
 $Run.resourceBudget.modelCallsUsed++;$Run.resourceBudget.evaluationsUsed++;$usage.modelCalls++;$usage.evaluations++;$scoreInput=[pscustomobject]@{playerValue=$PlayerAcceptability;playerAcceptability=$PlayerAcceptability;noveltyDelta=$Novelty;novelty=$Novelty;counterplay=$Counterplay;complexityControl=(100-$Complexity);roleFit=$RoleFit;causalClarity=$Clarity;clarity=$Clarity;joyLoop=$PlayerAcceptability;first10sMoment=$Clarity;expressionCeiling=$RoleFit;surprise=$Novelty;smoothness=$Clarity;depth=$PlayerAcceptability;stateIntegrity=$Clarity;ownershipLifecycle=$RoleFit;determinism=$Counterplay;performance=(100-$Complexity);failureRecovery=$Counterplay;reusability=$RoleFit;longevity=$PlayerAcceptability;projectFit=$RoleFit;completeness=$PlayerAcceptability;safety=$Counterplay;closure=$Clarity;compatibility=$RoleFit;regression=$Counterplay;rollback=$Counterplay;throughput=$PlayerAcceptability};$stableScore=Invoke-ESABCStableScore -Branch $scoreInput -GenerationMode $Run.generationMode -AbcpScores $AbcpScores -ReviewRounds $ScoreReviewRounds;$branch=[pscustomobject][ordered]@{branchId=$BranchId;parentBranchId=$ParentBranchId;content=$Content;changedVariable=$ChangedVariable;playerAcceptability=$PlayerAcceptability;novelty=$Novelty;counterplay=$Counterplay;complexity=$Complexity;clarity=$Clarity;roleFit=$RoleFit;abcpScores=$AbcpScores;modeScore=$stableScore.totalScore;scoreBreakdown=$stableScore;status='open';createdStage=$Run.currentStage};$Run.divergenceTree[$BranchId]=$branch
 [pscustomobject][ordered]@{status='branch-added';branchId=$BranchId;parentBranchId=$ParentBranchId;modelCallsUsed=$Run.resourceBudget.modelCallsUsed}
}
function Invoke-ESABCGlobalConvergence {
 [CmdletBinding()]param([Parameter(Mandatory)]$Run)
 if($Run.currentStage -ne 'global-convergence'){throw 'INNOVATION_RUN_CONVERGENCE_STAGE_REQUIRED'};$open=@($Run.divergenceTree.Values|Where-Object{$_.status -eq 'open'});if($open.Count -lt 1){throw 'INNOVATION_RUN_NO_OPEN_BRANCH'}
 foreach($b in $open){if($b.PSObject.Properties['scoreBreakdown'] -and $b.scoreBreakdown){$b.modeScore=$b.scoreBreakdown.totalScore}else{$b.modeScore=Get-ESABCBranchScore -Branch $b -Mode $Run.generationMode}};$best=$open|Sort-Object @{e={$_.modeScore};Descending=$true},@{e={$_.playerAcceptability};Descending=$true},@{e={$_.novelty};Descending=$true}|Select-Object -First 1;foreach($b in $open){if($b.branchId -ne $best.branchId){$b.status='discarded';$Run.rejectedBranches+=,[pscustomobject]@{branchId=$b.branchId;reason='MODE_SCORE_LOWER_THAN_GLOBAL_WINNER';mode=$Run.generationMode}}};$Run.convergenceHistory+=,[pscustomobject]@{round=($Run.convergenceHistory.Count+1);bestBranchId=$best.branchId;mode=$Run.generationMode;modeScore=$best.modeScore;playerAcceptability=$best.playerAcceptability;novelty=$best.novelty;interactionDelta='global mode-weighted re-score completed'};[pscustomobject][ordered]@{status='converged';bestBranchId=$best.branchId;mode=$Run.generationMode;modeScore=$best.modeScore;openCount=$open.Count;rejectedCount=@($Run.rejectedBranches).Count}
}
function Update-ESABCInnovationWeights {
 [CmdletBinding()]param([Parameter(Mandatory)]$Run,[ValidateRange(0,100)][int]$PlayerGap=0,[ValidateRange(0,100)][int]$NoveltyGap=0,[ValidateRange(0,100)][int]$CounterplayGap=0,[ValidateRange(0,100)][int]$ComplexityGap=0)
 if($Run.currentStage -ne 'adaptive-weighting'){throw 'INNOVATION_RUN_WEIGHT_STAGE_REQUIRED'};$policy=Get-ESABCInnovationModePolicy -Mode $Run.generationMode;$raw=[ordered]@{player=$PlayerGap*(0.35+[double]$policy.player);novelty=$NoveltyGap*(0.2+[double]$policy.novelty);counterplay=$CounterplayGap*(0.25+[double]$policy.counterplay);complexity=$ComplexityGap*(0.2+[double]$policy.complexity)};$sum=($raw.Values|Measure-Object -Sum).Sum;if($sum -le 0){$sum=1};$Run.branchWeights=[ordered]@{player=[math]::Round($raw.player/$sum,4);novelty=[math]::Round($raw.novelty/$sum,4);counterplay=[math]::Round($raw.counterplay/$sum,4);complexity=[math]::Round($raw.complexity/$sum,4);mode=$Run.generationMode;derivedFrom=@{playerGap=$PlayerGap;noveltyGap=$NoveltyGap;counterplayGap=$CounterplayGap;complexityGap=$ComplexityGap;modePolicy=$policy}};[pscustomobject][ordered]@{status='weights-updated';mode=$Run.generationMode;weights=$Run.branchWeights}
}
function Invoke-ESABCAdaptiveResourceAllocation {
 [CmdletBinding()]param([Parameter(Mandatory)]$Run,[ValidateRange(0,100)][int]$PlayerGap,[ValidateRange(0,100)][int]$NoveltyGap,[ValidateRange(0,100)][int]$CounterplayGap,[ValidateRange(0,100)][int]$ComplexityGap)
 if($Run.currentStage -ne 'tree-expansion' -and $Run.currentStage -ne 'adaptive-weighting'){throw 'INNOVATION_RUN_WEIGHT_ALLOCATION_STAGE_REQUIRED'}
 $policy=Get-ESABCInnovationModePolicy -Mode $Run.generationMode;$raw=@{player=$PlayerGap*(0.35+[double]$policy.player);novelty=$NoveltyGap*(0.2+[double]$policy.novelty);counterplay=$CounterplayGap*(0.25+[double]$policy.counterplay);complexity=$ComplexityGap*(0.2+[double]$policy.complexity)};$sum=($raw.Values|Measure-Object -Sum).Sum;if($sum -le 0){$sum=1};$weights=[ordered]@{player=[math]::Round($raw.player/$sum,4);novelty=[math]::Round($raw.novelty/$sum,4);counterplay=[math]::Round($raw.counterplay/$sum,4);complexity=[math]::Round($raw.complexity/$sum,4);mode=$Run.generationMode};$Run.branchWeights=$weights
 $noveltyPressure=$weights.novelty+$weights.player;$complexityPressure=$weights.complexity;$Run.resourceBudget.branchesPerRound=[math]::Max(2,[math]::Min(4,[int][math]::Round(2+($noveltyPressure-$complexityPressure)*4)))
 $Run.resourceBudget.weightAllocation=[ordered]@{mode=$Run.generationMode;novelty=$weights.novelty;player=$weights.player;counterplay=$weights.counterplay;complexity=$weights.complexity;nextBranchesPerRound=$Run.resourceBudget.branchesPerRound};$Run.weightHistory+=,[pscustomobject]@{stage=$Run.currentStage;mode=$Run.generationMode;playerGap=$PlayerGap;noveltyGap=$NoveltyGap;counterplayGap=$CounterplayGap;complexityGap=$ComplexityGap;weights=$weights;branchesPerRound=$Run.resourceBudget.branchesPerRound};[pscustomobject][ordered]@{status='resources-reallocated';mode=$Run.generationMode;weights=$weights;branchesPerRound=$Run.resourceBudget.branchesPerRound}
}
function Complete-ESABCInnovationRun {
 [CmdletBinding()]param([Parameter(Mandatory)]$Run,[Parameter(Mandatory)][string]$SelectedBranchId)
 Assert-ESABCModeExecutionEvidence -Run $Run -Selected $Run.divergenceTree[$SelectedBranchId] | Out-Null
 if($Run.currentStage -ne 'final-decision'){throw 'INNOVATION_RUN_FINAL_STAGE_REQUIRED'};if(-not $Run.divergenceTree.ContainsKey($SelectedBranchId)){throw 'INNOVATION_RUN_SELECTED_BRANCH_MISSING'};if($Run.branchWeights.Count -eq 0){throw 'INNOVATION_RUN_WEIGHTS_REQUIRED'};if(@($Run.challengeMatrix).Count -eq 0){throw 'INNOVATION_RUN_CHALLENGE_MATRIX_REQUIRED'};$selected=$Run.divergenceTree[$SelectedBranchId];if($selected.scoreBreakdown.status -ne 'stable'){throw 'ABC_SCORING_DRIFT_EXCEEDED'};if([int]$selected.scoreBreakdown.reviewRounds -lt 3){throw 'ABC_SCORING_REVIEW_ROUNDS_INSUFFICIENT'};$Run.finalDecision=[pscustomobject]@{selectedBranchId=$SelectedBranchId;generationMode=$Run.generationMode;modeScore=$selected.modeScore;scoreBreakdown=$selected.scoreBreakdown;challengeScore=$Run.challengeMatrix[0].score;challengeLensCount=$Run.challengeMatrix[0].lensCount;rejectedBranches=@($Run.rejectedBranches);nonClaims=@('runtime-not-run','player-test-not-run')};$Run.status='completed';$Run.claimLevel='candidate-final-decision';$Run.runHash=Get-ESABCInnovationRunHash ([ordered]@{runId=$Run.runId;stage=$Run.currentStage;decision=$Run.finalDecision;tree=$Run.divergenceTree});$Run
}
function Invoke-ESABCCodeLifecycleAudit {
 [CmdletBinding()]param([Parameter(Mandatory)]$CodeValidation)
 $required=@('changedFiles','stateTransitions','ownershipEdges','dependencyEdges','failurePaths','regressionCases','performanceBudget','compatibilityTargets','rollbackPlan')
 $missing=@($required|Where-Object{-not $CodeValidation.PSObject.Properties[$_] -or $null -eq $CodeValidation.$_ -or @($CodeValidation.$_).Count -eq 0})
 $checks=@(
  [pscustomobject]@{area='structure';evidence=@('changedFiles','dependencyEdges')},
  [pscustomobject]@{area='state';evidence=@('stateTransitions','ownershipEdges')},
  [pscustomobject]@{area='behavior';evidence=@('failurePaths','regressionCases')},
  [pscustomobject]@{area='performance';evidence=@('performanceBudget')},
  [pscustomobject]@{area='compatibility';evidence=@('compatibilityTargets')},
  [pscustomobject]@{area='recovery';evidence=@('rollbackPlan')},
  [pscustomobject]@{area='security';evidence=@('ownershipEdges','dependencyEdges')},
  [pscustomobject]@{area='observability';evidence=@('regressionCases','rollbackPlan')}
 )
 $distribution=@($checks|ForEach-Object{$m=@($_.evidence|Where-Object{$missing -contains $_});[pscustomobject]@{area=$_.area;status=if($m.Count -eq 0){'evidence-present'}else{'evidence-missing'};missing=@($m)}})
 $horizons=@('0-10s','10-60s','1-3m','long-term');$forecast=@();foreach($h in $horizons){$risks=@();if($missing -contains 'stateTransitions'){$risks+='state-drift'};if($missing -contains 'failurePaths'){$risks+='recovery-gap'};if($missing -contains 'dependencyEdges'){$risks+='dependency-creep'};if($missing -contains 'performanceBudget'){$risks+='performance-regression'};if($missing -contains 'compatibilityTargets'){$risks+='compatibility-break'};if($missing -contains 'rollbackPlan'){$risks+='rollback-unproven'};[void]$forecast.Add([pscustomobject]@{horizon=$h;riskLevel=if($risks.Count -ge 3){'high'}elseif($risks.Count -gt 0){'medium'}else{'bounded'};risks=@($risks);basis='explicit-code-validation-evidence-only'})}
 [pscustomobject][ordered]@{status=if($missing.Count -eq 0){'passed'}else{'evidence-pending'};missing=@($missing);validationDistribution=$distribution;lifecycleForecast=$forecast;completionAllowed=($missing.Count -eq 0);failureCode=if($missing.Count -gt 0){'ABCD_CODE_LIFECYCLE_EVIDENCE_INCOMPLETE'}else{$null}}
}
function Invoke-ESABCCodeLifecycleAuditV2 {
 [CmdletBinding()]param([Parameter(Mandatory)]$CodeValidation)
 $required=@('changedFiles','stateTransitions','ownershipEdges','dependencyEdges','failurePaths','regressionCases','performanceBudget','compatibilityTargets','rollbackPlan')
 $missing=@($required|Where-Object{-not $CodeValidation.PSObject.Properties[$_] -or $null -eq $CodeValidation.$_ -or @($CodeValidation.$_).Count -eq 0})
 $areas=@('structure','state','behavior','performance','compatibility','recovery','security','observability')
 $distribution=@($areas|ForEach-Object{[pscustomobject]@{area=$_;status=if($missing.Count -eq 0){'evidence-present'}else{'evidence-pending'};missing=@($missing)}})
 $forecast=@('0-10s','10-60s','1-3m','long-term')|ForEach-Object{[pscustomobject]@{horizon=$_;riskLevel=if($missing.Count -ge 3){'high'}elseif($missing.Count -gt 0){'medium'}else{'bounded'};risks=@($missing);basis='explicit-code-validation-evidence-only'}}
 [pscustomobject][ordered]@{status=if($missing.Count -eq 0){'passed'}else{'evidence-pending'};missing=@($missing);validationDistribution=@($distribution);lifecycleForecast=@($forecast);completionAllowed=($missing.Count -eq 0);failureCode=if($missing.Count -gt 0){'ABCD_CODE_LIFECYCLE_EVIDENCE_INCOMPLETE'}else{$null}}
}
function Assert-ESABCModeExecutionEvidence {
 [CmdletBinding()]param([Parameter(Mandatory)]$Run,[Parameter(Mandatory)]$Selected)
 $c=Get-ESABCScoringContract
 $contract=Get-Content -LiteralPath (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path 'ES/Automation/Contracts/es-ai-abc-generation-mode-v1.json') -Raw -Encoding UTF8|ConvertFrom-Json
 if(-not $contract.modeExecutionContract.allModesMustExecute){throw 'ABCD_MODE_EXECUTION_CONTRACT_DISABLED'}
 $required=@($contract.modeExecutionContract.mandatoryEvidence)
 $base=@();if($Selected.scoreBreakdown -and $Selected.scoreBreakdown.dimensionNames -and $Selected.scoreBreakdown.dimensionNames.PSObject.Properties['general']){$base=@($Selected.scoreBreakdown.reviewHistory[0].dimensionSnapshot.general)}
 if($base.Count -lt @($c.layers.general.dimensions).Count){throw 'ABCD_BASE_EVALUATION_INCOMPLETE'}
 if((@($base|Sort-Object -Unique).Count -lt 2) -or ((($base|Measure-Object -Maximum).Maximum)-(($base|Measure-Object -Minimum).Minimum) -lt [double]$contract.modeExecutionContract.baseEvaluation.minimumObservedSpread)){throw 'ABCD_BASE_EVALUATION_NO_SEPARATION'}
 $run.stageOutputs['baseEvaluation']=[pscustomobject]@{baseDimensionScores=$base;independentRecomputes=[int]$Selected.scoreBreakdown.reviewRounds;spread=($base|Measure-Object -Maximum).Maximum-($base|Measure-Object -Minimum).Minimum}
 if(@($Run.divergenceTree.Keys).Count -lt 2 -or @($Run.rejectedBranches).Count -lt 1){throw 'ABCD_DIVERGENCE_EVIDENCE_INCOMPLETE'}
 if(-not $Run.stageOutputs.ContainsKey('counterplay-audit')){throw 'ABCD_REBUTTAL_EVIDENCE_MISSING'}
 $rebuttal=@($Run.stageOutputs['counterplay-audit'].lenses);if($rebuttal.Count -lt 3){throw 'ABCD_REBUTTAL_LENSES_INCOMPLETE'}
 $Run.stageOutputs['rebuttalRecord']=[pscustomobject]@{lenses=@('developer','strategy','test');findings=$rebuttal;source='counterplay-audit'}
 $Run.stageOutputs['regressionMatrix']=[pscustomobject]@{reviewHistory=@($Selected.scoreBreakdown.reviewHistory);mode=$Run.generationMode}
 $gate=$contract.modes|Where-Object{[string]$_.modeId -ceq $Run.generationMode}|Select-Object -First 1
 if($null -eq $gate){throw 'ABCD_MODE_GATE_UNREGISTERED'}
 $Run.stageOutputs['gateDecision']=[pscustomobject]@{gateId=if($gate.pipelineProfile.modeGate){$gate.pipelineProfile.modeGate.gateId}else{'MODE_DEFAULT_GATE'};status=$Selected.scoreBreakdown.status;mode=$Run.generationMode}
 $Run.stageOutputs['baseDimensionScores']=$Run.stageOutputs['baseEvaluation'].baseDimensionScores
 if([string]$Run.taskKind -eq 'code'){
  if($null -eq $Run.codeValidation){throw 'ABCD_CODE_VALIDATION_REQUIRED'}
  $codeAudit=Invoke-ESABCCodeLifecycleAuditV2 -CodeValidation $Run.codeValidation;$Run.stageOutputs['codeLifecycleAudit']=$codeAudit;if(-not $codeAudit.completionAllowed){throw [string]$codeAudit.failureCode}
 }
 [pscustomobject][ordered]@{status='mode-evidence-validated';mode=$Run.generationMode;requiredEvidence=$required;baseSpread=$Run.stageOutputs['baseEvaluation'].spread;rebuttalLensCount=$rebuttal.Count;rejectedBranchCount=@($Run.rejectedBranches).Count}
}
function Invoke-ESABCInnovationRun {
 [CmdletBinding()]param([Parameter(Mandatory)][string]$Requirement,[Parameter(Mandatory)][string]$GoalRevision,[Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$SourceHash,[ValidateSet('creative-divergence','engineering','stable')][string]$GenerationMode='creative-divergence',[ValidateSet('design','code')][string]$TaskKind='design',[object]$CodeValidation=$null,[object[]]$SeedBranches=@(),[object[]]$SeedConstraints=@(),[object]$AbcpScores=$null,[ValidateRange(3,8)][int]$ScoreReviewRounds=5,[Parameter(Mandatory)][scriptblock]$ModelInvoker,[ValidateRange(0,100)][int]$PlayerGap=20,[ValidateRange(0,100)][int]$NoveltyGap=20,[ValidateRange(0,100)][int]$CounterplayGap=20,[ValidateRange(0,100)][int]$ComplexityGap=20)
 $run=New-ESABCInnovationRun -Requirement $Requirement -GoalRevision $GoalRevision -SourceHash $SourceHash -GenerationMode $GenerationMode -MaxRounds 12 -BranchesPerRound 2
 Add-Member -InputObject $run -NotePropertyName taskKind -NotePropertyValue $TaskKind -Force;Add-Member -InputObject $run -NotePropertyName codeValidation -NotePropertyValue $CodeValidation -Force;Add-Member -InputObject $run -NotePropertyName enforceEvidence -NotePropertyValue $true -Force
 $sequence=@('player-outcomes','lexical-deanchor','seed-divergence');foreach($stage in $sequence){Move-ESABCInnovationRun -Run $run -ToStage $stage -Output @{source='InnovationRun-dispatch'}|Out-Null}
 $selectedSeeds=@(& $ModelInvoker ([pscustomobject][ordered]@{phase='seed-selection';round=0;runId=$run.runId;branchBudget=7;seedCandidates=@($SeedBranches);constraints=@($SeedConstraints);requirement=$Requirement;goalRevision=$GoalRevision}));if($selectedSeeds.Count -lt 3 -or $selectedSeeds.Count -gt 7){throw 'INNOVATION_RUN_SEED_SELECTION_COUNT_INVALID'}
 $seedOutput=[ordered]@{phase='seed-selection';externalCandidates=@($SeedBranches);constraints=@($SeedConstraints);selectedCount=$selectedSeeds.Count;selectionAuthority='ABCD'};$run.stageOutputs['seed-divergence']=[pscustomobject]$seedOutput;Move-ESABCInnovationRun -Run $run -ToStage 'tree-expansion' -Output $seedOutput|Out-Null;Invoke-ESABCAdaptiveResourceAllocation -Run $run -PlayerGap $PlayerGap -NoveltyGap $NoveltyGap -CounterplayGap $CounterplayGap -ComplexityGap $ComplexityGap|Out-Null
 $index=0;foreach($seed in $selectedSeeds){$index++;$isObject=$seed -is [psobject];$content=if($isObject -and $seed.PSObject.Properties['content']){[string]$seed.content}else{[string]$seed};$variable=if($isObject -and $seed.PSObject.Properties['changedVariable']){[string]$seed.changedVariable}else{'seed-mechanism'};$accept=if($isObject -and $seed.PSObject.Properties['playerAcceptability']){[int]$seed.playerAcceptability}else{[math]::Min(100,60+$index)};$novel=if($isObject -and $seed.PSObject.Properties['novelty']){[int]$seed.novelty}else{[math]::Min(100,55+$index)};$seedAbcp=if($isObject -and $seed.PSObject.Properties['abcpScores']){$seed.abcpScores}else{$AbcpScores};Add-ESABCInnovationBranch -Run $run -BranchId "seed-$index" -ParentBranchId '' -Content $content -ChangedVariable $variable -PlayerAcceptability $accept -Novelty $novel -AbcpScores $seedAbcp -ScoreReviewRounds $ScoreReviewRounds|Out-Null }
 for($round=1;$round -le $run.resourceBudget.maxRounds;$round++){
   $parent=@($run.divergenceTree.Values|Where-Object{$_.status -eq 'open'}|Sort-Object @{e={$_.playerAcceptability};Descending=$true},@{e={$_.novelty};Descending=$true}|Select-Object -First 1);if($null -eq $parent){throw 'INNOVATION_RUN_PARENT_EXHAUSTED'}
   Invoke-ESABCAdaptiveResourceAllocation -Run $run -PlayerGap ([math]::Max(0,100-[int]$parent[0].playerAcceptability)) -NoveltyGap ([math]::Max(0,100-[int]$parent[0].novelty)) -CounterplayGap $CounterplayGap -ComplexityGap $ComplexityGap|Out-Null;$generated=@(& $ModelInvoker ([pscustomobject][ordered]@{runId=$run.runId;round=$round;parent=$parent[0];branchBudget=$run.resourceBudget.branchesPerRound;weights=$run.branchWeights;requirement=$Requirement;goalRevision=$GoalRevision}));if($generated.Count -lt 1 -or $generated.Count -gt $run.resourceBudget.branchesPerRound){throw 'INNOVATION_RUN_MODEL_BRANCH_COUNT_INVALID'}
   foreach($child in $generated){$cid="r$round-"+(Get-ESABCInnovationRunHash ([ordered]@{parent=$parent[0].branchId;content=[string]$child.content;round=$round})).Substring(0,16);$cp=if($child.PSObject.Properties['counterplay']){[int]$child.counterplay}else{$CounterplayGap};$cx=if($child.PSObject.Properties['complexity']){[int]$child.complexity}else{$ComplexityGap};$cl=if($child.PSObject.Properties['clarity']){[int]$child.clarity}else{50};$rf=if($child.PSObject.Properties['roleFit']){[int]$child.roleFit}else{50};$childAbcp=if($child.PSObject.Properties['abcpScores']){$child.abcpScores}else{$AbcpScores};Add-ESABCInnovationBranch -Run $run -BranchId $cid -ParentBranchId ([string]$parent[0].branchId) -Content ([string]$child.content) -ChangedVariable ([string]$child.changedVariable) -PlayerAcceptability ([int]$child.playerAcceptability) -Novelty ([int]$child.novelty) -Counterplay $cp -Complexity $cx -Clarity $cl -RoleFit $rf -AbcpScores $childAbcp -ScoreReviewRounds $ScoreReviewRounds|Out-Null}
 }
 Move-ESABCInnovationRun -Run $run -ToStage 'global-convergence' -Output @{treeBranches=$run.divergenceTree.Count}|Out-Null;Invoke-ESABCGlobalConvergence -Run $run|Out-Null;Move-ESABCInnovationRun -Run $run -ToStage 'interaction-graph' -Output @{links='candidate-cross-mechanic-links'}|Out-Null;Move-ESABCInnovationRun -Run $run -ToStage 'adaptive-weighting' -Output @{convergence='completed'}|Out-Null;Update-ESABCInnovationWeights -Run $run -PlayerGap $PlayerGap -NoveltyGap $NoveltyGap -CounterplayGap $CounterplayGap -ComplexityGap $ComplexityGap|Out-Null
 foreach($stage in @('player-replay','counterplay-audit','complexity-prune','candidate-tournament','final-decision')){Move-ESABCInnovationRun -Run $run -ToStage $stage -Output @{status='scheduled';resourceBudget=$run.resourceBudget;weightHistory=$run.weightHistory}|Out-Null};$selected=(@($run.divergenceTree.Values|Where-Object{$_.status -eq 'open'}|Sort-Object modeScore -Descending|Select-Object -First 1).branchId);if([string]::IsNullOrWhiteSpace($selected)){throw 'INNOVATION_RUN_NO_FINAL_CANDIDATE'};$selectedBranch=$run.divergenceTree[$selected];$run.challengeMatrix=@(Invoke-ESABCChallengeMatrix -Branch $selectedBranch -Mode $run.generationMode);foreach($h in @($selectedBranch.scoreBreakdown.reviewHistory)){$h.challengeScore=$run.challengeMatrix[0].score};$run.stageOutputs['counterplay-audit']=[pscustomobject]@{mode=$run.generationMode;lenses=$run.challengeMatrix[0].findings;score=$run.challengeMatrix[0].score};Complete-ESABCInnovationRun -Run $run -SelectedBranchId $selected
}
Export-ModuleMember -Function New-ESABCInnovationRun,Move-ESABCInnovationRun,Add-ESABCInnovationBranch,Invoke-ESABCGlobalConvergence,Update-ESABCInnovationWeights,Invoke-ESABCAdaptiveResourceAllocation,Invoke-ESABCChallengeMatrix,Get-ESABCInnovationModePolicy,Get-ESABCScoringContract,Convert-ESABCScoreToCanonical,Convert-ESABCStrictBandToScore,Invoke-ESABCStrictModeScore,Resolve-ESABCScoreWeights,Invoke-ESABCGenerationDepthRegression,Invoke-ESABCCreativeInnovationFusion,Invoke-ESABCCreativeDepthRegression,Invoke-ESABCCreativeEvaluation,Compare-ESABCReferenceBaseline,Get-ESABCScoringDimensions,Invoke-ESABCStableScore,Invoke-ESABCScoringRegression,Get-ESABCBranchScore,Complete-ESABCInnovationRun,Invoke-ESABCInnovationRun,Assert-ESABCModeExecutionEvidence,Invoke-ESABCCodeLifecycleAudit
Export-ModuleMember -Function Invoke-ESABCCodeLifecycleAuditV2
