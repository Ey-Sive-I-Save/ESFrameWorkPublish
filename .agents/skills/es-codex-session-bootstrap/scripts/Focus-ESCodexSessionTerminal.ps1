[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SessionId,
    [string]$RecordId = '',
    [string]$LaunchToken = '',
    [string]$StateRoot = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ESCodexSessionState.ps1')

$resolveArguments = @{ SessionId = $SessionId; RequireUnique = $true; StateRoot = $StateRoot }
if (-not [string]::IsNullOrWhiteSpace($RecordId)) { $resolveArguments.RecordId = $RecordId }
if (-not [string]::IsNullOrWhiteSpace($LaunchToken)) { $resolveArguments.LaunchToken = $LaunchToken }
$resolved = & (Join-Path $PSScriptRoot 'Resolve-ESCodexSessionRoute.ps1') @resolveArguments
$route = $resolved.route
if ($null -eq $route) { throw 'Terminal focus target could not be resolved uniquely.' }
if (-not [bool]$route.processAlive -or [int]$route.processId -le 0) {
    throw 'The exact managed terminal process is not alive. Refresh status or Resume the exact SessionId instead.'
}

$status = & (Join-Path $PSScriptRoot 'Get-ESCodexSessionStatus.ps1') `
    -SessionId ([string]$route.sessionId) `
    -RecordId ([string]$route.recordId) `
    -StateRoot $StateRoot
$record = @($status.sessions | Select-Object -First 1)[0]
if ($null -eq $record) { throw 'Terminal status did not return the exact managed session.' }

$terminalMode = [string]$record.terminalMode
$matchingTabs = @()
$tabSelected = $false
$windowHandle = [int64]0
if ($terminalMode -ne 'PlainCmd') {
    if (-not [bool]$status.uiAvailable) { throw 'Windows Terminal UI observation is unavailable; refusing to focus an unverified tab.' }
    $terminalWindowProcessId = [int]$record.terminalWindowProcessId
    if ($terminalWindowProcessId -le 0) {
        throw 'The terminal host process was not captured for this exact record. Refusing title-only focus; Resume the exact SessionId to create a fresh managed mapping.'
    }
    $matchingTabs = @(Get-ESCodexVisibleTerminalTabs | Where-Object {
            $_.windowProcessId -eq $terminalWindowProcessId -and $_.title -eq ([string]$record.tabTitle)
        })
    if ($matchingTabs.Count -ne 1) {
        throw "Expected one visible terminal tab for the exact record, observed $($matchingTabs.Count). Refusing ambiguous focus."
    }
    $tab = $matchingTabs[0]
    $selectionPattern = $tab.element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    if ($null -eq $selectionPattern) { throw 'The observed terminal tab does not expose a selection pattern.' }
    $selectionPattern.Select()
    $tabSelected = $true
    $windowHandle = [int64]$tab.windowHandle
}
else {
    $process = Get-Process -Id ([int]$record.processId) -ErrorAction Stop
    $windowHandle = [int64]$process.MainWindowHandle
    if ($windowHandle -eq 0) { throw 'The exact managed CMD process has no visible main window handle.' }
}

if ($windowHandle -eq 0) { throw 'The exact terminal window handle is unavailable.' }
if (-not ('ES.CodexTerminalFocus.NativeMethods' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace ES.CodexTerminalFocus
{
    public static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
'@
}

[ES.CodexTerminalFocus.NativeMethods]::ShowWindowAsync([IntPtr]$windowHandle, 9) | Out-Null
$foregroundAccepted = [ES.CodexTerminalFocus.NativeMethods]::SetForegroundWindow([IntPtr]$windowHandle)
[pscustomobject][ordered]@{
    focusContractVersion = 1
    success = $true
    recordId = [string]$record.recordId
    sessionId = [string]$record.sessionId
    processId = [int]$record.processId
    processAlive = [bool]$record.processAlive
    terminalMode = $terminalMode
    tabTitle = [string]$record.tabTitle
    terminalWindowProcessId = if ($terminalMode -eq 'PlainCmd') { 0 } else { [int]$record.terminalWindowProcessId }
    visibleTabCount = [int]$record.visibleTabCount
    windowHandle = $windowHandle
    tabSelected = $tabSelected
    foregroundRequested = $true
    foregroundAccepted = [bool]$foregroundAccepted
}
