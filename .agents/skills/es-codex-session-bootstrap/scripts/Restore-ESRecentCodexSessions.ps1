[CmdletBinding()]
param(
    [ValidateRange(1, 720)]
    [int]$RecentHours = 24,

    [ValidateRange(1, 50)]
    [int]$MaxSessions = 12,

    [switch]$IncludeUnclassified,

    [switch]$IncludeTests,

    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ESCodexSessionState.ps1')

function Get-Responsibility([string]$Text) {
    if ($Text -match 'LAUNCHER_TAB_SMOKE|RESUME_TAB_SMOKE|launcher-smoke|three-digit|3-digit|\u4e09\u4f4d\u6570\u4e58\u6cd5|\u4e09\u4f4d\u6570\u9664\u6cd5|\u9875\u7b7e\u5192\u70df') {
        return [pscustomobject]@{ key = 'test'; title = 'ES-Test'; isTest = $true; classified = $true }
    }
    if ($Text -match 'engineering-acceptance|release-acceptance|\u5de5\u7a0b\u9a8c\u6536\u804c\u8d23|\u5de5\u7a0b\u9a8c\u6536\u5347\u7ea7') {
        return [pscustomobject]@{ key = 'engineering-acceptance'; title = ''; isTest = $false; classified = $true }
    }
    if ($Text -match 'AITest|AI Test|AI-assisted end-to-end|AI \u9a71\u52a8\u7aef\u5230\u7aef\u6d4b\u8bd5') {
        return [pscustomobject]@{ key = 'aitest'; title = ''; isTest = $false; classified = $true }
    }
    if ($Text -match 'ResourcePlan|resource-pipeline|Manifest|Provider|Scope Registry|\u8d44\u6e90\u7ba1\u7ebf') {
        return [pscustomobject]@{ key = 'resource-pipeline'; title = ''; isTest = $false; classified = $true }
    }
    if ($Text -match 'GraphView|Graph V2|graph-audit|NodeRunner|\u56fe\u8d44\u4ea7') {
        return [pscustomobject]@{ key = 'graph-audit'; title = ''; isTest = $false; classified = $true }
    }
    return [pscustomobject]@{ key = ''; title = ''; isTest = $false; classified = $false }
}

function Get-ActiveSessionIds {
    $active = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $localStateBase = if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        Join-Path $env:LOCALAPPDATA 'ESFramework\CodexSessions'
    }
    else {
        Join-Path ([IO.Path]::GetTempPath()) 'ESFramework-CodexSessions'
    }
    $launchStateRoot = Join-Path $localStateBase 'launch-state'
    if (Test-Path -LiteralPath $launchStateRoot -PathType Container) {
        foreach ($file in @(Get-ChildItem -LiteralPath $launchStateRoot -File)) {
            try {
                $state = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
                if ([int]$state.processId -gt 0 -and $null -ne (Get-Process -Id ([int]$state.processId) -ErrorAction SilentlyContinue)) {
                    if (-not [string]::IsNullOrWhiteSpace([string]$state.sessionId)) { [void]$active.Add([string]$state.sessionId) }
                }
            }
            catch {
                Write-Verbose ("Ignoring malformed Codex launch-state file '" + $file.FullName + "': " + $_.Exception.Message)
            }
        }
    }
    return @($active)
}

function Get-ActiveLaunchTokens {
    try {
        return @(Get-CimInstance Win32_Process -ErrorAction Stop |
            Where-Object { $_.Name -in @('cmd.exe', 'node.exe', 'codex.exe') -and [string]$_.CommandLine -match 'CodexLaunch:[A-Za-z0-9:-]+' } |
            ForEach-Object { [regex]::Matches([string]$_.CommandLine, 'CodexLaunch:[A-Za-z0-9:-]+') } |
            ForEach-Object Value |
            Sort-Object -Unique)
    }
    catch { return @() }
}

