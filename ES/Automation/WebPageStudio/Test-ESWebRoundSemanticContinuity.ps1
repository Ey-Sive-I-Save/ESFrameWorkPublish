[CmdletBinding()]
param([Parameter(Mandatory=$true)][string[]]$RoundPaths,[string]$ProjectRoot='')
$ErrorActionPreference='Stop'
$root=if($ProjectRoot){[IO.Path]::GetFullPath($ProjectRoot)}else{(Get-Location).Path};$root=[IO.Path]::GetFullPath($root).TrimEnd('\')+'\'
function Resolve-PathBounded([string]$p){$f=if([IO.Path]::IsPathRooted($p)){[IO.Path]::GetFullPath($p)}else{[IO.Path]::GetFullPath((Join-Path $root $p))};if(-not $f.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'Round path outside project root.'};$f}
$findings=[Collections.Generic.List[string]]::new();$items=@()
if(@($RoundPaths).Count -ne 4){$findings.Add('blocked.round-continuity.requires-rounds-1-to-4')}else{foreach($p in $RoundPaths){$f=Resolve-PathBounded $p;if(-not(Test-Path -LiteralPath $f -PathType Leaf)){$findings.Add("blocked.round-continuity.missing:$p");continue};try{$items+=Get-Content -Raw -Encoding UTF8 $f|ConvertFrom-Json}catch{$findings.Add("blocked.round-continuity.invalid-json:$p")}}}
if($items.Count -eq 4){
  $expected=@('RequirementIntakeReceipt','FocusContextReceipt','TaskContextCreationReceipt','KnowledgeRouteReceipt');for($i=0;$i -lt 4;$i++){if([string]$items[$i].recordType -cne $expected[$i]){$findings.Add("blocked.round-continuity.record-type:$($i+1)")};if([string]$items[$i].status -cne 'accepted'){$findings.Add("blocked.round-continuity.status:$($i+1)")}}
  $a=$items[0];$b=$items[1];$c=$items[2];$d=$items[3]
  if([string]$b.intakeHash -cne [string]$a.inputHash){$findings.Add('blocked.round-continuity.round02-intake-hash')}
  if([string]$c.focusProposalHash -cne [string]$b.proposalHash -or [string]$c.focusScopeHash -cne [string]$b.focusScopeHash){$findings.Add('blocked.round-continuity.round03-focus-binding')}
  if([string]$d.taskContextHash -cne [string]$c.taskContextHash -or [string]$d.sourceScopeHash -cne [string]$c.sourceScopeHash){$findings.Add('blocked.round-continuity.round04-task-binding')}
  $ia=$a.aiInterpretation;if($null -eq $ia -or ([string]$ia.objectiveBrief).Trim().Length -lt 40 -or ([string]$a.objectiveBrief).Trim().Length -lt 40 -or ([string]$ia.analysis).Trim().Length -lt 80){$findings.Add('blocked.round-continuity.round01-ai-meaning-missing')}
  foreach($x in @(@($b,'round02',80,60),@($c,'round03',80,80),@($d,'round04',80,80))){if(([string]$x[0].aiAnalysis).Trim().Length -lt [int]$x[2] -or ([string]$x[0].execution).Trim().Length -lt 40 -or $null -eq $x[0].PSObject.Properties['returnReceipt']){$findings.Add("blocked.round-continuity.$($x[1])-ai-evidence-shallow")}}
  if([string]$b.focus.Trim().Length -lt 40){$findings.Add('blocked.round-continuity.focus-too-short')};if(@($d.selectedEntries).Count -lt 1){$findings.Add('blocked.round-continuity.knowledge-selection-empty')}
}
[ordered]@{schemaVersion=1;recordType='WebRoundSemanticContinuityReceipt';status=if($findings.Count){'blocked'}else{'passed'};roundCount=$items.Count;findings=@($findings);runtimeStatus='runtime-not-run';claimsNotProven=@('Semantic continuity does not prove design quality, browser behavior, or provider identity.') }|ConvertTo-Json -Depth 8
if($findings.Count){exit 1}
