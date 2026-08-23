[CmdletBinding()]
param(
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

function Read-JsonFile([string]$Path) {
    return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Save-JsonAtomic([string]$Path, [object]$Value) {
    $temporary = $Path + '.tmp-' + [Guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText($temporary, ($Value | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        $backup = $Path + '.bak-' + [Guid]::NewGuid().ToString('N')
        [IO.File]::Replace($temporary, $Path, $backup)
        if (Test-Path -LiteralPath $backup -PathType Leaf) { Remove-Item -LiteralPath $backup -Force }
    }
    else {
        [IO.File]::Move($temporary, $Path)
    }
}

function Set-Property([object]$Target, [string]$Name, [object]$Value) {
    if ($null -eq $Target.PSObject.Properties[$Name]) {
        $Target | Add-Member -NotePropertyName $Name -NotePropertyValue $Value
    }
    else {
        $Target.$Name = $Value
    }
}

function Test-ProcessAlive([int]$ProcessId) {
    if ($ProcessId -le 0) { return $false }
    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    return $null -ne $process -and -not $process.HasExited
}

$localStateBase = if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
    Join-Path $env:LOCALAPPDATA 'ESFramework\CodexSessions'
}
else {
    Join-Path ([IO.Path]::GetTempPath()) 'ESFramework-CodexSessions'
}
$envelopeRoot = Join-Path $localStateBase 'envelopes'
$legacyRoot = Join-Path $localStateBase 'legacy-v1\envelopes'
$legacyRegistryRoot = Join-Path $localStateBase 'legacy-v1\registry'
$legacyLaunchStateRoot = Join-Path $localStateBase 'legacy-v1\launch-state'
$launchStateRoot = Join-Path $localStateBase 'launch-state'
$registryPath = Join-Path $localStateBase 'sessions.json'
$legacyMap = @{}
$v1Envelopes = @()

if (Test-Path -LiteralPath $envelopeRoot -PathType Container) {
    foreach ($file in @(Get-ChildItem -LiteralPath $envelopeRoot -File -Filter '*.json')) {
        try {
            $envelope = Read-JsonFile $file.FullName
            if ([int]$envelope.schemaVersion -eq 1) {
                $destination = Join-Path $legacyRoot $file.Name
                $v1Envelopes += [pscustomobject]@{
                    source = $file.FullName
                    destination = $destination
                    launchToken = [string]$envelope.launchToken
                    taskKey = [string]$envelope.taskKey
                    responsibilityKey = [string]$envelope.responsibilityKey
                }
                $legacyMap[$file.FullName.ToLowerInvariant()] = $destination
            }
        }
        catch {
            throw "Invalid launch envelope prevents V2 migration: $($file.FullName)"
        }
    }
}

$registryUpdates = 0
$launchStateUpdates = 0
$legacyRegistryEntries = @()
$deadLegacyLaunchStates = @()
$invalidLegacyLaunchStates = @()
if (Test-Path -LiteralPath $registryPath -PathType Leaf) {
    $registry = Read-JsonFile $registryPath
    foreach ($session in @($registry.sessions)) {
        $key = ([string]$session.envelopePath).ToLowerInvariant()
        if ($legacyMap.ContainsKey($key)) { $registryUpdates++ }
        if ([bool]$session.requiresV2Resume -and [string]$session.responsibilityKey -eq 'launcher-smoke') {
            $legacyRegistryEntries += $session
        }
    }
}
if (Test-Path -LiteralPath $launchStateRoot -PathType Container) {
    foreach ($file in @(Get-ChildItem -LiteralPath $launchStateRoot -File -Filter '*.json')) {
        try {
            $state = Read-JsonFile $file.FullName
            $key = ([string]$state.envelopePath).ToLowerInvariant()
            if ($legacyMap.ContainsKey($key)) { $launchStateUpdates++ }
            if ([bool]$state.requiresV2Resume -and -not (Test-ProcessAlive ([int]$state.processId))) {
                $deadLegacyLaunchStates += [pscustomobject]@{
                    source = $file.FullName
                    destination = Join-Path $legacyLaunchStateRoot $file.Name
                }
            }
        }
        catch {
            # A malformed legacy state must not be silently treated as migrated.
            # Keep migration best-effort, but surface the rejected artifact in the receipt.
            $invalidLegacyLaunchStates += [pscustomobject]@{
                source = $file.FullName
                error = $_.Exception.Message
            }
        }
    }
}

$result = [ordered]@{
    mode = 'MigrateToV2'
    localStateBase = $localStateBase
    legacyEnvelopeDirectory = $legacyRoot
    v1EnvelopeCount = $v1Envelopes.Count
    registryUpdates = $registryUpdates
    launchStateUpdates = $launchStateUpdates
    invalidLegacyLaunchStates = @($invalidLegacyLaunchStates)
    legacyRegistryEntryCount = $legacyRegistryEntries.Count
    deadLegacyLaunchStateCount = $deadLegacyLaunchStates.Count
    dryRun = [bool]$DryRun
    migrated = $false
    legacyEnvelopes = $v1Envelopes
}
if ($DryRun) {
    [pscustomobject]$result
    return
}

if ($v1Envelopes.Count -gt 0) {
    [void][IO.Directory]::CreateDirectory($legacyRoot)
    foreach ($item in $v1Envelopes) {
        if (Test-Path -LiteralPath $item.destination) {
            throw "Legacy V1 destination already exists: $($item.destination)"
        }
        Move-Item -LiteralPath $item.source -Destination $item.destination
    }
}

if (Test-Path -LiteralPath $registryPath -PathType Leaf) {
    $registry = Read-JsonFile $registryPath
    foreach ($session in @($registry.sessions)) {
        $key = ([string]$session.envelopePath).ToLowerInvariant()
        if (-not $legacyMap.ContainsKey($key)) { continue }
        Set-Property $session 'legacyEnvelopePath' $legacyMap[$key]
        Set-Property $session 'envelopePath' $legacyMap[$key]
        Set-Property $session 'requiresV2Resume' $true
    }
    if ($legacyRegistryEntries.Count -gt 0) {
        [void][IO.Directory]::CreateDirectory($legacyRegistryRoot)
        foreach ($session in $legacyRegistryEntries) {
            $archivePath = Join-Path $legacyRegistryRoot (([string]$session.sessionId) + '.json')
            if (Test-Path -LiteralPath $archivePath) {
                throw "Legacy registry archive already exists: $archivePath"
            }
            [IO.File]::WriteAllText($archivePath, ($session | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
        }
        $registry.sessions = @($registry.sessions | Where-Object {
                -not ([bool]$_.requiresV2Resume -and [string]$_.responsibilityKey -eq 'launcher-smoke')
            })
    }
    Save-JsonAtomic $registryPath $registry
}

if (Test-Path -LiteralPath $launchStateRoot -PathType Container) {
    foreach ($file in @(Get-ChildItem -LiteralPath $launchStateRoot -File -Filter '*.json')) {
        try {
            $state = Read-JsonFile $file.FullName
            $key = ([string]$state.envelopePath).ToLowerInvariant()
            if (-not $legacyMap.ContainsKey($key)) { continue }
            Set-Property $state 'legacyEnvelopePath' $legacyMap[$key]
            Set-Property $state 'envelopePath' $legacyMap[$key]
            Set-Property $state 'requiresV2Resume' $true
            Save-JsonAtomic $file.FullName $state
        }
        catch {
            throw "Launch state migration failed: $($file.FullName)`n$($_.Exception.Message)"
        }
    }
}

if ($deadLegacyLaunchStates.Count -gt 0) {
    [void][IO.Directory]::CreateDirectory($legacyLaunchStateRoot)
    foreach ($item in $deadLegacyLaunchStates) {
        if (Test-Path -LiteralPath $item.destination) {
            throw "Legacy launch-state destination already exists: $($item.destination)"
        }
        Move-Item -LiteralPath $item.source -Destination $item.destination
    }
}

$result.migrated = $true
[pscustomobject]$result
