[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$ProjectRoot,
    [string]$OutputPath = 'ES/Output/KnowledgeValidation/refresh-plan.json',
    [ValidateRange(0, 1000)] [int]$SampleDelayMilliseconds = 10
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$entryRoot = Join-Path $root 'Documentation/AIKnowledge'

function Get-Relative([string]$Path) { $Path.Substring($root.Length).TrimStart('\', '/').Replace('\', '/') }
function Get-SourceRefs([string]$Text) {
    $pattern = '(?m)^-\s+(.+?)\s+.*?([0-9a-f]{64}).*$'
    @([regex]::Matches($Text, $pattern) | ForEach-Object {
        [pscustomobject]@{ path = $_.Groups[1].Value.Trim('`', ' '); declaredHash = $_.Groups[2].Value }
    })
}
function Get-DeclaredContentHash([string]$Text) {
    $match = [regex]::Match($Text, '(?m)^.*ContentHash.*?([0-9a-f]{64}).*$')
    if ($match.Success) { return $match.Groups[1].Value }
    return ''
}
function Get-StableFileHash([string]$Path, [int]$DelayMilliseconds) {
    $first = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($DelayMilliseconds -gt 0) { Start-Sleep -Milliseconds $DelayMilliseconds }
    $second = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    [pscustomobject]@{ first = $first; second = $second; stable = ($first -ceq $second) }
}

if (-not (Test-Path -LiteralPath $entryRoot -PathType Container)) { throw "Knowledge entries directory missing: $entryRoot" }
$changes = [System.Collections.Generic.List[object]]::new()
$samplesBySource = @{}
foreach ($entryPath in Get-ChildItem -LiteralPath $entryRoot -Filter '*.md' -File -Recurse | Sort-Object FullName) {
    $text = [IO.File]::ReadAllText($entryPath.FullName, [Text.UTF8Encoding]::new($false, $true))
    if ($text -notmatch '(?m)^(?:##\s+SourceRefs|`SourceRefs`\s*:|SourceRefs\s*:)') { continue }
    foreach ($ref in (Get-SourceRefs $text)) {
        $sourcePath = Join-Path $root ($ref.path.Replace('/', '\'))
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { continue }
        $sourceKey = $ref.path.Replace('\', '/').ToLowerInvariant()
        if (-not $samplesBySource.ContainsKey($sourceKey)) {
            $samplesBySource[$sourceKey] = Get-StableFileHash $sourcePath $SampleDelayMilliseconds
        }
        $sample = $samplesBySource[$sourceKey]
        $actual = $sample.second
        if (-not $sample.stable) {
            $changes.Add([pscustomobject]@{
                entry = Get-Relative $entryPath.FullName
                source = $ref.path
                declaredHash = $ref.declaredHash
                currentHash = $actual
                firstSampleHash = $sample.first
                snapshotStable = $false
                declaredContentHash = Get-DeclaredContentHash $text
                action = 'wait-for-source-stability'
            })
        } elseif ($actual -cne $ref.declaredHash) {
            $changes.Add([pscustomobject]@{
                entry = Get-Relative $entryPath.FullName
                source = $ref.path
                declaredHash = $ref.declaredHash
                currentHash = $actual
                snapshotStable = $true
                declaredContentHash = Get-DeclaredContentHash $text
                action = 'review-and-refresh-source-ref'
            })
        }
    }
}
$output = Join-Path $root ($OutputPath.Replace('/', '\'))
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $output) | Out-Null
$unstableFindingCount = @($changes | Where-Object { $_.snapshotStable -eq $false }).Count
$planCanonical = (@($changes | ForEach-Object { "$($_.entry)|$($_.source)|$($_.declaredHash)|$($_.currentHash)|$($_.snapshotStable)|$($_.action)" } | Sort-Object) -join "`n")
$planSha = [Security.Cryptography.SHA256]::Create()
try { $planHash = ([BitConverter]::ToString($planSha.ComputeHash([Text.Encoding]::UTF8.GetBytes($planCanonical)))).Replace('-', '').ToLowerInvariant() }
finally { $planSha.Dispose() }
$report = [ordered]@{
    schemaVersion = 1
    toolId = 'es-knowledge-validator.refresh-plan'
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    mutatesSources = $false
    mutatesKnowledge = $false
    planHash = $planHash
    findingCount = $changes.Count
    findings = @($changes)
    unstableFindingCount = $unstableFindingCount
    nextAction = if ($changes.Count -eq 0) { 'No SourceRef drift detected.' } elseif ($unstableFindingCount -gt 0) { 'Wait for unstable sources to settle, rerun this plan, then review stable drift before refreshing entries.' } else { 'Review each stable finding, update the entry and index deliberately, then rerun Invoke-ESKnowledgeValidation.ps1.' }
}
[IO.File]::WriteAllText($output, ($report | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))
$report | ConvertTo-Json -Depth 8
