[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [switch]$Json
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)

$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path.TrimEnd('\', '/')
$utf8Strict = [Text.UTF8Encoding]::new($false, $true)
$workerRelative = 'ES/Automation/Workers/PowerShell/Invoke-ESCodexAppServerWorker.ps1'
$schemaRelative = 'ES/Automation/Contracts/es-codex-app-server-v1.schema.json'
$candidateEnvelopeSchemaRelative = 'ES/Automation/Contracts/es-codex-candidate-envelope-v1.schema.json'
$declarationRelative = 'ES/Automation/Contracts/es-codex-app-server-integration-declaration-v1.json'
$adapterRelative = 'Assets/Plugins/ES/Editor/ESAutomation/ESCodexAppServerAutomation.cs'
$centerRelative = 'Assets/Plugins/ES/Editor/ESAutomation/ESAutomationCenter.cs'
$workerPath = Join-Path $root $workerRelative
$schemaPath = Join-Path $root $schemaRelative
$candidateEnvelopeSchemaPath = Join-Path $root $candidateEnvelopeSchemaRelative
$declarationPath = Join-Path $root $declarationRelative
$adapterPath = Join-Path $root $adapterRelative
$centerPath = Join-Path $root $centerRelative
$adapterMetaPath = $adapterPath + '.meta'
$commandMetaPath = @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets/Plugins/ES/AICommands') -Filter 'Codex*.md.meta' -File | Select-Object -First 1).FullName

function Read-Strict([string]$Path) {
    return $utf8Strict.GetString([IO.File]::ReadAllBytes($Path))
}

function Hash-File([string]$Path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($Path)))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

$cases = New-Object 'Collections.Generic.List[object]'
function Add-Case([string]$Id, [bool]$Passed, [string]$Finding) {
    [void]$cases.Add([pscustomobject]@{
        case = $Id
        status = if ($Passed) { 'passed' } else { 'failed' }
        finding = if ($Passed) { '' } else { $Finding }
    })
}

foreach ($path in @($workerPath, $schemaPath, $candidateEnvelopeSchemaPath, $declarationPath, $adapterPath)) {
    Add-Case ('file-present:' + $path.Substring($root.Length + 1)) (Test-Path -LiteralPath $path -PathType Leaf) 'Required Codex contract file is missing.'
}
Add-Case 'unity-script-meta-present' (Test-Path -LiteralPath $adapterMetaPath -PathType Leaf) 'Codex Editor source is missing its Unity .meta identity file.'
Add-Case 'ai-command-meta-present' (Test-Path -LiteralPath $commandMetaPath -PathType Leaf) 'Codex AICommand source is missing its Unity .meta identity file.'
Add-Case 'editor-discoverability-file-present' (Test-Path -LiteralPath $centerPath -PathType Leaf) 'ES Automation Center source is missing.'

$worker = Read-Strict $workerPath
$adapter = Read-Strict $adapterPath
$center = Read-Strict $centerPath
$schema = Read-Strict $schemaPath | ConvertFrom-Json
$declaration = Read-Strict $declarationPath | ConvertFrom-Json

try {
    [System.Management.Automation.Language.Parser]::ParseFile($workerPath, [ref]$null, [ref]$null) | Out-Null
    Add-Case 'worker-powershell-parse' $true ''
}
catch { Add-Case 'worker-powershell-parse' $false $_.Exception.Message }

