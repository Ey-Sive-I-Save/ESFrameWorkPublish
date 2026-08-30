[CmdletBinding()]
param([string]$SchemaPath=(Join-Path $PSScriptRoot '..\Contracts\es-web-ui-worker-runrecord-v1.schema.json'))
$ErrorActionPreference='Stop'
$schema=Get-Content -LiteralPath $SchemaPath -Raw -Encoding UTF8|ConvertFrom-Json
$required=@('schemaVersion','recordType','runId','taskId','attempt','status','lease','handleId','timeline','terminalEvidence','runtimeStatus','nonClaims')
$missing=@($required|Where-Object {$schema.required -notcontains $_})
$statuses=@('planned','queued','running','retry-waiting','succeeded','failed','cancelled','orphaned','recovery-pending')
$statusEnum=@($schema.properties.status.enum)
$ok=([int]$schema.properties.schemaVersion.const -eq 1 -and [string]$schema.properties.recordType.const -ceq 'WebPageStudioWorkerRunRecord' -and $missing.Count -eq 0 -and (@($statuses|Where-Object {$statusEnum -notcontains $_}).Count -eq 0) -and [string]$schema.properties.runtimeStatus.enum[0] -ceq 'runtime-not-run' -and @($schema.properties.terminalEvidence.required|Where-Object {$_ -notin @('handleState','processTreeObserved','handleReleased')}).Count -eq 0)
[ordered]@{validator='web-ui-worker-runrecord-contract';status=if($ok){'passed'}else{'failed'};requiredFieldCount=$required.Count;missing=$missing;statusCount=$statusEnum.Count;runtimeStatus='runtime-not-run';nonClaims=@('schema-static-only','does-not-prove-worker-lifecycle','does-not-prove-crash-recovery','does-not-prove-production-release')}|ConvertTo-Json -Depth 8;if(-not $ok){exit 1}
