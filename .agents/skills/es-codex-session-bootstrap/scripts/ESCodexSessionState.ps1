$ErrorActionPreference = 'Stop'

function Get-ESCodexLocalStateRoot {
    if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        return Join-Path $env:LOCALAPPDATA 'ESFramework\CodexSessions'
    }
    return Join-Path ([IO.Path]::GetTempPath()) 'ESFramework-CodexSessions'
}

function Get-ESCodexPropertyValue([object]$InputObject, [string]$Name, [object]$DefaultValue = $null) {
    if ($null -eq $InputObject) { return $DefaultValue }
    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) { return $DefaultValue }
    return $property.Value
}

function Set-ESCodexPropertyValue([object]$InputObject, [string]$Name, [object]$Value) {
    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) {
        $InputObject | Add-Member -NotePropertyName $Name -NotePropertyValue $Value
    }
    else {
        $property.Value = $Value
    }
}

function Get-ESCodexStableId([string]$Value) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes($Value)
        return (([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()).Substring(0, 32)
    }
    finally { $sha.Dispose() }
}

function ConvertTo-ESCodexSessionRecord([object]$Record) {
    $registeredAtUtc = [string](Get-ESCodexPropertyValue $Record 'registeredAtUtc' '')
    $lastSeenUtc = [string](Get-ESCodexPropertyValue $Record 'lastSeenUtc' '')
    if ([string]::IsNullOrWhiteSpace($registeredAtUtc)) {
        $registeredAtUtc = if ([string]::IsNullOrWhiteSpace($lastSeenUtc)) { [DateTime]::UtcNow.ToString('o') } else { $lastSeenUtc }
    }
    $lifecycleStatus = [string](Get-ESCodexPropertyValue $Record 'lifecycleStatus' '')
    if ([string]::IsNullOrWhiteSpace($lifecycleStatus)) {
        $lifecycleStatus = if (-not [string]::IsNullOrWhiteSpace([string](Get-ESCodexPropertyValue $Record 'closedAtUtc' ''))) {
            'Closed'
        }
        elseif ([string]::IsNullOrWhiteSpace([string](Get-ESCodexPropertyValue $Record 'sessionId' ''))) {
            'PendingRegistration'
        }
        else {
            'Registered'
        }
    }
    $recordId = [string](Get-ESCodexPropertyValue $Record 'recordId' '')
    if ([string]::IsNullOrWhiteSpace($recordId)) {
        $identitySeed = @(
            [string](Get-ESCodexPropertyValue $Record 'launchToken' ''),
            [string](Get-ESCodexPropertyValue $Record 'sessionId' ''),
            [string](Get-ESCodexPropertyValue $Record 'taskFingerprint' ''),
            [string](Get-ESCodexPropertyValue $Record 'taskKey' ''),
            [string](Get-ESCodexPropertyValue $Record 'tabTitle' '')
        ) -join '|'
        $recordId = Get-ESCodexStableId $identitySeed
    }

    return [pscustomobject][ordered]@{
        identityVersion = 1
        recordId = $recordId
        sessionId = [string](Get-ESCodexPropertyValue $Record 'sessionId' '')
        requestedSessionId = [string](Get-ESCodexPropertyValue $Record 'requestedSessionId' '')
        projectKey = [string](Get-ESCodexPropertyValue $Record 'projectKey' 'ESFramework')
        projectRoot = [string](Get-ESCodexPropertyValue $Record 'projectRoot' '')
        responsibilityKey = [string](Get-ESCodexPropertyValue $Record 'responsibilityKey' 'default')
        taskKey = [string](Get-ESCodexPropertyValue $Record 'taskKey' '')
        taskFingerprint = [string](Get-ESCodexPropertyValue $Record 'taskFingerprint' '')
        tabTitle = [string](Get-ESCodexPropertyValue $Record 'tabTitle' '')
        terminalMode = [string](Get-ESCodexPropertyValue $Record 'terminalMode' '')
        terminalWindowName = [string](Get-ESCodexPropertyValue $Record 'terminalWindowName' '')
        windowKey = [string](Get-ESCodexPropertyValue $Record 'windowKey' '')
        wtSession = [string](Get-ESCodexPropertyValue $Record 'wtSession' '')
        processId = [int](Get-ESCodexPropertyValue $Record 'processId' 0)
        terminalLauncherProcessId = [int](Get-ESCodexPropertyValue $Record 'terminalLauncherProcessId' 0)
        terminalWindowProcessId = [int](Get-ESCodexPropertyValue $Record 'terminalWindowProcessId' 0)
        launchToken = [string](Get-ESCodexPropertyValue $Record 'launchToken' '')
        envelopePath = [string](Get-ESCodexPropertyValue $Record 'envelopePath' '')
        handoffSnapshotDirectory = [string](Get-ESCodexPropertyValue $Record 'handoffSnapshotDirectory' '')
        commandWrapperPath = [string](Get-ESCodexPropertyValue $Record 'commandWrapperPath' '')
        launchPhase = [string](Get-ESCodexPropertyValue $Record 'launchPhase' '')
        promptObserved = [bool](Get-ESCodexPropertyValue $Record 'promptObserved' $false)
        contextAccepted = [bool](Get-ESCodexPropertyValue $Record 'contextAccepted' $false)
        startupFailed = [bool](Get-ESCodexPropertyValue $Record 'startupFailed' $false)
        startupTimedOut = [bool](Get-ESCodexPropertyValue $Record 'startupTimedOut' $false)
        startupFailureReason = [string](Get-ESCodexPropertyValue $Record 'startupFailureReason' '')
        acceptanceReceiptPath = [string](Get-ESCodexPropertyValue $Record 'acceptanceReceiptPath' '')
        startupDiagnosticPath = [string](Get-ESCodexPropertyValue $Record 'startupDiagnosticPath' '')
        externalClaimId = [string](Get-ESCodexPropertyValue $Record 'externalClaimId' '')
        externalClaimBindingId = [string](Get-ESCodexPropertyValue $Record 'externalClaimBindingId' '')
        externalClaimState = [string](Get-ESCodexPropertyValue $Record 'externalClaimState' '')
        externalClaimDirectory = [string](Get-ESCodexPropertyValue $Record 'externalClaimDirectory' '')
        externalClaimRequestSha256 = [string](Get-ESCodexPropertyValue $Record 'externalClaimRequestSha256' '')
        externalClaimResponseSha256 = [string](Get-ESCodexPropertyValue $Record 'externalClaimResponseSha256' '')
        externalClaimProcessId = [int](Get-ESCodexPropertyValue $Record 'externalClaimProcessId' 0)
        externalClaimProcessStartedAtUtc = [string](Get-ESCodexPropertyValue $Record 'externalClaimProcessStartedAtUtc' '')
        externalClaimExpectedCmdProcessId = [int](Get-ESCodexPropertyValue $Record 'externalClaimExpectedCmdProcessId' 0)
        externalClaimExpectedCmdProcessStartedAtUtc = [string](Get-ESCodexPropertyValue $Record 'externalClaimExpectedCmdProcessStartedAtUtc' '')
        externalClaimAcceptedAtUtc = [string](Get-ESCodexPropertyValue $Record 'externalClaimAcceptedAtUtc' '')
        requiresV2Resume = [bool](Get-ESCodexPropertyValue $Record 'requiresV2Resume' $false)
        legacyEnvelopePath = [string](Get-ESCodexPropertyValue $Record 'legacyEnvelopePath' '')
        handoffFiles = @(Get-ESCodexPropertyValue $Record 'handoffFiles' @())
        lastKnownHead = [string](Get-ESCodexPropertyValue $Record 'lastKnownHead' '')
        lifecycleStatus = $lifecycleStatus
        registeredAtUtc = $registeredAtUtc
        lastSeenUtc = $lastSeenUtc
        closedAtUtc = [string](Get-ESCodexPropertyValue $Record 'closedAtUtc' '')
        lastRepairUtc = [string](Get-ESCodexPropertyValue $Record 'lastRepairUtc' '')
        availability = [string](Get-ESCodexPropertyValue $Record 'availability' 'Unknown')
        availabilityUpdatedUtc = [string](Get-ESCodexPropertyValue $Record 'availabilityUpdatedUtc' '')
        availabilityExpiresUtc = [string](Get-ESCodexPropertyValue $Record 'availabilityExpiresUtc' '')
        activityKey = [string](Get-ESCodexPropertyValue $Record 'activityKey' '')
        activitySummary = [string](Get-ESCodexPropertyValue $Record 'activitySummary' '')
        acceptanceResponsibilityKey = [string](Get-ESCodexPropertyValue $Record 'acceptanceResponsibilityKey' '')
        acceptanceOnCompletion = [bool](Get-ESCodexPropertyValue $Record 'acceptanceOnCompletion' $false)
        acceptanceBindingUpdatedUtc = [string](Get-ESCodexPropertyValue $Record 'acceptanceBindingUpdatedUtc' '')
        lastAcceptanceRequestTurnId = [string](Get-ESCodexPropertyValue $Record 'lastAcceptanceRequestTurnId' '')
    }
}

