[CmdletBinding()]
param([string]$ProjectRoot)

$ErrorActionPreference = 'Stop'
$strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
$root = if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..\..'))
} else {
    [System.IO.Path]::GetFullPath($ProjectRoot)
}

function Add-Finding {
    param([System.Collections.Generic.List[object]]$List, [string]$Id, [bool]$Passed, [string]$Message)
    [void]$List.Add([ordered]@{
        id = $Id
        status = if ($Passed) { 'passed' } else { 'failed' }
        message = $Message
    })
}

function Read-StrictUtf8 {
    param([string]$Path)
    return $strictUtf8.GetString([System.IO.File]::ReadAllBytes($Path))
}

function From-CodePoints {
    param([int[]]$CodePoint)
    return (-join ($CodePoint | ForEach-Object { [char]$_ }))
}

$statusTitle = 'DSH ' + (From-CodePoints 0x63A5, 0x5165, 0x72B6, 0x6001)
$connectedLabel = 'DSH ' + [char]0x00B7 + ' ' + (From-CodePoints 0x5DF2, 0x63A5, 0x5165)
$notConnectedLabel = 'DSH ' + [char]0x00B7 + ' ' + (From-CodePoints 0x672A, 0x63A5, 0x5165)
$roleLabel = From-CodePoints 0x9AD8, 0x6743, 0x5A01, 0x5F00, 0x53D1, 0x8D21, 0x732E, 0x5C42
$authorityLabel = 'ES ' + (From-CodePoints 0x4FDD, 0x7559, 0x6700, 0x7EC8, 0x9A8C, 0x6536, 0x6743)
$recoveryLabel = From-CodePoints 0x6062, 0x590D

$findings = [System.Collections.Generic.List[object]]::new()
$centerPath = Join-Path $root 'Assets/Plugins/ES/Editor/ESAutomation/ESAutomationCenter.cs'
$bridgePath = Join-Path $root 'Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs'
if (-not (Test-Path -LiteralPath $centerPath -PathType Leaf)) {
    Add-Finding $findings 'editor-center-file' $false 'ESAutomationCenter.cs is missing.'
} else {
    try {
        $center = Read-StrictUtf8 $centerPath
        Add-Finding $findings 'status-section' ($center.Contains('DrawDeepSeekHarnessStatus') -and $center.Contains($statusTitle)) 'The DSH status section is registered in the Automation Center.'
        Add-Finding $findings 'connection-states' ($center.Contains($connectedLabel) -and $center.Contains($notConnectedLabel)) 'Connected and NotConnected labels are both present.'
        Add-Finding $findings 'role-display' ($center.Contains($roleLabel) -and $center.Contains($authorityLabel)) 'The DSH role and ES final authority are displayed.'
        Add-Finding $findings 'recovery-display' ($center.Contains($recoveryLabel) -and $center.Contains('RunLocalCheck(true)')) 'A recovery message and bounded local check action are present.'
        Add-Finding $findings 'unity-icon' $center.Contains('d_CloudConnect') 'The DSH Unity icon is declared.'
    }
    catch { Add-Finding $findings 'editor-center-utf8' $false 'ESAutomationCenter.cs is not valid strict UTF-8.' }
}
if (-not (Test-Path -LiteralPath $bridgePath -PathType Leaf)) {
    Add-Finding $findings 'bridge-file' $false 'ESAutomationAiBridge.cs is missing.'
} else {
    try {
        $bridge = Read-StrictUtf8 $bridgePath
        Add-Finding $findings 'registration' $bridge.Contains('ESDeepSeekHarnessAutomation.Register()') 'The DSH automation registration hook is present.'
    }
    catch { Add-Finding $findings 'bridge-utf8' $false 'ESAutomationAiBridge.cs is not valid strict UTF-8.' }
}

$failed = @($findings | Where-Object { $_.status -eq 'failed' })
[ordered]@{
    schemaVersion = 1
    validator = 'Test-ESDeepSeekHarnessUi'
    status = if ($failed.Count -eq 0) { 'passed' } else { 'failed' }
    findings = @($findings)
    staticStatus = if ($failed.Count -eq 0) { 'static-passed' } else { 'static-failed' }
    runtimeStatus = 'runtime-not-run'
    claimsNotProven = @('Unity compile/ReloadDomain', 'actual EditorWindow rendering', 'interaction and visual behavior')
} | ConvertTo-Json -Depth 8
if ($failed.Count -gt 0) { exit 1 }
