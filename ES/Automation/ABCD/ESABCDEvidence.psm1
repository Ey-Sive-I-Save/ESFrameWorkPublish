Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-ESABCDEvidenceCanonical($Value) {
    if ($null -eq $Value) { return 'null' }
    if ($Value -is [string] -or $Value -is [char]) { return ([string]$Value | ConvertTo-Json -Compress) }
    if ($Value -is [bool]) { return $(if ($Value) { 'true' } else { 'false' }) }
    if ($Value -is [Collections.IDictionary]) {
        return '{' + ((@($Value.Keys | ForEach-Object { [string]$_ } | Sort-Object) | ForEach-Object {
            ('{0}:{1}' -f ($_ | ConvertTo-Json -Compress), (ConvertTo-ESABCDEvidenceCanonical $Value[$_]))
        }) -join ',') + '}'
    }
    if ($Value -is [pscustomobject]) {
        return '{' + ((@($Value.PSObject.Properties | Sort-Object Name) | ForEach-Object {
            ('{0}:{1}' -f ($_.Name | ConvertTo-Json -Compress), (ConvertTo-ESABCDEvidenceCanonical $_.Value))
        }) -join ',') + '}'
    }
    if ($Value -is [Collections.IEnumerable] -and $Value -isnot [string]) {
        return '[' + ((@($Value) | ForEach-Object { ConvertTo-ESABCDEvidenceCanonical $_ }) -join ',') + ']'
    }
    return ([string]$Value | ConvertTo-Json -Compress)
}

function Get-ESABCDEvidenceHash($Value) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes((ConvertTo-ESABCDEvidenceCanonical $Value)))).Replace('-', '').ToLowerInvariant())
    } finally { $sha.Dispose() }
}

function Resolve-ESABCDEvidencePath {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$ProjectRoot,[Parameter(Mandatory)][string]$RelativePath)
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath) -or $RelativePath -match '(^|[/\\])\.\.([/\\]|$)' -or $RelativePath -match '[*?]') {
        throw 'EVIDENCE_PATH_INVALID'
    }
    $root = (Resolve-Path -LiteralPath $ProjectRoot).Path
    $full = [IO.Path]::GetFullPath((Join-Path $root $RelativePath))
    $prefix = $root.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'EVIDENCE_PATH_OUTSIDE_PROJECT' }
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "EVIDENCE_FILE_MISSING:$RelativePath" }
    $item = Get-Item -LiteralPath $full -Force
    if ($item.LinkType -or (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) { throw "EVIDENCE_REPARSE_POINT:$RelativePath" }
    [pscustomobject]@{ path = $RelativePath.Replace('\','/'); fullPath = $full; sha256 = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant() }
}

function Get-ESABCDReceiptHashInput($Receipt) {
    $input = [ordered]@{}
    foreach ($p in $Receipt.PSObject.Properties) { if ($p.Name -ne 'receiptHash') { $input[$p.Name] = $p.Value } }
    return $input
}

function Read-ESABCDReceipt {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ProjectRoot,
        [Parameter(Mandatory)][string]$Path,
        [string]$ExpectedSha256,
        [string]$ExpectedReceiptHash
    )
    $resolved = Resolve-ESABCDEvidencePath $ProjectRoot $Path
    if ($ExpectedSha256 -and $resolved.sha256 -cne $ExpectedSha256.ToLowerInvariant()) { throw "EVIDENCE_ARTIFACT_HASH_MISMATCH:$Path" }
    try {
        $raw = [Text.UTF8Encoding]::new($false, $true).GetString([IO.File]::ReadAllBytes($resolved.fullPath))
        $receipt = $raw | ConvertFrom-Json -ErrorAction Stop
    } catch { throw "EVIDENCE_RECEIPT_JSON_INVALID:$Path" }
    if ($null -eq $receipt -or $receipt -is [string]) { throw "EVIDENCE_RECEIPT_OBJECT_REQUIRED:$Path" }
    if ($null -eq $receipt.PSObject.Properties['receiptHash'] -or [string]$receipt.receiptHash -notmatch '^[a-f0-9]{64}$') { throw "EVIDENCE_RECEIPT_HASH_MISSING:$Path" }
    $actualReceiptHash = Get-ESABCDEvidenceHash (Get-ESABCDReceiptHashInput $receipt)
    if ([string]$receipt.receiptHash -cne $actualReceiptHash) { throw "EVIDENCE_RECEIPT_HASH_MISMATCH:$Path" }
    if ($ExpectedReceiptHash -and [string]$receipt.receiptHash -cne $ExpectedReceiptHash.ToLowerInvariant()) { throw "EVIDENCE_RECEIPT_REF_HASH_MISMATCH:$Path" }
    [pscustomobject][ordered]@{ path = $resolved.path; sha256 = $resolved.sha256; receiptHash = [string]$receipt.receiptHash; receipt = $receipt }
}

function Assert-ESABCDEvidenceReferences {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$ProjectRoot,[Parameter(Mandatory)]$References)
    $validated = [Collections.Generic.List[object]]::new()
    foreach ($reference in @($References)) {
        if ($reference -is [string]) { throw 'EVIDENCE_REFERENCE_MUST_INCLUDE_HASH' }
        $path = [string]$reference.path
        $sha = [string]$reference.sha256
        if ($sha -notmatch '^[a-f0-9]{64}$') { throw "EVIDENCE_REFERENCE_HASH_INVALID:$path" }
        $expectedReceiptHash = if ($null -ne $reference.PSObject.Properties['receiptHash']) { [string]$reference.receiptHash } else { $null }
        if ($expectedReceiptHash) {
            $item = Read-ESABCDReceipt $ProjectRoot $path $sha $expectedReceiptHash
            [void]$validated.Add([pscustomobject]$item)
        } else {
            $item = Resolve-ESABCDEvidencePath $ProjectRoot $path
            if ($item.sha256 -cne $sha.ToLowerInvariant()) { throw "EVIDENCE_ARTIFACT_HASH_MISMATCH:$path" }
            [void]$validated.Add([pscustomobject][ordered]@{ path = $item.path; sha256 = $item.sha256; receiptHash = $null })
        }
    }
    return @($validated)
}

