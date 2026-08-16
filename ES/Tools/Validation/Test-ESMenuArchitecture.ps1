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

function ConvertFrom-UnicodeEscape([string]$Value) {
    return ('"' + $Value + '"') | ConvertFrom-Json
}

$esRoot = ConvertFrom-UnicodeEscape '\u3010ES\u3011/'
$legacyTopRoot = ConvertFrom-UnicodeEscape '\u3010es\u3011/'
$automationDevelopment = ConvertFrom-UnicodeEscape '\u81ea\u52a8\u5316\u4e0e\u5f00\u53d1'
$legacyCompatibility = ConvertFrom-UnicodeEscape '\u9057\u7559\u517c\u5bb9'
$assetSamples = ConvertFrom-UnicodeEscape '\u793a\u4f8b'
$componentDevelopmentValidation = ConvertFrom-UnicodeEscape '\u5f00\u53d1\u4e0e\u9a8c\u8bc1'

$activeSourceRoots = @(
    'Assets/Plugins/ES',
    'Assets/Scripts/ESLogic'
)
$compatibilityRoot = 'Assets/Plugins/ES/Obsolete'
$currentDocumentPaths = @(
    'Assets/Plugins/ES/AIWarnings',
    'Assets/Plugins/ES/AICommands',
    'Documentation',
    '.agents/README.md',
    'ES/Automation/AI',
    'ES/Automation/Contracts',
    '.agents/skills',
    'ES/Documentation/Status',
    ('ES/Documentation/StaticSite/ESFrameworkPublish_' +
        (ConvertFrom-UnicodeEscape '\u6280\u672f\u6587\u6863') + '.html'),
    'ES/Documentation/StaticSite/DOCUMENT_SYNC.md',
    'ES/Documentation/StaticSite/DOCUMENT_READER_STANDARD.md'
)

$topAllowed = @(
    (ConvertFrom-UnicodeEscape '\u5e38\u7528\u7a97\u53e3'),
    (ConvertFrom-UnicodeEscape '\u5185\u5bb9\u5236\u4f5c'),
    (ConvertFrom-UnicodeEscape '\u9879\u76ee\u914d\u7f6e'),
    (ConvertFrom-UnicodeEscape '\u8d44\u6e90\u4e0e\u53d1\u5e03'),
    (ConvertFrom-UnicodeEscape '\u9a8c\u8bc1\u4e0e\u8bca\u65ad'),
    $automationDevelopment
)
$assetAllowed = @(
    (ConvertFrom-UnicodeEscape '\u5185\u5bb9'),
    (ConvertFrom-UnicodeEscape '\u914d\u7f6e'),
    (ConvertFrom-UnicodeEscape '\u8d44\u6e90\u7ba1\u7ebf'),
    $assetSamples
)
$componentAllowed = @(
    (ConvertFrom-UnicodeEscape '\u57fa\u7840\u8bbe\u65bd'),
    (ConvertFrom-UnicodeEscape '\u89d2\u8272\u4e0e\u4ea4\u4e92'),
    (ConvertFrom-UnicodeEscape '\u76f8\u673a\u4e0e\u8868\u73b0'),
    'UI',
    (ConvertFrom-UnicodeEscape '\u8d44\u6e90'),
    $componentDevelopmentValidation
)
$legacyPaths = @(
    ($esRoot + (ConvertFrom-UnicodeEscape '\u573a\u666f\u4e0e\u5bf9\u8c61') + '/'),
    ($esRoot + (ConvertFrom-UnicodeEscape '\u8fd0\u884c\u65f6\u8bca\u65ad') + '/'),
    ($esRoot + (ConvertFrom-UnicodeEscape '\u9879\u76ee\u8bbe\u7f6e') + '/'),
    ($esRoot + (ConvertFrom-UnicodeEscape '\u81ea\u52a8\u5316') + '/'),
    ($esRoot + (ConvertFrom-UnicodeEscape '\u5f00\u53d1\u4e0e\u7ef4\u62a4') + '/'),
    ($esRoot + (ConvertFrom-UnicodeEscape '\u5b89\u88c5\u4e0e\u96c6\u6210') + '/'),
    ($esRoot + (ConvertFrom-UnicodeEscape '\u793a\u4f8b\u4e0e\u6d4b\u8bd5') + '/'),
    ($esRoot + (ConvertFrom-UnicodeEscape '\u5df2\u5e9f\u5f03') + '/')
)
$legacyConstants = @(
    'SCENE_OBJECTS_PATH',
    'RUNTIME_DIAGNOSTICS_PATH',
    'PROJECT_SETTINGS_PATH',
    'DEVELOPMENT_MAINTENANCE_PATH',
    'INSTALL_INTEGRATION_PATH',
    'SAMPLES_TESTS_PATH',
    'OBSOLETE_PATH'
)

