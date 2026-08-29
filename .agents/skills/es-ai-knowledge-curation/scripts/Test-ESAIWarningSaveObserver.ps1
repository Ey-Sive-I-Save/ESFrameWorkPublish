[CmdletBinding()]
param([string]$ProjectRoot = (Get-Location).Path, [string]$WarningPath)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($ProjectRoot)
$observer = Join-Path $root '.agents/skills/es-ai-knowledge-curation/scripts/Invoke-ESAIWarningSaveObserver.ps1'
$warningRoot = Join-Path $root 'Assets/Plugins/ES/AIWarnings'
if ([string]::IsNullOrWhiteSpace($WarningPath)) {
    $f = Get-ChildItem -LiteralPath $warningRoot -Recurse -File -Filter 'AgentSkills*.md' | Select-Object -First 1
    if (-not $f) { throw 'DEFAULT_WARNING_NOT_FOUND' }
    $WarningPath = $f.FullName.Substring($root.Length).TrimStart('\','/').Replace('\','/')
}
$suffix = [Guid]::NewGuid().ToString('N')
$dir = 'ES/Automation/Candidates/AIWarningKnowledge'
$out = "$dir/.observer-test-$suffix.candidate.json"; $rec = "$dir/.observer-test-$suffix.receipt.json"; $queue = "$dir/.observer-test-$suffix.queue.json"; $lock = "$dir/.observer-test-$suffix.lock"
$files = @($out,$rec,$queue,$lock)
$cases = [Collections.Generic.List[object]]::new()
function Add-Case($name,[bool]$ok,$detail) { $cases.Add([pscustomobject]@{case=$name;status=if($ok){'passed'}else{'failed'};detail=$detail}) }
try {
    $positive = & $observer -ProjectRoot $root -WarningPath $WarningPath -OutputPath $out -ReceiptPath $rec -QueueStatePath $queue -LockPath $lock -DebounceMilliseconds 0 -StabilityMilliseconds 0 | ConvertFrom-Json
    Add-Case 'candidate-only-positive' ($positive.transactionExecuted -eq $false -and $positive.formalRegistration -eq 'not-run' -and $positive.status -in @('attached','candidate-created','review','blocked')) 'Save observation produced a bounded candidate receipt.'
    Add-Case 'queue-consumption' ($positive.queueLength -eq 0) 'A successfully consumed save is removed from pending instead of accumulating indefinitely.'
    $repeat = & $observer -ProjectRoot $root -WarningPath $WarningPath -OutputPath $out -ReceiptPath $rec -QueueStatePath $queue -LockPath $lock -DebounceMilliseconds 0 -StabilityMilliseconds 0 | ConvertFrom-Json
    Add-Case 'repeat-no-temp-artifacts' (-not (Test-Path -LiteralPath (Join-Path $root "$out.tmp.observer")) -and -not (Test-Path -LiteralPath (Join-Path $root "$rec.tmp.observer")) -and $repeat.transactionExecuted -eq $false) 'Idempotent observation leaves no temporary candidate or receipt artifacts.'
    $warningFull = Join-Path $root $WarningPath
    $hash = (Get-FileHash -LiteralPath $warningFull -Algorithm SHA256).Hash.ToLowerInvariant()
    $stale = & $observer -ProjectRoot $root -WarningPath $WarningPath -OutputPath "$dir/.observer-test-$suffix-stale.json" -ReceiptPath "$dir/.observer-test-$suffix-stale.receipt.json" -QueueStatePath "$dir/.observer-test-$suffix-stale.queue.json" -LockPath "$dir/.observer-test-$suffix-stale.lock" -ExpectedWarningHash ('0' * 64) -DebounceMilliseconds 0 -StabilityMilliseconds 0 | ConvertFrom-Json
    Add-Case 'hash-cas-stale' ($stale.status -eq 'stale/retry-required' -and $stale.reason -eq 'WARNING_HASH_MISMATCH_BEFORE_READ') 'A pre-read hash mismatch fails closed as stale/retry-required.'
    $queueValue = [ordered]@{ schemaVersion=1; queueLimit=1; pending=@([ordered]@{warningPath='existing.md';warningHash=('0'*64);candidateId='existing'}) }
    $queueFull = Join-Path $root $queue; [IO.File]::WriteAllText($queueFull, ($queueValue|ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
    $full = & $observer -ProjectRoot $root -WarningPath $WarningPath -OutputPath "$dir/.observer-test-$suffix-full.json" -ReceiptPath "$dir/.observer-test-$suffix-full.receipt.json" -QueueStatePath $queue -LockPath "$dir/.observer-test-$suffix-full.lock" -QueueLimit 1 -DebounceMilliseconds 0 -StabilityMilliseconds 0 | ConvertFrom-Json
    Add-Case 'queue-bound' ($full.status -eq 'blocked' -and $full.reason -eq 'QUEUE_LIMIT_REACHED') 'A full queue is bounded and does not start orchestration.'
    $heldPath = Join-Path $root "$dir/.observer-test-$suffix-held.lock"
    $held = [IO.File]::Open($heldPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        $busy = & $observer -ProjectRoot $root -WarningPath $WarningPath -OutputPath "$dir/.observer-test-$suffix-busy.json" -ReceiptPath "$dir/.observer-test-$suffix-busy.receipt.json" -QueueStatePath "$dir/.observer-test-$suffix-busy.queue.json" -LockPath "$dir/.observer-test-$suffix-held.lock" -DebounceMilliseconds 0 -StabilityMilliseconds 0 | ConvertFrom-Json
        Add-Case 'process-lock' ($busy.status -eq 'blocked' -and $busy.reason -eq 'OBSERVER_LOCK_BUSY') 'An exclusive process lock prevents concurrent orchestration.'
    } finally { $held.Dispose() }
}
catch { Add-Case 'harness-execution' $false ("$($_.Exception.Message) at $($_.InvocationInfo.PositionMessage)") }
finally { foreach($p in $files + @("$dir/.observer-test-$suffix-stale.json","$dir/.observer-test-$suffix-stale.receipt.json","$dir/.observer-test-$suffix.stale.queue.json","$dir/.observer-test-$suffix-stale.lock","$dir/.observer-test-$suffix-full.json","$dir/.observer-test-$suffix-full.receipt.json","$dir/.observer-test-$suffix-full.lock","$dir/.observer-test-$suffix-held.lock","$dir/.observer-test-$suffix-busy.json","$dir/.observer-test-$suffix-busy.receipt.json","$dir/.observer-test-$suffix-busy.queue.json")){ $x=Join-Path $root $p; if(Test-Path -LiteralPath $x){Remove-Item -LiteralPath $x -Force -ErrorAction SilentlyContinue} } }
$failed=@($cases|Where-Object status -eq 'failed')
[pscustomobject]@{schemaVersion=1;validator='Test-ESAIWarningSaveObserver';status=if($failed.Count -eq 0){'passed'}else{'failed'};caseCount=$cases.Count;passedCount=@($cases|Where-Object status -eq 'passed').Count;failedCount=$failed.Count;cases=@($cases);runtimeStatus='runtime-not-run';nonClaims=@('Static observer replay only; no Save event host or Unity runtime was started.','No formal Knowledge, Index, Warning, Git or Apply state was changed by the harness.')}|ConvertTo-Json -Depth 20
if($failed.Count -gt 0){exit 1}