function New-ESABCDImmutableSnapshot {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$ProjectRoot,[Parameter(Mandatory)][string]$SnapshotRoot,[Parameter(Mandatory)][string]$SnapshotId,[Parameter(Mandatory)]$State)
    if ($SnapshotId -notmatch '^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$') { throw 'SNAPSHOT_ID_INVALID' }
    $root = (Resolve-Path -LiteralPath $ProjectRoot).Path
    if ([IO.Path]::IsPathRooted($SnapshotRoot) -or $SnapshotRoot -match '(^|[/\\])\.\.([/\\]|$)') { throw 'SNAPSHOT_ROOT_INVALID' }
    $directory = Join-Path $root $SnapshotRoot
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $payload = [ordered]@{ schemaVersion = 1; recordType = 'ABCDImmutableBranchSnapshot'; snapshotId = $SnapshotId; state = $State }
    $snapshotHash = Get-ESABCDEvidenceHash $payload
    $relative = (Join-Path $SnapshotRoot ($SnapshotId + '.json')).Replace('\','/')
    $full = Join-Path $root $relative
    $json = $payload | ConvertTo-Json -Depth 40
    try {
        $stream = [IO.File]::Open($full, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
        try { $bytes = [Text.UTF8Encoding]::new($false).GetBytes($json); $stream.Write($bytes, 0, $bytes.Length) } finally { $stream.Dispose() }
    } catch [IO.IOException] {
        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw }
        $existing = [Text.UTF8Encoding]::new($false, $true).GetString([IO.File]::ReadAllBytes($full)) | ConvertFrom-Json
        if ((Get-ESABCDEvidenceHash $existing) -ne $snapshotHash) { throw 'SNAPSHOT_ID_ALREADY_EXISTS_WITH_DIFFERENT_CONTENT' }
    }
    [pscustomobject][ordered]@{ snapshotId = $SnapshotId; snapshotHash = $snapshotHash; path = $relative; artifactHash = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant() }
}

function Read-ESABCDImmutableSnapshot {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$ProjectRoot,[Parameter(Mandatory)][string]$Path,[Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$SnapshotHash)
    $resolved = Resolve-ESABCDEvidencePath $ProjectRoot $Path
    $raw = [Text.UTF8Encoding]::new($false, $true).GetString([IO.File]::ReadAllBytes($resolved.fullPath))
    try { $snapshot = $raw | ConvertFrom-Json -ErrorAction Stop } catch { throw 'SNAPSHOT_JSON_INVALID' }
    if ((Get-ESABCDEvidenceHash $snapshot) -cne $SnapshotHash.ToLowerInvariant()) { throw 'SNAPSHOT_HASH_MISMATCH' }
    if ([string]$snapshot.recordType -cne 'ABCDImmutableBranchSnapshot') { throw 'SNAPSHOT_RECORD_TYPE_INVALID' }
    [pscustomobject][ordered]@{ snapshotId = [string]$snapshot.snapshotId; snapshotHash = $SnapshotHash.ToLowerInvariant(); path = $resolved.path; artifactHash = $resolved.sha256; state = $snapshot.state }
}

Export-ModuleMember -Function ConvertTo-ESABCDEvidenceCanonical,Get-ESABCDEvidenceHash,Resolve-ESABCDEvidencePath,Get-ESABCDReceiptHashInput,Read-ESABCDReceipt,Assert-ESABCDEvidenceReferences,New-ESABCDImmutableSnapshot,Read-ESABCDImmutableSnapshot
