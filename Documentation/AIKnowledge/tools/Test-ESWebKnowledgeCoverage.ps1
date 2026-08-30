[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [string]$MatrixPath = 'Documentation/AIKnowledge/WebKnowledgeCoverageMatrix.yaml',
    [string]$IndexPath = 'Documentation/AIKnowledge/KnowledgeIndex.yaml',
    [string]$RegistryPath = 'Documentation/AIKnowledge/RouteProbeRegistry.json',
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$utf8 = [Text.UTF8Encoding]::new($false, $true)
$findings = [Collections.Generic.List[object]]::new()
function Add-Finding([string]$Code, [string]$Target, [string]$Message) {
    $findings.Add([pscustomobject]@{ code = $Code; target = $Target; message = $Message })
}
function Read-Strict([string]$Relative) {
    $path = Join-Path $root $Relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { Add-Finding 'MISSING_FILE' $Relative 'required file does not exist'; return '' }
    try { return $utf8.GetString([IO.File]::ReadAllBytes($path)) } catch { Add-Finding 'INVALID_UTF8' $Relative $_.Exception.Message; return '' }
}

$matrix = Read-Strict $MatrixPath
$index = Read-Strict $IndexPath
$registryText = Read-Strict $RegistryPath
$registry = $null
try { if ($registryText) { $registry = $registryText | ConvertFrom-Json } } catch { Add-Finding 'INVALID_JSON' $RegistryPath $_.Exception.Message }

$canonicalIds = [regex]::Matches($matrix, '(?m)^\s+canonicalKnowledgeId:\s*(es\.[^\s]+)') | ForEach-Object { $_.Groups[1].Value }
if ($canonicalIds.Count -eq 0) { Add-Finding 'EMPTY_MATRIX' $MatrixPath 'no canonicalKnowledgeId entries found' }
$canonicalIds = @($canonicalIds | Sort-Object -Unique)
foreach ($id in $canonicalIds) {
    $block = [regex]::Match($index, '(?ms)^  - knowledgeId:\s*' + [regex]::Escape($id) + '\s*$(.*?)(?=^  - knowledgeId:|\z)')
    if (-not $block.Success) { Add-Finding 'INDEX_BINDING_MISSING' $id 'canonical KnowledgeId is absent from KnowledgeIndex' ; continue }
    $fileMatch = [regex]::Match($block.Groups[1].Value, '(?m)^    file:\s*(\S+)')
    if (-not $fileMatch.Success) { Add-Finding 'INDEX_FILE_MISSING' $id 'index binding has no file' ; continue }
    $entryRelative = 'Documentation/AIKnowledge/' + $fileMatch.Groups[1].Value
    if (-not (Test-Path -LiteralPath (Join-Path $root $entryRelative) -PathType Leaf)) { Add-Finding 'ENTRY_FILE_MISSING' $id $entryRelative }
}

$probeIds = @()
if ($registry) { $probeIds = @($registry.probes | ForEach-Object { $_.probeId }) }
$matrixProbeIds = [regex]::Matches($matrix, '(?m)^\s+routeProbeIds:\s*\[([^\]]*)\]') | ForEach-Object { $_.Groups[1].Value -split ',' | ForEach-Object { $_.Trim(' ', '"') } } | Where-Object { $_ }
foreach ($probe in @($matrixProbeIds | Sort-Object -Unique)) {
    if ($probe -notin $probeIds) { Add-Finding 'PROBE_BINDING_MISSING' $probe 'matrix routeProbeId is absent from RouteProbeRegistry' }
}

$domains = [regex]::Matches($matrix, '(?m)^\s+- domain:\s*([^\s]+)') | ForEach-Object { $_.Groups[1].Value }
if ($domains.Count -ne $canonicalIds.Count) { Add-Finding 'DOMAIN_CANONICAL_COUNT' $MatrixPath "domains=$($domains.Count), canonicalKnowledgeIds=$($canonicalIds.Count)" }
if ($matrix -notmatch '(?m)^coverageRules:') { Add-Finding 'COVERAGE_RULES_MISSING' $MatrixPath 'coverageRules section is required' }

$status = if ($findings.Count -eq 0) { 'passed' } else { 'blocked' }
$result = [ordered]@{ validator='es-web-knowledge-coverage'; status=$status; staticStatus=if($status -eq 'passed'){'static-passed'}else{'static-blocked'}; domainCount=$domains.Count; canonicalKnowledgeCount=$canonicalIds.Count; matrixProbeCount=@($matrixProbeIds|Sort-Object -Unique).Count; findingCount=$findings.Count; findings=@($findings); runtimeStatus='runtime-not-run'; nonClaims=@('No browser, network, Unity, performance, or production claims') }
if ($Json) { $result | ConvertTo-Json -Depth 8 } else { $result | Format-List }
if ($findings.Count -gt 0) { exit 1 }
