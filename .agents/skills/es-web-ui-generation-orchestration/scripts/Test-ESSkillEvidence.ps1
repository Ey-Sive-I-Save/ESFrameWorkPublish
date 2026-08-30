[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ReceiptPath)
$ErrorActionPreference='Stop'
$r=Get-Content -LiteralPath $ReceiptPath -Raw -Encoding UTF8 | ConvertFrom-Json
foreach($k in 'skillName','profile','status','runtimeStatus','nonClaims'){if(-not $r.PSObject.Properties.Name.Contains($k)){throw "EVIDENCE_FIELD_MISSING:$k"}}
if([string]$r.runtimeStatus -eq 'runtime-not-run' -and @($r.nonClaims).Count -eq 0){throw 'RUNTIME_NOT_RUN_NONCLAIMS_MISSING'}
[ordered]@{validator='es-web-ui-generation-orchestration-evidence';status='passed';skillName=[string]$r.skillName;runtimeStatus=[string]$r.runtimeStatus}|ConvertTo-Json -Compress
