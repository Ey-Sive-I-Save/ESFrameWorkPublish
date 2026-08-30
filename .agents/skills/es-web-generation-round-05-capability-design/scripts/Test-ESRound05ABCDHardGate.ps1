[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ReceiptPath,[Parameter(Mandatory=$true)][string]$TaskId,[Parameter(Mandatory=$true)][string]$RoutePlanHash,[Parameter(Mandatory=$true)][string]$SourceScopeHash)
$ErrorActionPreference='Stop';$raw=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes((Resolve-Path $ReceiptPath).Path));$r=$raw|ConvertFrom-Json
foreach($n in @('recordType','taskId','routePlanHash','sourceScopeHash','invocationId','eventSequence','candidateCount','candidateHashes','selectedCandidateId','decision','existingArtifactBindings')){if($null -eq $r.PSObject.Properties[$n]){throw "blocked.round-05.abcd-missing:$n"}}
if([string]$r.taskId -cne $TaskId -or [string]$r.routePlanHash -cne $RoutePlanHash -or [string]$r.sourceScopeHash -cne $SourceScopeHash){throw 'blocked.round-05.abcd-binding-mismatch'}
if([int]$r.candidateCount -lt 3 -or @($r.candidateHashes).Count -lt 3){throw 'blocked.round-05.abcd-divergence-insufficient'}
if([string]::IsNullOrWhiteSpace([string]$r.selectedCandidateId) -or [string]::IsNullOrWhiteSpace([string]$r.decision)){throw 'blocked.round-05.abcd-no-court-decision'}
if(@($r.existingArtifactBindings).Count -eq 0){throw 'blocked.round-05.abcd-no-artifact-reuse'}
if(@($r.existingArtifactBindings|Where-Object{[string]::IsNullOrWhiteSpace([string]$_.path) -or -not(Test-Path -LiteralPath ([string]$_.path) -PathType Leaf)}).Count -gt 0){throw 'blocked.round-05.abcd-artifact-binding-missing'}
[pscustomobject]@{status='passed';recordType=[string]$r.recordType;invocationId=[string]$r.invocationId;candidateCount=[int]$r.candidateCount;selectedCandidateId=[string]$r.selectedCandidateId}
