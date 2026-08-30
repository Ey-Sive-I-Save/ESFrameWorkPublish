[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$DesignSystemReceiptPath,
 [Parameter(Mandatory=$true)][string]$DesignPacketPath,
 [Parameter(Mandatory=$true)][string]$ArtifactPath,
 [Parameter(Mandatory=$true)][string]$OutputReceiptPath,
 [Parameter(Mandatory=$true)][string]$ABCDReceiptPath,
 [ValidateRange(5,32)][int]$SelfReviewRounds=5,
 [string]$AiDesignTaskPath,
 [string[]]$AiRevisionReceiptPaths,
 [string[]]$RevisionPaths,
 [switch]$StartFromBlank
)
$ErrorActionPreference='Stop';$root=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
function ReadJ([string]$p){if(-not(Test-Path -LiteralPath $p -PathType Leaf)){throw "missing-input:$p"};[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes((Resolve-Path $p).Path))|ConvertFrom-Json}
function ReadUtf8([string]$p){if(-not(Test-Path -LiteralPath $p -PathType Leaf)){throw "missing-revision:$p"};[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes((Resolve-Path $p).Path))}
function Sha([string]$s){$h=[Security.Cryptography.SHA256]::Create();try{([BitConverter]::ToString($h.ComputeHash([Text.Encoding]::UTF8.GetBytes($s)))).Replace('-','').ToLowerInvariant()}finally{$h.Dispose()}}
$sys=ReadJ $DesignSystemReceiptPath;$design=ReadJ $DesignPacketPath;$abcd=ReadJ $ABCDReceiptPath
if([string]$sys.recordType -cne 'DesignSystemProfileReceipt' -or [string]$sys.status -cne 'accepted'){throw 'blocked.round-06.missing-upstream-design-system'}
if([string]$design.designStatus -cne 'accepted' -and [string]$design.status -cne 'accepted'){throw 'blocked.round-06.design-not-accepted'}
foreach($n in @('objectiveBrief','informationArchitecture','componentInventory','interactionStateGraph','responsiveMatrix','motionTimeline','a11yChecks','interactionContracts','viewContracts','detailContract','securityContract','semanticHtml')){$p=$design.PSObject.Properties[$n];$v=if($null -ne $p){$p.Value}else{$null};$missing=($null -eq $p) -or (($v -is [array]) -and @($v).Count -eq 0) -or (($v -isnot [array]) -and [string]::IsNullOrWhiteSpace([string]$v));if($missing){throw "blocked.round-06.design-incomplete:$n"}}
foreach($n in @('invocationId','eventSequence','candidateCount','candidateHashes','selectedCandidateId','decision')){if($null -eq $abcd.PSObject.Properties[$n]){throw "blocked.round-06.abcd-missing:$n"}}
if([int]$abcd.candidateCount -lt 3 -or @($abcd.candidateHashes).Count -lt 3){throw 'blocked.round-06.abcd-divergence-insufficient'}

# Round06 is an AI design-and-code orchestrator. A plain list of changed HTML files
# is not evidence of an AI turn; require the task contract and real revision receipts.
if([string]::IsNullOrWhiteSpace($AiDesignTaskPath)){throw 'blocked.round-06.ai-agent-contract-required'}
if($null -eq $AiRevisionReceiptPaths -or @($AiRevisionReceiptPaths).Count -lt 1){throw 'blocked.round-06.ai-agent-revision-required'}
$taskPreview=ReadJ $AiDesignTaskPath
if([string]$taskPreview.recordType -cne 'AiWebDesignTask' -or @($taskPreview.allowedWriteRoots).Count -lt 1 -or @($taskPreview.requiredChecks).Count -lt 1 -or [int]$taskPreview.minimumRevisionRounds -lt 1){throw 'blocked.round-06.ai-agent-execution-contract-incomplete'}
$admissionScript=Join-Path $PSScriptRoot 'Test-ESRound06AiDesignAgentReceipt.ps1'
$admission=& $admissionScript -TaskPath $AiDesignTaskPath -ReceiptPaths $AiRevisionReceiptPaths -ProjectRoot $root
if([string]$admission.status -cne 'admitted'){throw 'blocked.round-06.ai-agent-not-admitted'}

