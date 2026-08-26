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
# assessment is deterministic and reads only the user-request field from each
# formal T node when that field is available.
$profiles = [ordered]@{
    'es-editor-foundation-governance' = @(
        'editor(?:window)?|inspector|serializedproperty|\bundo\b|\bdirty\b|drawer|shadergui|compositeshader|workbench|reload|playmode|propertytree',
        '\u7f16\u8f91\u5668|\u68c0\u67e5\u5668|\u5e8f\u5217\u5316|\u64a4\u9500|\u91cd\u505a|\u5de5\u4f5c\u53f0|\u4f11\u7720|\u7ed8\u5236\u5668|\u5c5e\u6027\u6811|\u5f39\u7a97'
    )
    'es-session-bootstrap-maintenance' = @(
        '(?:fix|change|modify|test|audit|maintain|repair|implement|debug|diagnos\w*|govern)[^\r\n]{0,120}(?:session bootstrap|launch envelope|handoff receipt|sessions\.json|session registry|responsibility assessment|contextaccepted|start-escodexsession|complete-escodexhandoff|recordid|taskkey|mailbox|session hook)|(?:session bootstrap|launch envelope|handoff receipt|sessions\.json|session registry|responsibility assessment|contextaccepted|start-escodexsession|complete-escodexhandoff|recordid|taskkey|mailbox|session hook)[^\r\n]{0,120}(?:fix|change|modify|test|audit|maintain|repair|implement|debug|diagnos\w*|govern)',
        '(?:fix|change|modify|test|audit|maintain|repair|implement|debug|diagnos\w*|govern)[^\r\n]{0,120}(?:codex|session|handoff)|(?:codex|session|handoff)[^\r\n]{0,120}(?:fix|change|modify|test|audit|maintain|repair|implement|debug|diagnos\w*|govern)',
        '(?:\u4fee\u590d|\u4fee\u6539|\u7ef4\u62a4|\u6d4b\u8bd5|\u5ba1\u67e5|\u5b9e\u73b0|\u8bca\u65ad|\u6cbb\u7406)[^\r\n]{0,120}(?:\u4f1a\u8bdd|\u4ea4\u63a5|\u804c\u8d23|\u5f15\u5bfc|\u56de\u6267|\u542f\u52a8)|(?:\u4f1a\u8bdd|\u4ea4\u63a5|\u804c\u8d23|\u5f15\u5bfc|\u56de\u6267|\u542f\u52a8)[^\r\n]{0,120}(?:\u4fee\u590d|\u4fee\u6539|\u7ef4\u62a4|\u6d4b\u8bd5|\u5ba1\u67e5|\u5b9e\u73b0|\u8bca\u65ad|\u6cbb\u7406)'
    )
    'es-aibrain-architecture' = @(
        'aicommand|taskcontract|planhash|task routing',
        'aibrain[^\r\n]{0,120}(?:architecture|routing|orchestration|design|implement\w*|audit|govern\w*)|(?:architecture|routing|orchestration|design|implement\w*|audit|govern\w*)[^\r\n]{0,120}aibrain',
        'AI\s*\u5927\u8111[^\r\n]{0,120}(?:\u67b6\u6784|\u8def\u7531|\u7f16\u6392|\u8bbe\u8ba1|\u5b9e\u73b0|\u5ba1\u67e5|\u6cbb\u7406)|(?:\u67b6\u6784|\u8def\u7531|\u7f16\u6392|\u8bbe\u8ba1|\u5b9e\u73b0|\u5ba1\u67e5|\u6cbb\u7406)[^\r\n]{0,120}AI\s*\u5927\u8111'
    )
    'es-ui-knowledge-governance' = @(
        'screen ?spec|asset ?manifest|layout ?plan|behavior ?spec|materializer|fixture(?: scene)?|game-ui-component-registry',
        '(?:knowledge|knowledgeindex|\u77e5\u8bc6|\u6b63\u5f0f\u6ce8\u518c|\u6761\u76ee|\u7f3a\u53e3|\u8865\u5f3a|\u6cbb\u7406)[^\r\n]{0,160}\bui\b|\bui\b[^\r\n]{0,160}(?:knowledge|knowledgeindex|\u77e5\u8bc6|\u6b63\u5f0f\u6ce8\u518c|\u6761\u76ee|\u7f3a\u53e3|\u8865\u5f3a|\u6cbb\u7406)',
        '(?:(?:\bui\b|\u754c\u9762)[^\r\n]{0,160}(?:\u5546\u4e1a\u6e38\u620f|\u89c6\u89c9\u8bbe\u8ba1\u77e5\u8bc6|\u5e03\u5c40\u51b3\u7b56\u77e5\u8bc6|\u54cd\u5e94\u5f0f\u77e5\u8bc6|\u8d44\u6e90\u77e5\u8bc6|\u72b6\u6001\u4e0e\u4ea4\u4e92\u77e5\u8bc6|ES\s*\u5de5\u7a0b\u77e5\u8bc6)|(?:\u5546\u4e1a\u6e38\u620f|\u89c6\u89c9\u8bbe\u8ba1\u77e5\u8bc6|\u5e03\u5c40\u51b3\u7b56\u77e5\u8bc6|\u54cd\u5e94\u5f0f\u77e5\u8bc6|\u8d44\u6e90\u77e5\u8bc6|\u72b6\u6001\u4e0e\u4ea4\u4e92\u77e5\u8bc6|ES\s*\u5de5\u7a0b\u77e5\u8bc6)[^\r\n]{0,160}(?:\bui\b|\u754c\u9762))'
    )
}