$workerHash = Hash-File $workerPath
$schemaHash = Hash-File $schemaPath
$candidateEnvelopeSchemaHash = Hash-File $candidateEnvelopeSchemaPath
$workerHashMatch = [Regex]::IsMatch($adapter, [Regex]::Escape('WorkerEntrypointHash = "' + $workerHash + '"'))
$schemaHashMatch = [Regex]::IsMatch($adapter, [Regex]::Escape('InputSchemaHash = "' + $schemaHash + '"'))
Add-Case 'worker-entrypoint-hash' $workerHashMatch "Adapter is not pinned to current Worker SHA-256: $workerHash"
Add-Case 'input-schema-hash' $schemaHashMatch "Adapter is not pinned to current Schema SHA-256: $schemaHash"
$candidateEnvelopeSchemaHashMatch = [Regex]::IsMatch($adapter, [Regex]::Escape('CandidateEnvelopeSchemaHash = "' + $candidateEnvelopeSchemaHash + '"'))
Add-Case 'candidate-envelope-schema-hash' $candidateEnvelopeSchemaHashMatch "Adapter is not pinned to current CandidateEnvelope Schema SHA-256: $candidateEnvelopeSchemaHash"

Add-Case 'schema-identity' (
    $schema.properties.providerDeclaration.const -eq 'es-codex' -and
    $schema.properties.workerId.const -eq 'es.codex.app-server' -and
    $schema.properties.taskId.const -eq 'es.codex.app-server') 'Schema identity is not fixed to es-codex.'
Add-Case 'declaration-identity' (
    $declaration.providerDeclaration -eq 'es-codex' -and
    $declaration.frameworkId -eq 'codex-app-server' -and
    $declaration.taskId -eq 'es.codex.app-server' -and
    $declaration.authority -eq 'ESFramework/ESAI' -and
    $declaration.authorityLevel -eq 'candidate-contributor-not-final-acceptance') 'Declaration identity or authority boundary is invalid.'
Add-Case 'schema-identity-binding' (
    @('brainPlanHash', 'commandId', 'commandHash', 'taskContractHash', 'invocationId' | Where-Object { $_ -in @($schema.required) }).Count -eq 5 -and
    $schema.properties.commandId.const -eq 'codex.appserver.execute') 'Schema does not require the ES plan/command/TaskContract identity binding.'
Add-Case 'fixed-launcher' (
    $worker.Contains('Only the fixed codex.cmd launcher is accepted.') -and
    $worker.Contains('app-server --stdio') -and
    $worker.Contains('shell metacharacters')) 'Codex launcher is not fixed and shell-bounded.'
Add-Case 'launcher-ancestor-reparse-boundary' (
    $worker.Contains('Assert-NoReparseAncestors') -and
    $worker.Contains('reparse point in its launcher path')) 'Codex launcher parent traversal is not reparse-safe.'
Add-Case 'provider-network-evidence' (
    $worker.Contains('networkCalled = $false') -and
    $worker.Contains('$result.networkCalled = $true') -and
    $worker.Contains('provider-facing request')) 'Provider-facing Codex operations do not expose bounded network-attempt evidence.'
Add-Case 'run-root-containment' (
    $worker.Contains("Assert-Within `$InputPath") -and
    $worker.Contains("Assert-Within `$OutputDirectory")) 'Input or output is not constrained to the managed Run root.'
Add-Case 'reparse-point-boundary' (
    $worker.Contains('Assert-NoReparseTraversal') -and
    $worker.Contains('ancestorItem') -and
    $worker.Contains('at the managed root or its parent') -and
    $worker.Contains('cannot traverse a reparse point') -and
    $worker.Contains("ResultPath already exists; refusing to overwrite")) 'Run root traversal does not reject reparse points.'
Add-Case 'adapter-reparse-point-boundary' (
    $adapter.Contains('EnsureWorkerDirectory(directory, new[] { RunsRoot })') -and
    $adapter.Contains('ContainsExistingReparsePoint(active.ResultPath)') -and
    $adapter.Contains('ContainsExistingReparsePoint(recordPath)')) 'Adapter does not re-check managed Run paths before writing or reading.'
Add-Case 'authority-source-reparse-boundary' (
    $adapter.Contains('ContainsExistingReparsePoint(WorkerPath)') -and
    $adapter.Contains('ContainsExistingReparsePoint(SchemaPath)') -and
    $adapter.Contains('RequiredAuthorityReadsPresent()')) 'Authority source files are not rejected when they cross a reparse point.'
