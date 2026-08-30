[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$KnowledgeRouteReceiptPath,
    [Parameter(Mandatory=$true)][string]$DesignPacketPath,
    [Parameter(Mandatory=$true)][string]$ABCDReceiptPath,
    [ValidateSet('candidate','accept','reject')][string]$UserDecision='candidate',
    [string]$ReuseFromReceiptPath='',
    [string]$TemplateContractPath='.agents/skills/es-web-generation-round-05-capability-design/references/round-05-template-catalog.json',
    [string]$OutputPath='ES/Output/WebPageStudio/bootstrap/round-05-capability-design.json',
    [string]$DeepDesignPacketOutputPath='ES/Output/WebPageStudio/bootstrap/round-06-deep-design-packet.json'
)
$ErrorActionPreference='Stop'
$projectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
function Read-Json([string]$Path){
    $full=[IO.Path]::GetFullPath((Join-Path $projectRoot $Path))
    if(-not(Test-Path -LiteralPath $full -PathType Leaf)){throw "input-not-found: $Path"}
    [Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($full))|ConvertFrom-Json
}
function Hash([object]$Value){
    $sha=[Security.Cryptography.SHA256]::Create(); try {
        $json=$Value|ConvertTo-Json -Depth 60 -Compress
        ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($json)))).Replace('-','').ToLowerInvariant()
    } finally {$sha.Dispose()}
}
function Count-Items([object]$Value){ if($null -eq $Value){return 0}; return @($Value).Count }

$route=Read-Json $KnowledgeRouteReceiptPath
if([string]$route.recordType -cne 'KnowledgeRouteReceipt' -or [string]$route.status -notin @('accepted','partial')){throw 'blocked.round-05.missing-route'}
if([string]$route.status -eq 'partial' -and (Count-Items ($route.selectedEntries|Where-Object {$_.status -eq 'blocked'})) -gt 0){throw 'blocked.round-05.route-contains-hard-block'}
$expectedRoutePlanHash=if($route.PSObject.Properties['routePlanHash']){[string]$route.routePlanHash}else{[string]$route.routeHash}
$gate=& (Join-Path $projectRoot '.agents/skills/es-web-generation-round-05-capability-design/scripts/Test-ESRound05ABCDHardGate.ps1') -ReceiptPath (Join-Path $projectRoot $ABCDReceiptPath) -TaskId ([string]$route.taskId) -RoutePlanHash $expectedRoutePlanHash -SourceScopeHash ([string]$route.sourceScopeHash)
if($LASTEXITCODE -and $LASTEXITCODE -ne 0){throw 'blocked.round-05.abcd-hard-gate'}

