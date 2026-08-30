[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$PromptText,
  [string]$Selection='',
  [switch]$Animate,
  [switch]$NoColor,
  [int]$FrameDelayMs=45
)
$ErrorActionPreference='Stop'
$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
if($FrameDelayMs -lt 0 -or $FrameDelayMs -gt 1000){throw 'FrameDelayMs must be between 0 and 1000.'}
$invoke=Join-Path $PSScriptRoot 'Invoke-ESCollaborationMenu.ps1'
$invokeParams=@{PromptText=$PromptText}
if(-not [string]::IsNullOrWhiteSpace($Selection)){$invokeParams.Selection=$Selection}
$model=(& $invoke @invokeParams | Out-String)|ConvertFrom-Json
$display=$model.display
$cyan='Cyan';$dim='DarkGray';$yellow='Yellow';$green='Green';$white='White'
function Write-EsText([string]$Text,[string]$Color=''){
  if($NoColor -or [string]::IsNullOrEmpty($Color)){Write-Host $Text}else{Write-Host $Text -ForegroundColor $Color}
}
function Write-Animated([string]$Text,[string]$Color=''){
  if(-not $Animate){Write-EsText $Text $Color;return}
  if($NoColor){Write-Host $Text;return}
  for($i=0;$i -lt $Text.Length;$i++){Write-Host -NoNewline $Text[$i] -ForegroundColor $Color;Start-Sleep -Milliseconds ([Math]::Min($FrameDelayMs,120))};Write-Host
}
$line='─'*48
Write-Animated "╭$line╮" $cyan
Write-Animated "│              $($display.title.PadRight(31))│" $cyan
Write-Animated "│  $($display.subtitle.PadRight(44))│" $dim
Write-Animated "╰$line╯" $cyan
Write-Host
Write-EsText "┌─ $($display.sections.quickAccess) ───────────────────────────────┐" $yellow
foreach($item in @($display.quickAccess)){
  $mark=if($item.recommended){' ★推荐'}else{''}
  Write-EsText ("│ {0}  {1}{2}" -f $item.number,$item.label,$mark) $(if($item.recommended){$green}else{$white})
}
Write-EsText '└────────────────────────────────────────────────┘' $yellow
Write-Host
Write-EsText "┌─ $($display.sections.main) ───────────────────────────────────┐" $cyan
foreach($item in @($display.mainOptions)){
  $mark=if($item.recommended){' ★推荐'}else{''}
  Write-EsText ("│ 【{0}】 {1} {2}{3}" -f $item.number,$item.icon,$item.title,$mark) $(if($item.recommended){$green}else{$white})
  Write-EsText ("│     $($item.subtitle)" ) $dim
  Write-EsText ("│     例：$($item.examples -join '；')") $dim
}
Write-EsText '└────────────────────────────────────────────────┘' $cyan
$selectedPage=@($model.options | Where-Object { $_.id -eq $(if($model.selection){$model.selection.optionId}else{$model.recommendedOptionId}) } | Select-Object -First 1)
if($selectedPage.pageGuidance){$g=$selectedPage.pageGuidance;Write-Host;Write-EsText "┌─ 🧠 $($g.title) ────────────────────────────────┐" $yellow;Write-EsText "│ 目的：$($g.purpose)" $white;Write-EsText "│ 需要：$($g.intake)" $dim;Write-EsText "│ 交付：$($g.deliverable)" $dim;Write-EsText "│ 下一步：$($g.next)" $green;Write-EsText '└────────────────────────────────────────────────┘' $yellow}
if($model.selection.level -eq 'submenu'){
  $selectedMain=@($model.options | Where-Object { $_.id -eq $model.selection.optionId } | Select-Object -First 1)
  if($selectedMain.submenu){
    Write-Host
    Write-EsText '┌─ 当前子菜单 ───────────────────────────────────┐' $yellow
    foreach($sub in @($selectedMain.submenu.options)){$mark=if($sub.id -eq $model.selection.submenuOptionId){' ←已选'}else{''};Write-EsText ("│ {0}{1}" -f $sub.label,$mark) $white}
    Write-EsText '└────────────────────────────────────────────────┘' $yellow
  }
}
Write-Host
Write-EsText "R1 超级语义   R2 路由目录   R3 能力与边界" $yellow
Write-EsText "输入编号选择入口 · 不会自动执行动作" $dim
