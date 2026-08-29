[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,
    [string]$ReportPath = 'ES/Output/StaticReplay/es-abcd-evidence.json'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
Import-Module (Join-Path $root 'ES/Automation/ABCD/ESABCDEvidence.psm1') -Force
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('es-abcd-evidence-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
$results = [Collections.Generic.List[object]]::new()
function Case([string]$Name,[scriptblock]$Body) { try { & $Body; [void]$results.Add([pscustomobject]@{case=$Name;status='passed';finding=$null}) } catch { [void]$results.Add([pscustomobject]@{case=$Name;status='failed';finding=$_.Exception.Message}) } }
function Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }

Case 'immutable-snapshot-roundtrip' {
    $state = [ordered]@{taskId='task-snapshot';routePlanHash=('a'*64);sourceScopeHash=('b'*64);assumption='baseline'}
    $created = New-ESABCDImmutableSnapshot -ProjectRoot $fixture -SnapshotRoot 'snapshots' -SnapshotId 'branch-a' -State $state
    $read = Read-ESABCDImmutableSnapshot -ProjectRoot $fixture -Path $created.path -SnapshotHash $created.snapshotHash
    if ($read.snapshotHash -cne $created.snapshotHash -or $read.state.assumption -ne 'baseline') { throw 'snapshot roundtrip mismatch' }
}
Case 'snapshot-idempotency-and-conflict' {
    $state = [ordered]@{taskId='task-idem';value=1}
    $a = New-ESABCDImmutableSnapshot -ProjectRoot $fixture -SnapshotRoot 'snapshots' -SnapshotId 'same' -State $state
    $b = New-ESABCDImmutableSnapshot -ProjectRoot $fixture -SnapshotRoot 'snapshots' -SnapshotId 'same' -State $state
    if ($a.snapshotHash -cne $b.snapshotHash) { throw 'same snapshot was not idempotent' }
    try { New-ESABCDImmutableSnapshot -ProjectRoot $fixture -SnapshotRoot 'snapshots' -SnapshotId 'same' -State ([ordered]@{taskId='task-idem';value=2}) | Out-Null; throw 'snapshot conflict accepted' } catch { if ($_.Exception.Message -notlike '*SNAPSHOT_ID_ALREADY_EXISTS_WITH_DIFFERENT_CONTENT*') { throw } }
}
Case 'snapshot-tamper-rejected' {
    $a = New-ESABCDImmutableSnapshot -ProjectRoot $fixture -SnapshotRoot 'snapshots' -SnapshotId 'tamper' -State ([ordered]@{taskId='task-tamper';value=1})
    $full = Join-Path $fixture $a.path
    $raw = Get-Content -LiteralPath $full -Raw -Encoding UTF8 | ConvertFrom-Json
    $raw.state.value = 99
    [IO.File]::WriteAllText($full, ($raw | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
    try { Read-ESABCDImmutableSnapshot -ProjectRoot $fixture -Path $a.path -SnapshotHash $a.snapshotHash | Out-Null; throw 'tampered snapshot accepted' } catch { if ($_.Exception.Message -notlike '*SNAPSHOT_HASH_MISMATCH*') { throw } }
}
Case 'receipt-artifact-and-hash-are-verified' {
    $payload = [ordered]@{schemaVersion=1;recordType='ABCDVerificationReceipt';status='passed';taskId='task-receipt';observed='ok';receiptHash=$null}
    $payload.receiptHash = Get-ESABCDEvidenceHash ([ordered]@{schemaVersion=1;recordType='ABCDVerificationReceipt';status='passed';taskId='task-receipt';observed='ok'})
    $path = Join-Path $fixture 'receipt.json'; [IO.File]::WriteAllText($path, ($payload | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
    $ref = [pscustomobject]@{path='receipt.json';sha256=(Hash $path);receiptHash=[string]$payload.receiptHash}
    $items = Assert-ESABCDEvidenceReferences -ProjectRoot $fixture -References @($ref)
    if (@($items).Count -ne 1 -or [string]$items[0].receiptHash -cne [string]$payload.receiptHash) { throw 'receipt verification mismatch' }
}
Case 'receipt-tamper-and-path-expansion-are-rejected' {
    $payload = [ordered]@{schemaVersion=1;recordType='ABCDVerificationReceipt';status='passed';taskId='task-receipt-2';receiptHash=$null}
    $payload.receiptHash = Get-ESABCDEvidenceHash ([ordered]@{schemaVersion=1;recordType='ABCDVerificationReceipt';status='passed';taskId='task-receipt-2'})
    $path = Join-Path $fixture 'receipt-2.json'; [IO.File]::WriteAllText($path, ($payload | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
    $payload.status = 'failed'; [IO.File]::WriteAllText($path, ($payload | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
    try { Read-ESABCDReceipt -ProjectRoot $fixture -Path 'receipt-2.json' -ExpectedReceiptHash ([string]$payload.receiptHash) | Out-Null; throw 'tampered receipt accepted' } catch { if ($_.Exception.Message -notlike '*EVIDENCE_RECEIPT_HASH_MISMATCH*') { throw } }
    try { Resolve-ESABCDEvidencePath -ProjectRoot $fixture -RelativePath '..\outside.json' | Out-Null; throw 'path expansion accepted' } catch { if ($_.Exception.Message -notlike '*EVIDENCE_PATH_INVALID*') { throw } }
}

$failed = @($results | Where-Object status -eq 'failed')
$sourceRefs = @('ES/Automation/ABCD/ESABCDEvidence.psm1','ES/Automation/ABCD/Test-ESABCDEvidence.ps1')
$sourceHashes = [ordered]@{}; foreach ($ref in $sourceRefs) { $sourceHashes[$ref] = (Get-FileHash -LiteralPath (Join-Path $root $ref) -Algorithm SHA256).Hash.ToLowerInvariant() }
$evidenceContractPath = Join-Path $root 'ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json'
$evidenceContractHash = (Get-FileHash -LiteralPath $evidenceContractPath -Algorithm SHA256).Hash.ToLowerInvariant()
$planHash = Get-ESABCDEvidenceHash ([ordered]@{validator='Test-ESABCDEvidence';sourceRefHashes=$sourceHashes;cases=@($results)})
$report = [ordered]@{
    schemaVersion=1;validator='Test-ESABCDEvidence';status=if($failed.Count){'failed'}else{'passed'};caseCount=$results.Count;passedCount=@($results|Where-Object status -eq 'passed').Count;failedCount=$failed.Count;cases=@($results);staticStatus=if($failed.Count){'static-failed'}else{'static-passed'};runtimeStatus='runtime-not-run';evidenceLevel='S1';capturedUtc=[DateTime]::UtcNow.ToString('o');authorizationKind='read-only';planHash=$planHash;evidenceContractId='es.skill-evidence-receipt';evidenceContractHash=$evidenceContractHash;skillName='es-agent-mechanism-replication';case='evidence-integrity';receiptPath=$ReportPath.Replace('\','/');sourceRefs=$sourceRefs;sourceRefHashes=$sourceHashes;toolId='es-abcd-evidence-validator';unityVersion='not-run';claimsNotProven=@('cross-process snapshot locking under hostile filesystem','Unity/Worker/host Runtime','external authority')
}
$fullReport = Join-Path $root $ReportPath; New-Item -ItemType Directory -Force -Path (Split-Path $fullReport) | Out-Null
[IO.File]::WriteAllText($fullReport, ($report | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
$report | ConvertTo-Json -Depth 20
if ($failed.Count) { exit 1 }