$profilePriorities = @{
    'es-editor-foundation-governance' = 10
    'es-aibrain-architecture' = 10
    'es-session-bootstrap-maintenance' = 20
    'es-ui-knowledge-governance' = 30
}

$nodeMatches = [regex]::Matches($text, '(?ms)^### T\d{3}.*?(?=^### T\d{3}|^## 覆盖审计|\z)')
$nodes = @($nodeMatches | ForEach-Object { $_.Value })
$scores = [ordered]@{}
$matchedNodes = [ordered]@{}
foreach ($key in $profiles.Keys) {
    $scores[$key] = 0
    $matchedNodes[$key] = 0
}

$assignedNodeCount = 0
foreach ($node in $nodes) {
    $requestMatch = [regex]::Match($node, '(?m)^- \*\*\u7528\u6237\u8981\u6c42\uff08\u539f\u6587\u8282\u9009\uff09\*\*\uff1a(.*)$')
    $topicText = if ($requestMatch.Success) { $requestMatch.Groups[1].Value } else { $node }
    $nodeScores = [ordered]@{}
    foreach ($key in $profiles.Keys) {
        $nodeScore = 0
        foreach ($pattern in $profiles[$key]) {
            if ([regex]::IsMatch($topicText, '(?i)' + $pattern)) { $nodeScore++ }
        }
        $nodeScores[$key] = $nodeScore
        if ($nodeScore -gt 0) {
            $matchedNodes[$key]++
        }
    }

    $maxNodeScore = [int](($nodeScores.Values | Measure-Object -Maximum).Maximum)
    if ($maxNodeScore -le 0) { continue }
    $topCandidates = @($nodeScores.GetEnumerator() | Where-Object { [int]$_.Value -eq $maxNodeScore })
    $maxPriority = [int](($topCandidates | ForEach-Object { [int]$profilePriorities[[string]$_.Key] } | Measure-Object -Maximum).Maximum)
    $winnerCandidates = @($topCandidates | Where-Object { [int]$profilePriorities[[string]$_.Key] -eq $maxPriority })
    if ($winnerCandidates.Count -ne 1) { continue }

    $winner = [string]$winnerCandidates[0].Key
    $scores[$winner]++
    $assignedNodeCount++
}

$ordered = @($scores.GetEnumerator() | Sort-Object -Property @(
        @{ Expression = { [int]$_.Value }; Descending = $true },
        @{ Expression = { [string]$_.Key }; Descending = $false }
    ))
$top = if ($ordered.Count -gt 0) { $ordered[0] } else { $null }
$second = if ($ordered.Count -gt 1) { $ordered[1] } else { $null }
$confidence = if ($assignedNodeCount -gt 0) { [math]::Round(([double]$top.Value / $assignedNodeCount), 3) } else { 0 }
$minimumDominantNodes = 2
$clearWinner = $null -ne $top -and $top.Value -ge $minimumDominantNodes -and ($null -eq $second -or $top.Value -gt $second.Value)
$knownResponsibility = -not [string]::IsNullOrWhiteSpace($ResponsibilityKey) -and $profiles.Contains($ResponsibilityKey)
$explicitScopeMatchCount = if ($knownResponsibility) { [int]$matchedNodes[$ResponsibilityKey] } else { 0 }
$explicitScopeAccepted = $knownResponsibility -and $explicitScopeMatchCount -ge $minimumDominantNodes
$recommended = if ($explicitScopeAccepted) { $ResponsibilityKey } elseif ($clearWinner) { [string]$top.Key } else { '' }
$status = if ($nodes.Count -eq 0 -or $assignedNodeCount -eq 0) { 'insufficient-history' } elseif ($explicitScopeAccepted) { 'assessed' } elseif (-not $clearWinner -or $confidence -lt 0.45) { 'ambiguous-history' } else { 'assessed' }
$matchesRequested = [string]::IsNullOrWhiteSpace($ResponsibilityKey) -or [string]::Equals($ResponsibilityKey, $recommended, [StringComparison]::OrdinalIgnoreCase)

[pscustomobject]@{
    archivePath = $archive
    nodeCount = $nodes.Count
    status = $status
    recommendedResponsibilityKey = $recommended
    requestedResponsibilityKey = $ResponsibilityKey
    requestedMatchesRecommendation = $matchesRequested
    confidence = $confidence
    assessmentMode = if ($explicitScopeAccepted) { 'explicit-responsibility-scope' } else { 'automatic-full-history' }
    explicitScopeMatchCount = $explicitScopeMatchCount
    scores = [pscustomobject]$scores
    matchedNodes = [pscustomobject]$matchedNodes
    assignedNodeCount = $assignedNodeCount
    unassignedNodeCount = $nodes.Count - $assignedNodeCount
    scoringModel = 'per-node-specificity-v1'
    rule = 'Each formal T node casts at most one bounded topic vote from its user-request field; repeated terms do not add votes, and fewer than two dominant nodes remain ambiguous.'
} | ConvertTo-Json -Depth 6
