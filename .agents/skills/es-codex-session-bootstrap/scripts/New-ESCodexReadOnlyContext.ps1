[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SourcePath,
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [string]$TaskPrompt = '',
    [string]$TaskKey = 'read-only-context',
    [string]$ResponsibilityKey = 'session-context-review',
    [string]$TabTitle = '',
    [ValidateSet('Auto','CurrentWindow','ProjectWindow','NewWindow','PlainCmd')][string]$TerminalMode = 'ProjectWindow',
    [string]$TerminalWindowName = 'ESFramework',
    [switch]$SkipHooks,
    [switch]$PrepareOnly,
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$projectRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
$source = (Resolve-Path -LiteralPath $SourcePath).Path
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Read-only source was not found: $source" }

function Get-Hash([string]$Path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '').ToLowerInvariant() }
    finally { $stream.Dispose(); $sha.Dispose() }
}
function Redact([string]$Text) {
    if ($null -eq $Text) { return '' }
    $value = [string]$Text
    $value = [regex]::Replace($value, '(?i)CodexLaunch:[A-Za-z0-9-]+', '[redacted-launch-token]')
    $value = [regex]::Replace($value, '(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b', '[redacted-session-id]')
    $value = [regex]::Replace($value, '(?i)(api[_-]?key|token|secret|password)\s*[:=]\s*[^\s,;]+', '$1=[redacted]')
    $value = [regex]::Replace($value, '(?i)([A-Z]:\\|\\\\)[^\r\n]+', '[redacted-path]')
    if ($value.Length -gt 24000) { $value = $value.Substring(0, 24000) + "`n[truncated]" }
    return $value
}
function Write-CreateOnly([string]$Path, [string]$Text) {
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $bytes = [Text.UTF8Encoding]::new($false).GetBytes($Text); $stream.Write($bytes,0,$bytes.Length); $stream.Flush($true) }
    finally { $stream.Dispose() }
}

$sourceHash = Get-Hash $source
$sourceKind = if ([IO.Path]::GetExtension($source) -ieq '.jsonl') { 'transcript' } else { 'semantic-archive' }
$entries = [Collections.Generic.List[object]]::new()
if ($sourceKind -eq 'transcript') {
    $lines = @(Get-Content -LiteralPath $source -Encoding UTF8 | Select-Object -Last 160)
    foreach ($line in $lines) {
        try {
            $row = $line | ConvertFrom-Json
            $role = [string]$row.role
            if ([string]::IsNullOrWhiteSpace($role)) { $role = [string]$row.type }
            $text = [string]$row.text
            if ([string]::IsNullOrWhiteSpace($text)) { $text = [string]$row.content }
            if (-not [string]::IsNullOrWhiteSpace($text)) { [void]$entries.Add([ordered]@{ role = $role; text = (Redact $text) }) }
        } catch { continue }
    }
}
else {
    $archive = Get-Content -LiteralPath $source -Raw -Encoding UTF8 | ConvertFrom-Json
    $safeArchive = [ordered]@{}
    foreach ($name in @('objective','state','importantData','archiveReason','recentScope','expectation','evidence')) {
        $property = $archive.PSObject.Properties[$name]
        if ($null -ne $property) { $safeArchive[$name] = $property.Value }
    }
    $entries.Add([ordered]@{ role='archive'; text=(Redact (($safeArchive | ConvertTo-Json -Depth 8))) })
}
$packet = [ordered]@{
    schemaVersion = 1
    packetKind = 'es-read-only-context'
    sourceMode = 'read-only'
    resumeUsed = $false
    crossAiResume = $false
    sourceKind = $sourceKind
    sourceSha256 = $sourceHash
    createdUtc = [DateTime]::UtcNow.ToString('o')
    objective = (Redact $TaskPrompt)
    entries = @($entries | Select-Object -Last 120)
    instructions = 'This packet is navigation-only historical context. Use current project source and rules as authority. Never Resume/Fork or inherit any old identity, token, window, or authorization.'
}
$localBase = if ($env:LOCALAPPDATA) { Join-Path $env:LOCALAPPDATA 'ESFramework\CodexSessions' } else { Join-Path ([IO.Path]::GetTempPath()) 'ESFramework-CodexSessions' }
$packetRoot = Join-Path $localBase 'read-only-contexts'
$packetPath = Join-Path $packetRoot ((Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N') + '.json')
if (-not $DryRun) { Write-CreateOnly $packetPath (($packet | ConvertTo-Json -Depth 8)) }
if ($DryRun) { $packetPath = Join-Path $packetRoot '<created-on-launch>' }
$sourcePacket = [pscustomobject][ordered]@{ operation='ReadOnlyRestore'; mode='New'; sourceMode='read-only'; resumeUsed=$false; crossAiResume=$false; packetPath=$packetPath; sourceSha256=$sourceHash }
if ($PrepareOnly) { $sourcePacket; return }
$launcher = Join-Path $PSScriptRoot 'Start-ESCodexSession.ps1'
$prompt = ($TaskPrompt + "`n`nRead-only historical context is navigation only. Never Resume/Fork or inherit an old SessionId, token, window, or authorization; current source and rules are authoritative.")
$effectiveTaskKey = if ([string]::IsNullOrWhiteSpace($TaskKey)) { 'read-only-context' } else { $TaskKey }
$effectiveResponsibilityKey = if ([string]::IsNullOrWhiteSpace($ResponsibilityKey)) { 'session-context-review' } else { $ResponsibilityKey }
$launchParams = @{ Mode='New'; ProjectPath=$projectRoot; TaskKey=$effectiveTaskKey; ResponsibilityKey=$effectiveResponsibilityKey; TabTitle=$TabTitle; TaskPrompt=$prompt; TerminalMode=$TerminalMode; TerminalWindowName=$TerminalWindowName; SkipHooks=$SkipHooks; DryRun=$DryRun }
if (-not $DryRun) { $launchParams.ReadOnlyContextPath = $packetPath }
$launch = & $launcher @launchParams
[pscustomobject][ordered]@{ operation='ReadOnlyRestore'; mode='New'; sourceMode='read-only'; resumeUsed=$false; crossAiResume=$false; packetPath=$packetPath; sourceSha256=$sourceHash; launch=$launch }
