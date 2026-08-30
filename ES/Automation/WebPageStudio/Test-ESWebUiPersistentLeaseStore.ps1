[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$module=Join-Path $PSScriptRoot 'ESWebUiPersistentLeaseStore.psm1';$text=Get-Content -LiteralPath $module -Raw -Encoding UTF8
$checks=@(
 [pscustomobject]@{check='strict-utf8-and-module';passed=([bool]$text -and $text -match 'Set-StrictMode')},
 [pscustomobject]@{check='named-mutex-serialization';passed=($text -match 'Threading\.Mutex' -and $text -match 'WaitOne')},
 [pscustomobject]@{check='atomic-replace';passed=($text -match 'File\]\:\:Replace' -and $text -match 'File\]\:\:Move')},
 [pscustomobject]@{check='lease-cas-gate';passed=($text -match 'Test-ESLeaseCas' -and $text -match 'WEB_UI_LEASE_CAS_REJECTED')},
 [pscustomobject]@{check='expired-lease-recovery-state';passed=($text -match 'Recover-ESWebUiPersistentLeaseState' -and $text -match "status='orphaned'" -and $text -match 'recovery-pending')},
 [pscustomobject]@{check='path-and-reparse-guard';passed=($text -match 'MUST_BE_ABSOLUTE' -and $text -match 'REPARSE_PARENT' -and $text -match 'REPARSE_FILE')},
 [pscustomobject]@{check='runtime-nonclaim';passed=($text -match 'runtime-not-run' -and $text -match 'does-not-prove-cross-process-runtime')}
)
$failed=@($checks|Where-Object {-not $_.passed});[ordered]@{validator='web-ui-persistent-lease-store';status=if($failed.Count){'failed'}else{'passed'};checks=$checks;runtimeStatus='runtime-not-run';nonClaims=@('static-contract-inspection','does-not-prove-cross-process-atomicity','does-not-prove-crash-recovery') }|ConvertTo-Json -Depth 8;if($failed.Count){exit 1}
