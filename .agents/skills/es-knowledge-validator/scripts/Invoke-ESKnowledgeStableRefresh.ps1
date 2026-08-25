[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [string]$PlanPath = 'ES/Output/KnowledgeValidation/refresh-plan.json',
    [string]$OutputPath = 'ES/Output/KnowledgeValidation/stable-refresh-receipt.json',
    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
trap {
    [Console]::Error.WriteLine($_.Exception.ToString())
    [Console]::Error.WriteLine($_.ScriptStackTrace)
    exit 1
}
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$strictUtf8 = [Text.UTF8Encoding]::new($false, $true)
$refreshAlgorithmVersion = 'es-knowledge-stable-refresh-v2-source-normalized'
$colonPattern = '[:' + [char]0xFF1A + ']'
$sourceSectionPattern = '(?ms)(?:^##\s+SourceRefs\s*$|^`SourceRefs`\s*' + $colonPattern + '\s*$|^SourceRefs\s*' + $colonPattern + '\s*$)\r?\n(?<body>.*?)(?=^##\s+|^`?[A-Za-z][A-Za-z0-9]*`?\s*' + $colonPattern + '\s*|\z)'
$sourceBulletPattern = '^- `(?<path>[^`\r\n]+)` \(`(?<hash>[0-9a-f]{64})`\)$'

function Get-Hash([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
        $stream.Dispose()
    }
}

function Get-SourceHash([string]$Path) {
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

function Get-ContentHash([string[]]$Hashes) {
    Get-Sha256 (Join-Ordinal $Hashes '')
}

function Get-SourceSetHash([object[]]$SourceRefs) {
    $records = @($SourceRefs | ForEach-Object {
        New-CanonicalRecord -Kind 'source-set' -Values @($_.path, $_.declaredHash)
    })
    Get-Sha256 (Join-Ordinal $records "`n")
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

function Get-PlanHash($Plan) {
    $records = [Collections.Generic.List[string]]::new()
    $records.Add((New-CanonicalRecord -Kind 'plan' -Values @($Plan.schemaVersion, $Plan.toolId, $Plan.refreshAlgorithmVersion, $Plan.indexHash)))
    foreach ($entry in @($Plan.entrySnapshots)) {
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
    foreach ($finding in @($Plan.findings)) {
        $records.Add((New-CanonicalRecord -Kind 'finding' -Values @(
            $finding.code, $finding.entry, $finding.entryHash, $finding.source,
            $finding.declaredHash, $finding.currentHash, $finding.firstSampleHash,
            $finding.snapshotStable, $finding.declaredContentHash, $finding.action,
            $finding.reason
        )))
    }
    Get-Sha256 (Join-Ordinal $records.ToArray() "`n")
}

function Replace-First([string]$Text, [string]$Pattern, [string]$Replacement) {
    $match = ([regex]::new($Pattern)).Match($Text)
    if (-not $match.Success) { return $Text }
    $Text.Substring(0, $match.Index) + $match.Result($Replacement) + $Text.Substring($match.Index + $match.Length)
}

function Assert-ProjectFile([string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath) -or ($RelativePath -split '[/\\]') -contains '..') {
        throw "Path expansion denied: $RelativePath"
    }
    $full = [IO.Path]::GetFullPath((Join-Path $root $RelativePath.Replace('/', '\')))
    $prefix = $root.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path expansion denied: $RelativePath"
    }
    $current = $root.TrimEnd('\', '/')
    foreach ($segment in $full.Substring($current.Length).TrimStart('\', '/').Split(@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar), [StringSplitOptions]::RemoveEmptyEntries)) {
        $current = Join-Path $current $segment
        if (-not (Test-Path -LiteralPath $current)) { break }
        if (((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Path contains a reparse point: $RelativePath"
        }
    }
    $full
}

function Get-ProjectRelative([string]$Path) {
    $Path.Substring($root.Length).TrimStart('\', '/').Replace('\', '/')
}

function Assert-KnowledgeEntry([string]$RelativePath) {
    $full = Assert-ProjectFile $RelativePath
    $knowledgeRoot = [IO.Path]::GetFullPath((Join-Path $root 'Documentation\AIKnowledge')).TrimEnd('\', '/')
    $prefix = $knowledgeRoot + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetExtension($full) -cne '.md') {
        throw "Knowledge entry path is outside Documentation/AIKnowledge or is not Markdown: $RelativePath"
    }
    $full
}

function Assert-OutputPath([string]$RelativePath) {
    $full = Assert-ProjectFile $RelativePath
    $outputRoot = [IO.Path]::GetFullPath((Join-Path $root 'ES\Output')).TrimEnd('\', '/')
    if (-not $full.StartsWith($outputRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "OutputPath must remain below ES/Output: $RelativePath"
    }
    $full
}

function Assert-Property($Object, [string]$Name, [string]$Context) {
    if ($null -eq $Object -or $null -eq $Object.PSObject.Properties[$Name]) {
        throw "Refresh plan is missing ${Context}.${Name}."
    }
}

function Assert-PlanHashObject($Value, [string]$Context) {
    if ($null -eq $Value -or $Value -isnot [Management.Automation.PSCustomObject]) {
        throw "Refresh plan ${Context} must be a JSON object."
    }
}

function Assert-PlanHashStringProperty($Object, [string]$Name, [string]$Context) {
    Assert-Property $Object $Name $Context
    if ($Object.$Name -isnot [string]) {
        throw "Refresh plan ${Context}.${Name} must be a JSON string."
    }
}

function Assert-PlanHashIntegerProperty($Object, [string]$Name, [string]$Context) {
    Assert-Property $Object $Name $Context
    if ($Object.$Name -isnot [int] -and $Object.$Name -isnot [long]) {
        throw "Refresh plan ${Context}.${Name} must be a JSON integer."
    }
}

function Assert-PlanHashShape($Plan) {
    Assert-PlanHashObject $Plan 'plan'
    Assert-PlanHashIntegerProperty $Plan 'schemaVersion' 'plan'
    foreach ($name in @('toolId', 'refreshAlgorithmVersion', 'planHash', 'indexHash')) {
        Assert-PlanHashStringProperty $Plan $name 'plan'
    }
    foreach ($name in @('entrySnapshots', 'findings')) {
        Assert-Property $Plan $name 'plan'
    }

    $entryIndex = 0
    foreach ($entry in @($Plan.entrySnapshots)) {
        $context = "entrySnapshots[$entryIndex]"
        Assert-PlanHashObject $entry $context
        foreach ($name in @('entry', 'knowledgeId', 'entryMode', 'hashSchema', 'entryHash', 'status', 'sourceSetHash', 'declaredContentHash', 'declaredSourceSetHash', 'declaredEntryBodyHash', 'expectedContentHash', 'expectedSourceSetHash', 'expectedEntryBodyHash')) {
            Assert-PlanHashStringProperty $entry $name $context
        }
        foreach ($name in @('sourceRefCount', 'indexBindingCount')) {
            Assert-PlanHashIntegerProperty $entry $name $context
        }
        foreach ($name in @('sourceRefs', 'indexBindingIds', 'indexBindings')) {
            Assert-Property $entry $name $context
        }

        $bindingIndex = 0
        foreach ($bindingId in @($entry.indexBindingIds)) {
            if ($bindingId -isnot [string]) {
                throw "Refresh plan ${context}.indexBindingIds[$bindingIndex] must be a JSON string."
            }
            $bindingIndex++
        }

        $bindingIndex = 0
        foreach ($binding in @($entry.indexBindings)) {
            $bindingContext = "${context}.indexBindings[$bindingIndex]"
            Assert-PlanHashObject $binding $bindingContext
            foreach ($name in @('id', 'hashSchema', 'contentHash', 'sourceSetHash', 'entryBodyHash', 'expectedContentHash', 'expectedSourceSetHash', 'expectedEntryBodyHash')) {
                Assert-PlanHashStringProperty $binding $name $bindingContext
            }
            $bindingIndex++
        }

        $sourceIndex = 0
        foreach ($source in @($entry.sourceRefs)) {
            $sourceContext = "${context}.sourceRefs[$sourceIndex]"
            Assert-PlanHashObject $source $sourceContext
            foreach ($name in @('path', 'declaredHash', 'currentHash', 'firstSampleHash')) {
                Assert-PlanHashStringProperty $source $name $sourceContext
            }
            Assert-Property $source 'snapshotStable' $sourceContext
            if ($source.snapshotStable -isnot [bool]) {
                throw "Refresh plan ${sourceContext}.snapshotStable must be a JSON Boolean."
            }
            $sourceIndex++
        }
        $entryIndex++
    }

    $findingIndex = 0
    foreach ($finding in @($Plan.findings)) {
        $context = "findings[$findingIndex]"
        Assert-PlanHashObject $finding $context
        foreach ($name in @('code', 'entry', 'entryHash', 'source', 'declaredHash', 'currentHash', 'firstSampleHash', 'declaredContentHash', 'action', 'reason')) {
            Assert-PlanHashStringProperty $finding $name $context
        }
        Assert-Property $finding 'snapshotStable' $context
        if ($finding.snapshotStable -isnot [bool]) {
            throw "Refresh plan ${context}.snapshotStable must be a JSON Boolean."
        }
        $findingIndex++
    }
}

function Get-StrictTextSnapshot([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $hash = Get-Sha256Bytes $bytes
    $text = $strictUtf8.GetString($bytes)
    $postReadHash = if (Test-Path -LiteralPath $Path -PathType Leaf) { Get-Hash $Path } else { '' }
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

function Get-IndexBindingIds([string]$Text, [string]$KnowledgeRelative) {
    $ids = [Collections.Generic.List[string]]::new()
    $bindings = [Collections.Generic.List[object]]::new()
    $issues = [Collections.Generic.List[string]]::new()
    $blockPattern = '(?ms)^\s{2}-\s+knowledgeId:.*?(?=^\s{2}-\s+knowledgeId:|\z)'
    $filePattern = '(?m)^\s{4}file:\s*' + [regex]::Escape($KnowledgeRelative) + '\s*$'
    $idPattern = '(?m)^\s{2}-\s+knowledgeId:\s*(?<id>[a-z0-9._-]+)\s*$'
    $seen = [Collections.Generic.Dictionary[string, bool]]::new([StringComparer]::Ordinal)
    foreach ($block in @([regex]::Matches($Text, $blockPattern))) {
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

function Test-IndexBindingsEqual([object[]]$Left, [object[]]$Right) {
    $leftBindings = @($Left | Sort-Object id)
    $rightBindings = @($Right | Sort-Object id)
    if ($leftBindings.Count -ne $rightBindings.Count) { return $false }
    for ($i = 0; $i -lt $leftBindings.Count; $i++) {
        foreach ($name in @('id', 'hashSchema', 'contentHash', 'sourceSetHash', 'entryBodyHash')) {
            if ([string]$leftBindings[$i].$name -cne [string]$rightBindings[$i].$name) { return $false }
        }
    }
    $true
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

function Get-CandidateEntryPaths {
    $entryRoot = Assert-ProjectFile 'Documentation/AIKnowledge'
    if (-not (Test-Path -LiteralPath $entryRoot -PathType Container)) { throw 'Documentation/AIKnowledge is missing.' }
    @(
        Get-ChildItem -LiteralPath $entryRoot -Filter '*.md' -File -Recurse | Sort-Object FullName | ForEach-Object {
            $read = Get-StrictTextSnapshot $_.FullName
            if (-not $read.stable) { throw "Knowledge entry changed while enumerating candidates: $(Get-ProjectRelative $_.FullName)" }
            $text = [string]$read.text
            if ([regex]::IsMatch($text, '(?mi)^`?KnowledgeId`?\s*' + $colonPattern) -or [regex]::IsMatch($text, '(?m)^(?:##\s+SourceRefs\s*$|`SourceRefs`\s*' + $colonPattern + '\s*$|SourceRefs\s*' + $colonPattern + '\s*$)')) {
                Get-ProjectRelative $_.FullName
            }
        }
    )
}

function Assert-PlanShape($Plan) {
    foreach ($name in @('schemaVersion', 'toolId', 'refreshAlgorithmVersion', 'planHash', 'indexHash', 'planStatus', 'targetEntryCount', 'entrySnapshots', 'findingCount', 'findings', 'blockerCount', 'unstableFindingCount')) {
        Assert-Property $Plan $name 'plan'
    }
    if ($Plan.schemaVersion -ne 3 -or $Plan.toolId -ne 'es-knowledge-validator.refresh-plan' -or [string]$Plan.refreshAlgorithmVersion -cne $refreshAlgorithmVersion) {
        throw 'Unsupported refresh plan contract.'
    }
    foreach ($name in @('planHash', 'indexHash')) {
        if ([string]$Plan.$name -cnotmatch '^[0-9a-f]{64}$') { throw "Refresh plan ${name} is not a lowercase SHA-256 value." }
    }

    $entries = @($Plan.entrySnapshots)
    if ([int]$Plan.targetEntryCount -ne $entries.Count) { throw 'Refresh plan targetEntryCount does not match entrySnapshots.' }
    $seenEntries = [Collections.Generic.Dictionary[string, bool]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $entries) {
        foreach ($name in @('entry', 'knowledgeId', 'entryMode', 'hashSchema', 'entryHash', 'status', 'sourceSetHash', 'declaredContentHash', 'declaredSourceSetHash', 'declaredEntryBodyHash', 'expectedContentHash', 'expectedSourceSetHash', 'expectedEntryBodyHash', 'sourceRefCount', 'sourceRefs', 'indexBindingCount', 'indexBindingIds', 'indexBindings')) {
            Assert-Property $entry $name 'entrySnapshot'
        }
        if ([string]$entry.entryHash -cnotmatch '^[0-9a-f]{64}$' -or [string]$entry.sourceSetHash -cnotmatch '^[0-9a-f]{64}$') {
            throw "Refresh plan entry hashes are invalid: $($entry.entry)"
        }
        if ([string]$entry.status -notin @('ready', 'blocked')) { throw "Refresh plan entry status is invalid: $($entry.entry)" }
        if ([string]$entry.knowledgeId -cnotmatch '^[a-z0-9._-]+$') { throw "Refresh plan KnowledgeId is invalid: $($entry.entry)" }
        if ([string]$entry.entryMode -notin @('Canonical', 'SharedRouteProjection')) { throw "Refresh plan EntryMode is invalid: $($entry.entry)" }
        if ([string]$entry.hashSchema -notin @('legacy', 'v2')) { throw "Refresh plan HashSchema is invalid: $($entry.entry)" }
        if ([string]$entry.status -eq 'ready' -and [string]$entry.declaredContentHash -cnotmatch '^[0-9a-f]{64}$') {
            throw "Ready refresh plan ContentHash is invalid: $($entry.entry)"
        }
        if ([string]$entry.status -eq 'ready' -and [string]$entry.expectedContentHash -cnotmatch '^[0-9a-f]{64}$') {
            throw "Ready refresh plan expected ContentHash is invalid: $($entry.entry)"
        }
        if ([string]$entry.status -eq 'blocked' -and (-not [string]::IsNullOrEmpty([string]$entry.expectedContentHash) -or
            -not [string]::IsNullOrEmpty([string]$entry.expectedSourceSetHash) -or -not [string]::IsNullOrEmpty([string]$entry.expectedEntryBodyHash))) {
            throw "Blocked refresh plan entry contains an expected write projection: $($entry.entry)"
        }
        if ([string]$entry.hashSchema -eq 'v2') {
            if ([string]$entry.status -eq 'ready' -and ([string]$entry.declaredSourceSetHash -cnotmatch '^[0-9a-f]{64}$' -or [string]$entry.declaredEntryBodyHash -cnotmatch '^[0-9a-f]{64}$' -or
                [string]$entry.expectedSourceSetHash -cnotmatch '^[0-9a-f]{64}$' -or [string]$entry.expectedEntryBodyHash -cnotmatch '^[0-9a-f]{64}$')) {
                throw "Ready v2 refresh plan hashes are invalid: $($entry.entry)"
            }
        }
        elseif ([string]$entry.status -eq 'ready' -and (-not [string]::IsNullOrEmpty([string]$entry.declaredSourceSetHash) -or -not [string]::IsNullOrEmpty([string]$entry.declaredEntryBodyHash) -or
            -not [string]::IsNullOrEmpty([string]$entry.expectedSourceSetHash) -or -not [string]::IsNullOrEmpty([string]$entry.expectedEntryBodyHash))) {
            throw "Legacy refresh plan entry contains v2 hash metadata: $($entry.entry)"
        }
        $entryPath = Assert-KnowledgeEntry ([string]$entry.entry)
        if ((Get-ProjectRelative $entryPath) -cne [string]$entry.entry) { throw "Refresh plan entry path is not normalized: $($entry.entry)" }
        if ($seenEntries.ContainsKey([string]$entry.entry)) { throw "Refresh plan contains a duplicate entry snapshot: $($entry.entry)" }
        $seenEntries.Add([string]$entry.entry, $true)

        $refs = @($entry.sourceRefs)
        if ([int]$entry.sourceRefCount -ne $refs.Count) { throw "Refresh plan sourceRefCount mismatch: $($entry.entry)" }
        if ([string]$entry.status -eq 'ready' -and $refs.Count -eq 0) { throw "Ready refresh plan entry has no SourceRefs: $($entry.entry)" }
        $seenSources = [Collections.Generic.Dictionary[string, bool]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($source in $refs) {
            foreach ($name in @('path', 'declaredHash', 'currentHash', 'firstSampleHash', 'snapshotStable')) {
                Assert-Property $source $name 'sourceRefSnapshot'
            }
            if ([string]$source.declaredHash -cnotmatch '^[0-9a-f]{64}$') { throw "Refresh plan declared SourceRef hash is invalid: $($source.path)" }
            foreach ($hashName in @('currentHash', 'firstSampleHash')) {
                if (-not [string]::IsNullOrEmpty([string]$source.$hashName) -and [string]$source.$hashName -cnotmatch '^[0-9a-f]{64}$') {
                    throw "Refresh plan ${hashName} is invalid: $($source.path)"
                }
            }
            if ($source.snapshotStable -isnot [bool]) { throw "Refresh plan snapshotStable is not Boolean: $($source.path)" }
            if ([string]$entry.status -eq 'ready') {
                if ([string]::IsNullOrEmpty([string]$source.currentHash) -or [string]::IsNullOrEmpty([string]$source.firstSampleHash)) {
                    throw "Ready refresh plan SourceRef has no complete hash snapshot: $($source.path)"
                }
                $sourcePath = Assert-ProjectFile ([string]$source.path)
                if ((Get-ProjectRelative $sourcePath) -cne [string]$source.path) { throw "Refresh plan SourceRef path is not normalized: $($source.path)" }
                if ($seenSources.ContainsKey([string]$source.path)) { throw "Refresh plan contains a duplicate normalized SourceRef: $($source.path)" }
                $seenSources.Add([string]$source.path, $true)
            }
        }
        if ((Get-SourceSetHash $refs) -cne [string]$entry.sourceSetHash) { throw "Refresh plan sourceSetHash mismatch: $($entry.entry)" }
        if ([string]$entry.status -eq 'ready') {
            $expectedContentHash = Get-ContentHash @($refs | ForEach-Object { [string]$_.currentHash })
            if ($expectedContentHash -cne [string]$entry.expectedContentHash) { throw "Refresh plan expected ContentHash mismatch: $($entry.entry)" }
            if ([string]$entry.hashSchema -eq 'v2' -and [string]$entry.expectedSourceSetHash -cne $expectedContentHash) {
                throw "Refresh plan expected SourceSetHash mismatch: $($entry.entry)"
            }
        }
        $bindingIds = @($entry.indexBindingIds)
        if ([int]$entry.indexBindingCount -ne $bindingIds.Count) { throw "Refresh plan indexBindingCount mismatch: $($entry.entry)" }
        if (@($bindingIds | Where-Object { [string]$_ -cnotmatch '^[a-z0-9._-]+$' }).Count -gt 0 -or @($bindingIds | Sort-Object -CaseSensitive -Unique).Count -ne $bindingIds.Count) {
            throw "Refresh plan index binding identities are invalid: $($entry.entry)"
        }
        $sortedBindingIds = @($bindingIds | Sort-Object -CaseSensitive)
        for ($bindingIndex = 0; $bindingIndex -lt $bindingIds.Count; $bindingIndex++) {
            if ([string]$bindingIds[$bindingIndex] -cne [string]$sortedBindingIds[$bindingIndex]) {
                throw "Refresh plan index binding identities are not in Ordinal order: $($entry.entry)"
            }
        }
        if ([string]$entry.status -eq 'ready' -and $bindingIds.Count -eq 0) { throw "Ready refresh plan entry has no KnowledgeIndex binding: $($entry.entry)" }
        if ([string]$entry.entryMode -eq 'Canonical' -and [string]$entry.status -eq 'ready' -and ($bindingIds.Count -ne 1 -or [string]$bindingIds[0] -cne [string]$entry.knowledgeId)) {
            throw "Canonical refresh plan binding does not match KnowledgeId: $($entry.entry)"
        }
        $bindingSnapshots = @($entry.indexBindings)
        if ($bindingSnapshots.Count -ne $bindingIds.Count) { throw "Refresh plan index binding projection count mismatch: $($entry.entry)" }
        for ($bindingIndex = 0; $bindingIndex -lt $bindingSnapshots.Count; $bindingIndex++) {
            $binding = $bindingSnapshots[$bindingIndex]
            foreach ($name in @('id', 'hashSchema', 'contentHash', 'sourceSetHash', 'entryBodyHash', 'expectedContentHash', 'expectedSourceSetHash', 'expectedEntryBodyHash')) { Assert-Property $binding $name 'indexBindingSnapshot' }
            if ([string]$binding.id -cne [string]$bindingIds[$bindingIndex]) { throw "Refresh plan index binding projection identities are not in matching Ordinal order: $($entry.entry)" }
            if ([string]$binding.expectedContentHash -cne [string]$entry.expectedContentHash -or
                [string]$binding.expectedSourceSetHash -cne [string]$entry.expectedSourceSetHash -or
                [string]$binding.expectedEntryBodyHash -cne [string]$entry.expectedEntryBodyHash) {
                throw "Refresh plan expected Index projection does not match the Entry: $($entry.entry)"
            }
            if ([string]$entry.status -eq 'ready') {
                if ([string]$binding.hashSchema -cne [string]$entry.hashSchema -or [string]$binding.contentHash -cne [string]$entry.declaredContentHash) {
                    throw "Ready refresh plan Index projection does not match the Entry: $($entry.entry)"
                }
                if ([string]$entry.hashSchema -eq 'v2') {
                    if ([string]$binding.sourceSetHash -cne [string]$entry.declaredSourceSetHash -or [string]$binding.entryBodyHash -cne [string]$entry.declaredEntryBodyHash) {
                        throw "Ready v2 refresh plan Index projection does not match the Entry: $($entry.entry)"
                    }
                }
                elseif (-not [string]::IsNullOrEmpty([string]$binding.sourceSetHash) -or -not [string]::IsNullOrEmpty([string]$binding.entryBodyHash) -or
                    -not [string]::IsNullOrEmpty([string]$binding.expectedSourceSetHash) -or -not [string]::IsNullOrEmpty([string]$binding.expectedEntryBodyHash)) {
                    throw "Legacy refresh plan Index projection contains v2 hashes: $($entry.entry)"
                }
            }
        }
    }

    $findings = @($Plan.findings)
    if ([int]$Plan.findingCount -ne $findings.Count) { throw 'Refresh plan findingCount does not match findings.' }
    foreach ($finding in $findings) {
        foreach ($name in @('code', 'entry', 'entryHash', 'source', 'declaredHash', 'currentHash', 'firstSampleHash', 'snapshotStable', 'declaredContentHash', 'action', 'reason')) {
            Assert-Property $finding $name 'finding'
        }
    }
    $blockerCount = @($findings | Where-Object { $_.action -like 'reject-*' }).Count
    $unstableCount = @($findings | Where-Object { $_.action -like 'wait-for-*-stability' }).Count
    if ([int]$Plan.blockerCount -ne $blockerCount -or [int]$Plan.unstableFindingCount -ne $unstableCount) {
        throw 'Refresh plan blocker or unstable finding count is inconsistent.'
    }
    $expectedStatus = if ($blockerCount -gt 0) { 'blocked' } else { 'ready' }
    if ([string]$Plan.planStatus -cne $expectedStatus) { throw 'Refresh plan status is inconsistent with its blockers.' }
}

function Get-CurrentSourceRefs([string]$Text) {
    $refs = [Collections.Generic.List[object]]::new()
    $issues = [Collections.Generic.List[string]]::new()
    $sections = @([regex]::Matches($Text, $sourceSectionPattern))
    if ($sections.Count -eq 0) {
        $issues.Add('SourceRefs section is missing.')
        return [pscustomobject]@{ refs = @(); issues = @($issues); bodyIndex = -1; bodyLength = 0; body = '' }
    }
    if ($sections.Count -ne 1) {
        $issues.Add("SourceRefs section count changed; found $($sections.Count).")
        return [pscustomobject]@{ refs = @(); issues = @($issues); bodyIndex = -1; bodyLength = 0; body = '' }
    }

    $section = $sections[0]
    $seen = [Collections.Generic.Dictionary[string, bool]]::new([StringComparer]::OrdinalIgnoreCase)
    $lineNumber = 0
    foreach ($line in ($section.Groups['body'].Value -split '\r?\n')) {
        $lineNumber++
        if ($line -notmatch '^\s*[-*+]\s+') { continue }
        $match = [regex]::Match($line, $sourceBulletPattern)
        if (-not $match.Success) {
            $issues.Add("SourceRefs bullet $lineNumber is malformed.")
            continue
        }
        $declaredPath = $match.Groups['path'].Value
        try {
            $sourcePath = Assert-ProjectFile $declaredPath
            $normalizedPath = Get-ProjectRelative $sourcePath
            if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw "SourceRef file is missing: $declaredPath" }
            if ($seen.ContainsKey($normalizedPath)) { throw "Duplicate normalized SourceRef: $normalizedPath" }
            $seen.Add($normalizedPath, $true)
            $refs.Add([pscustomobject]@{
                path = $normalizedPath
                declaredPath = $declaredPath
                declaredHash = $match.Groups['hash'].Value
            })
        }
        catch {
            $issues.Add($_.Exception.Message)
        }
    }
    if ($refs.Count -eq 0) { $issues.Add('SourceRefs contains no valid bullets.') }
    [pscustomobject]@{
        refs = @($refs)
        issues = @($issues)
        bodyIndex = $section.Groups['body'].Index
        bodyLength = $section.Groups['body'].Length
        body = $section.Groups['body'].Value
    }
}

function Update-IndexBindings(
    [string]$Text,
    [string]$KnowledgeRelative,
    [object[]]$ExpectedBindings,
    [string]$HashSchema,
    [string]$NewContentHash,
    [string]$NewSourceSetHash,
    [string]$NewEntryBodyHash
) {
    $blockPattern = '(?ms)^\s{2}-\s+knowledgeId:.*?(?=^\s{2}-\s+knowledgeId:|\z)'
    $blocks = @([regex]::Matches($Text, $blockPattern))
    $escapedFile = [regex]::Escape($KnowledgeRelative)
    $filePattern = '(?m)^\s{4}file:\s*' + $escapedFile + '\s*$'
    $idPattern = '(?m)^\s{2}-\s+knowledgeId:\s*(?<id>[a-z0-9._-]+)\s*$'
    $hashPattern = '(?m)^([ ]{4}contentHash:[ \t]*)[0-9a-f]{64}([ \t]*\r?)$'
    $sourceSetHashPattern = '(?m)^([ ]{4}sourceSetHash:[ \t]*)[0-9a-f]{64}([ \t]*\r?)$'
    $entryBodyHashPattern = '(?m)^([ ]{4}entryBodyHash:[ \t]*)[0-9a-f]{64}([ \t]*\r?)$'
    $currentBindings = Get-IndexBindingIds $Text $KnowledgeRelative
    if ($currentBindings.issues.Count -gt 0) { throw ($currentBindings.issues -join ' | ') }
    if (-not (Test-IndexBindingsEqual $currentBindings.bindings $ExpectedBindings)) {
        throw "KnowledgeIndex binding identity set changed: $KnowledgeRelative"
    }
    $expected = [Collections.Generic.Dictionary[string, bool]]::new([StringComparer]::Ordinal)
    foreach ($binding in @($ExpectedBindings)) { $expected.Add([string]$binding.id, $true) }
    $builder = [Text.StringBuilder]::new()
    $cursor = 0
    $bindingCount = 0
    foreach ($blockMatch in $blocks) {
        [void]$builder.Append($Text.Substring($cursor, $blockMatch.Index - $cursor))
        $block = $blockMatch.Value
        $fileMatches = @([regex]::Matches($block, $filePattern))
        if ($fileMatches.Count -gt 0) {
            if ($fileMatches.Count -ne 1) { throw "KnowledgeIndex block has duplicate file fields: $KnowledgeRelative" }
            $idMatches = @([regex]::Matches($block, $idPattern))
            if ($idMatches.Count -ne 1 -or -not $expected.ContainsKey($idMatches[0].Groups['id'].Value)) {
                throw "KnowledgeIndex block identity does not match the refresh plan: $KnowledgeRelative"
            }
            $hashMatches = @([regex]::Matches($block, $hashPattern))
            if ($hashMatches.Count -ne 1) { throw "KnowledgeIndex block must have exactly one contentHash: $KnowledgeRelative" }
            $block = Replace-First $block $hashPattern "`${1}$NewContentHash`${2}"
            if ($HashSchema -eq 'v2') {
                $sourceSetHashMatches = @([regex]::Matches($block, $sourceSetHashPattern))
                $entryBodyHashMatches = @([regex]::Matches($block, $entryBodyHashPattern))
                if ($sourceSetHashMatches.Count -ne 1 -or $entryBodyHashMatches.Count -ne 1) {
                    throw "KnowledgeIndex v2 block must have exactly one sourceSetHash and entryBodyHash: $KnowledgeRelative"
                }
                $block = Replace-First $block $sourceSetHashPattern "`${1}$NewSourceSetHash`${2}"
                $block = Replace-First $block $entryBodyHashPattern "`${1}$NewEntryBodyHash`${2}"
            }
            $bindingCount++
        }
        [void]$builder.Append($block)
        $cursor = $blockMatch.Index + $blockMatch.Length
    }
    [void]$builder.Append($Text.Substring($cursor))
    [pscustomobject]@{ content = $builder.ToString(); bindingCount = $bindingCount }
}

function Add-Stale([Collections.Generic.List[string]]$List, [string]$Message) {
    if (-not $List.Contains($Message)) { $List.Add($Message) }
}

$planFile = Assert-ProjectFile $PlanPath
if (-not (Test-Path -LiteralPath $planFile -PathType Leaf)) { throw "Refresh plan not found: $PlanPath" }
$output = Assert-OutputPath $OutputPath
$plan = [IO.File]::ReadAllText($planFile, $strictUtf8) | ConvertFrom-Json
Assert-PlanHashShape $plan
$computedPlanHash = Get-PlanHash $plan
if ([string]$plan.planHash -cne $computedPlanHash) { throw 'Refresh plan hash mismatch; regenerate the plan.' }
Assert-PlanShape $plan

$changes = [Collections.Generic.List[object]]::new()
$staleAtApply = [Collections.Generic.List[string]]::new()
$prepared = [Collections.Generic.List[object]]::new()
$entryState = @{}
$indexPath = Assert-ProjectFile 'Documentation/AIKnowledge/KnowledgeIndex.yaml'
if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf)) { throw 'KnowledgeIndex.yaml is missing.' }
$indexRead = Get-StrictTextSnapshot $indexPath
$indexText = [string]$indexRead.text
$indexOriginalHash = [string]$indexRead.hash
$updatedIndex = $indexText
$sharedRouteProjectionAllowed = [regex]::IsMatch($indexText, '(?m)^[ ]{4}sharedRouteProjectionAllowed:[ \t]*true[ \t]*\r?$')
if (-not $indexRead.stable -or [string]$plan.indexHash -cne $indexOriginalHash) {
    Add-Stale $staleAtApply 'plan-index-drift <- Documentation/AIKnowledge/KnowledgeIndex.yaml'
}
foreach ($issue in @(Get-IndexGlobalIdentityIssues $indexText)) {
    Add-Stale $staleAtApply ("plan-index-identity-invalid <- Documentation/AIKnowledge/KnowledgeIndex.yaml: $issue")
}
foreach ($finding in @($plan.findings | Where-Object { $_.action -like 'reject-*' })) {
    Add-Stale $staleAtApply ("plan-blocker $($finding.code) <- $($finding.entry)")
}
foreach ($finding in @($plan.findings | Where-Object { $_.action -like 'wait-for-*-stability' })) {
    Add-Stale $staleAtApply ("plan-unstable $($finding.code) <- $($finding.entry) <- $($finding.source)")
}

$plannedEntryPaths = @($plan.entrySnapshots | ForEach-Object { [string]$_.entry })
$currentEntryPaths = @(Get-CandidateEntryPaths)
foreach ($missing in @($plannedEntryPaths | Where-Object { $currentEntryPaths -cnotcontains $_ })) {
    Add-Stale $staleAtApply ("plan-entry-set-missing <- $missing")
}
foreach ($added in @($currentEntryPaths | Where-Object { $plannedEntryPaths -cnotcontains $_ })) {
    Add-Stale $staleAtApply ("plan-entry-set-added <- $added")
}

# Validate every planned Entry, its complete declared SourceRef set, every source
# file, and its exact KnowledgeIndex binding identities before choosing write targets.
foreach ($snapshot in @($plan.entrySnapshots)) {
    $entryRelative = [string]$snapshot.entry
    if ([string]$snapshot.status -cne 'ready') {
        Add-Stale $staleAtApply ("plan-entry-blocked <- $entryRelative")
        continue
    }
    if (@($snapshot.sourceRefs | Where-Object { $_.snapshotStable -ne $true }).Count -gt 0) {
        Add-Stale $staleAtApply ("plan-entry-source-set-unstable <- $entryRelative")
    }

    $entryPath = Assert-KnowledgeEntry $entryRelative
    if (-not (Test-Path -LiteralPath $entryPath -PathType Leaf)) {
        Add-Stale $staleAtApply ("plan-entry-missing <- $entryRelative")
        continue
    }
    try {
        $entryRead = Get-StrictTextSnapshot $entryPath
    }
    catch {
        Add-Stale $staleAtApply ("plan-entry-read-failed <- ${entryRelative}: $($_.Exception.Message)")
        continue
    }
    if (-not $entryRead.stable -or [string]$snapshot.entryHash -cne [string]$entryRead.hash) {
        Add-Stale $staleAtApply ("plan-entry-drift <- $entryRelative")
    }

    $text = [string]$entryRead.text
    $currentKnowledgeIds = @(Get-MetadataValues $text 'KnowledgeId')
    $currentEntryModes = @(Get-MetadataValues $text 'EntryMode')
    $currentHashSchemas = @(Get-MetadataValues $text 'HashSchema')
    $currentHasHashSchema = [regex]::IsMatch($text, '(?mi)^`?HashSchema`?[ \t]*' + $colonPattern)
    $currentEntryMode = if ($currentEntryModes.Count -eq 0) { 'Canonical' } elseif ($currentEntryModes.Count -eq 1) { [string]$currentEntryModes[0] } else { '' }
    $currentHashSchema = if ($currentHashSchemas.Count -eq 0 -and -not $currentHasHashSchema) { 'legacy' } elseif ($currentHashSchemas.Count -eq 1) { [string]$currentHashSchemas[0] } else { '' }
    if ($currentKnowledgeIds.Count -ne 1 -or [string]$currentKnowledgeIds[0] -cne [string]$snapshot.knowledgeId -or $currentEntryMode -cne [string]$snapshot.entryMode -or $currentHashSchema -cne [string]$snapshot.hashSchema) {
        Add-Stale $staleAtApply ("plan-entry-identity-drift <- $entryRelative")
    }
    $currentContentHashes = @(Get-MetadataValues $text 'ContentHash')
    $currentSourceSetHashes = @(Get-MetadataValues $text 'SourceSetHash')
    $currentEntryBodyHashes = @(Get-MetadataValues $text 'EntryBodyHash')
    if ($currentContentHashes.Count -ne 1 -or [string]$currentContentHashes[0] -cne [string]$snapshot.declaredContentHash) {
        Add-Stale $staleAtApply ("plan-entry-content-hash-drift <- $entryRelative")
    }
    if ($currentHashSchema -eq 'legacy') {
        if ($currentSourceSetHashes.Count -gt 0 -or $currentEntryBodyHashes.Count -gt 0 -or [regex]::IsMatch($text, '(?mi)^`?(?:SourceSetHash|EntryBodyHash)`?[ \t]*' + $colonPattern)) {
            Add-Stale $staleAtApply ("plan-entry-hash-schema-partial <- $entryRelative")
        }
    }
    elseif ($currentHashSchema -eq 'v2') {
        if ($currentSourceSetHashes.Count -ne 1 -or [string]$currentSourceSetHashes[0] -cne [string]$snapshot.declaredSourceSetHash -or
            $currentEntryBodyHashes.Count -ne 1 -or [string]$currentEntryBodyHashes[0] -cne [string]$snapshot.declaredEntryBodyHash) {
            Add-Stale $staleAtApply ("plan-entry-v2-hash-drift <- $entryRelative")
        }
        elseif ([string]$currentEntryBodyHashes[0] -cne (Get-EntryBodyHash $text)) {
            Add-Stale $staleAtApply ("plan-entry-body-hash-invalid <- $entryRelative")
        }
    }
    if ($currentEntryMode -eq 'SharedRouteProjection') {
        $currentProjectionIds = Get-RouteProjectionIds $text
        foreach ($issue in @($currentProjectionIds.issues)) {
            Add-Stale $staleAtApply ("plan-route-projection-invalid <- ${entryRelative}: $issue")
        }
        if (-not $sharedRouteProjectionAllowed) {
            Add-Stale $staleAtApply ("plan-entry-mode-not-allowed <- $entryRelative")
        }
        if (-not (Test-OrdinalSetEqual $currentProjectionIds.ids @($snapshot.indexBindingIds))) {
            Add-Stale $staleAtApply ("plan-route-projection-drift <- $entryRelative")
        }
    }

    $currentSet = Get-CurrentSourceRefs $text
    foreach ($issue in @($currentSet.issues)) {
        Add-Stale $staleAtApply ("plan-source-set-invalid <- ${entryRelative}: $issue")
    }
    if ($currentSet.issues.Count -eq 0) {
        if ((Get-SourceSetHash $currentSet.refs) -cne [string]$snapshot.sourceSetHash -or $currentSet.refs.Count -ne [int]$snapshot.sourceRefCount) {
            Add-Stale $staleAtApply ("plan-source-set-drift <- $entryRelative")
        }
        else {
            $plannedByPath = @{}
            foreach ($planned in @($snapshot.sourceRefs)) { $plannedByPath[[string]$planned.path] = $planned }
            foreach ($current in @($currentSet.refs)) {
                if (-not $plannedByPath.ContainsKey([string]$current.path) -or [string]$plannedByPath[[string]$current.path].declaredHash -cne [string]$current.declaredHash) {
                    Add-Stale $staleAtApply ("plan-source-set-drift <- $entryRelative")
                    break
                }
            }
        }
        if ($currentHashSchema -eq 'v2') {
            $declaredSetHash = Get-ContentHash @($currentSet.refs | ForEach-Object { [string]$_.declaredHash })
            if ($declaredSetHash -cne [string]$snapshot.declaredSourceSetHash -or [string]$snapshot.declaredContentHash -cne [string]$snapshot.declaredSourceSetHash) {
                Add-Stale $staleAtApply ("plan-entry-v2-source-set-hash-invalid <- $entryRelative")
            }
        }
    }
    foreach ($planned in @($snapshot.sourceRefs)) {
        $plannedSource = Assert-ProjectFile ([string]$planned.path)
        if (-not (Test-Path -LiteralPath $plannedSource -PathType Leaf) -or (Get-SourceHash $plannedSource) -cne [string]$planned.currentHash) {
            Add-Stale $staleAtApply ("plan-source-drift <- $entryRelative <- $($planned.path)")
        }
    }

    $knowledgeRelative = $entryRelative.Substring('Documentation/AIKnowledge/'.Length)
    $currentBindings = Get-IndexBindingIds $indexText $knowledgeRelative
    foreach ($issue in @($currentBindings.issues)) {
        Add-Stale $staleAtApply ("plan-index-binding-invalid <- ${entryRelative}: $issue")
    }
    if (-not (Test-OrdinalSetEqual $currentBindings.ids @($snapshot.indexBindingIds)) -or -not (Test-IndexBindingsEqual $currentBindings.bindings @($snapshot.indexBindings))) {
        Add-Stale $staleAtApply ("plan-index-binding-drift <- $entryRelative")
    }
    $entryState[$entryRelative] = [pscustomobject]@{ path = $entryPath; read = $entryRead; sourceSet = $currentSet }
}

$entryTargets = @($plan.entrySnapshots | Where-Object {
    $_.status -eq 'ready' -and
    @($_.sourceRefs | Where-Object { $_.snapshotStable -eq $true -and $_.currentHash -cne $_.declaredHash }).Count -gt 0
})

# Prepare the complete write batch from the same byte snapshots validated above.
foreach ($snapshot in $entryTargets) {
    if ($staleAtApply.Count -gt 0) { break }
    $entryRelative = [string]$snapshot.entry
    $entryStaleStart = $staleAtApply.Count
    if (-not $entryState.ContainsKey($entryRelative)) {
        Add-Stale $staleAtApply ("plan-entry-state-missing <- $entryRelative")
        continue
    }
    $state = $entryState[$entryRelative]
    $entryPath = [string]$state.path
    $text = [string]$state.read.text
    $currentSet = $state.sourceSet
    if ($staleAtApply.Count -gt $entryStaleStart) { continue }

    $contentHashFieldPattern = '(?m)^(`ContentHash`\s*' + $colonPattern + '\s*`)[0-9a-f]{64}(`\s*$)'
    $sourceSetHashFieldPattern = '(?m)^(`SourceSetHash`\s*' + $colonPattern + '\s*`)[0-9a-f]{64}(`\s*$)'
    $entryBodyHashFieldPattern = '(?m)^(`EntryBodyHash`\s*' + $colonPattern + '\s*`)[0-9a-f]{64}(`\s*$)'
    $contentHashMatches = @([regex]::Matches($text, $contentHashFieldPattern))
    if ($contentHashMatches.Count -ne 1) {
        Add-Stale $staleAtApply ("plan-content-hash-field-invalid <- $entryRelative")
        continue
    }
    $sourceSetHashMatches = @([regex]::Matches($text, $sourceSetHashFieldPattern))
    $entryBodyHashMatches = @([regex]::Matches($text, $entryBodyHashFieldPattern))
    if ([string]$snapshot.hashSchema -eq 'v2' -and ($sourceSetHashMatches.Count -ne 1 -or $entryBodyHashMatches.Count -ne 1)) {
        Add-Stale $staleAtApply ("plan-v2-hash-fields-invalid <- $entryRelative")
        continue
    }

    $plannedByPath = @{}
    foreach ($planned in @($snapshot.sourceRefs)) { $plannedByPath[[string]$planned.path] = $planned }
    $sourceEvaluator = [Text.RegularExpressions.MatchEvaluator]{
        param($match)
        $declaredPath = $match.Groups['path'].Value
        $normalizedPath = Get-ProjectRelative (Assert-ProjectFile $declaredPath)
        $planned = $plannedByPath[$normalizedPath]
        '- `' + $declaredPath + '` (`' + [string]$planned.currentHash + '`)'
    }
    $updatedSourceBody = ([regex]::new($sourceBulletPattern, [Text.RegularExpressions.RegexOptions]::Multiline)).Replace([string]$currentSet.body, $sourceEvaluator)
    $updated = $text.Substring(0, [int]$currentSet.bodyIndex) + $updatedSourceBody + $text.Substring([int]$currentSet.bodyIndex + [int]$currentSet.bodyLength)
    $calculatedContentHash = Get-ContentHash @($snapshot.sourceRefs | ForEach-Object { [string]$_.currentHash })
    if ($calculatedContentHash -cne [string]$snapshot.expectedContentHash) {
        Add-Stale $staleAtApply ("plan-expected-content-hash-mismatch <- $entryRelative")
        continue
    }
    $newContentHash = [string]$snapshot.expectedContentHash
    $updated = Replace-First $updated $contentHashFieldPattern "`${1}$newContentHash`${2}"
    $newSourceSetHash = ''
    $newEntryBodyHash = ''
    if ([string]$snapshot.hashSchema -eq 'v2') {
        $newSourceSetHash = [string]$snapshot.expectedSourceSetHash
        if ($newSourceSetHash -cne $newContentHash) {
            Add-Stale $staleAtApply ("plan-expected-source-set-hash-mismatch <- $entryRelative")
            continue
        }
        $updated = Replace-First $updated $sourceSetHashFieldPattern "`${1}$newSourceSetHash`${2}"
        $calculatedEntryBodyHash = Get-EntryBodyHash $updated
        if ($calculatedEntryBodyHash -cne [string]$snapshot.expectedEntryBodyHash) {
            Add-Stale $staleAtApply ("plan-expected-entry-body-hash-mismatch <- $entryRelative")
            continue
        }
        $newEntryBodyHash = [string]$snapshot.expectedEntryBodyHash
        $updated = Replace-First $updated $entryBodyHashFieldPattern "`${1}$newEntryBodyHash`${2}"
        if ((Get-EntryBodyHash $updated) -cne $newEntryBodyHash) {
            Add-Stale $staleAtApply ("plan-entry-body-hash-did-not-converge <- $entryRelative")
            continue
        }
    }
    if ($updated -ceq $text) { continue }

    $knowledgeRelative = $entryRelative.Substring('Documentation/AIKnowledge/'.Length)
    try {
        $indexUpdate = Update-IndexBindings -Text $updatedIndex -KnowledgeRelative $knowledgeRelative -ExpectedBindings @($snapshot.indexBindings) -HashSchema ([string]$snapshot.hashSchema) -NewContentHash $newContentHash -NewSourceSetHash $newSourceSetHash -NewEntryBodyHash $newEntryBodyHash
        if ($indexUpdate.bindingCount -ne [int]$snapshot.indexBindingCount) { throw "KnowledgeIndex binding count changed for: $entryRelative" }
        $updatedIndex = $indexUpdate.content
    }
    catch {
        Add-Stale $staleAtApply ("plan-index-binding-invalid <- ${entryRelative}: $($_.Exception.Message)")
        continue
    }
    $prepared.Add([pscustomobject]@{ path = $entryPath; relative = $entryRelative; content = $updated; originalHash = [string]$state.read.hash })
    $changes.Add([pscustomobject]@{
        entry = $entryRelative
        hashSchema = [string]$snapshot.hashSchema
        contentHash = $newContentHash
        sourceSetHash = $newSourceSetHash
        entryBodyHash = $newEntryBodyHash
        sourceCount = $snapshot.sourceRefCount
        applied = $false
    })
}

function Invoke-FinalCas {
    # ESKNOWLEDGE_FINAL_CAS_BARRIER
    $precommitEntryPaths = @(Get-CandidateEntryPaths)
    if (-not (Test-OrdinalSetEqual $plannedEntryPaths $precommitEntryPaths)) {
        Add-Stale $staleAtApply 'precommit-entry-set-drift <- Documentation/AIKnowledge'
    }
    foreach ($snapshot in @($plan.entrySnapshots | Where-Object { $_.status -eq 'ready' })) {
        if ($entryState.ContainsKey([string]$snapshot.entry)) {
            $state = $entryState[[string]$snapshot.entry]
            if (-not (Test-Path -LiteralPath $state.path -PathType Leaf)) {
                Add-Stale $staleAtApply ("precommit-entry-drift <- $($snapshot.entry)")
            }
            else {
                try {
                    $precommitEntry = Get-StrictTextSnapshot $state.path
                    if (-not $precommitEntry.stable -or [string]$precommitEntry.hash -cne [string]$state.read.hash) {
                        Add-Stale $staleAtApply ("precommit-entry-drift <- $($snapshot.entry)")
                    }
                }
                catch {
                    Add-Stale $staleAtApply ("precommit-entry-read-failed <- $($snapshot.entry): $($_.Exception.Message)")
                }
            }
        }
        foreach ($planned in @($snapshot.sourceRefs)) {
            $sourcePath = Assert-ProjectFile ([string]$planned.path)
            if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf) -or (Get-SourceHash $sourcePath) -cne [string]$planned.currentHash) {
                Add-Stale $staleAtApply ("precommit-source-drift <- $($snapshot.entry) <- $($planned.path)")
            }
        }
    }
    $precommitIndex = Get-StrictTextSnapshot $indexPath
    if (-not $precommitIndex.stable -or [string]$precommitIndex.hash -cne $indexOriginalHash) {
        Add-Stale $staleAtApply 'precommit-index-drift <- Documentation/AIKnowledge/KnowledgeIndex.yaml'
    }
}

$shouldApply = $false
if ($Apply -and $staleAtApply.Count -eq 0 -and $prepared.Count -gt 0) {
    $shouldApply = $PSCmdlet.ShouldProcess("$($prepared.Count) Knowledge entries and KnowledgeIndex", 'Apply locked stable SourceRef refresh batch')
}
if (-not $Apply -and $staleAtApply.Count -eq 0 -and $prepared.Count -gt 0) {
    Invoke-FinalCas
}

$applied = $false
$transactionExecuted = $false
if ($shouldApply -and $staleAtApply.Count -eq 0 -and $prepared.Count -gt 0) {
    $prepared.Add([pscustomobject]@{ path = $indexPath; relative = 'Documentation/AIKnowledge/KnowledgeIndex.yaml'; content = $updatedIndex; originalHash = $indexOriginalHash })
    $lockRelative = 'ES/Output/KnowledgeValidation/stable-refresh.lock'
    $lockPath = Assert-OutputPath $lockRelative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $lockPath) | Out-Null
    $lockStream = $null
    try {
        $lockStream = [IO.FileStream]::new($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    }
    catch {
        throw ('Stable refresh lock unavailable for this project: ' + $lockRelative + '. ' + $_.Exception.Message)
    }
    $transactionExecuted = $true
    # ESKNOWLEDGE_LOCK_ACQUIRED_BARRIER

    $transactionId = [Guid]::NewGuid().ToString('N')
    $staged = [Collections.Generic.List[object]]::new()
    $rollbackCandidates = [Collections.Generic.List[object]]::new()
    $rollbackFailures = [Collections.Generic.List[string]]::new()
    $cleanupFailures = [Collections.Generic.List[string]]::new()
    $preserveBackups = $false
    $transactionError = $null
    try {
        foreach ($item in $prepared) {
            $temporary = "$($item.path).tmp-$transactionId"
            $backup = "$($item.path).bak-$transactionId"
            $restoreTemporary = "$($item.path).restore-$transactionId"
            [IO.File]::WriteAllText($temporary, [string]$item.content, $strictUtf8)
            $expectedHash = Get-Sha256 ([string]$item.content)
            if ((Get-Hash $temporary) -cne $expectedHash) { throw "Stable refresh staging hash mismatch: $($item.relative)" }
            $staged.Add([pscustomobject]@{
                path = [string]$item.path
                relative = [string]$item.relative
                temporary = $temporary
                backup = $backup
                restoreTemporary = $restoreTemporary
                originalHash = [string]$item.originalHash
                expectedHash = $expectedHash
                replaced = $false
            })
        }

        Invoke-FinalCas
        if ($staleAtApply.Count -eq 0) {
            # ESKNOWLEDGE_POST_FINAL_CAS_BARRIER
        }

        $commitOrdinal = 0
        foreach ($item in @($staged | Where-Object { $staleAtApply.Count -eq 0 })) {
            if (-not (Test-Path -LiteralPath $item.path -PathType Leaf) -or (Get-Hash $item.path) -cne [string]$item.originalHash) {
                throw "Stable refresh commit CAS failed: $($item.relative)"
            }
            $commitOrdinal++
            # ESKNOWLEDGE_BEFORE_COMMIT_REPLACE
            [IO.File]::Replace($item.temporary, $item.path, $item.backup, $true)
            $item.replaced = $true
            $rollbackCandidates.Add($item)
            if (-not (Test-Path -LiteralPath $item.backup -PathType Leaf)) { throw "Stable refresh commit backup is missing: $($item.relative)" }
            if ((Get-Hash $item.backup) -cne [string]$item.originalHash) { throw "Stable refresh backup hash mismatch: $($item.relative)" }
            if ((Get-Hash $item.path) -cne [string]$item.expectedHash) { throw "Stable refresh committed hash mismatch: $($item.relative)" }
        }
        if ($staleAtApply.Count -eq 0) {
            $applied = $true
            foreach ($change in $changes) { $change.applied = $true }
        }
    }
    catch {
        $transactionError = $_.Exception
        # ESKNOWLEDGE_ROLLBACK_BATCH_BARRIER
        for ($rollbackIndex = $rollbackCandidates.Count - 1; $rollbackIndex -ge 0; $rollbackIndex--) {
            $item = $rollbackCandidates[$rollbackIndex]
            try {
                # ESKNOWLEDGE_BEFORE_ROLLBACK_RESTORE
                if (-not (Test-Path -LiteralPath $item.backup -PathType Leaf)) { throw "Rollback backup is missing: $($item.backup)" }
                if ((Get-Hash $item.backup) -cne [string]$item.originalHash) { throw "Rollback backup hash mismatch: $($item.relative)" }
                [IO.File]::Copy($item.backup, $item.restoreTemporary, $false)
                if ((Get-Hash $item.restoreTemporary) -cne [string]$item.originalHash) { throw "Rollback restore-stage hash mismatch: $($item.relative)" }
                [IO.File]::Replace($item.restoreTemporary, $item.path, $item.temporary, $true)
                # ESKNOWLEDGE_AFTER_ROLLBACK_RESTORE_BARRIER
                if ((Get-Hash $item.path) -cne [string]$item.originalHash) { throw "Rollback hash mismatch: $($item.relative)" }
            }
            catch {
                $rollbackFailures.Add($item.backup + ' -> ' + $item.path + ': ' + $_.Exception.Message)
            }
        }
        if ($rollbackFailures.Count -gt 0) {
            $preserveBackups = $true
        }
    }

    foreach ($item in $staged) {
        foreach ($artifact in @(
            [pscustomobject]@{ kind = 'temporary'; path = [string]$item.temporary },
            [pscustomobject]@{ kind = 'restore'; path = [string]$item.restoreTemporary },
            [pscustomobject]@{ kind = 'backup'; path = [string]$item.backup }
        )) {
            if ($artifact.kind -eq 'backup' -and $preserveBackups) { continue }
            try {
                # ESKNOWLEDGE_BEFORE_CLEANUP_ARTIFACT
                [IO.File]::Delete([string]$artifact.path)
            }
            catch {
                $cleanupFailures.Add($artifact.kind + ' ' + $artifact.path + ': ' + $_.Exception.Message)
            }
        }
    }

    try { $lockStream.Dispose() }
    catch { $cleanupFailures.Add($lockRelative + ' release: ' + $_.Exception.Message) }

    if ($null -ne $transactionError) {
        $message = 'Stable refresh commit failed: ' + $transactionError.Message
        if ($rollbackFailures.Count -gt 0) { $message += '; rollback is incomplete and backups were preserved: ' + ($rollbackFailures -join ' | ') }
        else { $message += '; rollback completed.' }
        if ($cleanupFailures.Count -gt 0) { $message += '; cleanup failures: ' + ($cleanupFailures -join ' | ') }
        throw $message
    }
    if ($cleanupFailures.Count -gt 0) {
        throw ('Stable refresh transaction cleanup failed: ' + ($cleanupFailures -join ' | '))
    }
}

$transactionMode = if ($transactionExecuted) {
    'locked-exception-rollback'
}
elseif (-not $Apply) {
    'preview-no-transaction'
}
elseif ($prepared.Count -eq 0 -and $staleAtApply.Count -eq 0) {
    'apply-no-changes'
}
else {
    'apply-not-executed'
}
$receipt = [ordered]@{
    schemaVersion = 1
    toolId = 'es-knowledge-stable-refresh'
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    mutatesSources = $false
    mutatesKnowledge = $applied
    mode = if ($Apply) { 'apply-stable-only' } else { 'preview' }
    sourcePlan = $PlanPath
    planHash = [string]$plan.planHash
    transactionExecuted = $transactionExecuted
    atomicBatch = $transactionExecuted
    transactionMode = $transactionMode
    crashSafe = $false
    applied = $applied
    targetEntryCount = $entryTargets.Count
    staleAtApplyCount = $staleAtApply.Count
    staleAtApply = @($staleAtApply)
    changeCount = $changes.Count
    changes = @($changes)
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $output) | Out-Null
[IO.File]::WriteAllText($output, ($receipt | ConvertTo-Json -Depth 10), $strictUtf8)
$receipt | ConvertTo-Json -Depth 10
if ($staleAtApply.Count -gt 0) { exit 2 }
