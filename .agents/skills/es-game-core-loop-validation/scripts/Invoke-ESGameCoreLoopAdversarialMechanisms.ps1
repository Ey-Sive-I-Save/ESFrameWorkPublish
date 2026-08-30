[CmdletBinding()]
param([string]$Requirement='validate game core loop evidence boundaries',[string]$SourceHash=('a'*64))
$ErrorActionPreference='Stop';$root=(Get-Location).Path;Import-Module (Join-Path $root 'ES/Automation/ABCD/ESABCDDivergence.psm1') -Force;Import-Module (Join-Path $root 'ES/Automation/ABCD/ESABCDAuditConsistency.psm1') -Force
$div=Invoke-ESABCDFiveDirectionDivergence -Requirement $Requirement -SourceHash $SourceHash -MinimumDirections 5;$audit=Invoke-ESABCDAuditConsistency -AuditPrompt $Requirement -ArtifactHash $SourceHash -EvidenceComplete $false
[ordered]@{status='passed';divergence=[ordered]@{status=$div.status;directionCount=$div.directionCount;selectedDirectionId=$div.selectedDirectionId;hash=$div.hash};auditConsistency=[ordered]@{status=$audit.status;rubricVersion=$audit.rubricVersion;receiptHash=$audit.receiptHash};runtimeStatus='runtime-not-run';authority='ABCD-adversarial-candidate'}|ConvertTo-Json -Depth 12
