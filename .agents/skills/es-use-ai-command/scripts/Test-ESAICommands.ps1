[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new()

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (& git rev-parse --show-toplevel 2>$null)
}
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    throw 'Cannot resolve the Git project root. Pass -ProjectRoot.'
}

$ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot.Trim())
$commandRoot = Join-Path $ProjectRoot 'Assets\Plugins\ES\AICommands'
if (-not (Test-Path -LiteralPath $commandRoot -PathType Container)) {
    throw "AICommands directory not found: $commandRoot"
}

$strictUtf8 = [Text.UTF8Encoding]::new($false, $true)
$catalogRelativePath = 'Assets/Plugins/ES/AICommands/AICommandCatalog.json'
$catalogPath = Join-Path $ProjectRoot ($catalogRelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
$findScriptPath = Join-Path $PSScriptRoot 'Find-ESAICommands.ps1'
$navigationFileNames = @(
    'README.md',
    ([string]::Concat(([int[]](21629,20196,21512,38598,32034,24341,95,65,73,21629,20196,46,109,100) | ForEach-Object { [char]$_ })))
)
$allowedRoles = @('information', 'review', 'controlled-execution', 'candidate-generation', 'handover')
$allowedWriteModes = @('read-only', 'scoped-write', 'candidate-only', 'documentation-write', 'external-run')
$metadataPatterns = @(
    @{ name = 'command-type'; pattern = '(?m)^\u547D\u4EE4\u7C7B\u578B\uFF1A\s*\S+' },
    @{ name = 'default-write'; pattern = '(?m)^\u9ED8\u8BA4\u6539\u6587\u4EF6\uFF1A\s*\S+' },
    @{ name = 'risk-level'; pattern = '(?m)^\u98CE\u9669\u7B49\u7EA7\uFF1A\s*L[123](?:[/\s\u3002\uFF0C,]|$)' }
)

if (-not (Test-Path -LiteralPath $findScriptPath -PathType Leaf)) {
    throw "AICommand discovery script does not exist: $findScriptPath"
}
$parserTokens = $null
$parserErrors = $null
[System.Management.Automation.Language.Parser]::ParseFile($findScriptPath, [ref]$parserTokens, [ref]$parserErrors) | Out-Null
if ($parserErrors.Count -gt 0) {
    throw "AICommand discovery script syntax is invalid: $($parserErrors[0])"
}

function Add-UniqueError {
    param(
        [Collections.Generic.List[string]]$Errors,
        [string]$Message
    )
    if (-not $Errors.Contains($Message)) {
        $Errors.Add($Message)
    }
}

function Test-ProjectRelativePath {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path) -or [IO.Path]::IsPathRooted($Path)) { return $false }
    $normalized = $Path.Replace('\', '/').Trim()
    if (-not $normalized.StartsWith('Assets/Plugins/ES/AICommands/', [StringComparison]::Ordinal)) { return $false }
    foreach ($segment in $normalized.Split('/')) {
        if ([string]::IsNullOrEmpty($segment) -or $segment -eq '.' -or $segment -eq '..') { return $false }
    }
    return $normalized.EndsWith('.md', [StringComparison]::OrdinalIgnoreCase)
}