$skillDirectory = Split-Path -Parent $PSScriptRoot
$skillsDirectory = Split-Path -Parent $skillDirectory
$agentsDirectory = Split-Path -Parent $skillsDirectory
$derivedProjectRoot = Split-Path -Parent $agentsDirectory
$fixedProjectRoot = 'F:\aaProject\ESFrameWorkPublish'
$installedProjectRoot = [IO.Path]::GetFullPath($derivedProjectRoot).TrimEnd('\')
if (-not $installedProjectRoot.Equals($fixedProjectRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Skill location does not match the fixed ESFramework root: $fixedProjectRoot"
}

$sessionRoot = Join-Path $env:USERPROFILE '.codex\sessions'
if (-not (Test-Path -LiteralPath $sessionRoot -PathType Container)) { throw "Codex session root was not found: $sessionRoot" }

$activeSessionIds = @(Get-ActiveSessionIds)
$activeLaunchTokens = @(Get-ActiveLaunchTokens)
$cutoff = (Get-Date).AddHours(-$RecentHours)
$localStateBase = Get-ESCodexLocalStateRoot
$registryPath = Join-Path $localStateBase 'sessions.json'
$registry = Read-ESCodexSessionRegistry $registryPath
$registeredCandidates = @($registry.sessions | Where-Object {
        [string]$_.projectRoot -eq $fixedProjectRoot -and
        -not [string]::IsNullOrWhiteSpace([string]$_.sessionId) -and
        -not [string]::IsNullOrWhiteSpace([string]$_.responsibilityKey) -and
        [string]$_.lifecycleStatus -ne 'Closed' -and
        -not [string]::IsNullOrWhiteSpace([string]$_.lastSeenUtc) -and
        ([DateTime]$_.lastSeenUtc) -ge $cutoff
    } | ForEach-Object {
        [pscustomobject]@{
            sessionId = [string]$_.sessionId
            lastActivity = [DateTime]$_.lastSeenUtc
            responsibilityKey = [string]$_.responsibilityKey
            tabTitle = [string]$_.tabTitle
            isActive = (Test-ESCodexProcessAlive ([int]$_.processId)) -or ([string]$_.sessionId -in $activeSessionIds) -or ([string]$_.launchToken -in $activeLaunchTokens)
            isTest = [string]$_.responsibilityKey -eq 'launcher-smoke' -or [string]$_.responsibilityKey -eq 'test'
            sessionPath = ''
            source = 'registry'
        }
    })
$registeredSessionIds = @($registry.sessions | ForEach-Object { [string]$_.sessionId } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
$recentFiles = @(Get-ChildItem -LiteralPath $sessionRoot -Recurse -Filter 'rollout-*.jsonl' -File |
    Where-Object { $_.LastWriteTime -ge $cutoff } |
    Sort-Object LastWriteTime -Descending)
$candidates = @($registeredCandidates)

foreach ($file in $recentFiles) {
    $prefix = @(Get-Content -LiteralPath $file.FullName -TotalCount 500 -Encoding UTF8)
    $sessionId = ''
    $cwd = ''
    $combined = ''
    foreach ($line in $prefix) {
        try { $row = $line | ConvertFrom-Json } catch { continue }
        if ($row.type -eq 'session_meta') {
            $sessionId = [string]$row.payload.id
            $cwd = [string]$row.payload.cwd
            continue
        }
        $message = ''
        if ($row.type -eq 'event_msg' -and $row.payload.type -eq 'user_message') {
            $message = [string]$row.payload.message
        }
        elseif ($row.type -eq 'response_item' -and $row.payload.role -eq 'user') {
            $message = (@($row.payload.content | ForEach-Object { [string]$_.text }) -join "`n")
        }
        if (-not [string]::IsNullOrWhiteSpace($message) -and $combined.Length -lt 24000) {
            $remaining = 24000 - $combined.Length
            if ($message.Length -gt $remaining) { $message = $message.Substring(0, $remaining) }
            $combined = ($combined + "`n" + $message).Trim()
        }
    }
    if ($cwd -ne $fixedProjectRoot) { continue }
    if ([string]::IsNullOrWhiteSpace($sessionId)) { continue }
    if ($sessionId -in $registeredSessionIds) { continue }
    $responsibility = Get-Responsibility $combined
    if (-not $responsibility.classified) {
        if (-not $IncludeUnclassified) { continue }
        $responsibility.key = 'history-' + $sessionId.Substring(0, 8)
        $responsibility.title = 'ES' + [string][char]0x00B7 + 'History-' + $file.LastWriteTime.ToString('HHmm')
    }
    if ($responsibility.isTest -and -not $IncludeTests) { continue }
    $tokenIsActive = $false
    foreach ($token in $activeLaunchTokens) {
        if ($combined -like ('*' + $token + '*')) { $tokenIsActive = $true; break }
    }
    $candidates += [pscustomobject]@{
        sessionId = $sessionId
        lastActivity = $file.LastWriteTime
        responsibilityKey = [string]$responsibility.key
        tabTitle = [string]$responsibility.title
        isActive = $sessionId -in $activeSessionIds -or $tokenIsActive
        isTest = [bool]$responsibility.isTest
        sessionPath = $file.FullName
        source = 'history-fallback'
    }
}

$selected = @()
$skippedActive = @($candidates | Where-Object isActive)
$activeResponsibilities = @($skippedActive | ForEach-Object responsibilityKey | Sort-Object -Unique)
$skippedActiveResponsibility = @($candidates | Where-Object {
        -not $_.isActive -and $_.responsibilityKey -in $activeResponsibilities
    })
$eligible = @($candidates | Where-Object {
        -not $_.isActive -and $_.responsibilityKey -notin $activeResponsibilities
    })
foreach ($group in @($eligible | Group-Object responsibilityKey)) {
    $selected += @($group.Group | Sort-Object lastActivity -Descending | Select-Object -First 1)
}
$selected = @($selected | Sort-Object lastActivity -Descending | Select-Object -First $MaxSessions)
$deduplicated = @($eligible | Where-Object { $_.sessionId -notin @($selected.sessionId) })

$result = [ordered]@{
    projectRoot = $fixedProjectRoot
    recentHours = $RecentHours
    cutoff = $cutoff
    activeSessionIds = $activeSessionIds
    registryPath = $registryPath
    registryCandidateCount = $registeredCandidates.Count
    historyFallbackCandidateCount = @($candidates | Where-Object source -eq 'history-fallback').Count
    selected = $selected
    skippedActive = $skippedActive
    skippedActiveResponsibility = $skippedActiveResponsibility
    skippedDuplicateResponsibility = $deduplicated
    launched = @()
    dryRun = [bool]$DryRun
}

if (-not $DryRun) {
    $launcherPath = Join-Path $PSScriptRoot 'Start-ESCodexSession.ps1'
    foreach ($candidate in $selected) {
        $arguments = @{
            Mode = 'Resume'
            SessionId = $candidate.sessionId
            ResponsibilityKey = $candidate.responsibilityKey
            TerminalMode = 'ProjectWindow'
            # Recent-session recovery must not be blocked by project hook delivery.
            # This only affects the launched Codex process; hook configuration remains unchanged.
            SkipHooks = $true
            TaskPrompt = 'Restore this exact recent ESFramework session. Recheck the immutable launch envelope, current branch, HEAD, worktree, rules, and evidence before continuing. Do not assume old evidence remains valid.'
        }
        if (-not [string]::IsNullOrWhiteSpace($candidate.tabTitle)) { $arguments.TabTitle = $candidate.tabTitle }
        $launchResult = & $launcherPath @arguments
        $result.launched += $launchResult
    }
}

[pscustomobject]$result
