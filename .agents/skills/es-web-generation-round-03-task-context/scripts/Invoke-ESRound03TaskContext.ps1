[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$FocusReceiptPath,
    [Parameter(Mandatory=$true)][string]$TaskId,
    [Parameter(Mandatory=$true)][string]$RoutePlanPath,
    [Parameter(Mandatory=$true)][string]$GoalRevisionPath,
    [Parameter(Mandatory=$true)][string]$AcceptanceProfileId,
    [Parameter(Mandatory=$true)][string]$OutcomeEvaluatorId,
    [Parameter(Mandatory=$true)][string[]]$RequestedSourceScope,
    [Parameter(Mandatory=$true)][string]$IdempotencyKey,
    [string[]]$RequiredClaim = @(),
    [string]$OutputPath = 'ES/Output/WebPageStudio/bootstrap/round-03-task-context.json',
    [string]$StoreRoot = 'ES/Output/TaskContextRuntime',
    [string]$AiEvidencePath = ''
)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
function Read-StrictJson([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "input-not-found: $Path" }
    $raw = [Text.UTF8Encoding]::new($false, $true).GetString([IO.File]::ReadAllBytes((Resolve-Path $Path).Path)).TrimStart([char]0xFEFF)
    return ($raw | ConvertFrom-Json)
}
function Assert-Hash([string]$Value, [string]$Name) {
    if ($Value -notmatch '^[a-f0-9]{64}$') { throw "blocked.round-03.invalid-focus-identity: $Name" }
}
function Resolve-ProjectRelative([string]$Path, [string]$Name) {
    if ([IO.Path]::IsPathRooted($Path) -or $Path -match '(^|[\\/])\.\.([\\/]|$)' -or $Path -match '[*?]') { throw "blocked.round-03.path-boundary: $Name" }
    $full = [IO.Path]::GetFullPath((Join-Path $projectRoot ($Path.Replace('/', [IO.Path]::DirectorySeparatorChar))))
    if (-not $full.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "blocked.round-03.missing-$Name" }
    return $Path.Replace('\','/')
}

$focus = Read-StrictJson $FocusReceiptPath
$aiEvidenceFull = if ([IO.Path]::IsPathRooted($AiEvidencePath)) { [IO.Path]::GetFullPath($AiEvidencePath) } elseif ($AiEvidencePath) { [IO.Path]::GetFullPath((Join-Path $projectRoot $AiEvidencePath)) } else { '' }
if (-not $aiEvidenceFull -or -not $aiEvidenceFull.StartsWith($projectRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $aiEvidenceFull -PathType Leaf)) { throw 'blocked.round-03.ai-evidence-required' }
$aiEvidence = Read-StrictJson $aiEvidenceFull
if ([string]$aiEvidence.focusProposalHash -cne [string]$focus.proposalHash -or [string]$aiEvidence.focusScopeHash -cne [string]$focus.focusScopeHash) { throw 'blocked.round-03.ai-evidence-focus-hash-mismatch' }
if ([string]$aiEvidence.taskId -cne [string]$TaskId) { throw 'blocked.round-03.ai-evidence-task-mismatch' }
foreach ($field in @('aiAnalysis','execution','taskContextRationale','returnReceipt')) { if ($null -eq $aiEvidence.PSObject.Properties[$field] -or [string]::IsNullOrWhiteSpace([string]$aiEvidence.$field)) { throw "blocked.round-03.ai-evidence-incomplete:$field" } }
if(([string]$aiEvidence.aiAnalysis).Trim().Length -lt 80 -or ([string]$aiEvidence.execution).Trim().Length -lt 40 -or ([string]$aiEvidence.taskContextRationale).Trim().Length -lt 80){throw 'blocked.round-03.ai-evidence-too-shallow'}
if ([string]$aiEvidence.aiAnalysis -match '(?i)bind the confirmed FocusContext|preserve identity, scope') { throw 'blocked.round-03.synthetic-ai-evidence' }
if ([string]$focus.recordType -cne 'FocusContextReceipt' -or [string]$focus.roundId -cne 'web-generation-round-02' -or [string]$focus.status -cne 'accepted') { throw 'blocked.round-03.missing-focus' }
$intake = Read-StrictJson ([string]$focus.intakePath)
if ($null -eq $intake.PSObject.Properties['aiInterpretation'] -or $null -eq $intake.aiInterpretation -or
    [string]::IsNullOrWhiteSpace([string]$intake.aiInterpretation.objectiveBrief) -or
    [string]::IsNullOrWhiteSpace([string]$intake.aiInterpretation.acceptanceSignals)) {
    throw 'blocked.round-03.ai-interpretation-required'
}
$focusId = [string]$focus.focusContextId
$focusRevision = [int]$focus.focusRevision
$focusProposalHash = if ($focus.PSObject.Properties['focusProposalHash']) { [string]$focus.focusProposalHash } else { [string]$focus.proposalHash }
$focusScopeHash = [string]$focus.focusScopeHash
if ($focusId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,80}$' -or $focusRevision -lt 1) { throw 'blocked.round-03.invalid-focus-identity' }
Assert-Hash $focusProposalHash 'focusProposalHash'; Assert-Hash $focusScopeHash 'focusScopeHash'
$route = Resolve-ProjectRelative $RoutePlanPath 'route-plan'
$goal = Resolve-ProjectRelative $GoalRevisionPath 'goal-revision'
if (@($RequestedSourceScope).Count -eq 0) { throw 'blocked.round-03.scope-expansion: source scope is empty' }

$module = Join-Path $projectRoot 'ES\Automation\TaskContextRuntime\ESTaskContextRuntime.psm1'
Import-Module $module -Force
$state = New-ESTaskContextTask -ProjectRoot $projectRoot -StoreRoot $StoreRoot -TaskId $TaskId -RoutePlanPath $route -GoalRevisionPath $goal `
    -AcceptanceProfileId $AcceptanceProfileId -OutcomeEvaluatorId $OutcomeEvaluatorId -RequiredClaim $RequiredClaim `
    -FocusContextId $focusId -FocusRevision $focusRevision -FocusProposalHash $focusProposalHash -FocusScopeHash $focusScopeHash `
    -RequestedSourceScope $RequestedSourceScope -IdempotencyKey $IdempotencyKey
$state = Confirm-ESTaskSourceScope -ProjectRoot $projectRoot -StoreRoot $StoreRoot -TaskId $state.taskId -ExpectedTaskRevision $state.taskRevision -ExpectedContextVersion $state.contextVersion -IdempotencyKey ($IdempotencyKey + '-source-verify')

$outFull = [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputPath))
$scopeHash=[string]$state.verifiedSourceScopeHash
if($scopeHash -notmatch '^[a-f0-9]{64}$'){throw 'blocked.round-03.source-scope-hash-missing'}
$ctxBytes=[Text.Encoding]::UTF8.GetBytes((@([string]$state.taskId,[int]$state.taskRevision,[int]$state.contextVersion,[string]$state.goalRevisionHash,[string]$state.routePlan.routePlanHash,$scopeHash)|ConvertTo-Json -Compress));$taskContextHash=([BitConverter]::ToString(([Security.Cryptography.SHA256]::Create().ComputeHash($ctxBytes))).Replace('-','').ToLowerInvariant())
$parent = Split-Path -Parent $outFull
if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
$receipt = [ordered]@{
    schemaVersion=1; recordType='TaskContextCreationReceipt'; roundId='web-generation-round-03'; stageId='task-context-create'; status='accepted'
    taskId=[string]$state.taskId; taskRevision=[int]$state.taskRevision; contextVersion=[int]$state.contextVersion
    focusContextId=$focusId; focusRevision=$focusRevision; focusProposalHash=$focusProposalHash; focusScopeHash=$focusScopeHash
    goalRevisionHash=[string]$state.goalRevisionHash; routePlanHash=[string]$state.routePlan.routePlanHash; requestedSourceScope=@($state.requestedSourceScope); sourceScopeHash=$scopeHash; taskContextHash=$taskContextHash
    acceptanceProfileId=$AcceptanceProfileId; outcomeEvaluatorId=$OutcomeEvaluatorId; idempotencyKey=$IdempotencyKey
    aiAnalysis=[string]$aiEvidence.aiAnalysis
    execution=[string]$aiEvidence.execution
    decision='accepted-for-knowledge-route'; returnReceipt=[ordered]@{taskId=[string]$state.taskId; taskRevision=[int]$state.taskRevision; contextVersion=[int]$state.contextVersion; aiReturn=$aiEvidence.returnReceipt; nextRound='web-generation-round-04-knowledge-route'}
    nonClaims=@('not Knowledge routed','not design or generation','not SubAgent or ABCD execution','not Unity/runtime/network/release')
}
$json = $receipt | ConvertTo-Json -Depth 30
[IO.File]::WriteAllText($outFull, $json, [Text.UTF8Encoding]::new($false))
[pscustomobject]@{status='accepted';outputPath=$outFull;taskId=$state.taskId;taskRevision=$state.taskRevision;contextVersion=$state.contextVersion}
