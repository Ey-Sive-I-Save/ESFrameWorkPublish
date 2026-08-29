[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$RequestPath)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$full = (Resolve-Path -LiteralPath $RequestPath -ErrorAction Stop).Path
$root = [IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\') + '\'
if (-not $full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'RequestPath must remain under the project root.' }
$request = Get-Content -Raw -Encoding UTF8 -LiteralPath $full | ConvertFrom-Json -ErrorAction Stop
$schemaPath = Join-Path (Get-Location) 'ES/Automation/Contracts/es-web-page-studio-request-v1.schema.json'
Import-Module (Join-Path (Get-Location) 'ES/Automation/Contracts/ESJsonSchemaLite.psm1') -Force
$schemaErrors = @(Test-ESJsonSchemaValue -SchemaPath $schemaPath -Value $request)
if ($schemaErrors.Count -gt 0) { $schemaErrors | ForEach-Object { Write-Error $_ }; throw 'Request schema validation failed.' }
$required = @('schemaVersion', 'recordType', 'requestId', 'pageKind', 'objective', 'audience', 'primaryAction', 'visualDirection', 'responsiveProfiles', 'states', 'backend', 'network', 'output', 'acceptance', 'nonClaims')
foreach ($name in $required) { if ($null -eq $request.PSObject.Properties[$name]) { throw "Missing required property: $name" } }
if ([int]$request.schemaVersion -ne 1 -or [string]$request.recordType -ne 'WebPageStudioRequest') { throw 'Unsupported request schema.' }
if ([string]$request.pageKind -notin @('marketing', 'dashboard')) { throw 'Invalid pageKind.' }
function Normalize-Id([string]$value) { return (($value.ToLowerInvariant() -replace '[^a-z0-9]+', '-') -replace '(^-|-$)', '') }
$profileIds = @($request.responsiveProfiles | ForEach-Object { Normalize-Id ([string]$_.id) })
if (@($profileIds | Where-Object { $_.Length -lt 2 }).Count -gt 0) { throw 'Responsive profile IDs must normalize to at least two characters.' }
if (@($profileIds | Sort-Object -Unique).Count -ne $profileIds.Count) { throw 'Responsive profile IDs must be unique after normalization.' }
$stateIds = @($request.states | ForEach-Object { Normalize-Id ([string]$_) })
if (@($stateIds | Where-Object { $_.Length -lt 2 }).Count -gt 0) { throw 'State IDs must normalize to at least two characters.' }
if (@($stateIds | Sort-Object -Unique).Count -ne $stateIds.Count) { throw 'State IDs must be unique after normalization.' }
$networkEnabled = [bool]$request.network.enabled
$backendMode = [string]$request.backend.mode
$apiBase = [string]$request.backend.apiBase
$allowlist = @($request.network.allowlist | ForEach-Object { [string]$_ })
if ($networkEnabled) {
    if ($backendMode -ne 'user-authorized-service') { throw 'Enabled network requires user-authorized-service backend mode.' }
    if ([string]::IsNullOrWhiteSpace($apiBase)) { throw 'Enabled network requires ApiBase.' }
    $apiUri = [Uri]::new($apiBase, [UriKind]::Absolute)
    if ($apiUri.Scheme -notin @('http', 'https') -or [string]::IsNullOrWhiteSpace($apiUri.Host)) { throw 'ApiBase must be an absolute http(s) URL.' }
    if ($allowlist.Count -eq 0) { throw 'Enabled network requires allowlist.' }
    if (@($allowlist | Where-Object { $_ -match '\s' -or $_ -notmatch '^[A-Za-z0-9._*:-]+$' }).Count -gt 0) { throw 'Allowlist entries must be host-like tokens without whitespace.' }
} else {
    if ($backendMode -notin @('mock-contract-only', 'local-adapter')) { throw 'Disabled network requires mock-contract-only or local-adapter backend mode.' }
    if (-not [string]::IsNullOrWhiteSpace($apiBase) -or $allowlist.Count -gt 0) { throw 'Disabled network cannot carry ApiBase or allowlist.' }
}
if ([string]$request.output.format -ne 'static-html-css') { throw 'Only static-html-css is supported by this MVP.' }
[pscustomobject]@{ status = 'passed'; requestPath = $full; requestId = [string]$request.requestId; network = [bool]$request.network.enabled; outputFormat = [string]$request.output.format }