Add-Case 'low-level-adapter-authority-gate' (
    $adapter.Contains('public ProcessStartInfo CreateStartInfo') -and
    $adapter.Contains('VerifyBindings();') -and
    $adapter.Contains('RequiredAuthorityReadsPresent()')) 'Low-level ProcessRunner adapter startup can bypass the ES authority-read gate.'
Add-Case 'bounded-redacted-output' (
    $worker.Contains('function Redact-Text') -and
    $worker.Contains('OPENAI_API_KEY') -and
    $worker.Contains('DEEPSEEK_API_KEY') -and
    $worker.Contains('CreateNew')) 'Codex output is not redacted and atomically created within the run root.'
Add-Case 'output-byte-budget' (
    $worker.Contains('codexEventBytes') -and
    $worker.Contains('800000') -and
    $worker.Contains('$script:codexEventBytes')) 'Codex event output is not bounded below the ES TaskContract byte budget.'
Add-Case 'stderr-drain' (
    $worker.Contains('StandardError.ReadToEndAsync') -and
    $worker.Contains('Codex stderr:') -and
    $worker.Contains('8192')) 'Codex stderr is not drained and bounded before the Worker writes its result.'
Add-Case 'read-only-and-approval-boundary' (
    $worker.Contains("type = 'readOnly'") -and
    $worker.Contains("approvalPolicy = 'never'") -and
    $worker.Contains('permission or approval') -and
    $worker.Contains("status = 'Blocked'")) 'Read-only or fail-closed approval boundary is missing.'
Add-Case 'protocol-sandbox-field-shape' (
    $worker.Contains('$threadParams.sandbox = ''read-only''') -and
    $worker.Contains('sandboxPolicy = [ordered]@{ type = ''readOnly''')) 'thread/start must use the protocol sandbox mode while turn/start uses the structured sandboxPolicy.'
Add-Case 'codex-wire-envelope' (
    -not $worker.Contains("jsonrpc = '2.0'") -and
    $worker.Contains("method = 'initialize'") -and
    $worker.Contains("method = 'initialized'")) 'Codex App Server wire messages must use its frameless JSON-RPC dialect.'
Add-Case 'hard-deadlines' (
    $worker.Contains('initialize handshake timed out') -and
    $worker.Contains('thread/turn timed out') -and
    $worker.Contains('if ($remaining -le 0)')) 'Codex handshake or turn loop has no hard deadline.'
Add-Case 'check-local-capability' (
    $worker.Contains("method = 'thread/loaded/list'") -and
    $worker.Contains('if ($message.id -eq 2) { $result.status = ''Passed''; $result.exitCode = 0; break }')) 'check-local does not exercise the stable thread capability after initialization.'
Add-Case 'resume-identity-match' (
    $worker.Contains('returned a different thread identity') -and
    $worker.Contains('Equals($result.threadId, [string]$requestThreadId')) 'thread/resume result is not checked against the requested exact thread identity.'
Add-Case 'exact-thread-resume' (
    $worker.Contains("'thread/resume'") -and
    $worker.Contains('turn requires an exact threadId')) 'Exact thread resume is not enforced.'
Add-Case 'preserves-codex-turn-terminal-status' (
    $worker.Contains('$result.status = ''Cancelled''; $result.exitCode = 20') -and
    $worker.Contains('$result.status = ''Failed''; $result.exitCode = 1') -and
    $worker.Contains('message.params.turn.error.message')) 'Codex turn interrupted/failed states are collapsed instead of preserved.'
