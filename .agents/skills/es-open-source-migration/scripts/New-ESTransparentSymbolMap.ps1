[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceRoot,

    [string]$OutputMapPath = '',

    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,
    [string]$SourceToken = '',
    [string[]]$SourceTextTokens = @(),
    [string]$SourceRevision = '',
    [ValidateRange(1, 1000000)][int]$MaxFiles = 10000,
    [ValidateRange(1, 2147483647)][long]$MaxBytes = 536870912
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)

function Resolve-FullPath([string]$Path) {
    return [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path).Path).TrimEnd('\')
}

function Test-PathWithin([string]$Child, [string]$Parent) {
    $childFull = ([IO.Path]::GetFullPath($Child)).TrimEnd('\')
    $parentFull = ([IO.Path]::GetFullPath($Parent)).TrimEnd('\')
    return $childFull.Equals($parentFull, [StringComparison]::OrdinalIgnoreCase) -or $childFull.StartsWith($parentFull + '\', [StringComparison]::OrdinalIgnoreCase)
}

function Get-RelativePath([string]$Root, [string]$Path) {
    return $Path.Substring($Root.Length).TrimStart('\').Replace('\', '/')
}

function Test-HashExcludedPath([string]$RelativePath) {
    $p = $RelativePath.ToLowerInvariant()
    return $p -eq '.git' -or $p.StartsWith('.git/') -or $p.StartsWith('node_modules/') -or
        $p.StartsWith('bin/') -or $p.StartsWith('obj/') -or $p.StartsWith('dist/') -or
        $p.StartsWith('build/') -or $p.StartsWith('out/') -or
        $p -eq '.es-migration' -or $p.StartsWith('.es-migration/')
}

function Test-ExcludedPath([string]$RelativePath) {
    return (Test-HashExcludedPath $RelativePath) -or
        $RelativePath.ToLowerInvariant().StartsWith('src/pro/') -or
        $RelativePath.ToLowerInvariant() -eq 'src/pro'
}

function Test-TextExtension([string]$Path) {
    $fileName = [IO.Path]::GetFileName($Path).ToUpperInvariant()
    if ($fileName -eq 'LICENSE' -or $fileName.StartsWith('LICENSE.') -or $fileName -eq 'NOTICE' -or $fileName.StartsWith('NOTICE.')) { return $true }
    $extensions = @('.cs','.csproj','.sln','.props','.targets','.ts','.tsx','.js','.jsx','.mjs','.c','.h','.cc','.cpp','.cxx','.hpp','.py','.go','.rs','.java','.kt','.swift','.json','.yaml','.yml','.md','.txt','.xml','.shader','.asmdef','.toml','.ini','.sql','.css','.scss','.html','.vue')
    if ($extensions -contains ([IO.Path]::GetExtension($Path).ToLowerInvariant())) { return $true }
    # Whole-repository mode also covers extensionless/opaque-text project
    # files such as `.gitignore`, `.env.example`, `.mdc`, `.snap`, and shell
    # helpers.  Treat a file as text only when strict UTF-8 decoding succeeds,
    # it has no NUL byte, and it contains no material control-character run;
    # this keeps binary assets out of lexical replacement without maintaining
    # an unbounded extension allow-list.
    try {
        $bytes = [IO.File]::ReadAllBytes($Path)
        if ([Array]::IndexOf($bytes, [byte]0) -ge 0) { return $false }
        $decoded = ([Text.UTF8Encoding]::new($false, $true)).GetString($bytes)
        $controls = [Regex]::Matches($decoded, '[\x00-\x08\x0B\x0C\x0E-\x1F]').Count
        return ($controls -eq 0 -or $controls -lt [Math]::Max(1, [Math]::Floor($decoded.Length * 0.01)))
    } catch { return $false }
}

function Test-DeclarationExtension([string]$Path) {
    # Declaration discovery is intentionally narrower than text remapping:
    # Markdown/JSON/YAML often contain prose such as "class Foo" or
    # `import type` examples and must not manufacture source identities.
    $extensions = @('.cs','.ts','.tsx','.js','.jsx','.mjs','.c','.h','.cc','.cpp','.cxx','.hpp','.py','.go','.rs','.java','.kt','.swift','.vue')
    return $extensions -contains ([IO.Path]::GetExtension($Path).ToLowerInvariant())
}

function Get-StrictUtf8Text([string]$Path) {
    return ([Text.UTF8Encoding]::new($false, $true)).GetString([IO.File]::ReadAllBytes($Path))
}

function ConvertTo-DeclarationScanText([string]$Text) {
    # Keep line structure while blanking comments and quoted literals.  The
    # declaration regex must not turn examples such as "class Fake {}" in a
    # string/comment into source identities.  This is deliberately a lexical
    # filter, not a language parser; semantic claims remain out of scope.
    $builder = [Text.StringBuilder]::new($Text.Length)
    $state = 0 # 0 normal, 1 single quote, 2 double quote, 3 template/backtick, 4 line comment, 5 block comment, 6/7 triple quote
    for ($i = 0; $i -lt $Text.Length; $i++) {
        $c = $Text[$i]
        $next = if ($i + 1 -lt $Text.Length) { $Text[$i + 1] } else { [char]0 }
        if ($state -eq 0) {
            if ($c -eq '/' -and $next -eq '/') { [void]$builder.Append('  '); $i++; $state = 4; continue }
            if ($c -eq '/' -and $next -eq '*') { [void]$builder.Append('  '); $i++; $state = 5; continue }
            if ($c -eq [char]39 -and $next -eq [char]39 -and $i + 2 -lt $Text.Length -and $Text[$i + 2] -eq [char]39) { [void]$builder.Append('   '); $i += 2; $state = 6; continue }
            if ($c -eq [char]34 -and $next -eq [char]34 -and $i + 2 -lt $Text.Length -and $Text[$i + 2] -eq [char]34) { [void]$builder.Append('   '); $i += 2; $state = 7; continue }
            if ($c -eq [char]39) { [void]$builder.Append(' '); $state = 1; continue }
            if ($c -eq [char]34) { [void]$builder.Append(' '); $state = 2; continue }
            if ($c -eq [char]96) { [void]$builder.Append(' '); $state = 3; continue }
            [void]$builder.Append($c)
            continue
        }
        if ($state -eq 4) {
            if ($c -eq [char]10 -or $c -eq [char]13) { [void]$builder.Append($c); $state = 0 }
            else { [void]$builder.Append(' ') }
            continue
        }
        if ($state -eq 5) {
            if ($c -eq '*' -and $next -eq '/') { [void]$builder.Append('  '); $i++; $state = 0; continue }
            if ($c -eq [char]10 -or $c -eq [char]13) { [void]$builder.Append($c) }
            else { [void]$builder.Append(' ') }
            continue
        }
        if ($state -eq 6 -or $state -eq 7) {
            $quote = if ($state -eq 6) { [char]39 } else { [char]34 }
            if ($c -eq $quote -and $next -eq $quote -and $i + 2 -lt $Text.Length -and $Text[$i + 2] -eq $quote) { [void]$builder.Append('   '); $i += 2; $state = 0; continue }
            if ($c -eq [char]10 -or $c -eq [char]13) { [void]$builder.Append($c) }
            else { [void]$builder.Append(' ') }
            continue
        }
        # Quoted literal.  Preserve newlines, blank escapes as a unit, and
        # return to normal after the matching quote.
        if ($c -eq '\' -and $i + 1 -lt $Text.Length) { [void]$builder.Append('  '); $i++; continue }
        if (($state -eq 1 -and $c -eq [char]39) -or ($state -eq 2 -and $c -eq [char]34) -or ($state -eq 3 -and $c -eq [char]96)) { [void]$builder.Append(' '); $state = 0; continue }
        if ($c -eq [char]10 -or $c -eq [char]13) { [void]$builder.Append($c) }
        else { [void]$builder.Append(' ') }
    }
    return $builder.ToString()
}

function Get-FileSha256([string]$Path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($Path)
    try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '').ToLowerInvariant() }
    finally { $stream.Dispose(); $sha.Dispose() }
}

function Get-TreeSha256([array]$Files) {
    $lines = [Collections.Generic.List[string]]::new()
    foreach ($file in ($Files | Sort-Object RelativePath)) { $lines.Add("$($file.RelativePath)`t$(Get-FileSha256 $file.FullName)") }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.UTF8Encoding]::new($false).GetBytes(($lines -join "`n"))))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function ConvertTo-Identifier([string]$Value) {
    $parts = [Regex]::Matches($Value, '[\p{L}\p{N}]+') | ForEach-Object { $_.Value }
    if (@($parts).Count -eq 0) { return '' }
    $joined = ($parts | ForEach-Object { $_.Substring(0, 1).ToUpperInvariant() + $_.Substring(1) }) -join ''
    if ($joined -match '^[\p{N}]') { $joined = '_' + $joined }
    return $joined
}

function Get-SourceRevision([string]$Root, [string]$Provided) {
    if (-not [string]::IsNullOrWhiteSpace($Provided)) { return $Provided }
    if (Test-Path -LiteralPath (Join-Path $Root '.git')) {
        $revision = (& git -C $Root rev-parse HEAD 2>$null)
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($revision)) { return ([string]$revision).Trim() }
    }
    return ''
}

