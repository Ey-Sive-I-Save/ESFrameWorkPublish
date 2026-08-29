[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [Parameter(Mandatory = $true)][string]$WarningPath,
    [string]$OutputPath,
    [string]$ReceiptPath,
    [string]$LockPath = 'ES/Automation/Candidates/AIWarningKnowledge/.save-observer.lock',
    [string]$QueueStatePath = 'ES/Automation/Candidates/AIWarningKnowledge/save-observer.queue.json',
    [int]$QueueLimit = 32,
    [int]$DebounceMilliseconds = 150,
    [int]$StabilityMilliseconds = 100,
    [string]$ExpectedWarningHash
)

$ErrorActionPreference = 'Stop'
$strict = [Text.UTF8Encoding]::new($false, $true)
$noBom = [Text.UTF8Encoding]::new($false)
$root = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)

function Resolve-Relative([string]$Path, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($Path) -or [IO.Path]::IsPathRooted($Path)) { throw "${Label}_PATH_NOT_PROJECT_RELATIVE" }
    $full = [IO.Path]::GetFullPath([IO.Path]::Combine($root, $Path))
    if (-not $full.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "${Label}_PATH_OUTSIDE_PROJECT" }
    return $full
}
function Hash-Bytes([byte[]]$Bytes) { $sha = [Security.Cryptography.SHA256]::Create(); try { return ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant() } finally { $sha.Dispose() } }
function Read-Hash([string]$Path) { return Hash-Bytes ([IO.File]::ReadAllBytes($Path)) }
function Write-AtomicJson([string]$Path, $Value) {
    $full = Resolve-Relative $Path 'OUTPUT'
    $dir = Split-Path -Parent $full; [IO.Directory]::CreateDirectory($dir) | Out-Null
    $tmp = "$full.tmp.$([Guid]::NewGuid().ToString('N'))"
    try {
        [IO.File]::WriteAllText($tmp, ($Value | ConvertTo-Json -Depth 20), $noBom)
        if (Test-Path -LiteralPath $full -PathType Leaf) { Move-Item -LiteralPath $tmp -Destination $full -Force } else { [IO.File]::Move($tmp, $full) }
    }
    catch { if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force }; throw }
}

