[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$WorkerPath,
    [string]$AdapterContractPath,
    [string]$AdapterSchemaPath,
    [string]$SchemaModulePath
)

$ErrorActionPreference = 'Stop'
$scriptRoot=$PSScriptRoot
if([string]::IsNullOrWhiteSpace($ProjectRoot)){$ProjectRoot=(Resolve-Path (Join-Path $scriptRoot '..\..\..')).Path}
if([string]::IsNullOrWhiteSpace($WorkerPath)){$WorkerPath=Join-Path $scriptRoot '..\Workers\PowerShell\Invoke-ESTaskContextEvaluationWorker.ps1'}
if([string]::IsNullOrWhiteSpace($AdapterContractPath)){$AdapterContractPath=Join-Path $scriptRoot '..\Contracts\es-task-context-evaluation-adapter-v1.json'}
if([string]::IsNullOrWhiteSpace($AdapterSchemaPath)){$AdapterSchemaPath=Join-Path $scriptRoot '..\Contracts\es-task-context-evaluation-adapter-v1.schema.json'}
if([string]::IsNullOrWhiteSpace($SchemaModulePath)){$SchemaModulePath=Join-Path $scriptRoot '..\Contracts\ESJsonSchemaLite.psm1'}
$strictUtf8 = [Text.UTF8Encoding]::new($false, $true)
Import-Module (Resolve-Path -LiteralPath $SchemaModulePath).Path -Force
. (Join-Path $PSScriptRoot 'Test-ESTaskContextRoutePlanFixture.ps1')
$results = [Collections.Generic.List[object]]::new()
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('es-task-context-eval-adapter-' + [Guid]::NewGuid().ToString('N'))
Initialize-ESTestRoutePlanRepository $testRoot

function Add-Case([string]$Name, [scriptblock]$Body) {
    try {
        & $Body
        [void]$results.Add([pscustomobject]@{ case = $Name; status = 'passed'; finding = $null })
    } catch {
        [void]$results.Add([pscustomobject]@{ case = $Name; status = 'failed'; finding = $_.Exception.GetBaseException().Message })
    }
}