$strictUtf8 = [Text.UTF8Encoding]::new($false, $true)
$ascii = [Text.Encoding]::ASCII
$compatibilityRootFull = [IO.Path]::GetFullPath((Join-Path $ProjectRoot $compatibilityRoot))
$issues = New-Object Collections.Generic.List[object]

function Get-RelativePath([string]$FullPath) {
    return $FullPath.Substring($ProjectRoot.Length).TrimStart(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar).Replace('\', '/')
}

function Add-Issue(
    [string]$Kind,
    [string]$Path,
    [string]$Value,
    [string]$Message,
    [int]$LineNumber = 0
) {
    $issues.Add([pscustomobject]@{
        kind = $Kind
        path = $Path
        lineNumber = $LineNumber
        value = $Value
        message = $Message
    })
}

function Get-Files([string[]]$Paths, [string[]]$Extensions) {
    $result = New-Object Collections.Generic.List[IO.FileInfo]
    foreach ($item in $Paths) {
        $fullPath = if ([IO.Path]::IsPathRooted($item)) {
            [IO.Path]::GetFullPath($item)
        }
        else {
            [IO.Path]::GetFullPath((Join-Path $ProjectRoot $item))
        }
        if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
            $file = Get-Item -LiteralPath $fullPath
            if ($Extensions -contains $file.Extension.ToLowerInvariant()) {
                $result.Add($file)
            }
            continue
        }
        if (-not (Test-Path -LiteralPath $fullPath -PathType Container)) {
            Add-Issue 'MissingScope' (Get-RelativePath $fullPath) '' 'Configured scan path does not exist.'
            continue
        }
        foreach ($file in Get-ChildItem -LiteralPath $fullPath -Recurse -File) {
            if ($Extensions -contains $file.Extension.ToLowerInvariant()) {
                $result.Add($file)
            }
        }
    }
    return @($result | Sort-Object FullName -Unique)
}

function Get-FirstSegment([string]$Path, [string]$Prefix) {
    return $Path.Substring($Prefix.Length).Split('/')[0]
}

