[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot,

    [Parameter(Mandatory = $true)]
    [string]$CandidatePath,

    [string]$ReceiptPath
)

$ErrorActionPreference = 'Stop'
$utf8Strict = [Text.UTF8Encoding]::new($false, $true)
$root = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
$schemaModulePath = Join-Path $root 'ES/Automation/Contracts/ESJsonSchemaLite.psm1'
$candidateSchemaPath = Join-Path $root 'ES/Automation/Contracts/es-aiwarning-knowledge-candidate-v1.schema.json'
$receiptSchemaPath = Join-Path $root 'ES/Automation/Contracts/es-aiwarning-knowledge-receipt-v1.schema.json'
Import-Module $schemaModulePath -Force

function Resolve-ProjectPath([string]$Path, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($Path) -or [IO.Path]::IsPathRooted($Path)) { throw "${Label}_PATH_NOT_PROJECT_RELATIVE" }
    $candidate = [IO.Path]::GetFullPath([IO.Path]::Combine($root, $Path))
    if (-not $candidate.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "${Label}_PATH_OUTSIDE_PROJECT" }
    return $candidate
}

function Read-Strict([string]$Path) { return $utf8Strict.GetString([IO.File]::ReadAllBytes($Path)) }
function Hash-Bytes([byte[]]$Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}
function Hash-Text([string]$Text) { return Hash-Bytes ([Text.Encoding]::UTF8.GetBytes($Text)) }
function Add-Finding([Collections.Generic.List[object]]$List, [string]$Code, [string]$Message) {
    $List.Add([pscustomobject]@{ code = $Code; message = $Message })
}

$findings = [Collections.Generic.List[object]]::new()
$candidateFull = Resolve-ProjectPath $CandidatePath 'CANDIDATE'
if (-not (Test-Path -LiteralPath $candidateFull -PathType Leaf)) { throw 'CANDIDATE_NOT_FOUND' }
$candidateText = Read-Strict $candidateFull
try { $candidate = $candidateText | ConvertFrom-Json -ErrorAction Stop } catch { throw 'CANDIDATE_JSON_INVALID' }

$schemaErrors = @(Test-ESJsonSchemaValue -SchemaPath $candidateSchemaPath -Value $candidate)
foreach ($e in $schemaErrors) { Add-Finding $findings 'CANDIDATE_SCHEMA_INVALID' ([string]$e) }

$snapshot = $candidate.sourceSnapshot
$warningFull = $null
try { $warningFull = Resolve-ProjectPath ([string]$snapshot.warningPath) 'WARNING' } catch { Add-Finding $findings 'WARNING_PATH_INVALID' $_.Exception.Message }
if ($warningFull -and -not (Test-Path -LiteralPath $warningFull -PathType Leaf)) { Add-Finding $findings 'WARNING_NOT_FOUND' ([string]$snapshot.warningPath) }

$warningHashActual = $null
if ($warningFull -and (Test-Path -LiteralPath $warningFull -PathType Leaf)) {
    try { $warningHashActual = Hash-Bytes ([IO.File]::ReadAllBytes($warningFull)) } catch { Add-Finding $findings 'WARNING_UTF8_INVALID' $_.Exception.Message }
    if ($warningHashActual -and $warningHashActual -cne [string]$snapshot.warningHash) { Add-Finding $findings 'SOURCE_HASH_DRIFT' "Expected $($snapshot.warningHash), actual $warningHashActual" }
}

$expectedIdempotency = Hash-Text ("$($snapshot.stableId):$($snapshot.warningHash)")
if ([string]$candidate.idempotencyKey -cne $expectedIdempotency) { Add-Finding $findings 'IDEMPOTENCY_KEY_MISMATCH' 'idempotencyKey is not stableId+warningHash SHA-256.' }
$expectedSourceSetHash = Hash-Text ([string]$snapshot.warningHash)
if ([string]$candidate.proposedEntry.expectedHashes.sourceSetHash -cne $expectedSourceSetHash) { Add-Finding $findings 'SOURCE_SET_HASH_MISMATCH' 'sourceSetHash does not match the sorted source hash calculation.' }
if ([string]$candidate.proposedEntry.expectedHashes.contentHash -cne $expectedSourceSetHash) { Add-Finding $findings 'CONTENT_HASH_MISMATCH' 'Compatibility ContentHash does not match the source-set hash.' }

$proposedSource = @($candidate.proposedEntry.sourceRefs | Where-Object { $_.role -eq 'warning-authority' })
if ($proposedSource.Count -ne 1) { Add-Finding $findings 'SOURCE_REF_CARDINALITY' 'Exactly one warning-authority SourceRef is required for a candidate.' }
elseif ([string]$proposedSource[0].path -cne [string]$snapshot.warningPath -or [string]$proposedSource[0].sha256 -cne [string]$snapshot.warningHash) {
    Add-Finding $findings 'SOURCE_REF_BINDING_MISMATCH' 'warning-authority SourceRef does not bind to the captured Warning path and hash.'
}

