[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [string]$OutputPath = '.agents/SKILL_REGISTRY.manifest.json',
    # Test-only synchronization hook. It is hidden from ordinary command discovery.
    [Parameter(DontShow = $true)][scriptblock]$BeforeCommitTestHook,
    [Parameter(DontShow = $true)][scriptblock]$AfterReplaceTestHook,
    [Parameter(DontShow = $true)][scriptblock]$BeforeFailedCandidateCleanupTestHook
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ESPathBoundary.Common.ps1')

$script:ESRegistryMetadataPaths = @(
    '.agents/SKILL_DISCOVERY_POLICY.json',
    '.agents/SKILL_RESOURCE_INDEX.yaml',
    '.agents/SKILL_CATALOG.yaml',
    'Documentation/AIKnowledge/AIBRAIN_ENTRY.md',
    'Assets/Plugins/ES/AICommands/AICommandCatalog.json'
)
$script:ESStrictUtf8 = New-Object Text.UTF8Encoding($false, $true)
$script:ESUtf8NoBom = New-Object Text.UTF8Encoding($false)

function Get-ESSha256Bytes([byte[]]$Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }
}

function Get-ESRegistryOutputState([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return [pscustomobject][ordered]@{
            Exists = $false
            Sha256 = $null
        }
    }
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw 'OutputPath must identify a file, not a directory.'
    }

    return [pscustomobject][ordered]@{
        Exists = $true
        Sha256 = Get-ESSha256Bytes ([IO.File]::ReadAllBytes($Path))
    }
}

function Assert-ESRegistryOutputStateCurrent([string]$Path, [object]$Expected) {
    $current = Get-ESRegistryOutputState $Path
    if ([bool]$current.Exists -ne [bool]$Expected.Exists) {
        throw 'Registry output changed during the build.'
    }
    if ($current.Exists -and -not [string]::Equals(
            [string]$current.Sha256,
            [string]$Expected.Sha256,
            [StringComparison]::Ordinal)) {
        throw 'Registry output changed during the build.'
    }
}

function Remove-ESRegistryRecoveryFileIfExpected(
        [string]$Path,
        [string[]]$ExpectedHashes,
        [string]$ArtifactLabel) {
    try {
        $state = Get-ESRegistryOutputState $Path
    } catch {
        Write-Warning "Preserving $ArtifactLabel because its current state could not be verified: $Path"
        return $false
    }
    if (-not $state.Exists) {
        return $true
    }

    $recognized = $false
    foreach ($expectedHash in @($ExpectedHashes)) {
        if (-not [string]::IsNullOrWhiteSpace($expectedHash) -and
                [string]::Equals(
                    [string]$state.Sha256,
                    $expectedHash,
                    [StringComparison]::Ordinal)) {
            $recognized = $true
            break
        }
    }
    if (-not $recognized) {
        Write-Warning "Preserving $ArtifactLabel with an unexpected hash '$($state.Sha256)': $Path"
        return $false
    }

    try {
        [IO.File]::Delete($Path)
        if (Test-Path -LiteralPath $Path) {
            Write-Warning "Failed to remove verified $ArtifactLabel; preserving: $Path"
            return $false
        }
        return $true
    } catch {
        Write-Warning "Failed to remove verified $ArtifactLabel; preserving: $Path"
        return $false
    }
}

function Get-ESRegistrySkillNames([string]$Root) {
    $skillsTarget = Resolve-ESContainedRelativePath -Candidate '.agents/skills' -ContainerRoot $Root -Label 'SkillsRoot'
    if (-not (Test-Path -LiteralPath $skillsTarget.FullPath -PathType Container)) {
        throw 'ProjectRoot must contain .agents/skills.'
    }

    return @(
        Get-ChildItem -LiteralPath $skillsTarget.FullPath -Directory |
            Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'SKILL.md') -PathType Leaf } |
            Sort-Object Name |
            ForEach-Object { [string]$_.Name }
    )
}