function Test-LiteralPath(
    [string]$Kind,
    [string]$MenuPath,
    [string]$RelativePath,
    [bool]$Compatibility
) {
    $topPrefix = $esRoot
    $assetContextPrefix = 'Assets/Create/' + $esRoot
    if ($Kind -eq 'MenuItem') {
        if ($MenuPath.StartsWith($topPrefix, [StringComparison]::Ordinal)) {
            $segment = Get-FirstSegment $MenuPath $topPrefix
            if ($topAllowed -notcontains $segment) {
                Add-Issue 'InvalidTopCategory' $RelativePath $MenuPath 'MenuItem uses a non-six-domain top category.'
            }
            $expectedCompatibilityPath = $esRoot + $automationDevelopment + '/' + $legacyCompatibility + '/'
            if ($Compatibility -and -not $MenuPath.StartsWith($expectedCompatibilityPath, [StringComparison]::Ordinal)) {
                Add-Issue 'InvalidCompatibilityMenu' $RelativePath $MenuPath 'Obsolete top-menu entries must live under the legacy compatibility category.'
            }
            return
        }
        if ($MenuPath.StartsWith($assetContextPrefix, [StringComparison]::Ordinal)) {
            $segment = Get-FirstSegment $MenuPath $assetContextPrefix
            if ($assetAllowed -notcontains $segment) {
                Add-Issue 'InvalidAssetCategory' $RelativePath $MenuPath 'Assets/Create MenuItem uses an invalid asset category.'
            }
            $expectedCompatibilityAssetPath = 'Assets/Create/' + $esRoot + $assetSamples + '/' + $legacyCompatibility + '/'
            if ($Compatibility -and -not $MenuPath.StartsWith($expectedCompatibilityAssetPath, [StringComparison]::Ordinal)) {
                Add-Issue 'InvalidCompatibilityAssetMenu' $RelativePath $MenuPath 'Obsolete asset entries must live under the legacy compatibility asset category.'
            }
            return
        }
    }
    elseif ($Kind -eq 'CreateAssetMenu' -and $MenuPath.StartsWith($topPrefix, [StringComparison]::Ordinal)) {
        $segment = Get-FirstSegment $MenuPath $topPrefix
        if ($assetAllowed -notcontains $segment) {
            Add-Issue 'InvalidAssetCategory' $RelativePath $MenuPath 'CreateAssetMenu uses an invalid asset category.'
        }
        $expectedCompatibilityAssetPath = $esRoot + $assetSamples + '/' + $legacyCompatibility + '/'
        if ($Compatibility -and -not $MenuPath.StartsWith($expectedCompatibilityAssetPath, [StringComparison]::Ordinal)) {
            Add-Issue 'InvalidCompatibilityAssetMenu' $RelativePath $MenuPath 'Obsolete assets must live under the legacy compatibility asset category.'
        }
        return
    }
    elseif ($Kind -eq 'AddComponentMenu' -and $MenuPath.StartsWith($topPrefix, [StringComparison]::Ordinal)) {
        $segment = Get-FirstSegment $MenuPath $topPrefix
        if ($componentAllowed -notcontains $segment) {
            Add-Issue 'InvalidComponentCategory' $RelativePath $MenuPath 'AddComponentMenu uses an invalid component category.'
        }
        $expectedCompatibilityComponentPath = $esRoot + $componentDevelopmentValidation + '/' + $legacyCompatibility + '/'
        if ($Compatibility -and -not $MenuPath.StartsWith($expectedCompatibilityComponentPath, [StringComparison]::Ordinal)) {
            Add-Issue 'InvalidCompatibilityComponentMenu' $RelativePath $MenuPath 'Obsolete components must live under the legacy compatibility component category.'
        }
        return
    }

    foreach ($wrongRoot in @($legacyTopRoot, '[ES]/', 'ES/', 'Window/ES/', 'Tools/ES/')) {
        if ($MenuPath.StartsWith($wrongRoot, [StringComparison]::Ordinal)) {
            Add-Issue 'InvalidRoot' $RelativePath $MenuPath 'ES-owned menu paths must use the exact branded ES root.'
            return
        }
    }
}

function New-SourceCount {
    return [ordered]@{
        filesWithMenuAttributes = 0
        menuItemAttributes = 0
        menuItemLiteralArguments = 0
        menuItemSymbolicArguments = 0
        createAssetMenuAttributes = 0
        createAssetMenuLiteralPaths = 0
        createAssetMenuUnresolvedPaths = 0
        addComponentMenuAttributes = 0
        addComponentMenuLiteralArguments = 0
        addComponentMenuSymbolicArguments = 0
    }
}

