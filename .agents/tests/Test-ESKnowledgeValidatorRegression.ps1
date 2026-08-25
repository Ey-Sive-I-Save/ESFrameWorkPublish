[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot
)

$ErrorActionPreference = 'Stop'
$utf8 = [Text.UTF8Encoding]::new($false)
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd('\', '/')
$fixtureRoot = Join-Path $root ('ES/Output/SkillValidationFixtures/es-knowledge-validator-' + [Guid]::NewGuid().ToString('N'))
$outsideFixtureRoot = $fixtureRoot + '-outside'
$rootPrefix = $root + [IO.Path]::DirectorySeparatorChar
if (-not [IO.Path]::GetFullPath($fixtureRoot).StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture path escapes ProjectRoot.' }
if (-not [IO.Path]::GetFullPath($outsideFixtureRoot).StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Outside fixture path escapes ProjectRoot.' }
$validator = Join-Path $root '.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1'

function Write-Utf8([string]$Path, [string]$Content) {
    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    [IO.File]::WriteAllText($Path, $Content, $utf8)
}

function Get-ContentHash([string[]]$Hashes) {
    $joined = ($Hashes | Sort-Object -CaseSensitive) -join ''
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($joined)))).Replace('-', '').ToLowerInvariant() } finally { $sha.Dispose() }
}

function Get-EntryBodyHash([string]$Text) {
    $normalized = $Text.Replace("`r`n", "`n").Replace("`r", "`n")
    $lines = [Collections.Generic.List[string]]::new()
    foreach ($line in $normalized.Split([char]10)) {
        if ($line -match '(?i)^\s*`EntryBodyHash`\s*[:\uFF1A]\s*`[^`]*`\s*$') { continue }
        $lines.Add($line.TrimEnd([char[]]@(' ', "`t")))
    }
    while ($lines.Count -gt 0 -and $lines[$lines.Count - 1].Length -eq 0) { $lines.RemoveAt($lines.Count - 1) }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($lines -join "`n") + "`n")))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function Seal-EntryBodyHash([string]$Text) {
    $bodyHash = Get-EntryBodyHash $Text
    return $Text -replace '(?m)^(`EntryBodyHash`\s*:\s*`)[0-9a-f]{64}(`\s*$)', ('${1}' + $bodyHash + '${2}')
}

function Invoke-Fixture([string]$Mode, [string]$EntryPath) {
    $arguments = @('-NoProfile', '-File', $validator, '-ProjectRoot', $fixtureRoot, '-Mode', $Mode)
    if ($EntryPath) { $arguments += @('-EntryPath', $EntryPath) }
    $output = & powershell @arguments 2>&1
    $exitCode = $LASTEXITCODE
    $json = ($output | Out-String).Trim() | ConvertFrom-Json
    return [pscustomobject]@{ exitCode = $exitCode; result = $json }
}

