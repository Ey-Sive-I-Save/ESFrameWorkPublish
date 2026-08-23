[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [Parameter(Mandatory = $true)][string]$EntryPath
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$entry = (Resolve-Path -LiteralPath (Join-Path $root $EntryPath)).Path
$utf8 = [Text.UTF8Encoding]::new($false, $true)
$text = $utf8.GetString([IO.File]::ReadAllBytes($entry))

foreach ($field in @('KnowledgeId','Authority','RouteKeys','ContentHash','SourceRefs','EvidenceLevel','StaleWhen')) {
    $fieldPattern = [Regex]::Escape('`' + $field + '`')
    if ($field -eq 'SourceRefs') { $fieldPattern = '(?m)^##\s+SourceRefs\s*$|' + $fieldPattern }
    if ($text -notmatch $fieldPattern) {
        throw "Missing knowledge field: $field"
    }
}
if ($text -match '\b(PENDING|TODO)\b') { throw 'Placeholder remains in knowledge entry.' }
if ($text -notmatch '`EvidenceLevel`: `S[0-6]`') { throw 'Invalid EvidenceLevel.' }

$sourcePattern = '(?m)^- `(.+?)` \(`([0-9a-f]{64})`\)\r?$'
$refs = [Regex]::Matches($text, $sourcePattern)
if ($refs.Count -eq 0) { throw 'Knowledge entry has no SourceRefs.' }
$hashes = New-Object System.Collections.Generic.List[string]
foreach ($match in $refs) {
    $relative = $match.Groups[1].Value
    $declared = $match.Groups[2].Value
    $source = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "SourceRef missing: $relative" }
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $source).Hash.ToLowerInvariant()
    if ($actual -ne $declared) { throw "SourceRef hash drift: $relative" }
    [void]$hashes.Add($declared)
}
$joined = ($hashes | Sort-Object) -join ''
$sha = [Security.Cryptography.SHA256]::Create()
$expected = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($joined)))).Replace('-','').ToLowerInvariant()
$declaredContent = [Regex]::Match($text, '(?m)^`ContentHash`: `([0-9a-f]{64})`$').Groups[1].Value
if ($declaredContent -ne $expected) { throw "ContentHash mismatch: declared=$declaredContent expected=$expected" }

Write-Output "PASS: knowledge entry contract and SourceRef hashes: $EntryPath"