function Test-SourceFiles([IO.FileInfo[]]$Files, [bool]$Compatibility) {
    $count = New-SourceCount
    foreach ($file in $Files) {
        $bytes = [IO.File]::ReadAllBytes($file.FullName)
        $asciiText = $ascii.GetString($bytes)
        if ($asciiText -notmatch 'MenuItem|CreateAssetMenu|AddComponentMenu|ExecuteMenuItem|MenuPath|MenuItemPathDefine') {
            continue
        }
        $relativePath = Get-RelativePath $file.FullName
        try {
            $source = $strictUtf8.GetString($bytes)
        }
        catch {
            Add-Issue 'InvalidUtf8' $relativePath '' "Strict UTF-8 decoding failed: $($_.Exception.Message)"
            continue
        }
        foreach ($legacyPath in $legacyPaths) {
            $start = 0
            while (($index = $source.IndexOf($legacyPath, $start, [StringComparison]::Ordinal)) -ge 0) {
                $lineNumber = 1 + ([regex]::Matches($source.Substring(0, $index), "`n")).Count
                Add-Issue 'LegacySourcePath' $relativePath $legacyPath 'Source still contains a legacy menu path.' $lineNumber
                $start = $index + $legacyPath.Length
            }
        }

        $menuAttributes = [regex]::Matches($source, '\[\s*(?:UnityEditor\.)?MenuItem\s*\(', 'IgnoreCase')
        $menuLiterals = [regex]::Matches($source, '\[\s*(?:UnityEditor\.)?MenuItem\s*\(\s*"(?<path>[^"]+)"', 'IgnoreCase')
        $count.menuItemAttributes += $menuAttributes.Count
        $count.menuItemLiteralArguments += $menuLiterals.Count
        $count.menuItemSymbolicArguments += $menuAttributes.Count - $menuLiterals.Count
        foreach ($match in $menuLiterals) {
            Test-LiteralPath 'MenuItem' $match.Groups['path'].Value $relativePath $Compatibility
        }

        $createAttributes = [regex]::Matches($source, '\[\s*CreateAssetMenu\s*\((?<body>.*?)\)\s*\]', 'Singleline,IgnoreCase')
        $count.createAssetMenuAttributes += $createAttributes.Count
        foreach ($match in $createAttributes) {
            $pathMatch = [regex]::Match($match.Groups['body'].Value, 'menuName\s*=\s*"(?<path>[^"]+)"', 'IgnoreCase')
            if (-not $pathMatch.Success) {
                $count.createAssetMenuUnresolvedPaths++
                continue
            }
            $count.createAssetMenuLiteralPaths++
            Test-LiteralPath 'CreateAssetMenu' $pathMatch.Groups['path'].Value $relativePath $Compatibility
        }

        $componentAttributes = [regex]::Matches($source, '\[\s*AddComponentMenu\s*\(', 'IgnoreCase')
        $componentLiterals = [regex]::Matches($source, '\[\s*AddComponentMenu\s*\(\s*"(?<path>[^"]+)"', 'IgnoreCase')
        $count.addComponentMenuAttributes += $componentAttributes.Count
        $count.addComponentMenuLiteralArguments += $componentLiterals.Count
        $count.addComponentMenuSymbolicArguments += $componentAttributes.Count - $componentLiterals.Count
        foreach ($match in $componentLiterals) {
            Test-LiteralPath 'AddComponentMenu' $match.Groups['path'].Value $relativePath $Compatibility
        }

        if ($menuAttributes.Count -gt 0 -or $createAttributes.Count -gt 0 -or $componentAttributes.Count -gt 0) {
            $count.filesWithMenuAttributes++
        }

        foreach ($legacyConstant in $legacyConstants) {
            if ($source -match ('MenuItemPathDefine\.' + [regex]::Escape($legacyConstant) + '\b')) {
                Add-Issue 'LegacyMenuConstant' $relativePath $legacyConstant 'Active project source still references a legacy compatibility constant.'
            }
        }
    }
    return [pscustomobject]$count
}

