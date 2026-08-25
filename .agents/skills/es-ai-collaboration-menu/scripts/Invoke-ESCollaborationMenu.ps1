[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$PromptText,[string]$ContextJson="{}")
$ErrorActionPreference="Stop"
$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$root=Split-Path $PSScriptRoot -Parent
$options=Get-Content -LiteralPath (Join-Path $root "references/menu-options.json") -Raw -Encoding utf8|ConvertFrom-Json
$sessionSubmenu=Get-Content -LiteralPath (Join-Path $root "references/session-submenu.json") -Raw -Encoding utf8|ConvertFrom-Json
$intentRules=Get-Content -LiteralPath (Join-Path $root "references/intent-rules.json") -Raw -Encoding utf8|ConvertFrom-Json
$areaRules=Get-Content -LiteralPath (Join-Path $root "references/area-rules.json") -Raw -Encoding utf8|ConvertFrom-Json
$negationRules=Get-Content -LiteralPath (Join-Path $root "references/negation-rules.json") -Raw -Encoding utf8|ConvertFrom-Json
$submenuCatalog=Get-Content -LiteralPath (Join-Path $root "references/menu-submenus.json") -Raw -Encoding utf8|ConvertFrom-Json
$allowed=@("taskKind","projectArea","routeStatus","contextFreshness","riskLevel")
$sets=@{taskKind=@("create","iterate","govern","validate","discover","collaborate","unknown");projectArea=@("gamecore","resource","entity","input","editor","ui","shader","graph","session","unknown");routeStatus=@("resolved","ambiguous","missing","unknown");contextFreshness=@("fresh","stale","unknown");riskLevel=@("low","high","unknown")}
if([string]::IsNullOrWhiteSpace($PromptText)){throw "PromptText must not be empty."}
try{$input=$ContextJson|ConvertFrom-Json}catch{throw "ContextJson must be valid JSON."}
foreach($p in @($input.PSObject.Properties)){if($p.Name -notin $allowed){throw "Unsupported context field: $($p.Name)"}}
$signals=[ordered]@{}
foreach($name in $allowed){$value=[string]$input.$name;if([string]::IsNullOrWhiteSpace($value)){$value="unknown"};if($value -notin $sets[$name]){throw "Invalid context value for ${name}: $value"};$signals[$name]=$value}
$text=$PromptText.Trim();$intentCandidates=@();$negatedIntents=@();$evidence=@();$operation=""
foreach($rule in @($intentRules.rules)) {
  foreach($term in @($rule.terms)) {
    $termText=[string]$term;$index=$text.IndexOf($termText,[StringComparison]::OrdinalIgnoreCase)
    if($index -lt 0){continue}
    $prefixStart=[Math]::Max(0,$index-8);$prefixLength=$index-$prefixStart;$prefix=if($prefixLength -gt 0){$text.Substring($prefixStart,$prefixLength)}else{""}
    $negated=$false;foreach($marker in @($negationRules.markers)){if($prefix.Contains([string]$marker)){$negated=$true;break}}
    if($negated){$negatedIntents += [string]$rule.id;continue}
    $intentCandidates += [pscustomobject]@{id=[string]$rule.id;optionId=[string]$rule.optionId;weight=[int]$rule.weight;operation=[string]$rule.operation;index=$index;matchedTerm=$termText}
    $evidence += @(([string]$rule.id)+"-language")
    break
  }
}
$orderedIntents=@($intentCandidates|Sort-Object -Property index,weight);$rankedIntents=@($intentCandidates|Sort-Object -Property weight,index);$top=$rankedIntents|Select-Object -First 1;$second=$rankedIntents|Select-Object -Skip 1 -First 1;$confidence="low";if($top){if($top.weight -ge 90 -and (-not $second -or $top.weight - $second.weight -ge 15)){$confidence="high"}elseif($top.weight -ge 55){$confidence="medium"}}
$compoundPlan=@($orderedIntents|Group-Object optionId|ForEach-Object {$_.Group|Select-Object -First 1})
$compoundPlan=@($compoundPlan|Where-Object {$_.optionId})
$areaMatches=@();foreach($rule in @($areaRules.rules)){foreach($term in @($rule.terms)){if($text.Contains([string]$term)){$areaMatches += [string]$rule.area;break}}};$inferredArea=if($areaMatches.Count -gt 0){$areaMatches[0]}else{"unknown"}
$explicitContext=$false;foreach($name in $allowed){if([string]$input.$name -and [string]$input.$name -ne "unknown"){$explicitContext=$true}}
$recommended="";$reason="";$decisionSource="derived-semantic";$recommendedSubmenu=""
if($signals.routeStatus -in @("ambiguous","missing") -or $signals.contextFreshness -eq "stale"){$recommended="discover-context";$reason="stale-or-ambiguous-context";$decisionSource="derived-context-safety"}
elseif($orderedIntents.Count -gt 1 -and $confidence -ne "low"){$stage=$orderedIntents[0];$recommended=[string]$stage.optionId;$reason="compound-intent:$($stage.id)";$decisionSource="derived-semantic";$operation=[string]$stage.operation;if($stage.optionId -eq "coordinate-session"){$recommendedSubmenu=[string]$stage.id}}
elseif($top -and $confidence -ne "low"){$recommended=[string]$top.optionId;$reason="semantic-intent:$($top.id)";$operation=[string]$top.operation;if($top.optionId -eq "coordinate-session"){$recommendedSubmenu=[string]$top.id}}
elseif($signals.taskKind -eq "validate"){$recommended="validate-evidence";$reason="validation-task";$decisionSource="derived-context"}
elseif($signals.taskKind -eq "create"){$recommended="create-content";$reason="creation-task";$decisionSource="derived-context"}
elseif($signals.taskKind -eq "iterate"){$recommended="iterate-feature";$reason="iteration-task";$decisionSource="derived-context"}
elseif($signals.taskKind -eq "govern" -or $signals.riskLevel -eq "high"){$recommended="govern-framework";$reason="governance-or-high-risk-task";$decisionSource="derived-context"}
elseif($signals.taskKind -eq "collaborate"){$recommended="coordinate-session";$reason="collaboration-task";$decisionSource="derived-context"}
else{$recommended="discover-context";$reason="insufficient-intent-context";$decisionSource="derived-fallback"}
$rendered=@();$number=0
$openBracket=[char]0x3010;$closeBracket=[char]0x3011
foreach($option in @($options.options)) {
  $number++
  $entry=[ordered]@{id=[string]$option.id;label="$openBracket$number$closeBracket$([string]$option.label)";number=$number;numberLabel="$openBracket$number$closeBracket";reason=[string]$option.reason;risk=[string]$option.risk;routeKeys=@($option.routeKeys);relatedSkills=@($option.relatedSkills);recommended=([string]$option.id -eq $recommended);requiresUserChoice=$true;capability="present-and-route-only"}
  $submenuSource=$null
  if($option.id -eq "coordinate-session"){$submenuSource=$sessionSubmenu}
  elseif($submenuCatalog.submenus.PSObject.Properties[$option.id]){$submenuSource=$submenuCatalog.submenus.PSObject.Properties[$option.id].Value}
  if($submenuSource) {
    $sub=@()
    $subNumber=0
    foreach($item in @($submenuSource.options)) {
      $subNumber++;$itemId=[string]$item.id;$itemLabel=[string]$item.label;$itemOperation=if($item.operation){[string]$item.operation}else{"Route"};$itemRoute=if($item.routeKey){[string]$item.routeKey}else{"$($option.id)"};$itemCapability=if($item.capability){[string]$item.capability}else{"present-and-route-only"}
      $related=@($item.relatedSkills|Where-Object {$_});$sub += [ordered]@{id=$itemId;label="$openBracket$subNumber$closeBracket$itemLabel";number=$subNumber;numberLabel="$openBracket$subNumber$closeBracket";operation=$itemOperation;routeKey=$itemRoute;relatedSkills=$related;capability=$itemCapability;requiresExactIdentity=([bool]$item.requiresExactIdentity);requiresLaunchAcceptance=([bool]$item.requiresLaunchAcceptance);usesHandoffOrchestrator=([bool]$item.usesHandoffOrchestrator);requiresUserChoice=$true;recommended=($itemId -eq $recommendedSubmenu)}
    }
    $submenuId=if($submenuSource.submenuId){[string]$submenuSource.submenuId}else{"$($option.id).v1"};$entry.submenu=[ordered]@{submenuId=$submenuId;options=$sub;nonClaims=@("Submenu options are navigation only","No submenu option was executed","User choice is required before route handling")}
  }
  $rendered += $entry
}
$primaryIntent="unknown";if($orderedIntents.Count -gt 0){$primaryIntent=[string]$orderedIntents[0].id}
$compound=@($compoundPlan|ForEach-Object {[ordered]@{id=$_.id;optionId=$_.optionId;operation=$_.operation;order=([array]::IndexOf($compoundPlan,$_) + 1)}})
$intent=[ordered]@{primary=$primaryIntent;operation=$operation;confidence=$confidence;candidates=@($rankedIntents|Select-Object -First 3|ForEach-Object {[ordered]@{id=$_.id;optionId=$_.optionId;operation=$_.operation;weight=$_.weight;matchedTerm=$_.matchedTerm}});negatedIntents=@($negatedIntents|Select-Object -Unique);compoundPlan=$compound;conflicts=@();requiresClarification=($confidence -eq "low" -and $rankedIntents.Count -gt 0);inferredProjectArea=$inferredArea;evidence=@($evidence|Select-Object -Unique);userSignalsAreOptional=$true}
$sha=[Security.Cryptography.SHA256]::Create();$promptHash=([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))).Replace("-","")).ToLowerInvariant();$rulesHash=(Get-FileHash -LiteralPath (Join-Path $root "references/intent-rules.json") -Algorithm SHA256).Hash.ToLowerInvariant();$menuHash=(Get-FileHash -LiteralPath (Join-Path $root "references/menu-options.json") -Algorithm SHA256).Hash.ToLowerInvariant()
[ordered]@{schemaVersion=1;menuId=[string]$options.menuId;promptText=$text;signals=$signals;intent=$intent;recommendedOptionId=$recommended;recommendedSubmenuId=$recommendedSubmenu;recommendationReason=$reason;decisionSource=$decisionSource;capabilityPolicy=[ordered]@{canPresent=$true;canRecommend=$true;canInterpretIntent=$true;canRoute=$true;canDispatch=$false;canWrite=$false;canRunRuntime=$false;canStartProcess=$false;canUseNetwork=$false;canChangeGit=$false;canPublish=$false};decisionReceipt=[ordered]@{receiptType="menu-decision";promptHash=$promptHash;intentRulesHash=$rulesHash;menuSchemaHash=$menuHash;selectedOptionId=$null;readSet=@();runtimeStatus="runtime-not-run";nonClaims=@("Inference is not project fact","No action executed")};options=$rendered;nonClaims=@("Menu and submenu options are navigation only","Natural-language intent is an interpretation, not authoritative project fact","No option was executed","Fork is not window handoff","Window handoff requires the session bootstrap Skill and its acceptance gate","No write, Runtime, Git, network, release, or credential authority was granted")}|ConvertTo-Json -Depth 14