$sourceFull = Resolve-FullPath $SourceRoot
$projectFull = Resolve-FullPath $ProjectRoot
$controlDirectory = Join-Path $sourceFull '.es-migration'
$mapFull = if ([string]::IsNullOrWhiteSpace($OutputMapPath)) {
    Join-Path $controlDirectory 'es-symbol-map.json'
} else {
    [IO.Path]::GetFullPath($OutputMapPath).TrimEnd('\')
}
if (Test-PathWithin $sourceFull $projectFull) { throw "SourceRoot must be outside the protected project root: $projectFull" }
if (Test-PathWithin $mapFull $projectFull) { throw "OutputMapPath must be outside the protected project root: $projectFull" }
if ((Test-PathWithin $mapFull $sourceFull) -and -not (Test-PathWithin $mapFull $controlDirectory)) { throw "OutputMapPath inside SourceRoot is only allowed below .es-migration: $sourceFull" }
if (-not (Test-Path -LiteralPath $sourceFull -PathType Container)) { throw "SourceRoot does not exist: $sourceFull" }
$sourceItem = Get-Item -LiteralPath $sourceFull
if (($sourceItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'SourceRoot reparse points are not accepted.' }
$reparse = @(Get-ChildItem -LiteralPath $sourceFull -Recurse -Force | Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 })
if ($reparse.Count -gt 0) { throw "Source tree contains reparse points: $($reparse[0].FullName)" }
$mapReceiptPath = [IO.Path]::ChangeExtension($mapFull, '.receipt.json')

$allFiles = @(Get-ChildItem -LiteralPath $sourceFull -Recurse -Force -File | ForEach-Object {
        $relative = Get-RelativePath $sourceFull $_.FullName
        if (-not (Test-HashExcludedPath $relative)) {
            [pscustomobject]@{ FullName = $_.FullName; RelativePath = $relative; IsText = (Test-TextExtension $_.FullName); Length = $_.Length }
        }
    } | Sort-Object RelativePath)
if ($allFiles.Count -eq 0) { throw 'No source files found.' }
$totalBytes = [long](($allFiles | Measure-Object -Property Length -Sum).Sum)
if ($allFiles.Count -gt $MaxFiles) { throw "Source file limit exceeded: $($allFiles.Count) > $MaxFiles" }
if ($totalBytes -gt $MaxBytes) { throw "Source byte limit exceeded: $totalBytes > $MaxBytes" }
$eligibleFiles = @($allFiles | Where-Object { -not (Test-ExcludedPath $_.RelativePath) })
$excludedProFiles = @($allFiles | Where-Object { $_.RelativePath.ToLowerInvariant().StartsWith('src/pro/') -or $_.RelativePath.ToLowerInvariant() -eq 'src/pro' }).Count
$sourceTreeHash = Get-TreeSha256 $allFiles

# An in-place map is reusable only when the accepted remap manifest still
# accompanies the transformed checkout, or when the source tree itself is
# unchanged.  The previous early replay check trusted the map alone, so a
# restore/recovery followed by a fresh run could silently reuse a stale map.
# Keep replay idempotent while forcing regeneration after recovery or drift.
if ((Test-PathWithin $mapFull $controlDirectory) -and (Test-Path -LiteralPath $mapFull -PathType Leaf) -and (Test-Path -LiteralPath $mapReceiptPath -PathType Leaf)) {
    $existingInPlaceMap = Get-Content -LiteralPath $mapFull -Raw -Encoding UTF8 | ConvertFrom-Json
    $existingInPlaceReceipt = Get-Content -LiteralPath $mapReceiptPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $manifestPath = Join-Path $controlDirectory 'es-remap-manifest.json'
    $remapReceiptPath = Join-Path $controlDirectory 'es-remap-receipt.json'
    $manifestAccepted = $false
    if ((Test-Path -LiteralPath $manifestPath -PathType Leaf) -and (Test-Path -LiteralPath $remapReceiptPath -PathType Leaf)) {
        try {
            $manifestState = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $remapState = Get-Content -LiteralPath $remapReceiptPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $manifestAccepted = ([string]$manifestState.status -eq 'written-in-place' -and [string]$remapState.status -eq 'passed')
        } catch { $manifestAccepted = $false }
    }
    $sourceTreeUnchanged = ([string]$existingInPlaceMap.source.treeSha256 -eq $sourceTreeHash)
    if ([string]$existingInPlaceReceipt.status -eq 'passed' -and [string]$existingInPlaceReceipt.planHash -eq [string]$existingInPlaceMap.generation.planHash -and ($manifestAccepted -or $sourceTreeUnchanged)) {
        $existingInPlaceReceipt | Add-Member -NotePropertyName idempotentReplay -NotePropertyValue $true -Force
        $existingInPlaceReceipt | Add-Member -NotePropertyName sourceTreeReplay -NotePropertyValue $sourceTreeUnchanged -Force
        $existingInPlaceReceipt | ConvertTo-Json -Depth 12
        exit 0
    }
}

$resolvedToken = $SourceToken
if ([string]::IsNullOrWhiteSpace($resolvedToken)) {
    $packagePath = Join-Path $sourceFull 'package.json'
    if (Test-Path -LiteralPath $packagePath -PathType Leaf) {
        try {
            $package = Get-Content -LiteralPath $packagePath -Raw -Encoding UTF8 | ConvertFrom-Json
            $resolvedToken = [string]$package.name
            if ($resolvedToken.Contains('/')) { $resolvedToken = $resolvedToken.Substring($resolvedToken.LastIndexOf('/') + 1) }
        } catch { $resolvedToken = '' }
    }
}
if ([string]::IsNullOrWhiteSpace($resolvedToken)) { $resolvedToken = [IO.Path]::GetFileName($sourceFull) }
$rootIdentifier = ConvertTo-Identifier $resolvedToken
if ([string]::IsNullOrWhiteSpace($rootIdentifier)) { throw 'Unable to derive a source identifier; pass -SourceToken.' }

$textSeeds = [Collections.Generic.List[string]]::new()
$packageBrandingWarnings = [Collections.Generic.List[string]]::new()
function Add-TextSeed([string]$Value) {
    $normalized = if ($null -eq $Value) { '' } else { $Value.Trim() }
    if (-not [string]::IsNullOrWhiteSpace($normalized) -and -not ($textSeeds -ccontains $normalized)) {
        [void]$textSeeds.Add($normalized)
    }
}
Add-TextSeed $resolvedToken
Add-TextSeed ([IO.Path]::GetFileName($sourceFull))
foreach ($seed in @($SourceTextTokens)) { Add-TextSeed ([string]$seed) }
$packagePath = Join-Path $sourceFull 'package.json'
if (Test-Path -LiteralPath $packagePath -PathType Leaf) {
    try {
        $packageForBranding = Get-Content -LiteralPath $packagePath -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($propertyName in @('name', 'productName', 'displayName', 'author', 'publisher')) {
            $property = $packageForBranding.PSObject.Properties[$propertyName]
            if ($null -eq $property) { continue }
            if ($property.Value -is [string]) { Add-TextSeed ([string]$property.Value) }
            elseif ($property.Value) {
                if ($property.Value.name) { Add-TextSeed ([string]$property.Value.name) }
                if ($property.Value.email) { Add-TextSeed ([string]$property.Value.email) }
            }
        }
        foreach ($contributor in @($packageForBranding.contributors)) {
            if ($contributor -is [string]) { Add-TextSeed ([string]$contributor) }
            elseif ($contributor) {
                if ($contributor.name) { Add-TextSeed ([string]$contributor.name) }
                if ($contributor.email) { Add-TextSeed ([string]$contributor.email) }
            }
        }
    } catch {
        [void]$packageBrandingWarnings.Add('package.json branding seeds unavailable: ' + $_.Exception.Message)
    }
}

# Declaration discovery is lexical rather than AST-backed. Preserve a small
# high-confidence set of language/tooling identifiers so whole-text mode does
# not turn executable protocol tokens (`git`, `path`, `run`, ... ) into
# `ESgit`/`ESpath` and break scripts or package tooling. A name that carries
# the repository identity (for example `getDyadAppPath`) is still remapped.
$genericIdentifierPrefixes = @(
    'git','path','node','run','request','response','entry','route','ref','assert','fake',
    'main','index','url','http','https','fs','os','util','process','console','log','error',
    'warn','info','debug','config','option','state','context','client','server','token',
    'value','result','data','type','name','body','header','method','query','param','stream',
    'buffer','read','write','open','close','create','update','delete','remove','resolve','parse',
    'format','load','save','handle','check','build','make','copy','move','exist','find','get',
    'set','is','has','can','should','ensure','validate','normalize','serialize','deserialize',
    'encode','decode'
)
function Test-GenericIdentifier([string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Name)) { return $false }
    if ($Name -match [regex]::Escape($rootIdentifier)) { return $false }
    foreach ($prefix in $genericIdentifierPrefixes) {
        if ($Name -match "^(?i:$([regex]::Escape($prefix)))(?:[\p{Lu}_]|$)") { return $true }
    }
    return $false
}

