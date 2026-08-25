[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$builder = Join-Path $PSScriptRoot 'Build-ESSkillRegistryManifest.ps1'
$utf8 = New-Object Text.UTF8Encoding($false)
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) (
    'es-skill-registry-builder-' + [Guid]::NewGuid().ToString('N'))
$projectRoot = Join-Path $fixtureRoot 'project'
$outsideRoot = Join-Path $fixtureRoot 'outside'

function Write-FixtureText([string]$Path, [string]$Text) {
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
    [IO.File]::WriteAllText($Path, $Text, $utf8)
}

function Assert-Fixture([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Invoke-ExpectedFailure([scriptblock]$Action, [string]$Pattern) {
    $message = ''
    try {
        & $Action
    } catch {
        $message = $_.Exception.Message
    }
    if ([string]::IsNullOrWhiteSpace($message)) {
        throw "Expected failure did not occur: $Pattern"
    }
    if ($message -notmatch $Pattern) {
        throw "Expected failure '$Pattern', received: $message"
    }
}

try {
    [void][IO.Directory]::CreateDirectory($projectRoot)
    [void][IO.Directory]::CreateDirectory($outsideRoot)
    Write-FixtureText (Join-Path $projectRoot '.agents/SKILL_DISCOVERY_POLICY.json') @'
{
  "schemaVersion": 1,
  "policyId": "fixture-policy",
  "states": {
    "Stable": {
      "discoveryState": "operational",
      "planEligibility": "plan-authorized",
      "runtimeEligibility": "authorized-only"
    }
  },
  "deliveryOverrides": {},
  "registrationOverrides": {
    "Current": { "reviewRequired": false }
  }
}
'@
    Write-FixtureText (Join-Path $projectRoot '.agents/SKILL_RESOURCE_INDEX.yaml') "schemaVersion: 1`n"
    Write-FixtureText (Join-Path $projectRoot '.agents/SKILL_CATALOG.yaml') @'
schemaVersion: 1
skills:
  es-fixture:
    family: governance
    registrationState: Current
'@
    Write-FixtureText (Join-Path $projectRoot 'Documentation/AIKnowledge/AIBRAIN_ENTRY.md') "# Fixture AIBrain entry`n"
    Write-FixtureText (Join-Path $projectRoot 'Assets/Plugins/ES/AICommands/AICommandCatalog.json') "{`"schemaVersion`":1}`n"
    Write-FixtureText (Join-Path $projectRoot '.agents/skills/es-fixture/SKILL.md') @'
---
name: es-fixture
description: Fixture Skill.
---
'@
    Write-FixtureText (Join-Path $projectRoot '.agents/skills/es-fixture/governance.json') @'
{
  "maturity": "Stable",
  "delivery": "Accepted",
  "routeKeys": ["fixture", "governance"],
  "owner": "fixture-owner",
  "acceptanceOwner": "fixture-acceptance-owner"
}
'@

    # First creation and existing-file replacement must both be deterministic.
    $null = & $builder -ProjectRoot $projectRoot
    $manifestPath = Join-Path $projectRoot '.agents/SKILL_REGISTRY.manifest.json'
    Assert-Fixture (Test-Path -LiteralPath $manifestPath -PathType Leaf) 'Default Registry output was not created.'
    $firstBytes = [IO.File]::ReadAllBytes($manifestPath)
    $first = $utf8.GetString($firstBytes) | ConvertFrom-Json
    Assert-Fixture ([string]$first.manifestId -eq 'esframework-skill-registry') 'Manifest identity is invalid.'
    Assert-Fixture ([string]$first.inputSnapshotHash -match '^[0-9a-f]{64}$') 'Manifest does not bind an input snapshot hash.'
    Assert-Fixture (@($first.skills).Count -eq 1) 'Fixture Skill was not registered exactly once.'

    $stableWriteTime = [DateTime]::SpecifyKind([DateTime]'2001-02-03T04:05:06', [DateTimeKind]::Utc)
    [IO.File]::SetLastWriteTimeUtc($manifestPath, $stableWriteTime)
    $null = & $builder -ProjectRoot $projectRoot
    $secondBytes = [IO.File]::ReadAllBytes($manifestPath)
    Assert-Fixture ([Convert]::ToBase64String($firstBytes) -ceq [Convert]::ToBase64String($secondBytes)) 'Stable inputs produced different Registry bytes.'
    Assert-Fixture ([IO.File]::GetLastWriteTimeUtc($manifestPath) -eq $stableWriteTime) 'Stable inputs physically replaced the Registry output.'
    Assert-Fixture (@(Get-ChildItem -LiteralPath (Split-Path -Parent $manifestPath) -Filter 'SKILL_REGISTRY.manifest.json.tmp-*').Count -eq 0) 'Successful replacement left a temporary file.'
    Assert-Fixture (@(Get-ChildItem -LiteralPath (Split-Path -Parent $manifestPath) -Filter 'SKILL_REGISTRY.manifest.json.replace-backup-*').Count -eq 0) 'Successful replacement left a replacement backup.'

    $sentinel = '{"sentinel":"preserve-old-manifest"}'
    Write-FixtureText $manifestPath $sentinel
    Invoke-ExpectedFailure {
        $null = & $builder -ProjectRoot $projectRoot -OutputPath '../escaped-registry.json'
    } 'OutputPath escapes|OutputPath must be relative'
    Assert-Fixture ([IO.File]::ReadAllText($manifestPath, $utf8) -ceq $sentinel) 'Traversal denial changed the old manifest.'

    Invoke-ExpectedFailure {
        $null = & $builder -ProjectRoot $projectRoot -OutputPath '.agents/other-registry.json'
    } 'OutputPath must be .agents/SKILL_REGISTRY.manifest.json'
    Invoke-ExpectedFailure {
        $null = & $builder -ProjectRoot $projectRoot -OutputPath '.git/index'
    } 'OutputPath must be .agents/SKILL_REGISTRY.manifest.json'
    Invoke-ExpectedFailure {
        $null = & $builder -ProjectRoot $projectRoot -OutputPath '.agents/SKILL_REGISTRY.manifest.json:stream'
    } 'alternate data stream'

    Invoke-ExpectedFailure {
        $null = & $builder -ProjectRoot $projectRoot -OutputPath '.agents/SKILL_CATALOG.yaml'
    } 'OutputPath must be .agents/SKILL_REGISTRY.manifest.json'
    Assert-Fixture (([IO.File]::ReadAllText((Join-Path $projectRoot '.agents/SKILL_CATALOG.yaml'), $utf8)) -match 'es-fixture') 'Input collision denial changed the Catalog.'

    $catalogFixturePath = Join-Path $projectRoot '.agents/SKILL_CATALOG.yaml'
    $catalogFixtureBytes = [IO.File]::ReadAllBytes($catalogFixturePath)
    [IO.File]::WriteAllBytes($catalogFixturePath, [byte[]](0xC3, 0x28))
    Invoke-ExpectedFailure {
        $null = & $builder -ProjectRoot $projectRoot
    } '.'
    Assert-Fixture ([IO.File]::ReadAllText($manifestPath, $utf8) -ceq $sentinel) 'Invalid UTF-8 input changed the old manifest.'
    [IO.File]::WriteAllBytes($catalogFixturePath, $catalogFixtureBytes)

    $junctionPath = Join-Path $projectRoot 'outside-link'
    $null = New-Item -ItemType Junction -Path $junctionPath -Target $outsideRoot
    Invoke-ExpectedFailure {
        $null = & $builder -ProjectRoot $projectRoot -OutputPath 'outside-link/escaped-registry.json'
    } 'crosses a reparse point'
    Assert-Fixture (-not (Test-Path -LiteralPath (Join-Path $outsideRoot 'escaped-registry.json'))) 'Reparse-point denial wrote outside ProjectRoot.'

    # The test hook mutates only this system-temp fixture after the candidate was
    # flushed. The final CAS must reject it, preserve the old output, and clean up.
    $driftHook = {
        param($capturedRoot, $capturedSnapshot)
        $catalogPath = Join-Path $capturedRoot '.agents/SKILL_CATALOG.yaml'
        [IO.File]::AppendAllText($catalogPath, "# drift`n", (New-Object Text.UTF8Encoding($false)))
    }
    Invoke-ExpectedFailure {
        $null = & $builder -ProjectRoot $projectRoot -BeforeCommitTestHook $driftHook
    } 'input snapshot drifted'
    Assert-Fixture ([IO.File]::ReadAllText($manifestPath, $utf8) -ceq $sentinel) 'Input drift damaged the old manifest.'
    Assert-Fixture (@(Get-ChildItem -LiteralPath (Split-Path -Parent $manifestPath) -Filter 'SKILL_REGISTRY.manifest.json.tmp-*').Count -eq 0) 'Failed commit left a temporary file.'
    Assert-Fixture (@(Get-ChildItem -LiteralPath (Split-Path -Parent $manifestPath) -Filter 'SKILL_REGISTRY.manifest.json.replace-backup-*').Count -eq 0) 'Failed commit left a replacement backup.'

    $concurrentOutput = '{"concurrent":"preserve-output"}'
    $outputDriftHook = {
        param($capturedRoot, $capturedSnapshot)
        $outputPath = Join-Path $capturedRoot '.agents/SKILL_REGISTRY.manifest.json'
        [IO.File]::WriteAllText($outputPath, '{"concurrent":"preserve-output"}', (New-Object Text.UTF8Encoding($false)))
    }
    Invoke-ExpectedFailure {
        $null = & $builder -ProjectRoot $projectRoot -BeforeCommitTestHook $outputDriftHook
    } 'Registry output changed during the build'
    Assert-Fixture ([IO.File]::ReadAllText($manifestPath, $utf8) -ceq $concurrentOutput) 'Output CAS overwrote a concurrent Registry change.'
    Assert-Fixture (@(Get-ChildItem -LiteralPath (Split-Path -Parent $manifestPath) -Filter 'SKILL_REGISTRY.manifest.json.tmp-*').Count -eq 0) 'Output CAS failure left a temporary file.'

    # Cooperative writers use a per-project/output lock in the system temp root.
    $normalizedRoot = [IO.Path]::GetFullPath($projectRoot).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    $lockIdentity = $normalizedRoot.ToLowerInvariant() + "`0" + '.agents/SKILL_REGISTRY.manifest.json'.ToLowerInvariant()
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $lockHash = ([BitConverter]::ToString(
            $sha.ComputeHash($utf8.GetBytes($lockIdentity)))).Replace('-', '').ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }
    $lockPath = Join-Path ([IO.Path]::GetTempPath()) ("es-skill-registry-$lockHash.lock")
    $heldLock = [IO.FileStream]::new(
        $lockPath,
        [IO.FileMode]::OpenOrCreate,
        [IO.FileAccess]::ReadWrite,
        [IO.FileShare]::None)
    try {
        Invoke-ExpectedFailure {
            $null = & $builder -ProjectRoot $projectRoot
        } 'Registry build lock is held by another writer'
        Assert-Fixture ([IO.File]::ReadAllText($manifestPath, $utf8) -ceq $concurrentOutput) 'Lock contention changed the Registry output.'
    } finally {
        $heldLock.Dispose()
        if (Test-Path -LiteralPath $lockPath) {
            [IO.File]::Delete($lockPath)
        }
    }

    # A caught failure immediately after File.Replace must restore the prior file.
    $null = & $builder -ProjectRoot $projectRoot
    $beforeInjectedReplace = [IO.File]::ReadAllBytes($manifestPath)
    Write-FixtureText (Join-Path $projectRoot '.agents/skills/es-fixture/SKILL.md') @'
---
name: es-fixture
description: Fixture Skill changed for replacement recovery.
---
'@
    $replaceFailureHook = { throw 'injected after-replace failure' }
    Invoke-ExpectedFailure {
        $null = & $builder -ProjectRoot $projectRoot -AfterReplaceTestHook $replaceFailureHook
    } 'injected after-replace failure'
    $afterInjectedReplace = [IO.File]::ReadAllBytes($manifestPath)
    Assert-Fixture ([Convert]::ToBase64String($beforeInjectedReplace) -ceq [Convert]::ToBase64String($afterInjectedReplace)) 'After-replace failure did not restore the prior Registry output.'
    Assert-Fixture (@(Get-ChildItem -LiteralPath (Split-Path -Parent $manifestPath) -Filter 'SKILL_REGISTRY.manifest.json.replace-backup-*').Count -eq 0) 'Verified rollback left a replacement backup.'
    Assert-Fixture (@(Get-ChildItem -LiteralPath (Split-Path -Parent $manifestPath) -Filter 'SKILL_REGISTRY.manifest.json.failed-candidate-*').Count -eq 0) 'Verified rollback left a failed candidate.'

    # Unknown recovery content must never be deleted merely because the current
    # output already matches the baseline.
    $unknownBackupContent = '{"external":"must-preserve-backup"}'
    $unknownBackupHook = {
        param($capturedRoot, $capturedSnapshot)
        $parent = Join-Path $capturedRoot '.agents'
        $output = Join-Path $parent 'SKILL_REGISTRY.manifest.json'
        $backup = Get-ChildItem -LiteralPath $parent -Filter 'SKILL_REGISTRY.manifest.json.replace-backup-*' |
            Select-Object -First 1
        if ($null -eq $backup) { throw 'injected hook could not find replacement backup' }
        $baselineBytes = [IO.File]::ReadAllBytes($backup.FullName)
        [IO.File]::WriteAllBytes($output, $baselineBytes)
        [IO.File]::WriteAllText(
            $backup.FullName,
            '{"external":"must-preserve-backup"}',
            (New-Object Text.UTF8Encoding($false)))
        throw 'injected unknown replacement backup state'
    }
    Invoke-ExpectedFailure {
        $null = & $builder -ProjectRoot $projectRoot -AfterReplaceTestHook $unknownBackupHook
    } 'injected unknown replacement backup state'
    Assert-Fixture ([Convert]::ToBase64String($beforeInjectedReplace) -ceq [Convert]::ToBase64String([IO.File]::ReadAllBytes($manifestPath))) 'Unknown-backup failure changed the prior Registry output.'
    $preservedBackups = @(Get-ChildItem -LiteralPath (Split-Path -Parent $manifestPath) -Filter 'SKILL_REGISTRY.manifest.json.replace-backup-*')
    Assert-Fixture ($preservedBackups.Count -eq 1) 'Unknown replacement backup was not preserved exactly once.'
    Assert-Fixture ([IO.File]::ReadAllText($preservedBackups[0].FullName, $utf8) -ceq $unknownBackupContent) 'Preserved replacement backup content changed.'
    [IO.File]::Delete($preservedBackups[0].FullName)

    # A non-cooperating write that replaces the failed candidate during rollback
    # must be preserved instead of being classified from an earlier hash read.
    $unknownFailedCandidateContent = '{"external":"must-preserve-failed-candidate"}'
    $failedCandidateDriftHook = {
        param($capturedRoot, $capturedSnapshot, $failedCandidatePath)
        [IO.File]::WriteAllText(
            $failedCandidatePath,
            '{"external":"must-preserve-failed-candidate"}',
            (New-Object Text.UTF8Encoding($false)))
    }
    Invoke-ExpectedFailure {
        $null = & $builder `
            -ProjectRoot $projectRoot `
            -AfterReplaceTestHook $replaceFailureHook `
            -BeforeFailedCandidateCleanupTestHook $failedCandidateDriftHook
    } 'injected after-replace failure'
    Assert-Fixture ([Convert]::ToBase64String($beforeInjectedReplace) -ceq [Convert]::ToBase64String([IO.File]::ReadAllBytes($manifestPath))) 'Failed-candidate drift changed the restored Registry output.'
    $preservedCandidates = @(Get-ChildItem -LiteralPath (Split-Path -Parent $manifestPath) -Filter 'SKILL_REGISTRY.manifest.json.failed-candidate-*')
    Assert-Fixture ($preservedCandidates.Count -eq 1) 'Unknown failed candidate was not preserved exactly once.'
    Assert-Fixture ([IO.File]::ReadAllText($preservedCandidates[0].FullName, $utf8) -ceq $unknownFailedCandidateContent) 'Preserved failed-candidate content changed.'
    [IO.File]::Delete($preservedCandidates[0].FullName)

    Write-Output 'PASS: Registry builder fixed-scope containment, strict UTF-8, deterministic no-op replay, writer lock, input/output CAS, verified rollback, and unknown recovery preservation fixtures'
} finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}
