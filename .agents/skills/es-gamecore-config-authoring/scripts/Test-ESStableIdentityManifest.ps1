[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)][string]$ProjectRoot,
  [Parameter(Mandatory = $true)][string]$ManifestPath
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
if ([IO.Path]::IsPathRooted($ManifestPath)) { throw 'ManifestPath must be project-relative.' }

$relativePath = $ManifestPath.Replace('\', '/').Trim()
if ($relativePath.Contains('..') -or $relativePath -notmatch '^ES/Output/.+\.json$') {
  throw 'ManifestPath must remain under ES/Output.'
}

$fullPath = Join-Path $root ($relativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
  throw "Identity manifest missing: $relativePath"
}
$outputRoot = (Resolve-Path -LiteralPath (Join-Path $root 'ES\Output')).Path.TrimEnd('\', '/')
$resolvedManifest = (Resolve-Path -LiteralPath $fullPath).Path
if (-not $resolvedManifest.StartsWith("$outputRoot$([IO.Path]::DirectorySeparatorChar)", [StringComparison]::OrdinalIgnoreCase)) {
  throw 'Resolved manifest path escapes ES/Output.'
}
$cursor = Get-Item -LiteralPath $resolvedManifest
while ($null -ne $cursor -and $cursor.FullName.Length -ge $outputRoot.Length) {
  if (($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw "Reparse points are not allowed in the manifest path: $($cursor.FullName)"
  }
  if ($cursor.FullName -eq $outputRoot) { break }
  $cursor = $cursor.Parent
}

$utf8 = [Text.UTF8Encoding]::new($false, $true)
$raw = $utf8.GetString([IO.File]::ReadAllBytes($resolvedManifest))
if ($raw -match '(?i)"runtime(?:key|id)"\s*:') {
  throw 'RuntimeKey/RuntimeId is process-local and must not be persisted in an identity manifest.'
}
$manifest = $raw | ConvertFrom-Json
if ([string]$manifest.schemaVersion -ne '1' -or @($manifest.entries).Count -eq 0) {
  throw 'Identity manifest requires schemaVersion 1 and at least one entry.'
}
if ([string]$manifest.collisionPolicy -ne 'reject') {
  throw 'collisionPolicy must be reject.'
}

$stableIds = @{}
$scopedValues = @{}
$previousStableId = $null
foreach ($entry in @($manifest.entries)) {
  foreach ($property in @('stableId', 'scope', 'identityKind', 'serializedValue', 'schemaHash', 'source')) {
    if ([string]::IsNullOrWhiteSpace([string]$entry.$property)) {
      throw "Identity entry missing $property."
    }
  }
  if ([string]$entry.identityKind -notmatch '^(enum|string)$') {
    throw "Unsupported identityKind: $($entry.identityKind)"
  }
  if ($entry.serializedValue -is [Array] -or $entry.serializedValue -is [Management.Automation.PSCustomObject]) {
    throw "serializedValue must be a scalar: $($entry.stableId)"
  }
  if ([string]$entry.stableId -notmatch '^[a-z0-9][a-z0-9._:-]*$' -or [string]$entry.scope -notmatch '^[a-z0-9][a-z0-9._:-]*$') {
    throw 'stableId and scope must use canonical lowercase identity characters.'
  }
  if ([string]$entry.schemaHash -notmatch '^[0-9a-fA-F]{64}$') {
    throw "schemaHash must be a SHA-256 hex value: $($entry.stableId)"
  }

  $stableId = [string]$entry.stableId
  $scopedValue = "$( [string]$entry.scope )`0$( [string]$entry.serializedValue )"
  if ($stableIds.ContainsKey($stableId)) { throw "Duplicate stable identity: $stableId" }
  if ($scopedValues.ContainsKey($scopedValue)) { throw "Duplicate serialized identity in scope: $($entry.scope)" }
  if ($null -ne $previousStableId -and [string]::CompareOrdinal($previousStableId, $stableId) -ge 0) {
    throw 'Entries must be ordered by stableId using ordinal ascending order.'
  }
  $stableIds[$stableId] = $true
  $scopedValues[$scopedValue] = $true
  $previousStableId = $stableId
}

Write-Output "PASS: stable identity manifest preserves persistent identity and excludes RuntimeKey: $relativePath"
