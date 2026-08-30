[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$path=Join-Path $PSScriptRoot 'fixtures\aggregate.blocked.json'
$raw=& (Join-Path $PSScriptRoot 'Convert-ESWebUiAggregateToABCD.ps1') -AggregatePath $path -FocusVerificationStatus blocked -FocusVerificationFindings @('FOCUS_IDENTITY_MISMATCH','RECEIPT_TASK_MISMATCH')
$projection=($raw -join "`n")|ConvertFrom-Json
$event=$projection.verificationEvent
$ok=([string]$projection.decision -ceq 'blocked' -and [string]$event.verificationStatus -ceq 'failed' -and [string]$event.focusVerification.status -ceq 'blocked' -and [bool](-not $event.readyForABCD) -and [bool](-not $projection.promotionAllowed))
[ordered]@{validator='web-ui-focus-abcd-projection';status=if($ok){'passed'}else{'failed'};decision=$projection.decision;verificationStatus=$event.verificationStatus;focusStatus=$event.focusVerification.status;readyForABCD=$event.readyForABCD;promotionAllowed=$projection.promotionAllowed;runtimeStatus='runtime-not-run';nonClaims=@('static-projection-replay','no-abcd-write-or-worker-dispatch')}|ConvertTo-Json -Depth 8
if(-not $ok){exit 1}