function Read-ESRegistryInputFile([string]$Root, [string]$RelativePath) {
    $target = Resolve-ESContainedRelativePath -Candidate $RelativePath -ContainerRoot $Root -Label 'Registry input path'
    if (-not (Test-Path -LiteralPath $target.FullPath -PathType Leaf)) {
        throw "Required project file is missing: $RelativePath"
    }

    $bytes = [IO.File]::ReadAllBytes($target.FullPath)
    $text = $script:ESStrictUtf8.GetString($bytes)
    return [pscustomobject][ordered]@{
        Path = $target.RelativePath
        Sha256 = Get-ESSha256Bytes $bytes
        Text = $text
    }
}

function New-ESRegistryInputSnapshot([string]$Root) {
    $skillNames = @(Get-ESRegistrySkillNames $Root)
    $files = [ordered]@{}

    foreach ($relativePath in $script:ESRegistryMetadataPaths) {
        $files[$relativePath] = Read-ESRegistryInputFile -Root $Root -RelativePath $relativePath
    }
    foreach ($skillName in $skillNames) {
        foreach ($leafName in @('SKILL.md', 'governance.json', 'evidence-contract.binding.json')) {
            $relativePath = ".agents/skills/$skillName/$leafName"
            $files[$relativePath] = Read-ESRegistryInputFile -Root $Root -RelativePath $relativePath
        }
    }

    $identityFiles = @(
        foreach ($relativePath in $files.Keys) {
            [ordered]@{ path = [string]$relativePath; sha256 = [string]$files[$relativePath].Sha256 }
        }
    )
    $identity = [ordered]@{
        schemaVersion = 1
        skillNames = @($skillNames)
        files = @($identityFiles)
    }
    $identityJson = $identity | ConvertTo-Json -Depth 6 -Compress

    return [pscustomobject][ordered]@{
        SkillNames = @($skillNames)
        Files = $files
        SnapshotHash = Get-ESSha256Bytes $script:ESUtf8NoBom.GetBytes($identityJson)
    }
}

function Assert-ESRegistryInputSnapshotCurrent([string]$Root, [object]$Snapshot) {
    $currentSkillNames = @(Get-ESRegistrySkillNames $Root)
    if ($currentSkillNames.Count -ne $Snapshot.SkillNames.Count) {
        throw 'Registry input snapshot drifted: direct Skill inventory changed.'
    }
    for ($index = 0; $index -lt $currentSkillNames.Count; $index++) {
        if (-not [string]::Equals(
                [string]$currentSkillNames[$index],
                [string]$Snapshot.SkillNames[$index],
                [StringComparison]::Ordinal)) {
            throw 'Registry input snapshot drifted: direct Skill inventory changed.'
        }
    }

    foreach ($relativePath in $Snapshot.Files.Keys) {
        $current = Read-ESRegistryInputFile -Root $Root -RelativePath ([string]$relativePath)
        if (-not [string]::Equals(
                [string]$current.Sha256,
                [string]$Snapshot.Files[$relativePath].Sha256,
                [StringComparison]::Ordinal)) {
            throw "Registry input snapshot drifted: $relativePath"
        }
    }
}

function Get-ESRegistrySnapshotText([object]$Snapshot, [string]$RelativePath) {
    $entry = $Snapshot.Files[$RelativePath]
    if ($null -eq $entry) {
        throw "Registry input was not captured: $RelativePath"
    }
    return [string]$entry.Text
}

function Get-ESRegistryCatalogBlock([string]$Text, [string]$Name) {
    $match = [regex]::Match(
        $Text,
        '(?ms)^  ' + [regex]::Escape($Name) + ':\s*\n(?:(?!^  [a-z0-9][a-z0-9-]*:\s*$).)*')
    if ($match.Success) { return $match.Value }
    return $null
}

function Get-ESRegistryCatalogScalar([string]$Block, [string]$Key) {
    $match = [regex]::Match(
        $Block,
        '(?m)^[ \t]+' + [regex]::Escape($Key) + ':[ \t]*(?<value>[^\r\n]+)')
    if ($match.Success) { return $match.Groups['value'].Value.Trim().Trim([char]34, [char]39) }
    return ''
}