function Test-CatalogId {
    param([string]$Value)
    return -not [string]::IsNullOrWhiteSpace($Value) -and $Value.Length -ge 3 -and $Value.Length -le 80 `
        -and $Value -match '^[a-z0-9][a-z0-9.-]*$'
}

$catalogErrors = New-Object Collections.Generic.List[string]
$catalogEntries = @()
if (-not (Test-Path -LiteralPath $catalogPath -PathType Leaf)) {
    $catalogErrors.Add("Catalog does not exist: $catalogRelativePath")
}
else {
    try {
        $catalogText = $strictUtf8.GetString([IO.File]::ReadAllBytes($catalogPath))
        if ($catalogText.Contains([char]0xFFFD)) { $catalogErrors.Add('Catalog contains Unicode replacement character U+FFFD.') }
        $catalog = $catalogText | ConvertFrom-Json
        if ($null -eq $catalog -or $catalog.schemaVersion -ne 1) {
            $catalogErrors.Add('Catalog schemaVersion must be 1.')
        }
        elseif ($null -eq $catalog.commands) {
            $catalogErrors.Add('Catalog commands array is missing.')
        }
        else {
            $catalogEntries = @($catalog.commands)
        }
    }
    catch {
        $catalogErrors.Add("Catalog strict UTF-8 decoding or JSON parsing failed: $($_.Exception.Message)")
    }
}

$catalogIds = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
$catalogPaths = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
foreach ($entry in $catalogEntries) {
    if ($null -eq $entry) {
        $catalogErrors.Add('Catalog contains a null command entry.')
        continue
    }
    $id = [string]$entry.id
    $path = [string]$entry.path
    $role = [string]$entry.role
    $risk = [string]$entry.riskLevel
    $writeMode = [string]$entry.writeMode
    if (-not (Test-CatalogId $id)) { $catalogErrors.Add("Catalog has invalid command id: $id") }
    elseif (-not $catalogIds.Add($id)) { $catalogErrors.Add("Catalog contains duplicate command id: $id") }
    if (-not (Test-ProjectRelativePath $path)) { $catalogErrors.Add("Catalog has unmanaged or invalid command path: $path") }
    elseif (-not $catalogPaths.Add($path)) { $catalogErrors.Add("Catalog contains duplicate command path: $path") }
    if ([string]::IsNullOrWhiteSpace([string]$entry.title) -or ([string]$entry.title).Trim().Length -gt 80) {
        $catalogErrors.Add("Catalog title missing or too long: $id")
    }
    if ([string]::IsNullOrWhiteSpace([string]$entry.summary) -or ([string]$entry.summary).Trim().Length -gt 240) {
        $catalogErrors.Add("Catalog summary missing or too long: $id")
    }
    if ([string]::IsNullOrWhiteSpace([string]$entry.keywords) -or ([string]$entry.keywords).Trim().Length -gt 320) {
        $catalogErrors.Add("Catalog keywords missing or too long: $id")
    }
    if ($allowedRoles -notcontains $role) { $catalogErrors.Add("Catalog role is not allowed for ${id}: $role") }
    if ($allowedWriteModes -notcontains $writeMode) { $catalogErrors.Add("Catalog writeMode is not allowed for ${id}: $writeMode") }
    if (@('L1', 'L2', 'L3') -notcontains $risk) { $catalogErrors.Add("Catalog riskLevel is not allowed for ${id}: $risk") }
    if (($role -in @('information', 'review')) -and $writeMode -ne 'read-only') {
        $catalogErrors.Add("Catalog role/writeMode conflict for ${id}: $role must be read-only")
    }
    if ($role -eq 'candidate-generation' -and $writeMode -ne 'candidate-only') {
        $catalogErrors.Add("Catalog role/writeMode conflict for ${id}: candidate-generation must be candidate-only")
    }
    if ($role -eq 'handover' -and $writeMode -ne 'documentation-write') {
        $catalogErrors.Add("Catalog role/writeMode conflict for ${id}: handover must be documentation-write")
    }
    if ($role -eq 'controlled-execution' -and $writeMode -notin @('scoped-write', 'external-run')) {
        $catalogErrors.Add("Catalog role/writeMode conflict for ${id}: controlled-execution must be scoped-write or external-run")
    }
}

$results = New-Object Collections.Generic.List[object]
$files = Get-ChildItem -LiteralPath $commandRoot -Filter '*.md' -File -Recurse | Sort-Object FullName

foreach ($file in $files) {
    $errors = New-Object Collections.Generic.List[string]
    $relativeFile = $file.FullName.Substring($ProjectRoot.Length).TrimStart([IO.Path]::DirectorySeparatorChar).Replace([IO.Path]::DirectorySeparatorChar, '/')
    $isNavigation = $navigationFileNames -contains $file.Name
    try {
        $text = $strictUtf8.GetString([IO.File]::ReadAllBytes($file.FullName))
    }
    catch {
        $errors.Add("Strict UTF-8 decoding failed: $($_.Exception.Message)")
        $text = ''
    }

    if ($text.Contains([char]0xFFFD)) {
        $errors.Add('Contains Unicode replacement character U+FFFD.')
    }

    foreach ($metadata in $metadataPatterns) {
        if ($text -notmatch $metadata.pattern) {
            Add-UniqueError $errors "Missing or invalid metadata: $($metadata.name)"
        }
    }

    if (-not $isNavigation) {
        if (-not $catalogPaths.Contains($relativeFile)) {
            Add-UniqueError $errors 'Executable AICommand is missing from AICommandCatalog.json.'
        }
        # Existing strong-constraint commands may use a domain-specific middle section, but every
        # task contract must retain a reading gate and a delivery contract. Use metadata-derived
        # heading semantics so Windows PowerShell 5.1 source decoding cannot corrupt Chinese names.
        $headingLines = @([regex]::Matches($text, '(?m)^##\s+(.+?)\s*$') | ForEach-Object { $_.Groups[1].Value })
        $requiredRead = [string]::Concat(([int[]](24517,39035,20808,35835) | ForEach-Object { [char]$_ }))
        $delivery = [string]::Concat(([int[]](20132,20184,26684,24335) | ForEach-Object { [char]$_ }))
        if ($headingLines -notcontains $requiredRead) {
            Add-UniqueError $errors 'Missing required section: required-reading gate.'
        }
        if ($headingLines -notcontains $delivery) {
            Add-UniqueError $errors 'Missing required section: delivery contract.'
        }
    }
    elseif ($catalogPaths.Contains($relativeFile)) {
        Add-UniqueError $errors 'Navigation document must not appear in AICommandCatalog.json.'
    }

    $pathMatches = [regex]::Matches($text, '(?m)^(Assets|Documentation|ES|Packages)/[^\r\n`]+')
    foreach ($match in $pathMatches) {
        $relativePath = $match.Value.Trim()
        $candidate = Join-Path $ProjectRoot ($relativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $candidate)) {
            $errors.Add("Referenced path does not exist: $relativePath")
        }
    }

    $results.Add([pscustomobject]@{
        file = $relativeFile
        role = if ($isNavigation) { 'navigation' } else { 'contract' }
        valid = $errors.Count -eq 0
        errors = $errors.ToArray()
    })
}

