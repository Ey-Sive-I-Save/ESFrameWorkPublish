[CmdletBinding()]
param([string]$SchemaPath=(Join-Path $PSScriptRoot '..\Contracts\es-web-ui-worker-handle-v1.schema.json'))
$ErrorActionPreference='Stop'
$s=Get-Content -LiteralPath $SchemaPath -Raw -Encoding UTF8|ConvertFrom-Json
$required=@('schemaVersion','recordType','handleId','runId','state','processTree','cancellation','timeout','disposal','runtimeStatus','nonClaims')
$states=@('created','starting','running','terminating','exited','timed-out','cancelled','disposed','orphaned')
$ok=([int]$s.properties.schemaVersion.const -eq 1 -and [string]$s.properties.recordType.const -ceq 'WebPageStudioWorkerHandle' -and @($required|Where-Object {$s.required -notcontains $_}).Count -eq 0 -and @($states|Where-Object {@($s.properties.state.enum) -notcontains $_}).Count -eq 0 -and @('rootPid','descendantPids','terminationRequired'|Where-Object {@($s.properties.processTree.required) -notcontains $_}).Count -eq 0 -and [string]$s.properties.runtimeStatus.enum[0] -ceq 'runtime-not-run')
[ordered]@{validator='web-ui-worker-handle-contract';status=if($ok){'passed'}else{'failed'};requiredFieldCount=$required.Count;stateCount=@($s.properties.state.enum).Count;runtimeStatus='runtime-not-run';nonClaims=@('schema-static-only','does-not-start-or-terminate-process','does-not-prove-timeout-or-cancellation-runtime')}|ConvertTo-Json -Depth 8;if(-not $ok){exit 1}
