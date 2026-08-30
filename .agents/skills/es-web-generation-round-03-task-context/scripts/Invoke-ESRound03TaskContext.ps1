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
    [string]$StoreRoot = 'ES/Output/TaskContextRuntime'
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
if ([string]$focus.recordType -cne 'FocusContextReceipt' -or [string]$focus.roundId -cne 'web-generation-round-02' -or [string]$focus.status -cne 'accepted') { throw 'blocked.round-03.missing-focus' }
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

$outFull = [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputPath))
$parent = Split-Path -Parent $outFull
if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
$receipt = [ordered]@{
    schemaVersion=1; recordType='TaskContextCreationReceipt'; roundId='web-generation-round-03'; stageId='task-context-create'; status='accepted'
    taskId=[string]$state.taskId; taskRevision=[int]$state.taskRevision; contextVersion=[int]$state.contextVersion
    focusContextId=$focusId; focusRevision=$focusRevision; focusProposalHash=$focusProposalHash; focusScopeHash=$focusScopeHash
    goalRevisionHash=[string]$state.goalRevisionHash; routePlanHash=[string]$state.routePlan.routePlanHash; requestedSourceScope=@($state.requestedSourceScope); sourceScopeHash=[string]$state.verifiedSourceScopeHash
    acceptanceProfileId=$AcceptanceProfileId; outcomeEvaluatorId=$OutcomeEvaluatorId; idempotencyKey=$IdempotencyKey
    aiAnalysis='Bind the confirmed FocusContext to one platform task; preserve identity, scope, route, goal, and CAS invariants.'
    execution='Created TaskContextRuntime task through the platform API; no downstream stage was invoked.'
    decision='accepted-for-knowledge-route'; returnReceipt=[ordered]@{taskId=[string]$state.taskId; taskRevision=[int]$state.taskRevision; contextVersion=[int]$state.contextVersion; nextRound='web-generation-round-04-knowledge-route'}
    nonClaims=@('not Knowledge routed','not design or generation','not SubAgent or ABCD execution','not Unity/runtime/network/release')
}
$json = $receipt | ConvertTo-Json -Depth 30
[IO.File]::WriteAllText($outFull, $json, [Text.UTF8Encoding]::new($false))
[pscustomobject]@{status='accepted';outputPath=$outFull;taskId=$state.taskId;taskRevision=$state.taskRevision;contextVersion=$state.contextVersion}
