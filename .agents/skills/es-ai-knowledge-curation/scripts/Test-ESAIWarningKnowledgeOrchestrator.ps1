[CmdletBinding()]
param(
    [string]$ProjectRoot = (Get-Location).Path,
    [string]$WarningPath
)

$ErrorActionPreference = 'Stop'
$utf8 = [Text.UTF8Encoding]::new($false, $true)
$root = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
$generator = Join-Path $root '.agents/skills/es-ai-knowledge-curation/scripts/New-ESAIWarningKnowledgeCandidate.ps1'
$validator = Join-Path $root '.agents/skills/es-ai-knowledge-curation/scripts/Test-ESAIWarningKnowledgeCandidate.ps1'
$candidatePath = 'ES/Automation/Candidates/AIWarningKnowledge/.orchestrator-test.candidate.json'
$receiptPath = 'ES/Automation/Candidates/AIWarningKnowledge/.orchestrator-test.candidate.receipt.json'
$deniedPath = 'ES/Automation/Candidates/AIWarningKnowledge/.orchestrator-test.denied-expansion.json'
$recoveryPath = 'ES/Automation/Candidates/AIWarningKnowledge/.orchestrator-test.recovery.json'
$fixtures = @($candidatePath, $receiptPath, $deniedPath, $recoveryPath)
$preexistingFixtures = @{}
foreach ($fixture in $fixtures) { $preexistingFixtures[$fixture] = Test-Path -LiteralPath (Join-Path $root $fixture) -PathType Leaf }
$cases = [Collections.Generic.List[object]]::new()

if ([string]::IsNullOrWhiteSpace($WarningPath)) {
    $warningRoot = Join-Path $root 'Assets/Plugins/ES/AIWarnings'
    $found = Get-ChildItem -LiteralPath $warningRoot -Recurse -File -Filter 'AgentSkills*.md' | Select-Object -First 1
    if (-not $found) { throw 'DEFAULT_WARNING_NOT_FOUND' }
    $WarningPath = $found.FullName.Substring($root.Length).TrimStart([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar).Replace('\', '/')
}

function Add-Case([string]$Name, [bool]$Passed, [string]$Detail) {
    $cases.Add([pscustomobject]@{ case = $Name; status = if ($Passed) { 'passed' } else { 'failed' }; detail = $Detail })
}

function Read-Json([string]$RelativePath) {
    $full = Join-Path $root $RelativePath
    return $utf8.GetString([IO.File]::ReadAllBytes($full)) | ConvertFrom-Json
}

function Write-Fixture([string]$RelativePath, $Value) {
    $full = Join-Path $root $RelativePath
    [IO.Directory]::CreateDirectory((Split-Path -Parent $full)) | Out-Null
    [IO.File]::WriteAllText($full, ($Value | ConvertTo-Json -Depth 30), $utf8)
}

try {
    $first = & $generator -ProjectRoot $root -WarningPath $WarningPath -OutputPath $candidatePath | ConvertFrom-Json
    $second = & $generator -ProjectRoot $root -WarningPath $WarningPath -OutputPath $candidatePath | ConvertFrom-Json
    Add-Case 'existing-knowledge-positive' ($first.status -eq 'attached' -and $first.matchDecision -eq 'existing' -and $first.transactionExecuted -eq $false) 'Existing Knowledge pointer produced an attached candidate without a transaction.'
    Add-Case 'repeat-idempotency' ($first.candidateId -eq $second.candidateId -and $first.idempotencyKey -eq $second.idempotencyKey) 'Repeated orchestration kept the same StableId+WarningHash identity.'

    $validated = & $validator -ProjectRoot $root -CandidatePath $candidatePath -ReceiptPath $receiptPath | ConvertFrom-Json
    Add-Case 'candidate-receipt-validation' ($validated.status -eq 'passed' -and $validated.findingCount -eq 0) 'Candidate and candidate-only receipt passed structural and binding validation.'

    $bad = Read-Json $candidatePath
    $bad.replay.commands = @('pwsh -NoProfile -Command move source target')
    Write-Fixture $deniedPath $bad
    $deniedOutput = $null
    $deniedRejected = $false
    try { $deniedOutput = & $validator -ProjectRoot $root -CandidatePath $deniedPath | ConvertFrom-Json } catch { $deniedRejected = $true }
    if (-not $deniedRejected -and $deniedOutput) { $deniedRejected = @($deniedOutput.findings | Where-Object { $_.code -eq 'DESTRUCTIVE_REPLAY_COMMAND' }).Count -gt 0 }
    Add-Case 'denied-expansion' $deniedRejected 'A replay command that attempts a destructive move was rejected.'

    $recovery = Read-Json $candidatePath
    Write-Fixture $recoveryPath $recovery
    $recoveryOutput = & $validator -ProjectRoot $root -CandidatePath $recoveryPath | ConvertFrom-Json
    Add-Case 'interruption-preserves-candidate' ($recoveryOutput.status -eq 'passed' -and $recoveryOutput.candidateStatus -eq 'attached') 'A candidate remains independently verifiable when no receipt is supplied, representing an interrupted receipt write without Index mutation.'

    $invalidRejected = $false
    try { & $generator -ProjectRoot $root -WarningPath '..\Documentation\AIKnowledge\KnowledgeIndex.yaml' *> $null } catch { $invalidRejected = $_.Exception.Message -like '*OUTSIDE_PROJECT*' }
    Add-Case 'invalid-path-input' $invalidRejected 'Project traversal input was rejected before reading a source.'
}
catch {
    Add-Case 'harness-execution' $false $_.Exception.Message
}
finally {
    foreach ($fixture in $fixtures) {
        $full = Join-Path $root $fixture
        if (-not $preexistingFixtures[$fixture] -and (Test-Path -LiteralPath $full -PathType Leaf)) { Remove-Item -LiteralPath $full -Force }
    }
}

$failed = @($cases | Where-Object { $_.status -eq 'failed' })
[pscustomobject]@{
    schemaVersion = 1
    validator = 'Test-ESAIWarningKnowledgeOrchestrator'
    status = if ($failed.Count -eq 0) { 'passed' } else { 'failed' }
    caseCount = $cases.Count
    passedCount = @($cases | Where-Object { $_.status -eq 'passed' }).Count
    failedCount = $failed.Count
    cases = @($cases)
    runtimeStatus = 'runtime-not-run'
    nonClaims = @(
        'This replay covers candidate orchestration, not formal Knowledge Apply or save-event integration.',
        'No AIWarning, KnowledgeIndex.yaml, formal Knowledge entry, Git, Runtime or release state was changed by the harness.'
    )
} | ConvertTo-Json -Depth 20
if ($failed.Count -gt 0) { exit 1 }
