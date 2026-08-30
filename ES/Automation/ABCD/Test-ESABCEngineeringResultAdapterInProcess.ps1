[CmdletBinding()]
param([string]$ProjectRoot)
$ErrorActionPreference='Stop'
if([string]::IsNullOrWhiteSpace($ProjectRoot)){$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path}
Push-Location $ProjectRoot
try {
 Import-Module (Join-Path $ProjectRoot 'ES/Automation/ABCD/ESABCInnovationRun.psm1') -Force
 $head=(git rev-parse HEAD).Trim()
 $relative='ES/Automation/ABCD/ESABCInnovationRun.psm1'
 $content=Get-Content -Raw -Encoding UTF8 $relative
 $hash=(Get-FileHash -LiteralPath 'ES/Automation/Contracts/es-ai-abc-engineering-architecture-competition-v1.schema.json' -Algorithm SHA256).Hash.ToLowerInvariant()
 $result=[pscustomobject]@{status='completed';completionEligible=$true;winnerId='arch-1';contractHash=$hash;evidenceSummary=[pscustomobject]@{eligibleCount=2;implementationEvidenceVerifiedCount=2;independentReplayVerifiedCount=2;providerScoreComparisonStatus='stable'}}
 $envelope=[pscustomobject]@{status='candidate';generationMode='engineering';candidateSetHash=('c'*64);candidates=@([pscustomobject]@{candidateId='arch-1';proposedChanges=@([pscustomobject]@{path=$relative;changeId='noop';afterContent=$content})})}
 $converted=Convert-ESABCEngineeringResultToCandidatePatchPlan -EngineeringResult $result -CandidateEnvelope $envelope -Scenario DesignChange -CurrentHead $head -AuthorizationRef 'in-process-test' -AllowedWriteScopes @($relative)
 $blocked=$false
 try { Convert-ESABCEngineeringResultToCandidatePatchPlan -EngineeringResult ([pscustomobject]@{status='completed';completionEligible=$true;winnerId='arch-1'}) -CandidateEnvelope $envelope -Scenario DesignChange -CurrentHead $head -AuthorizationRef 'in-process-test' -AllowedWriteScopes @($relative)|Out-Null } catch {$blocked=$_.Exception.Message -like '*COMPLETION_EVIDENCE*'}
 $pass=($converted.status -eq 'candidate-only' -and $converted.patchPlan.planStatus -eq 'awaiting-abcd-audit' -and $blocked)
 [pscustomobject]@{status=if($pass){'passed'}else{'failed'};convertedStatus=$converted.status;planStatus=$converted.patchPlan.planStatus;forgedCompletionBlocked=$blocked}
} finally { Pop-Location }
if(-not $pass){exit 1}
