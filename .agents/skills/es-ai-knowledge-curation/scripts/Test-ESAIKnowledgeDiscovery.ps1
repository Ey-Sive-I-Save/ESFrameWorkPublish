[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot
)

$ErrorActionPreference = 'Stop'
$utf8 = [Text.UTF8Encoding]::new($false, $true)
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$entryRelative = 'Documentation/AIKnowledge/AIBRAIN_ENTRY.md'
$indexRelative = 'Documentation/AIKnowledge/KnowledgeIndex.yaml'

$requiredPointers = @(
    'AGENTS.md',
    '.agents/README.md',
    '.agents/SKILL_RESOURCE_INDEX.yaml',
    'Assets/Plugins/ES/AICommands/README.md'
)

function Read-StrictUtf8([string]$relativePath) {
    $path = Join-Path $root $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required AI discovery file is missing: $relativePath"
    }
    return $utf8.GetString([IO.File]::ReadAllBytes($path))
}

$entry = Read-StrictUtf8 $entryRelative
$index = Read-StrictUtf8 $indexRelative

if ($entry -notmatch '## AI') {
    throw 'AIBRAIN_ENTRY.md does not declare the minimum AI startup protocol.'
}
if ($entry -notmatch [Regex]::Escape('KnowledgeIndex.yaml')) {
    throw 'AIBRAIN_ENTRY.md does not route to KnowledgeIndex.yaml.'
}
if ($index -notmatch '(?m)^entries:\s*$') {
    throw 'KnowledgeIndex.yaml does not expose an entries collection.'
}

foreach ($relativePath in $requiredPointers) {
    $content = Read-StrictUtf8 $relativePath
    if ($relativePath -eq 'AGENTS.md' -and ($content -notmatch 'AIBRAIN_ENTRY\.md' -or $content -notmatch 'KnowledgeIndex\.yaml')) {
        throw 'AGENTS.md does not enforce AIKnowledge discovery.'
    }
    if ($content -notmatch [Regex]::Escape($entryRelative)) {
        throw "AI entrypoint pointer is missing from: $relativePath"
    }
}

$startReadmes = Get-ChildItem -LiteralPath (Join-Path $root 'Assets/Plugins/ES/AIWarnings') -Recurse -File -Filter 'README.md' |
    Where-Object { $_.Directory.Name -match 'Start' }
if (-not $startReadmes) {
    throw 'AIWarnings Start README is missing.'
}
$startContent = $utf8.GetString([IO.File]::ReadAllBytes($startReadmes[0].FullName))
if ($startContent -notmatch [Regex]::Escape($entryRelative)) {
    throw 'AIWarnings Start README does not point to AIBRAIN_ENTRY.md.'
}

$knowledgeIds = [Regex]::Matches($index, '(?m)^\s*- knowledgeId:\s*(\S+)\s*$') |
    ForEach-Object { $_.Groups[1].Value }
if ($knowledgeIds.Count -eq 0) {
    throw 'KnowledgeIndex.yaml contains no knowledgeId entries.'
}
$duplicates = $knowledgeIds | Group-Object | Where-Object Count -gt 1
if ($duplicates) {
    throw ('Duplicate knowledgeId values: ' + (($duplicates.Name | Sort-Object) -join ', '))
}

$indexedFiles = [Regex]::Matches($index, '(?m)^\s*file:\s*(\S+)\s*$') |
    ForEach-Object { $_.Groups[1].Value }
foreach ($relativePath in $indexedFiles) {
    [void](Read-StrictUtf8 (Join-Path 'Documentation/AIKnowledge' $relativePath))
}

Write-Output ("PASS: AIKnowledge discovery is enforced across {0} entry pointers; {1} unique Knowledge entries resolve." -f ($requiredPointers.Count + 1), $knowledgeIds.Count)
