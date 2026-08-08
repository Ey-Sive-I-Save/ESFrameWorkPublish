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
$nodeNumbers = @($nodeMatches | ForEach-Object { [int]$_.Groups[1].Value })
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

$result = [pscustomobject]@{
    SessionPath = (Resolve-Path -LiteralPath $SessionPath).Path
    ArchivePath = (Resolve-Path -LiteralPath $ArchivePath).Path
    UserMessages = $userMessages
    TimelineNodes = $nodeNumbers.Count
    Stages = $stageMatches.Count
    FieldLines = $fieldMatches.Count
    ParseErrors = $parseErrors
    Passed = ($issues.Count -eq 0)
    Issues = @($issues)
}
$result | ConvertTo-Json -Depth 4
if ($issues.Count -gt 0) { exit 2 }
