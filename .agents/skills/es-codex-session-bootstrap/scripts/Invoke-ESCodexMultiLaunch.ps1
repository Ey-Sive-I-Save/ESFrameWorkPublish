[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PlanPath,
    [string]$ProjectPath = '',
    [ValidateRange(1,16)][int]$MaxParallel = 3,
    [switch]$Launch,
    [switch]$Reissue,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRootCandidate = if ([string]::IsNullOrWhiteSpace($ProjectPath)) { [IO.Path]::GetFullPath((Join-Path $scriptRoot '..\..\..\..')) } else { [IO.Path]::GetFullPath($ProjectPath) }
$projectRoot = (Resolve-Path -LiteralPath $projectRootCandidate).Path
$sharedPathBoundary = Join-Path $projectRoot '.agents/skills/es-skill-governance/scripts/ESPathBoundary.Common.ps1'
if (-not (Test-Path -LiteralPath $sharedPathBoundary -PathType Leaf)) { throw 'Shared path boundary contract is missing.' }
. $sharedPathBoundary
$planCandidate = [IO.Path]::GetFullPath($PlanPath)
$projectPrefix = ([IO.Path]::GetFullPath($projectRoot)).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
$tempPrefix = ([IO.Path]::GetFullPath([IO.Path]::GetTempPath())).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
if (-not ($planCandidate.StartsWith($projectPrefix,[StringComparison]::OrdinalIgnoreCase) -or $planCandidate.StartsWith($tempPrefix,[StringComparison]::OrdinalIgnoreCase))) { throw 'PlanPath must remain within ProjectPath or the approved system Temp root.' }
$planFull = (Resolve-Path -LiteralPath $planCandidate).Path
$plan = Get-Content -LiteralPath $planFull -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $plan) {
    [pscustomobject][ordered]@{
        operation='MultiLaunch'; status='NeedsInputs'; reasonCode='EmptyPlan'
        userMessage='Request accepted, but the plan is empty. Add at least one launches[] responsibility before starting any window.'
        requiredInputs=@('batchId','maxParallel','dryRun','launches[]'); launchStarted=$false
    } | ConvertTo-Json -Depth 10
    exit 0
}
$entries = @($plan.launches)
if ($entries.Count -eq 0) {
    [pscustomobject][ordered]@{
        operation='MultiLaunch'; status='NeedsInputs'; reasonCode='MissingLaunches'
        userMessage='Request accepted, but launches[] is missing. Add taskKey, responsibilityKey, tabTitle, taskPrompt, and mode for each window.'
        requiredInputs=@('launches[].taskKey','launches[].responsibilityKey','launches[].tabTitle','launches[].taskPrompt','launches[].mode'); launchStarted=$false
    } | ConvertTo-Json -Depth 10
    exit 0
}
if ($entries.Count -gt 50) { throw 'Launch plan exceeds the 50-target safety bound.' }

function Get-Sha256([string]$text) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))).Replace('-', '').ToLowerInvariant()) }
    finally { $sha.Dispose() }
}
function Get-ProjectIdentityFingerprint([string]$Root) {
    $identityFiles = @('AGENTS.md','ProjectSettings/ProjectVersion.txt')
    $parts = foreach ($relative in $identityFiles) {
        $full = (Resolve-ESContainedRelativePath -Candidate $relative -ContainerRoot $Root -Label 'ProjectIdentityFile').FullPath
        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "Project identity file is missing: $relative" }
        $hash = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant()
        "$relative|$hash"
    }
    return Get-Sha256 (($parts | Sort-Object) -join "`n")
}
$projectIdentityFingerprint = Get-ProjectIdentityFingerprint $projectRoot
$declaredProjectIdentity = [string]$plan.projectIdentityFingerprint
if (($Launch -or $Reissue) -and [string]::IsNullOrWhiteSpace($declaredProjectIdentity)) { throw 'Project identity fingerprint is required for Launch or Reissue.' }
if (-not [string]::IsNullOrWhiteSpace($declaredProjectIdentity) -and $declaredProjectIdentity -cne $projectIdentityFingerprint) { throw 'Project identity fingerprint does not match the selected ProjectPath.' }