$declarations = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
$existingIdentifiers = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$declarationFiles = @($eligibleFiles | Where-Object { $_.IsText -and (Test-DeclarationExtension $_.FullName) })
foreach ($file in $declarationFiles) {
    $text = Get-StrictUtf8Text $file.FullName
    $scanText = ConvertTo-DeclarationScanText $text
    $matches = @()
    $matches += @([Regex]::Matches($scanText, '\b(?:class|interface|enum|struct|record|function)\s+([\p{L}_][\p{L}\p{N}_]*)'))
    # `type` is ambiguous in TypeScript (`import type`, annotations, etc.).
    # Require an alias body so only declarations such as `type Foo = ...` or
    # `type Foo { ... }` become candidates.
    $matches += @([Regex]::Matches($scanText, '\btype\s+([\p{L}_][\p{L}\p{N}_]*)\s*(?==|\{)'))
    foreach ($match in $matches) {
        $name = [string]$match.Groups[1].Value
        [void]$existingIdentifiers.Add($name)
        if ($name.Length -gt 1 -and $name -notmatch '^ES(?:[\p{L}\p{N}_]|$)' -and -not (Test-GenericIdentifier $name) -and -not $declarations.ContainsKey($name)) { $declarations.Add($name, 'declaration') }
    }
}

