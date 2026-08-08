[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EnvelopePath,

    [string]$ProjectPath = '',

    [string]$LaunchToken = '',

    [switch]$StrictGit
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

function Get-FileSha256([string]$Path) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
        $stream.Dispose()
    }
}

function Get-TextSha256([string]$Value) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes($Value)
        return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
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

$resolvedProjectRoot = if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $fixedProjectRoot
}
else {
    [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $ProjectPath).Path).TrimEnd('\')
}
if (-not $resolvedProjectRoot.Equals($fixedProjectRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "ProjectPath must resolve to the fixed ESFramework root: $fixedProjectRoot"
}

$localStateBase = if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
    Join-Path $env:LOCALAPPDATA 'ESFramework\CodexSessions'
}
else {
    Join-Path ([IO.Path]::GetTempPath()) 'ESFramework-CodexSessions'
}
$receiptRoot = Join-Path $localStateBase 'acceptance-receipts'
$requestedEnvelopePath = [IO.Path]::GetFullPath($EnvelopePath)
if (-not (Test-Path -LiteralPath $requestedEnvelopePath -PathType Leaf)) {
    if ([string]::IsNullOrWhiteSpace($LaunchToken)) {
        throw "Launch envelope is missing and no LaunchToken was supplied for accepted-context recovery: $requestedEnvelopePath"
    }
    $receiptPath = Join-Path $receiptRoot ((Get-TextSha256 $LaunchToken.Trim()) + '.json')
    if (-not (Test-Path -LiteralPath $receiptPath -PathType Leaf)) {
        throw "Launch envelope is missing and no prior acceptance receipt exists: $requestedEnvelopePath"
    }
    $receipt = Get-Content -LiteralPath $receiptPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $receiptValid = [string]$receipt.launchToken -eq $LaunchToken.Trim() -and
        [string]$receipt.envelopePath -eq $requestedEnvelopePath -and
        [int]$receipt.schemaVersion -eq 2 -and
        [string]$receipt.projectRoot -eq $resolvedProjectRoot
    [pscustomobject]@{
        valid = $receiptValid
        acceptedPreviously = $receiptValid
        envelopeAvailable = $false
        continuationMode = if ($receiptValid) { 'AcceptedContext' } else { 'HardFailure' }
        envelopePath = $requestedEnvelopePath
        receiptPath = $receiptPath
        launchToken = $LaunchToken.Trim()
        schemaVersion = [int]$receipt.schemaVersion
        handoffMode = 'AcceptedTranscriptOnly'
        handoffSnapshotDirectory = ''
        schemaValid = $receiptValid
        projectRootValid = [string]$receipt.projectRoot -eq $resolvedProjectRoot
        handoffFilesValid = $null
        gitValid = $null
        branchDrift = $null
        headDrift = $null
        handoffFiles = @()
        warning = 'The envelope is unavailable after prior acceptance. Continue only from already accepted transcript/context; do not substitute another handoff source or claim current artifact verification.'
    }
    if (-not $receiptValid) { exit 1 }
    return
}

