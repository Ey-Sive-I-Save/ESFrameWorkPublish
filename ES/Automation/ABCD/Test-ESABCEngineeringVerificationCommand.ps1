[CmdletBinding()]param()
$ErrorActionPreference='Stop';Import-Module (Join-Path $PSScriptRoot 'ESABCInnovationRun.psm1') -Force
$ok=Invoke-ESABCEngineeringVerificationCommand -Command { 'verification-ok' } -TimeoutSeconds 5
$timeout=Invoke-ESABCEngineeringVerificationCommand -Command { Start-Sleep -Seconds 3; 'late' } -TimeoutSeconds 1
$pass=($ok.status -eq 'passed' -and $ok.exitCode -eq 0 -and $ok.outputHash -match '^[0-9a-fA-F]{64}$' -and $timeout.status -eq 'failed' -and $timeout.timedOut -and $timeout.exitCode -eq -1)
[pscustomobject]@{status=if($pass){'passed'}else{'failed'};successStatus=$ok.status;timeoutStatus=$timeout.status;timeout=$timeout.timedOut;timeoutExitCode=$timeout.exitCode}
if(-not $pass){exit 1}
