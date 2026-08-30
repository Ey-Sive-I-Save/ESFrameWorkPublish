[CmdletBinding()]
param(
    [string]$ProjectPath = '',
    [string]$TaskPrompt = '',
    [string]$TaskKey = 'current-tab-recycle',
    [string]$ResponsibilityKey = 'session-context-review',
    [string]$TabTitle = '',
    [ValidateSet('Auto','CurrentWindow','ProjectWindow','NewWindow','PlainCmd')][string]$TerminalMode = 'CurrentWindow',
    [string]$TerminalWindowName = 'ESFramework',
    [ValidateRange(10,300)][int]$AcceptanceWaitSeconds = 90,
    [switch]$SkipHooks,
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$projectRoot = if ([string]::IsNullOrWhiteSpace($ProjectPath)) { Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))) } else { (Resolve-Path -LiteralPath $ProjectPath).Path }
$localBase = if ($env:LOCALAPPDATA) { Join-Path $env:LOCALAPPDATA 'ESFramework\CodexSessions' } else { Join-Path ([IO.Path]::GetTempPath()) 'ESFramework-CodexSessions' }
$registryPath = Join-Path $localBase 'sessions.json'
if (-not (Test-Path -LiteralPath $registryPath -PathType Leaf)) { throw 'Current-tab recycle requires a registered source session.' }
$registry = Get-Content -LiteralPath $registryPath -Raw -Encoding UTF8 | ConvertFrom-Json
$launchToken = [string]$env:ES_CODEX_LAUNCH_TOKEN
$source = @($registry.sessions | Where-Object { [string]$_.launchToken -eq $launchToken } | Select-Object -First 1)[0]
if ($null -eq $source -or [string]::IsNullOrWhiteSpace([string]$source.sessionId)) { throw 'Current-tab recycle could not resolve the exact current SessionId from ES_CODEX_LAUNCH_TOKEN.' }
$sourceId = [string]$source.sessionId
$sourceEnvelope = [string]$source.envelopePath
$sessionRoot = Join-Path $env:USERPROFILE '.codex\sessions'
$sourceJsonl = Get-ChildItem -LiteralPath $sessionRoot -Recurse -Filter (('*' + $sourceId + '*.jsonl')) -File -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $sourceJsonl) { throw 'Current-tab recycle could not locate the exact current transcript JSONL.' }
$packetScript = Join-Path $PSScriptRoot 'New-ESCodexReadOnlyContext.ps1'
$launcher = Join-Path $PSScriptRoot 'New-ESCodexReadOnlyContext.ps1'
$effectiveTabTitle = if ([string]::IsNullOrWhiteSpace($TabTitle)) { 'ES-ContextRefresh' } else { $TabTitle }
$packet = & $packetScript -SourcePath $sourceJsonl.FullName -ProjectPath $projectRoot -TaskPrompt $TaskPrompt -TaskKey $TaskKey -ResponsibilityKey $ResponsibilityKey -TabTitle $effectiveTabTitle -TerminalMode $TerminalMode -TerminalWindowName $TerminalWindowName -SkipHooks:$SkipHooks -DryRun:$DryRun
if ($DryRun) { [pscustomobject][ordered]@{ operation='CurrentTabRecycle'; replacementMode='same-window-new-tab-then-close-source'; physicalTabReused=$false; samePhysicalTab=$false; sourceSessionId=$sourceId; sourceEnvelope=$sourceEnvelope; sourceTranscript=$sourceJsonl.FullName; dryRun=$true; next=$packet }; return }
$packetPath = [string]$packet.packetPath
$new = $packet.launch
if ($null -eq $new) { throw 'Read-only replacement launch returned no launch result.' }
$newToken = [string]$new.launchToken
$newEnvelope = [string]$new.envelopePath
$hash = [Security.Cryptography.SHA256]::Create()
try { $receiptName = ([BitConverter]::ToString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($newToken)))).Replace('-', '').ToLowerInvariant() + '.json' }
finally { $hash.Dispose() }
$receiptPath = Join-Path (Join-Path $localBase 'acceptance-receipts') $receiptName
$deadline = [DateTime]::UtcNow.AddSeconds($AcceptanceWaitSeconds)
$accepted = $false
while ([DateTime]::UtcNow -lt $deadline) {
    if (Test-Path -LiteralPath $receiptPath -PathType Leaf) {
        try {
            $receipt = Get-Content -LiteralPath $receiptPath -Raw -Encoding UTF8 | ConvertFrom-Json
            if ([string]$receipt.launchToken -eq $newToken -and [string]$receipt.envelopePath -eq $newEnvelope) { $accepted = $true; break }
        } catch { Write-Verbose ("acceptance receipt parse failed: {0}" -f $_.Exception.Message) }
    }
    Start-Sleep -Milliseconds 1000
}
if (-not $accepted) { throw "Replacement session was not accepted within $AcceptanceWaitSeconds seconds; source tab remains open. Receipt: $receiptPath" }
$closeScript = Join-Path $PSScriptRoot 'Start-ESCodexSession.ps1'
$closeArgs = @('-Mode','Close','-ProjectPath',$projectRoot,'-SessionId',$sourceId)
# Close the source asynchronously: the receiving session is already accepted,
# and the source Codex process must not be required to survive its own closure.
Start-Process -FilePath 'powershell.exe' -WindowStyle Hidden -ArgumentList (@('-NoLogo','-NoProfile','-NonInteractive','-ExecutionPolicy','RemoteSigned','-File',$closeScript) + $closeArgs) | Out-Null
[pscustomobject][ordered]@{
    operation = 'CurrentTabRecycle'
    # Frozen contract: transactional replacement, never physical tab reuse.
    replacementMode = 'same-window-new-tab-then-close-source'
    physicalTabReused = $false
    samePhysicalTab = $false
    sourceSessionId = $sourceId
    sourceEnvelope = $sourceEnvelope
    sourceCloseRequestedAfterAcceptance = $true
    sourceCloseVerification = 'deferred-to-close-operation'
    receivingSessionId = [string]$new.sessionId
    receivingRecordId = [string]$new.recordId
    receivingLaunchToken = $newToken
    receivingEnvelope = $newEnvelope
    receivingTab = [ordered]@{ terminalMode = [string]$new.terminalMode; terminalWindowName = [string]$new.terminalWindowName; tabTitle = [string]$new.tabTitle; responsibilityKey = [string]$new.responsibilityKey }
    readOnlyContext = $true
    resumeUsed = $false
    crossAiResume = $false
    acceptanceReceiptPath = $receiptPath
}
