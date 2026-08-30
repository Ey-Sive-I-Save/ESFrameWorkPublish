[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$PreflightPath,
  [Parameter(Mandatory=$true)][string]$OutputPath,
  [string]$ModelResponsePath=''
)
$ErrorActionPreference='Stop';$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
# AI-owned page instance: this adapter may validate/map a real response, but never invents layout, content or interaction.
$root=[IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\')+'\'
function Resolve-RootPath([string]$p){$f=if([IO.Path]::IsPathRooted($p)){[IO.Path]::GetFullPath($p)}else{[IO.Path]::GetFullPath((Join-Path (Get-Location) $p))};if(-not $f.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'Path outside project root.'};$f}
function Write-Receipt($value,[string]$path){$out=Resolve-RootPath $path;New-Item -ItemType Directory -Path (Split-Path $out) -Force|Out-Null;$json=$value|ConvertTo-Json -Depth 40;[IO.File]::WriteAllText($out,$json,[Text.UTF8Encoding]::new($false));$json}
$preflightFull=Resolve-RootPath $PreflightPath
$pf=Get-Content -LiteralPath $preflightFull -Raw -Encoding UTF8|ConvertFrom-Json
$common=[ordered]@{schemaVersion=2;recordType='WebPageStudioDeepDesignSpec';designEngine='ESWebPageStudioDeepDesign.AIAdapter';sourcePreflightPath=$preflightFull.Substring($root.Length).Replace('\','/');sourcePromptHash=(Get-FileHash -LiteralPath $preflightFull -Algorithm SHA256).Hash.ToLowerInvariant();runtimeStatus='runtime-not-run';nonClaims=@('no browser, network or release proof','adapter does not author design decisions','review is not acceptance')}
if([string]$pf.status -ne 'accepted'){ $r=[ordered]@{}+$common;$r.status='blocked';$r.designStatus='review';$r.decisionStatus='review';$r.blockedReason='P0_DESIGN_NOT_ACCEPTED: preflight is not accepted.';Write-Receipt $r $OutputPath|Out-Null;throw $r.blockedReason }
if([string]::IsNullOrWhiteSpace($ModelResponsePath)){
  $r=[ordered]@{}+$common;$r.status='review';$r.designStatus='review';$r.decisionStatus='review';$r.objective=[string]$pf.intent.objective;$r.blockedReason='BLOCKED_WEB_DEEP_DESIGN_MODEL_RESPONSE_REQUIRED';$r.requiredResponse=@('objectiveBrief','securityContract','semanticHtml','informationArchitecture','visualDirection','interactionModel','implementationPlan','filesToCreate','filesToModify','riskAssessment','rejectedAlternatives','componentInventory','interactionStateGraph','responsiveMatrix','motionTimeline','a11yChecks','interactionContracts','viewContracts','detailContract');Write-Receipt $r $OutputPath|Out-Null;throw $r.blockedReason
}
$responseFull=Resolve-RootPath $ModelResponsePath
if(-not(Test-Path -LiteralPath $responseFull -PathType Leaf)){throw "BLOCKED_WEB_DEEP_DESIGN_MODEL_RESPONSE_MISSING:$ModelResponsePath"}
$doc=Get-Content -LiteralPath $responseFull -Raw -Encoding UTF8|ConvertFrom-Json
$entry=@($doc.responses|Where-Object {[string]$_.phase -eq 'page-design-instantiation'}|Select-Object -First 1)
if(-not $entry){throw 'BLOCKED_WEB_DEEP_DESIGN_PAGE_RESPONSE_NOT_FOUND'}
$o=$entry[0].outputs|Select-Object -First 1
$providerRunId=[string]$doc.providerRunId
if([string]::IsNullOrWhiteSpace($providerRunId) -or $providerRunId.ToLowerInvariant().StartsWith('normalized-') -or $providerRunId.ToLowerInvariant().Contains('synthetic') -or $providerRunId.ToLowerInvariant().Contains('fixture')){throw 'BLOCKED_WEB_DEEP_DESIGN_PROVIDER_ID_INVALID'}
if($null -eq $o.provenance -or [string]$o.provenance.actor -notin @('current-ai-session','provider')){throw 'BLOCKED_WEB_DEEP_DESIGN_AI_PROVENANCE_REQUIRED'}
$required=@('objectiveBrief','securityContract','semanticHtml','informationArchitecture','visualDirection','interactionModel','implementationPlan','filesToCreate','filesToModify','riskAssessment','rejectedAlternatives','componentInventory','interactionStateGraph','responsiveMatrix','motionTimeline','a11yChecks','interactionContracts','viewContracts','detailContract')
$missing=@();foreach($name in $required){$p=$o.PSObject.Properties[$name];if($null -eq $p -or $null -eq $p.Value -or ([string]$p.Value -is [string] -and [string]::IsNullOrWhiteSpace([string]$p.Value)) -or ($p.Value -is [System.Array] -and @($p.Value).Count -eq 0)){$missing+=$name}}
if($missing.Count -gt 0 -or [string]$o.aiAnalysis -notmatch '.{40}' -or [string]$o.execution -notmatch '.{20}' -or $null -eq $o.returnReceipt -or $null -eq $o.evidence){throw ('BLOCKED_WEB_DEEP_DESIGN_MODEL_RESPONSE_INCOMPLETE:' + ($missing -join ','))}
$qualityRules = @(
  @{ name='objectiveBrief'; min=80 }, @{ name='informationArchitecture'; min=120 },
  @{ name='visualDirection'; min=120 }, @{ name='interactionModel'; min=160 },
  @{ name='implementationPlan'; min=160 }, @{ name='securityContract'; min=80 }
)
foreach($rule in $qualityRules){
  $fieldName=[string]$rule.name; $v=[string]$o.$fieldName
  if($v.Trim().Length -lt [int]$rule.min){throw "BLOCKED_WEB_DEEP_DESIGN_MODEL_RESPONSE_SHALLOW:$fieldName"}
  if($v -like '*placeholder*' -or $v -like '*todo*' -or $v -like '*lorem ipsum*' -or $v -like '*待补充*'){throw ('BLOCKED_WEB_DEEP_DESIGN_MODEL_RESPONSE_PLACEHOLDER:' + $fieldName)}
}
$design=[ordered]@{}+$common;$design.status='accepted';$design.designStatus='accepted';$design.decisionStatus='accepted';$design.modelResponsePath=$ModelResponsePath.Replace('\','/');$design.modelResponseHash=(Get-FileHash -LiteralPath $responseFull -Algorithm SHA256).Hash.ToLowerInvariant();$design.providerRunId=$providerRunId;$design.phase=[string]$entry[0].phase;$design.generationMode=[string]$entry[0].generationMode;$design.round=[int]$entry[0].round
foreach($name in $required){$design[$name]=$o.$name};$design.aiAnalysis=[string]$o.aiAnalysis;$design.execution=[string]$o.execution;$design.returnReceipt=$o.returnReceipt;$design.evidence=$o.evidence;$design.sourceResponseHash=$design.modelResponseHash
Write-Receipt $design $OutputPath
