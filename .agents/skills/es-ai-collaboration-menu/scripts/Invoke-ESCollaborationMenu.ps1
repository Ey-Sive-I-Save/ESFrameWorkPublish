[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$PromptText,[string]$ContextJson="{}",[string]$Selection="")
$ErrorActionPreference="Stop"
$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$sharedPathBoundary=[IO.Path]::GetFullPath((Join-Path $projectRoot '.agents\skills\es-skill-governance\scripts\ESPathBoundary.Common.ps1'))
if(-not (Test-Path -LiteralPath $sharedPathBoundary -PathType Leaf)){throw 'Shared path boundary contract is missing.'}
. $sharedPathBoundary
$root=(Resolve-ESContainedRelativePath -Candidate '.agents/skills/es-ai-collaboration-menu' -ContainerRoot $projectRoot -Label 'SkillRoot').FullPath
# `Resolve-ESContainedRelativePath` intentionally rejects `.` because it
# models a strict child path. The project root itself is already derived from
# this Skill's fixed location; resolve it directly without weakening child
# path checks below.
$projectRoot=(Resolve-Path -LiteralPath $projectRoot -ErrorAction Stop).Path
# Consume the canonical super-semantics resolver at the menu boundary.  The
# menu remains navigation-only, but it must not re-implement or bypass the
# authoritative semantic decision and its scan/claim metadata.
$superSemanticResolver=Join-Path $projectRoot '.agents/scripts/Resolve-ESSuperSemantics.ps1'
if(-not (Test-Path -LiteralPath $superSemanticResolver -PathType Leaf)){throw 'Canonical super-semantics resolver is missing.'}
$superSemanticResolution=& powershell -NoProfile -File $superSemanticResolver -PromptText $PromptText -ProjectRoot $projectRoot | Out-String | ConvertFrom-Json
$referencesRoot=(Resolve-Path (Join-Path $root 'references')).Path
$superSemanticIndex=Get-Content -LiteralPath (Join-Path $projectRoot '.agents\SUPER_SEMANTICS_REGISTRY.json') -Raw -Encoding UTF8|ConvertFrom-Json
$options=Get-Content -LiteralPath (Join-Path $referencesRoot "menu-options.json") -Raw -Encoding utf8|ConvertFrom-Json
$sessionSubmenu=Get-Content -LiteralPath (Join-Path $referencesRoot "session-submenu.json") -Raw -Encoding utf8|ConvertFrom-Json
$intentRules=Get-Content -LiteralPath (Join-Path $referencesRoot "intent-rules.json") -Raw -Encoding utf8|ConvertFrom-Json
$areaRules=Get-Content -LiteralPath (Join-Path $referencesRoot "area-rules.json") -Raw -Encoding utf8|ConvertFrom-Json
$negationRules=Get-Content -LiteralPath (Join-Path $referencesRoot "negation-rules.json") -Raw -Encoding utf8|ConvertFrom-Json
$submenuCatalog=Get-Content -LiteralPath (Join-Path $referencesRoot "menu-submenus.json") -Raw -Encoding utf8|ConvertFrom-Json
$routeDirectory=Get-Content -LiteralPath (Join-Path $referencesRoot "route-directory.json") -Raw -Encoding utf8|ConvertFrom-Json
$menuExamples=Get-Content -LiteralPath (Join-Path $referencesRoot "menu-examples.json") -Raw -Encoding utf8|ConvertFrom-Json
$pageGuidance=@{
  'create-content'=[ordered]@{title='先理解要新增的对象';purpose='确认目标模块、稳定身份、所有权和最小交付物，再选择实现入口。';intake='目标对象、已有入口、约束与验收信号';deliverable='有界实现路线、关联 Skill 和验证方式';next='补充对象名或直接选择对应子入口'}
  'iterate-feature'=[ordered]@{title='先理解现象再定位根因';purpose='区分复现、回归、兼容和性能问题，避免直接猜测修改。';intake='现象、复现步骤、预期与实际结果';deliverable='根因假设、最小修复范围和回归证据';next='提供报错、复现路径或选择根因分析'}
  'govern-framework'=[ordered]@{title='先理解权威和边界';purpose='确认规则来源、责任归属、权限范围和不可越过的副作用边界。';intake='涉及对象、权威入口、风险和禁止项';deliverable='边界判断、冲突点和可执行治理路线';next='指出要审查的规则、Skill 或模块'}
  'validate-evidence'=[ordered]@{title='先理解完成标准';purpose='把“做完”拆成静态、编译、Runtime 或发布证据，避免用低等级证据冒充完成。';intake='目标状态、验收信号、允许的运行范围';deliverable='验证矩阵、真实回执和未证实项';next='说明要验证的对象和证据等级'}
  'discover-context'=[ordered]@{title='先理解任务上下文';purpose='从最小范围发现项目事实、规则、Knowledge 和必要 Skill，不递归加载无关内容。';intake='任务目标、模块线索、当前分支和新鲜度';deliverable='上下文摘要、权威入口和最小路由集合';next='描述任务目标或给出模块线索'}
  'coordinate-session'=[ordered]@{title='先理解职责和会话边界';purpose='确认当前职责、交接对象、上下文快照和验收门，再选择新建、恢复或交接。';intake='职责、目标摘要、接收窗口和验收条件';deliverable='可接受的会话路线、上下文摘要和状态回执';next='说明要新建、恢复还是交接'}
  'ai-mechanism-atlas'=[ordered]@{title='先理解机制再选择入口';purpose='把超级语义、能力、权限、证据和公开 Agent 机制分开说明，避免把图鉴当执行器。';intake='想了解的机制、当前问题和需要的深度';deliverable='机制说明、适用边界、相关 Skill 和下一步路由';next='选择超级语义、大全/图鉴、能力、边界或证据'}
}
$allowed=@("taskKind","projectArea","routeStatus","contextFreshness","riskLevel")
$sets=@{taskKind=@("create","iterate","govern","validate","discover","collaborate","unknown");projectArea=@("gamecore","resource","entity","input","editor","ui","shader","graph","session","aispace","unknown");routeStatus=@("resolved","ambiguous","missing","unknown");contextFreshness=@("fresh","stale","unknown");riskLevel=@("low","high","unknown")}
if([string]::IsNullOrWhiteSpace($PromptText)){throw "PromptText must not be empty."}
try{$input=$ContextJson|ConvertFrom-Json}catch{throw "ContextJson must be valid JSON."}
foreach($p in @($input.PSObject.Properties)){if($p.Name -notin $allowed -and $p.Name -notin @('navigationState','menuState')){throw "Unsupported context field: $($p.Name)"}}
$navigationInput=if($input.navigationState){$input.navigationState}elseif($input.menuState){$input.menuState}else{$null}
$activeMainIndex=0
if($navigationInput -and $navigationInput.mainNumber){$activeMainIndex=[int]$navigationInput.mainNumber;if($activeMainIndex -lt 1 -or $activeMainIndex -gt 7){throw 'navigationState.mainNumber must be between 1 and 7.'}}
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
$orderedIntents=@($intentCandidates|Sort-Object -Property index,weight);$rankedIntents=@($intentCandidates|Sort-Object -Property @{Expression='weight';Descending=$true},index);$top=$rankedIntents|Select-Object -First 1;$second=$rankedIntents|Select-Object -Skip 1 -First 1;$confidence="low";if($top){if($top.weight -ge 90 -and (-not $second -or $top.weight - $second.weight -ge 15)){$confidence="high"}elseif($top.weight -ge 55){$confidence="medium"}}
$compoundPlan=@($orderedIntents|Group-Object optionId|ForEach-Object {$_.Group|Select-Object -First 1})
$compoundPlan=@($compoundPlan|Where-Object {$_.optionId})
$areaMatches=@();foreach($rule in @($areaRules.rules)){foreach($term in @($rule.terms)){if($text.Contains([string]$term)){$areaMatches += [string]$rule.area;break}}};$inferredArea=if($areaMatches.Count -gt 0){$areaMatches[0]}else{"unknown"}
$explicitContext=$false;foreach($name in $allowed){if([string]$input.$name -and [string]$input.$name -ne "unknown"){$explicitContext=$true}}
$recommended="";$reason="";$decisionSource="derived-semantic";$recommendedSubmenu=""
if($signals.routeStatus -in @("ambiguous","missing") -or $signals.contextFreshness -eq "stale"){$recommended="discover-context";$reason="stale-or-ambiguous-context";$decisionSource="derived-context-safety"}
elseif($orderedIntents.Count -gt 1 -and $confidence -ne "low" -and (-not $top -or -not $second -or ($top.weight - $second.weight -lt 15))){$stage=$orderedIntents[0];$recommended=[string]$stage.optionId;$reason="compound-intent:$($stage.id)";$decisionSource="derived-semantic";$operation=[string]$stage.operation;if($stage.optionId -eq "coordinate-session"){$recommendedSubmenu=[string]$stage.id}}
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
  $entry=[ordered]@{id=[string]$option.id;label="$openBracket$number$closeBracket$([string]$option.label)";number=$number;numberLabel="$openBracket$number$closeBracket";reason=[string]$option.reason;risk=[string]$option.risk;routeKeys=@($option.routeKeys);relatedSkills=@($option.relatedSkills);recommended=([string]$option.id -eq $recommended);requiresUserChoice=$true;capability="present-and-route-only";pageGuidance=$pageGuidance[[string]$option.id]}
  if ($option.id -eq "ai-mechanism-atlas") {
    $semanticEntries=@()
    foreach ($semanticSource in @($superSemanticIndex.sources)) {
      $semanticPath=Join-Path $projectRoot ([string]$semanticSource)
      if (-not (Test-Path -LiteralPath $semanticPath -PathType Leaf)) { throw "Super-semantics source missing for menu projection: $semanticSource" }
      $semanticRegistry=Get-Content -LiteralPath $semanticPath -Raw -Encoding UTF8|ConvertFrom-Json
      foreach ($semanticTrigger in @($semanticRegistry.triggers)) {
        $semanticEntries += [ordered]@{id=[string]$semanticTrigger.id;label=[string]$semanticTrigger.label;operation=[string]$semanticTrigger.operation;priority=[int]$semanticTrigger.priority;triggerPhrases=@($semanticTrigger.triggerPhrases);requiresUserChoice=[bool]$semanticTrigger.requiresUserChoice;allowAutonomousExpansion=[bool]$semanticTrigger.allowAutonomousExpansion;source=[string]$semanticSource}
      }
    }
    $entry.superSemantics=[ordered]@{registryId=[string]$superSemanticIndex.registryId;entries=@($semanticEntries|Sort-Object priority -Descending);cancellation=$superSemanticIndex.cancellation;precedenceRules=@($superSemanticIndex.precedenceRules|ForEach-Object {[ordered]@{id=$_.id;label=$_.label;priority=$_.priority}});nonClaims=@("目录来自中央注册表投影","目录只提供发现，不执行语义")}
  }
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
$directories=@();$directoryNumber=0
foreach($category in @($routeDirectory.categories)) {
  $directoryNumber++;$items=@();$itemNumber=0
  foreach($item in @($category.items)) {
    $itemNumber++
    $items += [ordered]@{id=[string]$item.id;label="$openBracket$directoryNumber.$itemNumber$closeBracket $([string]$item.label)";number="$directoryNumber.$itemNumber";routeKey=[string]$item.routeKey;relatedSkills=@($item.relatedSkills|Where-Object {$_});requiresUserChoice=$true;capability="present-and-route-only"}
  }
  $directories += [ordered]@{id=[string]$category.id;label="$openBracket$directoryNumber$closeBracket $([string]$category.label)";number=$directoryNumber;description=[string]$category.description;options=$items;requiresUserChoice=$true}
}
$selectionResult=$null
if(-not [string]::IsNullOrWhiteSpace($Selection)) {
  $normalizedSelection=$Selection.Trim() -replace '[【】\[\] ]',''
  if($normalizedSelection -match '^([1-7])$' -and $activeMainIndex -gt 0 -and $rendered[$activeMainIndex-1].submenu) {
    $subIndex=[int]$normalizedSelection;$selectedMain=$rendered[$activeMainIndex-1]
    if($subIndex -gt @($selectedMain.submenu.options).Count){throw "Selection is outside the current submenu: $Selection"}
    $selectedSub=$selectedMain.submenu.options[$subIndex-1]
    $selectionResult=[ordered]@{selection="$activeMainIndex.$subIndex";level="submenu";optionId=$selectedMain.id;submenuOptionId=$selectedSub.id;routeKey=$selectedSub.routeKey;requiresUserChoice=$true;capability="present-and-route-only"}
  } elseif($normalizedSelection -match '^([1-7])$') {
    $mainIndex=[int]$Matches[1]
    if($mainIndex -gt $rendered.Count){throw "Selection is outside the current menu: $Selection"}
    $selectedMain=$rendered[$mainIndex-1]
    $selectionResult=[ordered]@{selection=$Selection;level="main";optionId=$selectedMain.id;routeKey=([string]$selectedMain.routeKeys[0]);requiresUserChoice=$true;capability="present-and-route-only"}
  } elseif($normalizedSelection -match '^([1-7])\.(\d+)$') {
    $mainIndex=[int]$Matches[1];$subIndex=[int]$Matches[2]
    if($mainIndex -le $rendered.Count -and $rendered[$mainIndex-1].submenu -and $subIndex -le @($rendered[$mainIndex-1].submenu.options).Count) {
      $selectedMain=$rendered[$mainIndex-1];$selectedSub=$selectedMain.submenu.options[$subIndex-1]
      $selectionResult=[ordered]@{selection=$Selection;level="submenu";optionId=$selectedMain.id;submenuOptionId=$selectedSub.id;routeKey=$selectedSub.routeKey;requiresUserChoice=$true;capability="present-and-route-only"}
    } else {throw "Selection is outside the current submenu: $Selection"}
  } elseif($normalizedSelection -match '^R?([1-3])\.(\d+)$') {
    $categoryIndex=[int]$Matches[1];$itemIndex=[int]$Matches[2]
    if($categoryIndex -gt $directories.Count -or $itemIndex -gt @($directories[$categoryIndex-1].options).Count){throw "Selection is outside the route directory: $Selection"}
    $selectedDirectory=$directories[$categoryIndex-1];$selectedItem=$selectedDirectory.options[$itemIndex-1]
    $selectionResult=[ordered]@{selection=$Selection;level="route-directory";categoryId=$selectedDirectory.id;optionId=$selectedItem.id;routeKey=$selectedItem.routeKey;requiresUserChoice=$true;capability="present-and-route-only"}
  } else {throw "Selection must be a main number (1-7), submenu number (n.m), or route-directory number (R1.1): $Selection"}
}
$primaryIntent="unknown";if($orderedIntents.Count -gt 0){$primaryIntent=[string]$orderedIntents[0].id}
$compound=@($compoundPlan|ForEach-Object {[ordered]@{id=$_.id;optionId=$_.optionId;operation=$_.operation;order=([array]::IndexOf($compoundPlan,$_) + 1)}})
$intent=[ordered]@{primary=$primaryIntent;operation=$operation;confidence=$confidence;candidates=@($rankedIntents|Select-Object -First 3|ForEach-Object {[ordered]@{id=$_.id;optionId=$_.optionId;operation=$_.operation;weight=$_.weight;matchedTerm=$_.matchedTerm}});negatedIntents=@($negatedIntents|Select-Object -Unique);compoundPlan=$compound;conflicts=@();requiresClarification=($confidence -eq "low" -and $rankedIntents.Count -gt 0);inferredProjectArea=$inferredArea;evidence=@($evidence|Select-Object -Unique);userSignalsAreOptional=$true}
$agentShortcut=([regex]::IsMatch($text,'(?<![A-Za-z0-9_])AG(?![A-Za-z0-9_])',[Text.RegularExpressions.RegexOptions]::IgnoreCase) -or $text -match '(?i)子\s*Agent|子代理')
$contextSummary=[ordered]@{projectArea=$inferredArea;taskKind=$signals.taskKind;routeStatus=$signals.routeStatus;contextFreshness=$signals.contextFreshness;riskLevel=$signals.riskLevel;intent=$primaryIntent;confidence=$confidence;agentMode=if($agentShortcut){'sub-agent'}else{'current-window'};summary=if($inferredArea -ne 'unknown'){"当前识别领域：$inferredArea；任务类型：$($signals.taskKind)；风险：$($signals.riskLevel)"}else{'尚未识别具体领域，建议先发现上下文'}}
$quickAccess=@([ordered]@{id='agent-delegation';number='A';label='使用 AG 子 Agent 分工';reason='按当前任务上下文拆分职责；只选择编排方式，不扩大权限';recommended=$agentShortcut;requiresUserChoice=$true;capability='present-and-route-only'},[ordered]@{id='continue-with-context';number='B';label='按当前上下文继续';reason='使用当前识别到的领域、任务类型和风险继续路由';recommended=$false;requiresUserChoice=$true;capability='present-and-route-only'},[ordered]@{id='show-complete-menu';number='C';label='查看完整菜单';reason='展开七类主入口与分类路由目录';recommended=$false;requiresUserChoice=$true;capability='present-and-route-only'})
$menuIcons=@{'create-content'='✦';'iterate-feature'='↻';'govern-framework'='⌘';'validate-evidence'='✓';'discover-context'='⌕';'coordinate-session'='⇄';'ai-mechanism-atlas'='◇'}
$menuSubtitles=@{'create-content'='新建内容、配置或资源';'iterate-feature'='定位问题并改进已有功能';'govern-framework'='检查规则、架构与权限边界';'validate-evidence'='用证据确认是否真的完成';'discover-context'='先找必要的 Skill、知识与规则';'coordinate-session'='管理会话、职责与窗口协作';'ai-mechanism-atlas'='了解语义、能力、权限与证据'}
$displayOptions=@($rendered|ForEach-Object {
  $icon=if($menuIcons.ContainsKey([string]$_.id)){[string]$menuIcons[[string]$_.id]}else{'•'}
  $subtitle=if($menuSubtitles.ContainsKey([string]$_.id)){[string]$menuSubtitles[[string]$_.id]}else{''}
  $examples=@(); if($menuExamples.examples.PSObject.Properties[[string]$_.id]){$examples=@($menuExamples.examples.PSObject.Properties[[string]$_.id].Value|ForEach-Object {[string]$_})}
  $displayRecommended=([bool]$_.recommended -and -not $agentShortcut)
  [ordered]@{id=[string]$_.id;number=[int]$_.number;icon=$icon;title=([string]$_.label -replace '^【\d+】','').Trim();subtitle=$subtitle;reason=[string]$_.reason;examples=$examples;recommended=$displayRecommended}
})
$displayModel=[ordered]@{version='es-menu-display.v1';title='ES AI 协作中心';subtitle='先选方向，再进入对应 Skill；所有入口均为导航';layout='compact-cards';density='comfortable';recommendationMarker='★';sections=[ordered]@{quickAccess='快捷入口';main='工作方向';directory='分类路由'};quickAccess=@($quickAccess|ForEach-Object {[ordered]@{number=[string]$_.number;label=[string]$_.label;recommended=[bool]$_.recommended}});mainOptions=$displayOptions;skillDisclosurePolicy=[ordered]@{userFacingFormat='stable-skill-name-only';forbiddenPatterns=@('./','../','SKILL.md','references/','scripts/');internalPaths='machine-evidence-only'};footer='输入编号选择入口 · 不会自动执行动作'}
$sha=[Security.Cryptography.SHA256]::Create();$promptHash=([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))).Replace("-","")).ToLowerInvariant();$rulesHash=(Get-FileHash -LiteralPath (Join-Path $referencesRoot "intent-rules.json") -Algorithm SHA256).Hash.ToLowerInvariant();$menuHash=(Get-FileHash -LiteralPath (Join-Path $referencesRoot "menu-options.json") -Algorithm SHA256).Hash.ToLowerInvariant()
$navigationState=[ordered]@{level=if($selectionResult){[string]$selectionResult.level}else{'root'};mainNumber=if($selectionResult -and $selectionResult.optionId){[int](@($rendered|Where-Object id -eq $selectionResult.optionId|Select-Object -First 1).number)}else{$null};selectionPath=if($selectionResult){[string]$selectionResult.selection}else{$null};nextInputHint=if($selectionResult -and $selectionResult.level -eq 'main' -and $rendered[$selectionResult.selection-1].submenu){"输入子菜单编号（例如 $($selectionResult.selection).1）"}else{'输入主菜单编号'}}
[ordered]@{schemaVersion=2;menuId=[string]$options.menuId;promptText=$text;signals=$signals;contextSummary=$contextSummary;display=$displayModel;quickAccess=$quickAccess;intent=$intent;superSemanticResolution=$superSemanticResolution;recommendedOptionId=$recommended;recommendedSubmenuId=$recommendedSubmenu;recommendationReason=$reason;decisionSource=$decisionSource;selection=$selectionResult;navigationState=$navigationState;routeDirectory=[ordered]@{directoryId=[string]$routeDirectory.directoryId;title=[string]$routeDirectory.title;categories=$directories;numbering="Rcategory.item"};capabilityPolicy=[ordered]@{canPresent=$true;canRecommend=$true;canInterpretIntent=$true;canRoute=$true;canDispatch=$false;canWrite=$false;canRunRuntime=$false;canStartProcess=$false;canUseNetwork=$false;canChangeGit=$false;canPublish=$false};decisionReceipt=[ordered]@{receiptType="menu-decision";promptHash=$promptHash;intentRulesHash=$rulesHash;menuSchemaHash=$menuHash;selectedOptionId=if($selectionResult){$selectionResult.optionId}else{$null};superSemanticReceiptHash=if($superSemanticResolution){[string]$superSemanticResolution.receiptHash}else{$null};readSet=@();runtimeStatus="runtime-not-run";nonClaims=@("Inference is not project fact","No action executed")};options=$rendered;nonClaims=@("Menu and submenu options are navigation only","Route directory is categorized navigation only","Enter a number to select; selection does not execute an action","Natural-language intent is an interpretation, not authoritative project fact","No option was executed","Fork is not window handoff","Window handoff requires the session bootstrap Skill and its acceptance gate","No write, Runtime, Git, network, release, or credential authority was granted")}|ConvertTo-Json -Depth 14