$packet=Read-Json $DesignPacketPath
$templateContract=Read-Json $TemplateContractPath
if([string]$packet.recordType -cne 'AiWebDesignPacket'){throw 'blocked.round-05.design-packet-type-invalid'}
if([string]$packet.taskId -cne [string]$route.taskId){throw 'blocked.round-05.task-binding-mismatch'}
if($null -eq $packet.PSObject.Properties['aiDesignEvidence'] -or $null -eq $packet.aiDesignEvidence){throw 'blocked.round-05.ai-design-evidence-required'}
if([string]$packet.aiDesignEvidence.actor -notin @('current-ai-session','provider') -or [string]::IsNullOrWhiteSpace([string]$packet.aiDesignEvidence.responseHash)){throw 'blocked.round-05.ai-design-evidence-invalid'}
$evidencePath=[string]$packet.aiDesignEvidence.responsePath
if([string]::IsNullOrWhiteSpace($evidencePath)){throw 'blocked.round-05.ai-design-response-path-required'}
$evidenceFull=if([IO.Path]::IsPathRooted($evidencePath)){[IO.Path]::GetFullPath($evidencePath)}else{[IO.Path]::GetFullPath((Join-Path $projectRoot $evidencePath))}
if(-not $evidenceFull.StartsWith($projectRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or -not(Test-Path -LiteralPath $evidenceFull -PathType Leaf)){throw 'blocked.round-05.ai-design-response-missing'}
$actualEvidenceHash=(Get-FileHash -LiteralPath $evidenceFull -Algorithm SHA256).Hash.ToLowerInvariant()
if($actualEvidenceHash -cne ([string]$packet.aiDesignEvidence.responseHash).ToLowerInvariant()){throw 'blocked.round-05.ai-design-response-hash-mismatch'}
if([string]::IsNullOrWhiteSpace([string]$packet.aiDesignEvidence.analysis) -or [string]::IsNullOrWhiteSpace([string]$packet.aiDesignEvidence.selectionRationale) -or [string]::IsNullOrWhiteSpace([string]$packet.aiDesignEvidence.execution) -or $null -eq $packet.aiDesignEvidence.returnReceipt){throw 'blocked.round-05.ai-design-analysis-incomplete'}
if($packet.PSObject.Properties['knowledgeRouteHash'] -and [string]$packet.knowledgeRouteHash -and [string]$packet.knowledgeRouteHash -cne [string]$route.routeHash){throw 'blocked.round-05.route-hash-mismatch'}
if([string]$packet.PSObject.Properties['templateSource'] -and -not [string]::IsNullOrWhiteSpace([string]$packet.templateSource)){throw 'blocked.round-05.template-rejection'}

# Validate the task-authored packet shape. No page, brand, metadata, DOM or numeric budget is assumed here.
foreach($name in @('resourceBudget','resourceUsage','subagentPlan','subagentReceipts','innovationRun','abccNegotiation','templateLibrary','motionEffectLibrary','effectUsagePolicy','styleProfile','innovationProfile','acceptanceAssertions')){
    if($null -eq $packet.PSObject.Properties[$name] -or $null -eq $packet.$name){throw "blocked.round-05.design-incomplete: $name"}
}
$candidates=@($packet.divergenceCandidates)
if($candidates.Count -lt 3){throw 'blocked.round-05.divergence-insufficient'}
if((Count-Items ($candidates|Where-Object {$_.decision -eq 'selected'})) -lt 1){throw 'blocked.round-05.selection-missing'}
function Assert-MeaningfulText([object]$Value,[string]$Field,[int]$Min=30){
    if([string]::IsNullOrWhiteSpace([string]$Value) -or ([string]$Value).Trim().Length -lt $Min){throw "blocked.round-05.candidate-design-content-missing:$Field"}
}
foreach($candidate in $candidates){
    Assert-MeaningfulText $candidate.candidateId 'candidateId' 3
    Assert-MeaningfulText $candidate.title 'title' 8
    Assert-MeaningfulText $(if($candidate.description){$candidate.description}else{$candidate.summary}) 'description' 40
    Assert-MeaningfulText $candidate.interactionModel 'interactionModel' 40
    Assert-MeaningfulText $candidate.visualRationale 'visualRationale' 40
    Assert-MeaningfulText $(if($candidate.tradeoffs){$candidate.tradeoffs}else{$candidate.riskAssessment}) 'tradeoffs' 30
}
if((Count-Items ($candidates|Where-Object {$_.discardReason -and -not [string]::IsNullOrWhiteSpace([string]$_.discardReason)})) -lt 1){throw 'blocked.round-05.divergence-insufficient: discard reasons required'}
foreach($kind in @('interactionTemplates','cardTemplates','pageTemplates')){if((Count-Items $packet.templateLibrary.$kind) -eq 0){throw "blocked.round-05.template-contract-missing: $kind"}}
if((Count-Items $templateContract) -eq 0){throw 'blocked.round-05.template-contract-empty'}
if((Count-Items $packet.motionEffectLibrary) -eq 0 -and (Count-Items $packet.motionEffectLibrary.effects) -eq 0){throw 'blocked.round-05.effect-policy-missing'}
if($null -eq $packet.effectUsagePolicy){throw 'blocked.round-05.effect-policy-missing'}
if($null -eq $packet.styleProfile -or $null -eq $packet.innovationProfile.limit){throw 'blocked.round-05.style-profile-missing'}
$stagePlan=if($packet.innovationRun.stagePlan){@($packet.innovationRun.stagePlan)}else{@()}
$trace=if($packet.innovationRun.iterationTrace){@($packet.innovationRun.iterationTrace)}else{@()}
if([string]$packet.innovationRun.selectionAuthority -and [string]$packet.innovationRun.selectionAuthority -cne 'ABCD'){throw 'blocked.round-05.innovation-authority-invalid'}
if($stagePlan.Count -eq 0 -and $trace.Count -eq 0 -and [string]$packet.innovationRun.status -notin @('review','completed','accepted')){throw 'blocked.round-05.innovation-run-missing'}
$children=if($packet.subagentPlan.children){@($packet.subagentPlan.children)}else{@($packet.subagentPlan)}
if($children.Count -eq 0 -or (Count-Items $packet.subagentReceipts) -lt $children.Count){throw 'blocked.round-05.child-join-incomplete'}
if($packet.abccNegotiation.status -and [string]$packet.abccNegotiation.status -in @('blocked','missing')){throw 'blocked.round-05.abcc-negotiation-missing'}

$status=if($UserDecision -eq 'accept'){'accepted'}elseif($UserDecision -eq 'reject'){'rejected'}else{'candidate'}
$packetHash=Hash $packet
$deep=[ordered]@{schemaVersion=1;recordType='DeepDesignPacket';roundId='web-generation-round-06';stageId='deep-design-input';designStatus=$status;taskId=$route.taskId;sourceDesignPacketPath=$DesignPacketPath;sourceDesignPacketHash=$packetHash;templateContractPath=$TemplateContractPath;templateContractHash=(Hash $templateContract);requirement=[string]$packet.taskIntent;capabilityProfile=$packet.templateLibrary;motionEffectPolicy=$packet.motionEffectLibrary;styleProfile=$packet.styleProfile;innovationProfile=$packet.innovationProfile;acceptanceAssertions=@($packet.acceptanceAssertions);nonClaims=@('Round05 does not emit HTML/CSS/JS','no page-specific markup or metadata was generated','no runtime, browser, network, Unity or release evidence')}
$deepFull=[IO.Path]::GetFullPath((Join-Path $projectRoot $DeepDesignPacketOutputPath));$deepDir=Split-Path -Parent $deepFull;if(-not(Test-Path -LiteralPath $deepDir)){New-Item -ItemType Directory -Path $deepDir -Force|Out-Null};[IO.File]::WriteAllText($deepFull,($deep|ConvertTo-Json -Depth 60),[Text.UTF8Encoding]::new($false))
$reuse=$null;$reuseHash=$null;if($ReuseFromReceiptPath){$reuse=Read-Json $ReuseFromReceiptPath;$reuseHash=(Get-FileHash (Join-Path $projectRoot $ReuseFromReceiptPath) -Algorithm SHA256).Hash.ToLowerInvariant()}
$receipt=[ordered]@{schemaVersion=1;recordType='DesignSystemProfileReceipt';roundId='web-generation-round-05';stageId='design-system-profile';status=$status;taskId=$route.taskId;taskRevision=$route.taskRevision;contextVersion=$route.contextVersion;knowledgeRouteHash=$route.routeHash;designPacketPath=$DesignPacketPath;designPacketHash=$packetHash;templateContractPath=$TemplateContractPath;templateContractHash=(Hash $templateContract);resourceBudget=$packet.resourceBudget;resourceUsage=$packet.resourceUsage;subagentPlan=$packet.subagentPlan;subagentReceipts=$packet.subagentReceipts;innovationRun=$packet.innovationRun;abccNegotiation=$packet.abccNegotiation;templateLibrary=$packet.templateLibrary;motionEffectLibrary=$packet.motionEffectLibrary;effectUsagePolicy=$packet.effectUsagePolicy;styleProfile=$packet.styleProfile;innovationProfile=$packet.innovationProfile;acceptanceAssertions=$packet.acceptanceAssertions;reuseBinding=[ordered]@{enabled=($null -ne $reuse);sourceReceipt=$ReuseFromReceiptPath;sourceReceiptHash=$reuseHash};aiAnalysis=[string]$packet.aiDesignEvidence.analysis;execution=[string]$packet.aiDesignEvidence.execution;decision=if($status -eq 'accepted'){'user-accepted-for-round-06'}elseif($status -eq 'rejected'){'user-rejected'}else{'awaiting-user-acceptance'};returnReceipt=[ordered]@{status=$status;aiReturn=$packet.aiDesignEvidence.returnReceipt;nextRound='web-generation-round-06-deep-design-and-html';deepDesignPacketPath=$DeepDesignPacketOutputPath};nonClaims=@('not requirement-specific page design','not HTML/CSS/JS','not visual/runtime proof','not Unity/network/release')}
$outFull=[IO.Path]::GetFullPath((Join-Path $projectRoot $OutputPath));$outDir=Split-Path -Parent $outFull;if(-not(Test-Path -LiteralPath $outDir)){New-Item -ItemType Directory -Path $outDir -Force|Out-Null};[IO.File]::WriteAllText($outFull,($receipt|ConvertTo-Json -Depth 60),[Text.UTF8Encoding]::new($false))
[pscustomobject]@{status=$status;outputPath=$outFull;deepDesignPacketPath=$deepFull;taskId=$route.taskId;designPacketHash=$packetHash;candidateCount=$candidates.Count}
