param(
    [Parameter(Mandatory = $true)]
    [string]$SessionPath,
    [Parameter(Mandatory = $true)]
    [string]$ArchivePath
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new()

if (-not (Test-Path -LiteralPath $SessionPath -PathType Leaf)) { throw "Session file not found: $SessionPath" }
if (-not (Test-Path -LiteralPath $ArchivePath -PathType Leaf)) { throw "Archive file not found: $ArchivePath" }

$userMessages = 0
$parseErrors = 0
foreach ($line in (Get-Content -LiteralPath $SessionPath -Encoding UTF8)) {
    try {
        $row = $line | ConvertFrom-Json
        if ($row.type -eq 'event_msg' -and $row.payload.type -eq 'user_message') { $userMessages++ }
    }
    catch { $parseErrors++ }
}

$archive = Get-Content -LiteralPath $ArchivePath -Encoding UTF8 -Raw
$nodeMatches = [regex]::Matches($archive, '(?m)^### T(\d{3})')
$stageMatches = [regex]::Matches($archive, '(?m)^### (?:Stage S\d{3}|\u9636\u6bb5)')
$fieldMatches = [regex]::Matches($archive, '(?m)^-\s+\*\*[^*]+\*\*(?::|\uFF1A)')
$archiveTimeMatches = [regex]::Matches($archive, '(?m)^\u5F52\u6863\u65F6\u95F4\s*(?::|\uFF1A)\s*\S+')
$nodeNumbers = @($nodeMatches | ForEach-Object { [int]$_.Groups[1].Value })
$nodeBlocks = @([regex]::Split($archive, '(?m)(?=^### T\d{3}\b)') | Where-Object { $_ -match '(?m)^### T\d{3}\b' })
$issues = [Collections.Generic.List[string]]::new()

if ($parseErrors -gt 0) { $issues.Add("JSONL parse errors: $parseErrors") }
if ($nodeNumbers.Count -ne $userMessages) { $issues.Add("User messages $userMessages != timeline nodes $($nodeNumbers.Count)") }
for ($i = 0; $i -lt $nodeNumbers.Count; $i++) {
    if ($nodeNumbers[$i] -ne ($i + 1)) {
        $issues.Add("Timeline numbering is not contiguous at position $($i + 1)")
        break
    }
}
if ($userMessages -gt 0 -and $stageMatches.Count -eq 0) { $issues.Add('Missing Stage containers') }
if ($userMessages -gt 0 -and $fieldMatches.Count -lt ($userMessages * 5)) { $issues.Add('Too few required node fields; every node needs request, scope, evidence, result and remaining-work fields') }
$requiredRecentSummaries = [Math]::Min(10, $nodeBlocks.Count)
$recentBlocks = @($nodeBlocks | Select-Object -Last $requiredRecentSummaries)
$missingRecentRequestNodes = [Collections.Generic.List[string]]::new()
$missingRecentSummaryNodes = [Collections.Generic.List[string]]::new()
$missingRecentRemainingNodes = [Collections.Generic.List[string]]::new()
foreach ($block in $recentBlocks) {
    $nodeMatch = [regex]::Match($block, '(?m)^### T(\d{3})\b')
    $nodeLabel = if ($nodeMatch.Success) { 'T' + $nodeMatch.Groups[1].Value } else { 'unknown' }
    if ($block -notmatch '(?m)^-\s+\*\*[^\r\n]*\u7528\u6237\u8981\u6C42[^\r\n]*\*\*(?::|\uFF1A)') { $missingRecentRequestNodes.Add($nodeLabel) }
    if ($block -notmatch '(?m)^-\s+\*\*[^\r\n]*\u5F53\u65F6\u7B54\u590D\u6458\u8981[^\r\n]*\*\*(?::|\uFF1A)') { $missingRecentSummaryNodes.Add($nodeLabel) }
    if ($block -notmatch '(?m)^-\s+\*\*[^\r\n]*(?:\u5269\u4F59\u9879|\u672A\u5B8C\u6210|\u4E0D\u786E\u5B9A)[^\r\n]*\*\*(?::|\uFF1A)') { $missingRecentRemainingNodes.Add($nodeLabel) }
}
if ($missingRecentRequestNodes.Count -gt 0) { $issues.Add('Missing recent user-request fields: ' + ($missingRecentRequestNodes -join ', ')) }
if ($missingRecentSummaryNodes.Count -gt 0) { $issues.Add('Missing recent answer-summary fields: ' + ($missingRecentSummaryNodes -join ', ')) }
if ($missingRecentRemainingNodes.Count -gt 0) { $issues.Add('Missing recent remaining-work/uncertainty fields: ' + ($missingRecentRemainingNodes -join ', ')) }
if ($archiveTimeMatches.Count -ne 1) { $issues.Add('Archive must contain exactly one explicit archive timestamp') }

$result = [pscustomobject]@{
    SessionPath = (Resolve-Path -LiteralPath $SessionPath).Path
    ArchivePath = (Resolve-Path -LiteralPath $ArchivePath).Path
    UserMessages = $userMessages
    TimelineNodes = $nodeNumbers.Count
    Stages = $stageMatches.Count
    FieldLines = $fieldMatches.Count
    ConversationSummaries = @([regex]::Matches($archive, '(?m)^-\s+\*\*[^\r\n]*\u5F53\u65F6\u7B54\u590D\u6458\u8981[^\r\n]*\*\*(?::|\uFF1A)')).Count
    RequiredRecentSummaries = $requiredRecentSummaries
    ArchiveTimestamps = $archiveTimeMatches.Count
    MissingRecentRequestNodes = @($missingRecentRequestNodes)
    MissingRecentSummaryNodes = @($missingRecentSummaryNodes)
    MissingRecentRemainingNodes = @($missingRecentRemainingNodes)
    ParseErrors = $parseErrors
    Passed = ($issues.Count -eq 0)
    Issues = @($issues)
}
$result | ConvertTo-Json -Depth 4
if ($issues.Count -gt 0) { exit 2 }
