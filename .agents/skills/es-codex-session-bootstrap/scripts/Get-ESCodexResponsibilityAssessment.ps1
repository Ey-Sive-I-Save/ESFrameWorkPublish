[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ArchivePath,
    [string]$ResponsibilityKey = ''
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..') -ErrorAction Stop).Path
$archive = (Resolve-Path -LiteralPath $ArchivePath -ErrorAction Stop).Path
if (-not $archive.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'ArchivePath must remain inside the ES project root.'
}
$text = [IO.File]::ReadAllText($archive, [Text.UTF8Encoding]::new($false))

# Topic profiles are content responsibilities, not lifecycle operations. The
# assessment is deliberately deterministic and only reads formal T nodes.
$profiles = [ordered]@{
    'es-editor-foundation-governance' = @(
        'editor|window|dialog|inspector|section|compositeshader|workbench|reload|playmode|sleep|visual|ui',
        '\u7f16\u8f91\u5668|\u7a97\u53e3|\u5bf9\u8bdd\u6846|\u68c0\u67e5\u5668|\u4f11\u7720|\u5de5\u4f5c\u53f0|\u5f39\u7a97|\u5e03\u5c40'
    )
    'es-session-bootstrap-maintenance' = @(
        'codex|session|bootstrap|handoff|handover|receipt|launch envelope|responsibility',
        '\u4f1a\u8bdd|\u4ea4\u63a5|\u65b0\u7a97\u53e3|\u804c\u8d23|\u5f15\u5bfc|\u56de\u6267|\u542f\u52a8'
    )
    'es-aibrain-architecture' = @(
        'aibrain|aicommand|taskcontract|knowledge|automation|worker|mcp|planhash|task routing',
        '\u67b6\u6784|\u8def\u7531|\u77e5\u8bc6|\u81ea\u52a8\u5316|\u5de5\u4f5c\u8fdb\u7a0b|\u547d\u4ee4|\u8ba1\u5212'
    )
}

$nodeMatches = [regex]::Matches($text, '(?ms)^### T\d{3}.*?(?=^### T\d{3}|^## 覆盖审计|\z)')
$nodes = @($nodeMatches | ForEach-Object { $_.Value })
$scores = [ordered]@{}
$matchedNodes = [ordered]@{}
foreach ($key in $profiles.Keys) {
    $scores[$key] = 0
    $matchedNodes[$key] = 0
    foreach ($node in $nodes) {
        $nodeScore = 0
        foreach ($pattern in $profiles[$key]) {
            $nodeScore += @([regex]::Matches($node, '(?i)' + $pattern)).Count
        }
        if ($nodeScore -gt 0) {
            $scores[$key] += $nodeScore
            $matchedNodes[$key]++
        }
    }
}

$ordered = @($scores.GetEnumerator() | Sort-Object Value -Descending)
$top = if ($ordered.Count -gt 0) { $ordered[0] } else { $null }
$second = if ($ordered.Count -gt 1) { $ordered[1] } else { $null }
$total = [int](($scores.Values | Measure-Object -Sum).Sum)
$confidence = if ($total -gt 0) { [math]::Round(([double]$top.Value / $total), 3) } else { 0 }
$clearWinner = $null -ne $top -and $top.Value -gt 0 -and ($null -eq $second -or $top.Value -gt $second.Value)
$recommended = if ($clearWinner) { [string]$top.Key } else { '' }
$status = if ($nodes.Count -eq 0) { 'insufficient-history' } elseif (-not $clearWinner -or $confidence -lt 0.45) { 'ambiguous-history' } else { 'assessed' }
$matchesRequested = [string]::IsNullOrWhiteSpace($ResponsibilityKey) -or [string]::Equals($ResponsibilityKey, $recommended, [StringComparison]::OrdinalIgnoreCase)

[pscustomobject]@{
    archivePath = $archive
    nodeCount = $nodes.Count
    status = $status
    recommendedResponsibilityKey = $recommended
    requestedResponsibilityKey = $ResponsibilityKey
    requestedMatchesRecommendation = $matchesRequested
    confidence = $confidence
    scores = [pscustomobject]$scores
    matchedNodes = [pscustomobject]$matchedNodes
    rule = 'A handoff responsibility must match the dominant formal T-node topic; ambiguous or insufficient histories require explicit review.'
} | ConvertTo-Json -Depth 6
