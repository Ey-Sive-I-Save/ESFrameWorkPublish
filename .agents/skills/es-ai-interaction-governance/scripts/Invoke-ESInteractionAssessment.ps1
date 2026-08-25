[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$PromptText,
  [ValidateSet('default','engineering','handover','release','minimal')][string]$Profile='default',
  [string]$EvidenceText='',
  [string]$IntentEvidenceText='',
  [string]$ClaimEvidenceText='',
  [switch]$UserCorrection,
  [switch]$PriorMisalignment,
  [switch]$CurrentIntentConfirmed,
  [switch]$UncertaintyDisclosed,
  [int]$PriorHighScoreStreak=0,
  [string]$PreviousObjective='',
  [switch]$WritesRequested,
  [switch]$RuntimeRequired,
  [switch]$HandoffIntent,
  [switch]$TaskStarted,
  [ValidateSet('read-only','write','release','unknown')][string]$TaskKind='unknown',
  [ValidateSet('resolved','ambiguous','missing','unknown')][string]$RouteStatus='unknown',
  [ValidateSet('fresh','stale','unknown')][string]$ContextFreshness='unknown',
  [ValidateSet('low','high','unknown')][string]$RiskLevel='unknown',
  [switch]$AlreadyCollected,
  [switch]$ContextCollectionRecommended,
  [switch]$AllowTestOverride,
  [string]$ReportPath
)

