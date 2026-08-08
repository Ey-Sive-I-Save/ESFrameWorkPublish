[CmdletBinding()]
param(
    [string]$SessionId = '',
    [string]$TaskKey = '',
    [string]$ResponsibilityKey = '',
    [string]$TabTitle = '',
    [switch]$AllMatches,
    [switch]$DryRun,
    [string]$StateRoot = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ESCodexSessionState.ps1')

function Close-WindowsTerminalTab([object]$Tab) {
    $closeCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        'CloseButton')
    $buttons = $Tab.element.FindAll([System.Windows.Automation.TreeScope]::Descendants, $closeCondition)
    if ($buttons.Count -ne 1) { throw "Expected one close button for tab: $($Tab.title)" }
    $pattern = $buttons[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
}

if (@($SessionId, $TaskKey, $ResponsibilityKey, $TabTitle | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) }).Count -eq 0) {
    throw 'Pass SessionId, TaskKey, ResponsibilityKey, or TabTitle. Closing an unspecified tab is forbidden.'
}
if (-not [string]::IsNullOrWhiteSpace($SessionId)) {
    $parsedSessionId = [Guid]::Empty
    if (-not [Guid]::TryParse($SessionId.Trim(), [ref]$parsedSessionId)) { throw 'SessionId must be an exact Codex session UUID.' }
    $SessionId = $parsedSessionId.ToString()
}

$localStateRoot = if ([string]::IsNullOrWhiteSpace($StateRoot)) { Get-ESCodexLocalStateRoot } else { [IO.Path]::GetFullPath($StateRoot) }
$registryPath = Join-Path $localStateRoot 'sessions.json'
$registry = Read-ESCodexSessionRegistry $registryPath
$records = @($registry.sessions | Where-Object {
        ([string]::IsNullOrWhiteSpace($SessionId) -or [string]$_.sessionId -eq $SessionId) -and
        ([string]::IsNullOrWhiteSpace($TaskKey) -or [string]$_.taskKey -eq $TaskKey) -and
        ([string]::IsNullOrWhiteSpace($ResponsibilityKey) -or [string]$_.responsibilityKey -eq $ResponsibilityKey) -and
        ([string]::IsNullOrWhiteSpace($TabTitle) -or [string]$_.tabTitle -eq $TabTitle) -and
        [string]$_.lifecycleStatus -ne 'Closed'
    })
if ($records.Count -eq 0) { throw 'No authoritative ES Codex registry record matched the requested selector.' }
if ($records.Count -gt 1 -and -not $AllMatches) {
    $candidates = @($records | ForEach-Object { "$($_.sessionId) | $($_.responsibilityKey) | $($_.tabTitle) | $($_.recordId)" }) -join "`n"
    throw "The selector matched multiple registry records. Pass an exact SessionId or explicitly use -AllMatches:`n$candidates"
}

$titles = @($records | ForEach-Object { [string]$_.tabTitle } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
$allTabs = @(Get-ESCodexVisibleTerminalTabs)
$uiMatches = @($allTabs | Where-Object { $_.title -in $titles })
$ambiguousTitles = @($uiMatches | Group-Object title | Where-Object Count -gt 1)
if ($ambiguousTitles.Count -gt 0 -and -not $AllMatches) {
    $details = @($ambiguousTitles | ForEach-Object { "$($_.Name) x$($_.Count)" }) -join "`n"
    throw "Visible tab identity is ambiguous. Title-only fallback will not close automatically:`n$details"
}
if (-not [string]::IsNullOrWhiteSpace($SessionId) -and $uiMatches.Count -gt 1) {
    throw 'An exact SessionId resolved to multiple visible title matches; refusing to guess which tab belongs to the session.'
}
foreach ($group in @($uiMatches | Group-Object title)) {
    $registeredTitleCount = @($records | Where-Object { [string]$_.tabTitle -eq [string]$group.Name }).Count
    if ($group.Count -gt $registeredTitleCount) {
        throw "Visible tabs outnumber authoritative records for title '$($group.Name)'; refusing to close potentially unrelated tabs."
    }
}

$status = & (Join-Path $PSScriptRoot 'Get-ESCodexSessionStatus.ps1') -IncludeClosed -StateRoot $localStateRoot
$selectedStatus = @($status.sessions | Where-Object { [string]$_.recordId -in @($records.recordId) })
foreach ($observation in $selectedStatus) {
    if ([string]$observation.terminalMode -ne 'PlainCmd' -and $observation.processAlive -and [int]$observation.visibleTabCount -eq 0) {
        throw "The registered process is alive but its visual tab cannot be located safely: $($observation.sessionId)"
    }
}

$plainCmdRecords = @($selectedStatus | Where-Object { $_.terminalMode -eq 'PlainCmd' -and $_.processAlive })
$result = [ordered]@{
    mode = 'Close'
    selector = [ordered]@{ sessionId = $SessionId; taskKey = $TaskKey; responsibilityKey = $ResponsibilityKey; tabTitle = $TabTitle }
    matchedRecordIds = @($records.recordId)
    matchedTitles = $titles
    visibleTabCount = $uiMatches.Count
    plainCmdCount = $plainCmdRecords.Count
    identityBasis = 'authoritative-registry + unique-visible-title'
    closedVisibleTabs = 0
    closedPlainCmdWindows = 0
    alreadyClosed = $false
    dryRun = [bool]$DryRun
    success = $false
}

if ($DryRun) {
    $result.success = $true
    [pscustomobject]$result
    return
}

foreach ($tab in $uiMatches) {
    Close-WindowsTerminalTab $tab
    $result.closedVisibleTabs++
}
foreach ($record in $plainCmdRecords) {
    $process = Get-Process -Id ([int]$record.processId) -ErrorAction SilentlyContinue
    if ($null -eq $process) { continue }
    [void]$process.CloseMainWindow()
    if (-not $process.WaitForExit(3000)) { & taskkill.exe /PID ([int]$record.processId) /T /F | Out-Null }
    $result.closedPlainCmdWindows++
}

Start-Sleep -Milliseconds 500
$remainingTabs = @(Get-ESCodexVisibleTerminalTabs | Where-Object { $_.title -in $titles })
$remainingPlain = @($plainCmdRecords | Where-Object { Test-ESCodexProcessAlive ([int]$_.processId) })
$result.alreadyClosed = $uiMatches.Count -eq 0 -and $plainCmdRecords.Count -eq 0
$result.success = $remainingTabs.Count -eq 0 -and $remainingPlain.Count -eq 0

if ($result.success) {
    $recordIds = @($records.recordId)
    $closeUpdateContext = [pscustomobject]@{ recordIds = @($recordIds) }
    Invoke-ESCodexRegistryUpdate -Path $registryPath -Update {
        param($currentRegistry, $context)
        foreach ($record in @($currentRegistry.sessions | Where-Object { [string]$_.recordId -in @($context.recordIds) })) {
            $record.processId = 0
            $record.lifecycleStatus = 'Closed'
            $record.closedAtUtc = [DateTime]::UtcNow.ToString('o')
            $record.lastSeenUtc = $record.closedAtUtc
        }
    } -Argument $closeUpdateContext | Out-Null
}

[pscustomobject]$result
if (-not $result.success) { exit 1 }