function Read-ESCodexSessionRegistry([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return [pscustomobject][ordered]@{
            schemaVersion = 2
            sourceSchemaVersion = 2
            requiresWriteUpgrade = $false
            revision = 0
            updatedUtc = ''
            sessions = @()
        }
    }
    $source = $null
    $lastReadError = $null
    for ($attempt = 0; $attempt -lt 6; $attempt++) {
        try {
            $source = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 -ErrorAction Stop | ConvertFrom-Json
            $lastReadError = $null
            break
        }
        catch {
            $lastReadError = $_
            $message = $_.Exception.Message
            if ($message -notmatch 'being used by another process|sharing violation|cannot access the file') { break }
            if ($attempt -lt 5) { Start-Sleep -Milliseconds (25 * ($attempt + 1)) }
        }
    }
    if ($null -ne $lastReadError -or $null -eq $source) {
        $detail = if ($null -eq $lastReadError) { 'Registry content was empty.' } else { $lastReadError.Exception.Message }
        throw "Codex session registry is invalid: $Path`n$detail"
    }
    $sessions = @(Get-ESCodexPropertyValue $source 'sessions' @()) | ForEach-Object { ConvertTo-ESCodexSessionRecord $_ }
    $sourceSchemaVersion = [int](Get-ESCodexPropertyValue $source 'schemaVersion' (Get-ESCodexPropertyValue $source 'version' 1))
    $duplicateTokens = @($sessions | Where-Object { -not [string]::IsNullOrWhiteSpace($_.launchToken) } | Group-Object launchToken | Where-Object Count -gt 1)
    if ($duplicateTokens.Count -gt 0) {
        throw "Codex session registry contains duplicate launchToken values: $Path"
    }
    $duplicateRecordIds = @($sessions | Group-Object recordId | Where-Object Count -gt 1)
    if ($duplicateRecordIds.Count -gt 0) {
        throw "Codex session registry contains duplicate recordId values: $Path"
    }
    $duplicateSessionIds = @($sessions | Where-Object { -not [string]::IsNullOrWhiteSpace($_.sessionId) } | Group-Object sessionId | Where-Object Count -gt 1)
    if ($duplicateSessionIds.Count -gt 0) {
        throw "Codex session registry contains duplicate sessionId values: $Path"
    }
    return [pscustomobject][ordered]@{
        schemaVersion = 2
        sourceSchemaVersion = $sourceSchemaVersion
        requiresWriteUpgrade = $sourceSchemaVersion -ne 2
        revision = [int](Get-ESCodexPropertyValue $source 'revision' 0)
        updatedUtc = [string](Get-ESCodexPropertyValue $source 'updatedUtc' '')
        sessions = @($sessions)
    }
}

