[CmdletBinding()]
param([string]$SkillRoot = (Join-Path (Get-Location) '.agents/skills/es-game-core-loop-validation'))
$ErrorActionPreference='Stop';$r=& (Join-Path $SkillRoot 'scripts/Invoke-ESGameCoreLoopABCDCapabilities.ps1')|ConvertFrom-Json;if($r.status -ne 'passed' -or [int]$r.receiptCount -ne 6){throw 'ABCD_CAPABILITY_DISPATCH_FAILED'}
foreach($id in @('bounded-tool-action','failure-recovery','branch-evaluation','state-transition-guard','environment-trust-gate','audit-evidence-chain')){if(-not @($r.receipts|Where-Object capabilityId -eq $id)){throw "ABCD_CAPABILITY_RECEIPT_MISSING:$id"}}
[ordered]@{status='passed';receiptCount=$r.receiptCount;allSixBound=$true;runtimeStatus='runtime-not-run';deterministic=$true}|ConvertTo-Json
