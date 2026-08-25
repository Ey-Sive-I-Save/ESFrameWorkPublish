[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$ProjectRoot,
    [string]$OutputPath = 'ES/Output/KnowledgeValidation/refresh-plan.json',
    [ValidateRange(0, 1000)] [int]$SampleDelayMilliseconds = 10
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$entryRoot = Join-Path $root 'Documentation/AIKnowledge'
$indexPath = Join-Path $entryRoot 'KnowledgeIndex.yaml'
$strictUtf8 = [Text.UTF8Encoding]::new($false, $true)
$refreshAlgorithmVersion = 'es-knowledge-stable-refresh-v2-source-normalized'
$colonPattern = '[:' + [char]0xFF1A + ']'
$sourceSectionPattern = '(?ms)(?:^##\s+SourceRefs\s*$|^`SourceRefs`\s*' + $colonPattern + '\s*$|^SourceRefs\s*' + $colonPattern + '\s*$)\r?\n(?<body>.*?)(?=^##\s+|^`?[A-Za-z][A-Za-z0-9]*`?\s*' + $colonPattern + '\s*|\z)'
$sourceSectionHeaderPattern = '(?m)^(?:##\s+SourceRefs\s*$|`SourceRefs`\s*' + $colonPattern + '\s*$|SourceRefs\s*' + $colonPattern + '\s*$)'
$sourceBulletPattern = '^- `(?<path>[^`\r\n]+)` \(`(?<hash>[0-9a-f]{64})`\)$'

function Get-Relative([string]$Path) {
    $Path.Substring($root.Length).TrimStart('\', '/').Replace('\', '/')
}

function Get-Sha256([string]$Text) {
    Get-Sha256Bytes ([Text.Encoding]::UTF8.GetBytes($Text))
}

function Get-Sha256Bytes([byte[]]$Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Get-SourceRefHash([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $extension = [IO.Path]::GetExtension($Path).ToLowerInvariant()
    $textExtensions = @('.cs','.csproj','.md','.json','.yaml','.yml','.ps1','.py','.txt','.asmdef','.asset','.meta')
    if ($extension -in $textExtensions) {
        $text = $strictUtf8.GetString($bytes)
        $text = $text -replace "`r`n", "`n"
        $text = $text -replace "`r", "`n"
        return Get-Sha256Bytes ([Text.Encoding]::UTF8.GetBytes($text))
    }
    return Get-Sha256Bytes $bytes
}

function Join-Ordinal([string[]]$Values, [string]$Separator) {
    $copy = [string[]]@($Values)
    [Array]::Sort($copy, [StringComparer]::Ordinal)
    $copy -join $Separator
}

function Format-CanonicalField($Value) {
    if ($null -eq $Value) {
        $text = ''
    }
    elseif ($Value -is [bool]) {
        $text = if ([bool]$Value) { 'true' } else { 'false' }
    }
    else {
        $text = [string]$Value
    }
    "$($text.Length):$text"
}

function New-CanonicalRecord([string]$Kind, [object[]]$Values) {
    $fields = [Collections.Generic.List[string]]::new()
    $fields.Add((Format-CanonicalField $Kind))
    foreach ($value in @($Values)) {
        $fields.Add((Format-CanonicalField $value))
    }
    $fields -join '|'
}

function Get-SourceSetHash([object[]]$SourceRefs) {
    $records = @($SourceRefs | ForEach-Object {
        New-CanonicalRecord -Kind 'source-set' -Values @($_.path, $_.declaredHash)
    })
    Get-Sha256 (Join-Ordinal $records "`n")
}

function Get-ContentHash([string[]]$Hashes) {
    Get-Sha256 (Join-Ordinal $Hashes '')
}

function Get-EntryBodyHash([string]$Text) {
    $normalized = $Text.Replace("`r`n", "`n").Replace("`r", "`n")
    $lines = [Collections.Generic.List[string]]::new()
    foreach ($line in $normalized.Split([char]10)) {
        if ($line -match '(?i)^\s*`EntryBodyHash`\s*[:\uFF1A]\s*`[^`]*`\s*$') { continue }
        $lines.Add($line.TrimEnd([char[]]@(' ', "`t")))
    }
    while ($lines.Count -gt 0 -and $lines[$lines.Count - 1].Length -eq 0) { $lines.RemoveAt($lines.Count - 1) }
    Get-Sha256 (($lines -join "`n") + "`n")
}

function Replace-First([string]$Text, [string]$Pattern, [string]$Replacement) {
    $match = ([regex]::new($Pattern)).Match($Text)
    if (-not $match.Success) { return $Text }
    $Text.Substring(0, $match.Index) + $match.Result($Replacement) + $Text.Substring($match.Index + $match.Length)
}

function Get-PlanHash([object[]]$EntrySnapshots, [object[]]$Findings, [string]$IndexHash) {
    $records = [Collections.Generic.List[string]]::new()
    $records.Add((New-CanonicalRecord -Kind 'plan' -Values @(3, 'es-knowledge-validator.refresh-plan', $refreshAlgorithmVersion, $IndexHash)))
    foreach ($entry in @($EntrySnapshots)) {
        $records.Add((New-CanonicalRecord -Kind 'entry' -Values @(
            $entry.entry, $entry.knowledgeId, $entry.entryMode, $entry.hashSchema,
            $entry.entryHash, $entry.status, $entry.sourceSetHash, $entry.sourceRefCount,
            $entry.indexBindingCount, $entry.declaredContentHash,
            $entry.declaredSourceSetHash, $entry.declaredEntryBodyHash,
            $entry.expectedContentHash, $entry.expectedSourceSetHash, $entry.expectedEntryBodyHash
        )))
        foreach ($bindingId in @($entry.indexBindingIds)) {
            $records.Add((New-CanonicalRecord -Kind 'index-binding' -Values @($entry.entry, $bindingId)))
        }
        foreach ($binding in @($entry.indexBindings)) {
            $records.Add((New-CanonicalRecord -Kind 'index-binding-projection' -Values @(
                $entry.entry, $binding.id, $binding.hashSchema, $binding.contentHash,
                $binding.sourceSetHash, $binding.entryBodyHash, $binding.expectedContentHash,
                $binding.expectedSourceSetHash, $binding.expectedEntryBodyHash
            )))
        }
        foreach ($source in @($entry.sourceRefs)) {
            $records.Add((New-CanonicalRecord -Kind 'source' -Values @(
                $entry.entry, $source.path, $source.declaredHash, $source.currentHash,
                $source.firstSampleHash, $source.snapshotStable
            )))
        }
    }
    foreach ($finding in @($Findings)) {
        $records.Add((New-CanonicalRecord -Kind 'finding' -Values @(
            $finding.code, $finding.entry, $finding.entryHash, $finding.source,
            $finding.declaredHash, $finding.currentHash, $finding.firstSampleHash,
            $finding.snapshotStable, $finding.declaredContentHash, $finding.action,
            $finding.reason
        )))
    }
    Get-Sha256 (Join-Ordinal $records.ToArray() "`n")
}

function Get-DeclaredContentHash([string]$Text) {
    $match = [regex]::Match($Text, '(?m)^.*ContentHash.*?([0-9a-f]{64}).*$')
    if ($match.Success) { return $match.Groups[1].Value }
    ''
}

function Get-StableFileHash([string]$Path, [int]$DelayMilliseconds) {
    $first = Get-SourceRefHash $Path
    if ($DelayMilliseconds -gt 0) { Start-Sleep -Milliseconds $DelayMilliseconds }
    $second = Get-SourceRefHash $Path
    [pscustomobject]@{ first = $first; second = $second; stable = ($first -ceq $second) }
}

function Get-StrictTextSnapshot([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $hash = Get-Sha256Bytes $bytes
    $text = $strictUtf8.GetString($bytes)
    $postReadHash = if (Test-Path -LiteralPath $Path -PathType Leaf) {
        (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    else {
        ''
    }
    [pscustomobject]@{
        text = $text
        hash = $hash
        stable = ($hash -ceq $postReadHash)
        postReadHash = $postReadHash
    }
}

function Get-MetadataValues([string]$Text, [string]$Name) {
    $pattern = '(?m)^`?' + [regex]::Escape($Name) + '`?[ \t]*' + $colonPattern + '[ \t]*`?(?<value>[A-Za-z0-9._-]+)`?[ \t]*\r?$'
    @([regex]::Matches($Text, $pattern) | ForEach-Object { $_.Groups['value'].Value })
}

function Test-OrdinalSetEqual([object[]]$Left, [object[]]$Right) {
    $leftValues = @($Left | ForEach-Object { [string]$_ } | Sort-Object -CaseSensitive -Unique)
    $rightValues = @($Right | ForEach-Object { [string]$_ } | Sort-Object -CaseSensitive -Unique)
    if ($leftValues.Count -ne $rightValues.Count) { return $false }
    for ($i = 0; $i -lt $leftValues.Count; $i++) {
        if ($leftValues[$i] -cne $rightValues[$i]) { return $false }
    }
    $true
}

function Get-IndexGlobalIdentityIssues([string]$IndexText) {
    $issues = [Collections.Generic.List[string]]::new()
    $seen = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    $blockPattern = '(?ms)^\s{2}-\s+knowledgeId:.*?(?=^\s{2}-\s+knowledgeId:|\z)'
    foreach ($block in @([regex]::Matches($IndexText, $blockPattern))) {
        $idMatches = @([regex]::Matches($block.Value, '(?m)^[ ]{2}-[ \t]+knowledgeId:[ \t]*(?<id>[a-z0-9._-]+)[ \t]*\r?$'))
        $fileMatches = @([regex]::Matches($block.Value, '(?m)^[ ]{4}file:[ \t]*(?<file>[^\r\n]+?)[ \t]*\r?$'))
        if ($idMatches.Count -ne 1 -or $fileMatches.Count -ne 1) {
            $issues.Add("KnowledgeIndex block must contain exactly one normalized knowledgeId and file; found id=$($idMatches.Count), file=$($fileMatches.Count).")
            continue
        }
        $id = $idMatches[0].Groups['id'].Value
        $file = $fileMatches[0].Groups['file'].Value
        if ($seen.ContainsKey($id)) {
            $issues.Add("KnowledgeIndex has duplicate knowledgeId ${id}: $($seen[$id]) and $file")
        }
        else {
            $seen.Add($id, $file)
        }
    }
    @($issues)
}

function Get-IndexBindingIds([string]$IndexText, [string]$KnowledgeRelative) {
    $ids = [Collections.Generic.List[string]]::new()
    $bindings = [Collections.Generic.List[object]]::new()
    $issues = [Collections.Generic.List[string]]::new()
    $blockPattern = '(?ms)^\s{2}-\s+knowledgeId:.*?(?=^\s{2}-\s+knowledgeId:|\z)'
    $filePattern = '(?m)^\s{4}file:\s*' + [regex]::Escape($KnowledgeRelative) + '\s*$'
    $idPattern = '(?m)^\s{2}-\s+knowledgeId:\s*(?<id>[a-z0-9._-]+)\s*$'
    $seen = [Collections.Generic.Dictionary[string, bool]]::new([StringComparer]::Ordinal)
    foreach ($block in @([regex]::Matches($IndexText, $blockPattern))) {
        $fileMatches = @([regex]::Matches($block.Value, $filePattern))
        if ($fileMatches.Count -eq 0) { continue }
        if ($fileMatches.Count -ne 1) {
            $issues.Add("KnowledgeIndex block has duplicate file fields: $KnowledgeRelative")
            continue
        }
        $idMatches = @([regex]::Matches($block.Value, $idPattern))
        if ($idMatches.Count -ne 1) {
            $issues.Add("KnowledgeIndex block must have exactly one knowledgeId: $KnowledgeRelative")
            continue
        }
        $id = $idMatches[0].Groups['id'].Value
        if ($seen.ContainsKey($id)) {
            $issues.Add("KnowledgeIndex has a duplicate binding identity for ${KnowledgeRelative}: $id")
            continue
        }
        $seen.Add($id, $true)
        $ids.Add($id)
        $hashSchemaMatches = @([regex]::Matches($block.Value, '(?m)^[ ]{4}hashSchema:[ \t]*(?<schema>[A-Za-z0-9._-]+)[ \t]*\r?$'))
        $contentHashMatches = @([regex]::Matches($block.Value, '(?m)^[ ]{4}contentHash:[ \t]*(?<hash>[^\r\n]*?)[ \t]*\r?$'))
        $sourceSetHashMatches = @([regex]::Matches($block.Value, '(?m)^[ ]{4}sourceSetHash:[ \t]*(?<hash>[^\r\n]*?)[ \t]*\r?$'))
        $entryBodyHashMatches = @([regex]::Matches($block.Value, '(?m)^[ ]{4}entryBodyHash:[ \t]*(?<hash>[^\r\n]*?)[ \t]*\r?$'))
        $hasHashSchema = [regex]::IsMatch($block.Value, '(?m)^[ ]{4}hashSchema:[ \t]*')
        $hasSourceSetHash = $sourceSetHashMatches.Count -gt 0
        $hasEntryBodyHash = $entryBodyHashMatches.Count -gt 0
        if ($hashSchemaMatches.Count -gt 1) {
            $issues.Add("KnowledgeIndex binding has duplicate hashSchema fields: $id")
        }
        elseif ($hashSchemaMatches.Count -eq 0 -and $hasHashSchema) {
            $issues.Add("KnowledgeIndex binding has a malformed hashSchema field: $id")
        }
        if ($contentHashMatches.Count -ne 1) {
            $issues.Add("KnowledgeIndex binding must contain exactly one contentHash: $id")
        }
        $contentHash = if ($contentHashMatches.Count -eq 1) { $contentHashMatches[0].Groups['hash'].Value.Trim() } else { '' }
        if ($contentHash -cnotmatch '^[0-9a-f]{64}$') {
            $issues.Add("KnowledgeIndex binding contentHash must be lowercase SHA-256: $id")
        }
        $hashSchema = if ($hashSchemaMatches.Count -eq 0 -and -not $hasHashSchema) { 'legacy' } elseif ($hashSchemaMatches.Count -eq 1) { $hashSchemaMatches[0].Groups['schema'].Value } else { '' }
        if ($hashSchema -notin @('legacy', 'v2')) {
            $issues.Add("KnowledgeIndex binding has an unsupported hashSchema: ${id}: $hashSchema")
        }
        $sourceSetHash = if ($sourceSetHashMatches.Count -eq 1) { $sourceSetHashMatches[0].Groups['hash'].Value.Trim() } else { '' }
        $entryBodyHash = if ($entryBodyHashMatches.Count -eq 1) { $entryBodyHashMatches[0].Groups['hash'].Value.Trim() } else { '' }
        if ($hashSchema -eq 'v2') {
            if ($sourceSetHashMatches.Count -ne 1) { $issues.Add("KnowledgeIndex v2 binding must contain exactly one sourceSetHash: $id") }
            if ($entryBodyHashMatches.Count -ne 1) { $issues.Add("KnowledgeIndex v2 binding must contain exactly one entryBodyHash: $id") }
            if ($sourceSetHash -cnotmatch '^[0-9a-f]{64}$') { $issues.Add("KnowledgeIndex sourceSetHash must be lowercase SHA-256: $id") }
            if ($entryBodyHash -cnotmatch '^[0-9a-f]{64}$') { $issues.Add("KnowledgeIndex entryBodyHash must be lowercase SHA-256: $id") }
        }
        elseif ($hasSourceSetHash -or $hasEntryBodyHash) {
            $issues.Add("KnowledgeIndex binding has partial v2 fields without hashSchema v2: $id")
        }
        $bindings.Add([pscustomobject]@{
            id = $id
            hashSchema = $hashSchema
            contentHash = $contentHash
            sourceSetHash = $sourceSetHash
            entryBodyHash = $entryBodyHash
        })
    }
    [pscustomobject]@{
        ids = @($ids | Sort-Object -CaseSensitive)
        bindings = @($bindings | Sort-Object id)
        issues = @($issues)
    }
}

function Get-RouteProjectionIds([string]$Text) {
    $ids = [Collections.Generic.List[string]]::new()
    $issues = [Collections.Generic.List[string]]::new()
    $sections = @([regex]::Matches($Text, '(?ms)^##\s+RouteProjections\s*$\r?\n(?<body>.*?)(?=^##\s+|\z)'))
    if ($sections.Count -ne 1) {
        $issues.Add("SharedRouteProjection requires exactly one RouteProjections section; found $($sections.Count).")
        return [pscustomobject]@{ ids = @(); issues = @($issues) }
    }
    $seen = [Collections.Generic.Dictionary[string, bool]]::new([StringComparer]::Ordinal)
    $lineNumber = 0
    foreach ($line in ($sections[0].Groups['body'].Value -split '\r?\n')) {
        $lineNumber++
        if ($line -notmatch '^\s*[-*+]\s+') { continue }
        $match = [regex]::Match($line, '^-\s+`(?<id>[a-z0-9._-]+)`\s*[:\uFF1A]\s*.+?\s*$')
        if (-not $match.Success) {
            $issues.Add("RouteProjections bullet $lineNumber is malformed.")
            continue
        }
        $id = $match.Groups['id'].Value
        if ($seen.ContainsKey($id)) {
            $issues.Add("RouteProjections contains duplicate knowledgeId: $id")
            continue
        }
        $seen.Add($id, $true)
        $ids.Add($id)
    }
    if ($ids.Count -eq 0) { $issues.Add('RouteProjections contains no valid binding identities.') }
    [pscustomobject]@{ ids = @($ids | Sort-Object -CaseSensitive); issues = @($issues) }
}

function Resolve-ProjectSource([string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath) -or ($RelativePath -split '[/\\]') -contains '..') {
        throw "SourceRef must be a project-relative file: $RelativePath"
    }
    $full = [IO.Path]::GetFullPath((Join-Path $root $RelativePath.Replace('/', '\')))
    $prefix = $root.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "SourceRef escapes ProjectRoot: $RelativePath"
    }
    $current = $root.TrimEnd('\', '/')
    foreach ($segment in $full.Substring($current.Length).TrimStart('\', '/').Split(@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar), [StringSplitOptions]::RemoveEmptyEntries)) {
        $current = Join-Path $current $segment
        if (-not (Test-Path -LiteralPath $current)) { break }
        if (((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "SourceRef contains a reparse point: $RelativePath"
        }
    }
    $full
}

function Resolve-OutputPath([string]$RelativePath) {
    $full = Resolve-ProjectSource $RelativePath
    $outputRoot = [IO.Path]::GetFullPath((Join-Path $root 'ES\Output')).TrimEnd('\', '/')
    if (-not $full.StartsWith($outputRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "OutputPath must remain below ES/Output: $RelativePath"
    }
    $full
}

function Get-DeclaredSourceRefs([string]$Text) {
    $refs = [Collections.Generic.List[object]]::new()
    $issues = [Collections.Generic.List[object]]::new()
    $sections = @([regex]::Matches($Text, $sourceSectionPattern))
    if ($sections.Count -eq 0) {
        $issues.Add([pscustomobject]@{ code = 'SOURCE_REFS_SECTION_MISSING'; reason = 'Entry has no explicit SourceRefs section.' })
        return [pscustomobject]@{ refs = @(); issues = @($issues); bodyIndex = -1; bodyLength = 0; body = '' }
    }
    if ($sections.Count -ne 1) {
        $issues.Add([pscustomobject]@{ code = 'SOURCE_REFS_SECTION_COUNT'; reason = "Entry must contain exactly one SourceRefs section; found $($sections.Count)." })
        return [pscustomobject]@{ refs = @(); issues = @($issues); bodyIndex = -1; bodyLength = 0; body = '' }
    }

    $lineNumber = 0
    foreach ($line in ($sections[0].Groups['body'].Value -split '\r?\n')) {
        $lineNumber++
        if ($line -notmatch '^\s*[-*+]\s+') { continue }
        $match = [regex]::Match($line, $sourceBulletPattern)
        if (-not $match.Success) {
            $issues.Add([pscustomobject]@{
                code = 'SOURCE_REF_BULLET_MALFORMED'
                reason = "SourceRefs bullet $lineNumber must exactly match: - ``project/relative/path`` (``lowercase-sha256``)."
            })
            continue
        }
        $refs.Add([pscustomobject]@{
            declaredPath = $match.Groups['path'].Value
            declaredHash = $match.Groups['hash'].Value
        })
    }
    if ($refs.Count -eq 0) {
        $issues.Add([pscustomobject]@{ code = 'SOURCE_REFS_EMPTY'; reason = 'SourceRefs must contain at least one valid bullet.' })
    }
    [pscustomobject]@{
        refs = @($refs)
        issues = @($issues)
        bodyIndex = $sections[0].Groups['body'].Index
        bodyLength = $sections[0].Groups['body'].Length
        body = $sections[0].Groups['body'].Value
    }
}

function Get-ExpectedEntryProjection(
    [string]$Text,
    $DeclaredSourceSet,
    [object[]]$SnapshotRefs,
    [string]$HashSchema
) {
    if ($DeclaredSourceSet.bodyIndex -lt 0 -or $SnapshotRefs.Count -eq 0 -or $SnapshotRefs.Count -ne @($DeclaredSourceSet.refs).Count) {
        throw 'A complete SourceRefs body is required to calculate the expected refresh projection.'
    }

    $plannedByDeclaredPath = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    for ($sourceIndex = 0; $sourceIndex -lt $SnapshotRefs.Count; $sourceIndex++) {
        $declaredPath = [string]$DeclaredSourceSet.refs[$sourceIndex].declaredPath
        $plannedByDeclaredPath.Add($declaredPath, $SnapshotRefs[$sourceIndex])
    }
    $replacedPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $sourceEvaluator = [Text.RegularExpressions.MatchEvaluator]{
        param($match)
        $declaredPath = $match.Groups['path'].Value
        if (-not $plannedByDeclaredPath.ContainsKey($declaredPath) -or -not $replacedPaths.Add($declaredPath)) {
            throw "SourceRefs replacement does not match the unique planned source set: $declaredPath"
        }
        $currentHash = [string]$plannedByDeclaredPath[$declaredPath].currentHash
        if ($currentHash -cnotmatch '^[0-9a-f]{64}$') { throw 'A planned SourceRef currentHash is not a lowercase SHA-256 value.' }
        '- `' + $declaredPath + '` (`' + $currentHash + '`)'
    }
    $updatedSourceBody = ([regex]::new($sourceBulletPattern, [Text.RegularExpressions.RegexOptions]::Multiline)).Replace([string]$DeclaredSourceSet.body, $sourceEvaluator)
    if ($replacedPaths.Count -ne $SnapshotRefs.Count) { throw 'SourceRefs replacement did not consume the complete planned source set.' }
    $updated = $Text.Substring(0, [int]$DeclaredSourceSet.bodyIndex) + $updatedSourceBody + $Text.Substring([int]$DeclaredSourceSet.bodyIndex + [int]$DeclaredSourceSet.bodyLength)

    $contentHashFieldPattern = '(?m)^(`ContentHash`\s*' + $colonPattern + '\s*`)[0-9a-f]{64}(`\s*$)'
    if (@([regex]::Matches($updated, $contentHashFieldPattern)).Count -ne 1) { throw 'Entry must expose exactly one writable ContentHash field.' }
    $expectedContentHash = Get-ContentHash @($SnapshotRefs | ForEach-Object { [string]$_.currentHash })
    $updated = Replace-First $updated $contentHashFieldPattern "`${1}$expectedContentHash`${2}"

    $expectedSourceSetHash = ''
    $expectedEntryBodyHash = ''
    if ($HashSchema -eq 'v2') {
        $sourceSetHashFieldPattern = '(?m)^(`SourceSetHash`\s*' + $colonPattern + '\s*`)[0-9a-f]{64}(`\s*$)'
        $entryBodyHashFieldPattern = '(?m)^(`EntryBodyHash`\s*' + $colonPattern + '\s*`)[0-9a-f]{64}(`\s*$)'
        if (@([regex]::Matches($updated, $sourceSetHashFieldPattern)).Count -ne 1 -or @([regex]::Matches($updated, $entryBodyHashFieldPattern)).Count -ne 1) {
            throw 'HashSchema v2 requires exactly one writable SourceSetHash and EntryBodyHash field.'
        }
        $expectedSourceSetHash = $expectedContentHash
        $updated = Replace-First $updated $sourceSetHashFieldPattern "`${1}$expectedSourceSetHash`${2}"
        $expectedEntryBodyHash = Get-EntryBodyHash $updated
    }

    [pscustomobject]@{
        contentHash = $expectedContentHash
        sourceSetHash = $expectedSourceSetHash
        entryBodyHash = $expectedEntryBodyHash
    }
}

function New-RefreshFinding(
    [string]$Code,
    [string]$Entry,
    [string]$EntryHash,
    [string]$Source,
    [string]$DeclaredHash,
    [string]$CurrentHash,
    [string]$FirstSampleHash,
    [bool]$SnapshotStable,
    [string]$DeclaredContentHash,
    [string]$Action,
    [string]$Reason
) {
    [pscustomobject]@{
        code = $Code
        entry = $Entry
        entryHash = $EntryHash
        source = $Source
        declaredHash = $DeclaredHash
        currentHash = $CurrentHash
        firstSampleHash = $FirstSampleHash
        snapshotStable = $SnapshotStable
        declaredContentHash = $DeclaredContentHash
        action = $Action
        reason = $Reason
    }
}

if (-not (Test-Path -LiteralPath $entryRoot -PathType Container)) { throw "Knowledge entries directory missing: $entryRoot" }
if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf)) { throw "Knowledge index missing: $indexPath" }
$indexSnapshot = Get-StrictTextSnapshot $indexPath
if (-not $indexSnapshot.stable) { throw 'KnowledgeIndex.yaml changed while the refresh plan was being read; rerun the plan.' }
$indexText = [string]$indexSnapshot.text
$indexHash = [string]$indexSnapshot.hash
$changes = [Collections.Generic.List[object]]::new()
$entrySnapshots = [Collections.Generic.List[object]]::new()
$samplesBySource = @{}
$sharedRouteProjectionAllowed = [regex]::IsMatch($indexText, '(?m)^[ ]{4}sharedRouteProjectionAllowed:[ \t]*true[ \t]*\r?$')
foreach ($issue in @(Get-IndexGlobalIdentityIssues $indexText)) {
    $changes.Add((New-RefreshFinding -Code 'INDEX_IDENTITY_INVALID' -Entry 'Documentation/AIKnowledge/KnowledgeIndex.yaml' -EntryHash $indexHash -Source '' -DeclaredHash '' -CurrentHash '' -FirstSampleHash '' -SnapshotStable $false -DeclaredContentHash '' -Action 'reject-invalid-index-identity' -Reason $issue))
}

foreach ($entryPath in Get-ChildItem -LiteralPath $entryRoot -Filter '*.md' -File -Recurse | Sort-Object FullName) {
    $entryRead = Get-StrictTextSnapshot $entryPath.FullName
    $text = [string]$entryRead.text
    $hasKnowledgeId = [regex]::IsMatch($text, '(?mi)^`?KnowledgeId`?\s*' + $colonPattern)
    $hasSourceRefsHeader = [regex]::IsMatch($text, $sourceSectionHeaderPattern)
    if (-not $hasKnowledgeId -and -not $hasSourceRefsHeader) { continue }

    $entryRelative = Get-Relative $entryPath.FullName
    $entryHash = [string]$entryRead.hash
    $contentHashes = @(Get-MetadataValues $text 'ContentHash')
    $declaredContentHash = if ($contentHashes.Count -eq 1) { [string]$contentHashes[0] } else { '' }
    $declared = Get-DeclaredSourceRefs $text
    $snapshotRefs = [Collections.Generic.List[object]]::new()
    $blocked = $declared.issues.Count -gt 0

    if ($contentHashes.Count -ne 1 -or $declaredContentHash -cnotmatch '^[0-9a-f]{64}$') {
        $blocked = $true
        $changes.Add((New-RefreshFinding -Code 'ENTRY_CONTENT_HASH_INVALID' -Entry $entryRelative -EntryHash $entryHash -Source '' -DeclaredHash '' -CurrentHash '' -FirstSampleHash '' -SnapshotStable $false -DeclaredContentHash $declaredContentHash -Action 'reject-invalid-entry-content-hash' -Reason "Entry must contain exactly one lowercase SHA-256 ContentHash; found $($contentHashes.Count)."))
    }

    if (-not $entryRead.stable) {
        $changes.Add((New-RefreshFinding -Code 'ENTRY_SNAPSHOT_UNSTABLE' -Entry $entryRelative -EntryHash $entryHash -Source '' -DeclaredHash '' -CurrentHash $entryRead.postReadHash -FirstSampleHash $entryHash -SnapshotStable $false -DeclaredContentHash $declaredContentHash -Action 'wait-for-entry-stability' -Reason 'Entry hash changed while its strict UTF-8 bytes were being read.'))
    }

    $knowledgeIds = @(Get-MetadataValues $text 'KnowledgeId')
    $knowledgeId = if ($knowledgeIds.Count -eq 1) { [string]$knowledgeIds[0] } else { '' }
    if ($knowledgeIds.Count -ne 1) {
        $blocked = $true
        $changes.Add((New-RefreshFinding -Code 'KNOWLEDGE_IDENTITY_INVALID' -Entry $entryRelative -EntryHash $entryHash -Source '' -DeclaredHash '' -CurrentHash '' -FirstSampleHash '' -SnapshotStable $false -DeclaredContentHash $declaredContentHash -Action 'reject-invalid-knowledge-identity' -Reason "Entry must contain exactly one normalized KnowledgeId; found $($knowledgeIds.Count)."))
    }

    $entryModes = @(Get-MetadataValues $text 'EntryMode')
    $entryMode = if ($entryModes.Count -eq 0) { 'Canonical' } elseif ($entryModes.Count -eq 1) { [string]$entryModes[0] } else { '' }
    if ($entryMode -notin @('Canonical', 'SharedRouteProjection')) {
        $blocked = $true
        $changes.Add((New-RefreshFinding -Code 'ENTRY_MODE_INVALID' -Entry $entryRelative -EntryHash $entryHash -Source '' -DeclaredHash '' -CurrentHash '' -FirstSampleHash '' -SnapshotStable $false -DeclaredContentHash $declaredContentHash -Action 'reject-invalid-entry-mode' -Reason 'EntryMode must be omitted/Canonical or SharedRouteProjection.'))
    }

    $hashSchemas = @(Get-MetadataValues $text 'HashSchema')
    $hasHashSchema = [regex]::IsMatch($text, '(?mi)^`?HashSchema`?[ \t]*' + $colonPattern)
    $hashSchema = if ($hashSchemas.Count -eq 0 -and -not $hasHashSchema) { 'legacy' } elseif ($hashSchemas.Count -eq 1) { [string]$hashSchemas[0] } else { '' }
    $sourceSetHashes = @(Get-MetadataValues $text 'SourceSetHash')
    $entryBodyHashes = @(Get-MetadataValues $text 'EntryBodyHash')
    $hasSourceSetHash = [regex]::IsMatch($text, '(?mi)^`?SourceSetHash`?[ \t]*' + $colonPattern)
    $hasEntryBodyHash = [regex]::IsMatch($text, '(?mi)^`?EntryBodyHash`?[ \t]*' + $colonPattern)
    $declaredSourceSetHash = if ($sourceSetHashes.Count -eq 1) { [string]$sourceSetHashes[0] } else { '' }
    $declaredEntryBodyHash = if ($entryBodyHashes.Count -eq 1) { [string]$entryBodyHashes[0] } else { '' }
    if ($hashSchema -eq 'v2') {
        if ($sourceSetHashes.Count -ne 1 -or $declaredSourceSetHash -cnotmatch '^[0-9a-f]{64}$' -or $entryBodyHashes.Count -ne 1 -or $declaredEntryBodyHash -cnotmatch '^[0-9a-f]{64}$') {
            $blocked = $true
            $changes.Add((New-RefreshFinding -Code 'ENTRY_HASH_SCHEMA_PARTIAL' -Entry $entryRelative -EntryHash $entryHash -Source '' -DeclaredHash '' -CurrentHash '' -FirstSampleHash '' -SnapshotStable $false -DeclaredContentHash $declaredContentHash -Action 'reject-partial-hash-schema' -Reason 'HashSchema v2 requires exactly one lowercase SHA-256 SourceSetHash and EntryBodyHash.'))
        }
        elseif ($declaredContentHash -cne $declaredSourceSetHash) {
            $blocked = $true
            $changes.Add((New-RefreshFinding -Code 'ENTRY_V2_HASH_ALIAS_MISMATCH' -Entry $entryRelative -EntryHash $entryHash -Source '' -DeclaredHash '' -CurrentHash '' -FirstSampleHash '' -SnapshotStable $false -DeclaredContentHash $declaredContentHash -Action 'reject-invalid-v2-hash-projection' -Reason 'HashSchema v2 requires compatibility ContentHash to equal SourceSetHash.'))
        }
        if ($entryBodyHashes.Count -eq 1 -and $declaredEntryBodyHash -cmatch '^[0-9a-f]{64}$' -and $declaredEntryBodyHash -cne (Get-EntryBodyHash $text)) {
            $blocked = $true
            $changes.Add((New-RefreshFinding -Code 'ENTRY_BODY_HASH_MISMATCH' -Entry $entryRelative -EntryHash $entryHash -Source '' -DeclaredHash '' -CurrentHash '' -FirstSampleHash '' -SnapshotStable $false -DeclaredContentHash $declaredContentHash -Action 'reject-entry-body-hash-mismatch' -Reason 'EntryBodyHash does not match the normalized Knowledge body.'))
        }
    }
    elseif ($hashSchema -ne 'legacy') {
        $blocked = $true
        $changes.Add((New-RefreshFinding -Code 'ENTRY_HASH_SCHEMA_INVALID' -Entry $entryRelative -EntryHash $entryHash -Source '' -DeclaredHash '' -CurrentHash '' -FirstSampleHash '' -SnapshotStable $false -DeclaredContentHash $declaredContentHash -Action 'reject-invalid-hash-schema' -Reason 'Entry must omit HashSchema for legacy mode or declare exactly one supported schema.'))
    }
    elseif ($hasSourceSetHash -or $hasEntryBodyHash) {
        $blocked = $true
        $changes.Add((New-RefreshFinding -Code 'ENTRY_HASH_SCHEMA_PARTIAL' -Entry $entryRelative -EntryHash $entryHash -Source '' -DeclaredHash '' -CurrentHash '' -FirstSampleHash '' -SnapshotStable $false -DeclaredContentHash $declaredContentHash -Action 'reject-partial-hash-schema' -Reason 'SourceSetHash and EntryBodyHash require HashSchema v2; stable refresh will not write a half-closed projection.'))
    }

    $knowledgeRelative = $entryRelative.Substring('Documentation/AIKnowledge/'.Length)
    $indexBindings = Get-IndexBindingIds $indexText $knowledgeRelative
    foreach ($issue in @($indexBindings.issues)) {
        $blocked = $true
        $changes.Add((New-RefreshFinding -Code 'INDEX_BINDING_INVALID' -Entry $entryRelative -EntryHash $entryHash -Source '' -DeclaredHash '' -CurrentHash '' -FirstSampleHash '' -SnapshotStable $false -DeclaredContentHash $declaredContentHash -Action 'reject-invalid-index-binding' -Reason $issue))
    }
    if (@($indexBindings.bindings | Where-Object { $_.hashSchema -cne $hashSchema }).Count -gt 0) {
        $blocked = $true
        $changes.Add((New-RefreshFinding -Code 'INDEX_HASH_SCHEMA_MISMATCH' -Entry $entryRelative -EntryHash $entryHash -Source '' -DeclaredHash '' -CurrentHash '' -FirstSampleHash '' -SnapshotStable $false -DeclaredContentHash $declaredContentHash -Action 'reject-index-hash-schema-mismatch' -Reason "Entry HashSchema does not match every same-file KnowledgeIndex binding: $hashSchema."))
    }
    if ($hashSchema -eq 'v2' -and @($indexBindings.bindings | Where-Object {
        $_.contentHash -cne $declaredContentHash -or
        $_.sourceSetHash -cne $declaredSourceSetHash -or
        $_.entryBodyHash -cne $declaredEntryBodyHash
    }).Count -gt 0) {
        $blocked = $true
        $changes.Add((New-RefreshFinding -Code 'INDEX_V2_HASH_PROJECTION_MISMATCH' -Entry $entryRelative -EntryHash $entryHash -Source '' -DeclaredHash '' -CurrentHash '' -FirstSampleHash '' -SnapshotStable $false -DeclaredContentHash $declaredContentHash -Action 'reject-index-v2-hash-projection-mismatch' -Reason 'Every same-file v2 KnowledgeIndex binding must match the Entry ContentHash, SourceSetHash, and EntryBodyHash.'))
    }
    if ($entryMode -eq 'Canonical' -and ($indexBindings.ids.Count -ne 1 -or [string]$indexBindings.ids[0] -cne $knowledgeId)) {
        $blocked = $true
        $changes.Add((New-RefreshFinding -Code 'INDEX_BINDING_IDENTITY_INVALID' -Entry $entryRelative -EntryHash $entryHash -Source '' -DeclaredHash '' -CurrentHash '' -FirstSampleHash '' -SnapshotStable $false -DeclaredContentHash $declaredContentHash -Action 'reject-invalid-index-binding' -Reason "KnowledgeIndex bindings do not match EntryMode/KnowledgeId: mode=$entryMode, knowledgeId=$knowledgeId, bindings=$(@($indexBindings.ids) -join ',')."))
    }
    elseif ($entryMode -eq 'SharedRouteProjection') {
        $routeProjectionIds = Get-RouteProjectionIds $text
        foreach ($issue in @($routeProjectionIds.issues)) {
            $blocked = $true
            $changes.Add((New-RefreshFinding -Code 'ROUTE_PROJECTION_IDENTITY_INVALID' -Entry $entryRelative -EntryHash $entryHash -Source '' -DeclaredHash '' -CurrentHash '' -FirstSampleHash '' -SnapshotStable $false -DeclaredContentHash $declaredContentHash -Action 'reject-invalid-route-projection' -Reason $issue))
        }
        if (-not $sharedRouteProjectionAllowed) {
            $blocked = $true
            $changes.Add((New-RefreshFinding -Code 'ENTRY_MODE_NOT_ALLOWED' -Entry $entryRelative -EntryHash $entryHash -Source '' -DeclaredHash '' -CurrentHash '' -FirstSampleHash '' -SnapshotStable $false -DeclaredContentHash $declaredContentHash -Action 'reject-disallowed-entry-mode' -Reason 'KnowledgeIndex does not enable SharedRouteProjection.'))
        }
        if ($indexBindings.ids.Count -eq 0 -or -not (Test-OrdinalSetEqual $indexBindings.ids $routeProjectionIds.ids)) {
            $blocked = $true
            $changes.Add((New-RefreshFinding -Code 'INDEX_BINDING_IDENTITY_INVALID' -Entry $entryRelative -EntryHash $entryHash -Source '' -DeclaredHash '' -CurrentHash '' -FirstSampleHash '' -SnapshotStable $false -DeclaredContentHash $declaredContentHash -Action 'reject-invalid-index-binding' -Reason "SharedRouteProjection identities differ from KnowledgeIndex bindings: projections=$(@($routeProjectionIds.ids) -join ','), bindings=$(@($indexBindings.ids) -join ',')."))
        }
    }

    foreach ($issue in @($declared.issues)) {
        $changes.Add((New-RefreshFinding -Code $issue.code -Entry $entryRelative -EntryHash $entryHash -Source '' -DeclaredHash '' -CurrentHash '' -FirstSampleHash '' -SnapshotStable $false -DeclaredContentHash $declaredContentHash -Action 'reject-invalid-source-ref-set' -Reason $issue.reason))
    }

    $seenPaths = [Collections.Generic.Dictionary[string, bool]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($ref in @($declared.refs)) {
        $normalizedPath = ([string]$ref.declaredPath).Replace('\', '/')
        $currentHash = ''
        $firstSampleHash = ''
        $snapshotStable = $false
        try {
            $sourcePath = Resolve-ProjectSource ([string]$ref.declaredPath)
            $normalizedPath = Get-Relative $sourcePath
            if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
                throw "SourceRef file is missing: $($ref.declaredPath)"
            }
            if ($seenPaths.ContainsKey($normalizedPath)) {
                throw "Duplicate normalized SourceRef: $normalizedPath"
            }
            $seenPaths.Add($normalizedPath, $true)
            $sourceKey = $normalizedPath.ToLowerInvariant()
            if (-not $samplesBySource.ContainsKey($sourceKey)) {
                $samplesBySource[$sourceKey] = Get-StableFileHash $sourcePath $SampleDelayMilliseconds
            }
            $sample = $samplesBySource[$sourceKey]
            $currentHash = $sample.second
            $firstSampleHash = $sample.first
            $snapshotStable = [bool]$sample.stable
        }
        catch {
            $blocked = $true
            $changes.Add((New-RefreshFinding -Code 'SOURCE_REF_INVALID' -Entry $entryRelative -EntryHash $entryHash -Source $normalizedPath -DeclaredHash $ref.declaredHash -CurrentHash '' -FirstSampleHash '' -SnapshotStable $false -DeclaredContentHash $declaredContentHash -Action 'reject-invalid-source-ref' -Reason $_.Exception.Message))
        }

        $snapshotRefs.Add([pscustomobject]@{
            path = $normalizedPath
            declaredHash = [string]$ref.declaredHash
            currentHash = $currentHash
            firstSampleHash = $firstSampleHash
            snapshotStable = $snapshotStable
        })

        if (-not $blocked -and -not $snapshotStable) {
            $changes.Add((New-RefreshFinding -Code 'SOURCE_SNAPSHOT_UNSTABLE' -Entry $entryRelative -EntryHash $entryHash -Source $normalizedPath -DeclaredHash $ref.declaredHash -CurrentHash $currentHash -FirstSampleHash $firstSampleHash -SnapshotStable $false -DeclaredContentHash $declaredContentHash -Action 'wait-for-source-stability' -Reason 'Source hash changed between the two planning samples.'))
        }
        elseif (-not $blocked -and $currentHash -cne [string]$ref.declaredHash) {
            $changes.Add((New-RefreshFinding -Code 'SOURCE_HASH_DRIFT' -Entry $entryRelative -EntryHash $entryHash -Source $normalizedPath -DeclaredHash $ref.declaredHash -CurrentHash $currentHash -FirstSampleHash $firstSampleHash -SnapshotStable $true -DeclaredContentHash $declaredContentHash -Action 'review-and-refresh-source-ref' -Reason 'Declared SourceRef hash differs from the stable current source hash.'))
        }
    }

    if ($hashSchema -eq 'v2' -and $declared.issues.Count -eq 0 -and $declared.refs.Count -gt 0) {
        $expectedDeclaredSourceSetHash = Get-ContentHash @($declared.refs | ForEach-Object { [string]$_.declaredHash })
        if ($declaredContentHash -cne $expectedDeclaredSourceSetHash -or $declaredSourceSetHash -cne $expectedDeclaredSourceSetHash) {
            $blocked = $true
            $changes.Add((New-RefreshFinding -Code 'ENTRY_V2_SOURCE_SET_HASH_MISMATCH' -Entry $entryRelative -EntryHash $entryHash -Source '' -DeclaredHash '' -CurrentHash $expectedDeclaredSourceSetHash -FirstSampleHash '' -SnapshotStable $false -DeclaredContentHash $declaredContentHash -Action 'reject-invalid-v2-source-set-hash' -Reason 'ContentHash and SourceSetHash must equal the hash of the complete declared SourceRef hash set.'))
        }
    }

    $expectedContentHash = ''
    $expectedSourceSetHash = ''
    $expectedEntryBodyHash = ''
    if (-not $blocked) {
        try {
            $expectedProjection = Get-ExpectedEntryProjection -Text $text -DeclaredSourceSet $declared -SnapshotRefs $snapshotRefs.ToArray() -HashSchema $hashSchema
            $expectedContentHash = [string]$expectedProjection.contentHash
            $expectedSourceSetHash = [string]$expectedProjection.sourceSetHash
            $expectedEntryBodyHash = [string]$expectedProjection.entryBodyHash
        }
        catch {
            $blocked = $true
            $changes.Add((New-RefreshFinding -Code 'ENTRY_REFRESH_PROJECTION_INVALID' -Entry $entryRelative -EntryHash $entryHash -Source '' -DeclaredHash '' -CurrentHash '' -FirstSampleHash '' -SnapshotStable $false -DeclaredContentHash $declaredContentHash -Action 'reject-invalid-refresh-projection' -Reason $_.Exception.Message))
        }
    }
    $projectedIndexBindings = @($indexBindings.bindings | ForEach-Object {
        [pscustomobject]@{
            id = [string]$_.id
            hashSchema = [string]$_.hashSchema
            contentHash = [string]$_.contentHash
            sourceSetHash = [string]$_.sourceSetHash
            entryBodyHash = [string]$_.entryBodyHash
            expectedContentHash = $expectedContentHash
            expectedSourceSetHash = $expectedSourceSetHash
            expectedEntryBodyHash = $expectedEntryBodyHash
        }
    })

    $entrySnapshots.Add([pscustomobject]@{
        entry = $entryRelative
        knowledgeId = $knowledgeId
        entryMode = $entryMode
        hashSchema = $hashSchema
        entryHash = $entryHash
        status = if ($blocked) { 'blocked' } else { 'ready' }
        sourceSetHash = Get-SourceSetHash $snapshotRefs.ToArray()
        declaredContentHash = $declaredContentHash
        declaredSourceSetHash = $declaredSourceSetHash
        declaredEntryBodyHash = $declaredEntryBodyHash
        expectedContentHash = $expectedContentHash
        expectedSourceSetHash = $expectedSourceSetHash
        expectedEntryBodyHash = $expectedEntryBodyHash
        sourceRefCount = $snapshotRefs.Count
        sourceRefs = @($snapshotRefs)
        indexBindingCount = $indexBindings.ids.Count
        indexBindingIds = @($indexBindings.ids)
        indexBindings = @($projectedIndexBindings)
    })
}

$output = Resolve-OutputPath $OutputPath
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $output) | Out-Null
$blockerCount = @($changes | Where-Object { $_.action -like 'reject-*' }).Count
$unstableFindingCount = @($changes | Where-Object { $_.action -like 'wait-for-*-stability' }).Count
$planHash = Get-PlanHash -EntrySnapshots $entrySnapshots.ToArray() -Findings $changes.ToArray() -IndexHash $indexHash
$report = [ordered]@{
    schemaVersion = 3
    toolId = 'es-knowledge-validator.refresh-plan'
    refreshAlgorithmVersion = $refreshAlgorithmVersion
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    mutatesSources = $false
    mutatesKnowledge = $false
    planHash = $planHash
    indexHash = $indexHash
    planStatus = if ($blockerCount -gt 0) { 'blocked' } else { 'ready' }
    targetEntryCount = $entrySnapshots.Count
    entrySnapshots = @($entrySnapshots)
    findingCount = $changes.Count
    findings = @($changes)
    blockerCount = $blockerCount
    unstableFindingCount = $unstableFindingCount
    nextAction = if ($blockerCount -gt 0) {
        'Repair the blocked SourceRefs structure or path, then regenerate the plan.'
    }
    elseif ($changes.Count -eq 0) {
        'No SourceRef drift detected.'
    }
    elseif ($unstableFindingCount -gt 0) {
        'Wait for unstable sources to settle, regenerate the plan, then refresh only entries whose complete source set is stable.'
    }
    else {
        'Review each stable finding, then run Invoke-ESKnowledgeStableRefresh.ps1 in preview and apply modes.'
    }
}
[IO.File]::WriteAllText($output, ($report | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
$report | ConvertTo-Json -Depth 12
if ($blockerCount -gt 0) { exit 2 }
