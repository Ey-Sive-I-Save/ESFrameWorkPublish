[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$StateMachinePath,
  [Parameter(Mandatory)][ValidateSet('intake','authority-locked','snapshot-frozen','decomposed','fanout-running','evidence-joined','adversarial-reviewed','converged','final-decision','closed')][string]$CurrentState,
  [Parameter(Mandatory)][ValidateSet('advance','failure','conflict','staleSnapshot','cancel')][string]$Event,
  [Parameter(Mandatory)][hashtable]$Outputs
)
$ErrorActionPreference='Stop';$u=[Text.UTF8Encoding]::new($false,$true)
if(-not(Test-Path -LiteralPath $StateMachinePath -PathType Leaf)){throw 'STATE_MACHINE_INPUT_MISSING'}
$sm=$u.GetString([IO.File]::ReadAllBytes($StateMachinePath))|ConvertFrom-Json
$states=@($sm.states);$idx=0;for($n=0;$n -lt $states.Count;$n++){if($states[$n].id -ceq $CurrentState){$idx=$n;break}}
if($states[$idx].id -cne $CurrentState){throw 'STATE_NOT_DECLARED'}
if($Event -eq 'advance'){
  if($idx -ge $states.Count-1){throw 'STATE_ALREADY_TERMINAL'}
  $next=$states[$idx+1].id
  foreach($required in @($states[$idx+1].requires)){if(-not $Outputs.ContainsKey($required) -or $null -eq $Outputs[$required] -or [string]::IsNullOrWhiteSpace([string]$Outputs[$required])){throw "STATE_OUTPUT_MISSING:$required"}}
  $transitionType='normal'
} else {
  $edge=$sm.transitions.$Event;if($null -eq $edge){throw "STATE_EVENT_UNDECLARED:$Event"};if([string]$edge.from -ne '*' -and [string]$edge.from -cne $CurrentState){throw "STATE_EVENT_SOURCE_MISMATCH:$Event"};$next=[string]$edge.to;if(-not @($states.id).Contains($next)){throw "STATE_EVENT_TARGET_INVALID:$Event"};foreach($required in @($edge.requires)){if(-not $Outputs.ContainsKey($required) -or $null -eq $Outputs[$required] -or [string]::IsNullOrWhiteSpace([string]$Outputs[$required])){throw "STATE_EVENT_OUTPUT_MISSING:$required"}};$transitionType=$Event
}
[ordered]@{status='accepted';stateMachineId=$sm.stateMachineId;from=$CurrentState;event=$Event;to=$next;transitionType=$transitionType;outputKeys=@($Outputs.Keys|Sort-Object);authority=$sm.authority;runtimeStatus='runtime-not-run'}|ConvertTo-Json -Depth 8