$seenTask = @{}
$seenResponsibility = @{}
$results = @()
$statusScript = Join-Path $scriptRoot 'Get-ESCodexSessionStatus.ps1'
$index = 0
foreach ($entry in $entries) {
    $index++
    $taskKey = [string]$entry.taskKey
    $responsibility = [string]$entry.responsibilityKey
    $prompt = [string]$entry.taskPrompt
    $tabTitle = [string]$entry.tabTitle
    $mode = if ([string]::IsNullOrWhiteSpace([string]$entry.mode)) { 'New' } else { [string]$entry.mode }
    $item = [ordered]@{ index=$index; wave=[math]::Ceiling($index / [double]$MaxParallel); taskKey=$taskKey; responsibilityKey=$responsibility; tabTitle=$tabTitle; mode=$mode; status='Prepared'; launchAttempted=$false; nonClaims=@('No context acceptance or runtime success is claimed by this planner.') }
    if ([string]::IsNullOrWhiteSpace($taskKey) -or [string]::IsNullOrWhiteSpace($responsibility) -or [string]::IsNullOrWhiteSpace($prompt)) { $item.status='InvalidPlan'; $item.reasonCode='MissingRequiredField'; $results += [pscustomobject]$item; continue }
    if ($mode -notin @('New','Handoff','Reissue')) { $item.status='InvalidPlan'; $item.reasonCode='UnknownMode'; $results += [pscustomobject]$item; continue }
    if ($seenTask.ContainsKey($taskKey)) { $item.status='InvalidPlan'; $item.reasonCode='DuplicateTaskKey'; $results += [pscustomobject]$item; continue }
    if ($seenResponsibility.ContainsKey($responsibility)) { $item.status='InvalidPlan'; $item.reasonCode='DuplicateResponsibilityKey'; $results += [pscustomobject]$item; continue }
    $seenTask[$taskKey] = $true; $seenResponsibility[$responsibility] = $true

    # Check the authoritative local registry before launching. Closed records
    # are excluded by Get-ESCodexSessionStatus, so a rerun is safely idempotent.
    try {
        $statusRaw = @(& $statusScript -TaskKey $taskKey -ResponsibilityKey $responsibility 2>&1)
        # The status script normally returns a PSCustomObject. Preserve it
        # directly; only parse text when a host wrapper serialized the result.
        $statusJson = @($statusRaw | Where-Object {
            $_ -isnot [System.Management.Automation.ErrorRecord] -and
            $null -ne $_.PSObject.Properties['sessions']
        } | Select-Object -Last 1)
        if ($statusJson.Count -eq 0) {
            $statusText = ($statusRaw | ForEach-Object { [string]$_ }) -join [Environment]::NewLine
            if ($statusText.Trim().StartsWith('{')) { try { $statusJson = @($statusText | ConvertFrom-Json) } catch { $statusJson = @() } }
        }
        $statusJson = $statusJson | Select-Object -Last 1
        $active = @($statusJson.sessions | Where-Object { [string]$_.lifecycleStatus -ne 'Closed' -and -not [string]::IsNullOrWhiteSpace([string]$_.sessionId) })
        if ($active.Count -gt 0) {
            $item.status='AlreadyRunning'; $item.reasonCode='ActiveSessionForTask'; $item.existingSessionIds=@($active | ForEach-Object { [string]$_.sessionId }); $results += [pscustomobject]$item; continue
        }
    } catch {
        $item.status='PreflightFailed'; $item.reasonCode='SessionRegistryUnavailable'; $item.error=$_.Exception.Message; $results += [pscustomobject]$item; continue
    }

    if ($mode -in @('Handoff','Reissue')) {
        $sessionPath = [string]$entry.sessionPath
        $archivePath = [string]$entry.archivePath
        if ([string]::IsNullOrWhiteSpace($sessionPath) -or [string]::IsNullOrWhiteSpace($archivePath)) { $item.status='NeedsInputs'; $item.reasonCode='MissingSessionOrArchive'; $results += [pscustomobject]$item; continue }
        if (-not (Test-Path -LiteralPath $sessionPath) -or -not (Test-Path -LiteralPath $archivePath)) { $item.status='NeedsInputs'; $item.reasonCode='SessionOrArchiveNotFound'; $results += [pscustomobject]$item; continue }
        $existingEnvelope = [string]$entry.existingEnvelopePath
        $existingToken = [string]$entry.existingLaunchToken
        if (-not [string]::IsNullOrWhiteSpace($existingEnvelope)) {
            if ([string]::IsNullOrWhiteSpace($existingToken)) { $item.status='NeedsReissue'; $item.reasonCode='MissingLaunchTokenForExistingEnvelope'; $results += [pscustomobject]$item; continue }
            $validator = Join-Path $scriptRoot 'Test-ESCodexLaunchEnvelope.ps1'
            $checkRaw = @(& $validator -EnvelopePath $existingEnvelope -LaunchToken $existingToken -ProjectPath $projectRoot -StrictGit 2>&1)
            $check = [string]($checkRaw | ForEach-Object { [string]$_ } | Where-Object { $_.Trim() } | Select-Object -Last 1)
            try { $checkJson = $check | ConvertFrom-Json } catch { $checkJson = $null }
            if (-not $checkJson -or -not [bool]$checkJson.valid) { $item.status='NeedsReissue'; $item.reasonCode='ExistingEnvelopeDrift'; $item.validation=$checkJson; $results += [pscustomobject]$item; continue }
        }
    }
    if (-not $Launch) { $item.status = 'Prepared'; $results += [pscustomobject]$item; continue }
    $item.launchAttempted = $true
    try {
        if ($mode -eq 'New') {
            $launcher = Join-Path $scriptRoot 'Start-ESCodexSession.ps1'
            $args = @{ Mode='New'; ProjectPath=$projectRoot; ProjectIdentityFingerprint=$projectIdentityFingerprint; TaskKey=$taskKey; ResponsibilityKey=$responsibility; TabTitle=$tabTitle; TaskPrompt=''; DeferTaskPrompt=$true }
            if ($DryRun) { $args.DryRun=$true }
            $out = & $launcher @args 2>&1
            $launchResult = @($out | Where-Object { $_.PSObject.Properties.Name -contains 'contextAccepted' } | Select-Object -Last 1)[0]
            if ($launchResult -and [bool]$launchResult.contextAccepted) {
                $sender = Join-Path $scriptRoot 'Start-ESCodexSession.ps1'
                $delivery = & $sender -Mode SendMessage -ProjectPath $projectRoot -SessionId ([string]$launchResult.sessionId) -ResponsibilityKey $responsibility -MessageBody $prompt -IdempotencyKey ('multilaunch-' + $taskKey) 2>&1
                $item.taskDelivery = @($delivery | Select-Object -Last 1)
                $item.taskDeliveryStatus = if ([bool]$item.taskDelivery.queued -or [bool]$item.taskDelivery.accepted) { 'QueuedAfterContextAccepted' } else { 'DeliveryBlocked' }
            }
            else {
                $item.taskDeliveryStatus = 'BlockedBeforeContextAccepted'
                $item.reasonCode = 'ContextNotAccepted'
            }
        } else {
            $orchestrator = Join-Path $projectRoot 'ES\AI协作历程（Codex）\Tools\Complete-ESCodexHandoff.ps1'
            $args = @{ SessionPath=$sessionPath; ArchivePath=$archivePath; ProjectPath=$projectRoot; ProjectIdentityFingerprint=$projectIdentityFingerprint; TaskKey=$taskKey; ResponsibilityKey=$responsibility; TabTitle=$tabTitle; TaskPrompt=$prompt; OpenNew=$true; DryRun=$DryRun }
            if ($mode -eq 'Reissue' -and -not $Reissue) { $item.status='NeedsReissue'; $item.reasonCode='ExplicitReissueRequired'; $results += [pscustomobject]$item; continue }
            $out = & $orchestrator @args 2>&1
        }
        $item.status = if ($DryRun) { 'DryRunPrepared' } else { 'Launched' }
        $item.result = @($out | Select-Object -Last 1)
    } catch { $item.status='Failed'; $item.reasonCode='LaunchError'; $item.error=$_.Exception.Message }
    $results += [pscustomobject]$item
}