$resolvedEnvelopePath = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $requestedEnvelopePath).Path)
$envelope = Get-Content -LiteralPath $resolvedEnvelopePath -Raw -Encoding UTF8 | ConvertFrom-Json
$schemaVersion = [int]$envelope.schemaVersion
$tokenValid = [string]::IsNullOrWhiteSpace($LaunchToken) -or [string]$envelope.launchToken -eq $LaunchToken.Trim()
$schemaValid = $schemaVersion -eq 2 -and [bool]$envelope.immutable -and $tokenValid
$rootValid = [string]$envelope.projectRoot -eq $resolvedProjectRoot
$snapshotRoot = [IO.Path]::GetFullPath((Join-Path $localStateBase 'handoff-snapshots')).TrimEnd('\')
$handoffResults = @()
$handoffValid = $true

foreach ($handoff in @($envelope.handoffFiles)) {
    $path = [string]$handoff.absolutePath
    $resolvedHandoffPath = if ([string]::IsNullOrWhiteSpace($path)) { '' } else { [IO.Path]::GetFullPath($path) }
    $snapshotPathValid = [bool]$handoff.snapshot -and $resolvedHandoffPath.StartsWith($snapshotRoot + '\', [StringComparison]::OrdinalIgnoreCase)
    $exists = Test-Path -LiteralPath $path -PathType Leaf
    $actualHash = if ($exists) { Get-FileSha256 $path } else { '' }
    $matches = $snapshotPathValid -and $exists -and $actualHash -eq [string]$handoff.sha256
    if (-not $matches) { $handoffValid = $false }
    $sourcePath = [string]$handoff.sourceAbsolutePath
    $sourceExists = -not [string]::IsNullOrWhiteSpace($sourcePath) -and (Test-Path -LiteralPath $sourcePath -PathType Leaf)
    $currentSourceHash = if ($sourceExists) { Get-FileSha256 $sourcePath } else { '' }
    $sourceDrift = (-not $sourceExists) -or $currentSourceHash -ne [string]$handoff.sourceSha256AtSnapshot
    $handoffResults += [pscustomobject]@{
        relativePath = [string]$handoff.relativePath
        absolutePath = $path
        snapshotPathValid = $snapshotPathValid
        exists = $exists
        expectedSha256 = [string]$handoff.sha256
        actualSha256 = $actualHash
        matches = $matches
        sourceAbsolutePath = $sourcePath
        sourceExists = $sourceExists
        sourceSha256AtSnapshot = [string]$handoff.sourceSha256AtSnapshot
        currentSourceSha256 = $currentSourceHash
        sourceDrift = $sourceDrift
    }
}

$currentBranch = [string](& git -C $resolvedProjectRoot branch --show-current 2>$null | Select-Object -First 1)
$currentHead = [string](& git -C $resolvedProjectRoot rev-parse HEAD 2>$null | Select-Object -First 1)
$branchDrift = $currentBranch -ne [string]$envelope.git.branch
$headDrift = $currentHead -ne [string]$envelope.git.head
$gitValid = -not ($branchDrift -or $headDrift)
$valid = $schemaValid -and $rootValid -and $handoffValid -and (-not $StrictGit -or $gitValid)
$effectiveLaunchToken = [string]$envelope.launchToken
$receiptPath = Join-Path $receiptRoot ((Get-TextSha256 $effectiveLaunchToken) + '.json')
$acceptedPreviously = Test-Path -LiteralPath $receiptPath -PathType Leaf
if ($valid) {
    $envelopeHash = Get-FileSha256 $resolvedEnvelopePath
    if ($acceptedPreviously) {
        $receipt = Get-Content -LiteralPath $receiptPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ([string]$receipt.launchToken -ne $effectiveLaunchToken -or
            [string]$receipt.envelopePath -ne $resolvedEnvelopePath -or
            [string]$receipt.envelopeSha256 -ne $envelopeHash) {
            throw "Acceptance receipt conflicts with the validated envelope: $receiptPath"
        }
    }
    else {
        [void][IO.Directory]::CreateDirectory($receiptRoot)
        $receipt = [ordered]@{
            schemaVersion = 2
            launchToken = $effectiveLaunchToken
            envelopePath = $resolvedEnvelopePath
            envelopeSha256 = $envelopeHash
            projectRoot = $resolvedProjectRoot
            acceptedUtc = [DateTime]::UtcNow.ToString('o')
            handoffFiles = @($envelope.handoffFiles | ForEach-Object {
                    [ordered]@{ absolutePath = [string]$_.absolutePath; sha256 = [string]$_.sha256 }
                })
        }
        $receiptJson = $receipt | ConvertTo-Json -Depth 8
        $stream = [IO.File]::Open($receiptPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
        try {
            $bytes = [Text.UTF8Encoding]::new($false).GetBytes($receiptJson)
            $stream.Write($bytes, 0, $bytes.Length)
        }
        finally {
            $stream.Dispose()
        }
    }
}

[pscustomobject]@{
    valid = $valid
    acceptedPreviously = $acceptedPreviously
    envelopeAvailable = $true
    continuationMode = if ($valid) { 'ValidatedNow' } else { 'HardFailure' }
    envelopePath = $resolvedEnvelopePath
    receiptPath = $receiptPath
    launchToken = $effectiveLaunchToken
    schemaVersion = $schemaVersion
    handoffMode = [string]$envelope.handoffMode
    handoffSnapshotDirectory = [string]$envelope.handoffSnapshotDirectory
    schemaValid = $schemaValid
    projectRootValid = $rootValid
    handoffFilesValid = $handoffValid
    gitValid = $gitValid
    branchAtLaunch = [string]$envelope.git.branch
    currentBranch = $currentBranch
    branchDrift = $branchDrift
    headAtLaunch = [string]$envelope.git.head
    currentHead = $currentHead
    headDrift = $headDrift
    handoffFiles = $handoffResults
}

if (-not $valid) { exit 1 }
