[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$IntakePath,
    [Parameter(Mandatory=$true)][string]$Focus,
    [Parameter(Mandatory=$true)][string[]]$AllowedScope,
    [Parameter(Mandatory=$true)][string[]]$ForbiddenExpansion,
    [Parameter(Mandatory=$true)][string[]]$RequiredReads,
    [Parameter(Mandatory=$true)][string[]]$AcceptanceSignals,
    [ValidateSet('normal','elevated','critical')][string]$Priority = 'normal',
    [ValidateSet('confirm','reject')][string]$UserDecision = 'confirm',
    [string]$AiEvidencePath = '',
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
if (-not $OutputPath) {
    $OutputPath = Join-Path $projectRoot 'ES\Output\WebPageStudio\bootstrap\round-02-focus.json'
}

function Read-JsonUtf8([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "input-not-found: $Path" }
    return (Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json)
}
function Write-JsonUtf8([object]$Value, [string]$Path) {
    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    $json = $Value | ConvertTo-Json -Depth 20
    $temp = $Path + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    [IO.File]::WriteAllText($temp, $json, [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temp -Destination $Path -Force
}

$intake = Read-JsonUtf8 $IntakePath
if ([string]$intake.recordType -cne 'RequirementIntakeReceipt' -or
    [string]$intake.roundId -cne 'web-generation-round-01' -or
    [string]$intake.status -cne 'accepted') {
    throw 'blocked.round-02.missing-intake: accepted Round 01 receipt is required'
}
if ($null -eq $intake.PSObject.Properties['aiInterpretation'] -or $null -eq $intake.aiInterpretation -or
    [string]::IsNullOrWhiteSpace([string]$intake.aiInterpretation.objectiveBrief) -or
    [string]::IsNullOrWhiteSpace([string]$intake.aiInterpretation.acceptanceSignals)) {
    throw 'blocked.round-02.ai-interpretation-required'
}
if ([string]$intake.inputHash -notmatch '^[a-f0-9]{64}$') {
    throw 'blocked.round-02.invalid-intake-hash'
}
if ([string]::IsNullOrWhiteSpace($Focus)) { throw 'blocked.round-02.ambiguous-focus' }
if (@($AllowedScope).Count -eq 0 -or @($AcceptanceSignals).Count -eq 0) {
    throw 'blocked.round-02.scope-or-acceptance-empty'
}
if ([string]::IsNullOrWhiteSpace($AiEvidencePath)) { throw 'blocked.round-02.ai-evidence-required' }
$aiEvidenceFull = if ([IO.Path]::IsPathRooted($AiEvidencePath)) { [IO.Path]::GetFullPath($AiEvidencePath) } else { [IO.Path]::GetFullPath((Join-Path $projectRoot $AiEvidencePath)) }
if (-not $aiEvidenceFull.StartsWith($projectRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $aiEvidenceFull -PathType Leaf)) { throw 'blocked.round-02.ai-evidence-missing' }
$aiEvidence = Read-JsonUtf8 $aiEvidenceFull
if ([string]$aiEvidence.intakeHash -cne [string]$intake.inputHash) { throw 'blocked.round-02.ai-evidence-intake-hash-mismatch' }
foreach ($field in @('aiAnalysis','execution','focusRationale','returnReceipt')) {
    if ($null -eq $aiEvidence.PSObject.Properties[$field] -or [string]::IsNullOrWhiteSpace([string]$aiEvidence.$field)) { throw "blocked.round-02.ai-evidence-incomplete:$field" }
}
if ([string]$aiEvidence.focusStatement -cne [string]$Focus) { throw 'blocked.round-02.ai-evidence-focus-mismatch' }
if(([string]$aiEvidence.aiAnalysis).Trim().Length -lt 80 -or ([string]$aiEvidence.execution).Trim().Length -lt 40 -or ([string]$aiEvidence.focusRationale).Trim().Length -lt 60){throw 'blocked.round-02.ai-evidence-too-shallow'}
if ([string]$aiEvidence.aiAnalysis -match '(?i)freeze one bounded focus|do not invent business intent' -or [string]$aiEvidence.execution -match '(?i)create proposal, verify revision') { throw 'blocked.round-02.synthetic-ai-evidence' }

$module = Join-Path $projectRoot 'ES\Automation\TaskFocusContext\ESTaskFocusContext.psm1'
Import-Module $module -Force
$proposal = New-TaskFocusProposal -Focus $Focus -Priority $Priority -Source 'round-01-intake' `
    -AllowedScope $AllowedScope -ForbiddenExpansion $ForbiddenExpansion `
    -RequiredReads $RequiredReads -AcceptanceSignals $AcceptanceSignals
$pending = Invoke-TaskFocusProposal -Current $null -Proposal $proposal -UserDecision 'none' -ExpectedRevision 0
if ([string]$pending.status -ne 'pending-confirmation') { throw 'blocked.round-02.pending-focus-not-created' }

if ($UserDecision -eq 'reject') {
    $decision = Invoke-TaskFocusProposal -Current $pending -Proposal $proposal -UserDecision 'reject' -ExpectedRevision ([int]$pending.revision)
    $projection = $null
    $status = 'rejected'
} else {
    $decision = Invoke-TaskFocusProposal -Current $pending -Proposal $proposal -UserDecision 'confirm' -ExpectedRevision ([int]$pending.revision)
    if ([string]$decision.status -ne 'confirmed') { throw 'blocked.round-02.confirmation-failed' }
    $projection = New-FocusContextProjection -Context $decision
    $status = 'accepted'
}

$receipt = [ordered]@{
    schemaVersion = 1; recordType = 'FocusContextReceipt'; roundId = 'web-generation-round-02'; stageId = 'task-focus-lock'
    status = $status; intakePath = (Resolve-Path $IntakePath).Path; intakeHash = [string]$intake.inputHash
    focusContextId = [string]$decision.focusContextId; focusRevision = [int]$decision.revision
    proposalHash = [string]$proposal.proposalHash; focusScopeHash = [string]$decision.focusScopeHash
    focus = $Focus; allowedScope = @($AllowedScope); forbiddenExpansion = @($ForbiddenExpansion)
    requiredReads = @($RequiredReads); acceptanceSignals = @($AcceptanceSignals)
    aiAnalysis = [string]$aiEvidence.aiAnalysis
    execution = [string]$aiEvidence.execution
    decision = $(if ($UserDecision -eq 'confirm') { 'confirmed-for-task-context' } else { 'rejected-by-user' })
    returnReceipt = [ordered]@{ status = $status; projection = $projection; aiReturn = $aiEvidence.returnReceipt; nextRound = 'web-generation-round-03-task-context' }
    nonClaims = @('not TaskContext created','not Knowledge routed','not SubAgent or ABCD execution','not page generation','not runtime or network')
}
Write-JsonUtf8 ([pscustomobject]$receipt) $OutputPath
Write-Output ([pscustomobject]@{ status = $status; outputPath = (Resolve-Path $OutputPath).Path; focusContextId = $decision.focusContextId; focusRevision = $decision.revision; proposalHash = $proposal.proposalHash })
