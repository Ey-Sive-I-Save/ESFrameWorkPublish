[CmdletBinding()]param()
$ErrorActionPreference='Stop';Import-Module (Join-Path $PSScriptRoot 'ESTaskFocusContext.psm1') -Force
$p=New-TaskFocusProposal -Focus 'default activation fixture' -AllowedScope @('ES/Automation') -AcceptanceSignals @('static-pass')
$d=Invoke-TaskFocusDefaultActivation -Current $null -Proposal $p -LatestUserMessage 'continue-latest-message' -ExpectedRevision 0
$elevatedProposal=New-TaskFocusProposal -Focus 'elevated activation fixture' -Priority elevated -AllowedScope @('ES/Automation') -AcceptanceSignals @('static-pass')
$elevated=Invoke-TaskFocusDefaultActivation -Current $null -Proposal $elevatedProposal -LatestUserMessage 'continue-latest-message' -ExpectedRevision 0
$o=Invoke-TaskFocusDefaultActivation -Current $null -Proposal $p -LatestUserMessage ([char]0x4E0D+[char]0x7528) -ExpectedRevision 0
$ok=$d.status -eq 'confirmed' -and $elevated.status -eq 'pending-confirmation' -and $o.status -eq 'closed'
[pscustomobject]@{schemaVersion=1;validator='Test-ESTaskFocusDefaultActivation';status=$(if($ok){'passed'}else{'failed'});caseCount=3;passedCount=$(if($ok){3}else{0});failedCount=$(if($ok){0}else{3});cases=@([pscustomobject]@{case='normal-low-risk-auto-attaches';status=$(if($d.status -eq 'confirmed'){'passed'}else{'failed'})},[pscustomobject]@{case='elevated-waits-for-real-confirmation';status=$(if($elevated.status -eq 'pending-confirmation'){'passed'}else{'failed'})},[pscustomobject]@{case='explicit-opt-out-closes-focus';status=$(if($o.status -eq 'closed'){'passed'}else{'failed'})});runtimeStatus='runtime-not-run';claimsNotProven=@('host UI prompt integration')}|ConvertTo-Json -Depth 10
if(-not $ok){exit 1}