Add-Case 'jsonrpc-handshake-and-order' (
    $worker.IndexOf("method = 'initialize'") -ge 0 -and
    $worker.IndexOf("method = 'initialized'") -gt $worker.IndexOf("method = 'initialize'") -and
    $worker.IndexOf("'thread/start'") -gt $worker.IndexOf("method = 'initialized'") -and
    $worker.IndexOf("'thread/resume'") -gt $worker.IndexOf("method = 'initialized'") -and
    $worker.IndexOf("method = 'turn/start'") -gt $worker.IndexOf("'thread/start'") -and
    $worker.IndexOf("method = 'turn/start'") -gt $worker.IndexOf("'thread/resume'")) 'Codex JSON-RPC handshake or thread/turn ordering is not statically bounded.'
Add-Case 'es-process-owner' (
    $adapter.Contains('ESAutomationProcessRunner.Start') -and
    -not $adapter.Contains('Process.Start(')) 'Adapter bypasses the ES ProcessRunner or starts a process directly.'
Add-Case 'provider-process-identity' (
    $worker.Contains('codexProcessId = 0') -and
    $worker.Contains('$result.codexProcessId = $process.Id') -and
    $adapter.Contains('result["codexProcessId"]')) 'RunRecord does not preserve the provider launcher process identity separately from the Worker process.'
Add-Case 'candidate-only-authority' (
    $adapter.Contains('candidate-only-not-final-acceptance') -and
    $adapter.Contains('candidate-only / orchestration status only')) 'Codex candidate-only authority boundary is not visible.'
Add-Case 'preserves-terminal-status' (
    $adapter.Contains('status = record.status') -and
    $adapter.Contains('record.status != ESAutomationRunStatus.Failed') -and
    $adapter.Contains('record.status != ESAutomationRunStatus.Cancelled') -and
    $adapter.Contains('record.status != ESAutomationRunStatus.Blocked') -and
    $adapter.Contains('record.status != ESAutomationRunStatus.TimedOut')) 'RunRecord terminal status is not preserved through the adapter.'
Add-Case 'no-final-completion-decision' (
    $adapter.Contains('completionDecision = null') -and
    $adapter.Contains('["completionDecision"] = null') -and
    $worker.Contains('completionDecision = $null')) 'Codex adapter or Worker does not explicitly keep ES CompletionDecision unset.'
Add-Case 'recovery-reparse-boundary' (
    $adapter.Contains('ContainsExistingReparsePoint(RunsRoot)') -and
    $adapter.Contains('ContainsExistingReparsePoint(directory)') -and
    $adapter.Contains('ContainsExistingReparsePoint(path)') -and
    $adapter.Contains('safeDirectories')) 'Interrupted-run recovery does not reject reparse points before reading or rewriting RunRecords.'
Add-Case 'plan-command-contract-binding' (
    $worker.Contains("'brainPlanHash','commandId','commandHash','taskContractHash','invocationId'") -and
    $worker.Contains('InvocationId must bind exactly to RunId') -and
    $worker.Contains('commandId does not match the fixed Codex AICommand') -and
    $worker.Contains('Request commandHash does not match the fixed ES AICommand source') -and
    $adapter.Contains('invocation.brainPlanHash') -and
    $adapter.Contains('ComputeHash(CommandPath)') -and
    $adapter.Contains('registeredContract.ComputeStableHash()') -and
    $adapter.Contains('Codex App Server Worker')) 'Codex execution is missing AIBrain/AICommand/TaskContract identity binding.'
Add-Case 'es-owned-receipt-verifier' (
    $adapter.Contains('ESAutomationVerifierRegistry.Register(ReceiptVerifierId, VerifyReceipt)') -and
    $adapter.Contains('result.evidenceScope == ESAutomationEvidenceScope.Runtime') -and
    $adapter.Contains('runtimeRequired = true')) 'Codex receipt cannot be verified by the ES-owned runtime evidence gate.'
Add-Case 'no-asset-or-publish-capability' (
    -not $adapter.Contains('WriteAssets') -and
    @($declaration.policy.forbidden | Where-Object { $_ -in @('direct-assets-write', 'automatic-es-acceptance') }).Count -eq 2) 'Codex declaration does not forbid asset writes or automatic ES acceptance.'
