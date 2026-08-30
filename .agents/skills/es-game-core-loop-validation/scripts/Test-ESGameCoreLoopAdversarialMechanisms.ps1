[CmdletBinding()]
param([string]$SkillRoot = (Join-Path (Get-Location) '.agents/skills/es-game-core-loop-validation'))
$ErrorActionPreference='Stop';$r=& (Join-Path $SkillRoot 'scripts/Invoke-ESGameCoreLoopAdversarialMechanisms.ps1')|ConvertFrom-Json;if($r.status -ne 'passed' -or [int]$r.divergence.directionCount -lt 5 -or $r.auditConsistency.status -ne 'review'){throw 'ADVERSARIAL_MECHANISM_REPLAY_FAILED'}
[ordered]@{status='passed';directionCount=$r.divergence.directionCount;auditStatus=$r.auditConsistency.status;runtimeStatus='runtime-not-run';deterministic=$true}|ConvertTo-Json