$canonical = ($entries | ConvertTo-Json -Depth 12 -Compress)
$batchId = if (-not [string]::IsNullOrWhiteSpace([string]$plan.batchId)) { [string]$plan.batchId } else { 'batch-' + (Get-Sha256 $canonical).Substring(0,16) }
$messageByReason = @{
    MissingRequiredField='Required fields are missing; no window was created. Fill taskKey, responsibilityKey, tabTitle, and taskPrompt.'
    MissingSessionOrArchive='Handoff/Reissue requires sessionPath and archivePath. Use mode=New for a new responsibility window.'
    SessionOrArchiveNotFound='The session or archive path does not exist; provide current paths or use mode=New.'
    ExistingEnvelopeDrift='The immutable handoff snapshot drifted; reissue it instead of switching sources silently.'
    ActiveSessionForTask='An active session already owns this task or responsibility; no duplicate window was created.'
}
foreach ($result in $results) {
    if ($messageByReason.ContainsKey([string]$result.reasonCode)) { $result | Add-Member -NotePropertyName userMessage -NotePropertyValue $messageByReason[[string]$result.reasonCode] -Force }
}
$failed = @($results | Where-Object { $_.status -in @('InvalidPlan','NeedsInputs','NeedsReissue','PreflightFailed','Failed') }).Count
$operatorMessage = if ($failed -eq 0) { 'Plan accepted and preflighted; responsibility delivery is proven only by ContextAccepted.' } elseif ($failed -eq $entries.Count) { 'Request accepted, but no responsibility meets the start conditions. Follow each item userMessage and retry.' } else { 'Request accepted; some responsibilities are prepared while others remain paused for missing input or evidence.' }
[pscustomobject][ordered]@{
    operation='MultiLaunch'; status=if($failed -eq 0){'Prepared'}else{'NeedsInputs'}; userMessage=$operatorMessage; batchId=$batchId; batchFingerprint=(Get-Sha256 $canonical); projectRoot=$projectRoot; projectIdentityFingerprint=$projectIdentityFingerprint
    requestedCount=$entries.Count; waveCount=[math]::Ceiling($entries.Count / [double]$MaxParallel); preparedCount=@($results | Where-Object status -in @('Prepared','DryRunPrepared')).Count
    launchedCount=@($results | Where-Object status -eq 'Launched').Count; failedCount=$failed
    partialFailure=($failed -gt 0 -and $failed -lt $entries.Count); maxParallel=$MaxParallel
    concurrencyNote=if ($entries.Count -gt $MaxParallel) { 'Plan is bounded; external host must schedule waves and must not exceed MaxParallel.' } else { '' }
    launches=$results
} | ConvertTo-Json -Depth 20
