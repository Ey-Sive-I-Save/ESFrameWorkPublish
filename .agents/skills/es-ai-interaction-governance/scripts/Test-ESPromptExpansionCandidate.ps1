[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$CandidatePath)
$ErrorActionPreference='Stop';$o=Get-Content -Raw -Encoding UTF8 $CandidatePath|ConvertFrom-Json
$required=@('schemaVersion','recordType','status','idempotencyKey','source','expandedPrompt','nonClaims');$missing=@($required|?{ $null -eq $o.$_ })
$ok=($missing.Count -eq 0 -and $o.schemaVersion -eq 1 -and $o.recordType -eq 'PromptExpansionCandidate' -and $o.idempotencyKey -match '^[a-f0-9]{64}$' -and $o.expandedPrompt.mustPreserve.Count -gt 0 -and $o.expandedPrompt.acceptanceSignals.Count -gt 0 -and $o.expandedPrompt.evidencePlan.Count -gt 0)
[ordered]@{status=if($ok){'passed'}else{'failed'};missing=$missing;candidatePath=$CandidatePath}|ConvertTo-Json -Depth 5
if(!$ok){exit 1}
