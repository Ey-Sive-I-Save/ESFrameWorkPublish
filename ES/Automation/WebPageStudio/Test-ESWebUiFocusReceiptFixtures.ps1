[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$fixtures = Join-Path $PSScriptRoot 'fixtures'
$acceptedReceipts = @('network','preview','visual','release' | ForEach-Object { Join-Path $fixtures ($_ + '-receipt.accepted.synthetic.json') })
$aggregateScript = Join-Path $PSScriptRoot 'Invoke-ESWebUiEvidenceAggregate.ps1'
$aggregateJson = (& $aggregateScript -NetworkReceiptPath $acceptedReceipts[0] -PreviewReceiptPath $acceptedReceipts[1] -VisualReceiptPath $acceptedReceipts[2] -ReleaseReceiptPath $acceptedReceipts[3]) -join "`n"
$aggregatePath = Join-Path $env:TEMP 'es-web-ui-focus-fixture-aggregate.json'
[IO.File]::WriteAllText($aggregatePath,$aggregateJson,[Text.UTF8Encoding]::new($false))
$projectionScript = Join-Path $PSScriptRoot 'Invoke-ESWebUiSubAgentProjection.ps1'
$valid = (& $projectionScript -AggregatePath $aggregatePath -FocusReceiptPath (Join-Path $fixtures 'focus-receipt.valid.json')) -join "`n" | ConvertFrom-Json
$invalid = (& $projectionScript -AggregatePath $aggregatePath -FocusReceiptPath (Join-Path $fixtures 'focus-receipt.invalid.json')) -join "`n" | ConvertFrom-Json
$crossTask = (& $projectionScript -AggregatePath $aggregatePath -FocusReceiptPath (Join-Path $fixtures 'focus-receipt.cross-task.json')) -join "`n" | ConvertFrom-Json
$crossReceiptJson = (& $aggregateScript -TaskId 'web-ui-closure' -NetworkReceiptPath (Join-Path $fixtures 'network-receipt.cross-task.json') -PreviewReceiptPath $acceptedReceipts[1] -VisualReceiptPath $acceptedReceipts[2] -ReleaseReceiptPath $acceptedReceipts[3]) -join "`n"
$crossReceiptPath = Join-Path $env:TEMP 'es-web-ui-cross-task-aggregate.json'
[IO.File]::WriteAllText($crossReceiptPath,$crossReceiptJson,[Text.UTF8Encoding]::new($false))
$joint = (& $projectionScript -AggregatePath $crossReceiptPath -FocusReceiptPath (Join-Path $fixtures 'focus-receipt.cross-task.json')) -join "`n" | ConvertFrom-Json
$refPattern = '\|sha256=[a-f0-9]{64}$'
$refsValid = @($valid.resultEnvelopes | ForEach-Object { @($_.evidenceRefs) } | Where-Object { $_ -notmatch $refPattern }).Count -eq 0
$objectsValid = @($valid.evidenceReferences | Where-Object { [string]$_.sha256 -notmatch '^[a-f0-9]{64}$' -or ($_.receiptHash -and [string]$_.receiptHash -notmatch '^[a-f0-9]{64}$') -or [string]::IsNullOrWhiteSpace([string]$_.path) }).Count -eq 0
$checks = @(
    [pscustomobject]@{case='focus-receipt-verified';passed=([string]$valid.focusBinding.verification.status -ceq 'verified')},
    [pscustomobject]@{case='focus-receipt-invalid-blocked';passed=([string]$invalid.focusBinding.verification.status -ceq 'blocked' -and @($invalid.focusBinding.verification.findings) -contains 'FOCUS_RECEIPT_HASH_INVALID')},
    [pscustomobject]@{case='cross-task-focus-identity-blocked';passed=([string]$crossTask.focusBinding.verification.status -ceq 'blocked' -and @($crossTask.focusBinding.verification.findings) -contains 'FOCUS_IDENTITY_MISMATCH')},
    [pscustomobject]@{case='joint-cross-task-receipt-and-focus-blocked';passed=([string]$joint.parentAggregation.status -in @('partial','conflict') -and [string]$joint.focusBinding.verification.status -ceq 'blocked')},
    [pscustomobject]@{case='parent-evidence-refs-carry-file-hash';passed=$refsValid}
    ,[pscustomobject]@{case='structured-evidence-reference-hashes';passed=$objectsValid}
)
$failed=@($checks|Where-Object {-not $_.passed})
[ordered]@{validator='web-ui-focus-receipt-fixtures';status=if($failed.Count){'failed'}else{'passed'};checks=$checks;runtimeStatus='runtime-not-run';nonClaims=@('static-fixtures','no-focus-host-or-worker-runtime','no-completion-promotion')}|ConvertTo-Json -Depth 8
if($failed.Count){exit 1}