function Assert([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Write-ValidFixture {
    $sourcePath = Join-Path $fixtureRoot 'src\source.txt'
    Write-Utf8 $sourcePath "current fact`n"
    $sourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $contentHash = Get-ContentHash @($sourceHash)
    $entry = @'
# Fixture knowledge

`KnowledgeId`: `fixture.knowledge.v1`
`Authority`: `Source`
`RouteKeys`: `knowledge`, `validation`
`HashSchema`: `v2`
`SourceSetHash`: `{0}`
`ContentHash`: `{0}`
`EntryBodyHash`: `0000000000000000000000000000000000000000000000000000000000000000`
`EvidenceLevel`: `S1`
`StaleWhen`: source hash changes

## SourceRefs

- `src/source.txt` (`{1}`)

## Stop Conditions

- Stop when source or route evidence drifts.

## Evidence Boundary

- Static validation does not prove Runtime behavior.
'@ -f $contentHash, $sourceHash
    $entry = Seal-EntryBodyHash $entry
    $entryBodyHash = ([regex]::Match($entry, '(?m)^`EntryBodyHash`: `([0-9a-f]{64})`$')).Groups[1].Value
    Write-Utf8 (Join-Path $fixtureRoot 'Documentation\AIKnowledge\entries\fixture.md') $entry
    $index = @"
schemaVersion: 1
qualityGate:
  deduplication:
    sharedRouteProjectionAllowed: true
entries:
  - knowledgeId: fixture.knowledge.v1
    file: entries/fixture.md
    topic: Fixture
    routeKeys: [knowledge, validation]
    relatedSkills: [es-example]
    requiredReads:
      - src/source.txt
    authority: Source
    evidenceLevel: S1
    contentHash: $contentHash
    hashSchema: v2
    sourceSetHash: $contentHash
    entryBodyHash: $entryBodyHash
    staleWhen: source hash changes
  - knowledgeId: fixture.unrelated-incomplete.v1
    file: entries/unrelated.md
"@
    Write-Utf8 (Join-Path $fixtureRoot 'Documentation\AIKnowledge\KnowledgeIndex.yaml') $index
    Write-Utf8 (Join-Path $fixtureRoot '.agents\skills\es-example\SKILL.md') "---`nname: es-example`ndescription: Fixture.`n---`n"
    Write-Utf8 (Join-Path $fixtureRoot '.agents\skills\es-example\agents\openai.yaml') "interface:`n  display_name: `"Fixture`"`n"
}

function Convert-FixtureToLegacy {
    $entryPath = Join-Path $fixtureRoot 'Documentation\AIKnowledge\entries\fixture.md'
    $entryText = [IO.File]::ReadAllText($entryPath, $utf8)
    $entryText = $entryText -replace '(?m)^`(?:HashSchema|SourceSetHash|EntryBodyHash)`:\s*`[^`]+`\r?\n?', ''
    Write-Utf8 $entryPath $entryText
    $indexPath = Join-Path $fixtureRoot 'Documentation\AIKnowledge\KnowledgeIndex.yaml'
    $indexText = [IO.File]::ReadAllText($indexPath, $utf8)
    $indexText = $indexText -replace '(?m)^    (?:hashSchema|sourceSetHash|entryBodyHash):.*\r?\n?', ''
    Write-Utf8 $indexPath $indexText
}

function Write-SharedProjectionFixture([switch]$CompleteSecondBinding) {
    Write-ValidFixture
    $entryPath = Join-Path $fixtureRoot 'Documentation\AIKnowledge\entries\fixture.md'
    $entryText = [IO.File]::ReadAllText($entryPath, $utf8)
    $entryMode = '`EntryMode`: `SharedRouteProjection`' + "`n" + '`Authority`:'
    $entryText = $entryText -replace '(?m)^`Authority`:', $entryMode
    $routeProjectionSection = @'

## RouteProjections

- `fixture.knowledge.v1`: `knowledge`
- `fixture.knowledge.validation.v1`: `validation`
'@
    $entryText = $entryText.TrimEnd() + "`n" + $routeProjectionSection.TrimStart()
    $entryText = Seal-EntryBodyHash $entryText
    Write-Utf8 $entryPath $entryText
    $contentHash = ([regex]::Match($entryText, '(?m)^`ContentHash`: `([0-9a-f]{64})`$')).Groups[1].Value
    $entryBodyHash = ([regex]::Match($entryText, '(?m)^`EntryBodyHash`: `([0-9a-f]{64})`$')).Groups[1].Value
    $secondRead = if ($CompleteSecondBinding) { 'src/second.txt' } else { 'src/missing.txt' }
    $secondSkill = if ($CompleteSecondBinding) { 'es-second' } else { 'es-missing' }
    $index = @"
schemaVersion: 1
qualityGate:
  deduplication:
    sharedRouteProjectionAllowed: true
entries:
  - knowledgeId: fixture.knowledge.v1
    file: entries/fixture.md
    topic: Fixture canonical route
    routeKeys: [knowledge]
    relatedSkills: [es-example]
    requiredReads: [src/source.txt]
    authority: Source
    evidenceLevel: S1
    contentHash: $contentHash
    hashSchema: v2
    sourceSetHash: $contentHash
    entryBodyHash: $entryBodyHash
    staleWhen: source hash changes
  - knowledgeId: fixture.knowledge.validation.v1
    file: entries/fixture.md
    topic: Fixture validation projection
    routeKeys: [validation]
    relatedSkills: [$secondSkill]
    requiredReads: [$secondRead]
    authority: Source
    evidenceLevel: S1
    contentHash: $contentHash
    hashSchema: v2
    sourceSetHash: $contentHash
    entryBodyHash: $entryBodyHash
    staleWhen: source hash changes
"@
    Write-Utf8 (Join-Path $fixtureRoot 'Documentation\AIKnowledge\KnowledgeIndex.yaml') $index
    if ($CompleteSecondBinding) {
        Write-Utf8 (Join-Path $fixtureRoot 'src\second.txt') "second required read`n"
        Write-Utf8 (Join-Path $fixtureRoot '.agents\skills\es-second\SKILL.md') "---`nname: es-second`ndescription: Fixture.`n---`n"
        Write-Utf8 (Join-Path $fixtureRoot '.agents\skills\es-second\agents\openai.yaml') "interface:`n  display_name: `"Second`"`n"
    }
}

function Write-ReparseFixture {
    Write-ValidFixture
    New-Item -ItemType Directory -Path $outsideFixtureRoot -Force | Out-Null
    $outsideSource = Join-Path $outsideFixtureRoot 'source.txt'
    Write-Utf8 $outsideSource "external fact`n"
    $sourceHash = (Get-FileHash -LiteralPath $outsideSource -Algorithm SHA256).Hash.ToLowerInvariant()
    $contentHash = Get-ContentHash @($sourceHash)
    $junctionPath = Join-Path $fixtureRoot 'src-link'
    New-Item -ItemType Junction -Path $junctionPath -Target $outsideFixtureRoot | Out-Null

    $entryPath = Join-Path $fixtureRoot 'Documentation\AIKnowledge\entries\fixture.md'
    $entryText = [IO.File]::ReadAllText($entryPath, $utf8)
    $entryText = $entryText -replace '(?m)^`ContentHash`: `[0-9a-f]{64}`$', ('`ContentHash`: `' + $contentHash + '`')
    $entryText = $entryText -replace '(?m)^- `src/source\.txt` \(`[0-9a-f]{64}`\)$', ('- `src-link/source.txt` (`' + $sourceHash + '`)')
    Write-Utf8 $entryPath $entryText

    $indexPath = Join-Path $fixtureRoot 'Documentation\AIKnowledge\KnowledgeIndex.yaml'
    $indexText = [IO.File]::ReadAllText($indexPath, $utf8)
    $indexText = $indexText -replace 'src/source\.txt', 'src-link/source.txt'
    $indexText = $indexText -replace '(?m)^    contentHash: [0-9a-f]{64}$', ('    contentHash: ' + $contentHash)
    Write-Utf8 $indexPath $indexText
}

try {
    New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
    Write-ValidFixture
    $positive = Invoke-Fixture 'Entry' 'Documentation/AIKnowledge/entries/fixture.md'
    Assert ($positive.exitCode -eq 0 -and $positive.result.status -eq 'passed') ("positive fixture did not pass: " + ($positive | ConvertTo-Json -Depth 8 -Compress))

    $repeat = Invoke-Fixture 'Entry' 'Documentation/AIKnowledge/entries/fixture.md'
    Assert ($repeat.exitCode -eq 0 -and $repeat.result.inputHash -eq $positive.result.inputHash) 'repeat validation is not deterministic'
    Assert (($repeat.result.findings | ConvertTo-Json -Compress) -eq ($positive.result.findings | ConvertTo-Json -Compress)) 'repeat findings changed'

    Convert-FixtureToLegacy
    $legacy = Invoke-Fixture 'Entry' 'Documentation/AIKnowledge/entries/fixture.md'
    Assert ($legacy.exitCode -eq 0 -and $legacy.result.status -eq 'passed') 'legacy compatibility fixture did not pass'

    Write-ValidFixture
    $entryPath = Join-Path $fixtureRoot 'Documentation\AIKnowledge\entries\fixture.md'
    $entryText = [IO.File]::ReadAllText($entryPath, $utf8)
    Write-Utf8 $entryPath ($entryText + "`nUnsupported body-only claim.`n")
    $bodyChanged = Invoke-Fixture 'Entry' 'Documentation/AIKnowledge/entries/fixture.md'
    Assert ($bodyChanged.exitCode -eq 1 -and @($bodyChanged.result.findings.code) -contains 'ENTRY_BODY_HASH_MISMATCH') 'body-only change was not blocked'

    Write-ValidFixture
    $entryText = [IO.File]::ReadAllText($entryPath, $utf8)
    $entryText = $entryText -replace '(?ms)^## Stop Conditions\s*\r?\n.*?(?=^## Evidence Boundary)', ''
    Write-Utf8 $entryPath $entryText
    $stopRemoved = Invoke-Fixture 'Entry' 'Documentation/AIKnowledge/entries/fixture.md'
    Assert ($stopRemoved.exitCode -eq 1 -and @($stopRemoved.result.findings.code) -contains 'ENTRY_BODY_HASH_MISMATCH') 'removing stop conditions was not blocked'

    Write-ValidFixture
    $entryText = [IO.File]::ReadAllText($entryPath, $utf8)
    $entryText = $entryText -replace '(?ms)^## Evidence Boundary\s*\r?\n.*\z', ''
    Write-Utf8 $entryPath $entryText
    $evidenceRemoved = Invoke-Fixture 'Entry' 'Documentation/AIKnowledge/entries/fixture.md'
    Assert ($evidenceRemoved.exitCode -eq 1 -and @($evidenceRemoved.result.findings.code) -contains 'ENTRY_BODY_HASH_MISMATCH') 'removing the evidence boundary was not blocked'

    Write-ValidFixture
    $indexPath = Join-Path $fixtureRoot 'Documentation\AIKnowledge\KnowledgeIndex.yaml'
    $indexText = [IO.File]::ReadAllText($indexPath, $utf8)
    $indexText = $indexText -replace '(?m)^    entryBodyHash: [0-9a-f]{64}$', ('    entryBodyHash: ' + ('f' * 64))
    Write-Utf8 $indexPath $indexText
    $bodyProjectionDrift = Invoke-Fixture 'Entry' 'Documentation/AIKnowledge/entries/fixture.md'
    Assert ($bodyProjectionDrift.exitCode -eq 1 -and @($bodyProjectionDrift.result.findings.code) -contains 'ENTRY_BODY_HASH_INDEX_MISMATCH') 'index EntryBodyHash drift was not blocked'

    Write-ValidFixture
    $sourcePath = Join-Path $fixtureRoot 'src\source.txt'
    Write-Utf8 $sourcePath "new current fact`n"
    $newSourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $entryText = [IO.File]::ReadAllText($entryPath, $utf8)
    $entryText = $entryText -replace '(?m)^- `src/source\.txt` \(`[0-9a-f]{64}`\)$', ('- `src/source.txt` (`' + $newSourceHash + '`)')
    Write-Utf8 $entryPath $entryText
    $sourceSetChanged = Invoke-Fixture 'Entry' 'Documentation/AIKnowledge/entries/fixture.md'
    Assert ($sourceSetChanged.exitCode -eq 1 -and @($sourceSetChanged.result.findings.code) -contains 'SOURCE_SET_HASH_MISMATCH') 'SourceRef change without SourceSetHash refresh was not blocked'

    Write-ValidFixture
    $entryText = [IO.File]::ReadAllText($entryPath, $utf8)
    $entryText = $entryText -replace '(?m)^`RouteKeys`: `knowledge`, `validation`$', '`RouteKeys`: `knowledge`'
    $entryText = Seal-EntryBodyHash $entryText
    Write-Utf8 $entryPath $entryText
    $entryRouteMissing = Invoke-Fixture 'Entry' 'Documentation/AIKnowledge/entries/fixture.md'
    $entryRouteFinding = @($entryRouteMissing.result.findings | Where-Object code -eq 'ROUTE_SET_MISMATCH')[0]
    Assert ($entryRouteMissing.exitCode -eq 1 -and @($entryRouteFinding.missingFromEntry) -contains 'validation' -and @($entryRouteFinding.missingFromIndex).Count -eq 0) 'Entry-side missing routeKey was not reported precisely'

    Write-ValidFixture
    $indexText = [IO.File]::ReadAllText($indexPath, $utf8)
    $indexText = $indexText -replace '(?m)^    routeKeys: \[knowledge, validation\]$', '    routeKeys: [knowledge]'
    Write-Utf8 $indexPath $indexText
    $indexRouteMissing = Invoke-Fixture 'Entry' 'Documentation/AIKnowledge/entries/fixture.md'
    $indexRouteFinding = @($indexRouteMissing.result.findings | Where-Object code -eq 'ROUTE_SET_MISMATCH')[0]
    Assert ($indexRouteMissing.exitCode -eq 1 -and @($indexRouteFinding.missingFromIndex) -contains 'validation' -and @($indexRouteFinding.missingFromEntry).Count -eq 0) 'Index-side missing routeKey was not reported precisely'
    $indexModeRouteMissing = Invoke-Fixture 'Index' $null
    Assert ($indexModeRouteMissing.exitCode -eq 1 -and @($indexModeRouteMissing.result.findings.code) -contains 'ROUTE_SET_MISMATCH') 'Index mode did not enforce exact canonical route sets'

    Write-ValidFixture
    $indexText = [IO.File]::ReadAllText($indexPath, $utf8)
    $indexText = $indexText -replace '(?m)^    routeKeys: \[knowledge, validation\]$', '    routeKeys: [validation, knowledge]'
    Write-Utf8 $indexPath $indexText
    $routeOrderOnly = Invoke-Fixture 'Entry' 'Documentation/AIKnowledge/entries/fixture.md'
    Assert ($routeOrderOnly.exitCode -eq 0 -and $routeOrderOnly.result.status -eq 'passed') 'routeKey order-only difference did not pass'

    Write-ValidFixture
    $indexText = [IO.File]::ReadAllText($indexPath, $utf8)
    $canonicalBlock = [regex]::Match($indexText, '(?ms)^  - knowledgeId: fixture\.knowledge\.v1.*?(?=^  - knowledgeId:|\z)').Value
    $extraBinding = $canonicalBlock -replace 'fixture\.knowledge\.v1', 'fixture.knowledge.extra.v1'
    Write-Utf8 $indexPath ($indexText.TrimEnd() + "`n" + $extraBinding)
    $canonicalExtra = Invoke-Fixture 'Entry' 'Documentation/AIKnowledge/entries/fixture.md'
    Assert ($canonicalExtra.exitCode -eq 1 -and @($canonicalExtra.result.findings.code) -contains 'INDEX_BINDING_COUNT') 'extra canonical file binding was not blocked'

    Write-SharedProjectionFixture
    $sharedIncomplete = Invoke-Fixture 'Entry' 'Documentation/AIKnowledge/entries/fixture.md'
    Assert ($sharedIncomplete.exitCode -eq 1 -and @($sharedIncomplete.result.findings.code) -contains 'PATH_FILE_MISSING') 'shared projection did not validate the second binding requiredReads'
    Assert (@($sharedIncomplete.result.findings.code) -contains 'RELATED_SKILL_INCOMPLETE') 'shared projection did not validate the second binding relatedSkills'

    Write-SharedProjectionFixture -CompleteSecondBinding
    $sharedComplete = Invoke-Fixture 'Entry' 'Documentation/AIKnowledge/entries/fixture.md'
    Assert ($sharedComplete.exitCode -eq 0 -and $sharedComplete.result.status -eq 'passed') 'shared projection did not recover after completing every binding'

    $indexPath = Join-Path $fixtureRoot 'Documentation\AIKnowledge\KnowledgeIndex.yaml'
    $indexText = [IO.File]::ReadAllText($indexPath, $utf8)
    $indexText = $indexText -replace '(?m)^    routeKeys: \[validation\]$', '    routeKeys: [validation, undeclared]'
    Write-Utf8 $indexPath $indexText
    $sharedRouteMismatch = Invoke-Fixture 'Entry' 'Documentation/AIKnowledge/entries/fixture.md'
    Assert ($sharedRouteMismatch.exitCode -eq 1 -and @($sharedRouteMismatch.result.findings.code) -contains 'ROUTE_SET_MISMATCH') 'undeclared shared projection route difference was not blocked'

    Write-SharedProjectionFixture -CompleteSecondBinding
    $entryText = [IO.File]::ReadAllText($entryPath, $utf8)
    $entryText = $entryText -replace '(?ms)^## RouteProjections\s*\r?\n.*\z', ''
    $entryText = Seal-EntryBodyHash $entryText
    $entryBodyHash = ([regex]::Match($entryText, '(?m)^`EntryBodyHash`: `([0-9a-f]{64})`$')).Groups[1].Value
    Write-Utf8 $entryPath $entryText
    $indexText = [IO.File]::ReadAllText($indexPath, $utf8)
    $indexText = $indexText -replace '(?m)^    entryBodyHash: [0-9a-f]{64}$', ('    entryBodyHash: ' + $entryBodyHash)
    Write-Utf8 $indexPath $indexText
    $sharedUndeclared = Invoke-Fixture 'Entry' 'Documentation/AIKnowledge/entries/fixture.md'
    Assert ($sharedUndeclared.exitCode -eq 1 -and @($sharedUndeclared.result.findings.code) -contains 'ROUTE_PROJECTION_DECLARATION_MISSING') 'SharedRouteProjection without explicit declarations was not blocked'

    Write-SharedProjectionFixture -CompleteSecondBinding

    $indexText = [IO.File]::ReadAllText($indexPath, $utf8)
    $duplicateProjection = @'
  - knowledgeId: fixture.knowledge.validation.v1
    file: entries/unrelated.md
    topic: Duplicate projection identity
    routeKeys: [validation]
    relatedSkills: [es-second]
    requiredReads: [src/second.txt]
    authority: Source
    evidenceLevel: S1
    contentHash: 0000000000000000000000000000000000000000000000000000000000000000
    staleWhen: fixture only
'@
    Write-Utf8 $indexPath ($indexText.TrimEnd() + "`n" + $duplicateProjection)
    $sharedDuplicate = Invoke-Fixture 'Entry' 'Documentation/AIKnowledge/entries/fixture.md'
    Assert ($sharedDuplicate.exitCode -eq 1 -and @($sharedDuplicate.result.findings.code) -contains 'INDEX_DUPLICATE_ID') 'shared projection binding identity duplication was not blocked'

    Write-ReparseFixture
    $reparse = Invoke-Fixture 'Entry' 'Documentation/AIKnowledge/entries/fixture.md'
    Assert ($reparse.exitCode -eq 1 -and @($reparse.result.findings.code) -contains 'PATH_REPARSE_DENIED') 'reparse-point SourceRef expansion was not denied'
    $junctionPath = Join-Path $fixtureRoot 'src-link'
    if (Test-Path -LiteralPath $junctionPath) { [IO.Directory]::Delete($junctionPath) }
    Write-ValidFixture

    $entryPath = Join-Path $fixtureRoot 'Documentation\AIKnowledge\entries\fixture.md'
    $invalidReplacement = '`ContentHash`: `' + ('0' * 64) + '`'
    $invalidEntry = ([IO.File]::ReadAllText($entryPath, $utf8) -replace '(?m)^`ContentHash`: `[0-9a-f]{64}`$', $invalidReplacement)
    Write-Utf8 $entryPath $invalidEntry
    $invalid = Invoke-Fixture 'Entry' 'Documentation/AIKnowledge/entries/fixture.md'
    Assert ($invalid.exitCode -eq 1 -and @($invalid.result.findings.code) -contains 'CONTENT_HASH_MISMATCH') 'content hash mismatch was not blocked'

    Write-ValidFixture
    Write-Utf8 (Join-Path $fixtureRoot 'src\source.txt') "changed fact`n"
    $drift = Invoke-Fixture 'Entry' 'Documentation/AIKnowledge/entries/fixture.md'
    Assert ($drift.exitCode -eq 1 -and @($drift.result.findings.code) -contains 'SOURCE_HASH_DRIFT') 'SourceRef drift was not blocked'

    Write-ValidFixture
    $indexPath = Join-Path $fixtureRoot 'Documentation\AIKnowledge\KnowledgeIndex.yaml'
    $indexText = [IO.File]::ReadAllText($indexPath, $utf8)
    $block = [regex]::Match($indexText, '(?ms)^\s{2}-\s+knowledgeId:.*$').Value
    Write-Utf8 $indexPath ($indexText.TrimEnd() + "`n" + $block)
    $duplicate = Invoke-Fixture 'Index' $null
    Assert ($duplicate.exitCode -eq 1 -and @($duplicate.result.findings.code) -contains 'INDEX_DUPLICATE_ID') 'duplicate KnowledgeId was not blocked'

    Write-ValidFixture
    $denied = Invoke-Fixture 'Entry' '../outside.md'
    Assert ($denied.exitCode -eq 1 -and @($denied.result.findings.code) -contains 'PATH_EXPANSION_DENIED') 'path expansion was not denied'

    $sourcePath = Join-Path $fixtureRoot 'src\source.txt'
    Remove-Item -LiteralPath $sourcePath -Force
    $interrupted = Invoke-Fixture 'Entry' 'Documentation/AIKnowledge/entries/fixture.md'
    Assert ($interrupted.exitCode -eq 1 -and @($interrupted.result.findings.code) -contains 'PATH_FILE_MISSING') 'missing interrupted input was not blocked'
    Write-ValidFixture
    $recovered = Invoke-Fixture 'Entry' 'Documentation/AIKnowledge/entries/fixture.md'
    Assert ($recovered.exitCode -eq 0 -and $recovered.result.status -eq 'passed') 'interruption recovery did not pass after restoring inputs'

    Write-Output 'PASS: es-knowledge-validator regression cases passed (legacy/v2, body/source hashes, exact routes, shared projection, invalid, drift, duplicate, denial, reparse, repeat, recovery).'
} finally {
    $resolvedFixture = [IO.Path]::GetFullPath($fixtureRoot)
    $junctionPath = Join-Path $resolvedFixture 'src-link'
    if (Test-Path -LiteralPath $junctionPath) { [IO.Directory]::Delete($junctionPath) }
    if ($resolvedFixture.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $resolvedFixture)) {
        Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
    }
    $resolvedOutside = [IO.Path]::GetFullPath($outsideFixtureRoot)
    if ($resolvedOutside.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $resolvedOutside)) {
        Remove-Item -LiteralPath $resolvedOutside -Recurse -Force
    }
}
