[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$ProjectRoot,
    [string]$KnowledgeIndexPath = 'Documentation/AIKnowledge/KnowledgeIndex.yaml',
    [Parameter(Mandatory = $true)] [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
$utf8 = [Text.UTF8Encoding]::new($false, $true)

function Resolve-ProjectRelative([string]$Path, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($Path) -or [IO.Path]::IsPathRooted($Path)) { throw "${Label}_PATH_MUST_BE_PROJECT_RELATIVE" }
    $full = [IO.Path]::GetFullPath([IO.Path]::Combine($root, $Path))
    if (-not $full.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "${Label}_PATH_OUTSIDE_PROJECT" }
    return $full
}

function Get-Hash([byte[]]$Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

$indexFull = Resolve-ProjectRelative $KnowledgeIndexPath 'KNOWLEDGE_INDEX'
$outputFull = Resolve-ProjectRelative $OutputPath 'OUTPUT'
if (-not (Test-Path -LiteralPath $indexFull -PathType Leaf)) { throw 'KNOWLEDGE_INDEX_NOT_FOUND' }
$indexBytes = [IO.File]::ReadAllBytes($indexFull)
$indexText = $utf8.GetString($indexBytes)
$blocks = [regex]::Split($indexText, '(?m)(?=^  - knowledgeId:)')
$entries = [Collections.Generic.List[object]]::new()
foreach ($block in $blocks) {
    $id = [regex]::Match($block, '(?m)^\s{2}-\s+knowledgeId:\s*(\S+)\s*$')
    if (-not $id.Success) { continue }
    $file = [regex]::Match($block, '(?m)^\s{4}file:\s*(\S+)\s*$')
    $topic = [regex]::Match($block, '(?m)^\s{4}topic:\s*(.+?)\s*$')
    $routes = [regex]::Match($block, '(?m)^\s{4}routeKeys:\s*\[(.*?)\]\s*$')
    $routeKeys = @()
    if ($routes.Success) { $routeKeys = @($routes.Groups[1].Value -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ } | Select-Object -Unique) }
    $filePath = if ($file.Success) { $file.Groups[1].Value.Trim().Replace('\','/') } else { '' }
    $entries.Add([ordered]@{ knowledgeId=$id.Groups[1].Value.Trim(); file=$filePath; topic=if($topic.Success){$topic.Groups[1].Value.Trim()}else{''}; routeKeys=$routeKeys })
}
$byId = [ordered]@{}
$bySource = [ordered]@{}
$byRoute = [ordered]@{}
foreach ($entry in $entries) {
    $entryFull = if ($entry.file -match '(?i)^Documentation/AIKnowledge/') { Resolve-ProjectRelative $entry.file 'KNOWLEDGE_ENTRY' } else { Resolve-ProjectRelative ("Documentation/AIKnowledge/$($entry.file)") 'KNOWLEDGE_ENTRY' }
    if (Test-Path -LiteralPath $entryFull -PathType Leaf) {
        $entryText = $utf8.GetString([IO.File]::ReadAllBytes($entryFull))
        $stable = [regex]::Match($entryText, '(?im)^.*StableId.*?[`:]\s*`?([^`\s;]+)')
        if ($stable.Success) { $byId[$stable.Groups[1].Value.Trim()] = @($entry.knowledgeId) }
        foreach ($src in [regex]::Matches($entryText, '(?i)(?:Assets|Documentation|\.agents)/[^\s`;,]+')) { $bySource[$src.Value.Replace('\','/')] = @($entry.knowledgeId) }
    }
    $byId[$entry.knowledgeId] = @($entry.knowledgeId)
    foreach ($route in $entry.routeKeys) { if (-not $byRoute.Contains($route)) { $byRoute[$route] = [Collections.Generic.List[string]]::new() }; $byRoute[$route].Add($entry.knowledgeId) }
}
$index = [ordered]@{ schemaVersion=1; cacheKind='derived-knowledge-reverse-index'; generatedAtUtc=(Get-Date).ToUniversalTime().ToString('O'); sourceIndexPath=$KnowledgeIndexPath.Replace('\','/'); sourceIndexSha256=(Get-Hash $indexBytes); entryCount=$entries.Count; byStableId=$byId; bySourceRef=$bySource; byRouteKey=$byRoute; entries=$entries }
$json = $index | ConvertTo-Json -Depth 20
[IO.Directory]::CreateDirectory((Split-Path -Parent $outputFull)) | Out-Null
$tmp = "$outputFull.tmp.$PID"
[IO.File]::WriteAllText($tmp, $json, [Text.UTF8Encoding]::new($false))
Move-Item -LiteralPath $tmp -Destination $outputFull -Force
$index | ConvertTo-Json -Depth 20
