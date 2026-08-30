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
$bindingValidator = Join-Path $root '.agents/skills/es-skill-governance/scripts/Test-ESEvidenceContractBindings.ps1'
$centralContractPath = Join-Path $root 'ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json'
$schemaModulePath = Join-Path $root 'ES/Automation/Contracts/ESJsonSchemaLite.psm1'

if (-not (Test-Path -LiteralPath $bindingValidator -PathType Leaf)) { throw 'Central Evidence binding validator is missing' }
& powershell -NoProfile -File $bindingValidator -ProjectRoot $root -SkillPath $skill -Quiet
if ($LASTEXITCODE -ne 0) { throw 'Skill Evidence contract binding is missing, stale, or invalid' }
Import-Module $schemaModulePath -Force
$centralContractHash = (Get-FileHash -LiteralPath $centralContractPath -Algorithm SHA256).Hash.ToLowerInvariant()
$bindingPath = Join-Path $skill 'evidence-contract.binding.json'
$binding = [IO.File]::ReadAllText($bindingPath, [Text.UTF8Encoding]::new($false, $true)) | ConvertFrom-Json

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

# Safe observation metadata is not a semantic acceptance signal.  For direct
# collaboration/read-only receipts, recover only the bounded fields whose
# value can be derived from the receipt file itself.  Managed/high-risk
# receipts remain strict: a producer must persist these fields explicitly.
$normalizations = [Collections.Generic.List[string]]::new()
$authorizationKindHint = if ($null -ne $r.PSObject.Properties['authorizationKind']) { [string]$r.authorizationKind } else { 'managed-aibrain' }
if ($authorizationKindHint -in @('current-user-direct', 'read-only')) {
    if ($null -eq $r.PSObject.Properties['capturedUtc'] -or [string]::IsNullOrWhiteSpace([string]$r.capturedUtc)) {
        $r | Add-Member -NotePropertyName capturedUtc -NotePropertyValue ((Get-Item -LiteralPath $receipt).LastWriteTimeUtc.ToString('o')) -Force
        [void]$normalizations.Add('capturedUtc=file.LastWriteTimeUtc')
    }
    if ($null -eq $r.PSObject.Properties['receiptPath'] -or [string]::IsNullOrWhiteSpace([string]$r.receiptPath)) {
        $r | Add-Member -NotePropertyName receiptPath -NotePropertyValue (Relative $receipt) -Force
        [void]$normalizations.Add('receiptPath=receipt.absolutePath')
    }
}

$contractIdProperty = Get-ReceiptProperty $r 'evidenceContractId'
$contractHashProperty = Get-ReceiptProperty $r 'evidenceContractHash'
if (($null -eq $contractIdProperty) -xor ($null -eq $contractHashProperty)) {
    throw 'evidenceContractId and evidenceContractHash must be supplied together'
}
if ($null -eq $contractIdProperty) {
    # Explicit compatibility projection for receipts created before the central
    # contract binding. The bounded window prevents new producers from using
    # the legacy shape indefinitely.
    $capturedProperty = Get-ReceiptProperty $r 'capturedUtc'
    if ($null -eq $capturedProperty) { throw 'Legacy Evidence receipt requires capturedUtc' }
    try {
        $legacyCaptured = [DateTimeOffset]::Parse([string]$capturedProperty.Value).ToUniversalTime()
        $legacyBefore = [DateTimeOffset]::Parse([string]$binding.compatibility.legacyReceiptBeforeUtc).ToUniversalTime()
        $legacyEnds = [DateTimeOffset]::Parse([string]$binding.compatibility.legacyReceiptAcceptanceEndsUtc).ToUniversalTime()
    } catch { throw 'Legacy Evidence compatibility window is invalid' }
    if ($legacyCaptured -gt $legacyBefore) { throw 'New Evidence receipts must persist the central contract ID and hash' }
    if ([DateTimeOffset]::UtcNow -gt $legacyEnds) { throw 'Legacy Evidence receipt compatibility window has ended' }
    $r | Add-Member -NotePropertyName evidenceContractId -NotePropertyValue 'es.skill-evidence-receipt'
    $r | Add-Member -NotePropertyName evidenceContractHash -NotePropertyValue $centralContractHash
} elseif ([string]$contractIdProperty.Value -cne 'es.skill-evidence-receipt' -or [string]$contractHashProperty.Value -cne $centralContractHash) {
    throw 'Evidence receipt central contract binding is stale or forged'
}
$schemaErrors = @(Test-ESJsonSchemaValue -SchemaPath $centralContractPath -Value $r)
if ($schemaErrors.Count -gt 0) { throw "Evidence receipt does not satisfy the central schema: $($schemaErrors -join '; ')" }

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
$normalizationText = if ($normalizations.Count -gt 0) { "; normalized=$($normalizations -join ',')" } else { '' }
Write-Output "PASS: strict evidence receipt contract: $name/$($r.case)$normalizationText"