$allActiveSourceFiles = Get-Files $activeSourceRoots @('.cs')
$activeSourceFiles = @($allActiveSourceFiles | Where-Object {
    -not $_.FullName.StartsWith($compatibilityRootFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
})
$compatibilityFiles = Get-Files @($compatibilityRoot) @('.cs')
$activeSourceCounts = Test-SourceFiles $activeSourceFiles $false
$compatibilityCounts = Test-SourceFiles $compatibilityFiles $true

$documentFiles = Get-Files $currentDocumentPaths @(
    '.md', '.html', '.txt', '.json', '.yaml', '.yml', '.ps1'
)
$legacyDocumentHitCount = 0
foreach ($file in $documentFiles) {
    $relativePath = Get-RelativePath $file.FullName
    try {
        $text = $strictUtf8.GetString([IO.File]::ReadAllBytes($file.FullName))
    }
    catch {
        Add-Issue 'InvalidUtf8' $relativePath '' "Strict UTF-8 decoding failed: $($_.Exception.Message)"
        continue
    }
    foreach ($legacyPath in $legacyPaths) {
        $start = 0
        while (($index = $text.IndexOf($legacyPath, $start, [StringComparison]::Ordinal)) -ge 0) {
            $legacyDocumentHitCount++
            $lineNumber = 1 + ([regex]::Matches($text.Substring(0, $index), "`n")).Count
            Add-Issue 'LegacyDocumentPath' $relativePath $legacyPath 'Current technical documentation still contains a legacy menu path.' $lineNumber
            $start = $index + $legacyPath.Length
        }
    }
}

$report = [pscustomobject]@{
    projectRoot = $ProjectRoot
    policy = [pscustomobject]@{
        activeProjectSourceRoots = $activeSourceRoots
        compatibilitySourceRoot = $compatibilityRoot
        currentDocumentPaths = $currentDocumentPaths
        activeSourceExcludesCompatibilityRoot = $true
        thirdPartyAssetsExcluded = $true
        conditionalCompilationEvaluated = $false
        attributeOccurrenceDeduplication = 'none'
        note = 'The active project-owned source scope includes ES tests and examples. Attribute totals count source occurrences, including validator overloads. Literal and symbolic arguments are separated; only literal paths are category-parsed.'
    }
    activeProjectSource = $activeSourceCounts
    compatibility = $compatibilityCounts
    currentDocumentation = [pscustomobject]@{
        checkedFileCount = $documentFiles.Count
        legacyPathHitCount = $legacyDocumentHitCount
    }
    valid = $issues.Count -eq 0
    issueCount = $issues.Count
    issues = $issues.ToArray()
}

if ($Json) {
    $report | ConvertTo-Json -Depth 8
}
else {
    "ES menu architecture: valid=$($report.valid), issues=$($report.issueCount)"
    "Active project source: MenuItem=$($activeSourceCounts.menuItemAttributes) (literal=$($activeSourceCounts.menuItemLiteralArguments), symbolic=$($activeSourceCounts.menuItemSymbolicArguments)); CreateAssetMenu=$($activeSourceCounts.createAssetMenuAttributes); AddComponentMenu=$($activeSourceCounts.addComponentMenuAttributes) (literal=$($activeSourceCounts.addComponentMenuLiteralArguments), symbolic=$($activeSourceCounts.addComponentMenuSymbolicArguments))"
    "Compatibility: MenuItem=$($compatibilityCounts.menuItemAttributes); CreateAssetMenu=$($compatibilityCounts.createAssetMenuAttributes); AddComponentMenu=$($compatibilityCounts.addComponentMenuAttributes)"
    "Current documentation: files=$($documentFiles.Count), legacy path hits=$legacyDocumentHitCount"
    $issues | Format-Table kind, path, lineNumber, value, message -AutoSize -Wrap
}

if (-not $report.valid) {
    exit 1
}
