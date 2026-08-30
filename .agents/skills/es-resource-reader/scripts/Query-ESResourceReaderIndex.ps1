[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$IndexPath,
    [string]$PathPattern='*',
    [string]$Format,
    [ValidateSet('ready','error')][string]$Status,
    [string]$HashPrefix,
    [int]$MaxResults=128
)
$ErrorActionPreference='Stop'
if($MaxResults -lt 1 -or $MaxResults -gt 4096){throw 'MaxResults must be between 1 and 4096.'}
$index=Get-Content -LiteralPath $IndexPath -Raw -Encoding UTF8|ConvertFrom-Json -ErrorAction Stop
if($index.indexId -ne 'es-resource-reader.resource-index.v1'){throw 'Unsupported resource index.'}
$items=@($index.items)
$matches=@($items|Where-Object {
    ($_.sourcePath -like $PathPattern) -and
    ([string]::IsNullOrWhiteSpace($Format) -or $_.detectedFormat -ieq $Format) -and
    ([string]::IsNullOrWhiteSpace($Status) -or $_.status -ieq $Status) -and
    ([string]::IsNullOrWhiteSpace($HashPrefix) -or $_.sourceSha256 -like ($HashPrefix.ToLowerInvariant()+'*'))
}|Sort-Object sourcePath|Select-Object -First $MaxResults)
[ordered]@{schemaVersion=1;queryId='es-resource-reader.index-query.v1';indexPath=(Resolve-Path -LiteralPath $IndexPath).Path;matchedCount=$matches.Count;items=$matches;nonClaims=@('source freshness beyond index capture','Unity import','runtime loading','release readiness')}|ConvertTo-Json -Depth 12
