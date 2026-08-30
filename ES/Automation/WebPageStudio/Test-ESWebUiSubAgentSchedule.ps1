[CmdletBinding()]
param([string]$AggregatePath=(Join-Path $PSScriptRoot 'fixtures/aggregate.valid.json'),[ValidateRange(1,8)][int]$ConcurrencyBudget=4)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
Import-Module (Join-Path $projectRoot 'ES\Automation\Contracts\ESJsonSchemaLite.psm1') -Force
$schedule=& (Join-Path $PSScriptRoot 'Invoke-ESWebUiSubAgentSchedule.ps1') -AggregatePath $AggregatePath -ConcurrencyBudget $ConcurrencyBudget | ConvertFrom-Json
$waves=@($schedule.waves)
$schemaErrors=@(Test-ESJsonSchemaValue -SchemaPath (Join-Path $projectRoot 'ES\Automation\Contracts\es-web-ui-sub-agent-schedule-v1.schema.json') -Value $schedule)
$hashInput=[ordered]@{}; foreach($property in $schedule.PSObject.Properties){if($property.Name -ne 'scheduleHash'){$hashInput[$property.Name]=$property.Value}}
$calculatedHash=(Get-FileHash -InputStream ([IO.MemoryStream]::new([Text.Encoding]::UTF8.GetBytes(($hashInput|ConvertTo-Json -Depth 20 -Compress)))) -Algorithm SHA256).Hash.ToLowerInvariant()
$waveNumbers=@($waves|ForEach-Object {[int]$_.wave})
$allTaskIds=@($waves|ForEach-Object {@($_.taskIds|ForEach-Object {[string]$_})})
$waveShapeValid=($waveNumbers.Count -eq (@(0..($waveNumbers.Count-1)).Count) -and (@(0..($waveNumbers.Count-1)) -join ',') -eq ($waveNumbers -join ',') -and (@($allTaskIds|Select-Object -Unique).Count -eq $allTaskIds.Count) -and @($waves|Where-Object {[int]$_.maxParallel -gt [int]$schedule.maxParallel}).Count -eq 0)
$checks=@(
 [pscustomobject]@{check='schedule-schema';passed=($schemaErrors.Count -eq 0)},
 [pscustomobject]@{check='schedule-hash';passed=([string]$schedule.scheduleHash -ceq $calculatedHash)},
 [pscustomobject]@{check='schedule-record';passed=([string]$schedule.projectionRecordType -ceq 'WebPageStudioSubAgentProjection')},
 [pscustomobject]@{check='evidence-wave-parallel';passed=(@($waves|Where-Object stage -eq 'layer-evidence').Count -gt 0 -and @($waves|Where-Object stage -eq 'layer-evidence'|ForEach-Object {$_.taskIds}).Count -eq 4)},
 [pscustomobject]@{check='validator-wave';passed=(@($waves|Where-Object stage -eq 'layer-validation').Count -eq 1 -and @($waves|Where-Object stage -eq 'layer-validation').taskIds.Count -eq 4)},
 [pscustomobject]@{check='aggregate-last';passed=([string]$waves[-1].stage -ceq 'evidence-aggregation' -and [int]$waves[-1].maxParallel -eq 1)},
 [pscustomobject]@{check='wave-dependencies';passed=([int]$waves[-1].dependsOnWaves[0] -eq [int]$waves[-2].wave)},
 [pscustomobject]@{check='wave-shape-and-no-oversell';passed=$waveShapeValid},
 [pscustomobject]@{check='budget-propagated';passed=([int]$schedule.maxParallel -eq $ConcurrencyBudget -and @($waves|Where-Object {[int]$_.maxParallel -gt $ConcurrencyBudget}).Count -eq 0)},
 [pscustomobject]@{check='external-dispatch-boundary';passed=([string]$schedule.dispatch -ceq 'external-managed-worker-required' -and @($schedule.nonClaims) -contains 'does-not-start-workers')}
 [pscustomobject]@{check='worker-contract-binding';passed=([string]$schedule.leafExecutor -ceq 'ESAutomationProcessRunner' -and @($schedule.workerContractIds).Count -ge 4)}
 [pscustomobject]@{check='admission-before-dispatch';passed=([string]$schedule.admissionGate -ceq 'web-ui-sub-agent-admission')}
)
$failed=@($checks|Where-Object {-not $_.passed})
[ordered]@{validator='web-ui-sub-agent-schedule';status=if($failed.Count){'failed'}else{'passed'};checks=$checks;waveCount=$waves.Count;runtimeStatus='runtime-not-run';nonClaims=@('schedule-only','does-not-start-workers','does-not-prove-runtime-speedup')}|ConvertTo-Json -Depth 10
if($failed.Count){exit 1}
