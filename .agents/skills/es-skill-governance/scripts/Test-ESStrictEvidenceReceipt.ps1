[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SkillPath,
    [Parameter(Mandatory = $true)][string]$EvidencePath,
    [string]$ProjectRoot,
    [ValidateRange(1, 8760)][int]$MaxEvidenceAgeHours = 168
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$skill = (Resolve-Path -LiteralPath $SkillPath -ErrorAction Stop).Path
$root = if ($ProjectRoot) {
    (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
} else {
    Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $skill))
}
$root = $root.TrimEnd('\', '/')
$prefix = $root + [IO.Path]::DirectorySeparatorChar

function Get-ReceiptProperty([object]$Receipt, [string]$Name) {
    return $Receipt.PSObject.Properties[$Name]
}

function Assert-Sha256Field([object]$Receipt, [string]$Name) {
    $property = Get-ReceiptProperty $Receipt $Name
    if ($null -eq $property -or -not ($property.Value -is [string]) -or [string]$property.Value -notmatch '^[a-fA-F0-9]{64}$') {
        throw "$Name must be a SHA-256 value"
    }
}

function Assert-NonEmptyStringArray([object]$Receipt, [string]$Name) {
    $property = Get-ReceiptProperty $Receipt $Name
    if ($null -eq $property -or -not ($property.Value -is [Array]) -or @($property.Value).Count -eq 0) {
        throw "$Name must be a non-empty JSON string array"
    }

    foreach ($item in @($property.Value)) {
        if (-not ($item -is [string]) -or [string]::IsNullOrWhiteSpace([string]$item)) {
            throw "$Name must contain only non-empty strings"
        }
    }

    return @($property.Value)
}

function Test-PathInsideProject([string]$Path) {
    return $Path.Equals($root, [StringComparison]::OrdinalIgnoreCase) -or
        $Path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)
}

function Assert-AuthorizedPaths([object]$Receipt) {
    $paths = @(Assert-NonEmptyStringArray $Receipt 'authorizedPaths')
    foreach ($authorizedPath in $paths) {
        $pathText = [string]$authorizedPath
        if ($pathText -ne $pathText.Trim() -or
            [IO.Path]::IsPathRooted($pathText) -or
            $pathText -match '^[a-zA-Z]:' -or
            $pathText -match '^[\\/]{2}') {
            throw "authorizedPaths must contain project-relative paths: $pathText"
        }

        try {
            $fullPath = [IO.Path]::GetFullPath([IO.Path]::Combine($root, $pathText))
        } catch {
            throw "authorizedPaths contains an invalid project-relative path: $pathText"
        }
        if (-not (Test-PathInsideProject $fullPath)) {
            throw "authorizedPaths escapes ProjectRoot: $pathText"
        }
    }
}

