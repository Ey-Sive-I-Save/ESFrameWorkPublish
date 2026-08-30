[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PromptText,
    [string]$AiInterpretationPath = '',
    [string]$OutputPath = 'ES/Output/WebPageStudio/bootstrap/round-01-intake.json'
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path '.').Path
$full=[IO.Path]::GetFullPath((Join-Path $root $OutputPath))
if(-not $full.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'OutputPath must remain under project root.'}
New-Item -ItemType Directory -Force (Split-Path $full)|Out-Null
$bytes=[Text.UTF8Encoding]::new($false).GetBytes($PromptText)
$sha=[Security.Cryptography.SHA256]::Create();try{$hex=(-join($sha.ComputeHash($bytes)|%{$_.ToString('x2')}))}finally{$sha.Dispose()}
$interpretation=$null
if([string]::IsNullOrWhiteSpace($AiInterpretationPath)){throw 'blocked.round-01.ai-interpretation-required'}
if($AiInterpretationPath){
  $ip=[IO.Path]::GetFullPath((Join-Path $root $AiInterpretationPath))
  if(-not(Test-Path -LiteralPath $ip -PathType Leaf)){throw 'blocked.round-01.ai-interpretation-missing'}
  $interpretation=([Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($ip))|ConvertFrom-Json)
  foreach($field in @('objectiveBrief','userGoals','nonGoals','acceptanceSignals','unknowns','interactionIntent','analysis','execution','returnReceipt','provenance')){if($null -eq $interpretation.PSObject.Properties[$field] -or [string]::IsNullOrWhiteSpace([string]$interpretation.$field)){throw "blocked.round-01.ai-interpretation-incomplete:$field"}}
  if([string]$interpretation.provenance.actor -notin @('current-ai-session','provider')){throw 'blocked.round-01.ai-interpretation-provenance-invalid'}
  if(([string]$interpretation.objectiveBrief).Trim().Length -lt 40 -or ([string]$interpretation.analysis).Trim().Length -lt 80){throw 'blocked.round-01.ai-interpretation-too-shallow'}
  if([string]$interpretation.analysis -match '(?i)persist intake evidence|raw prompt hash|do not invent'){throw 'blocked.round-01.synthetic-ai-interpretation'}
}
$status='accepted'
$r=[ordered]@{
 schemaVersion=1;recordType='RequirementIntakeReceipt';roundId='web-generation-round-01';stageId='requirement-intake';status=$status;inputHash=$hex;rawPrompt=$PromptText
 allowedScope=@('requirement intake receipt');forbiddenScope=@('FocusContext','TaskContext','Knowledge','SubAgent','ABCD','generation','runtime','network','Unity','Git','release')
 unknowns=if($interpretation){@()}else{@('objective intent','acceptance signals','interaction scope')};aiInterpretation=$interpretation
 acceptanceSignals=if($interpretation){@($interpretation.acceptanceSignals)}else{@('raw prompt preserved','strict UTF-8 hash','explicit AI interpretation required')}
 aiAnalysis=if($interpretation){[string]$interpretation.analysis}else{'No AI interpretation supplied; acceptance withheld.'}
 execution=if($interpretation){[string]$interpretation.execution}else{'Persist raw intake only and stop before Round 02.'};decision=if($interpretation){'ai-interpretation-accepted-for-focus'}else{'awaiting-ai-interpretation'}
 returnReceipt=if($interpretation){$interpretation.returnReceipt}else{[ordered]@{status=$status;path=$OutputPath;hash=$hex}};nonClaims=@('not user acceptance','not downstream execution','not focus or page design')
}
[IO.File]::WriteAllText($full,($r|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
$r|ConvertTo-Json -Depth 12
