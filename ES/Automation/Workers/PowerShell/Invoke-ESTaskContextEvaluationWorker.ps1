[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,
    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot
)

$ErrorActionPreference = 'Stop'
$strictUtf8 = [Text.UTF8Encoding]::new($false, $true)
$startedUtc = [DateTime]::UtcNow
$request = $null
$resultPath = $null

function Assert-SafeRelativePath([string]$Value, [string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Value) -or [IO.Path]::IsPathRooted($Value) -or $Value -match '(^|[\\/])\.\.([\\/]|$)') {
        throw "$Name must be a safe project-relative path."
    }
}

function Resolve-ProjectPath([string]$Root, [string]$RelativePath, [string]$Name, [bool]$RequireLeaf) {
    Assert-SafeRelativePath $RelativePath $Name
    $full = [IO.Path]::GetFullPath((Join-Path $Root $RelativePath))
    if (-not $full.StartsWith($Root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Name escapes ProjectRoot."
    }
    if ($RequireLeaf -and -not (Test-Path -LiteralPath $full -PathType Leaf)) {
        throw "$Name is missing."
    }
    $relative = $full.Substring($Root.Length).TrimStart([char]'\', [char]'/')
    $current = $Root
    foreach ($segment in $relative.Split(@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar), [StringSplitOptions]::RemoveEmptyEntries)) {
        $current = Join-Path $current $segment
        if (-not (Test-Path -LiteralPath $current)) { break }
        $item = Get-Item -LiteralPath $current -Force
        if ($item.LinkType -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "$Name cannot traverse a reparse point."
        }
    }
    return $full
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Assert-ExactShape($Value, [string[]]$Required, [string]$Name) {
    $actual = @($Value.PSObject.Properties | ForEach-Object { [string]$_.Name })
    foreach ($field in $Required) {
        if ($actual -cnotcontains $field) { throw "$Name is missing required property: $field" }
    }
    foreach ($field in $actual) {
        if ($Required -cnotcontains $field) { throw "$Name contains an unsupported property: $field" }
    }
}

function Write-CreateOnlyJson([string]$Path, $Value) {
    $json = $Value | ConvertTo-Json -Depth 50
    $bytes = $strictUtf8.GetBytes($json)
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    try { $stream.Write($bytes, 0, $bytes.Length) } finally { $stream.Dispose() }
}

function New-RunResult([string]$Status, [int]$ExitCode, [string[]]$Outputs, [string[]]$Findings, [string[]]$Errors) {
    $outputHashes = @($Outputs | ForEach-Object { Get-Sha256 (Join-Path $OutputDirectory $_) })
    return [ordered]@{
        protocolVersion = 1
        taskId = [string]$request.automationTaskId
        taskVersion = [int]$request.automationTaskVersion
        runId = [string]$request.runId
        workerType = [string]$request.workerType
        workerId = [string]$request.workerId
        workerVersion = [string]$request.workerVersion
        entrypointHash = [string]$request.entrypointHash
        status = $Status
        exitCode = $ExitCode
        retryCount = 0
        startedAtUtc = $startedUtc.ToUniversalTime().ToString('o')
        finishedAtUtc = [DateTime]::UtcNow.ToUniversalTime().ToString('o')
        inputManifestHash = Get-Sha256 $InputPath
        outputs = @($Outputs)
        outputHashes = @($outputHashes)
        findings = @($Findings)
        errors = @($Errors)
        idempotencyKey = [string]$request.idempotencyKey
        executionSnapshot = $null
        completionDecision = $null
        traceReconciliation = $null
    }
}

try {
    $root = (Resolve-Path -LiteralPath $ProjectRoot).Path
    $inputFull = [IO.Path]::GetFullPath($InputPath)
    $outputFull = [IO.Path]::GetFullPath($OutputDirectory)
    if (-not $inputFull.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'InputPath escapes ProjectRoot.' }
    if (-not $outputFull.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'OutputDirectory escapes ProjectRoot.' }
    $inputRelative = $inputFull.Substring($root.Length).TrimStart([char]'\', [char]'/')
    $outputRelative = $outputFull.Substring($root.Length).TrimStart([char]'\', [char]'/')
    $inputFull = Resolve-ProjectPath $root $inputRelative 'InputPath' $true
    $outputFull = Resolve-ProjectPath $root $outputRelative 'OutputDirectory' $false
    $registeredRunsRoot = [IO.Path]::GetFullPath((Join-Path $root 'ES/Automation/Runs/TaskContextEvaluation'))
    if (-not $outputFull.StartsWith($registeredRunsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'OutputDirectory is outside the registered TaskContextEvaluation run root.'
    }
    if (-not [string]::Equals((Split-Path -Parent $outputFull), $registeredRunsRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'OutputDirectory must be a direct runId child of the registered TaskContextEvaluation root.'
    }
    $runDirectoryName = Split-Path -Leaf $outputFull
    if ($runDirectoryName -cnotmatch '^[a-f0-9]{32}$') { throw 'OutputDirectory must end with the registered N-format runId.' }
    if (-not [string]::Equals($inputFull, (Join-Path $outputFull 'request.json'), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'InputPath must be the request.json inside its registered run directory.'
    }
    if (-not (Test-Path -LiteralPath $inputFull -PathType Leaf) -or -not (Test-Path -LiteralPath $outputFull -PathType Container)) { throw 'Worker input or output directory is missing.' }
    $InputPath = $inputFull
    $OutputDirectory = $outputFull
    $resultPath = Join-Path $OutputDirectory 'result.json'

    $request = $strictUtf8.GetString([IO.File]::ReadAllBytes($InputPath)) | ConvertFrom-Json -ErrorAction Stop
    $required = @(
        'protocolVersion', 'automationTaskId', 'automationTaskVersion', 'runId',
        'workerType', 'workerId', 'workerVersion', 'entrypointHash', 'operation',
        'storeRoot', 'taskContextId', 'expectedTaskRevision', 'expectedContextVersion',
        'idempotencyKey', 'evaluationContractId', 'evaluationContractHash',
        'platformCliPath', 'platformCliHash', 'platformModulePath', 'platformModuleHash',
        'outcomeEvaluatorRegistryPath', 'outcomeEvaluatorRegistryHash'
    )
    Assert-ExactShape $request $required 'TaskContextEvaluationWorkerRequest'
    if ([int]$request.protocolVersion -ne 1 -or [string]$request.operation -cne 'Evaluate') { throw 'Worker request identity is invalid.' }
    if ([string]$request.automationTaskId -cne 'es.task-context.evaluate' -or [int]$request.automationTaskVersion -ne 1) { throw 'Automation task binding is invalid.' }
    if ([string]$request.runId -cne $runDirectoryName) { throw 'Worker request runId does not match OutputDirectory.' }
    if ([string]$request.workerType -cne 'PowerShell' -or [string]$request.workerId -cne 'es.task-context.evaluate' -or [string]$request.workerVersion -cne '1.0.0') { throw 'Worker identity is invalid.' }
    if ([string]$request.entrypointHash -cne (Get-Sha256 $PSCommandPath)) { throw 'Worker entrypointHash does not match the executing worker.' }
    if ([string]$request.storeRoot -cne 'ES/Output/TaskContextRuntime') { throw 'TaskContext StoreRoot is not the registered single scope.' }
    if ([string]$request.taskContextId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,80}$') { throw 'TaskContextId is invalid.' }
    if ([string]$request.idempotencyKey -notmatch '^[A-Za-z0-9][A-Za-z0-9._:-]{0,159}$') { throw 'IdempotencyKey is invalid.' }
    if ([int]$request.expectedTaskRevision -lt 1 -or [int]$request.expectedContextVersion -lt 1) { throw 'Expected revisions must be positive.' }

    $cliPath = Resolve-ProjectPath $root ([string]$request.platformCliPath) 'platformCliPath' $true
    $modulePath = Resolve-ProjectPath $root ([string]$request.platformModulePath) 'platformModulePath' $true
    $registryPath = Resolve-ProjectPath $root ([string]$request.outcomeEvaluatorRegistryPath) 'outcomeEvaluatorRegistryPath' $true
    foreach ($binding in @(
        @($cliPath, [string]$request.platformCliHash, 'platformCliHash'),
        @($modulePath, [string]$request.platformModuleHash, 'platformModuleHash'),
        @($registryPath, [string]$request.outcomeEvaluatorRegistryHash, 'outcomeEvaluatorRegistryHash')
    )) {
        if ([string]$binding[1] -notmatch '^[a-f0-9]{64}$' -or (Get-Sha256 $binding[0]) -cne [string]$binding[1]) {
            throw "$($binding[2]) does not match the current platform artifact."
        }
    }

    $evaluationRequest = [ordered]@{
        schemaVersion = 1
        contractId = [string]$request.evaluationContractId
        contractHash = [string]$request.evaluationContractHash
        recordType = 'EvaluationRequest'
        storeRoot = [string]$request.storeRoot
        taskId = [string]$request.taskContextId
        expectedTaskRevision = [int]$request.expectedTaskRevision
        expectedContextVersion = [int]$request.expectedContextVersion
        idempotencyKey = [string]$request.idempotencyKey
    }
    $evaluationRequestPath = Join-Path $OutputDirectory 'evaluation-request.json'
    Write-CreateOnlyJson $evaluationRequestPath $evaluationRequest
    $evaluationRequestRelative = $evaluationRequestPath.Substring($root.Length).TrimStart([char]'\', [char]'/')
    $rawEvaluation = & $cliPath -Action Evaluate -InputPath $evaluationRequestRelative -ProjectRoot $root
    if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "TaskContext /eval exited with code $LASTEXITCODE." }
    $evaluation = ($rawEvaluation -join [Environment]::NewLine) | ConvertFrom-Json -ErrorAction Stop
    if ([string]$evaluation.recordType -cne 'EvaluationRecord' -or [string]$evaluation.taskId -cne [string]$request.taskContextId -or [string]$evaluation.decisionScope -cne 'task-object') {
        throw 'TaskContext /eval returned an invalid or expanded EvaluationRecord.'
    }
    $evaluationPath = Join-Path $OutputDirectory 'evaluation-record.json'
    Write-CreateOnlyJson $evaluationPath $evaluation
    $result = New-RunResult 'Passed' 0 @('evaluation-record.json') @(
        "evaluationId=$([string]$evaluation.evaluationId)",
        "decision=$([string]$evaluation.decision)",
        "evidenceState=$([string]$evaluation.evidenceState)",
        'automation-status-does-not-project-to-task-context-decision'
    ) @()
    Write-CreateOnlyJson $resultPath $result
    exit 0
}
catch {
    $message = $_.Exception.GetBaseException().Message
    if ($null -ne $request -and -not [string]::IsNullOrWhiteSpace($resultPath) -and -not (Test-Path -LiteralPath $resultPath)) {
        try {
            $result = New-RunResult 'Failed' 1 @() @('task-context-evaluation-worker-failed') @($message)
            Write-CreateOnlyJson $resultPath $result
        } catch { }
    }
    [Console]::Error.WriteLine($message)
    exit 1
}
