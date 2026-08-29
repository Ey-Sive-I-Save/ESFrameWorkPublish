[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,
    [string]$SchemaPath = ''
)

$ErrorActionPreference='Stop'
if([string]::IsNullOrWhiteSpace($SchemaPath)){$SchemaPath=Join-Path $ProjectRoot 'ES/Automation/Contracts/es-ai-abc-task-binding-v1.schema.json'}
Import-Module (Join-Path $ProjectRoot 'ES/Automation/TaskContextRuntime/ESTaskContextRuntime.psm1') -Force
. (Join-Path $ProjectRoot 'ES/Automation/TaskContextRuntime/Test-ESTaskContextRoutePlanFixture.ps1')
Import-Module (Join-Path $ProjectRoot 'ES/Automation/Contracts/ESJsonSchemaLite.psm1') -Force

$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('es-abc-binding-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$cases=[Collections.Generic.List[object]]::new()
function Add-Case([string]$Id,[bool]$Passed,[string]$Finding=''){[void]$cases.Add([pscustomobject][ordered]@{case=$Id;status=if($Passed){'passed'}else{'failed'};finding=$Finding})}
function Invoke-Case([string]$Id,[scriptblock]$Body){try{$finding=& $Body;Add-Case $Id $true ''}catch{Add-Case $Id $false $_.Exception.Message}}
function New-Fixture([string]$Name){$root=Join-Path $testRoot $Name;New-Item -ItemType Directory -Path $root -Force|Out-Null;Initialize-ESTestRoutePlanRepository $root;[IO.File]::WriteAllText((Join-Path $root 'source.txt'),"source:$Name",[Text.UTF8Encoding]::new($false));$goal=New-ESGoalRevision -ProjectRoot $root -StoreRoot 'state' -GoalId ('goal-'+$Name) -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'abc binding' -Budget ([ordered]@{maxReads=16});$route=New-ESTestRoutePlan -Root $root -Goal $goal;return [pscustomobject]@{root=$root;goal=$goal;route=$route}}
function Get-PrivateHash($Value){$m=Get-Module ESTaskContextRuntime;return (& $m {param($v) Get-ESObjectHash $v} $Value)}
function New-Binding($f,[string]$TaskId='task-abc',[int]$TaskRevision=1,[int]$ContextVersion=1,$Focus=$null){$file=Get-Item (Join-Path $f.root 'source.txt');$sha=(Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant();$scopeHash=Get-PrivateHash @([ordered]@{path='source.txt';length=[int64]$file.Length;sha256=$sha});$exchange=[ordered]@{schemaVersion=1;contractId='es://automation/contracts/ai-abc/exchange-receipt/v1';recordType='ABCExchangeReceipt';exchangeId=('ex-'+([Guid]::NewGuid().ToString('N')));taskId=$TaskId;routePlanHash=$f.route.routePlanHash;sourceScopeHash=$scopeHash;coreRef='es.ai-abc.core.v1';partRefs=@();status='accepted';producerId='abcc.exchange.v1';receiptHash=$null};$exchangeHashInput=[ordered]@{};foreach($p in $exchange.GetEnumerator()){if($p.Key-ne'receiptHash'){$exchangeHashInput[$p.Key]=$p.Value}};$exchange.receiptHash=Get-PrivateHash $exchangeHashInput;$exchangePath=Join-Path $f.root 'exchange-receipt.json';[IO.File]::WriteAllText($exchangePath,($exchange|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false));$exchangeFileHash=(Get-FileHash $exchangePath -Algorithm SHA256).Hash.ToLowerInvariant();$b=[ordered]@{schemaVersion=1;bindingId=('atb-'+([Guid]::NewGuid().ToString('N')));task=[ordered]@{taskId=$TaskId;taskRevision=$TaskRevision;contextVersion=$ContextVersion};route=[ordered]@{routePlanId=$f.route.routePlanId;routePlanHash=$f.route.routePlanHash};abc=[ordered]@{coreRef='es.ai-abc.core.v1';partRefs=@();exchangeReceiptHash=$exchange.receiptHash;exchangeReceiptRef=[ordered]@{path='exchange-receipt.json';sha256=$exchangeFileHash;producerId=$exchange.producerId}};focus=$Focus;sourceScopeHash=$scopeHash;bindingHash=$null};$b.bindingHash=Get-PrivateHash (& (Get-Module ESTaskContextRuntime) {param($v) Get-ESABCTaskBindingHashInput $v} ([pscustomobject]$b));$path=Join-Path $f.root 'binding.json';[IO.File]::WriteAllText($path,($b|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false));return [pscustomobject]@{binding=$b;path='binding.json';scopeHash=$scopeHash}}

Invoke-Case 'normal-binding' {
    $f=New-Fixture 'normal';$b=New-Binding $f
    $schemaErrors=@(Test-ESJsonSchemaValue -SchemaPath $SchemaPath -Value ([pscustomobject]$b.binding));if($schemaErrors.Count){throw ('Binding schema validation failed: '+($schemaErrors -join '; '))}
    $s=New-ESTaskContextTask -ProjectRoot $f.root -StoreRoot 'state' -TaskId 'task-abc' -PlanHash $f.route.routePlanHash -RoutePlanPath $f.route.path -GoalRevisionPath $f.goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequestedSourceScope 'source.txt' -TaskBindingPath $b.path -IdempotencyKey 'create'
    if([string]$s.taskBindingRef.bindingHash -cne [string]$b.binding.bindingHash){throw 'TaskBindingRef was not persisted.'}
    $copied=Join-Path $f.root ('state/task-abc/bindings/'+$b.binding.bindingId+'.json');if(-not(Test-Path $copied)){throw 'Binding sidecar was not copied into task store.'}
}
Invoke-Case 'binding-hash-tamper' {
    $f=New-Fixture 'tamper';$b=New-Binding $f;$raw=Get-Content (Join-Path $f.root $b.path) -Raw|ConvertFrom-Json;$raw.bindingHash='0'*64;[IO.File]::WriteAllText((Join-Path $f.root $b.path),($raw|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false));try{New-ESTaskContextTask -ProjectRoot $f.root -StoreRoot 'state' -TaskId 'task-abc' -PlanHash $f.route.routePlanHash -RoutePlanPath $f.route.path -GoalRevisionPath $f.goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequestedSourceScope 'source.txt' -TaskBindingPath $b.path -IdempotencyKey 'create'|Out-Null;throw 'Tampered binding was accepted.'}catch{if($_.Exception.Message -notmatch 'Binding hash mismatch|pattern'){throw}}
}
Invoke-Case 'task-revision-context-conflict' {
    $f=New-Fixture 'cas';$b=New-Binding $f -TaskRevision 2 -ContextVersion 1;try{New-ESTaskContextTask -ProjectRoot $f.root -StoreRoot 'state' -TaskId 'task-abc' -PlanHash $f.route.routePlanHash -RoutePlanPath $f.route.path -GoalRevisionPath $f.goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequestedSourceScope 'source.txt' -TaskBindingPath $b.path -IdempotencyKey 'create'|Out-Null;throw 'Revision conflict was accepted.'}catch{if($_.Exception.Message -notmatch 'CAS revision/context mismatch'){throw}}
}
Invoke-Case 'focus-supersede' {
    $f=New-Fixture 'focus';$focus=[pscustomobject]@{checkpointId='FCK-'+('c'*32);revision=1;proposalHash=('1'*64);checkpointHash=('2'*64)};$b=New-Binding $f -Focus $focus;$s=New-ESTaskContextTask -ProjectRoot $f.root -StoreRoot 'state' -TaskId 'task-abc' -PlanHash $f.route.routePlanHash -RoutePlanPath $f.route.path -GoalRevisionPath $f.goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequestedSourceScope 'source.txt' -TaskBindingPath $b.path -FocusContextId $focus.checkpointId -FocusRevision 1 -FocusProposalHash $focus.proposalHash -FocusScopeHash $b.scopeHash -IdempotencyKey 'create';$copied=Join-Path $f.root ('state/task-abc/bindings/'+$b.binding.bindingId+'.json');$mut=Get-Content $copied -Raw|ConvertFrom-Json;$mut.focus.revision=2;$mut.bindingHash=Get-PrivateHash (& (Get-Module ESTaskContextRuntime) {param($v) Get-ESABCTaskBindingHashInput $v} $mut);[IO.File]::WriteAllText($copied,($mut|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false));try{Complete-ESTaskContextTask -ProjectRoot $f.root -StoreRoot 'state' -TaskId 'task-abc' -ExpectedTaskRevision 1 -ExpectedContextVersion 1 -IdempotencyKey 'complete'|Out-Null;throw 'Superseded Focus binding was accepted.'}catch{if($_.Exception.Message -notmatch 'focus revision mismatch'){throw}}
}
Invoke-Case 'missing-abc-core-binding' {
    $route=Get-Content (Join-Path $ProjectRoot 'ES/Automation/Contracts/es-route-stage.registry.json') -Raw -Encoding UTF8|ConvertFrom-Json;$weapon=@($route.stages|Where-Object stageContractId -eq 'es.route-stage.weapon-abc-part.v1')[0];$core=@($route.stages|Where-Object stageContractId -eq 'es.route-stage.ai-abc-negotiation.v1')[0];if(@($weapon.requires)-notcontains 'abc-core-binding' -or @($core.produces)-notcontains 'abc-core-binding'){throw 'Route producer/consumer closure is missing.'}
}
Invoke-Case 'exchange-receipt-tamper' {
    $f=New-Fixture 'exchange-tamper';$b=New-Binding $f
    $receiptPath=Join-Path $f.root 'exchange-receipt.json';$receipt=Get-Content -LiteralPath $receiptPath -Raw -Encoding UTF8|ConvertFrom-Json;$receipt.status='accepted';$receipt.taskId='other-task';[IO.File]::WriteAllText($receiptPath,($receipt|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false))
    try { New-ESTaskContextTask -ProjectRoot $f.root -StoreRoot 'state' -TaskId 'task-abc' -PlanHash $f.route.routePlanHash -RoutePlanPath $f.route.path -GoalRevisionPath $f.goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequestedSourceScope 'source.txt' -TaskBindingPath $b.path -IdempotencyKey 'create'|Out-Null; throw 'Tampered exchange receipt was accepted.' } catch { if($_.Exception.Message -notmatch 'exchange receipt artifact hash mismatch|exchange receipt hash mismatch|does not match'){throw} }
}
Invoke-Case 'legacy-task-without-binding' {
    $f=New-Fixture 'legacy';$s=New-ESTaskContextTask -ProjectRoot $f.root -StoreRoot 'state' -TaskId 'legacy-task' -PlanHash $f.route.routePlanHash -RoutePlanPath $f.route.path -GoalRevisionPath $f.goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequestedSourceScope 'source.txt' -IdempotencyKey 'create';$g=Get-ESTaskContextState -ProjectRoot $f.root -StoreRoot 'state' -TaskId 'legacy-task' -VerifyIntegrity;if($null -ne $g.taskBindingRef){throw 'Legacy task unexpectedly acquired a binding.'}
}
Invoke-Case 'duplicate-idempotency' {
    $f=New-Fixture 'idem';$b=New-Binding $f;$a=New-ESTaskContextTask -ProjectRoot $f.root -StoreRoot 'state' -TaskId 'task-abc' -PlanHash $f.route.routePlanHash -RoutePlanPath $f.route.path -GoalRevisionPath $f.goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequestedSourceScope 'source.txt' -TaskBindingPath $b.path -IdempotencyKey 'same';$c=New-ESTaskContextTask -ProjectRoot $f.root -StoreRoot 'state' -TaskId 'task-abc' -PlanHash $f.route.routePlanHash -RoutePlanPath $f.route.path -GoalRevisionPath $f.goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequestedSourceScope 'source.txt' -TaskBindingPath $b.path -IdempotencyKey 'same';$events=@(Get-ChildItem (Join-Path $f.root 'state/task-abc/events') -Filter '*.json');if($events.Count-ne1){throw "Idempotent create wrote $($events.Count) events."}
}
Invoke-Case 'abcd-orchestration-uses-platform-event-chain' {
    $f=New-Fixture 'abcd-runtime';$b=New-Binding $f
    $s=New-ESTaskContextTask -ProjectRoot $f.root -StoreRoot 'state' -TaskId 'task-abc' -PlanHash $f.route.routePlanHash -RoutePlanPath $f.route.path -GoalRevisionPath $f.goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequestedSourceScope 'source.txt' -TaskBindingPath $b.path -IdempotencyKey 'create'
    $before=Get-ESTaskContextState -ProjectRoot $f.root -StoreRoot 'state' -TaskId 'task-abc'
    $payload=[ordered]@{roundNo=1;decision='retry';nextAction='retry-same-plan'}
    $after=Add-ESTaskABCDOrchestrationEvent -ProjectRoot $f.root -StoreRoot 'state' -TaskId 'task-abc' -EventType 'iteration-round-started' -Payload $payload -TaskBindingId $b.binding.bindingId -TaskBindingHash $b.binding.bindingHash -RoutePlanHash $f.route.routePlanHash -SourceScopeHash $b.scopeHash -ExpectedTaskRevision $before.taskRevision -ExpectedContextVersion $before.contextVersion -IdempotencyKey 'abcd-event-1'
    if([int]$after.taskRevision -ne ([int]$before.taskRevision+1) -or [int]$after.contextVersion -ne ([int]$before.contextVersion+1)){throw 'ABCD event did not advance platform CAS.'}
    if([string]$after.abcdOrchestration.eventType -ne 'iteration-round-started'){throw 'ABCD event metadata was not persisted.'}
    $replay=Add-ESTaskABCDOrchestrationEvent -ProjectRoot $f.root -StoreRoot 'state' -TaskId 'task-abc' -EventType 'iteration-round-started' -Payload $payload -TaskBindingId $b.binding.bindingId -TaskBindingHash $b.binding.bindingHash -RoutePlanHash $f.route.routePlanHash -SourceScopeHash $b.scopeHash -ExpectedTaskRevision 1 -ExpectedContextVersion 1 -IdempotencyKey 'abcd-event-1'
    if([int]$replay.taskRevision -ne [int]$after.taskRevision){throw 'ABCD event replay was not idempotent.'}
}

$failed=@($cases|Where-Object status -eq 'failed')
[pscustomobject][ordered]@{schemaVersion=1;validator='Test-ESAIABCTaskBinding';status=if($failed.Count){'failed'}else{'passed'};caseCount=$cases.Count;passedCount=@($cases|Where-Object status -eq 'passed').Count;failedCount=$failed.Count;cases=@($cases);runtimeStatus='static-task-api-event-store';claimsNotProven=@('Unity/Worker/host Runtime','joint StaticDeepReplay receipt freshness')}|ConvertTo-Json -Depth 12