$reserved = @('Object','String','Boolean','Number','Date','Promise','Array','Map','Set','Error','Console','Math','JSON','React','UnityEngine')
$rules = [Collections.Generic.List[object]]::new()
$rootEs = if ($rootIdentifier -match '^ES') { $rootIdentifier } else { "ES$rootIdentifier" }
$rules.Add([pscustomobject]@{ source = $rootIdentifier; es = $rootEs; kind = 'repository-root'; strategy = 'transparent-prefix' })
# Enumerate hashtable entries explicitly.  Large PowerShell hashtables can expose
# an accidental member/key named `Keys` through member enumeration; the typed
# dictionary plus GetEnumerator() keeps source identity and value unambiguous.
$declarationNames = @($declarations.GetEnumerator() | ForEach-Object { [string]$_.Key } | Sort-Object)
foreach ($name in $declarationNames) {
    if ($reserved -contains $name) { continue }
    $candidate = "ES$name"
    if ($candidate -ceq $rootEs -and $name -cne $rootIdentifier) { continue }
    if ($existingIdentifiers.Contains($candidate)) { throw "Generated identity collision: $name -> $candidate already exists in source declarations." }
    $rules.Add([pscustomobject]@{ source = $name; es = $candidate; kind = [string]$declarations[$name]; strategy = 'transparent-prefix' })
}