$actualContractPaths = @($results | Where-Object { $_.role -eq 'contract' } | ForEach-Object { $_.file })
foreach ($catalogPathEntry in $catalogPaths) {
    if ($actualContractPaths -notcontains $catalogPathEntry) {
        $catalogErrors.Add("Catalog references a missing executable contract: $catalogPathEntry")
    }
}

if ($catalogErrors.Count -eq 0 -and $catalogEntries.Count -gt 0) {
    try {
        $discoveryOutput = & $findScriptPath -ProjectRoot $ProjectRoot -Query ([string]$catalogEntries[0].id) -MaxResults 1 -Json
        $discovery = $discoveryOutput | ConvertFrom-Json
        if (
            $null -eq $discovery -or $discovery.totalContracts -ne $catalogEntries.Count -or
            $discovery.returnedCount -ne 1 -or $discovery.matchedCount -lt $discovery.returnedCount -or
            @($discovery.candidates).Count -ne $discovery.returnedCount
        ) {
            $catalogErrors.Add('AICommand discovery output is malformed or not bounded.')
        }
        elseif ([string]$discovery.candidates[0].id -ne [string]$catalogEntries[0].id) {
            $catalogErrors.Add('AICommand discovery did not resolve the requested exact command id.')
        }
    }
    catch {
        $catalogErrors.Add("AICommand discovery script execution failed: $($_.Exception.Message)")
    }
}

$invalid = @($results | Where-Object { -not $_.valid })
$report = [pscustomobject]@{
    projectRoot = $ProjectRoot
    commandCount = $actualContractPaths.Count
    navigationCount = @($results | Where-Object { $_.role -eq 'navigation' }).Count
    catalogCount = $catalogEntries.Count
    catalogValid = $catalogErrors.Count -eq 0
    invalidCount = $invalid.Count + $catalogErrors.Count
    valid = $invalid.Count -eq 0 -and $catalogErrors.Count -eq 0
    catalogErrors = $catalogErrors.ToArray()
    commands = $results.ToArray()
}

if ($Json) {
    $report | ConvertTo-Json -Depth 8
}
else {
    "AICommands: $($report.commandCount), navigation: $($report.navigationCount), catalog: $($report.catalogCount), invalid: $($report.invalidCount)"
    foreach ($error in $catalogErrors) {
        "[CATALOG INVALID] $error"
    }
    foreach ($item in $invalid) {
        "[INVALID] $($item.file)"
        foreach ($error in $item.errors) { "  - $error" }
    }
}

if (-not $report.valid) { exit 1 }