function Relative([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not (Test-PathInsideProject $full) -or $full.Equals($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Path escapes ProjectRoot or does not identify a project file'
    }
    return $full.Substring($root.Length + 1).Replace('\', '/')
}

function Hash([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

if (-not (Test-Path -LiteralPath $EvidencePath -PathType Leaf)) {
    throw "Evidence receipt not found: $EvidencePath. The evidence claim is unavailable; project action authority is unchanged."
}
$receipt = (Resolve-Path -LiteralPath $EvidencePath -ErrorAction Stop).Path
if (-not (Test-PathInsideProject $receipt) -or $receipt.Equals($root, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'EvidencePath must identify a receipt inside ProjectRoot'
}
$raw = [IO.File]::ReadAllText($receipt, (New-Object Text.UTF8Encoding($false, $true)))
try {
    $r = $raw | ConvertFrom-Json
} catch {
    throw 'Evidence receipt is not strict UTF-8 JSON'
}

foreach ($field in @('skillName', 'case', 'status', 'evidenceLevel', 'receiptPath', 'sourceRefs', 'sourceRefHashes', 'toolId', 'unityVersion', 'capturedUtc')) {
    $property = Get-ReceiptProperty $r $field
    if ($null -eq $property -or $null -eq $property.Value) {
        throw "Missing strict receipt field: $field"
    }
    if ($property.Value -is [string] -and [string]::IsNullOrWhiteSpace([string]$property.Value)) {
        throw "Missing strict receipt field: $field"
    }
}

$sourceRefsProperty = Get-ReceiptProperty $r 'sourceRefs'
if (-not ($sourceRefsProperty.Value -is [Array]) -or @($sourceRefsProperty.Value).Count -eq 0) {
    throw 'sourceRefs must be a non-empty JSON string array'
}
$sourceRefHashesProperty = Get-ReceiptProperty $r 'sourceRefHashes'
if ($sourceRefHashesProperty.Value -is [string] -or @($sourceRefHashesProperty.Value.PSObject.Properties).Count -eq 0) {
    throw 'sourceRefHashes must be a non-empty JSON object'
}

$authorizationKindProperty = Get-ReceiptProperty $r 'authorizationKind'
if ($null -eq $authorizationKindProperty) {
    $legacyPlanHash = Get-ReceiptProperty $r 'planHash'
    if ($null -eq $legacyPlanHash -or -not ($legacyPlanHash.Value -is [string]) -or [string]$legacyPlanHash.Value -notmatch '^[a-fA-F0-9]{64}$') {
        throw 'authorizationKind is required unless a legacy SHA-256 planHash is present'
    }
    $authorizationKind = 'managed-aibrain'
} else {
    if (-not ($authorizationKindProperty.Value -is [string]) -or [string]::IsNullOrWhiteSpace([string]$authorizationKindProperty.Value)) {
        throw 'authorizationKind must be a non-empty string'
    }
    $authorizationKind = [string]$authorizationKindProperty.Value
}

switch -CaseSensitive ($authorizationKind) {
    'managed-aibrain' {
        Assert-Sha256Field $r 'planHash'
    }
    'current-user-direct' {
        Assert-Sha256Field $r 'userInstructionHash'
        [void](Assert-NonEmptyStringArray $r 'authorizedOperations')
        Assert-AuthorizedPaths $r
    }
    'read-only' {
        # Read-only evidence does not consume an action-authorization hash.
    }
    default {
        throw "Unsupported authorizationKind: $authorizationKind"
    }
}

$name = Split-Path -Leaf $skill
if ([string]$r.skillName -ne $name) { throw 'Receipt skillName mismatch' }
if ([string]$r.status -notmatch '^(passed|failed|blocked|not-run)$') { throw 'Invalid receipt status' }
if ([string]$r.evidenceLevel -notmatch '^S[0-6]$') { throw 'Invalid evidence level' }
if ([string]$r.receiptPath -ne (Relative $receipt)) { throw 'receiptPath does not identify this receipt' }
try {
    $captured = [DateTime]::Parse([string]$r.capturedUtc).ToUniversalTime()
} catch {
    throw 'capturedUtc must be an ISO timestamp'
}
if (([DateTime]::UtcNow - $captured).TotalHours -gt $MaxEvidenceAgeHours) {
    throw "Evidence receipt is older than $MaxEvidenceAgeHours hours"
}

foreach ($ref in @($r.sourceRefs)) {
    if (-not ($ref -is [string]) -or [string]::IsNullOrWhiteSpace([string]$ref)) {
        throw 'sourceRefs must contain only non-empty strings'
    }
    $refText = [string]$ref
    if ([IO.Path]::IsPathRooted($refText)) { throw "sourceRef must be project-relative: $refText" }
    $refPath = [IO.Path]::GetFullPath([IO.Path]::Combine($root, $refText))
    if (-not (Test-PathInsideProject $refPath) -or -not (Test-Path -LiteralPath $refPath -PathType Leaf)) {
        throw "Receipt sourceRef missing: $refText"
    }
    $prop = $r.sourceRefHashes.PSObject.Properties[$refText]
    if ($null -eq $prop) { $prop = $r.sourceRefHashes.PSObject.Properties[$refText.Replace('/', '_')] }
    if ($null -eq $prop -or [string]$prop.Value -ne (Hash $refPath)) {
        throw "Receipt sourceRef hash is stale: $refText"
    }
}
Write-Output "PASS: strict evidence receipt contract: $name/$($r.case)"
