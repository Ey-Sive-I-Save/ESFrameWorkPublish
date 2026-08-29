[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$ReportPath='ES/Output/StaticReplay/es-task-context-cross-process.json'
)

$ErrorActionPreference = 'Stop'
$modulePath = Join-Path $PSScriptRoot 'ESTaskContextRuntime.psm1'
$fixturePath = Join-Path $PSScriptRoot 'Test-ESTaskContextRoutePlanFixture.ps1'
$projectRoot = if ($ProjectRoot) { (Resolve-Path -LiteralPath $ProjectRoot).Path } else { (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path }
Import-Module $modulePath -Force
. $fixturePath

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Read-StrictJson([string]$Path) {
    $raw = [Text.UTF8Encoding]::new($false, $true).GetString([IO.File]::ReadAllBytes($Path))
    return $raw | ConvertFrom-Json -ErrorAction Stop
}

function Write-Report($Report) {
    if ([IO.Path]::IsPathRooted($ReportPath) -or $ReportPath -match '(^|[\\/])\.\.([\\/]|$)' -or $ReportPath -match '[*?]') { throw 'ReportPath must be project-relative and bounded.' }
    $full = [IO.Path]::GetFullPath((Join-Path $projectRoot $ReportPath))
    if (-not $full.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'ReportPath escapes ProjectRoot.' }
    $parent = Split-Path -Parent $full
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    [IO.File]::WriteAllText($full, ($Report | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
}

function Start-TaskWorker {
    param(
        [Parameter(Mandatory=$true)][string]$Operation,
        [Parameter(Mandatory=$true)][string]$IdempotencyKey,
        [Parameter(Mandatory=$true)][int]$ExpectedTaskRevision,
        [Parameter(Mandatory=$true)][int]$ExpectedContextVersion,
        [Parameter(Mandatory=$true)][string]$ResultPath,
        [Parameter(Mandatory=$true)][string]$GatePath,
        [Parameter(Mandatory=$true)][bool]$RetryCas
    )
    $worker = @'
$ErrorActionPreference='Stop'
Import-Module $env:ES_TCR_MODULE -Force
$deadline=[DateTime]::UtcNow.AddSeconds(15)
while(-not(Test-Path -LiteralPath $env:ES_TCR_GATE -PathType Leaf)){
    if([DateTime]::UtcNow-ge$deadline){throw 'WORKER_GATE_TIMEOUT'}
    Start-Sleep -Milliseconds 10
}
$attempts=0;$initial='unknown';$retry='not-run';$state=$null;$errorText=$null;$retryKey=$null
try{
    $attempts++
    $state=Invoke-ESTaskContextTransition -ProjectRoot $env:ES_TCR_ROOT -StoreRoot 'state' -TaskId 'task' -Transition $env:ES_TCR_OPERATION -ExpectedTaskRevision ([int]$env:ES_TCR_REVISION) -ExpectedContextVersion ([int]$env:ES_TCR_CONTEXT) -IdempotencyKey $env:ES_TCR_KEY
    $initial='success'
}catch{
    $errorText=$_.Exception.Message
    if($env:ES_TCR_RETRY-cne'true'-or$errorText-notlike'CAS conflict:*'){throw}
    $initial='cas-conflict'
    $current=Get-ESTaskContextState -ProjectRoot $env:ES_TCR_ROOT -StoreRoot 'state' -TaskId 'task' -VerifyIntegrity
    $attempts++
    $retryKey=$env:ES_TCR_KEY+'-retry'
    $state=Invoke-ESTaskContextTransition -ProjectRoot $env:ES_TCR_ROOT -StoreRoot 'state' -TaskId 'task' -Transition $env:ES_TCR_OPERATION -ExpectedTaskRevision $current.taskRevision -ExpectedContextVersion $current.contextVersion -IdempotencyKey $retryKey
    $retry='success'
}
$result=[ordered]@{operation=$env:ES_TCR_OPERATION;idempotencyKey=$env:ES_TCR_KEY;retryIdempotencyKey=$retryKey;initial=$initial;retry=$retry;attempts=$attempts;initialError=$errorText;taskRevision=[int]$state.taskRevision;contextVersion=[int]$state.contextVersion;taskStatus=[string]$state.taskStatus;contextStatus=[string]$state.contextStatus}
[IO.File]::WriteAllText($env:ES_TCR_RESULT,($result|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
'@
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($worker))
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = (Get-Process -Id $PID).Path
    $start.Arguments = '-NoProfile -NonInteractive -EncodedCommand ' + $encoded
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.EnvironmentVariables['ES_TCR_MODULE'] = $modulePath
    $start.EnvironmentVariables['ES_TCR_ROOT'] = $script:testRoot
    $start.EnvironmentVariables['ES_TCR_GATE'] = $GatePath
    $start.EnvironmentVariables['ES_TCR_RESULT'] = $ResultPath
    $start.EnvironmentVariables['ES_TCR_OPERATION'] = $Operation
    $start.EnvironmentVariables['ES_TCR_KEY'] = $IdempotencyKey
    $start.EnvironmentVariables['ES_TCR_REVISION'] = [string]$ExpectedTaskRevision
    $start.EnvironmentVariables['ES_TCR_CONTEXT'] = [string]$ExpectedContextVersion
    $start.EnvironmentVariables['ES_TCR_RETRY'] = if ($RetryCas) { 'true' } else { 'false' }
    return [Diagnostics.Process]::Start($start)
}

function Wait-TaskWorkers([Diagnostics.Process[]]$Processes, [string[]]$ResultPaths) {
    foreach ($process in $Processes) {
        if (-not $process.WaitForExit(30000)) { try { $process.Kill() } catch {}; throw 'TASK_WORKER_TIMEOUT' }
        if ($process.ExitCode -ne 0) { throw "TASK_WORKER_FAILED:$($process.Id):$($process.ExitCode)" }
    }
    foreach ($path in $ResultPaths) { if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "TASK_WORKER_RESULT_MISSING:$path" } }
    return @($ResultPaths | ForEach-Object { Read-StrictJson $_ })
}

$script:testRoot = Join-Path ([IO.Path]::GetTempPath()) ('es-task-context-cross-process-' + [Guid]::NewGuid().ToString('N'))
$results = [Collections.Generic.List[object]]::new()
try {
    New-Item -ItemType Directory -Path $script:testRoot | Out-Null
    Initialize-ESTestRoutePlanRepository $script:testRoot
    [IO.File]::WriteAllText((Join-Path $script:testRoot 'source.txt'), 'cross-process-source', [Text.UTF8Encoding]::new($false))
    $goal = New-ESGoalRevision -ProjectRoot $script:testRoot -StoreRoot 'state' -GoalId 'goal-cross-process' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'cross-process CAS' -Budget ([ordered]@{maxReads=8;maxSeconds=30})
    $routePlan = New-ESTestRoutePlan -Root $script:testRoot -Goal $goal
    $state = New-ESTaskContextTask -ProjectRoot $script:testRoot -StoreRoot 'state' -TaskId 'task' -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequestedSourceScope 'source.txt' -IdempotencyKey 'create'

    $gate1 = Join-Path $script:testRoot 'gate-cas'
    $result1 = Join-Path $script:testRoot 'worker-compaction.json'
    $result2 = Join-Path $script:testRoot 'worker-suspend.json'
    $workers = @(
        (Start-TaskWorker BeginCompaction 'race-compaction' $state.taskRevision $state.contextVersion $result1 $gate1 $true),
        (Start-TaskWorker Suspend 'race-suspend' $state.taskRevision $state.contextVersion $result2 $gate1 $true)
    )
    [IO.File]::WriteAllText($gate1, 'go', [Text.UTF8Encoding]::new($false))
    $race = @(Wait-TaskWorkers $workers @($result1,$result2))
    Assert-True (@($race | Where-Object initial -eq 'success').Count -eq 1) 'Exactly one competing operation must win the initial CAS.'
    Assert-True (@($race | Where-Object initial -eq 'cas-conflict').Count -eq 1) 'Exactly one competing operation must observe a stale CAS.'
    Assert-True (@($race | Where-Object { $_.initial -eq 'cas-conflict' -and $_.retry -eq 'success' }).Count -eq 1) 'The CAS loser did not reread and retry successfully.'
    Assert-True (@($race | Where-Object { $_.initial -eq 'cas-conflict' -and $_.retryIdempotencyKey -eq ($_.idempotencyKey + '-retry') }).Count -eq 1) 'The CAS loser reused the initial operation idempotency identity.'
    $state = Get-ESTaskContextState -ProjectRoot $script:testRoot -StoreRoot 'state' -TaskId 'task' -VerifyIntegrity
    Assert-True ($state.taskStatus -eq 'Suspended' -and $state.contextStatus -eq 'Compacting') 'Competing operations did not both commit after bounded retry.'
    [void]$results.Add([pscustomobject]@{case='cross-process-cas-race-and-retry';status='passed';detail=$race})

    $eventsBefore = @(Get-ChildItem -LiteralPath (Join-Path $script:testRoot 'state/task/events') -File -Filter '*.json').Count
    $gate2 = Join-Path $script:testRoot 'gate-idempotency'
    $result3 = Join-Path $script:testRoot 'worker-idempotent-a.json'
    $result4 = Join-Path $script:testRoot 'worker-idempotent-b.json'
    $workers = @(
        (Start-TaskWorker EndCompaction 'same-idempotency-key' $state.taskRevision $state.contextVersion $result3 $gate2 $false),
        (Start-TaskWorker EndCompaction 'same-idempotency-key' $state.taskRevision $state.contextVersion $result4 $gate2 $false)
    )
    [IO.File]::WriteAllText($gate2, 'go', [Text.UTF8Encoding]::new($false))
    $same = @(Wait-TaskWorkers $workers @($result3,$result4))
    $eventsAfter = @(Get-ChildItem -LiteralPath (Join-Path $script:testRoot 'state/task/events') -File -Filter '*.json').Count
    Assert-True ($eventsAfter -eq ($eventsBefore + 1)) 'Repeated cross-process idempotency key appended more than one event.'
    Assert-True (@($same | Where-Object initial -eq 'success').Count -eq 2) 'An exact idempotent replay did not return the committed state.'
    Assert-True (@($same | Select-Object taskRevision,contextVersion,taskStatus,contextStatus -Unique).Count -eq 1) 'Idempotent callers observed different committed states.'
    [void]$results.Add([pscustomobject]@{case='cross-process-idempotency-single-event';status='passed';detail=$same})

    $receiptRoot = Join-Path $script:testRoot 'state/task/receipts'
    New-Item -ItemType Directory -Path $receiptRoot -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $receiptRoot 'interrupted-orphan.json'), '{}', [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $script:testRoot 'state/task/events/.0000009999-interrupted.json.deadbeef.tmp'), '{', [Text.UTF8Encoding]::new($false))
    $integrity = Test-ESTaskContextIntegrity -ProjectRoot $script:testRoot -StoreRoot 'state' -TaskId 'task'
    Assert-True ($integrity.status -eq 'passed') 'The committed event chain failed integrity validation.'
    Assert-True ($integrity.eventCount -eq $eventsAfter) 'The interrupted partial event was treated as committed.'
    Assert-True ($integrity.orphanReceiptCount -eq 1 -and $integrity.orphanReceiptsAuthoritative -eq $false) 'The orphan receipt was treated as authoritative.'
    [void]$results.Add([pscustomobject]@{case='event-chain-and-interruption-recovery';status='passed';detail=$integrity})

    $damagedFinal = Join-Path $script:testRoot 'state/task/events/0000009999-damaged.json'
    [IO.File]::WriteAllText($damagedFinal, '{', [Text.UTF8Encoding]::new($false))
    $damagedIntegrity = Test-ESTaskContextIntegrity -ProjectRoot $script:testRoot -StoreRoot 'state' -TaskId 'task'
    Assert-True ($damagedIntegrity.status -eq 'failed') 'A damaged final event file was not rejected fail-closed.'
    Assert-True ([string]$damagedIntegrity.finding -like 'Event log contains a revision gap or duplicate.*' -or [string]$damagedIntegrity.finding -like 'Invalid strict UTF-8 JSON:*') 'The damaged final event failed for an unrelated reason.'
    [void]$results.Add([pscustomobject]@{case='damaged-final-event-fails-closed';status='passed';detail=$damagedIntegrity.finding})

    $sourceRefs=@('ES/Automation/TaskContextRuntime/ESTaskContextRuntime.psm1','ES/Automation/TaskContextRuntime/Test-ESTaskContextCrossProcess.ps1')
    $sourceRefHashes=[ordered]@{};foreach($ref in $sourceRefs){$sourceRefHashes[$ref]=(Get-FileHash -LiteralPath (Join-Path $projectRoot $ref) -Algorithm SHA256).Hash.ToLowerInvariant()}
    $evidenceContractPath=Join-Path $projectRoot 'ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json'
    $evidenceContractHash=(Get-FileHash -LiteralPath $evidenceContractPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $seed=($sourceRefs|ForEach-Object{$_+':'+$sourceRefHashes[$_]})-join'|'
    $planHash=([Security.Cryptography.SHA256]::Create()).ComputeHash([Text.Encoding]::UTF8.GetBytes($seed+'|'+(($results|ConvertTo-Json -Compress -Depth 20))))
    $planHash=([BitConverter]::ToString($planHash)).Replace('-','').ToLowerInvariant()
    $finalReport=[pscustomobject][ordered]@{schemaVersion=1;validator='Test-ESTaskContextCrossProcess';status='passed';caseCount=$results.Count;passedCount=$results.Count;failedCount=0;cases=@($results);staticStatus='static-passed';runtimeStatus='runtime-not-run';evidenceLevel='S2';capturedUtc=[DateTime]::UtcNow.ToString('o');authorizationKind='read-only';planHash=$planHash;evidenceContractId='es.skill-evidence-receipt';evidenceContractHash=$evidenceContractHash;skillName='es-agent-mechanism-replication';case='cross-process-cas';toolId='es-task-context-cross-process-validator';unityVersion='not-run';receiptPath=$ReportPath.Replace('\','/');sourceRefs=$sourceRefs;sourceRefHashes=$sourceRefHashes;claimsNotProven=@('Unity Runtime','Worker Runtime','cross-machine distributed locking','physical power-loss durability')}
    Write-Report $finalReport
    $finalReport | ConvertTo-Json -Depth 20
} catch {
    $sourceRefs=@('ES/Automation/TaskContextRuntime/ESTaskContextRuntime.psm1','ES/Automation/TaskContextRuntime/Test-ESTaskContextCrossProcess.ps1')
    $sourceRefHashes=[ordered]@{};foreach($ref in $sourceRefs){$sourceRefHashes[$ref]=(Get-FileHash -LiteralPath (Join-Path $projectRoot $ref) -Algorithm SHA256).Hash.ToLowerInvariant()}
    $evidenceContractHash=(Get-FileHash -LiteralPath (Join-Path $projectRoot 'ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json') -Algorithm SHA256).Hash.ToLowerInvariant()
    $failedReport=[pscustomobject][ordered]@{schemaVersion=1;validator='Test-ESTaskContextCrossProcess';status='failed';caseCount=$results.Count+1;passedCount=$results.Count;failedCount=1;cases=@($results)+@([pscustomobject]@{case='execution';status='failed';detail=$_.Exception.Message});staticStatus='static-failed';runtimeStatus='runtime-not-run';evidenceLevel='S2';capturedUtc=[DateTime]::UtcNow.ToString('o');authorizationKind='read-only';evidenceContractId='es.skill-evidence-receipt';evidenceContractHash=$evidenceContractHash;skillName='es-agent-mechanism-replication';case='cross-process-cas';toolId='es-task-context-cross-process-validator';unityVersion='not-run';receiptPath=$ReportPath.Replace('\','/');sourceRefs=$sourceRefs;sourceRefHashes=$sourceRefHashes;claimsNotProven=@('Unity Runtime','Worker Runtime','cross-machine distributed locking','physical power-loss durability')}
    Write-Report $failedReport
    $failedReport | ConvertTo-Json -Depth 20
    exit 1
} finally {
    if (Test-Path -LiteralPath $script:testRoot -PathType Container) {
        $resolved = [IO.Path]::GetFullPath($script:testRoot)
        $tempPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
        if ($resolved.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase) -and (Split-Path -Leaf $resolved) -like 'es-task-context-cross-process-*') {
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
    }
}