# AI owns the design. This materializer never invents a layout or UI copy. The accepted packet is the initial candidate;
# subsequent rounds must be supplied as real AI revision artifacts. Missing revisions are a hard stop, not a fake pass.
$candidates=@([pscustomobject]@{round=0;path=$DesignPacketPath;html=[string]$design.semanticHtml;source='ai-authored-design-packet'})
$r=0;foreach($a in @($admission.revisions)){$r++;$candidates+=[pscustomobject]@{round=$r;path=$a.artifactPath;html=(ReadUtf8 $a.artifactPath);source='ai-agent-revision-receipt'}}
if($candidates.Count -lt ($SelfReviewRounds+1)){throw "blocked.round-06.ai-candidate-missing:expected=$SelfReviewRounds,provided=$($candidates.Count-1)"}
$trace=@();$previous=$candidates[0].html
for($round=1;$round -le $SelfReviewRounds;$round++){
 $current=$candidates[$round].html
 if([string]::IsNullOrWhiteSpace($current)){throw "blocked.round-06.empty-ai-revision:$round"}
 if($current -eq $previous){throw "blocked.round-06.ai-revision-unchanged:$round"}
 if($current -match [char]0xfffd){throw "blocked.round-06.encoding-replacement:$round"}
 $trace+=[ordered]@{round=$round;source=$candidates[$round].source;sourcePath=$candidates[$round].path.Replace('\','/');beforeHash=(Sha $previous);afterHash=(Sha $current);beforeBytes=[Text.Encoding]::UTF8.GetByteCount($previous);afterBytes=[Text.Encoding]::UTF8.GetByteCount($current);byteDelta=[Text.Encoding]::UTF8.GetByteCount($current)-[Text.Encoding]::UTF8.GetByteCount($previous);changedSelectors='ai-authored-html-diff';changedFunctions='ai-authored-js-diff';contractDiff='ai-authored-design-revision';interactionGate='requires-static-replay';layoutGate='requires-browser-or-dom-replay'}
 $previous=$current
}
$html=$previous
foreach($m in @('loading','empty','error','success','dialog','application/ld+json','prefers-reduced-motion')){if($html -notmatch [regex]::Escape($m)){throw "blocked.round-06.required-marker:$m"}}
$full=[IO.Path]::GetFullPath((Join-Path $root $ArtifactPath));$parent=Split-Path -Parent $full;if(-not(Test-Path $parent)){New-Item -ItemType Directory $parent -Force|Out-Null};[IO.File]::WriteAllText($full,$html,[Text.UTF8Encoding]::new($false))
$hash=(Get-FileHash $full -Algorithm SHA256).Hash.ToLowerInvariant();$out=[IO.Path]::GetFullPath((Join-Path $root $OutputReceiptPath));$op=Split-Path -Parent $out;if(-not(Test-Path $op)){New-Item -ItemType Directory $op -Force|Out-Null}
$receipt=[ordered]@{schemaVersion=1;recordType='DeepDesignHtmlReceipt';roundId='web-generation-round-06';stageId='deep-design-and-html';status='static-generated';taskId=$sys.taskId;designPacketHash=(Sha ([string]($design|ConvertTo-Json -Depth 80 -Compress)));abcdInvocationId=$abcd.invocationId;selectedCandidateId=$abcd.selectedCandidateId;artifactPath=$ArtifactPath.Replace('\','/');artifactHash=$hash;artifactBytes=[Text.Encoding]::UTF8.GetByteCount($html);aiMaterialization=[ordered]@{mode='ai-design-and-code-orchestration';agentTaskPath=$AiDesignTaskPath.Replace('\','/');revisionReceiptPaths=@($AiRevisionReceiptPaths|ForEach-Object{$_.Replace('\','/')});revisionCount=@($admission.revisions).Count;templateGeneration='forbidden';fixedRoundMutation='forbidden';timeOnlyProgress='forbidden'};selfReview=[ordered]@{rounds=$SelfReviewRounds;trace=$trace;overall='review-required'};designBindings=[ordered]@{informationArchitecture=@($design.informationArchitecture).Count;components=@($design.componentInventory).Count;interactionContracts=@($design.interactionContracts).Count;responsiveProfiles=@($design.responsiveMatrix).Count;motionEntries=@($design.motionTimeline).Count;views=@($design.viewContracts).Count};staticChecks=@('utf8','candidate-diff','design-to-dom-markers','no-template-round-mutation','ai-agent-receipt-admission');execution='AI agent authored design/code revisions; scripts only admitted receipts, materialized selected artifact, and collected static evidence. No network, browser, Unity or release execution.';decision='static-generated';returnReceipt=[ordered]@{status='static-generated';next='run-static-and-browser-replay'};nonClaims=@('does-not-prove browser rendering','does-not-prove visual quality','does-not-prove runtime/network/release') }|ConvertTo-Json -Depth 80
[IO.File]::WriteAllText($out,$receipt,[Text.UTF8Encoding]::new($false));[pscustomobject]@{status='static-generated';artifactPath=$full;artifactHash=$hash;receiptPath=$out;artifactBytes=[Text.Encoding]::UTF8.GetByteCount($html)}
