param(
    [Parameter(Mandatory = $true)]
    [string]$Query,

    [string]$HistoryPath = (Join-Path $env:USERPROFILE '.codex\history.jsonl'),

    [string]$SessionsRoot = (Join-Path $env:USERPROFILE '.codex\sessions'),

    [string]$ProjectPath = '',

    [datetime]$Since = [datetime]::MinValue,

    [datetime]$Until = [datetime]::MaxValue,

    [ValidateRange(1, 50)]
    [int]$Top = 10,

    [switch]$IncludeMissingFiles,

    [switch]$AsJson
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new()

function Normalize-SearchText([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) {
        return ''
    }

    return (($value.ToLowerInvariant() -replace '[^\p{L}\p{Nd}]', '').Trim())
}

function Get-BigramSet([string]$value) {
    $set = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    if ($value.Length -eq 1) {
        $null = $set.Add($value)
        return $set
    }

    for ($i = 0; $i -lt $value.Length - 1; $i++) {
        $null = $set.Add($value.Substring($i, 2))
    }
    return $set
}

function Get-BigramDice([Collections.Generic.HashSet[string]]$left, [string]$rightText) {
    if ($left.Count -eq 0 -or [string]::IsNullOrEmpty($rightText)) {
        return 0.0
    }

    $right = Get-BigramSet $rightText
    if ($right.Count -eq 0) {
        return 0.0
    }

    $intersection = 0
    foreach ($item in $left) {
        if ($right.Contains($item)) {
            $intersection++
        }
    }
    return (2.0 * $intersection) / ($left.Count + $right.Count)
}

function Get-Excerpt([string]$value, [int]$limit = 180) {
    if ([string]::IsNullOrWhiteSpace($value)) {
        return ''
    }

    $text = ($value -replace '\r?\n', ' ') -replace '\s+', ' '
    $text = $text.Trim()
    if ($text.Length -gt $limit) {
        return $text.Substring(0, $limit) + '...'
    }
    return $text
}

function Get-Confidence([double]$score) {
    if ($score -ge 99.9) { return 'ExactSessionId' }
    if ($score -ge 80.0) { return 'HighCandidate' }
    if ($score -ge 50.0) { return 'ManualReview' }
    return 'LowCandidate'
}

if (-not (Test-Path -LiteralPath $HistoryPath -PathType Leaf)) {
    throw "History index not found: $HistoryPath"
}
if (-not (Test-Path -LiteralPath $SessionsRoot -PathType Container)) {
    throw "Sessions root not found: $SessionsRoot"
}

$queryNormalized = Normalize-SearchText $Query
if ([string]::IsNullOrEmpty($queryNormalized)) {
    throw 'Query must contain at least one letter or digit.'
}
$queryBigrams = Get-BigramSet $queryNormalized
$queryTokens = @(
    $Query.ToLowerInvariant() -split '[^\p{L}\p{Nd}_]+' |
        Where-Object { $_.Length -ge 2 } |
        Select-Object -Unique
)

$groups = @{}
$parseErrors = 0
Get-Content -LiteralPath $HistoryPath -Encoding UTF8 | ForEach-Object {
    try {
        $row = $_ | ConvertFrom-Json
        $sessionId = [string]$row.session_id
        if ([string]::IsNullOrWhiteSpace($sessionId)) {
            return
        }
        if (-not $groups.ContainsKey($sessionId)) {
            $groups[$sessionId] = [pscustomobject]@{
                SessionId = $sessionId
                FirstTs = [long]$row.ts
                LastTs = [long]$row.ts
                Messages = [Collections.Generic.List[string]]::new()
                SearchMessages = [Collections.Generic.List[string]]::new()
            }
        }

        $group = $groups[$sessionId]
        $timestamp = [long]$row.ts
        if ($timestamp -lt $group.FirstTs) { $group.FirstTs = $timestamp }
        if ($timestamp -gt $group.LastTs) { $group.LastTs = $timestamp }
        $text = [string]$row.text
        $group.Messages.Add($text)
        if ($text.Length -gt 5000) {
            $text = $text.Substring(0, 4000) + $text.Substring($text.Length - 1000)
        }
        $group.SearchMessages.Add((Normalize-SearchText $text))
    }
    catch {
        $parseErrors++
    }
}

$sessionFiles = @{}
Get-ChildItem -LiteralPath $SessionsRoot -Recurse -File -Filter 'rollout-*.jsonl' | ForEach-Object {
    if ($_.BaseName -match '([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$') {
        $sessionFiles[$Matches[1]] = $_.FullName
    }
}

$results = [Collections.Generic.List[object]]::new()
foreach ($group in $groups.Values) {
    $firstDate = [DateTimeOffset]::FromUnixTimeSeconds($group.FirstTs).LocalDateTime
    $lastDate = [DateTimeOffset]::FromUnixTimeSeconds($group.LastTs).LocalDateTime
    if ($lastDate -lt $Since) { continue }
    if ($firstDate -gt $Until) { continue }

    $exactId = (Normalize-SearchText $group.SessionId) -eq $queryNormalized -or $queryNormalized.Contains((Normalize-SearchText $group.SessionId))
    $exactText = $false
    $bestDice = 0.0
    $matchedTokens = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($message in $group.SearchMessages) {
        if (-not $exactText -and $message.Contains($queryNormalized)) {
            $exactText = $true
        }
        foreach ($token in $queryTokens) {
            if ($message.Contains((Normalize-SearchText $token))) {
                $null = $matchedTokens.Add($token)
            }
        }
        if (-not $exactId -and -not $exactText) {
            $dice = Get-BigramDice $queryBigrams $message
            if ($dice -gt $bestDice) { $bestDice = $dice }
        }
    }

    $tokenCoverage = if ($queryTokens.Count -gt 0) { $matchedTokens.Count / [double]$queryTokens.Count } else { 0.0 }
    if ($exactId) {
        $score = 100.0
    }
    elseif ($exactText) {
        $score = 90.0
    }
    else {
        $score = [Math]::Min(89.0, 55.0 * $tokenCoverage + 35.0 * $bestDice)
    }

    $path = if ($sessionFiles.ContainsKey($group.SessionId)) { $sessionFiles[$group.SessionId] } else { '' }
    if ([string]::IsNullOrWhiteSpace($path) -and -not $IncludeMissingFiles) {
        continue
    }
    $cwd = ''
    if (-not [string]::IsNullOrWhiteSpace($path)) {
        try {
            $meta = Get-Content -LiteralPath $path -Encoding UTF8 -TotalCount 1 | ConvertFrom-Json
            if ($meta.type -eq 'session_meta') { $cwd = [string]$meta.payload.cwd }
        }
        catch {
            $cwd = ''
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($ProjectPath)) {
        $projectNormalized = Normalize-SearchText $ProjectPath
        $projectMatched = (Normalize-SearchText $cwd).Contains($projectNormalized)
        if (-not $projectMatched) {
            foreach ($message in $group.SearchMessages) {
                if ($message.Contains($projectNormalized)) {
                    $projectMatched = $true
                    break
                }
            }
        }
        if ($projectMatched) {
            $score = [Math]::Min(99.0, $score + 10.0)
        }
        else {
            $score = [Math]::Max(0.0, $score - 10.0)
        }
    }

    $results.Add([pscustomobject]@{
        Score = [Math]::Round($score, 2)
        Confidence = Get-Confidence $score
        SessionId = $group.SessionId
        SessionPath = $path
        SessionPathExists = -not [string]::IsNullOrWhiteSpace($path)
        Cwd = $cwd
        FirstLocal = $firstDate.ToString('yyyy-MM-dd HH:mm:ss')
        LastLocal = $lastDate.ToString('yyyy-MM-dd HH:mm:ss')
        UserMessages = $group.Messages.Count
        FirstPrompt = Get-Excerpt $group.Messages[0]
        LastPrompt = Get-Excerpt $group.Messages[$group.Messages.Count - 1]
        ParseErrors = $parseErrors
    })
}

$topResults = @($results | Sort-Object Score, LastLocal -Descending | Select-Object -First $Top)
if ($AsJson) {
    $topResults | ConvertTo-Json -Depth 4
}
else {
    $topResults
}
