[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [switch]$Json,
    [switch]$StrictCompleteness
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
        [Collections.Generic.List[string]]$Issues,
        [string]$Message
    )
    if (-not $Issues.Contains($Message)) {
        $Issues.Add($Message)
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

function Get-UnicodeText {
    param([int[]]$CodePoints)
    return [string]::Concat(($CodePoints | ForEach-Object { [char]$_ }))
}

function Get-ContractMetadataValue {
    param(
        [string]$Text,
        [string]$FieldName
    )
    $match = [regex]::Match($Text, ('(?m)^' + [regex]::Escape($FieldName) + '\uFF1A\s*(.+?)\s*$'))
    if ($match.Success) {
        return $match.Groups[1].Value.Trim()
    }
    return ''
}

function Get-ExpectedCatalogSemantics {
    param(
        [string]$CommandType,
        [string]$DefaultWrite
    )

    $information = Get-UnicodeText @(20449,24687,34917,20840)
    $handover = Get-UnicodeText @(20132,25509,27785,28096)
    $candidate = Get-UnicodeText @(20505,36873,20869,23481,29983,25104)
    $safeExecution = Get-UnicodeText @(23433,20840,25191,34892)
    $p0GameCore = Get-UnicodeText @(80,48,32,28216,25103,26680,24515,25645,24314)
    $no = Get-UnicodeText @(21542)
    $yes = Get-UnicodeText @(26159)
    $allow = Get-UnicodeText @(20801,35768)
    $onlyAllow = Get-UnicodeText @(20165,20801,35768)

    if ([string]::IsNullOrWhiteSpace($CommandType) -or [string]::IsNullOrWhiteSpace($DefaultWrite)) {
        throw 'Contract command metadata is missing.'
    }
    if ($CommandType.StartsWith($information, [StringComparison]::Ordinal)) {
        if (-not $DefaultWrite.StartsWith($no, [StringComparison]::Ordinal)) {
            throw 'Information contract must declare no default file write.'
        }
        return [pscustomobject]@{ role = 'information'; writeMode = 'read-only' }
    }
    if ($CommandType.StartsWith($handover, [StringComparison]::Ordinal)) {
        if (-not $DefaultWrite.StartsWith($yes, [StringComparison]::Ordinal)) {
            throw 'Handover contract must declare a documentation write.'
        }
        return [pscustomobject]@{ role = 'handover'; writeMode = 'documentation-write' }
    }
    if ($CommandType.StartsWith($candidate, [StringComparison]::Ordinal)) {
        if (-not $DefaultWrite.StartsWith($onlyAllow, [StringComparison]::Ordinal)) {
            throw 'Candidate-generation contract must declare its candidate-only path.'
        }
        return [pscustomobject]@{ role = 'candidate-generation'; writeMode = 'candidate-only' }
    }
    if (
        $CommandType.StartsWith($safeExecution, [StringComparison]::Ordinal) -or
        $CommandType.StartsWith($p0GameCore, [StringComparison]::Ordinal)
    ) {
        if ($DefaultWrite.StartsWith($no, [StringComparison]::Ordinal)) {
            return [pscustomobject]@{ role = 'controlled-execution'; writeMode = 'external-run' }
        }
        if (
            $DefaultWrite.StartsWith($yes, [StringComparison]::Ordinal) -or
            $DefaultWrite.StartsWith($allow, [StringComparison]::Ordinal)
        ) {
            return [pscustomobject]@{ role = 'controlled-execution'; writeMode = 'scoped-write' }
        }
        throw 'Controlled-execution contract has no recognized write boundary.'
    }
    if (-not $DefaultWrite.StartsWith($no, [StringComparison]::Ordinal)) {
        throw 'Review contract must declare no default file write.'
    }
    return [pscustomobject]@{ role = 'review'; writeMode = 'read-only' }
}

function Invoke-DiscoveryIsolationRegression {
    param(
        [string]$ProjectRoot,
        [string]$FindScriptPath,
        [object]$CatalogEntry
    )

    $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("ESAICommands-Discovery-" + [guid]::NewGuid().ToString('N'))
    $junctionPath = $null
    try {
        $projectPath = Join-Path $tempRoot 'Project'
        $commandPath = Join-Path $projectPath 'Assets\Plugins\ES\AICommands'
        $outsidePath = Join-Path $tempRoot 'Outside'
        [IO.Directory]::CreateDirectory($commandPath) | Out-Null
        [IO.Directory]::CreateDirectory($outsidePath) | Out-Null

        $fixtureCatalog = [pscustomobject]@{
            schemaVersion = 1
            catalogTitle = 'Discovery isolation fixture'
            catalogPurpose = 'Regression fixture only.'
            commands = @($CatalogEntry)
        }
        $fixtureCatalogBytes = [Text.UTF8Encoding]::new($false).GetBytes(($fixtureCatalog | ConvertTo-Json -Depth 6))
        $catalogDestination = Join-Path $commandPath 'AICommandCatalog.json'
        [IO.File]::WriteAllBytes($catalogDestination, $fixtureCatalogBytes)
        [IO.File]::WriteAllBytes((Join-Path $outsidePath 'AICommandCatalog.json'), $fixtureCatalogBytes)

        $relativeContractPath = ([string]$CatalogEntry.path).Replace('/', [IO.Path]::DirectorySeparatorChar)
        $contractDestination = Join-Path $projectPath $relativeContractPath
        [IO.Directory]::CreateDirectory((Split-Path -Parent $contractDestination)) | Out-Null
        [IO.File]::WriteAllBytes($contractDestination, [byte[]](0xFF, 0xFE, 0x00, 0x01))

        $discoveryOutput = & $FindScriptPath -ProjectRoot $projectPath -CommandPath ([string]$CatalogEntry.path) -Json
        $discovery = $discoveryOutput | ConvertFrom-Json
        if ($null -eq $discovery -or $discovery.returnedCount -ne 1 -or [string]$discovery.candidates[0].id -ne [string]$CatalogEntry.id) {
            throw 'Discovery unexpectedly required a contract Markdown body.'
        }

        $junctionPath = Join-Path $projectPath 'Assets\Plugins\ES\AICommands'
        Remove-Item -LiteralPath $junctionPath -Recurse -Force
        New-Item -ItemType Junction -Path $junctionPath -Target $outsidePath | Out-Null
        if (
            -not (Test-Path -LiteralPath $junctionPath -PathType Container) -or
            (((Get-Item -LiteralPath $junctionPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0)
        ) {
            throw 'Could not create the isolated junction regression fixture.'
        }

        $rejected = $false
        try {
            & $FindScriptPath -ProjectRoot $projectPath -Query 'test' -Json | Out-Null
        }
        catch {
            $rejected = $true
        }
        if (-not $rejected) {
            throw 'Discovery accepted an AICommand directory behind a junction or symlink.'
        }
    }
    finally {
        if (-not [string]::IsNullOrWhiteSpace($junctionPath) -and (Test-Path -LiteralPath $junctionPath)) {
            $junctionItem = Get-Item -LiteralPath $junctionPath -Force
            if (($junctionItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                [IO.Directory]::Delete($junctionPath)
            }
        }
        if (Test-Path -LiteralPath $tempRoot) {
            Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
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
            $abc = $catalog.contractStandard.abcBindingProjection
            $requiredAbcCapabilities = @('bounded-tool-action','failure-recovery','branch-evaluation','state-transition-guard','environment-trust-gate','audit-evidence-chain')
            if ($null -eq $abc -or [string]$abc.providerId -ne 'es-ai-command' -or [string]$abc.fallback -ne 'explicit-only') {
                $catalogErrors.Add('Catalog abcBindingProjection is missing or has an unsafe provider/fallback.')
            }
            else {
                foreach ($capability in $requiredAbcCapabilities) {
                    if (@($abc.allowedCapabilities) -notcontains $capability) {
                        $catalogErrors.Add("Catalog abcBindingProjection is missing capability: $capability")
                    }
                }
                if ([string]$abc.missingCapabilityDisposition -ne 'blocked' -or
                    [string]$abc.semanticMismatchDisposition -ne 'replan' -or
                    [string]$abc.missingEvidenceDisposition -ne 'claim-cap') {
                    $catalogErrors.Add('Catalog abcBindingProjection dispositions must be blocked/replan/claim-cap.')
                }
                $profileModes = @('read-only','candidate-only','documentation-write','scoped-write','external-run')
                foreach ($mode in $profileModes) {
                    $binding = @($abc.profileBindings | Where-Object { [string]$_.writeMode -eq $mode })
                    if ($binding.Count -ne 1 -or @($binding[0].requiredCapabilities).Count -eq 0) {
                        $catalogErrors.Add("Catalog abcBindingProjection is missing profile binding: $mode")
                    }
                }
            }
        }
    }
    catch {
        $catalogErrors.Add("Catalog strict UTF-8 decoding or JSON parsing failed: $($_.Exception.Message)")
    }
}

$catalogIds = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
$catalogPaths = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
$catalogByPath = @{}
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
    if (-not [string]::IsNullOrWhiteSpace($path) -and -not $catalogByPath.ContainsKey($path)) {
        $catalogByPath[$path] = $entry
    }
}

# Keep the human navigation index closed over the machine catalog. Read complete lines
# so filenames containing spaces remain intact.
$navigationFileName = [string]::Concat(([int[]](21629,20196,21512,38598,32034,24341,95,65,73,21629,20196,46,109,100) | ForEach-Object { [char]$_ }))
$navigationRelativePath = 'Assets/Plugins/ES/AICommands/' + $navigationFileName
$navigationPath = Join-Path $ProjectRoot ($navigationRelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
if (-not (Test-Path -LiteralPath $navigationPath -PathType Leaf)) {
    $catalogErrors.Add("Navigation index does not exist: $navigationRelativePath")
}
else {
    try {
        $navigationText = $strictUtf8.GetString([IO.File]::ReadAllBytes($navigationPath))
        $navigationPaths = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
        foreach ($line in ($navigationText -split "`r?`n")) {
            $candidate = $line.Trim()
            if ($candidate -match '^Assets/Plugins/ES/AICommands/.+\.md$' -and
                $candidate -ne 'Assets/Plugins/ES/AICommands/README.md' -and
                $candidate -ne $navigationRelativePath) {
                if (-not $navigationPaths.Add($candidate)) {
                    $catalogErrors.Add("Navigation index contains a duplicate contract path: $candidate")
                }
            }
        }
        foreach ($catalogPathEntry in $catalogPaths) {
            if (-not $navigationPaths.Contains($catalogPathEntry)) {
                $catalogErrors.Add("Navigation index is missing catalog contract: $catalogPathEntry")
            }
        }
        foreach ($navigationPathEntry in $navigationPaths) {
            if (-not $catalogPaths.Contains($navigationPathEntry)) {
                $catalogErrors.Add("Navigation index references a non-catalog contract: $navigationPathEntry")
            }
        }
        if ($catalogPaths.Count -gt 0) {
            $probePath = @($catalogPaths)[0]
            $probeMissing = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
            foreach ($pathEntry in $navigationPaths) {
                if ($pathEntry -ne $probePath) { [void]$probeMissing.Add($pathEntry) }
            }
            if (@($catalogPaths | Where-Object { -not $probeMissing.Contains($_) }).Count -eq 0) {
                $catalogErrors.Add('Navigation index negative probe failed to detect a missing catalog contract.')
            }
            $probeExtra = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
            foreach ($pathEntry in $navigationPaths) { [void]$probeExtra.Add($pathEntry) }
            [void]$probeExtra.Add('Assets/Plugins/ES/AICommands/__negative_probe__.md')
            if (@($probeExtra | Where-Object { -not $catalogPaths.Contains($_) }).Count -eq 0) {
                $catalogErrors.Add('Navigation index negative probe failed to detect a non-catalog contract.')
            }
        }
    }
    catch {
        $catalogErrors.Add("Navigation index strict UTF-8 read failed: $($_.Exception.Message)")
    }
}

$results = New-Object Collections.Generic.List[object]
$completenessDiagnostics = New-Object Collections.Generic.List[object]
$files = Get-ChildItem -LiteralPath $commandRoot -Filter '*.md' -File -Recurse | Sort-Object FullName

foreach ($file in $files) {
    $validationErrors = New-Object Collections.Generic.List[string]
    $relativeFile = $file.FullName.Substring($ProjectRoot.Length).TrimStart([IO.Path]::DirectorySeparatorChar).Replace([IO.Path]::DirectorySeparatorChar, '/')
    $isNavigation = $navigationFileNames -contains $file.Name
    try {
        $text = $strictUtf8.GetString([IO.File]::ReadAllBytes($file.FullName))
    }
    catch {
        $validationErrors.Add("Strict UTF-8 decoding failed: $($_.Exception.Message)")
        $text = ''
    }

    if ($text.Contains([char]0xFFFD)) {
        $validationErrors.Add('Contains Unicode replacement character U+FFFD.')
    }

    foreach ($metadata in $metadataPatterns) {
        if ($text -notmatch $metadata.pattern) {
            Add-UniqueError $validationErrors "Missing or invalid metadata: $($metadata.name)"
        }
    }

    if (-not $isNavigation) {
        if (-not $catalogPaths.Contains($relativeFile)) {
            Add-UniqueError $validationErrors 'Executable AICommand is missing from AICommandCatalog.json.'
        }
        elseif ($catalogByPath.ContainsKey($relativeFile)) {
            try {
                $catalogEntry = $catalogByPath[$relativeFile]
                $declaredIdMatch = [regex]::Match($text, '(?im)^(?:\u547D\u4EE4\s*ID|commandId)\s*[\u003A\uFF1A]\s*`?([a-z0-9][a-z0-9.-]*)`?\s*$')
                if ($declaredIdMatch.Success -and $declaredIdMatch.Groups[1].Value -ne [string]$catalogEntry.id) {
                    Add-UniqueError $validationErrors 'Catalog id differs from the command ID declared in the contract body.'
                }
                $commandType = Get-ContractMetadataValue $text (Get-UnicodeText @(21629,20196,31867,22411))
                $defaultWrite = Get-ContractMetadataValue $text (Get-UnicodeText @(40664,35748,25913,25991,20214))
                $bodyRisk = Get-ContractMetadataValue $text (Get-UnicodeText @(39118,38505,31561,32423))
                $expected = Get-ExpectedCatalogSemantics $commandType $defaultWrite
                if ($bodyRisk -notmatch '^L[123](?:[/\s\u3002\uFF0C,]|$)') {
                    Add-UniqueError $validationErrors 'Contract risk level is missing or invalid.'
                }
                elseif (-not $bodyRisk.StartsWith([string]$catalogEntry.riskLevel, [StringComparison]::Ordinal)) {
                    Add-UniqueError $validationErrors 'Catalog riskLevel differs from the contract body.'
                }
                if ([string]$catalogEntry.role -ne $expected.role) {
                    Add-UniqueError $validationErrors 'Catalog role differs from the contract body semantics.'
                }
                if ([string]$catalogEntry.writeMode -ne $expected.writeMode) {
                    Add-UniqueError $validationErrors 'Catalog writeMode differs from the contract body semantics.'
                }

                # Report-only contract completeness baseline. These observations intentionally do
                # not change valid/invalid status: migration must be staged before strict gating.
                # Completeness fields require an explicit section/field marker. Incidental prose
                # (for example, a sentence mentioning failure) is retained only as a hint and
                # must not satisfy StrictCompleteness.
                $hasCancellation = [regex]::IsMatch($text, '(?im)^##\s+.*(?:cancellation|取消)|^cancellation\s*:')
                $hasRecovery = [regex]::IsMatch($text, '(?im)^##\s+.*(?:recovery|恢复)|^recovery\s*:')
                $hasEvidence = [regex]::IsMatch($text, '(?im)^##\s+.*(?:evidenceRef|证据)|^evidenceRef\s*:')
                $hasValidation = [regex]::IsMatch($text, '(?im)^##\s+.*(?:validation|验证)|^validation\s*:')
                $riskRange = [regex]::IsMatch($bodyRisk, '(?i)L[123]\s*(?:/|至|到|和|-)\s*L[123]')
                $scopeAllow = [regex]::IsMatch($text, '(?im)allowRoots|allowPaths|允许根|允许路径')
                $scopeDeny = [regex]::IsMatch($text, '(?im)denyPaths|拒绝路径|禁止目录|禁止路径')
                $requiredCapabilities = switch ([string]$catalogEntry.writeMode) {
                    'read-only' { @('branch-evaluation', 'audit-evidence-chain') }
                    'candidate-only' { @('bounded-tool-action', 'failure-recovery', 'audit-evidence-chain') }
                    'scoped-write' { @('bounded-tool-action', 'state-transition-guard', 'failure-recovery', 'audit-evidence-chain') }
                    'external-run' { @('environment-trust-gate', 'state-transition-guard', 'failure-recovery', 'audit-evidence-chain') }
                    'documentation-write' { @('state-transition-guard', 'failure-recovery', 'audit-evidence-chain') }
                    default { @() }
                }
                $diagnostic = [pscustomobject]@{
                    id = [string]$catalogEntry.id
                    path = $relativeFile
                    role = [string]$catalogEntry.role
                    writeMode = [string]$catalogEntry.writeMode
                    riskLevel = [string]$catalogEntry.riskLevel
                    riskRange = $riskRange
                    fields = [pscustomobject]@{
                        cancellation = $hasCancellation
                        recovery = $hasRecovery
                        validation = $hasValidation
                        evidence = $hasEvidence
                    }
                    scope = [pscustomobject]@{ allow = $scopeAllow; deny = $scopeDeny; symmetric = ($scopeAllow -and $scopeDeny) }
                    requiredCapabilities = @($requiredCapabilities)
                    abcBindingPresent = ((@($abc.profileBindings | Where-Object { [string]$_.writeMode -eq [string]$catalogEntry.writeMode }).Count -eq 1) -or [regex]::IsMatch($text, '(?im)abcBinding|bounded-tool-action|audit-evidence-chain'))
                    mode = 'report-only'
                }
                $completenessDiagnostics.Add($diagnostic)
                if ($StrictCompleteness) {
                    if ($riskRange) { Add-UniqueError $validationErrors 'Strict completeness: contract risk level must be a single L1/L2/L3 value.' }
                    if ($catalogEntry.writeMode -eq 'scoped-write' -and -not $diagnostic.scope.symmetric) {
                        Add-UniqueError $validationErrors 'Strict completeness: scoped-write requires symmetric allow and deny path declarations.'
                    }
                    $requiredFieldNames = switch ([string]$catalogEntry.writeMode) {
                        'read-only' { @('validation', 'evidence') }
                        'candidate-only' { @('cancellation', 'recovery', 'validation', 'evidence') }
                        'documentation-write' { @('cancellation', 'recovery', 'validation', 'evidence') }
                        'scoped-write' { @('cancellation', 'recovery', 'validation', 'evidence') }
                        'external-run' { @('cancellation', 'recovery', 'validation', 'evidence') }
                        default { @() }
                    }
                    foreach ($fieldName in $requiredFieldNames) {
                        if (-not [bool]$diagnostic.fields.$fieldName) {
                            Add-UniqueError $validationErrors "Strict completeness: missing required field marker '$fieldName'."
                        }
                    }
                }
            }
            catch {
                Add-UniqueError $validationErrors "Catalog/body semantic validation failed: $($_.Exception.Message)"
            }
        }
        # Existing strong-constraint commands may use a domain-specific middle section, but every
        # task contract must retain a reading gate and a delivery contract. Use metadata-derived
        # heading semantics so Windows PowerShell 5.1 source decoding cannot corrupt Chinese names.
        $headingLines = @([regex]::Matches($text, '(?m)^##\s+(.+?)\s*$') | ForEach-Object { $_.Groups[1].Value })
        $requiredRead = [string]::Concat(([int[]](24517,39035,20808,35835) | ForEach-Object { [char]$_ }))
        $delivery = [string]::Concat(([int[]](20132,20184,26684,24335) | ForEach-Object { [char]$_ }))
        if ($headingLines -notcontains $requiredRead) {
            Add-UniqueError $validationErrors 'Missing required section: required-reading gate.'
        }
        if ($headingLines -notcontains $delivery) {
            Add-UniqueError $validationErrors 'Missing required section: delivery contract.'
        }
    }
    elseif ($catalogPaths.Contains($relativeFile)) {
        Add-UniqueError $validationErrors 'Navigation document must not appear in AICommandCatalog.json.'
    }

    $pathMatches = [regex]::Matches($text, '(?m)^(Assets|Documentation|ES|Packages)/[^\r\n`]+')
    foreach ($match in $pathMatches) {
        $relativePath = $match.Value.Trim()
        $candidate = Join-Path $ProjectRoot ($relativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $candidate)) {
            $validationErrors.Add("Referenced path does not exist: $relativePath")
        }
    }

    $results.Add([pscustomobject]@{
        file = $relativeFile
        role = if ($isNavigation) { 'navigation' } else { 'contract' }
        valid = $validationErrors.Count -eq 0
        errors = $validationErrors.ToArray()
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

        $exactPathOutput = & $findScriptPath -ProjectRoot $ProjectRoot -CommandPath ([string]$catalogEntries[0].path) -Json
        $exactPath = $exactPathOutput | ConvertFrom-Json
        if (
            $null -eq $exactPath -or $exactPath.selectionMode -ne 'exact-path' -or
            $exactPath.returnedCount -ne 1 -or [string]$exactPath.candidates[0].id -ne [string]$catalogEntries[0].id
        ) {
            $catalogErrors.Add('AICommand exact-path discovery did not resolve the requested catalog entry.')
        }

        foreach ($catalogEntry in $catalogEntries) {
            $exactOutput = & $findScriptPath -ProjectRoot $ProjectRoot -CommandPath ([string]$catalogEntry.path) -Json
            $exact = $exactOutput | ConvertFrom-Json
            if (
                $null -eq $exact -or $exact.returnedCount -ne 1 -or
                [string]$exact.candidates[0].id -ne [string]$catalogEntry.id
            ) {
                $catalogErrors.Add("AICommand exact-path discovery mismatch: $($catalogEntry.id)")
                break
            }
        }

        $truncationQuery = Get-UnicodeText @(26816,26597)
        $truncationOutput = & $findScriptPath -ProjectRoot $ProjectRoot -Query $truncationQuery -Json
        $truncation = $truncationOutput | ConvertFrom-Json
        if (
            $null -eq $truncation -or $truncation.matchedCount -le 6 -or $truncation.returnedCount -ne 6 -or
            @($truncation.candidates).Count -ne 6
        ) {
            $catalogErrors.Add('AICommand discovery default result cap is not enforced or not disclosed.')
        }

        $reviewEntry = @($catalogEntries | Where-Object { $_.role -eq 'review' -and $_.riskLevel -eq 'L1' })[0]
        $filteredOutput = & $findScriptPath -ProjectRoot $ProjectRoot -Query ([string]$reviewEntry.id) -Role review -RiskLevel L1 -Json
        $filtered = $filteredOutput | ConvertFrom-Json
        if (
            $null -eq $filtered -or $filtered.returnedCount -ne 1 -or
            [string]$filtered.candidates[0].id -ne [string]$reviewEntry.id -or
            [string]$filtered.candidates[0].role -ne 'review' -or
            [string]$filtered.candidates[0].riskLevel -ne 'L1'
        ) {
            $catalogErrors.Add('AICommand discovery role/risk filter did not preserve the requested contract.')
        }

        $negativeCases = @(
            @('-Query', ' ', '-Json'),
            @('-CommandPath', 'Assets/Plugins/ES/AICommands/../README.md', '-Json'),
            @('-Query', $truncationQuery, '-MaxResults', '7', '-Json')
        )
        foreach ($negativeArguments in $negativeCases) {
            $rejected = $false
            try {
                & $findScriptPath -ProjectRoot $ProjectRoot @negativeArguments | Out-Null
            }
            catch {
                $rejected = $true
            }
            if (-not $rejected) {
                $catalogErrors.Add('AICommand discovery accepted an invalid query, path, or result limit.')
                break
            }
        }

        Invoke-DiscoveryIsolationRegression -ProjectRoot $ProjectRoot -FindScriptPath $findScriptPath -CatalogEntry $catalogEntries[0]
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
    completeness = [pscustomobject]@{
        mode = if ($StrictCompleteness) { 'strict' } else { 'report-only' }
        contracts = $completenessDiagnostics.ToArray()
        summary = [pscustomobject]@{
            missingCancellation = @($completenessDiagnostics | Where-Object { -not $_.fields.cancellation }).Count
            missingRecovery = @($completenessDiagnostics | Where-Object { -not $_.fields.recovery }).Count
            missingValidation = @($completenessDiagnostics | Where-Object { -not $_.fields.validation }).Count
            missingEvidence = @($completenessDiagnostics | Where-Object { -not $_.fields.evidence }).Count
            riskRanges = @($completenessDiagnostics | Where-Object { $_.riskRange }).Count
            scopeAmbiguous = @($completenessDiagnostics | Where-Object { $_.writeMode -eq 'scoped-write' -and -not $_.scope.symmetric }).Count
            abcBindingMissing = @($completenessDiagnostics | Where-Object { -not $_.abcBindingPresent }).Count
        }
    }
    commands = $results.ToArray()
}

if ($Json) {
    $report | ConvertTo-Json -Depth 8
}
else {
    "AICommands: $($report.commandCount), navigation: $($report.navigationCount), catalog: $($report.catalogCount), invalid: $($report.invalidCount)"
    foreach ($catalogIssue in $catalogErrors) {
        "[CATALOG INVALID] $catalogIssue"
    }
    foreach ($item in $invalid) {
        "[INVALID] $($item.file)"
        foreach ($itemIssue in $item.errors) { "  - $itemIssue" }
    }
}

if (-not $report.valid) { exit 1 }
