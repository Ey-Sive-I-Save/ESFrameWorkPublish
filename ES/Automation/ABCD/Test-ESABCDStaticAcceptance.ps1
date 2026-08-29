[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,
    [string]$ReportPath = 'ES/Output/StaticReplay/es-abcd-static-acceptance.json',
    [switch]$VerifyNetwork
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$outputRoot = Join-Path $root 'ES/Output/StaticReplay'
$runner = (Get-Command powershell.exe -ErrorAction SilentlyContinue)
if ($null -eq $runner) { $runner = Get-Command pwsh -ErrorAction SilentlyContinue }
if ($null -eq $runner) { throw 'PowerShell executable is required for subprocess acceptance.' }

$components = @(
    [pscustomobject]@{ id = 'orchestration'; script = 'ES/Automation/ABCD/Test-ESABCDOrchestration.ps1'; report = 'ES/Output/StaticReplay/es-abcd-orchestration.json'; args = @() },
    [pscustomobject]@{ id = 'dynamic-controller'; script = 'ES/Automation/ABCD/Test-ESABCDDynamicController.ps1'; report = 'ES/Output/StaticReplay/es-abcd-dynamic-controller.json'; args = @() },
    [pscustomobject]@{ id = 'evidence'; script = 'ES/Automation/ABCD/Test-ESABCDEvidence.ps1'; report = 'ES/Output/StaticReplay/es-abcd-evidence.json'; args = @() },
    [pscustomobject]@{ id = 'self-iteration'; script = 'ES/Automation/ABCD/Test-ESABCDSelfIteration.ps1'; report = 'ES/Output/StaticReplay/es-abcd-self-iteration.json'; args = @() },
    [pscustomobject]@{ id = 'learning'; script = 'ES/Automation/ABCD/Test-ESABCDLearning.ps1'; report = 'ES/Output/StaticReplay/es-abcd-learning.json'; args = @() },
    [pscustomobject]@{ id = 'learning-review'; script = 'ES/Automation/ABCD/Test-ESABCDLearningReview.ps1'; report = 'ES/Output/StaticReplay/es-abcd-learning-review.json'; args = @() },
    [pscustomobject]@{ id = 'certification'; script = 'ES/Automation/ABCD/Test-ESABCDCertification.ps1'; report = 'ES/Output/StaticReplay/es-abcd-certification.json'; args = @() },
    [pscustomobject]@{ id = 'audit-gate'; script = 'ES/Automation/ABCD/Test-ESABCDAuditGate.ps1'; report = 'ES/Output/StaticReplay/es-abcd-audit-gate.json'; args = @() },
    [pscustomobject]@{ id = 'framework-parity'; script = 'ES/Automation/ABCD/Test-ESABCDFrameworkParity.ps1'; report = 'ES/Output/StaticReplay/es-abcd-framework-parity.json'; args = @() },
    [pscustomobject]@{ id = 'persistence'; script = 'ES/Automation/ABCD/Test-ESABCDPersistence.ps1'; report = 'ES/Output/StaticReplay/es-abcd-persistence.json'; args = @() },
    [pscustomobject]@{ id = 'stress'; script = 'ES/Automation/ABCD/Test-ESABCDStress.ps1'; report = 'ES/Output/StaticReplay/es-abcd-stress.json'; args = @() },
    [pscustomobject]@{ id = 'worker-runtime'; script = 'ES/Automation/ABCD/Test-ESABCDWorkerRuntime.ps1'; report = 'ES/Output/StaticReplay/es-abcd-worker-runtime.json'; args = @() },
    [pscustomobject]@{ id = 'divergence'; script = 'ES/Automation/ABCD/Test-ESABCDDivergence.ps1'; report = 'ES/Output/StaticReplay/es-abcd-divergence.json'; args = @() },
    [pscustomobject]@{ id = 'audit-consistency'; script = 'ES/Automation/ABCD/Test-ESABCDAuditConsistency.ps1'; report = 'ES/Output/StaticReplay/es-abcd-audit-consistency.json'; args = @() },
    [pscustomobject]@{ id = 'iteration-feedback'; script = 'ES/Automation/ABCD/Test-ESABCDIterationFeedback.ps1'; report = 'ES/Output/StaticReplay/es-abcd-iteration-feedback.json'; args = @() },
    [pscustomobject]@{ id = 'external-source-lock'; script = 'ES/Automation/ABCD/Test-ESABCDExternalSourceLock.ps1'; report = 'ES/Output/StaticReplay/es-abcd-external-source-lock.json'; args = if ($VerifyNetwork) { @('-VerifyNetwork') } else { @() } },
    [pscustomobject]@{ id = 'task-context-cross-process'; script = 'ES/Automation/TaskContextRuntime/Test-ESTaskContextCrossProcess.ps1'; report = 'ES/Output/StaticReplay/es-task-context-cross-process.json'; args = @() }
)

function Get-Relative([string]$Path) { return $Path.Replace('\','/') }
function Read-StrictJson([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $text = [Text.UTF8Encoding]::new($false, $true).GetString($bytes)
    return ($text | ConvertFrom-Json -ErrorAction Stop)
}
function Get-Hash([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }

$results = [Collections.Generic.List[object]]::new()
$receiptRefs = [Collections.Generic.List[string]]::new()
$strictValidator = Join-Path $root '.agents/skills/es-first-principles-analysis/scripts/Test-ESSkillEvidence.ps1'
foreach ($component in $components) {
    $scriptPath = Join-Path $root $component.script
    $reportFull = Join-Path $root $component.report
    $status = 'passed'
    $finding = $null
    $exitCode = 0
    try {
        if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) { throw "ACCEPTANCE_SCRIPT_MISSING:$($component.script)" }
        $invokeArgs = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$scriptPath,'-ProjectRoot',$root,'-ReportPath',$component.report)
        if (@($component.args).Count -gt 0) { $invokeArgs += @($component.args) }
        $null = & $runner.Source @invokeArgs 2>&1
        $exitCode = $LASTEXITCODE
        if ($exitCode -ne 0) { throw "ACCEPTANCE_SCRIPT_FAILED:$($component.id):exit=$exitCode" }
        if (-not (Test-Path -LiteralPath $reportFull -PathType Leaf)) { throw "ACCEPTANCE_RECEIPT_MISSING:$($component.report)" }
        $receipt = Read-StrictJson $reportFull
        if ([string]$receipt.status -ne 'passed' -or [string]$receipt.staticStatus -ne 'static-passed') { throw "ACCEPTANCE_RECEIPT_NOT_STATIC_PASSED:$($component.id)" }
        if ([string]$receipt.runtimeStatus -notin @('runtime-not-run','worker-process-passed')) { throw "ACCEPTANCE_RUNTIME_BOUNDARY_INVALID:$($component.id)" }
        $null = & $runner.Source -NoProfile -ExecutionPolicy Bypass -File $strictValidator -SkillPath (Join-Path $root '.agents/skills/es-agent-mechanism-replication') -EvidencePath $reportFull -ProjectRoot $root
        if ($LASTEXITCODE -ne 0) { throw "ACCEPTANCE_RECEIPT_STRICT_VALIDATION_FAILED:$($component.id)" }
        [void]$receiptRefs.Add($component.report)
    } catch {
        $status = 'failed'
        $finding = $_.Exception.Message
    }
    [void]$results.Add([pscustomobject][ordered]@{ case = [string]$component.id; status = $status; exitCode = $exitCode; reportPath = Get-Relative $component.report; finding = $finding })
}

$failed = @($results | Where-Object status -eq 'failed')
$sourceRefs = [Collections.Generic.List[string]]::new()
[void]$sourceRefs.Add('ES/Automation/ABCD/Test-ESABCDStaticAcceptance.ps1')
[void]$sourceRefs.Add('.agents/skills/es-agent-mechanism-replication/static-replay.manifest.json')
foreach ($component in $components) {
    [void]$sourceRefs.Add((Get-Relative $component.script))
    if (Test-Path -LiteralPath (Join-Path $root $component.report) -PathType Leaf) { [void]$sourceRefs.Add((Get-Relative $component.report)) }
}
$sourceRefs = @($sourceRefs | Sort-Object -Unique)
$sourceRefHashes = [ordered]@{}
foreach ($sourceRef in $sourceRefs) { $sourceRefHashes[$sourceRef] = Get-Hash (Join-Path $root $sourceRef) }
$evidenceContractPath = Join-Path $root 'ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json'
$evidenceContractHash = Get-Hash $evidenceContractPath
$sha = [Security.Cryptography.SHA256]::Create()
try {
    $seed = ($sourceRefs | ForEach-Object { $_ + ':' + $sourceRefHashes[$_] }) -join '|'
    $planHash = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($seed + '|' + (($results | ConvertTo-Json -Compress -Depth 10)))))).Replace('-','').ToLowerInvariant()
    $instructionInput = [ordered]@{
        operation = 'run-abcd-static-acceptance'
        reportPath = (Get-Relative $ReportPath)
        verifyNetwork = [bool]$VerifyNetwork
        componentCount = $components.Count
    }
    $userInstructionHash = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($instructionInput | ConvertTo-Json -Compress))))).Replace('-','').ToLowerInvariant()
} finally { $sha.Dispose() }
$authorizationKind = if ($VerifyNetwork) { 'current-user-direct' } else { 'read-only' }
$report = [ordered]@{
    schemaVersion = 1
    validator = 'Test-ESABCDStaticAcceptance'
    status = if ($failed.Count) { 'failed' } else { 'passed' }
    staticStatus = if ($failed.Count) { 'static-failed' } else { 'static-passed' }
    runtimeStatus = 'runtime-not-run'
    evidenceLevel = 'S1'
    capturedUtc = [DateTime]::UtcNow.ToString('o')
    authorizationKind = $authorizationKind
    planHash = $planHash
    evidenceContractId = 'es.skill-evidence-receipt'
    evidenceContractHash = $evidenceContractHash
    skillName = 'es-agent-mechanism-replication'
    case = 'abcd-static-acceptance'
    receiptPath = Get-Relative $ReportPath
    sourceRefs = $sourceRefs
    sourceRefHashes = $sourceRefHashes
    toolId = 'es-abcd-static-acceptance'
    unityVersion = 'not-run'
    componentReceipts = @($receiptRefs | ForEach-Object { [ordered]@{ path = $_; sha256 = Get-Hash (Join-Path $root $_) } })
    cases = @($results)
    claimsNotProven = @('Unity/Worker/host Runtime','RuntimeAcceptance','ReleaseAcceptance','external authority certification')
}
if ($VerifyNetwork) {
    $report.userInstructionHash = $userInstructionHash
    $report.authorizedOperations = @('run-abcd-static-acceptance','verify-network-content-hashes')
    $report.authorizedPaths = @($ReportPath) + @($components | ForEach-Object { $_.script })
}
$reportFull = Join-Path $root $ReportPath
New-Item -ItemType Directory -Path (Split-Path $reportFull) -Force | Out-Null
[IO.File]::WriteAllText($reportFull, ($report | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
$report | ConvertTo-Json -Depth 20
if ($failed.Count) { exit 1 }