# Text/path seeds are explicit identities too.  Add identifier-safe case
# variants to the token map so package names and path segments are rewritten
# even when they never appear as declarations.  Free-form seeds remain in the
# separate textReplacements list below.
$textRules = [Collections.Generic.List[object]]::new()
foreach ($seed in @($textSeeds | Sort-Object @{Expression={([string]$_).Length};Descending=$true}, @{Expression={[string]$_};Ascending=$true})) {
    $seedText = [string]$seed
    if ([string]::IsNullOrWhiteSpace($seedText)) { continue }
    $seedIdentifier = ConvertTo-Identifier $seedText
    if ($seedIdentifier -and $seedIdentifier -notmatch '^ES') {
        $seedEs = "ES$seedIdentifier"
        if (-not (@($rules | Where-Object { [string]$_.source -ceq $seedIdentifier }).Count)) {
            $rules.Add([pscustomobject]@{ source = $seedIdentifier; es = $seedEs; kind = 'repository-text'; strategy = 'transparent-prefix' })
        }
    }
    $seedEsText = if ($seedText -match '^(?i:ES)') { $seedText } else { "ES$seedText" }
    $textRules.Add([pscustomobject]@{ source = $seedText; es = $seedEsText; kind = 'text'; strategy = 'exact-boundary' })
    foreach ($variant in @($seedText.ToLowerInvariant(), $seedText.ToUpperInvariant())) {
        if ($variant -cne $seedText -and $variant.Length -gt 0) {
            $variantEs = if ($variant -match '^(?i:ES)') { $variant } else { "ES$variant" }
            $textRules.Add([pscustomobject]@{ source = $variant; es = $variantEs; kind = 'text-case-variant'; strategy = 'exact-boundary' })
        }
    }
    # Human-facing project names commonly use title case even when the
    # package/root identifier is lowercase (for example `dyad` -> `Dyad`).
    # Include that deterministic variant so README/UI/config prose cannot
    # retain the old project label while protocol words such as `git` remain
    # untouched because they are not text seeds.
    if ($seedText -match '[\p{L}]' -and $seedText -notmatch '^(?i:ES)') {
        $titleVariant = [char]::ToUpperInvariant($seedText[0]) + $seedText.Substring(1).ToLowerInvariant()
        if ($titleVariant -cne $seedText -and $titleVariant.Length -gt 0) {
            $textRules.Add([pscustomobject]@{ source = $titleVariant; es = "ES$titleVariant"; kind = 'text-case-variant'; strategy = 'exact-boundary' })
        }
    }
}
$textRules = @($textRules | Group-Object { "$(($_.source).ToString())`t$(($_.es).ToString())" } -CaseSensitive | ForEach-Object { $_.Group[0] } | Sort-Object @{Expression={([string]$_.source).Length};Descending=$true}, @{Expression={[string]$_.source};Ascending=$true})
$duplicateSource = @($rules | Group-Object { [string]$_.source } -CaseSensitive | Where-Object Count -gt 1)
$duplicateEs = @($rules | Group-Object { [string]$_.es } -CaseSensitive | Where-Object Count -gt 1)
if ($duplicateSource.Count -gt 0 -or $duplicateEs.Count -gt 0) { throw 'Generated mapping contains duplicate identities.' }