function Assert-True([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Assert-Equal($Actual, $Expected, [string]$Message) { if ([string]$Actual -cne [string]$Expected) { throw "$Message Expected=$Expected Actual=$Actual" } }
function Read-Json([string]$Path) { return $strictUtf8.GetString([IO.File]::ReadAllBytes($Path)) | ConvertFrom-Json -ErrorAction Stop }
function Write-Json([string]$Path, $Value) { [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 50), [Text.UTF8Encoding]::new($false)) }
function Get-Hash([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }

function Copy-PlatformFixture([string]$Name) {
    $root = Join-Path $testRoot $Name
    $runtime = Join-Path $root 'ES/Automation/TaskContextRuntime'
    $contracts = Join-Path $root 'ES/Automation/Contracts'
    $routePlan = Join-Path $root 'ES/Automation/RoutePlan'
    $abcd = Join-Path $root 'ES/Automation/ABCD'
    $ai = Join-Path $root 'ES/Automation/AI'
    $evaluation = Join-Path $root 'ES/Automation/Evaluation'
    $staticReplayRunner = Join-Path $root '.agents/skills/es-static-deep-replay/scripts'
    $run = Join-Path $root 'ES/Automation/Runs/TaskContextEvaluation/11111111111111111111111111111111'
    New-Item -ItemType Directory -Path $runtime, $contracts, $routePlan, $abcd, $ai, $evaluation, $staticReplayRunner, $run -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $ProjectRoot 'ES/Automation/TaskContextRuntime/ESTaskContextRuntime.psm1') -Destination $runtime
    Copy-Item -LiteralPath (Join-Path $ProjectRoot 'ES/Automation/TaskContextRuntime/Invoke-ESTaskContextRuntime.ps1') -Destination $runtime
    Copy-Item -LiteralPath (Join-Path $ProjectRoot 'ES/Automation/RoutePlan/ESRoutePlanContract.psm1') -Destination $routePlan
    Copy-Item -LiteralPath (Join-Path $ProjectRoot 'ES/Automation/ABCD/ESABCDAuthorityKernel.psm1') -Destination $abcd
    Copy-Item -LiteralPath (Join-Path $ProjectRoot 'ES/Automation/AI/ESAuthorityDecisionPolicy.psm1') -Destination $ai
    Copy-Item -LiteralPath (Join-Path $ProjectRoot 'ES/Automation/Evaluation/ESTranscriptCorrectionObservation.psm1') -Destination $evaluation
    Copy-Item -LiteralPath (Join-Path $ProjectRoot '.agents/skills/es-static-deep-replay/scripts/Invoke-ESStaticDeepReplay.ps1') -Destination $staticReplayRunner
    foreach ($name in @(
        'es-task-context-runtime-v1.schema.json', 'es-platform-evidence-v1.schema.json',
        'es-goal-v1.schema.json', 'es-evidence-verifier.registry.json', 'es-task-transcript-slice-v1.schema.json',
        'es-outcome-evaluator.registry.json', 'es-evaluation-record-v1.schema.json',
        'es-route-plan-v1.schema.json', 'es-route-stage.registry.json',
        'es-route-stage-registry-v1.schema.json', 'es-authority-ai-decision-policy-v1.json', 'ESJsonSchemaLite.psm1'
    )) {
        Copy-Item -LiteralPath (Join-Path $ProjectRoot "ES/Automation/Contracts/$name") -Destination $contracts
    }
    [IO.File]::WriteAllText((Join-Path $root 'source.txt'), 'source', [Text.UTF8Encoding]::new($false))
    Import-Module (Join-Path $runtime 'ESTaskContextRuntime.psm1') -Force
    $goal = New-ESGoalRevision -ProjectRoot $root -StoreRoot 'ES/Output/TaskContextRuntime' -GoalId 'goal-adapter' -GoalRevision 'r1' -Scope @('source.txt') -AcceptanceIntent 'static source integrity' -Budget ([ordered]@{ maxReads = 8 })
    $routePlan = New-ESTestRoutePlan -Root $root -Goal $goal
    $state = New-ESTaskContextTask -ProjectRoot $root -StoreRoot 'ES/Output/TaskContextRuntime' -TaskId 'adapter-task' -PlanHash $routePlan.routePlanHash -RoutePlanPath $routePlan.path -GoalRevisionPath $goal.path -AcceptanceProfileId 'static' -OutcomeEvaluatorId 'platform.task-context-outcome-v1' -RequiredClaim 'source-integrity' -RequiredClaimVerifier ([ordered]@{ 'source-integrity' = 'platform.file-hash-manifest-v1' }) -RequestedSourceScope 'source.txt' -IdempotencyKey 'create'
    return [pscustomobject]@{ root = $root; runtime = $runtime; contracts = $contracts; routePlan = $routePlan; run = $run; state = $state }
}

function New-WorkerRequest($Fixture, [string]$RunId = '11111111111111111111111111111111') {
    $contract = Read-Json $AdapterContractPath
    return [ordered]@{
        protocolVersion = 1
        automationTaskId = 'es.task-context.evaluate'
        automationTaskVersion = 1
        runId = $RunId
        workerType = 'PowerShell'
        workerId = 'es.task-context.evaluate'
        workerVersion = '1.0.0'
        entrypointHash = [string]$contract.worker.entrypointHash
        operation = 'Evaluate'
        storeRoot = 'ES/Output/TaskContextRuntime'
        taskContextId = 'adapter-task'
        expectedTaskRevision = [int]$Fixture.state.taskRevision
        expectedContextVersion = [int]$Fixture.state.contextVersion
        idempotencyKey = 'evaluate-adapter'
        evaluationContractId = 'es://automation/contracts/evaluation-record/v1'
        evaluationContractHash = Get-Hash (Join-Path $Fixture.contracts 'es-evaluation-record-v1.schema.json')
        platformCliPath = 'ES/Automation/TaskContextRuntime/Invoke-ESTaskContextRuntime.ps1'
        platformCliHash = Get-Hash (Join-Path $Fixture.runtime 'Invoke-ESTaskContextRuntime.ps1')
        platformModulePath = 'ES/Automation/TaskContextRuntime/ESTaskContextRuntime.psm1'
        platformModuleHash = Get-Hash (Join-Path $Fixture.runtime 'ESTaskContextRuntime.psm1')
        outcomeEvaluatorRegistryPath = 'ES/Automation/Contracts/es-outcome-evaluator.registry.json'
        outcomeEvaluatorRegistryHash = Get-Hash (Join-Path $Fixture.contracts 'es-outcome-evaluator.registry.json')
    }
}

function Invoke-Worker($Fixture, $Request) {
    $requestPath = Join-Path $Fixture.run 'request.json'
    Write-Json $requestPath $Request
    $powershell = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::System)) 'WindowsPowerShell/v1.0/powershell.exe'
    $arguments = @('-NoLogo', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $WorkerPath, '-InputPath', $requestPath, '-OutputDirectory', $Fixture.run, '-ProjectRoot', $Fixture.root)
    $process = Start-Process -FilePath $powershell -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
    return [pscustomobject]@{ exitCode = $process.ExitCode; result = Read-Json (Join-Path $Fixture.run 'result.json') }
}