$warningFull = Resolve-Relative $WarningPath 'WARNING'
if (-not (Test-Path -LiteralPath $warningFull -PathType Leaf)) { throw 'WARNING_NOT_FOUND' }
$lockFull = Resolve-Relative $LockPath 'LOCK'
$lockStream = $null
try {
    $lockDir = Split-Path -Parent $lockFull; [IO.Directory]::CreateDirectory($lockDir) | Out-Null
    try { $lockStream = [IO.File]::Open($lockFull, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
    catch [IO.IOException] {
        [pscustomobject]@{ schemaVersion = 1; recordType = 'AIWarningSaveObserverReceipt'; status = 'blocked'; reason = 'OBSERVER_LOCK_BUSY'; transactionExecuted = $false; formalRegistration = 'not-run'; runtimeStatus = 'runtime-not-run' } | ConvertTo-Json -Depth 10
        exit 0
    }
    if ($QueueLimit -lt 1) { throw 'QUEUE_LIMIT_INVALID' }
    $queueFull = Resolve-Relative $QueueStatePath 'QUEUE'
    $pending = @()
    if (Test-Path -LiteralPath $queueFull -PathType Leaf) {
        try { $queue = $strict.GetString([IO.File]::ReadAllBytes($queueFull)) | ConvertFrom-Json; $pending = @($queue.pending) } catch { throw 'QUEUE_STATE_INVALID' }
    }
    $initialHash = Read-Hash $warningFull
    if (-not [string]::IsNullOrWhiteSpace($ExpectedWarningHash) -and $ExpectedWarningHash -cne $initialHash) {
        [pscustomobject]@{ schemaVersion = 1; recordType = 'AIWarningSaveObserverReceipt'; status = 'stale/retry-required'; reason = 'WARNING_HASH_MISMATCH_BEFORE_READ'; warningHash = $initialHash; transactionExecuted = $false; formalRegistration = 'not-run'; runtimeStatus = 'runtime-not-run' } | ConvertTo-Json -Depth 10
        exit 0
    }
    if ($pending.Count -ge $QueueLimit) {
        [pscustomobject]@{ schemaVersion = 1; recordType = 'AIWarningSaveObserverReceipt'; status = 'blocked'; reason = 'QUEUE_LIMIT_REACHED'; queueLength = $pending.Count; queueLimit = $QueueLimit; transactionExecuted = $false; formalRegistration = 'not-run'; runtimeStatus = 'runtime-not-run' } | ConvertTo-Json -Depth 10
        exit 0
    }
    if ($DebounceMilliseconds -gt 0) { Start-Sleep -Milliseconds $DebounceMilliseconds }
    $stableHash = Read-Hash $warningFull
    if ($StabilityMilliseconds -gt 0) { Start-Sleep -Milliseconds $StabilityMilliseconds }
    $finalHash = Read-Hash $warningFull
    if ($stableHash -cne $finalHash) {
        [pscustomobject]@{ schemaVersion = 1; recordType = 'AIWarningSaveObserverReceipt'; status = 'stale/retry-required'; reason = 'WARNING_CHANGED_DURING_DEBOUNCE'; warningHashBefore = $stableHash; warningHashAfter = $finalHash; transactionExecuted = $false; formalRegistration = 'not-run'; runtimeStatus = 'runtime-not-run' } | ConvertTo-Json -Depth 10
        exit 0
    }
    $candidateDir = if ($OutputPath) { Split-Path -Parent $OutputPath } else { 'ES/Automation/Candidates/AIWarningKnowledge' }
    $safe = (([IO.Path]::GetFileNameWithoutExtension($WarningPath) -replace '[^A-Za-z0-9._-]', '-') + '-' + $finalHash.Substring(0, 12))
    $out = if ($OutputPath) { $OutputPath } else { "$candidateDir/$safe.observer.candidate.json" }
    $rec = if ($ReceiptPath) { $ReceiptPath } else { "$candidateDir/$safe.observer.receipt.json" }
    $gen = Join-Path $root '.agents/skills/es-ai-knowledge-curation/scripts/New-ESAIWarningKnowledgeCandidate.ps1'
    $tmpOut = "$out.tmp.observer"; $tmpRec = "$rec.tmp.observer"
    $result = & $gen -ProjectRoot $root -WarningPath $WarningPath -OutputPath $tmpOut -ReceiptPath $tmpRec | ConvertFrom-Json
    $outFull = Resolve-Relative $out 'OUTPUT'; $recFull = Resolve-Relative $rec 'RECEIPT'
    if (-not (Test-Path -LiteralPath $outFull -PathType Leaf)) { try { [IO.File]::Move((Resolve-Relative $tmpOut 'TEMP_OUTPUT'), $outFull) } catch [IO.IOException] { if (-not (Test-Path -LiteralPath $outFull -PathType Leaf)) { throw } } }
    if (-not (Test-Path -LiteralPath $recFull -PathType Leaf)) { try { [IO.File]::Move((Resolve-Relative $tmpRec 'TEMP_OUTPUT'), $recFull) } catch [IO.IOException] { if (-not (Test-Path -LiteralPath $recFull -PathType Leaf)) { throw } } }
    # A repeated idempotent run may find the final files already present; never leave temp artifacts.
    foreach ($temporary in @($tmpOut, $tmpRec)) {
        $temporaryFull = Resolve-Relative $temporary 'TEMP_OUTPUT'
        if (Test-Path -LiteralPath $temporaryFull -PathType Leaf) { Remove-Item -LiteralPath $temporaryFull -Force }
    }
    # This invocation is the consumer for the observed save. Do not leave a
    # completed item in pending; otherwise every save would permanently grow
    # the queue until QueueLimit was reached.
    $pending = @($pending | Where-Object { $_.warningPath -ne $WarningPath })
    Write-AtomicJson $QueueStatePath ([ordered]@{ schemaVersion = 1; queueLimit = $QueueLimit; pending = $pending; updatedAtUtc = [DateTime]::UtcNow.ToString('O') })
    [pscustomobject]@{ schemaVersion = 1; recordType = 'AIWarningSaveObserverReceipt'; status = [string]$result.status; reason = 'candidate-orchestrated'; warningPath = $WarningPath.Replace('\','/'); warningHash = $finalHash; candidateId = [string]$result.candidateId; queueLength = $pending.Count; queueLimit = $QueueLimit; transactionExecuted = $false; formalRegistration = 'not-run'; runtimeStatus = 'runtime-not-run' } | ConvertTo-Json -Depth 10
}
finally { if ($lockStream) { $lockStream.Dispose() } }