foreach ($readPath in @($candidate.proposedEntry.requiredReads)) {
    try {
        $readFull = Resolve-ProjectPath ([string]$readPath) 'REQUIRED_READ'
        if (-not (Test-Path -LiteralPath $readFull -PathType Leaf)) { Add-Finding $findings 'REQUIRED_READ_MISSING' ([string]$readPath) }
    } catch { Add-Finding $findings 'REQUIRED_READ_PATH_INVALID' $_.Exception.Message }
}

$destructivePattern = '(?i)(^|[\s''"`])(?:move|relocate|delete|remove|erase|purge|destroy|overwrite|replace|rename|clear|\S*\u79fb\u52a8\S*|\S*\u5220\u9664\S*|\S*\u91cd\u547D\u540D\S*|\S*\u8986\u76D6\S*)(?=($|[\s''"`]))'
foreach ($command in @($candidate.replay.commands)) {
    if ($command -match $destructivePattern) { Add-Finding $findings 'DESTRUCTIVE_REPLAY_COMMAND' ([string]$command) }
    if ($command -match '(?i)(^|\s)([A-Za-z]:[\\/]|/|\\\\)') { Add-Finding $findings 'ABSOLUTE_REPLAY_PATH' ([string]$command) }
}
if ($candidate.replay.candidateOnly -ne $true -or $candidate.replay.applyRequired -ne $true) { Add-Finding $findings 'APPLY_BOUNDARY_INVALID' 'Candidate replay must remain candidate-only and require explicit Apply.' }
if ($candidate.status -eq 'attached' -and ($candidate.match.decision -ne 'existing' -or $candidate.validation.state -ne 'passed')) { Add-Finding $findings 'ATTACHED_STATUS_INCONSISTENT' 'attached requires an existing match and passed candidate validation.' }
if ($candidate.status -eq 'candidate-created' -and $candidate.match.decision -ne 'new') { Add-Finding $findings 'NEW_STATUS_INCONSISTENT' 'candidate-created requires a new match decision.' }
if ($candidate.status -eq 'review' -and $candidate.match.decision -ne 'ambiguous') { Add-Finding $findings 'REVIEW_STATUS_INCONSISTENT' 'review requires an ambiguous match decision.' }
if ($candidate.status -eq 'blocked' -and $candidate.match.decision -ne 'blocked') { Add-Finding $findings 'BLOCKED_STATUS_INCONSISTENT' 'blocked requires a blocked match decision.' }
if ($candidate.status -eq 'stale/retry-required' -and $candidate.match.decision -ne 'stale') { Add-Finding $findings 'STALE_STATUS_INCONSISTENT' 'stale/retry-required requires a stale match decision.' }

if ($ReceiptPath) {
    $receiptFull = Resolve-ProjectPath $ReceiptPath 'RECEIPT'
    if (-not (Test-Path -LiteralPath $receiptFull -PathType Leaf)) { Add-Finding $findings 'RECEIPT_NOT_FOUND' $ReceiptPath }
    else {
        $receiptText = Read-Strict $receiptFull
        try { $receipt = $receiptText | ConvertFrom-Json -ErrorAction Stop } catch { Add-Finding $findings 'RECEIPT_JSON_INVALID' $ReceiptPath; $receipt = $null }
        if ($receipt) {
            foreach ($e in @(Test-ESJsonSchemaValue -SchemaPath $receiptSchemaPath -Value $receipt)) { Add-Finding $findings 'RECEIPT_SCHEMA_INVALID' ([string]$e) }
            $candidateJsonCanonical = $candidate | ConvertTo-Json -Depth 30 -Compress
            $candidateHashActual = Hash-Text $candidateJsonCanonical
            if ([string]$receipt.candidateId -cne [string]$candidate.candidateId) { Add-Finding $findings 'RECEIPT_CANDIDATE_BINDING_MISMATCH' 'Receipt candidateId differs from candidate.' }
            if ([string]$receipt.idempotencyKey -cne [string]$candidate.idempotencyKey) { Add-Finding $findings 'RECEIPT_IDEMPOTENCY_MISMATCH' 'Receipt idempotencyKey differs from candidate.' }
            if ([string]$receipt.candidateHash -cne $candidateHashActual) { Add-Finding $findings 'RECEIPT_CANDIDATE_HASH_MISMATCH' "Expected $candidateHashActual, actual $($receipt.candidateHash)" }
            if ($receipt.transactionExecuted -ne $false -or $receipt.formalRegistration -ne 'not-run') { Add-Finding $findings 'RECEIPT_APPLY_BOUNDARY_INVALID' 'Candidate receipt cannot claim an executed transaction or formal registration.' }
        }
    }
}

$status = if ($findings.Count -eq 0) { 'passed' } else { 'blocked' }
[pscustomobject]@{
    schemaVersion = 1
    validator = 'Test-ESAIWarningKnowledgeCandidate'
    status = $status
    candidateId = [string]$candidate.candidateId
    candidateStatus = [string]$candidate.status
    findingCount = $findings.Count
    findings = @($findings)
    runtimeStatus = 'runtime-not-run'
    nonClaims = @(
        'This validator proves candidate structure, current source binding, deterministic idempotency and candidate-only boundaries only.',
        'It does not apply Knowledge, alter KnowledgeIndex.yaml, or prove Unity/Runtime/release behavior.'
    )
} | ConvertTo-Json -Depth 20
if ($findings.Count -gt 0) { exit 1 }
