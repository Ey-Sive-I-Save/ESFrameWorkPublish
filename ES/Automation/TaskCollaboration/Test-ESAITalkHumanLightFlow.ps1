[CmdletBinding()]
param([string]$ProjectRoot,[switch]$NoReport)
$ErrorActionPreference='Stop'
if([string]::IsNullOrWhiteSpace($ProjectRoot)){$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path}
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
Import-Module (Join-Path $PSScriptRoot 'ESAITalkHumanLightFlow.psm1') -Force
Import-Module (Join-Path $root 'ES/Automation/Contracts/ESJsonSchemaLite.psm1') -Force
$results=[Collections.Generic.List[object]]::new()
function Case([string]$Name,[scriptblock]$Body){try{&$Body;[void]$results.Add([pscustomobject]@{case=$Name;status='passed';finding=$null})}catch{[void]$results.Add([pscustomobject]@{case=$Name;status='failed';finding=$_.Exception.Message})}}
function Assert-True([bool]$Value,[string]$Message){if(-not$Value){throw $Message}}
function Assert-Equal($Actual,$Expected,[string]$Message){if([string]$Actual -cne [string]$Expected){throw "$Message Expected=$Expected Actual=$Actual"}}
function Assert-Schema([string]$Name,[string]$Path,$Value){$errors=@(Test-ESJsonSchemaValue -SchemaPath (Join-Path $root $Path) -Value $Value);if($errors.Count){throw "$Name schema errors: $($errors -join '; ')"}}
$sessions=Join-Path $root 'Assets/Plugins/ES/AITalk/Sessions'
Case 'normal-flow-needs-no-human' {$p=Invoke-ESAITalkHumanLightFlow -SessionsRoot $sessions;Assert-Schema 'human plan' 'ES/Automation/TaskCollaboration/es-aitalk-human-action-plan-v1.schema.json' $p;Assert-Equal $p.status 'auto-ready' 'normal status';Assert-True (-not$p.humanActionRequired) 'normal flow asked for human';Assert-Equal @($p.actionItems).Count 0 'normal action items'}
Case 'resource-review-is-short-and-explicit' {$p=Invoke-ESAITalkHumanLightFlow -SessionsRoot $sessions -MaxMessagesPerSession 2;Assert-Equal $p.status 'needs-human-decision' 'resource status';Assert-True $p.humanActionRequired 'resource action missing';Assert-True (@($p.actionItems|Where-Object code -eq 'PROJECT_REVIEW').Count -eq 1) 'resource action code'}
Case 'five-round-interruption-is-actionable' {$rounds=@(1..5|ForEach-Object{[pscustomobject]@{round=$_;consensusReached=$false;userDecisionRequired=$false}});$p=Invoke-ESAITalkHumanLightFlow -SessionsRoot $sessions -RoundObservations $rounds;Assert-Equal $p.status 'interrupted' 'interruption status';Assert-True (@($p.actionItems|Where-Object code -eq 'ROUND_LIMIT_INTERRUPTED').Count -eq 1) 'interruption action'}
Case 'explicit-user-decision-is-actionable' {$rounds=@([pscustomobject]@{round=1;consensusReached=$false;userDecisionRequired=$true});$p=Invoke-ESAITalkHumanLightFlow -SessionsRoot $sessions -RoundObservations $rounds;Assert-Equal $p.status 'needs-human-decision' 'decision status';Assert-True (@($p.actionItems|Where-Object code -eq 'ROUND_USER_DECISION').Count -eq 1) 'decision action'}
Case 'partial-is-informational-not-blocking' {$p=Invoke-ESAITalkHumanLightFlow -SessionsRoot $sessions -MaxSessions 2;Assert-Equal $p.status 'auto-ready' 'bounded informational status';Assert-True (-not$p.humanActionRequired) 'partial should not force human'}
Case 'completion-boundary-is-preserved' {$p=Invoke-ESAITalkHumanLightFlow -SessionsRoot $sessions;Assert-True $p.completionDecisionRequired 'completion boundary missing';Assert-True ($p.nonClaims -join ';' -match 'business decisions') 'non-claim missing';Assert-True ($p.PSObject.Properties.Name -notcontains 'Accepted') 'accepted claim'}
$failed=@($results|Where-Object status -eq 'failed')
$report=[ordered]@{schemaVersion=1;validator='Test-ESAITalkHumanLightFlow';status=if($failed.Count){'failed'}else{'passed'};caseCount=$results.Count;passedCount=@($results|Where-Object status -eq 'passed').Count;failedCount=$failed.Count;cases=@($results);runtimeStatus='runtime-not-run';claimsNotProven=@('Human decision quality','Unity/Worker/MCP Runtime','cross-process mailbox delivery')}
if(-not$NoReport){$path=Join-Path $root 'ES/Output/Interaction/aitalk-human-light-flow.json';$parent=Split-Path -Parent $path;if(-not(Test-Path -LiteralPath $parent)){New-Item -ItemType Directory -Path $parent -Force|Out-Null};[IO.File]::WriteAllText($path,($report|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false))}
$report|ConvertTo-Json -Depth 20
if($failed.Count){exit 1}
