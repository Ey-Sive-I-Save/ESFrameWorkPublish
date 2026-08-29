[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceRoot,

    [string]$OutputRoot = '',

    [string]$MappingPath = '',

    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,
    [string]$SourceRevision = '',
    [ValidateRange(1, 1000000)][int]$MaxFiles = 10000,
    [ValidateRange(1, 2147483647)][long]$MaxBytes = 536870912,
    [switch]$DryRun,
    [switch]$RenamePathSegments,
    [switch]$InPlace,
    [switch]$WholeRepository
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)

function Resolve-FullPath([string]$Path) {
    return [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path).Path).TrimEnd('\')
}

function Test-PathWithin([string]$Child, [string]$Parent) {
    $childFull = ([IO.Path]::GetFullPath($Child)).TrimEnd('\')
    $parentFull = ([IO.Path]::GetFullPath($Parent)).TrimEnd('\')
    return $childFull.Equals($parentFull, [StringComparison]::OrdinalIgnoreCase) -or
        $childFull.StartsWith($parentFull + '\', [StringComparison]::OrdinalIgnoreCase)
}

function Get-StrictUtf8Text([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $encoding = [Text.UTF8Encoding]::new($false, $true)
    return $encoding.GetString($bytes)
}

function Write-StrictUtf8Text([string]$Path, [string]$Text) {
    $encoding = [Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText($Path, $Text, $encoding)
}

function Get-FileSha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-TextSha256([string]$Text) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes($Text)
        return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally { $sha.Dispose() }
}

function Get-TreeSha256([array]$Files, [string]$Root) {
    $lines = [Collections.Generic.List[string]]::new()
    foreach ($file in ($Files | Sort-Object RelativePath)) {
        $hash = Get-FileSha256 $file.FullName
        $lines.Add("$($file.RelativePath)`t$hash")
    }
    $payload = ($lines -join "`n")
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($payload)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function Get-RelativePath([string]$Root, [string]$Path) {
    return $Path.Substring($Root.Length).TrimStart('\').Replace('\', '/')
}

function Recover-InPlaceJournal([string]$SourceRoot, [string]$ControlDirectory, [string]$JournalPath) {
    if (-not (Test-Path -LiteralPath $JournalPath -PathType Leaf)) { return }
    $journal = Get-Content -LiteralPath $JournalPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string]$journal.status -eq 'passed') { return }
    $staging = [string]$journal.stagingRoot
    if ([string]::IsNullOrWhiteSpace($staging) -or -not (Test-Path -LiteralPath $staging -PathType Container)) {
        throw "In-place transaction is interrupted and has no recoverable staging tree: $JournalPath"
    }
    $controlFull = ([IO.Path]::GetFullPath($ControlDirectory)).TrimEnd('\')
    $stagingFull = ([IO.Path]::GetFullPath($staging)).TrimEnd('\')
    if (-not (Test-PathWithin $stagingFull $controlFull)) { throw "In-place journal staging path escapes .es-migration: $stagingFull" }
    $backup = Join-Path $stagingFull '__original'
    foreach ($row in @($journal.rows)) {
        $sourceRelative = [string]$row.sourceRelativePath
        $outputRelative = [string]$row.outputRelativePath
        if ([string]::IsNullOrWhiteSpace($sourceRelative) -or [string]::IsNullOrWhiteSpace($outputRelative)) { throw 'In-place journal contains an invalid path row.' }
        $sourcePath = [IO.Path]::GetFullPath((Join-Path $SourceRoot $sourceRelative.Replace('/', '\')))
        $outputPath = [IO.Path]::GetFullPath((Join-Path $SourceRoot $outputRelative.Replace('/', '\')))
        if (-not (Test-PathWithin $sourcePath $SourceRoot) -or -not (Test-PathWithin $outputPath $SourceRoot)) { throw 'In-place journal path escapes SourceRoot.' }
        if (-not $sourcePath.Equals($outputPath, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
            Remove-Item -LiteralPath $outputPath -Force
        }
    }
    if (Test-Path -LiteralPath $backup -PathType Container) {
        foreach ($old in @(Get-ChildItem -LiteralPath $backup -Recurse -File)) {
            $relative = Get-RelativePath $backup $old.FullName
            $destination = Join-Path $SourceRoot ($relative.Replace('/', '\'))
            $destinationDirectory = Split-Path -Parent $destination
            if (-not (Test-Path -LiteralPath $destinationDirectory)) { New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null }
            Move-Item -LiteralPath $old.FullName -Destination $destination -Force
        }
    }
    Remove-Item -LiteralPath $stagingFull -Recurse -Force
    $journal.status = 'recovered'
    $journal.recoveredUtc = [DateTime]::UtcNow.ToString('o')
    $journal.stagingRoot = $null
    Write-StrictUtf8Text $JournalPath ($journal | ConvertTo-Json -Depth 12)
}

function Get-InPlaceAcceptedReplay([string]$SourceRoot, [string]$ControlDirectory) {
    $manifestPath = Join-Path $ControlDirectory 'es-remap-manifest.json'
    $receiptPath = Join-Path $ControlDirectory 'es-remap-receipt.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -and -not (Test-Path -LiteralPath $receiptPath -PathType Leaf)) { return $null }
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or -not (Test-Path -LiteralPath $receiptPath -PathType Leaf)) { throw 'In-place acceptance artifacts are incomplete; refusing replay.' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $receipt = Get-Content -LiteralPath $receiptPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string]$receipt.status -ne 'passed' -or [string]$manifest.status -ne 'written-in-place') { throw 'In-place acceptance artifacts are not accepted; refusing replay.' }
    $expected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($row in @($manifest.files)) {
        $relative = [string]$row.outputRelativePath
        if ([string]::IsNullOrWhiteSpace($relative) -or -not $expected.Add($relative)) { throw "In-place manifest contains duplicate or empty output path: $relative" }
        $path = [IO.Path]::GetFullPath((Join-Path $SourceRoot $relative.Replace('/', '\')))
        if (-not (Test-PathWithin $path $SourceRoot) -or -not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "In-place replay file is missing: $relative" }
        if ((Get-FileSha256 $path) -ne ([string]$row.outputSha256).ToLowerInvariant()) { throw "In-place replay file drift: $relative" }
    }
    $current = @(Get-ChildItem -LiteralPath $SourceRoot -Recurse -File | ForEach-Object {
            $relative = Get-RelativePath $SourceRoot $_.FullName
            if (-not (Test-ExcludedPath $relative)) { $relative }
        })
    if ($current.Count -ne $expected.Count) { throw "In-place replay file set drift: expected $($expected.Count), found $($current.Count)" }
    foreach ($relative in $current) { if (-not $expected.Contains([string]$relative)) { throw "In-place replay has unexpected file: $relative" } }
    $receipt | Add-Member -NotePropertyName idempotentReplay -NotePropertyValue $true -Force
    $receipt | Add-Member -NotePropertyName sourceTreeReplay -NotePropertyValue $true -Force
    $receipt | Add-Member -NotePropertyName replayManifestPath -NotePropertyValue $manifestPath -Force
    return $receipt
}

function Test-ExcludedPath([string]$RelativePath) {
    $p = $RelativePath.ToLowerInvariant()
    return $p -eq '.git' -or $p.StartsWith('.git/') -or
        $p.StartsWith('node_modules/') -or $p.StartsWith('bin/') -or
        $p.StartsWith('obj/') -or $p.StartsWith('dist/') -or
        $p.StartsWith('build/') -or $p.StartsWith('out/') -or
        $p -eq '.es-migration' -or $p.StartsWith('.es-migration/') -or
        $p.StartsWith('src/pro/') -or $p -eq 'src/pro'
}

function Test-HashExcludedPath([string]$RelativePath) {
    $p = $RelativePath.ToLowerInvariant()
    return $p -eq '.git' -or $p.StartsWith('.git/') -or
        $p.StartsWith('node_modules/') -or $p.StartsWith('bin/') -or
        $p.StartsWith('obj/') -or $p.StartsWith('dist/') -or
        $p.StartsWith('build/') -or $p.StartsWith('out/') -or
        $p -eq '.es-migration' -or $p.StartsWith('.es-migration/')
}

function Test-TextExtension([string]$Path) {
    $fileName = [IO.Path]::GetFileName($Path).ToUpperInvariant()
    if ($fileName -eq 'LICENSE' -or $fileName.StartsWith('LICENSE.') -or $fileName -eq 'NOTICE' -or $fileName.StartsWith('NOTICE.')) {
        return $true
    }
    $textExtensions = @(
        '.cs','.csproj','.sln','.props','.targets','.ts','.tsx','.js','.jsx','.mjs','.c','.h',
        '.cc','.cpp','.cxx','.hpp','.py','.go','.rs','.java','.kt','.swift','.json','.yaml','.yml',
        '.md','.txt','.xml','.shader','.asmdef','.toml','.ini','.sql','.css','.scss','.html','.vue'
    )
    return $textExtensions -contains ([IO.Path]::GetExtension($Path).ToLowerInvariant())
}

function Test-CodeExtension([string]$Path) {
    $codeExtensions = @(
        '.cs','.ts','.tsx','.js','.jsx','.mjs','.c','.h','.cc','.cpp','.cxx','.hpp',
        '.py','.go','.rs','.java','.kt','.swift','.shader','.vue'
    )
    return $codeExtensions -contains ([IO.Path]::GetExtension($Path).ToLowerInvariant())
}

$script:TokenRegex = $null
$script:ReplacementBySource = $null
$script:ReplacementByTextIdentifier = $null
$script:FreeTextRegex = $null
$script:ReplacementByFreeText = $null

function Initialize-TokenRemapper([array]$Rules, [array]$TextRules) {
    # Tokenize once per file instead of applying one regex per rule.  The old
    # rule-by-rule loop was O(files × rules); a real repository can contain
    # thousands of declarations and make that approach effectively unusable.
    $script:TokenRegex = [Regex]::new(
        '[\p{L}_][\p{L}\p{N}_]*',
        [Text.RegularExpressions.RegexOptions]::CultureInvariant -bor [Text.RegularExpressions.RegexOptions]::Compiled
    )
    $script:ReplacementBySource = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    foreach ($rule in $Rules) { $script:ReplacementBySource[[string]$rule.source] = [string]$rule.es }
    $script:ReplacementByTextIdentifier = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    $script:ReplacementByFreeText = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    foreach ($rule in @($TextRules)) {
        $source = [string]$rule.source
        if ([string]::IsNullOrWhiteSpace($source)) { continue }
        if ($source -match '^[\p{L}_][\p{L}\p{N}_]*$') { $script:ReplacementByTextIdentifier[$source] = [string]$rule.es }
        else { $script:ReplacementByFreeText[$source] = [string]$rule.es }
    }
    $patterns = @($script:ReplacementByFreeText.Keys | Sort-Object @{Expression={([string]$_).Length};Descending=$true}, @{Expression={[string]$_};Ascending=$true} | ForEach-Object { [Regex]::Escape([string]$_) })
    if ($patterns.Count -gt 0) {
        $script:FreeTextRegex = [Regex]::new(
            '(?<![\p{L}\p{N}_])(?:' + ($patterns -join '|') + ')(?![\p{L}\p{N}_])',
            [Text.RegularExpressions.RegexOptions]::CultureInvariant
        )
    } else { $script:FreeTextRegex = $null }
}

function Get-RenamedToken([string]$Value, [array]$Rules, [array]$TextRules) {
    if ($null -eq $script:TokenRegex) { Initialize-TokenRemapper $Rules $TextRules }
    return $script:TokenRegex.Replace(
        $Value,
        [Text.RegularExpressions.MatchEvaluator]{
            param($match)
            $token = [string]$match.Value
            if ($script:ReplacementBySource.ContainsKey($token)) { return $script:ReplacementBySource[$token] }
            return $token
        }
    )
}

function Get-RenamedWholeText([string]$Value, [array]$Rules, [array]$TextRules) {
    if ($null -eq $script:TokenRegex) { Initialize-TokenRemapper $Rules $TextRules }
    # A giant alternation over thousands of declarations makes a whole-tree
    # rewrite scale poorly.  Use one compiled token pattern and a MatchEvaluator
    # (implemented in .NET) so the scan remains linear without a PowerShell
    # per-character loop; apply only the small free-form seed set afterwards.
    $result = $script:TokenRegex.Replace(
        $Value,
        [Text.RegularExpressions.MatchEvaluator]{
            param($match)
            $token = [string]$match.Value
            if ($script:ReplacementByTextIdentifier.ContainsKey($token)) { return $script:ReplacementByTextIdentifier[$token] }
            return $token
        }
    )
    if ($null -eq $script:FreeTextRegex) { return $result }
    return $script:FreeTextRegex.Replace(
        $result,
        [Text.RegularExpressions.MatchEvaluator]{
            param($match)
            $source = [string]$match.Value
            if ($script:ReplacementByFreeText.ContainsKey($source)) { return $script:ReplacementByFreeText[$source] }
            return $source
        }
    )
}

function Get-RenamedCode([string]$Value, [array]$Rules, [array]$TextRules) {
    if ($null -eq $script:TokenRegex) { Initialize-TokenRemapper $Rules $TextRules }
    $builder = [Text.StringBuilder]::new($Value.Length)
    $state = 0 # 0 normal, 1 single quote, 2 double quote, 3 backtick, 4 line comment, 5 block comment, 6/7 triple quote
    for ($i = 0; $i -lt $Value.Length; $i++) {
        $c = $Value[$i]
        $next = if ($i + 1 -lt $Value.Length) { $Value[$i + 1] } else { [char]0 }
        if ($state -eq 0) {
            if ($c -eq '/' -and $next -eq '/') { [void]$builder.Append('//'); $i++; $state = 4; continue }
            if ($c -eq '/' -and $next -eq '*') { [void]$builder.Append('/*'); $i++; $state = 5; continue }
            if ($c -eq [char]39 -and $next -eq [char]39 -and $i + 2 -lt $Value.Length -and $Value[$i + 2] -eq [char]39) { [void]$builder.Append("'''"); $i += 2; $state = 6; continue }
            if ($c -eq [char]34 -and $next -eq [char]34 -and $i + 2 -lt $Value.Length -and $Value[$i + 2] -eq [char]34) { [void]$builder.Append('"""'); $i += 2; $state = 7; continue }
            if ($c -eq [char]39) { [void]$builder.Append($c); $state = 1; continue }
            if ($c -eq [char]34) { [void]$builder.Append($c); $state = 2; continue }
            if ($c -eq [char]96) { [void]$builder.Append($c); $state = 3; continue }
            if ($c -eq '_' -or [char]::IsLetter($c)) {
                $start = $i
                $i++
                while ($i -lt $Value.Length -and ($Value[$i] -eq '_' -or [char]::IsLetterOrDigit($Value[$i]))) { $i++ }
                $token = $Value.Substring($start, $i - $start)
                if ($script:ReplacementBySource.ContainsKey($token)) { [void]$builder.Append($script:ReplacementBySource[$token]) }
                else { [void]$builder.Append($token) }
                $i--
                continue
            }
            [void]$builder.Append($c)
            continue
        }
        if ($state -eq 4) {
            [void]$builder.Append($c)
            if ($c -eq [char]10 -or $c -eq [char]13) { $state = 0 }
            continue
        }
        if ($state -eq 5) {
            if ($c -eq '*' -and $next -eq '/') { [void]$builder.Append('*/'); $i++; $state = 0; continue }
            [void]$builder.Append($c)
            continue
        }
        if ($state -eq 6 -or $state -eq 7) {
            $quote = if ($state -eq 6) { [char]39 } else { [char]34 }
            if ($c -eq $quote -and $next -eq $quote -and $i + 2 -lt $Value.Length -and $Value[$i + 2] -eq $quote) { [void]$builder.Append("$quote$quote$quote"); $i += 2; $state = 0; continue }
            [void]$builder.Append($c)
            continue
        }
        # Quoted literal: copy it byte-for-byte, including escaped delimiters.
        [void]$builder.Append($c)
        if ($c -eq '\' -and $i + 1 -lt $Value.Length) { [void]$builder.Append($Value[$i + 1]); $i++; continue }
        if (($state -eq 1 -and $c -eq [char]39) -or ($state -eq 2 -and $c -eq [char]34) -or ($state -eq 3 -and $c -eq [char]96)) { $state = 0 }
    }
    return $builder.ToString()
}

$sourceFull = Resolve-FullPath $SourceRoot
$projectFull = Resolve-FullPath $ProjectRoot
if (Test-PathWithin $sourceFull $projectFull) { throw "SourceRoot must be outside the protected project root: $projectFull" }

$inPlace = [bool]$InPlace -or [string]::IsNullOrWhiteSpace($OutputRoot)
$outputFull = if ($inPlace) { $sourceFull } else { [IO.Path]::GetFullPath($OutputRoot).TrimEnd('\') }
if (-not $inPlace -and (Test-PathWithin $outputFull $projectFull)) {
    throw "OutputRoot must be outside the protected project root: $projectFull"
}
if ($inPlace -and -not $outputFull.Equals($sourceFull, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'InPlace output must equal SourceRoot.'
}
if (-not $inPlace -and (Test-PathWithin $outputFull $sourceFull)) {
    throw "OutputRoot must not be inside SourceRoot: $sourceFull"
}
if (-not (Test-Path -LiteralPath $sourceFull -PathType Container)) { throw "SourceRoot does not exist: $sourceFull" }
$controlDirectory = Join-Path $sourceFull '.es-migration'
if ([string]::IsNullOrWhiteSpace($MappingPath)) {
    $MappingPath = Join-Path $controlDirectory 'es-symbol-map.json'
}
$mapFull = [IO.Path]::GetFullPath($MappingPath).TrimEnd('\')
if (Test-PathWithin $mapFull $projectFull) { throw "MappingPath must be outside the protected project root: $projectFull" }
if ((Test-PathWithin $mapFull $sourceFull) -and -not (Test-PathWithin $mapFull $controlDirectory)) { throw "MappingPath inside SourceRoot is only allowed below .es-migration: $sourceFull" }
if (-not (Test-Path -LiteralPath $mapFull -PathType Leaf)) { throw "MappingPath does not exist: $mapFull" }
if ($inPlace) { Recover-InPlaceJournal $sourceFull $controlDirectory (Join-Path $controlDirectory 'es-remap-journal.json') }
$sourceItem = Get-Item -LiteralPath $sourceFull
if (($sourceItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'SourceRoot reparse points are not accepted.' }
$sourceReparse = @(Get-ChildItem -LiteralPath $sourceFull -Recurse -Force | Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 })
if ($sourceReparse.Count -gt 0) { throw "Source tree contains reparse points: $($sourceReparse[0].FullName)" }

$mapping = Get-Content -LiteralPath $MappingPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ([int]$mapping.schemaVersion -ne 1) { throw 'Mapping schemaVersion must be 1.' }
if ([string]::IsNullOrWhiteSpace([string]$mapping.mapId)) { throw 'Mapping mapId is required.' }
$rules = @($mapping.symbols | Where-Object {
        -not [string]::IsNullOrWhiteSpace([string]$_.source) -and
        -not [string]::IsNullOrWhiteSpace([string]$_.es) -and
        -not ([string]$_.source).Contains('*')
    } | Sort-Object @{Expression = { ([string]$_.source).Length }; Descending = $true }, @{Expression = { [string]$_.source }; Ascending = $true })
if ($rules.Count -eq 0) { throw 'Mapping must contain at least one concrete source -> es symbol.' }
$invalidRule = @($rules | Where-Object {
        ([string]$_.source -notmatch '^[\p{L}_][\p{L}\p{N}_]*$') -or
        ([string]$_.es -notmatch '^[\p{L}_][\p{L}\p{N}_]*$')
})
if ($invalidRule.Count -gt 0) { throw "Mapping symbols must be identifier-safe: $($invalidRule[0].source) -> $($invalidRule[0].es)" }
$duplicateSource = @($rules | Group-Object { [string]$_.source } -CaseSensitive | Where-Object Count -gt 1)
if ($duplicateSource.Count -gt 0) { throw "Duplicate source identities are not allowed: $($duplicateSource.Name -join ', ')" }
$duplicateEs = @($rules | Group-Object { [string]$_.es } -CaseSensitive | Where-Object Count -gt 1)
if ($duplicateEs.Count -gt 0) { throw "Duplicate ES identities are not allowed: $($duplicateEs.Name -join ', ')" }
$textRules = @($mapping.textReplacements | Where-Object {
        -not [string]::IsNullOrWhiteSpace([string]$_.source) -and
        -not [string]::IsNullOrWhiteSpace([string]$_.es)
    })
if ($textRules.Count -eq 0) { $textRules = @($rules) }
Initialize-TokenRemapper $rules $textRules

$wholeRepository = [bool]$WholeRepository -or $inPlace
$renamePaths = [bool]$RenamePathSegments -or $inPlace
if ($inPlace) {
    $replayReceipt = Get-InPlaceAcceptedReplay $sourceFull $controlDirectory
    if ($null -ne $replayReceipt) {
        $replayReceipt | ConvertTo-Json -Depth 12
        exit 0
    }
}

$allFiles = @(Get-ChildItem -LiteralPath $sourceFull -Recurse -File | ForEach-Object {
        $relative = Get-RelativePath $sourceFull $_.FullName
        if (-not (Test-HashExcludedPath $relative)) {
            [pscustomobject]@{ FullName = $_.FullName; RelativePath = $relative; Length = $_.Length; IsText = (Test-TextExtension $_.FullName) }
        }
    } | Sort-Object RelativePath)
$excludedProFiles = @($allFiles | Where-Object { $_.RelativePath.ToLowerInvariant().StartsWith('src/pro/') -or $_.RelativePath.ToLowerInvariant() -eq 'src/pro' }).Count
$sourceFiles = @($allFiles | Where-Object { -not (Test-ExcludedPath $_.RelativePath) })
if ($sourceFiles.Count -eq 0) { throw 'No eligible files were found under SourceRoot.' }
$scannedBytes = [long](($allFiles | Measure-Object -Property Length -Sum).Sum)
if ($allFiles.Count -gt $MaxFiles) { throw "Source file limit exceeded: $($allFiles.Count) > $MaxFiles" }
if ($scannedBytes -gt $MaxBytes) { throw "Source byte limit exceeded: $scannedBytes > $MaxBytes" }
$sourceTreeHash = Get-TreeSha256 $allFiles $sourceFull

$planRows = [Collections.Generic.List[object]]::new()
$excludedBoundaryReferences = [Collections.Generic.List[string]]::new()
$outputRelSeen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$sourceRelSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($sourceFile in $sourceFiles) { [void]$sourceRelSet.Add([string]$sourceFile.RelativePath) }
$changedFiles = 0
foreach ($file in $sourceFiles) {
    $relative = $file.RelativePath
    $outputRelative = if ($renamePaths) { Get-RenamedWholeText $relative $rules $textRules } else { $relative }
    $candidateOutput = [IO.Path]::GetFullPath((Join-Path $outputFull ($outputRelative.Replace('/', '\')))).TrimEnd('\')
    if (-not (Test-PathWithin $candidateOutput $outputFull)) { throw "Output path escapes OutputRoot: $outputRelative" }
    if ($outputRelative.ToLowerInvariant().StartsWith('.git/') -or $outputRelative.ToLowerInvariant().StartsWith('.es-migration/') -or $outputRelative.ToLowerInvariant().StartsWith('src/pro/')) { throw "Output path enters a protected tree: $outputRelative" }
    if (-not $outputRelSeen.Add($outputRelative)) { throw "Output path collision: $outputRelative" }
    $isText = [bool]$file.IsText
    $sourceText = if ($isText) { Get-StrictUtf8Text $file.FullName } else { $null }
    $outputText = if (-not $isText) { $null } elseif ([IO.Path]::GetFileName($relative) -match '^(LICENSE|NOTICE)(\.|$)') {
        $sourceText
    } elseif ($wholeRepository) {
        Get-RenamedWholeText $sourceText $rules $textRules
    } elseif (Test-CodeExtension $relative) {
        Get-RenamedCode $sourceText $rules $textRules
    } else {
        $sourceText
    }
    $referencesExcludedTree = $isText -and $outputText -match '(?i)(src[\\/]pro(?:[\\/]|$)|from\s+["''][^"'']*pro[^"'']*["''])'
    if ($referencesExcludedTree) { $excludedBoundaryReferences.Add($relative) }
    $changed = if (-not $isText) { $false } else { $outputText -cne $sourceText }
    if ($changed) { $changedFiles++ }
    $planRows.Add([pscustomobject]@{
            sourceRelativePath = $relative
            outputRelativePath = $outputRelative
            sourceSha256 = Get-FileSha256 $file.FullName
            outputSha256 = if ($isText) { Get-TextSha256 $outputText } else { Get-FileSha256 $file.FullName }
            kind = if ($isText) { 'text' } else { 'binary' }
            referencesExcludedTree = $referencesExcludedTree
            changed = $changed
    })
}

if ($inPlace) {
    foreach ($row in $planRows) {
        if (-not $sourceRelSet.Contains([string]$row.outputRelativePath)) {
            $destination = Join-Path $sourceFull ([string]$row.outputRelativePath).Replace('/', '\')
            if (Test-Path -LiteralPath $destination -PathType Leaf) {
                throw "In-place destination collides with an unmanaged file: $($row.outputRelativePath)"
            }
        }
    }
}

$nonClaims = [Collections.Generic.List[string]]::new()
if ($inPlace) { [void]$nonClaims.Add('In-place mutation was requested; source tree is the target.') } else { [void]$nonClaims.Add('No target project files were written.') }
[void]$nonClaims.Add('No license clearance is granted.')
[void]$nonClaims.Add('No Unity or Runtime compatibility is proven.')
[void]$nonClaims.Add('References to excluded licensed trees are reported but not repaired.')
[void]$nonClaims.Add('Lexical remap is not an AST, compiler, or semantic-equivalence proof.')
if (-not $wholeRepository) { [void]$nonClaims.Add('Structured metadata and string/comment contents are intentionally not renamed.') }
[void]$nonClaims.Add('Git history is not rewritten by this tool.')

$mappingFingerprintInput = @(
    "mapId=$($mapping.mapId)",
    "sourceRevision=$SourceRevision",
    "sourceTreeSha256=$sourceTreeHash",
    "renamePathSegments=$renamePaths",
    "wholeRepository=$wholeRepository",
    (($rules | ForEach-Object { "$($_.source)=$($_.es)" }) -join "`n")
) -join "`n"
$sha = [Security.Cryptography.SHA256]::Create()
try { $planHash = ([BitConverter]::ToString($sha.ComputeHash([Text.UTF8Encoding]::new($false).GetBytes($mappingFingerprintInput)))).Replace('-', '').ToLowerInvariant() }
finally { $sha.Dispose() }

$manifest = [ordered]@{
    schemaVersion = 1
    tool = 'es-open-source-migration/transparent-namespace-remap'
    status = if ($DryRun) { 'dry-run' } else { 'written' }
    mapId = [string]$mapping.mapId
    source = [ordered]@{
        rootName = [IO.Path]::GetFileName($sourceFull)
        locatorPolicy = 'external-sibling-or-explicit-source-root; no mutable absolute source path persisted'
        revision = $SourceRevision
        treeSha256 = $sourceTreeHash
        supportedFileCount = $sourceFiles.Count
        scannedFileCount = $allFiles.Count
        scannedBytes = $scannedBytes
        transformedTextFileCount = @($sourceFiles | Where-Object IsText).Count
        copiedBinaryFileCount = @($sourceFiles | Where-Object { -not $_.IsText }).Count
    }
    output = [ordered]@{
        locator = $outputFull
        insideProtectedProject = $false
        renamePathSegments = $renamePaths
        inPlace = $inPlace
    }
    limits = [ordered]@{ maxFiles = $MaxFiles; maxBytes = $MaxBytes }
    mapping = [ordered]@{
        ruleCount = $rules.Count
        planHash = $planHash
        provenancePreserved = $true
        textPolicy = if ($wholeRepository) { 'all-supported-text-including-metadata-docs-comments-ui; LICENSE/NOTICE preserved' } else { 'code-identifiers-and-path-segments-only; string/comment/structured-text contents preserved' }
        licenseProtectedPatterns = @('src/pro/**', 'LICENSE*', 'NOTICE*')
    }
    boundaryFindings = [ordered]@{
        excludedTreeReferenceFileCount = $excludedBoundaryReferences.Count
        excludedTreeReferenceFiles = @($excludedBoundaryReferences | Sort-Object)
    }
    counts = [ordered]@{
        changedFiles = $changedFiles
        unchangedFiles = $sourceFiles.Count - $changedFiles
        excludedLicensedTreeFiles = $excludedProFiles
    }
    files = $planRows
    nonClaims = @($nonClaims)
}

$receipt = [ordered]@{
    skillName = 'es-open-source-migration'
    case = 'transparent-namespace-remap'
    status = if ($DryRun) { 'not-run' } else { 'passed' }
    evidenceLevel = 'static'
    receiptPath = if ($DryRun) { $null } elseif ($inPlace) { (Join-Path $controlDirectory 'es-remap-receipt.json') } else { (Join-Path $outputFull 'es-remap-receipt.json') }
    sourceRefs = @("mapping:$($mapping.mapId)", "source-tree:$sourceTreeHash", "plan-hash:$planHash")
    timestampUtc = [DateTime]::UtcNow.ToString('o')
    planHash = $planHash
    outputRoot = $outputFull
    nonClaims = $manifest.nonClaims
}

if ($DryRun) {
    [ordered]@{ manifest = $manifest; receipt = $receipt; files = $planRows } | ConvertTo-Json -Depth 12
    exit 0
}

if ($inPlace) {
    $manifestPath = Join-Path $controlDirectory 'es-remap-manifest.json'
    $receiptPath = Join-Path $controlDirectory 'es-remap-receipt.json'
    $journalPath = Join-Path $controlDirectory 'es-remap-journal.json'
    $staging = Join-Path $controlDirectory ('.staging-' + $planHash + '-' + [Guid]::NewGuid().ToString('N'))
    $backup = Join-Path $staging '__original'
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    $journal = [ordered]@{
        schemaVersion = 1
        status = 'staging'
        planHash = $planHash
        sourceTreeSha256 = $sourceTreeHash
        sourceRootName = [IO.Path]::GetFileName($sourceFull)
        wholeRepository = $wholeRepository
        renamePathSegments = $renamePaths
        stagingRoot = $staging
        rows = @($planRows | ForEach-Object { [ordered]@{ sourceRelativePath = $_.sourceRelativePath; outputRelativePath = $_.outputRelativePath; outputSha256 = $_.outputSha256; kind = $_.kind } })
        nonClaims = $manifest.nonClaims
    }
    Write-StrictUtf8Text $journalPath ($journal | ConvertTo-Json -Depth 12)
    try {
        foreach ($row in $planRows) {
            $destination = Join-Path $staging ([string]$row.outputRelativePath).Replace('/', '\')
            $destinationDirectory = Split-Path -Parent $destination
            if (-not (Test-Path -LiteralPath $destinationDirectory)) { New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null }
            $sourcePath = Join-Path $sourceFull ([string]$row.sourceRelativePath).Replace('/', '\')
            if ([string]$row.kind -eq 'binary') {
                [IO.File]::Copy($sourcePath, $destination, $true)
            } else {
                $sourceText = Get-StrictUtf8Text $sourcePath
                $outputText = if ([IO.Path]::GetFileName($row.sourceRelativePath) -match '^(LICENSE|NOTICE)(\.|$)') { $sourceText } elseif ($wholeRepository) { Get-RenamedWholeText $sourceText $rules $textRules } elseif (Test-CodeExtension $row.sourceRelativePath) { Get-RenamedCode $sourceText $rules $textRules } else { $sourceText }
                Write-StrictUtf8Text $destination $outputText
            }
        }
        $currentFiles = @(Get-ChildItem -LiteralPath $sourceFull -Recurse -File | ForEach-Object {
                $currentRelative = Get-RelativePath $sourceFull $_.FullName
                if (-not (Test-HashExcludedPath $currentRelative)) {
                    [pscustomobject]@{ FullName = $_.FullName; RelativePath = $currentRelative; Length = $_.Length; IsText = (Test-TextExtension $_.FullName) }
                }
            } | Sort-Object RelativePath)
        if ($currentFiles.Count -ne $allFiles.Count -or (Get-TreeSha256 $currentFiles $sourceFull) -ne $sourceTreeHash) { throw 'Source tree drifted during in-place staging; no source file was committed.' }

        $journal.status = 'commit-started'
        Write-StrictUtf8Text $journalPath ($journal | ConvertTo-Json -Depth 12)
        foreach ($row in $planRows) {
            $sourcePath = Join-Path $sourceFull ([string]$row.sourceRelativePath).Replace('/', '\')
            if (Test-Path -LiteralPath $sourcePath -PathType Leaf) {
                $backupPath = Join-Path $backup ([string]$row.sourceRelativePath).Replace('/', '\')
                $backupDirectory = Split-Path -Parent $backupPath
                if (-not (Test-Path -LiteralPath $backupDirectory)) { New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null }
                Move-Item -LiteralPath $sourcePath -Destination $backupPath -Force
            }
        }
        foreach ($row in $planRows) {
            $stagedPath = Join-Path $staging ([string]$row.outputRelativePath).Replace('/', '\')
            $destinationPath = Join-Path $sourceFull ([string]$row.outputRelativePath).Replace('/', '\')
            $destinationDirectory = Split-Path -Parent $destinationPath
            if (-not (Test-Path -LiteralPath $destinationDirectory)) { New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null }
            Move-Item -LiteralPath $stagedPath -Destination $destinationPath -Force
        }
        $manifest.output.locatorPolicy = 'in-place source root; no final external copy'
        $manifest.output.locator = [IO.Path]::GetFileName($sourceFull)
        $manifest.output.insideProtectedProject = $false
        $manifest.status = 'written-in-place'
        $receipt.receiptPath = $receiptPath
        $receipt.status = 'passed'
        $receipt.inPlace = $true
        Write-StrictUtf8Text $manifestPath ($manifest | ConvertTo-Json -Depth 12)
        Write-StrictUtf8Text $receiptPath ($receipt | ConvertTo-Json -Depth 12)
        # The staging tree is only a transaction buffer.  Remove it after the
        # accepted in-place commit so no final external copy or old-source
        # backup remains beside the checkout.
        if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
        $journal.stagingRoot = $null
        $journal.status = 'passed'
        $journal.completedUtc = [DateTime]::UtcNow.ToString('o')
        Write-StrictUtf8Text $journalPath ($journal | ConvertTo-Json -Depth 12)
        $receipt | ConvertTo-Json -Depth 12
        exit 0
    } catch {
        $journal.status = 'interrupted'
        $journal.error = $_.Exception.Message
        try { Write-StrictUtf8Text $journalPath ($journal | ConvertTo-Json -Depth 12) } catch { }
        throw
    }
}

if (Test-Path -LiteralPath $outputFull) {
    $existingReceipt = Join-Path $outputFull 'es-remap-receipt.json'
    if (-not (Test-Path -LiteralPath $existingReceipt -PathType Leaf)) { throw "OutputRoot exists without an acceptance receipt: $outputFull" }
    $existing = Get-Content -LiteralPath $existingReceipt -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string]$existing.planHash -ne $planHash) { throw "OutputRoot plan hash conflict; refusing overwrite: $outputFull" }
    if ([string]$existing.status -ne 'passed') { throw "OutputRoot receipt is not accepted: $outputFull" }
    $existingManifestPath = Join-Path $outputFull 'es-remap-manifest.json'
    if (-not (Test-Path -LiteralPath $existingManifestPath -PathType Leaf)) { throw "OutputRoot is missing its manifest: $outputFull" }
    $existingManifest = Get-Content -LiteralPath $existingManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string]$existingManifest.mapping.planHash -ne $planHash) { throw "OutputRoot manifest plan hash drift: $outputFull" }
    if ($null -eq $existingManifest.boundaryFindings -or $null -eq $existingManifest.files) { throw "OutputRoot manifest schema is stale; use a new OutputRoot: $outputFull" }
    foreach ($row in @($existingManifest.files)) {
        $existingFile = Join-Path $outputFull ([string]$row.outputRelativePath).Replace('/', '\')
        if (-not (Test-Path -LiteralPath $existingFile -PathType Leaf)) { throw "OutputRoot file missing: $($row.outputRelativePath)" }
        if ((Get-FileSha256 $existingFile) -ne ([string]$row.outputSha256).ToLowerInvariant()) { throw "OutputRoot file hash drift: $($row.outputRelativePath)" }
    }
    $expectedOutputFiles = @($existingManifest.files | ForEach-Object { [string]$_.outputRelativePath })
    $unexpected = @(Get-ChildItem -LiteralPath $outputFull -Recurse -File | ForEach-Object {
            $relative = Get-RelativePath $outputFull $_.FullName
            if ($relative -ne 'es-remap-manifest.json' -and $relative -ne 'es-remap-receipt.json' -and $expectedOutputFiles -notcontains $relative) { $relative }
        })
    if ($unexpected.Count -gt 0) { throw "OutputRoot contains unexpected files: $($unexpected[0])" }
    $receipt.status = 'passed'
    $receipt.idempotentReplay = $true
    $receipt | ConvertTo-Json -Depth 12
    exit 0
}

$staging = "$outputFull.__staging-$planHash-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $staging -Force | Out-Null
foreach ($row in $planRows) {
    $destination = Join-Path $staging ($row.outputRelativePath.Replace('/', '\'))
    $destinationDirectory = Split-Path -Parent $destination
    if (-not (Test-Path -LiteralPath $destinationDirectory)) { New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null }
    $sourcePath = Join-Path $sourceFull ($row.sourceRelativePath.Replace('/', '\'))
    if ([string]$row.kind -eq 'binary') {
        [IO.File]::Copy($sourcePath, $destination, $false)
    } else {
        $sourceText = Get-StrictUtf8Text $sourcePath
        $outputText = if ([IO.Path]::GetFileName($row.sourceRelativePath) -match '^(LICENSE|NOTICE)(\.|$)') { $sourceText } elseif ($wholeRepository) { Get-RenamedWholeText $sourceText $rules $textRules } elseif (Test-CodeExtension $row.sourceRelativePath) { Get-RenamedCode $sourceText $rules $textRules } else { $sourceText }
        Write-StrictUtf8Text $destination $outputText
    }
}
Write-StrictUtf8Text (Join-Path $staging 'es-remap-manifest.json') ($manifest | ConvertTo-Json -Depth 12)
$receipt.receiptPath = Join-Path $outputFull 'es-remap-receipt.json'
Write-StrictUtf8Text (Join-Path $staging 'es-remap-receipt.json') ($receipt | ConvertTo-Json -Depth 12)
# Re-scan before publication so a source edit/addition/deletion during the
# transform cannot be silently accepted under the initial source hash.
$currentFiles = @(Get-ChildItem -LiteralPath $sourceFull -Recurse -File | ForEach-Object {
        $currentRelative = Get-RelativePath $sourceFull $_.FullName
        if (-not (Test-HashExcludedPath $currentRelative)) {
            [pscustomobject]@{ FullName = $_.FullName; RelativePath = $currentRelative; Length = $_.Length; IsText = (Test-TextExtension $_.FullName) }
        }
    } | Sort-Object RelativePath)
if ($currentFiles.Count -ne $allFiles.Count -or (Get-TreeSha256 $currentFiles $sourceFull) -ne $sourceTreeHash) {
    throw 'Source tree drifted during remap; staging output was not published.'
}
# Receipt is written into staging before the final directory becomes visible.
Move-Item -LiteralPath $staging -Destination $outputFull
$receipt | ConvertTo-Json -Depth 12
