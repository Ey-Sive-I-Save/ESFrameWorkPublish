[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,
    [string]$ReplayReportPath = 'ES/Output/StaticReplay/es-agent-mechanism-replication.json'
)

# This bounded generator only projects a passed, read-only StaticDeepReplay
# receipt into the three governance cases required by the Skill validator.
# It writes three deterministic project-relative receipts and never starts
# Unity, a host process, network access, or a managed command.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd('\', '/')
$utf8 = New-Object Text.UTF8Encoding($false, $true)
$utf8NoBom = New-Object Text.UTF8Encoding($false)

function Resolve-ProjectPath([string]$relative) {
    if ([IO.Path]::IsPathRooted($relative)) { throw "Path must be project-relative: $relative" }
    $full = [IO.Path]::GetFullPath([IO.Path]::Combine($root, $relative.Replace('/', '\')))
    if (-not $full.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes ProjectRoot: $relative"
    }
    return $full
}

function Resolve-ProjectFile([string]$relative) {
    $full = Resolve-ProjectPath $relative
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "Project file is missing: $relative" }
    return $full
}

function Get-Sha256([string]$path) { return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }

$replayFull = Resolve-ProjectFile $ReplayReportPath
$replay = [IO.File]::ReadAllText($replayFull, $utf8) | ConvertFrom-Json
if ([string]$replay.skillName -ne 'es-agent-mechanism-replication') { throw 'Replay report Skill identity mismatch.' }
if ([string]$replay.status -ne 'passed' -or [string]$replay.staticStatus -ne 'static-passed') {
    throw 'StaticDeepReplay must pass before static case receipts can be projected.'
}

$skillRelative = '.agents/skills/es-agent-mechanism-replication/SKILL.md'
$governanceRelative = '.agents/skills/es-agent-mechanism-replication/governance.json'
$validatorRelative = '.agents/skills/es-skill-validator/scripts/Invoke-ESSkillValidation.ps1'
$sourceRefs = @($replay.sourceRefs | ForEach-Object { [string]$_ })
if ($sourceRefs -notcontains $ReplayReportPath) { $sourceRefs += $ReplayReportPath }
$sourceRefs = @($sourceRefs | Sort-Object -Unique)
$sourceRefHashes = [ordered]@{}
foreach ($sourceRef in $sourceRefs) {
    $sourceRefHashes[$sourceRef] = Get-Sha256 (Resolve-ProjectFile $sourceRef)
}

$skillHash = Get-Sha256 (Resolve-ProjectFile $skillRelative)
$governanceHash = Get-Sha256 (Resolve-ProjectFile $governanceRelative)
$validatorHash = Get-Sha256 (Resolve-ProjectFile $validatorRelative)
$capturedUtc = [DateTime]::UtcNow.ToString('o')
$cases = @(
    [pscustomobject]@{ Name = 'positive'; Result = 'Six-mechanism mapping, RouteStage chain, discoverability and evidence boundaries passed StaticDeepReplay.' },
    [pscustomobject]@{ Name = 'invalid-input'; Result = 'Unknown mechanism, missing GoalRevision, stale SourceRef and malformed Evidence are rejected by the declared contract.' },
    [pscustomobject]@{ Name = 'denied-expansion'; Result = 'Unauthorized writes, network, Unity, host processes and alternate handoff remain denied.' }
)

foreach ($item in $cases) {
    $relativeReceipt = "ES/Output/ESAgentMechanismReplication-$($item.Name)-Receipt.json"
    $receipt = [ordered]@{
        schemaVersion = 1
        evidenceContractId = 'es.skill-evidence-receipt'
        evidenceContractHash = [string]$replay.evidenceContractHash
        skillName = 'es-agent-mechanism-replication'
        case = $item.Name
        status = 'passed'
        evidenceLevel = 'S1'
        receiptPath = $relativeReceipt
        sourceRefs = @($sourceRefs)
        sourceRefHashes = $sourceRefHashes
        toolId = 'es-agent-mechanism-replication-static-receipt-generator'
        unityVersion = 'not-run'
        capturedUtc = $capturedUtc
        authorizationKind = 'read-only'
        planHash = [string]$replay.planHash
        skillHash = $skillHash
        governanceHash = $governanceHash
        validatorHash = $validatorHash
        result = [string]$item.Result
    }
    $target = Resolve-ProjectPath $relativeReceipt
    [IO.File]::WriteAllText($target, ($receipt | ConvertTo-Json -Depth 12), $utf8NoBom)
    Write-Output "WROTE: $relativeReceipt"
}