Add-Case 'editor-discoverability-no-runtime' (
    $center.Contains('DrawCodexAppServerStatus') -and
    $center.Contains('ESCodexAppServerAutomation.GetStatus()') -and
    $center.Contains('runtimeStatus')) 'ES Automation Center does not expose a no-side-effect Codex contract status surface.'
Add-Case 'authority-required-reads' (
    $adapter.Contains('"AGENTS.md"') -and
    $adapter.Contains('AISpace", "README.md') -and
    $adapter.Contains('AIBRAIN_ENTRY.md') -and
    $adapter.Contains('KnowledgeIndex.yaml') -and
    $adapter.Contains('codex-app-server-integration.md') -and
    $adapter.Contains('RequiredAuthorityReadsPresent()') -and
    $adapter.Contains('ContainsExistingReparsePoint(path)') -and
    @($declaration.requiredReads | Where-Object { $_ -eq 'AGENTS.md' }).Count -eq 1) 'Codex invocation does not declare the project authority/bootstrap reads.'
Add-Case 'candidate-envelope-authority-read' (
    $adapter.Contains('CandidateEnvelopeSchemaPath') -and
    $adapter.Contains('CandidateEnvelopeSchemaHash') -and
    $adapter.Contains('ContainsExistingReparsePoint(CandidateEnvelopeSchemaPath)')) 'CandidateEnvelope schema is not pinned and authority-checked.'

function Test-InputShape([hashtable]$Candidate) {
    $allowed = @('operation', 'prompt', 'threadId', 'model')
    if (@($Candidate.Keys | Where-Object { $_ -notin $allowed }).Count -gt 0) { return $false }
    $operation = [string]$Candidate['operation']
    if ($operation -notin @('dry-run', 'check-local', 'start-thread', 'turn')) { return $false }
    $prompt = [string]$Candidate['prompt']
    if ($prompt.Length -gt 12000) { return $false }
    if ($operation -eq 'turn' -and [string]::IsNullOrWhiteSpace([string]$Candidate['threadId'])) { return $false }
    return $true
}

Add-Case 'input-positive-dry-run' (Test-InputShape @{ operation = 'dry-run'; prompt = '' }) 'Valid dry-run input was rejected.'
Add-Case 'input-negative-executable' (-not (Test-InputShape @{ operation = 'dry-run'; prompt = ''; executable = 'cmd.exe' })) 'Arbitrary executable field was accepted.'
Add-Case 'input-negative-turn-without-thread' (-not (Test-InputShape @{ operation = 'turn'; prompt = 'x' })) 'Unbound turn was accepted.'
Add-Case 'input-negative-operation' (-not (Test-InputShape @{ operation = 'exec'; prompt = 'x' })) 'Unsupported operation was accepted.'
Add-Case 'input-negative-prompt-limit' (-not (Test-InputShape @{ operation = 'start-thread'; prompt = ('x' * 12001) })) 'Oversized prompt was accepted.'

$failed = @($cases | Where-Object { $_.status -eq 'failed' })
$passedCount = @($cases | Where-Object { $_.status -eq 'passed' }).Count
$caseCount = $cases.Count
$report = [ordered]@{
    schemaVersion = 1
    validator = 'Test-ESCodexAppServerContract'
    status = if ($failed.Count -eq 0) { 'passed' } else { 'failed' }
    caseCount = $caseCount
    passedCount = $passedCount
    failedCount = $failed.Count
    workerHash = $workerHash
    schemaHash = $schemaHash
    cases = @($cases.ToArray())
    runtimeStatus = 'runtime-not-run'
    claimsNotProven = @('Codex CLI/provider availability', 'Unity import/compile/runtime', 'business completion or release acceptance')
}
if ($Json) { $report | ConvertTo-Json -Depth 8 } else { $report | Format-List }
if ($failed.Count -gt 0) { exit 1 }