function Save-ESCodexSessionRegistry([string]$Path, [object]$Registry) {
    $resolvedPath = [IO.Path]::GetFullPath($Path)
    $root = Get-ESCodexLocalStateRoot
    $root = [IO.Path]::GetFullPath($root)
    if (-not $resolvedPath.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Session registry path must remain inside the approved ESFramework CodexSessions state root.'
    }
    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($parent)) { [void][IO.Directory]::CreateDirectory($parent) }
    $nextRevision = [int]$Registry.revision + 1
    $updatedUtc = [DateTime]::UtcNow.ToString('o')
    $payload = [ordered]@{
        schemaVersion = 2
        revision = $nextRevision
        updatedUtc = $updatedUtc
        sessions = @($Registry.sessions)
    }
    $retryCount = 5
    $lastCommitError = $null
    $committed = $false
    for ($attempt = 0; $attempt -lt $retryCount; $attempt++) {
        $temporary = $Path + '.tmp-' + [Guid]::NewGuid().ToString('N')
        try {
            [IO.File]::WriteAllText($temporary, ($payload | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
            if (Test-Path -LiteralPath $Path -PathType Leaf) {
                $backup = $Path + '.bak-' + [Guid]::NewGuid().ToString('N')
                try {
                    [IO.File]::Replace($temporary, $Path, $backup)
                }
                finally {
                    if (Test-Path -LiteralPath $backup -PathType Leaf) {
                        try { Remove-Item -LiteralPath $backup -Force -ErrorAction Stop }
                        catch {
                            Write-Warning ("Unable to remove backup state file '" + $backup + "': " + $_.Exception.Message)
                        }
                    }
                }
            }
            else {
                # Another writer may create the file after this test. The next bounded retry
                # will take the replace path and never overwrite an unverified registry.
                [IO.File]::Move($temporary, $Path)
            }
            $committed = $true
            break
        }
        catch [IO.IOException] {
            $lastCommitError = $_.Exception
            if ($attempt -lt ($retryCount - 1)) {
                Start-Sleep -Milliseconds (60 * ($attempt + 1))
            }
        }
        finally {
            if (Test-Path -LiteralPath $temporary -PathType Leaf) {
                try { Remove-Item -LiteralPath $temporary -Force -ErrorAction Stop }
                catch {
                    Write-Warning ("Unable to remove temporary state file '" + $temporary + "': " + $_.Exception.Message)
                }
            }
        }
    }
    if (-not $committed) {
        $detail = if ($null -eq $lastCommitError) { 'unknown file commit failure' } else { $lastCommitError.Message }
        throw "Codex Session Registry 正在被其他会话提交，已重试 $retryCount 次但未取得文件提交权。原有登记保持不变；请稍后重试。原因：$detail"
    }
    Set-ESCodexPropertyValue $Registry 'schemaVersion' 2
    Set-ESCodexPropertyValue $Registry 'sourceSchemaVersion' 2
    Set-ESCodexPropertyValue $Registry 'requiresWriteUpgrade' $false
    Set-ESCodexPropertyValue $Registry 'revision' $nextRevision
    Set-ESCodexPropertyValue $Registry 'updatedUtc' $updatedUtc
}

function Invoke-ESCodexRegistryUpdate([string]$Path, [scriptblock]$Update, [int]$ExpectedRevision = -1, [object]$Argument = $null) {
    $mutex = [Threading.Mutex]::new($false, 'ESFrameworkCodexSessionRegistryV2')
    $acquired = $false
    try {
        $acquired = $mutex.WaitOne(5000)
        if (-not $acquired) { throw 'Timed out waiting for the Codex session registry mutex.' }
        $registry = Read-ESCodexSessionRegistry $Path
        if ($ExpectedRevision -ge 0 -and [int]$registry.revision -ne $ExpectedRevision) {
            throw "Registry revision conflict. Expected $ExpectedRevision but found $([int]$registry.revision). Refresh Query/Status and retry."
        }
        $result = $Update.Invoke($registry, $Argument)
        Save-ESCodexSessionRegistry $Path $registry
        return $result
    }
    finally {
        if ($acquired) { $mutex.ReleaseMutex() }
        $mutex.Dispose()
    }
}

function AddOrUpdate-ESCodexSessionRecord([object]$Registry, [object]$Record) {
    $identityParts = @('launchToken', 'sessionId', 'taskFingerprint', 'taskKey', 'tabTitle') | ForEach-Object { [string](Get-ESCodexPropertyValue $Record $_ '') }
    if (@($identityParts | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count -eq 0) {
        throw 'Session record must contain at least one authoritative launch, session, task, or tab identity field.'
    }
    $normalized = ConvertTo-ESCodexSessionRecord $Record
    $matches = @($Registry.sessions | Where-Object {
            (-not [string]::IsNullOrWhiteSpace($normalized.launchToken) -and [string]$_.launchToken -eq $normalized.launchToken) -or
            (-not [string]::IsNullOrWhiteSpace($normalized.sessionId) -and [string]$_.sessionId -eq $normalized.sessionId)
        })
    if ($matches.Count -gt 0) {
        $match = @($matches | Sort-Object @{ Expression = {
                        $parsed = [DateTime]::MaxValue
                        [void][DateTime]::TryParse([string]$_.registeredAtUtc, [ref]$parsed)
                        $parsed
                    } }, recordId | Select-Object -First 1)[0]
        $normalized.recordId = [string]$match.recordId
        if (-not [string]::IsNullOrWhiteSpace([string]$match.registeredAtUtc)) { $normalized.registeredAtUtc = [string]$match.registeredAtUtc }
        $matchedRecordIds = @($matches | ForEach-Object { [string]$_.recordId })
        $Registry.sessions = @($Registry.sessions | Where-Object { [string]$_.recordId -notin $matchedRecordIds })
    }
    $Registry.sessions = @($Registry.sessions) + $normalized
    return $normalized
}

function Test-ESCodexProcessAlive([int]$ProcessId) {
    if ($ProcessId -le 0) { return $false }
    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    return $null -ne $process -and -not $process.HasExited
}

function Get-ESCodexTerminalHostProcessId([int]$ShellProcessId) {
    if ($ShellProcessId -le 0) { return 0 }
    $currentProcessId = $ShellProcessId
    for ($depth = 0; $depth -lt 8 -and $currentProcessId -gt 0; $depth++) {
        try {
            $process = Get-CimInstance Win32_Process -Filter "ProcessId=$currentProcessId" -OperationTimeoutSec 1 -ErrorAction Stop
            if ($null -eq $process) { break }
            $parentProcessId = [int]$process.ParentProcessId
            if ($parentProcessId -le 0 -or $parentProcessId -eq $currentProcessId) { break }
            $parent = Get-Process -Id $parentProcessId -ErrorAction Stop
            $processName = [string]$parent.ProcessName
            if ($processName -eq 'wt' -or $processName.StartsWith('WindowsTerminal', [StringComparison]::OrdinalIgnoreCase)) {
                return $parentProcessId
            }
            $currentProcessId = $parentProcessId
        }
        catch { break }
    }
    return 0
}

function Get-ESCodexCurrentProcessContext(
    [string]$LaunchToken = '',
    [switch]$IncludeProcessAncestry
) {
    $tokens = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    if (-not [string]::IsNullOrWhiteSpace($LaunchToken)) { [void]$tokens.Add($LaunchToken.Trim()) }
    if (-not [string]::IsNullOrWhiteSpace($env:ES_CODEX_LAUNCH_TOKEN)) { [void]$tokens.Add($env:ES_CODEX_LAUNCH_TOKEN.Trim()) }
    $ancestorProcessIds = [Collections.Generic.List[int]]::new()
    $processAncestryAttempted = $false
    $processAncestryComplete = $false
    if ($IncludeProcessAncestry) {
        $processAncestryAttempted = $true
        $processId = $PID
        for ($depth = 0; $depth -lt 8 -and $processId -gt 0; $depth++) {
            if (-not $ancestorProcessIds.Contains($processId)) { $ancestorProcessIds.Add($processId) }
            try {
                $process = Get-CimInstance Win32_Process -Filter "ProcessId=$processId" -OperationTimeoutSec 1 -ErrorAction Stop
                if ($null -eq $process) { break }
                foreach ($match in [regex]::Matches([string]$process.CommandLine, 'CodexLaunch:[A-Za-z0-9:-]+')) {
                    [void]$tokens.Add($match.Value)
                }
                $nextProcessId = [int]$process.ParentProcessId
                if ($nextProcessId -eq $processId) { break }
                $processId = $nextProcessId
            }
            catch { break }
        }
        $processAncestryComplete = $processId -le 0
    }
    return [pscustomobject][ordered]@{
        launchTokens = @($tokens)
        wtSession = [string]$env:WT_SESSION
        ancestorProcessIds = @($ancestorProcessIds)
        currentProcessId = $PID
        processAncestryAttempted = $processAncestryAttempted
        processAncestryComplete = $processAncestryComplete
    }
}

function Find-ESCodexSessionIdByToken([string]$HistoryPath, [string]$Token, [long]$StartedAtUnix = 0) {
    if ([string]::IsNullOrWhiteSpace($Token) -or -not (Test-Path -LiteralPath $HistoryPath -PathType Leaf)) { return '' }
    foreach ($line in (Get-Content -LiteralPath $HistoryPath -Tail 5000 -Encoding UTF8)) {
        try {
            $row = $line | ConvertFrom-Json
            if ([string]$row.text -like ('*' + $Token + '*') -and ([long]$row.ts -ge $StartedAtUnix)) {
                return [string]$row.session_id
            }
        }
        catch {
            Write-Verbose ("Ignoring malformed Codex history line while resolving session: " + $_.Exception.Message)
        }
    }
    return ''
}

function Get-ESCodexVisibleTerminalTabs {
    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes
    $windowCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Window)
    $tabCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::TabItem)
    $tabs = @()
    $desktop = [System.Windows.Automation.AutomationElement]::RootElement
    foreach ($window in @($desktop.FindAll([System.Windows.Automation.TreeScope]::Children, $windowCondition))) {
        if ([string]$window.Current.ClassName -ne 'CASCADIA_HOSTING_WINDOW_CLASS') { continue }
        foreach ($tab in @($window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $tabCondition))) {
            $tabs += [pscustomobject]@{
                windowProcessId = [int]$window.Current.ProcessId
                windowHandle = [int64]$window.Current.NativeWindowHandle
                windowTitle = [string]$window.Current.Name
                title = [string]$tab.Current.Name
                element = $tab
            }
        }
    }
    return $tabs
}