function Invoke-WorkerAtOutput($Fixture, $Request, [string]$OutputDirectory) {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $requestPath = Join-Path $OutputDirectory 'request.json'
    Write-Json $requestPath $Request
    $powershell = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::System)) 'WindowsPowerShell/v1.0/powershell.exe'
    $arguments = @('-NoLogo', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $WorkerPath, '-InputPath', $requestPath, '-OutputDirectory', $OutputDirectory, '-ProjectRoot', $Fixture.root)
    $process = Start-Process -FilePath $powershell -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
    return [pscustomobject]@{ exitCode = $process.ExitCode; resultExists = Test-Path -LiteralPath (Join-Path $OutputDirectory 'result.json') }
}

try {
    Add-Case 'adapter-contract-binds-current-worker-and-input-schema' {
        $contract = Read-Json $AdapterContractPath
        $schemaErrors = @(Test-ESJsonSchemaValue -SchemaPath $AdapterSchemaPath -Value $contract)
        Assert-Equal $schemaErrors.Count 0 ($schemaErrors -join '; ')
        Assert-Equal $contract.adapterId 'es.task-context.evaluate@1' 'adapterId'
        Assert-Equal $contract.inputContract.sha256 (Get-Hash (Join-Path $ProjectRoot $contract.inputContract.path)) 'input schema hash'
        Assert-Equal $contract.worker.entrypointHash (Get-Hash (Join-Path $ProjectRoot $contract.worker.entrypoint)) 'worker hash'
        Assert-True ($contract.integration.sourceRegistrationIntegrated -eq $true) 'Source registration integration flag is not true.'
        Assert-True ($contract.integration.unityRuntimeRegistrationVerified -eq $false) 'Unity Runtime registration was claimed without evidence.'
        Assert-True ($contract.integration.productionExecutionObserved -eq $false) 'Production execution was claimed without evidence.'
        Assert-True ($contract.integration.globalP0Integrated -eq $false) 'Global P0 was enabled by the adapter.'
    }

    Add-Case 'adapter-contract-rejects-runtime-overclaim-and-legacy-ambiguous-flag' {
        $contract = Read-Json $AdapterContractPath
        $contract.integration.unityRuntimeRegistrationVerified = $true
        $contract.integration | Add-Member -NotePropertyName productionRouteIntegrated -NotePropertyValue $true
        $schemaErrors = @(Test-ESJsonSchemaValue -SchemaPath $AdapterSchemaPath -Value $contract)
        Assert-True ($schemaErrors.Count -gt 0) 'Adapter schema accepted a Runtime overclaim or ambiguous productionRouteIntegrated flag.'
    }

    Add-Case 'managed-worker-produces-scoped-non-mutating-evaluation' {
        $fixture = Copy-PlatformFixture 'positive'
        $before = $fixture.state
        $run = Invoke-Worker $fixture (New-WorkerRequest $fixture)
        Assert-True ($run.exitCode -eq 0) ("worker exitCode Expected=0 Actual=$($run.exitCode) Errors=$(@($run.result.errors) -join ' | ')")
        Assert-Equal $run.result.status 'Passed' 'Automation result status'
        Assert-True ($null -eq $run.result.completionDecision) 'Worker projected a TaskContext evaluation into Automation CompletionDecision.'
        $evaluation = Read-Json (Join-Path $fixture.run 'evaluation-record.json')
        Assert-Equal $evaluation.recordType 'EvaluationRecord' 'recordType'
        Assert-Equal $evaluation.decisionScope 'task-object' 'decisionScope'
        $after = Get-ESTaskContextState -ProjectRoot $fixture.root -StoreRoot 'ES/Output/TaskContextRuntime' -TaskId 'adapter-task' -VerifyIntegrity
        Assert-Equal $after.taskRevision $before.taskRevision 'TaskRevision changed'
        Assert-Equal $after.contextVersion $before.contextVersion 'ContextVersion changed'
        Assert-Equal $after.taskStatus $before.taskStatus 'TaskStatus changed'
        Assert-Equal $after.contextStatus $before.contextStatus 'ContextStatus changed'
    }

    Add-Case 'cross-contract-field-is-rejected' {
        $fixture = Copy-PlatformFixture 'cross-contract'
        $request = New-WorkerRequest $fixture
        $request.governanceHash = 'b' * 64
        $run = Invoke-Worker $fixture $request
        Assert-Equal $run.exitCode 1 'worker exitCode'
        Assert-Equal $run.result.status 'Failed' 'Automation result status'
        Assert-True (@($run.result.errors | Where-Object { $_ -like '*unsupported property: governanceHash*' }).Count -eq 1) 'governanceHash projection was not rejected.'
    }

    Add-Case 'platform-artifact-hash-mismatch-is-rejected' {
        $fixture = Copy-PlatformFixture 'hash-mismatch'
        $request = New-WorkerRequest $fixture
        $request.platformModuleHash = '0' * 64
        $run = Invoke-Worker $fixture $request
        Assert-Equal $run.exitCode 1 'worker exitCode'
        Assert-Equal $run.result.status 'Failed' 'Automation result status'
        Assert-True (@($run.result.errors | Where-Object { $_ -like '*platformModuleHash does not match*' }).Count -eq 1) ("Platform module drift was not rejected. Errors=$(@($run.result.errors) -join ' | ')")
    }

    Add-Case 'task-context-cas-mismatch-is-rejected-without-evaluation-record' {
        $fixture = Copy-PlatformFixture 'cas-mismatch'
        $request = New-WorkerRequest $fixture
        $request.expectedTaskRevision = [int]$request.expectedTaskRevision + 1
        $run = Invoke-Worker $fixture $request
        Assert-Equal $run.exitCode 1 'worker exitCode'
        Assert-Equal $run.result.status 'Failed' 'Automation result status'
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $fixture.run 'evaluation-record.json'))) 'CAS mismatch produced an EvaluationRecord.'
    }

    Add-Case 'automation-completion-decision-injection-is-rejected' {
        $fixture = Copy-PlatformFixture 'completion-injection'
        $request = New-WorkerRequest $fixture
        $request.completionDecision = [ordered]@{ status = 'Accepted' }
        $run = Invoke-Worker $fixture $request
        Assert-Equal $run.exitCode 1 'worker exitCode'
        Assert-Equal $run.result.status 'Failed' 'Automation result status'
        Assert-True (@($run.result.errors | Where-Object { $_ -like '*unsupported property: completionDecision*' }).Count -eq 1) 'Automation CompletionDecision injection was not rejected.'
    }

    Add-Case 'worker-output-scope-expansion-is-rejected' {
        $fixture = Copy-PlatformFixture 'output-scope'
        $request = New-WorkerRequest $fixture
        $expanded = Join-Path $fixture.root 'ES/Output/Other/11111111111111111111111111111111'
        $run = Invoke-WorkerAtOutput $fixture $request $expanded
        Assert-Equal $run.exitCode 1 'worker exitCode'
        Assert-True (-not $run.resultExists) 'Out-of-scope worker invocation produced a trusted result.'
    }

    Add-Case 'worker-nested-run-directory-is-rejected' {
        $fixture = Copy-PlatformFixture 'nested-output-scope'
        $request = New-WorkerRequest $fixture
        $expanded = Join-Path $fixture.root 'ES/Automation/Runs/TaskContextEvaluation/nested/11111111111111111111111111111111'
        $run = Invoke-WorkerAtOutput $fixture $request $expanded
        Assert-Equal $run.exitCode 1 'worker exitCode'
        Assert-True (-not $run.resultExists) 'Nested run directory produced a trusted result.'
    }

    Add-Case 'worker-entrypoint-identity-mismatch-is-rejected' {
        $fixture = Copy-PlatformFixture 'entrypoint-identity'
        $request = New-WorkerRequest $fixture
        $request.entrypointHash = '0' * 64
        $run = Invoke-Worker $fixture $request
        Assert-Equal $run.exitCode 1 'worker exitCode'
        Assert-Equal $run.result.status 'Failed' 'Automation result status'
        Assert-True (@($run.result.errors | Where-Object { $_ -like '*entrypointHash does not match*' }).Count -eq 1) 'Executing Worker identity drift was not rejected.'
    }

    Add-Case 'csharp-registration-preserves-contract-isolation' {
        $bridgePath = Join-Path $ProjectRoot 'Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs'
        $brainPath = Join-Path $ProjectRoot 'Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs'
        $bridge = $strictUtf8.GetString([IO.File]::ReadAllBytes($bridgePath))
        $brain = $strictUtf8.GetString([IO.File]::ReadAllBytes($brainPath))
        foreach ($required in @(
            'internal const string TaskId = "es.task-context.evaluate"',
            'result.completionDecision != null',
            'Automation Accepted/Completed is not TaskContext evaluation accepted',
            '["globalP0Integrated"] = false',
            '["unityRuntimeRegistrationVerified"] = false',
            'ESTaskContextEvaluationAutomation.Register()',
            'TASK_CONTEXT_EVAL_REGISTRATION_ISOLATED'
        )) { Assert-True $bridge.Contains($required) "Missing C# boundary: $required" }
        $registerIndex = $bridge.IndexOf('ESTaskContextEvaluationAutomation.Register()', [StringComparison]::Ordinal)
        $catchIndex = $bridge.IndexOf('catch (Exception exception)', $registerIndex, [StringComparison]::Ordinal)
        Assert-True ($registerIndex -ge 0 -and $catchIndex -gt $registerIndex) 'TaskContext /eval registration failure is not isolated.'
        Assert-True ($brain.Contains('case "es.task-context.evaluate":') -and $brain.Contains('expectedCommandId = "task.context-runtime.mutate";')) 'AIBrain command/task binding is missing.'
    }

    Add-Case 'csharp-managed-result-forgery-and-recovery-boundaries-exist' {
        $bridgePath = Join-Path $ProjectRoot 'Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs'
        $bridge = $strictUtf8.GetString([IO.File]::ReadAllBytes($bridgePath))
        foreach ($required in @(
            'if (!string.Equals(existing.invocationHash, invocationHash',
            'result.inputManifestHash, active.Record.inputManifestHash',
            'if (result.completionDecision != null)',
            'ESAutomationPathPolicy.IsWithin(output, new[] { active.Directory })',
            'active.Execution.EnforceTimeout(DateTimeOffset.UtcNow)',
            'active.Execution.Terminate()'
        )) { Assert-True $bridge.Contains($required) "Missing managed negative boundary: $required" }
    }

    Add-Case 'path-policy-allows-only-task-context-store-under-es-output' {
        $pathPolicyPath = Join-Path $ProjectRoot 'Assets/Plugins/ES/Editor/ESAutomation/ESAutomationCenter.cs'
        $source = $strictUtf8.GetString([IO.File]::ReadAllBytes($pathPolicyPath))
        $start = $source.IndexOf('public static void EnsureDeclaredWriteRoot', [StringComparison]::Ordinal)
        $end = $source.IndexOf('private static IEnumerable<string> ProtectedWriteRoots', $start, [StringComparison]::Ordinal)
        Assert-True ($start -ge 0 -and $end -gt $start) 'EnsureDeclaredWriteRoot source boundary is missing.'
        $block = $source.Substring($start, $end - $start)
        Assert-True ($block.Contains('Path.Combine(ProjectRoot, "ES", "Output", "TaskContextRuntime")')) 'TaskContextRuntime StoreRoot is not registered.'
        Assert-True (-not $block.Contains('Path.Combine(ProjectRoot, "ES", "Output")')) 'Broad ES/Output write root was registered.'
    }

    Add-Case 'ai-request-schema-covers-current-bridge-actions' {
        $schema = Read-Json (Join-Path $ProjectRoot 'ES/Automation/Contracts/es-automation-ai-request.schema.json')
        $actual = @($schema.properties.action.enum | ForEach-Object { [string]$_ })
        foreach ($action in @('listTasks', 'listCapabilities', 'runKnowledgeRouteProbes', 'getFailureTelemetry', 'planTask', 'runTask', 'getRun', 'cancelRun', 'submitInput', 'submitContentProposal', 'getUnityCompilationState', 'setUnityAutoCompilation', 'triggerUnityCompilation', 'modifyActiveScene')) {
            Assert-True ($actual -ccontains $action) "AI request schema omits current action: $action"
        }
        Assert-True ($actual -cnotcontains 'evaluateTaskContext') 'A privileged direct /eval action bypassed planTask/runTask.'
    }
} finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
}

$failed = @($results | Where-Object { $_.status -eq 'failed' })
[pscustomobject][ordered]@{
    schemaVersion = 1
    validator = 'Test-ESTaskContextEvaluationAdapter'
    status = if ($failed.Count) { 'failed' } else { 'passed' }
    caseCount = $results.Count
    passedCount = @($results | Where-Object { $_.status -eq 'passed' }).Count
    failedCount = $failed.Count
    cases = @($results)
    runtimeStatus = 'runtime-not-run'
    claimsNotProven = @('Unity Editor registration/runtime execution', 'Release acceptance', 'global P0 integration')
} | ConvertTo-Json -Depth 12
if ($failed.Count) { exit 1 }