$ErrorActionPreference='Stop'
$base=Split-Path $PSScriptRoot -Parent
$profiles=Get-Content (Join-Path $base 'references/evaluation-profiles.json') -Raw -Encoding utf8|ConvertFrom-Json
$tree=Get-Content (Join-Path $base 'references/next-step-behavior-tree.json') -Raw -Encoding utf8|ConvertFrom-Json
$text=if($null -eq $PromptText){''}else{$PromptText.Trim()}
$evidence=if($null -eq $EvidenceText){''}else{$EvidenceText.Trim()}
$weights=$profiles.profiles.$Profile.promptWeights
$dimensionWeights=$profiles.profiles.$Profile.dimensionWeights
$signals=[ordered]@{
  objective=[bool]($text -match '(?i)\u505a|\u5b9e\u73b0|\u4fee\u6539|\u68c0\u67e5|\u521b\u5efa|\u5206\u6790|\u5b8c\u6210|\u8bc4\u4ef7|\u8bc4\u5206|\u95ee\u9898|\u5f3a\u5316|\u6539\u8fdb|\u8c03\u6574|build|fix|review|implement')
  target=[bool]($text -match '(?i)\u6587\u4ef6|\u76ee\u5f55|Skill|\u77e5\u8bc6\u5e93|\u7a97\u53e3|\u9879\u76ee|\u811a\u672c|\u4f53\u7cfb|\u4ea4\u4e92|\u610f\u56fe|\u8bc4\u5206|route|file|skill|project')
  scope=[bool]($text -match '(?i)\u8303\u56f4|\u4ec5|\u53ea|\u4e0d\u8981|\u4fdd\u6301|\u9650\u5b9a|\u6bcf\u6b21|\u6301\u7eed|\u4e0d\u5e94\u8be5|scope|only|preserve')
  acceptance=[bool]($text -match '(?i)\u9a8c\u8bc1|\u9a8c\u6536|\u901a\u8fc7|\u7ed3\u679c|\u6807\u51c6|\u8bc1\u636e|verify|accept|evidence')
  constraints=[bool]($text -match '(?i)\u4e0d|\u7981\u6b62|\u4e0d\u80fd|\u5fc5\u987b|\u8981\u6c42|\u7ea6\u675f|\u4e0d\u5f97|must|shall|without')
}
$total=0;$earned=0
foreach($p in $weights.PSObject.Properties){$total+=[int]$p.Value;if($signals[$p.Name]){$earned+=[int]$p.Value}}
$promptScore=if($total -eq 0){0}else{[math]::Round(10*$earned/$total,1)}
$feedbackMode=[bool]($text -match '(?i)\u89c9\u5f97|\u95ee\u9898|\u4e0d\u5e94|\u4e0d\u53ef\u80fd|\u6307\u51fa|\u5efa\u8bae|\u53cd\u9988|feedback|concern')
if($feedbackMode -and !$WritesRequested -and !$RuntimeRequired -and !$HandoffIntent){$promptScore=[math]::Min(7,$promptScore)}
$objectiveClarity=if($promptScore -ge 8){'clear'}elseif($promptScore -ge 5){'partial'}else{'unclear'}
$intentMarkers=@('intent','objective','goal','understand','match','alignment','user','request','scope','\u610f\u56fe','\u76ee\u6807','\u590d\u8ff0','\u5951\u5408','\u7406\u89e3','\u8981\u6c42','\u8303\u56f4')|Where-Object{$IntentEvidenceText -match "(?i)$_"}
$intentEvidenceCount=($intentMarkers|Measure-Object).Count
$intentAlignmentScore=if([string]::IsNullOrWhiteSpace($IntentEvidenceText)){3}else{[math]::Min(8,4+$intentEvidenceCount)}
if($CurrentIntentConfirmed){$intentAlignmentScore=[math]::Min(10,$intentAlignmentScore+1)}
if($UserCorrection -and !$CurrentIntentConfirmed){$intentAlignmentScore=[math]::Max(2,$intentAlignmentScore-1)}
$evidenceKinds=@('hash','receipt','static','runtime','test','\u9a8c\u8bc1','\u9a8c\u6536','\u8bc1\u636e')|Where-Object{$evidence -match "(?i)$_"}
$verificationScore=if([string]::IsNullOrWhiteSpace($evidence)){0}else{[math]::Min(8,2+($evidenceKinds|Measure-Object).Count*1.5)}
$evidenceQualityScore=if([string]::IsNullOrWhiteSpace($ClaimEvidenceText)){[math]::Min(5,$verificationScore)}else{[math]::Min(9,4+([regex]::Matches($ClaimEvidenceText,'(?i)claim|evidence|mapping|\u4e3b\u5f20|\u8bc1\u636e|\u5bf9\u5e94|\u652f\u6301')).Count)}
$calibrationScore=5
if($PriorHighScoreStreak -ge 2){$calibrationScore-=2}
if($PriorMisalignment){$calibrationScore-=1}
if($UncertaintyDisclosed){$calibrationScore+=1}
$calibrationScore=[math]::Max(0,[math]::Min(10,$calibrationScore))
$overallRaw=(($promptScore*[double]$dimensionWeights.prompt)+($intentAlignmentScore*[double]$dimensionWeights.intent)+($evidenceQualityScore*[double]$dimensionWeights.evidence))/(($dimensionWeights.prompt)+($dimensionWeights.intent)+($dimensionWeights.evidence))
$shortfall=[math]::Min($promptScore,[math]::Min($intentAlignmentScore,$evidenceQualityScore))+1
$confidenceScore=[math]::Min(10,2+$intentEvidenceCount+($evidenceKinds|Measure-Object).Count)
$overallScore=[math]::Round((($overallRaw*0.7)+(5*0.3))+($calibrationScore-5)*0.1,1)
$diagnosticReasons=@()
if($promptScore -lt 7){$diagnosticReasons+='prompt-scope-incomplete'}
if($intentAlignmentScore -lt 7){$diagnosticReasons+='current-intent-evidence-weak'}
if($verificationScore -lt 7){$diagnosticReasons+='claim-evidence-insufficient'}
if($PriorMisalignment){$diagnosticReasons+='prior-misalignment-recorded'}
if($confidenceScore -lt 6){$diagnosticReasons+='low-evidence-confidence'}
$warnIcon=[char]::ConvertFromUtf32(0x26A0);$dangerIcon=[char]::ConvertFromUtf32(0x1F6A8)
$riskNotice=if($overallScore -lt 4){"$dangerIcon LOW_SCORE_RISK: intent or evidence insufficient; do not claim completion or verification"}elseif($overallScore -lt 7){"$warnIcon LIMITED_EVIDENCE: state the conclusion conservatively"}else{''}
$goalDrift='none';if($PreviousObjective){$goalDrift=if(($PreviousObjective -split '\s+'|?{$_}).Count -gt 0 -and $text -notmatch [regex]::Escape(($PreviousObjective -split '\s+'|Select-Object -First 1))){'possible'}else{'none'}}
$runtimeStatus=if($RuntimeRequired){'not-run'}else{'not-applicable'}
$recommendationReasons=[Collections.Generic.List[string]]::new()
if($RouteStatus -in @('ambiguous','missing')){[void]$recommendationReasons.Add('ambiguous-route')}
if($ContextFreshness -in @('stale','unknown')){[void]$recommendationReasons.Add('stale-or-unknown-context')}
if($RiskLevel -eq 'high' -and $TaskKind -in @('write','release')){[void]$recommendationReasons.Add('high-risk-write-or-release')}
$suppressedBy=[Collections.Generic.List[string]]::new()
if(!$TaskStarted){[void]$suppressedBy.Add('task-not-started')}
if($AlreadyCollected){[void]$suppressedBy.Add('already-collected')}
$derivedRecommendation=([bool]$TaskStarted -and !$AlreadyCollected -and $recommendationReasons.Count -gt 0)
$decisionSource='derived'
if($AllowTestOverride){$derivedRecommendation=[bool]$ContextCollectionRecommended;$decisionSource='test-override'}
$ctx=[ordered]@{objectiveClarity=$objectiveClarity;goalDrift=$goalDrift;writesRequested=[bool]$WritesRequested;runtimeRequired=[bool]$RuntimeRequired;runtimeStatus=$runtimeStatus;handoffIntent=[bool]$HandoffIntent;taskStarted=[bool]$TaskStarted;contextCollectionRecommended=$derivedRecommendation;verificationScoreBelow=($verificationScore -lt 7)}
$next=@($tree.rules|Sort-Object priority -Descending|Where-Object{
  $w=$_.when; $ok=$true
  foreach($prop in $w.PSObject.Properties){$key=$prop.Name;$expected=$prop.Value;if($key -eq 'verificationScoreBelow'){$ok=$ok -and ($ctx.verificationScoreBelow -eq [bool]$expected)}else{$ok=$ok -and ($ctx[$key] -eq $expected)}}
  $ok
}|Select-Object -First ([int]$tree.maxSuggestions)|ForEach-Object -Begin {$option=0} -Process {$option++;[ordered]@{number=$option;id=$_.id;label=$_.label;reason=$_.reason;risk=$_.risk;requiresUserChoice=[bool]$_.requiresUserChoice;userInput=[string]$option}})
$result=[ordered]@{schemaVersion=1;skill='es-ai-interaction-governance';profile=$Profile;promptScore=$promptScore;verificationScore=$verificationScore;intentAlignmentScore=$intentAlignmentScore;evidenceQualityScore=$evidenceQualityScore;calibrationScore=$calibrationScore;confidenceScore=$confidenceScore;overallScore=$overallScore;scoreSource='deterministic-assessment';riskNotice=$riskNotice;diagnosticReasons=$diagnosticReasons;objectiveClarity=$objectiveClarity;goalDrift=$goalDrift;runtimeStatus=$runtimeStatus;taskStarted=[bool]$TaskStarted;taskKind=$TaskKind;routeStatus=$RouteStatus;contextFreshness=$ContextFreshness;riskLevel=$RiskLevel;alreadyCollected=[bool]$AlreadyCollected;contextCollectionRecommended=$derivedRecommendation;recommendationReasons=@($recommendationReasons);suppressedBy=@($suppressedBy);decisionSource=$decisionSource;claimsNotProven=@(if($RuntimeRequired){'Runtime behavior not proven'});nextSteps=$next;nonClaims=@('Scores are advisory','Suggestions are not executed')}
$json=$result|ConvertTo-Json -Depth 8
if($ReportPath){$full=[IO.Path]::GetFullPath($ReportPath);$dir=Split-Path $full -Parent;if(!(Test-Path $dir)){New-Item -ItemType Directory -Path $dir -Force|Out-Null};[IO.File]::WriteAllText($full,$json,(New-Object Text.UTF8Encoding($false)))}
$json