function Get-ESRegistryRouteKeys([object]$Governance) {
    return @(
        $Governance.routeKeys |
            ForEach-Object { [string]$_ } |
            Where-Object { $_ } |
            Sort-Object -Unique
    )
}

function Resolve-ESRegistryEligibility([object]$Policy, [object]$Governance, [string]$RegistrationState) {
    $state = $Policy.states.PSObject.Properties[[string]$Governance.maturity]
    if ($null -eq $state) { throw "Unknown maturity in policy: $($Governance.maturity)" }

    $value = $state.Value
    $discovery = [string]$value.discoveryState
    $plan = [string]$value.planEligibility
    $runtime = [string]$value.runtimeEligibility
    $override = $Policy.deliveryOverrides.PSObject.Properties[[string]$Governance.delivery]
    if ($null -ne $override) {
        if ($override.Value.PSObject.Properties.Name -contains 'discoveryState') {
            $discovery = [string]$override.Value.discoveryState
        }
        if ($override.Value.PSObject.Properties.Name -contains 'planEligibility') {
            $plan = [string]$override.Value.planEligibility
        }
        if ($override.Value.PSObject.Properties.Name -contains 'runtimeEligibility') {
            $runtime = [string]$override.Value.runtimeEligibility
        }
    }

    $registration = $Policy.registrationOverrides.PSObject.Properties[$RegistrationState]
    return [ordered]@{
        discoveryState = $discovery
        planEligibility = $plan
        runtimeEligibility = $runtime
        reviewRequired = if ($null -eq $registration) { $true } else { [bool]$registration.Value.reviewRequired }
    }
}

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    throw 'ProjectRoot must identify an existing directory.'
}
$rootItem = Get-Item -LiteralPath $ProjectRoot -Force -ErrorAction Stop
if (-not $rootItem.PSIsContainer) {
    throw 'ProjectRoot must identify an existing directory.'
}
$rootWithSeparator = [IO.Path]::GetFullPath($rootItem.FullName)
$fileSystemRoot = [IO.Path]::GetPathRoot($rootWithSeparator)
if ([string]::Equals($rootWithSeparator, $fileSystemRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'ProjectRoot must not be a filesystem root.'
}
$root = $rootWithSeparator.TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar)
$systemTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar)
if (($null -ne $BeforeCommitTestHook -or
        $null -ne $AfterReplaceTestHook -or
        $null -ne $BeforeFailedCandidateCleanupTestHook) -and
        -not $root.StartsWith(
            $systemTempRoot + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Registry test hooks are restricted to system-temporary fixtures.'
}
$outputTarget = Resolve-ESContainedRelativePath -Candidate $OutputPath -ContainerRoot $root -Label 'OutputPath'
if (-not [string]::Equals(
        $outputTarget.RelativePath,
        '.agents/SKILL_REGISTRY.manifest.json',
        [StringComparison]::Ordinal)) {
    throw 'OutputPath must be .agents/SKILL_REGISTRY.manifest.json.'
}
if (Test-Path -LiteralPath $outputTarget.FullPath -PathType Container) {
    throw 'OutputPath must identify a file, not a directory.'
}

$lockIdentity = $root.ToLowerInvariant() + "`0" + $outputTarget.RelativePath.ToLowerInvariant()
$lockHash = Get-ESSha256Bytes $script:ESUtf8NoBom.GetBytes($lockIdentity)
$lockPath = Join-Path $systemTempRoot ("es-skill-registry-$lockHash.lock")
$buildLock = $null
$lockAcquired = $false
$manifestJson = $null
try {
    try {
        $buildLock = [IO.FileStream]::new(
            $lockPath,
            [IO.FileMode]::OpenOrCreate,
            [IO.FileAccess]::ReadWrite,
            [IO.FileShare]::None)
        $lockAcquired = $true
    } catch [IO.IOException] {
        throw 'Registry build lock is held by another writer.'
    }

    $outputBaseline = Get-ESRegistryOutputState $outputTarget.FullPath
    $snapshot = New-ESRegistryInputSnapshot $root
    foreach ($relativePath in $snapshot.Files.Keys) {
        if ([string]::Equals(
                [string]$relativePath,
                [string]$outputTarget.RelativePath,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw 'OutputPath must not overwrite a Registry input file.'
        }
    }

    $policyPath = '.agents/SKILL_DISCOVERY_POLICY.json'
    $policy = Get-ESRegistrySnapshotText $snapshot $policyPath | ConvertFrom-Json
    if ([int]$policy.schemaVersion -ne 1) {
        throw 'Skill discovery policy schemaVersion must be 1.'
    }
    $catalogText = Get-ESRegistrySnapshotText $snapshot '.agents/SKILL_CATALOG.yaml'
    $metadata = [ordered]@{}
    foreach ($relativePath in $script:ESRegistryMetadataPaths) {
        $metadata[$relativePath] = [string]$snapshot.Files[$relativePath].Sha256
    }

    $records = New-Object 'System.Collections.Generic.List[object]'
    foreach ($skillName in $snapshot.SkillNames) {
        $skillRelative = ".agents/skills/$skillName/SKILL.md"
        $governanceRelative = ".agents/skills/$skillName/governance.json"
        $evidenceBindingRelative = ".agents/skills/$skillName/evidence-contract.binding.json"
        $governance = Get-ESRegistrySnapshotText $snapshot $governanceRelative | ConvertFrom-Json
        $catalogBlock = Get-ESRegistryCatalogBlock $catalogText $skillName
        if ($null -eq $catalogBlock) { throw "Catalog record missing: $skillName" }

        $family = Get-ESRegistryCatalogScalar $catalogBlock 'family'
        $registrationState = Get-ESRegistryCatalogScalar $catalogBlock 'registrationState'
        if ([string]::IsNullOrWhiteSpace($family) -or [string]::IsNullOrWhiteSpace($registrationState)) {
            throw "Catalog lifecycle identity missing: $skillName"
        }
        $eligibility = Resolve-ESRegistryEligibility $policy $governance $registrationState
        [void]$records.Add([ordered]@{
            skillName = $skillName
            maturity = [string]$governance.maturity
            delivery = [string]$governance.delivery
            registrationState = $registrationState
            discoveryState = $eligibility.discoveryState
            planEligibility = $eligibility.planEligibility
            runtimeEligibility = $eligibility.runtimeEligibility
            reviewRequired = $eligibility.reviewRequired
            routeKeys = @(Get-ESRegistryRouteKeys $governance)
            family = $family
            owner = [string]$governance.owner
            acceptanceOwner = [string]$governance.acceptanceOwner
            skillHash = [string]$snapshot.Files[$skillRelative].Sha256
            governanceHash = [string]$snapshot.Files[$governanceRelative].Sha256
            evidenceContractBindingHash = [string]$snapshot.Files[$evidenceBindingRelative].Sha256
        })
    }

    $canonical = [ordered]@{
        schemaVersion = 1
        policyId = [string]$policy.policyId
        metadata = $metadata
        skills = @($records.ToArray())
    }
    $canonicalJson = $canonical | ConvertTo-Json -Depth 12 -Compress
    $registryHash = Get-ESSha256Bytes $script:ESUtf8NoBom.GetBytes($canonicalJson)
    $manifest = [ordered]@{
        schemaVersion = 1
        manifestId = 'esframework-skill-registry'
        registryHash = $registryHash
        inputSnapshotHash = [string]$snapshot.SnapshotHash
        policyHash = $metadata[$policyPath]
        metadata = $metadata
        skills = @($records.ToArray())
    }
    $manifestJson = $manifest | ConvertTo-Json -Depth 14

    $outputParent = Split-Path -Parent $outputTarget.FullPath
    [void][IO.Directory]::CreateDirectory($outputParent)
    $currentOutputTarget = Resolve-ESContainedRelativePath -Candidate $outputTarget.RelativePath -ContainerRoot $root -Label 'OutputPath'
    if (-not [string]::Equals(
            $currentOutputTarget.FullPath,
            $outputTarget.FullPath,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'OutputPath identity changed during the Registry build.'
    }

    $temporary = Join-Path $outputParent (
        [IO.Path]::GetFileName($outputTarget.FullPath) + '.tmp-' + [Guid]::NewGuid().ToString('N'))
    $replacementBackup = Join-Path $outputParent (
        [IO.Path]::GetFileName($outputTarget.FullPath) + '.replace-backup-' + [Guid]::NewGuid().ToString('N'))
    $replacementVerified = $false
    $manifestFileHash = $null
    try {
        $manifestBytes = $script:ESUtf8NoBom.GetBytes($manifestJson)
        $manifestFileHash = Get-ESSha256Bytes $manifestBytes
        $stream = [IO.FileStream]::new(
            $temporary,
            [IO.FileMode]::CreateNew,
            [IO.FileAccess]::Write,
            [IO.FileShare]::None)
        try {
            $stream.Write($manifestBytes, 0, $manifestBytes.Length)
            $stream.Flush($true)
        } finally {
            $stream.Dispose()
        }

        if ($null -ne $BeforeCommitTestHook) {
            & $BeforeCommitTestHook $root $snapshot
        }
        Assert-ESRegistryInputSnapshotCurrent -Root $root -Snapshot $snapshot
        $currentOutputTarget = Resolve-ESContainedRelativePath -Candidate $outputTarget.RelativePath -ContainerRoot $root -Label 'OutputPath'
        if (-not [string]::Equals(
                $currentOutputTarget.FullPath,
                $outputTarget.FullPath,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw 'OutputPath identity changed before Registry commit.'
        }
        Assert-ESRegistryOutputStateCurrent -Path $outputTarget.FullPath -Expected $outputBaseline

        # An unchanged projection is a physical no-op. Existing files otherwise
        # use atomic replacement; first creation uses a same-directory move.
        # This does not claim immunity from non-cooperating writers in the final
        # compare/replace window or process- and power-loss crash safety.
        if ($outputBaseline.Exists -and [string]::Equals(
                [string]$outputBaseline.Sha256,
                $manifestFileHash,
                [StringComparison]::Ordinal)) {
            # The staged candidate is removed by finally without touching output.
        } elseif ($outputBaseline.Exists) {
            [IO.File]::Replace($temporary, $outputTarget.FullPath, $replacementBackup, $true)
            if ($null -ne $AfterReplaceTestHook) {
                & $AfterReplaceTestHook $root $snapshot
            }
            $currentOutput = Get-ESRegistryOutputState $outputTarget.FullPath
            $currentBackup = Get-ESRegistryOutputState $replacementBackup
            if (-not $currentOutput.Exists -or -not [string]::Equals(
                    [string]$currentOutput.Sha256,
                    $manifestFileHash,
                    [StringComparison]::Ordinal)) {
                throw 'Registry replacement did not produce the expected output hash.'
            }
            if (-not $currentBackup.Exists -or -not [string]::Equals(
                    [string]$currentBackup.Sha256,
                    [string]$outputBaseline.Sha256,
                    [StringComparison]::Ordinal)) {
                throw 'Registry replacement backup does not match the prior output hash.'
            }
            $replacementVerified = $true
        } else {
            [IO.File]::Move($temporary, $outputTarget.FullPath)
            $currentOutput = Get-ESRegistryOutputState $outputTarget.FullPath
            if (-not $currentOutput.Exists -or -not [string]::Equals(
                    [string]$currentOutput.Sha256,
                    $manifestFileHash,
                    [StringComparison]::Ordinal)) {
                throw 'Registry creation did not produce the expected output hash.'
            }
        }
    } finally {
        if (Test-Path -LiteralPath $temporary) {
            try {
                [IO.File]::Delete($temporary)
            } catch {
                Write-Warning "Failed to clean Registry temporary file: $temporary"
            }
        }
        if (Test-Path -LiteralPath $replacementBackup) {
            if ($replacementVerified) {
                $backupRemoved = Remove-ESRegistryRecoveryFileIfExpected `
                    -Path $replacementBackup `
                    -ExpectedHashes @([string]$outputBaseline.Sha256, [string]$manifestFileHash) `
                    -ArtifactLabel 'Registry replacement backup'
            } else {
                $backupState = Get-ESRegistryOutputState $replacementBackup
                $currentState = Get-ESRegistryOutputState $outputTarget.FullPath
                if ($outputBaseline.Exists -and $backupState.Exists -and
                        [string]::Equals(
                            [string]$backupState.Sha256,
                            [string]$outputBaseline.Sha256,
                            [StringComparison]::Ordinal) -and
                        -not $currentState.Exists) {
                    try {
                        [IO.File]::Move($replacementBackup, $outputTarget.FullPath)
                        Write-Warning 'Registry replacement failed; the prior output was restored from backup.'
                    } catch {
                        Write-Warning "Registry replacement failed and backup restoration failed; preserving: $replacementBackup"
                    }
                } elseif ($outputBaseline.Exists -and $backupState.Exists -and $currentState.Exists -and
                        [string]::Equals(
                            [string]$backupState.Sha256,
                            [string]$outputBaseline.Sha256,
                            [StringComparison]::Ordinal) -and
                        -not [string]::IsNullOrWhiteSpace($manifestFileHash) -and
                        [string]::Equals(
                            [string]$currentState.Sha256,
                            [string]$manifestFileHash,
                            [StringComparison]::Ordinal)) {
                    $failedCandidate = Join-Path $outputParent (
                        [IO.Path]::GetFileName($outputTarget.FullPath) + '.failed-candidate-' + [Guid]::NewGuid().ToString('N'))
                    try {
                        [IO.File]::Replace($replacementBackup, $outputTarget.FullPath, $failedCandidate, $true)
                        $restoredState = Get-ESRegistryOutputState $outputTarget.FullPath
                        if (-not $restoredState.Exists -or -not [string]::Equals(
                                [string]$restoredState.Sha256,
                                [string]$outputBaseline.Sha256,
                                [StringComparison]::Ordinal)) {
                            throw 'Restored Registry output does not match the prior output hash.'
                        }
                        if ($null -ne $BeforeFailedCandidateCleanupTestHook) {
                            & $BeforeFailedCandidateCleanupTestHook $root $snapshot $failedCandidate
                        }
                        $failedCandidateRemoved = Remove-ESRegistryRecoveryFileIfExpected `
                            -Path $failedCandidate `
                            -ExpectedHashes @([string]$outputBaseline.Sha256, [string]$manifestFileHash) `
                            -ArtifactLabel 'Registry failed candidate'
                        if ($failedCandidateRemoved) {
                            Write-Warning 'Registry replacement failed; the prior output was restored and the failed candidate was removed.'
                        } else {
                            Write-Warning "Registry replacement failed; the prior output was restored and the failed candidate was preserved: $failedCandidate"
                        }
                    } catch {
                        Write-Warning "Registry replacement failed and verified rollback failed; preserving recovery files near: $replacementBackup"
                    }
                } elseif ($currentState.Exists -and [string]::Equals(
                        [string]$currentState.Sha256,
                        [string]$outputBaseline.Sha256,
                        [StringComparison]::Ordinal)) {
                    $backupRemoved = Remove-ESRegistryRecoveryFileIfExpected `
                        -Path $replacementBackup `
                        -ExpectedHashes @([string]$outputBaseline.Sha256, [string]$manifestFileHash) `
                        -ArtifactLabel 'Registry replacement backup'
                } else {
                    Write-Warning "Preserving unverified Registry replacement backup: $replacementBackup"
                }
            }
        }
    }
} finally {
    if ($null -ne $buildLock) {
        $buildLock.Dispose()
    }
    if ($lockAcquired -and (Test-Path -LiteralPath $lockPath)) {
        try {
            [IO.File]::Delete($lockPath)
        } catch {
            Write-Warning "Failed to clean Registry build lock file: $lockPath"
        }
    }
}

$manifestJson