$revision = Get-SourceRevision $sourceFull $SourceRevision
$mapId = 'es-auto-symbol-map.' + $rootIdentifier.ToLowerInvariant() + '.v1'
$planInput = @("mapId=$mapId", "sourceRevision=$revision", "sourceTreeSha256=$sourceTreeHash", (($rules | ForEach-Object { "$($_.source)=$($_.es)" }) -join "`n")) -join "`n"
$sha = [Security.Cryptography.SHA256]::Create()
try { $planHash = ([BitConverter]::ToString($sha.ComputeHash([Text.UTF8Encoding]::new($false).GetBytes($planInput)))).Replace('-', '').ToLowerInvariant() }
finally { $sha.Dispose() }

$map = [ordered]@{
    schemaVersion = 1
    mapId = $mapId
    mode = 'transparent-identity-remap'
    cryptographicObfuscation = $false
    provenancePreserved = $true
    sourceNamespacePolicy = 'source names remain in evidence; generated ES names are explicit identities'
    source = [ordered]@{ rootName = [IO.Path]::GetFileName($sourceFull); revision = $revision; treeSha256 = $sourceTreeHash; scannedFileCount = $allFiles.Count; scannedBytes = $totalBytes; excludedLicensedTreeFiles = $excludedProFiles }
    generation = [ordered]@{ planHash = $planHash; rootToken = $rootIdentifier; declarationCount = $declarations.Count; maxFiles = $MaxFiles; maxBytes = $MaxBytes }
    symbols = @($rules | Sort-Object source)
    textReplacements = @($textRules)
    collisionPolicy = 'reject duplicate ES identity; do not create compatibility aliases'
    licensePolicy = 'retain LICENSE/NOTICE and exclude src/pro or unresolved dependency provenance'
    wholeRepositoryPolicy = 'in-place mode rewrites text metadata/docs/comments/UI/configuration; LICENSE/NOTICE and .git remain protected'
    warnings = @($packageBrandingWarnings)
    nonClaims = @('This map does not grant license clearance.', 'Lexical remap is not AST, compiler, or semantic-equivalence proof.', 'No Unity or Runtime compatibility is proven.', 'Git history is not rewritten by this tool.')
}

