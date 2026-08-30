[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$PromptText,
  [string]$Selection='',
  [switch]$HtmlColor,
  [switch]$NoFrame,
  [switch]$AllExamples
)
$ErrorActionPreference='Stop'
$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$invoke=Join-Path $PSScriptRoot 'Invoke-ESCollaborationMenu.ps1'
$themePath=Join-Path $PSScriptRoot '..\references\menu-theme.json'
$invokeParams=@{PromptText=$PromptText}
if(-not [string]::IsNullOrWhiteSpace($Selection)){$invokeParams.Selection=$Selection}
$model=(& $invoke @invokeParams | Out-String)|ConvertFrom-Json
$theme=Get-Content -LiteralPath $themePath -Raw -Encoding UTF8|ConvertFrom-Json
$p=$theme.palette
function Color([string]$Text,[string]$Name){if($HtmlColor){return ('<span style="color:' + [string]$p.$Name + '">' + $Text + '</span>')};return $Text}
function Get-DisplayWidth([string]$Text){$w=0;for($i=0;$i -lt $Text.Length;$i++){ $code=[int][char]$Text[$i]; if($code -ge 0xD800 -and $code -le 0xDBFF -and ($i+1) -lt $Text.Length){$i++;$w+=2}elseif($code -ge 0x2E80){$w+=2}else{$w++} };return $w}
function Pad-Display([string]$Text,[int]$Width){$n=$Width-(Get-DisplayWidth $Text);if($n -lt 0){$n=0};return $Text+(' '*$n)}
$lines=@()
$lines += '╭─ ✦ ES AI 协作中心 ─────────────────────────────╮'
$lines += "│  $(Color $model.display.subtitle 'muted')  │"
$lines += '╰────────────────────────────────────────────────╯'
$lines += ''
$lines += "### $(Color '⚡ 快捷入口' 'accent')"
foreach($q in @($model.display.quickAccess)){$mark=if($q.recommended){' ★推荐'}else{''};$lines += "- **$($q.number)** $($q.label)$mark"}
$lines += ''
$lines += "### $(Color '🧭 工作方向' 'accent')"
$lines += '| 入口 | 方向 | 说明 | 示例 |'
$lines += '|---|---|---|---|'
foreach($item in @($model.display.mainOptions)){$mark=if($item.recommended){' ★推荐'}else{''};$examples=($item.examples -join '<br>');$lines += "| **【$($item.number)】 $($item.icon)** | **$($item.title)$mark** | $($item.subtitle) | $examples |"}
$selectedSub = @($model.options | Where-Object { $_.id -eq $model.selection.optionId } | Select-Object -First 1).submenu
$selectedPage = @($model.options | Where-Object { $_.id -eq $(if($model.selection){$model.selection.optionId}else{$model.recommendedOptionId}) } | Select-Object -First 1)
if($selectedPage.pageGuidance){
  $g=$selectedPage.pageGuidance
  $lines += ''
  $lines += "### $(Color ('🧠 '+$g.title) 'accent')"
  $lines += "- 目的：$($g.purpose)"
  $lines += "- 需要：$($g.intake)"
  $lines += "- 交付：$($g.deliverable)"
  $lines += "- 下一步：$($g.next)"
}
if($model.selection.level -eq 'submenu' -and $selectedSub){
  $lines += ''
  $lines += "### $(Color '↳ 当前子菜单' 'accent')"
  foreach($sub in @($selectedSub.options)){$mark=if($sub.id -eq $model.selection.submenuOptionId){' ←已选'}else{''};$lines += "- $($sub.label)$mark"}
}
$lines += ''
$lines += "### $(Color '📚 分类路由' 'accent')"
$lines += '> `R1` 超级语义　·　`R2` 路由目录　·　`R3` 能力与边界'
$lines += ''
$lines += "$(Color '输入编号选择入口 · 不会自动执行动作' 'muted')"
if($NoFrame -or $HtmlColor){$lines -join "`n";return}
$frameLines=@('✦ ES AI 协作中心',$model.display.subtitle,'','⚡ 快捷入口')
foreach($q in @($model.display.quickAccess)){$mark=if($q.recommended){' ★推荐'}else{''};$frameLines += "$($q.number)  $($q.label)$mark"}
$frameLines += ''; $frameLines += '🧭 工作方向'
foreach($item in @($model.display.mainOptions)){$mark=if($item.recommended){' ★推荐'}else{''};$frameLines += "($($item.number))【$($item.icon) $($item.title)】$mark · $($item.subtitle)";$exampleLimit=if($AllExamples){@($item.examples).Count}else{1};for($exampleIndex=0;$exampleIndex -lt $exampleLimit;$exampleIndex++){$frameLines += "    例：$($item.examples[$exampleIndex])"}}
$selectedPage = @($model.options | Where-Object { $_.id -eq $(if($model.selection){$model.selection.optionId}else{$model.recommendedOptionId}) } | Select-Object -First 1)
if($selectedPage.pageGuidance){$g=$selectedPage.pageGuidance;$frameLines += ''; $frameLines += "🧠 $($g.title)"; $frameLines += "目的：$($g.purpose)"; $frameLines += "需要：$($g.intake)"; $frameLines += "交付：$($g.deliverable)"; $frameLines += "下一步：$($g.next)"}
$selectedSub = @($model.options | Where-Object { $_.id -eq $model.selection.optionId } | Select-Object -First 1).submenu
if($model.selection.level -eq 'submenu' -and $selectedSub){$frameLines += ''; $frameLines += '↳ 当前子菜单'; foreach($sub in @($selectedSub.options)){$mark=if($sub.id -eq $model.selection.submenuOptionId){' ←已选'}else{''};$frameLines += "$($sub.label)$mark"}}
$frameLines += ''; $frameLines += '📚 分类路由';$frameLines += 'R1 超级语义  ·  R2 路由目录  ·  R3 能力与边界';$frameLines += ''; $frameLines += '输入编号选择入口 · 不会自动执行动作'
$width=($frameLines|ForEach-Object {Get-DisplayWidth $_}|Measure-Object -Maximum).Maximum
$top='╭'+('─'*($width+2))+'╮';$bottom='╰'+('─'*($width+2))+'╯'
$framed=@($top);foreach($line in $frameLines){$framed += ('│ '+(Pad-Display $line $width)+' │')};$framed += $bottom
$framed -join "`n"
