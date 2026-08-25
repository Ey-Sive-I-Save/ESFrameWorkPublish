[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [ValidateSet('Entry', 'Index', 'All')][string]$Mode = 'Entry',
    [string]$EntryPath,
    [string]$ReportPath
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
$knowledgeRoot = Join-Path $root 'Documentation\AIKnowledge'
$indexPath = Join-Path $knowledgeRoot 'KnowledgeIndex.yaml'
$strictUtf8 = [Text.UTF8Encoding]::new($false, $true)
$findings = [Collections.Generic.List[object]]::new()
$inputFiles = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
$sharedRouteProjectionAllowed = $false

function Add-Finding([string]$Code, [string]$Path, [string]$Message) {
    $findings.Add([pscustomobject]@{ code = $Code; path = $Path.Replace('\', '/'); message = $Message })
}

function Get-ProjectRelative([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $prefix = $root.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { return $null }
    return $full.Substring($prefix.Length).Replace('\', '/')
}

function Test-ReparsePointPath([string]$TargetPath) {
    $rootFull = $root.TrimEnd('\', '/')
    $targetFull = [IO.Path]::GetFullPath($TargetPath)
    if (-not $targetFull.StartsWith($rootFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { return $true }
    $relative = $targetFull.Substring($rootFull.Length).TrimStart([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar))
    $current = $rootFull
    foreach ($segment in $relative.Split(@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar), [StringSplitOptions]::RemoveEmptyEntries)) {
        $current = Join-Path $current $segment
        if (-not (Test-Path -LiteralPath $current)) { break }
        if (((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { return $true }
    }
    return $false
}

function Resolve-ContainedFile([string]$RelativePath, [string]$BasePath, [string]$ScopeName) {
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath)) {
        Add-Finding 'PATH_EXPANSION_DENIED' $RelativePath "$ScopeName path must be project-relative"
        return $null
    }
    $baseFull = [IO.Path]::GetFullPath($BasePath).TrimEnd('\', '/')
    $candidate = [IO.Path]::GetFullPath((Join-Path $baseFull $RelativePath))
    $prefix = $baseFull + [IO.Path]::DirectorySeparatorChar
    if (-not $candidate.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        Add-Finding 'PATH_EXPANSION_DENIED' $RelativePath "$ScopeName path escapes its allowed root"
        return $null
    }
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        Add-Finding 'PATH_FILE_MISSING' $RelativePath "$ScopeName file does not exist"
        return $null
    }
    if (Test-ReparsePointPath $candidate) {
        Add-Finding 'PATH_REPARSE_DENIED' $RelativePath "$ScopeName path contains a reparse point"
        return $null
    }
    return $candidate
}

function Read-StrictText([string]$Path, [string]$DisplayPath) {
    try {
        $bytes = [IO.File]::ReadAllBytes($Path)
        $text = $strictUtf8.GetString($bytes)
        $relative = Get-ProjectRelative $Path
        if ($relative) {
            $inputFiles[$relative] = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
        }
        return $text
    } catch {
        Add-Finding 'UTF8_INVALID' $DisplayPath 'file is not strict UTF-8 or could not be read'
        return $null
    }
}

function Get-ScalarMatches([string]$Text, [string]$Field, [int]$Indent) {
    $spaces = ' ' * $Indent
    $pattern = '(?m)^' + [regex]::Escape($spaces + $Field + ':') + '\s*(.*?)\s*$'
    return @([regex]::Matches($Text, $pattern) | ForEach-Object { $_.Groups[1].Value.Trim().Trim([char]39, [char]34) })
}

function Get-InlineList([string]$Value) {
    $trimmed = $Value.Trim()
    if ($trimmed -notmatch '^\[(.*)\]$') { return @() }
    return @($Matches[1].Split(',') | ForEach-Object { $_.Trim().Trim([char]39, [char]34) } | Where-Object { $_ })
}

function Get-Sha256Text([string]$Text) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text)))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function Get-SourceRefHash([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $extension = [IO.Path]::GetExtension($Path).ToLowerInvariant()
    $textExtensions = @('.cs','.csproj','.md','.json','.yaml','.yml','.ps1','.py','.txt','.asmdef','.asset','.meta')
    if ($extension -in $textExtensions) {
        $text = [Text.UTF8Encoding]::new($false, $true).GetString($bytes)
        $text = $text -replace "`r`n", "`n"
        $text = $text -replace "`r", "`n"
        return Get-Sha256Text $text
    }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function Get-EntryBodyHash([string]$Text) {
    $normalized = $Text.Replace("`r`n", "`n").Replace("`r", "`n")
    $lines = [Collections.Generic.List[string]]::new()
    foreach ($line in $normalized.Split([char]10)) {
        if ($line -match '(?i)^\s*`EntryBodyHash`\s*[:\uFF1A]\s*`[^`]*`\s*$') { continue }
        $lines.Add($line.TrimEnd([char[]]@(' ', "`t")))
    }
    while ($lines.Count -gt 0 -and $lines[$lines.Count - 1].Length -eq 0) { $lines.RemoveAt($lines.Count - 1) }
    return Get-Sha256Text (($lines -join "`n") + "`n")
}

function Compare-RouteSets([string[]]$EntryRoutes, [string[]]$IndexRoutes, [string]$Path, [string]$BindingId) {
    $entrySet = @($EntryRoutes | Sort-Object -CaseSensitive -Unique)
    $indexSet = @($IndexRoutes | Sort-Object -CaseSensitive -Unique)
    $missingFromEntry = @($indexSet | Where-Object { $_ -cnotin $entrySet })
    $missingFromIndex = @($entrySet | Where-Object { $_ -cnotin $indexSet })
    if ($missingFromEntry.Count -gt 0 -or $missingFromIndex.Count -gt 0) {
        $findings.Add([pscustomobject]@{
            code = 'ROUTE_SET_MISMATCH'
            path = $Path.Replace('\', '/')
            message = "routeKeys differ for binding: $BindingId"
            binding = $BindingId
            missingFromEntry = @($missingFromEntry)
            missingFromIndex = @($missingFromIndex)
        })
    }
}

function Get-RouteProjections([string]$Text) {
    $section = [regex]::Match($Text, '(?ms)^##\s+RouteProjections\s*$\r?\n(?<body>.*?)(?=^##\s+|\Z)')
    if (-not $section.Success) { return @() }
    return @([regex]::Matches($section.Groups['body'].Value, '(?m)^-\s+`(?<id>[^`]+)`\s*[:\uFF1A]\s*(?<routes>.+?)\s*$') | ForEach-Object {
        [pscustomobject]@{ knowledgeId = $_.Groups['id'].Value; routeKeys = @(Get-EntryRouteKeys $_.Groups['routes'].Value) }
    })
}

function Get-RequiredReads([string]$Block) {
    $inline = [regex]::Match($Block, '(?m)^\s{4}requiredReads:\s*(?<value>\[[^\r\n]*\])\s*$')
    if ($inline.Success) { return @(Get-InlineList $inline.Groups['value'].Value) }
    $match = [regex]::Match($Block, '(?ms)^\s{4}requiredReads:\s*\r?\n(?<items>(?:\s{6}-\s*.*(?:\r?\n|$))*)')
    if (-not $match.Success) { return @() }
    return @([regex]::Matches($match.Groups['items'].Value, '(?m)^\s{6}-\s*(.+?)\s*$') | ForEach-Object { $_.Groups[1].Value.Trim().Trim([char]39, [char]34) })
}

function Get-EntryField([string]$Text, [string]$Field) {
    if ($Field -ne 'RouteKeys') {
        $inlinePattern = '`' + [regex]::Escape($Field) + '`\s*:\s*`([^`]+)`'
        $inlineMatches = @([regex]::Matches($Text, $inlinePattern, [Text.RegularExpressions.RegexOptions]::IgnoreCase) | ForEach-Object { $_.Groups[1].Value.Trim() })
        if ($inlineMatches.Count -gt 0) { return $inlineMatches }
        if ($Field -eq 'StaleWhen') {
            $plainMatches = @([regex]::Matches($Text, '(?mi)`StaleWhen`\s*:\s*([^`\r\n].*?)\s*$') | ForEach-Object { $_.Groups[1].Value.Trim() })
            if ($plainMatches.Count -gt 0) { return $plainMatches }
        }
    }
    $pattern = '(?mi)^`?' + [regex]::Escape($Field) + '`?\s*:\s*(.+?)\s*$'
    return @([regex]::Matches($Text, $pattern) | ForEach-Object { $_.Groups[1].Value.Trim() })
}

function Get-EntryRouteKeys([string]$Value) {
    $tokens = @([regex]::Matches($Value, '`([^`]+)`') | ForEach-Object { $_.Groups[1].Value })
    if ($tokens.Count -gt 0) {
        return @($tokens | ForEach-Object { $_.Split(',') | ForEach-Object { $_.Trim() } } | Where-Object { $_ })
    }
    return @($Value.Trim(' ', '[', ']').Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}

function Parse-Index([string]$Text, [bool]$ReportStructure) {
    if ($Text -notmatch '(?m)^entries:\s*$') { Add-Finding 'INDEX_ENTRIES_MISSING' 'Documentation/AIKnowledge/KnowledgeIndex.yaml' 'entries collection is missing' }
    $script:sharedRouteProjectionAllowed = $Text -match '(?m)^\s{4}sharedRouteProjectionAllowed:\s*true\s*$'
    $blocks = @([regex]::Matches($Text, '(?ms)^\s{2}-\s+knowledgeId:.*?(?=^\s{2}-\s+knowledgeId:|\Z)') | ForEach-Object { $_.Value })
    $parsed = [Collections.Generic.List[object]]::new()
    $requiredScalars = @('file', 'topic', 'routeKeys', 'relatedSkills', 'authority', 'evidenceLevel', 'contentHash', 'staleWhen')
    foreach ($block in $blocks) {
        $values = @{}
        $structureIssues = [Collections.Generic.List[string]]::new()
        $idMatches = @([regex]::Matches($block, '(?m)^\s{2}-\s+knowledgeId:\s*(.+?)\s*$') | ForEach-Object { $_.Groups[1].Value.Trim().Trim([char]39, [char]34) })
        if ($idMatches.Count -ne 1) {
            $structureIssues.Add("knowledgeId must occur exactly once; found $($idMatches.Count)")
        }
        $values.knowledgeId = if ($idMatches.Count -gt 0) { $idMatches[0] } else { '' }
        foreach ($field in $requiredScalars) {
            $matches = @(Get-ScalarMatches $block $field 4)
            if ($matches.Count -ne 1) {
                $structureIssues.Add("$field must occur exactly once; found $($matches.Count)")
            }
            $values[$field] = if ($matches.Count -gt 0) { $matches[0] } else { '' }
        }
        $hashSchemaMatches = @(Get-ScalarMatches $block 'hashSchema' 4)
        $sourceSetHashMatches = @(Get-ScalarMatches $block 'sourceSetHash' 4)
        $entryBodyHashMatches = @(Get-ScalarMatches $block 'entryBodyHash' 4)
        if ($hashSchemaMatches.Count -gt 1) { $structureIssues.Add("hashSchema must occur at most once; found $($hashSchemaMatches.Count)") }
        $hashSchema = if ($hashSchemaMatches.Count -eq 1) { $hashSchemaMatches[0] } else { 'legacy' }
        if ($hashSchema -notin @('legacy', 'v2')) { $structureIssues.Add("unsupported hashSchema: $hashSchema") }
        if ($hashSchema -eq 'v2') {
            if ($sourceSetHashMatches.Count -ne 1) { $structureIssues.Add("sourceSetHash must occur exactly once for v2; found $($sourceSetHashMatches.Count)") }
            if ($entryBodyHashMatches.Count -ne 1) { $structureIssues.Add("entryBodyHash must occur exactly once for v2; found $($entryBodyHashMatches.Count)") }
            if ($sourceSetHashMatches.Count -eq 1 -and $sourceSetHashMatches[0] -notmatch '^[0-9a-f]{64}$') { $structureIssues.Add('sourceSetHash must be lowercase SHA-256') }
            if ($entryBodyHashMatches.Count -eq 1 -and $entryBodyHashMatches[0] -notmatch '^[0-9a-f]{64}$') { $structureIssues.Add('entryBodyHash must be lowercase SHA-256') }
        } elseif ($sourceSetHashMatches.Count -gt 0 -or $entryBodyHashMatches.Count -gt 0) {
            $structureIssues.Add('sourceSetHash and entryBodyHash require hashSchema: v2')
        }
        if ($values.contentHash -notmatch '^[0-9a-f]{64}$') { $structureIssues.Add('contentHash must be lowercase SHA-256') }
        $requiredReadHeadings = @([regex]::Matches($block, '(?m)^\s{4}requiredReads:\s*(?:\[[^\r\n]*\])?\s*$'))
        if ($requiredReadHeadings.Count -ne 1) {
            $structureIssues.Add("requiredReads must occur exactly once; found $($requiredReadHeadings.Count)")
        }
        $routeKeys = Get-InlineList $values.routeKeys
        $relatedSkills = Get-InlineList $values.relatedSkills
        if ($routeKeys.Count -eq 0) { $structureIssues.Add('routeKeys must be a non-empty inline list') }
        if ($relatedSkills.Count -eq 0) { $structureIssues.Add('relatedSkills must be a non-empty inline list') }
        if ($ReportStructure) {
            foreach ($issue in $structureIssues) { Add-Finding 'INDEX_FIELD_COUNT' 'Documentation/AIKnowledge/KnowledgeIndex.yaml' $issue }
        }
        $parsed.Add([pscustomobject]@{
            knowledgeId = $values.knowledgeId
            file = $values.file
            contentHash = $values.contentHash
            hashSchema = $hashSchema
            sourceSetHash = if ($sourceSetHashMatches.Count -gt 0) { $sourceSetHashMatches[0] } else { '' }
            entryBodyHash = if ($entryBodyHashMatches.Count -gt 0) { $entryBodyHashMatches[0] } else { '' }
            routeKeys = @($routeKeys)
            relatedSkills = @($relatedSkills)
            requiredReads = @(Get-RequiredReads $block)
            structureIssues = @($structureIssues)
        })
    }
    if ($ReportStructure) {
        foreach ($group in @($parsed | Group-Object knowledgeId | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Name) -and $_.Count -gt 1 })) {
            Add-Finding 'INDEX_DUPLICATE_ID' 'Documentation/AIKnowledge/KnowledgeIndex.yaml' "duplicate knowledgeId: $($group.Name)"
        }
    }
    return @($parsed | Sort-Object knowledgeId, file)
}

function Test-IndexEntry([object]$Item, [switch]$SkipEntryHash) {
    $entryFile = Resolve-ContainedFile $Item.file $knowledgeRoot 'Knowledge entry'
    if ($entryFile -and -not $SkipEntryHash) {
        $entryText = Read-StrictText $entryFile ('Documentation/AIKnowledge/' + $Item.file)
        if ($null -ne $entryText) {
            $declared = @(Get-EntryField $entryText 'ContentHash')
            if ($declared.Count -ne 1 -or $declared[0].Trim('`') -cne $Item.contentHash) {
                Add-Finding 'CONTENT_HASH_INDEX_MISMATCH' $Item.file 'index contentHash does not match the entry declaration'
            }
            $entryHashSchema = @(Get-EntryField $entryText 'HashSchema')
            $effectiveEntryHashSchema = if ($entryHashSchema.Count -eq 1) { $entryHashSchema[0].Trim('`') } else { 'legacy' }
            if ($effectiveEntryHashSchema -cne $Item.hashSchema) {
                Add-Finding 'INDEX_HASH_SCHEMA_MISMATCH' $Item.file "entry hash schema differs from index binding: $($Item.knowledgeId)"
            }
            if ($Item.hashSchema -eq 'v2') {
                $declaredSourceSetHash = @(Get-EntryField $entryText 'SourceSetHash')
                $declaredEntryBodyHash = @(Get-EntryField $entryText 'EntryBodyHash')
                if ($declaredSourceSetHash.Count -ne 1 -or $declaredSourceSetHash[0].Trim('`') -cne $Item.sourceSetHash) {
                    Add-Finding 'SOURCE_SET_HASH_INDEX_MISMATCH' $Item.file "entry SourceSetHash differs from index binding: $($Item.knowledgeId)"
                }
                if ($declaredEntryBodyHash.Count -ne 1 -or $declaredEntryBodyHash[0].Trim('`') -cne $Item.entryBodyHash) {
                    Add-Finding 'ENTRY_BODY_HASH_INDEX_MISMATCH' $Item.file "entry EntryBodyHash differs from index binding: $($Item.knowledgeId)"
                }
                if ($declaredEntryBodyHash.Count -eq 1 -and $declaredEntryBodyHash[0].Trim('`') -cne (Get-EntryBodyHash $entryText)) {
                    Add-Finding 'ENTRY_BODY_HASH_MISMATCH' $Item.file 'EntryBodyHash does not match the normalized Knowledge body'
                }
            }
            if ($Mode -eq 'Index') {
                $sameFileBindings = @($indexItems | Where-Object { $_.file -ceq $Item.file } | Sort-Object knowledgeId)
                if ($sameFileBindings.Count -gt 0 -and $Item.knowledgeId -ceq $sameFileBindings[0].knowledgeId) {
                    $entryKnowledgeId = @(Get-EntryField $entryText 'KnowledgeId')
                    $entryRouteField = @(Get-EntryField $entryText 'RouteKeys')
                    $entryRoutes = if ($entryRouteField.Count -eq 1) { @(Get-EntryRouteKeys $entryRouteField[0]) } else { @() }
                    $sharedProjection = $entryText -match '(?m)^`EntryMode`\s*[:\uFF1A]\s*`SharedRouteProjection`\s*$'
                    if (-not $sharedProjection) {
                        $canonicalBindings = @($sameFileBindings | Where-Object { $entryKnowledgeId.Count -eq 1 -and $_.knowledgeId -ceq $entryKnowledgeId[0].Trim('`') })
                        if ($sameFileBindings.Count -ne 1 -or $canonicalBindings.Count -ne 1) {
                            Add-Finding 'INDEX_BINDING_COUNT' $Item.file "entry must have exactly one file and canonical KnowledgeId binding; found file=$($sameFileBindings.Count), canonical=$($canonicalBindings.Count)"
                        } elseif ($entryRouteField.Count -eq 1) {
                            Compare-RouteSets $entryRoutes $sameFileBindings[0].routeKeys $Item.file $sameFileBindings[0].knowledgeId
                        }
                    } else {
                        if (-not $sharedRouteProjectionAllowed) { Add-Finding 'ENTRY_MODE_NOT_ALLOWED' $Item.file 'KnowledgeIndex does not enable SharedRouteProjection' }
                        $routeProjections = @(Get-RouteProjections $entryText)
                        if ($routeProjections.Count -eq 0) { Add-Finding 'ROUTE_PROJECTION_DECLARATION_MISSING' $Item.file 'SharedRouteProjection requires a RouteProjections section' }
                        foreach ($binding in $sameFileBindings) {
                            $projection = @($routeProjections | Where-Object { $_.knowledgeId -ceq $binding.knowledgeId })
                            if ($projection.Count -ne 1) { Add-Finding 'ROUTE_PROJECTION_BINDING_MISSING' $Item.file "binding requires exactly one declared projection: $($binding.knowledgeId)" }
                            else { Compare-RouteSets $projection[0].routeKeys $binding.routeKeys $Item.file $binding.knowledgeId }
                        }
                        foreach ($projection in $routeProjections) {
                            if (@($sameFileBindings | Where-Object { $_.knowledgeId -ceq $projection.knowledgeId }).Count -ne 1) { Add-Finding 'ROUTE_PROJECTION_INDEX_MISSING' $Item.file "projection requires exactly one index binding: $($projection.knowledgeId)" }
                        }
                        $projectedUnion = @($routeProjections | ForEach-Object { $_.routeKeys } | Sort-Object -CaseSensitive -Unique)
                        Compare-RouteSets $entryRoutes $projectedUnion $Item.file 'EntryMode:SharedRouteProjection'
                    }
                }
            }
        }
    }
    foreach ($read in @($Item.requiredReads | Sort-Object -Unique)) {
        [void](Resolve-ContainedFile $read $root 'RequiredRead')
    }
    foreach ($skillName in @($Item.relatedSkills | Sort-Object -Unique)) {
        if ($skillName -notmatch '^es-[a-z0-9-]+$') {
            Add-Finding 'RELATED_SKILL_NAME_INVALID' $Item.file "invalid related Skill name: $skillName"
            continue
        }
        $skillRoot = Join-Path $root ('.agents\skills\' + $skillName)
        foreach ($required in @('SKILL.md', 'agents\openai.yaml')) {
            $requiredPath = Join-Path $skillRoot $required
            if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
                Add-Finding 'RELATED_SKILL_INCOMPLETE' $Item.file "related Skill is missing ${required}: $skillName"
            } elseif (Test-ReparsePointPath $requiredPath) {
                Add-Finding 'PATH_REPARSE_DENIED' $Item.file "related Skill path contains a reparse point: $skillName/$required"
            }
        }
    }
}

function Test-KnowledgeEntry([string]$ProjectRelative, [object[]]$IndexItems) {
    $entryFile = Resolve-ContainedFile $ProjectRelative $root 'Entry'
    if (-not $entryFile) { return }
    $entryPrefix = [IO.Path]::GetFullPath($knowledgeRoot).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $entryFile.StartsWith($entryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        Add-Finding 'PATH_ENTRY_SCOPE' $ProjectRelative 'entry must be below Documentation/AIKnowledge'
        return
    }
    $text = Read-StrictText $entryFile $ProjectRelative
    if ($null -eq $text) { return }
    # Do not reject legitimate status vocabulary such as `pending` inside a contract.
    # Only standalone placeholder markers at the beginning of a line are invalid.
    if ($text -match '(?im)^\s*(?:[-*]\s*)?(TODO|PENDING)\s*(?:$|:)') { Add-Finding 'ENTRY_PLACEHOLDER' $ProjectRelative 'entry contains a TODO or PENDING placeholder' }

    $fields = @{}
    foreach ($field in @('KnowledgeId', 'Authority', 'RouteKeys', 'ContentHash', 'EvidenceLevel')) {
        $matches = @(Get-EntryField $text $field)
        if ($matches.Count -ne 1) { Add-Finding 'ENTRY_FIELD_COUNT' $ProjectRelative "$field must occur exactly once; found $($matches.Count)" }
        $fields[$field] = if ($matches.Count -gt 0) {
            if ($field -eq 'RouteKeys') { $matches[0] } else { $matches[0].Trim('`') }
        } else { '' }
    }
    $staleCount = (Get-EntryField $text 'StaleWhen').Count + @([regex]::Matches($text, '(?mi)^##\s+StaleWhen\s*$')).Count
    if ($staleCount -eq 0) { Add-Finding 'ENTRY_FIELD_COUNT' $ProjectRelative 'StaleWhen is missing' }
    if ($fields.EvidenceLevel -notmatch '^S[0-6](?:\s*/\s*runtime-not-run)?$') { Add-Finding 'ENTRY_EVIDENCE_LEVEL' $ProjectRelative 'EvidenceLevel must be S0-S6 with optional runtime-not-run suffix' }
    if ($fields.ContentHash -notmatch '^[0-9a-f]{64}$') { Add-Finding 'CONTENT_HASH_FORMAT' $ProjectRelative 'ContentHash must be lowercase SHA-256' }

    $hashSchemaMatches = @(Get-EntryField $text 'HashSchema')
    if ($hashSchemaMatches.Count -gt 1) { Add-Finding 'ENTRY_FIELD_COUNT' $ProjectRelative "HashSchema must occur at most once; found $($hashSchemaMatches.Count)" }
    $hashSchema = if ($hashSchemaMatches.Count -eq 1) { $hashSchemaMatches[0].Trim('`') } else { 'legacy' }
    if ($hashSchema -notin @('legacy', 'v2')) { Add-Finding 'ENTRY_HASH_SCHEMA' $ProjectRelative "unsupported HashSchema: $hashSchema" }
    $sourceSetHashMatches = @(Get-EntryField $text 'SourceSetHash')
    $entryBodyHashMatches = @(Get-EntryField $text 'EntryBodyHash')
    if ($hashSchema -eq 'v2') {
        if ($sourceSetHashMatches.Count -ne 1) { Add-Finding 'ENTRY_FIELD_COUNT' $ProjectRelative "SourceSetHash must occur exactly once for v2; found $($sourceSetHashMatches.Count)" }
        if ($entryBodyHashMatches.Count -ne 1) { Add-Finding 'ENTRY_FIELD_COUNT' $ProjectRelative "EntryBodyHash must occur exactly once for v2; found $($entryBodyHashMatches.Count)" }
        if ($sourceSetHashMatches.Count -eq 1 -and $sourceSetHashMatches[0].Trim('`') -notmatch '^[0-9a-f]{64}$') { Add-Finding 'SOURCE_SET_HASH_FORMAT' $ProjectRelative 'SourceSetHash must be lowercase SHA-256' }
        if ($entryBodyHashMatches.Count -eq 1 -and $entryBodyHashMatches[0].Trim('`') -notmatch '^[0-9a-f]{64}$') { Add-Finding 'ENTRY_BODY_HASH_FORMAT' $ProjectRelative 'EntryBodyHash must be lowercase SHA-256' }
        if ($entryBodyHashMatches.Count -eq 1 -and $entryBodyHashMatches[0].Trim('`') -cne (Get-EntryBodyHash $text)) { Add-Finding 'ENTRY_BODY_HASH_MISMATCH' $ProjectRelative 'EntryBodyHash does not match the normalized Knowledge body' }
    } elseif ($sourceSetHashMatches.Count -gt 0 -or $entryBodyHashMatches.Count -gt 0) {
        Add-Finding 'ENTRY_HASH_SCHEMA' $ProjectRelative 'SourceSetHash and EntryBodyHash require HashSchema v2'
    }

    $colonPattern = '[:' + [char]0xFF1A + ']'
    $sourcePattern = '(?ms)(?:^##\s+SourceRefs\s*$|^`SourceRefs`\s*' + $colonPattern + '\s*$)\r?\n(?<body>.*?)(?=^##\s+|^`[A-Za-z][A-Za-z0-9]*`\s*' + $colonPattern + '\s*|\Z)'
    $sourceSection = [regex]::Match($text, $sourcePattern)
    $sourceBody = if ($sourceSection.Success) { $sourceSection.Groups['body'].Value } else { '' }
    $sourceMatches = @([regex]::Matches($sourceBody, '(?m)^- `(.+?)` \(`([0-9a-f]{64})`\)\r?$'))
    if ($sourceMatches.Count -eq 0) { Add-Finding 'SOURCE_REFS_EMPTY' $ProjectRelative 'entry has no valid SourceRefs' }
    $declaredPaths = @($sourceMatches | ForEach-Object { $_.Groups[1].Value })
    foreach ($duplicate in @($declaredPaths | Group-Object | Where-Object Count -gt 1)) {
        Add-Finding 'SOURCE_REF_DUPLICATE' $ProjectRelative "duplicate SourceRef: $($duplicate.Name)"
    }
    $sourceHashes = [Collections.Generic.List[string]]::new()
    foreach ($match in $sourceMatches) {
        $relative = $match.Groups[1].Value
        $declaredHash = $match.Groups[2].Value
        $source = Resolve-ContainedFile $relative $root 'SourceRef'
        if (-not $source) { continue }
        $actualHash = Get-SourceRefHash $source
        $inputFiles[(Get-ProjectRelative $source)] = $actualHash
        if ($actualHash -cne $declaredHash) { Add-Finding 'SOURCE_HASH_DRIFT' $relative 'declared SourceRef hash differs from the current file' }
        $sourceHashes.Add($declaredHash)
    }
    if ($sourceHashes.Count -gt 0) {
        $joined = (@($sourceHashes) | Sort-Object -CaseSensitive) -join ''
        $expected = Get-Sha256Text $joined
        if ($fields.ContentHash -cne $expected) { Add-Finding 'CONTENT_HASH_MISMATCH' $ProjectRelative 'ContentHash does not match the sorted SourceRef hashes' }
        if ($hashSchema -eq 'v2' -and $sourceSetHashMatches.Count -eq 1 -and $sourceSetHashMatches[0].Trim('`') -cne $expected) { Add-Finding 'SOURCE_SET_HASH_MISMATCH' $ProjectRelative 'SourceSetHash does not match the sorted SourceRef hashes' }
        if ($hashSchema -eq 'v2' -and $sourceSetHashMatches.Count -eq 1 -and $fields.ContentHash -cne $sourceSetHashMatches[0].Trim('`')) { Add-Finding 'CONTENT_HASH_LEGACY_ALIAS_MISMATCH' $ProjectRelative 'compatibility ContentHash must equal SourceSetHash for v2' }
    }

    $knowledgeRelative = $entryFile.Substring($entryPrefix.Length).Replace('\', '/')
    $bindings = @($IndexItems | Where-Object { $_.file -ceq $knowledgeRelative })
    $sharedProjection = $text -match ('(?m)^`EntryMode`\s*' + $colonPattern + '\s*`SharedRouteProjection`\s*$')
    $canonicalBindings = @($bindings | Where-Object { $_.knowledgeId -ceq $fields.KnowledgeId })
    if (-not $sharedProjection -and ($bindings.Count -ne 1 -or $canonicalBindings.Count -ne 1)) {
        Add-Finding 'INDEX_BINDING_COUNT' $ProjectRelative "entry must have exactly one file and canonical KnowledgeId binding; found file=$($bindings.Count), canonical=$($canonicalBindings.Count)"
    }
    if ($sharedProjection -and -not $sharedRouteProjectionAllowed) { Add-Finding 'ENTRY_MODE_NOT_ALLOWED' $ProjectRelative 'KnowledgeIndex does not enable SharedRouteProjection' }
    if ($sharedProjection -and $bindings.Count -eq 0) { Add-Finding 'INDEX_BINDING_COUNT' $ProjectRelative 'shared route projection has no index bindings' }
    if ((-not $sharedProjection -and $bindings.Count -eq 1 -and $canonicalBindings.Count -eq 1) -or ($sharedProjection -and $bindings.Count -gt 0)) {
        foreach ($bindingId in @($bindings.knowledgeId | Sort-Object -Unique)) {
            $sameIdBindings = @($IndexItems | Where-Object { $_.knowledgeId -ceq $bindingId })
            if ($sameIdBindings.Count -gt 1) { Add-Finding 'INDEX_DUPLICATE_ID' $ProjectRelative "duplicate knowledgeId: $bindingId" }
        }
        $entryRoutes = @(Get-EntryRouteKeys $fields.RouteKeys)
        $routeProjections = if ($sharedProjection) { @(Get-RouteProjections $text) } else { @() }
        if ($sharedProjection -and $routeProjections.Count -eq 0) { Add-Finding 'ROUTE_PROJECTION_DECLARATION_MISSING' $ProjectRelative 'SharedRouteProjection requires a RouteProjections section' }
        foreach ($duplicate in @($routeProjections | Group-Object knowledgeId | Where-Object Count -gt 1)) {
            Add-Finding 'ROUTE_PROJECTION_DUPLICATE_ID' $ProjectRelative "duplicate route projection: $($duplicate.Name)"
        }
        foreach ($fileBinding in $bindings) {
            foreach ($issue in @($fileBinding.structureIssues)) { Add-Finding 'INDEX_BINDING_STRUCTURE' $ProjectRelative "$($fileBinding.knowledgeId): $issue" }
            if ($fileBinding.contentHash -cne $fields.ContentHash) { Add-Finding 'CONTENT_HASH_INDEX_MISMATCH' $ProjectRelative "entry ContentHash differs from index binding: $($fileBinding.knowledgeId)" }
            if ($hashSchema -eq 'v2') {
                if ($fileBinding.hashSchema -cne 'v2') { Add-Finding 'INDEX_HASH_SCHEMA_MISMATCH' $ProjectRelative "entry hash schema differs from index binding: $($fileBinding.knowledgeId)" }
                if ($sourceSetHashMatches.Count -eq 1 -and $fileBinding.sourceSetHash -cne $sourceSetHashMatches[0].Trim('`')) { Add-Finding 'SOURCE_SET_HASH_INDEX_MISMATCH' $ProjectRelative "entry SourceSetHash differs from index binding: $($fileBinding.knowledgeId)" }
                if ($entryBodyHashMatches.Count -eq 1 -and $fileBinding.entryBodyHash -cne $entryBodyHashMatches[0].Trim('`')) { Add-Finding 'ENTRY_BODY_HASH_INDEX_MISMATCH' $ProjectRelative "entry EntryBodyHash differs from index binding: $($fileBinding.knowledgeId)" }
            }
            if ($sharedProjection) {
                $projection = @($routeProjections | Where-Object { $_.knowledgeId -ceq $fileBinding.knowledgeId })
                if ($projection.Count -ne 1) { Add-Finding 'ROUTE_PROJECTION_BINDING_MISSING' $ProjectRelative "binding requires exactly one declared projection: $($fileBinding.knowledgeId)" }
                else { Compare-RouteSets $projection[0].routeKeys $fileBinding.routeKeys $ProjectRelative $fileBinding.knowledgeId }
            } else {
                Compare-RouteSets $entryRoutes $fileBinding.routeKeys $ProjectRelative $fileBinding.knowledgeId
            }
            Test-IndexEntry $fileBinding -SkipEntryHash
        }
        if ($sharedProjection) {
            foreach ($projection in $routeProjections) {
                if (@($bindings | Where-Object { $_.knowledgeId -ceq $projection.knowledgeId }).Count -ne 1) { Add-Finding 'ROUTE_PROJECTION_INDEX_MISSING' $ProjectRelative "projection requires exactly one index binding: $($projection.knowledgeId)" }
            }
            $projectedUnion = @($routeProjections | ForEach-Object { $_.routeKeys } | Sort-Object -CaseSensitive -Unique)
            Compare-RouteSets $entryRoutes $projectedUnion $ProjectRelative 'EntryMode:SharedRouteProjection'
        }
    }
}

if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf)) {
    Add-Finding 'INDEX_FILE_MISSING' 'Documentation/AIKnowledge/KnowledgeIndex.yaml' 'Knowledge index does not exist'
    $indexText = $null
    $indexItems = @()
} elseif (Test-ReparsePointPath $indexPath) {
    Add-Finding 'PATH_REPARSE_DENIED' 'Documentation/AIKnowledge/KnowledgeIndex.yaml' 'Knowledge index path contains a reparse point'
    $indexText = $null
    $indexItems = @()
} else {
    $indexText = Read-StrictText $indexPath 'Documentation/AIKnowledge/KnowledgeIndex.yaml'
    $indexItems = if ($null -ne $indexText) { @(Parse-Index $indexText ($Mode -in @('Index', 'All'))) } else { @() }
    $indexItems = @($indexItems)
}

if ($Mode -in @('Index', 'All')) {
    foreach ($item in $indexItems) { Test-IndexEntry $item }
}
if ($Mode -eq 'Entry') {
    if ([string]::IsNullOrWhiteSpace($EntryPath)) { Add-Finding 'ENTRY_PATH_REQUIRED' '' 'EntryPath is required for Entry mode' }
    else { Test-KnowledgeEntry $EntryPath $indexItems }
}
if ($Mode -eq 'All') {
    foreach ($file in @($indexItems.file | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)) {
        Test-KnowledgeEntry ('Documentation/AIKnowledge/' + $file) $indexItems
    }
}

$orderedFindings = @($findings | Sort-Object code, path, message)
$inputLines = @($inputFiles.GetEnumerator() | Sort-Object Key | ForEach-Object { $_.Key.Replace('\', '/') + '=' + $_.Value })
$inputJoined = $inputLines -join "`n"
$inputSha = [Security.Cryptography.SHA256]::Create()
try { $inputHash = ([BitConverter]::ToString($inputSha.ComputeHash([Text.Encoding]::UTF8.GetBytes($inputJoined)))).Replace('-', '').ToLowerInvariant() } finally { $inputSha.Dispose() }
$result = [pscustomobject]@{
    validator = 'es-knowledge-validator'
    mode = $Mode
    entryPath = $EntryPath
    status = if ($orderedFindings.Count -eq 0) { 'passed' } else { 'blocked' }
    staticStatus = if ($orderedFindings.Count -eq 0) { 'static-passed' } else { 'static-blocked' }
    runtimeStatus = 'runtime-not-run'
    inputHash = $inputHash
    checkedInputCount = $inputFiles.Count
    checkedIndexEntryCount = @($indexItems).Count
    findingCount = $orderedFindings.Count
    findings = $orderedFindings
    claimsNotProven = @('Unity/editor/process behavior', 'Profiler/Player/IL2CPP/release behavior')
}
$json = $result | ConvertTo-Json -Depth 8

if (-not [string]::IsNullOrWhiteSpace($ReportPath)) {
    if ([IO.Path]::IsPathRooted($ReportPath)) { Write-Error 'ReportPath must be project-relative'; exit 2 }
    $reportRoot = [IO.Path]::GetFullPath((Join-Path $root 'ES\Output')).TrimEnd('\', '/')
    $report = [IO.Path]::GetFullPath((Join-Path $root $ReportPath))
    if (-not $report.StartsWith($reportRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { Write-Error 'ReportPath must remain below ES/Output'; exit 2 }
    if (Test-ReparsePointPath $report) { Write-Error 'ReportPath must not contain a reparse point'; exit 2 }
    $parent = Split-Path -Parent $report
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    [IO.File]::WriteAllText($report, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
}

Write-Output $json
if ($orderedFindings.Count -gt 0) { exit 1 }
exit 0