$receiptPath = $mapReceiptPath
$mapLocator = if (Test-PathWithin $mapFull $controlDirectory) { '.es-migration/es-symbol-map.json' } else { $mapFull }
$receiptLocator = if (Test-PathWithin $mapFull $controlDirectory) { '.es-migration/es-symbol-map.receipt.json' } else { $receiptPath }
$receipt = [ordered]@{ skillName = 'es-open-source-migration'; case = 'automatic-transparent-symbol-map'; status = 'passed'; evidenceLevel = 'static'; evidenceWarnings = @($packageBrandingWarnings); receiptPath = $receiptLocator; sourceRefs = @("map:$mapId", "source-tree:$sourceTreeHash", "plan-hash:$planHash"); timestampUtc = [DateTime]::UtcNow.ToString('o'); planHash = $planHash; mapPath = $mapLocator; nonClaims = $map.nonClaims }

if (Test-Path -LiteralPath $mapFull -PathType Leaf) {
    $existingMap = Get-Content -LiteralPath $mapFull -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string]$existingMap.generation.planHash -ne $planHash) { throw "OutputMapPath plan hash conflict; refusing overwrite: $mapFull" }
    if (-not (Test-Path -LiteralPath $receiptPath -PathType Leaf)) { throw "Output map receipt is missing: $receiptPath" }
    $existingReceipt = Get-Content -LiteralPath $receiptPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string]$existingReceipt.planHash -ne $planHash -or [string]$existingReceipt.status -ne 'passed') { throw "Output map receipt drift: $receiptPath" }
    $receipt.idempotentReplay = $true
    $receipt | ConvertTo-Json -Depth 12
    exit 0
}

$mapDirectory = Split-Path -Parent $mapFull
if (-not (Test-Path -LiteralPath $mapDirectory)) { New-Item -ItemType Directory -Path $mapDirectory -Force | Out-Null }
# Re-scan before publication so a source edit/addition/deletion during symbol
# extraction cannot be accepted under a stale tree hash.
$currentFiles = @(Get-ChildItem -LiteralPath $sourceFull -Recurse -Force -File | ForEach-Object {
        $currentRelative = Get-RelativePath $sourceFull $_.FullName
        if (-not (Test-HashExcludedPath $currentRelative)) {
            [pscustomobject]@{ FullName = $_.FullName; RelativePath = $currentRelative; IsText = (Test-TextExtension $_.FullName); Length = $_.Length }
        }
    } | Sort-Object RelativePath)
if ($currentFiles.Count -ne $allFiles.Count -or (Get-TreeSha256 $currentFiles) -ne $sourceTreeHash) {
    throw 'Source tree drifted during symbol-map generation; map was not published.'
}

# Publish through a private staging directory.  The receipt is moved first and
# the map last, so an interruption cannot leave an apparently accepted map
# without its receipt; a subsequent run can safely regenerate when the map is
# absent.
$staging = "$mapFull.__staging-$planHash-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $staging -Force | Out-Null
$stagedMap = Join-Path $staging ([IO.Path]::GetFileName($mapFull))
$stagedReceipt = Join-Path $staging ([IO.Path]::GetFileName($receiptPath))
[IO.File]::WriteAllText($stagedMap, ($map | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($stagedReceipt, ($receipt | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
Move-Item -LiteralPath $stagedReceipt -Destination $receiptPath -Force
Move-Item -LiteralPath $stagedMap -Destination $mapFull -Force
$receipt | ConvertTo-Json -Depth 12
