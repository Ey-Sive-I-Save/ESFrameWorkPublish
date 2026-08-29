[CmdletBinding()]
param(
    [ValidateSet('mock-contract-only', 'local-adapter', 'user-authorized-service')]
    [string]$Mode = 'mock-contract-only',
    [string]$ApiBase = '',
    [string[]]$Allowlist = @(),
    [ValidateRange(1, 300)][int]$TimeoutSeconds = 10,
    [ValidateRange(0, 5)][int]$MaxAttempts = 2,
    [ValidateRange(0, 60)][double]$BackoffSeconds = 1,
    [ValidateRange(0, 300)][double]$MaxBackoffSeconds = 8,
    [int[]]$RetryableStatusCodes = @(408, 425, 429, 500, 502, 503, 504),
    [ValidateRange(0, 30)][int]$CancellationGraceSeconds = 5,
    [string]$ContractId = '',
    [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$root = [IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\') + '\'

function Test-HostAllowlisted([string]$hostName, [string[]]$entries) {
    foreach ($entry in @($entries)) {
        $candidate = $entry.ToLowerInvariant()
        $hostValue = $hostName.ToLowerInvariant()
        if ($candidate -eq $hostValue -or ($candidate.StartsWith('*.') -and $hostValue.EndsWith($candidate.Substring(1)))) { return $true }
    }
    return $false
}

$allow = @($Allowlist | ForEach-Object { [string]$_ })
if ($Mode -eq 'user-authorized-service') {
    if ([string]::IsNullOrWhiteSpace($ApiBase)) { throw 'Service mode requires ApiBase.' }
    $apiUri = $null
    if (-not [Uri]::TryCreate($ApiBase, [UriKind]::Absolute, [ref]$apiUri) -or $apiUri.Scheme -notin @('http', 'https') -or [string]::IsNullOrWhiteSpace($apiUri.Host)) { throw 'ApiBase must be an absolute http(s) URL.' }
    if ($allow.Count -eq 0) { throw 'Service mode requires a non-empty host allowlist.' }
    if (-not (Test-HostAllowlisted $apiUri.Host $allow)) { throw 'ApiBase host is not covered by allowlist.' }
} else {
    if (-not [string]::IsNullOrWhiteSpace($ApiBase) -or $allow.Count -gt 0) { throw 'Mock and local modes cannot carry ApiBase or network allowlist.' }
    $ApiBase = ''
    $allow = @()
}
if ($MaxBackoffSeconds -lt $BackoffSeconds) { throw 'MaxBackoffSeconds must be greater than or equal to BackoffSeconds.' }
if (@($RetryableStatusCodes | Where-Object { $_ -lt 408 -or $_ -gt 599 }).Count -gt 0) { throw 'RetryableStatusCodes must be HTTP status codes from 408 through 599.' }

if ([string]::IsNullOrWhiteSpace($ContractId)) { $ContractId = "web-backend-$([guid]::NewGuid().ToString('N'))" }
if ($ContractId -notmatch '^[a-z][a-z0-9._-]{1,127}$') { throw 'ContractId must be a lowercase stable identifier.' }
$executionKind = switch ($Mode) {
    'mock-contract-only' { 'contract-only' }
    'local-adapter' { 'local-process' }
    'user-authorized-service' { 'remote-http' }
}
$adapterId = switch ($Mode) {
    'mock-contract-only' { 'es.web.mock' }
    'local-adapter' { 'es.web.local' }
    'user-authorized-service' { 'es.web.service' }
}

$requiredFields = @('accessToken', 'apiKey', 'authorization', 'cookie', 'password', 'secret', 'token')
$requiredHeaders = @('Authorization', 'Cookie', 'Set-Cookie', 'X-Api-Key')
$requiredQuery = @('access_token', 'api_key', 'token', 'password', 'secret')
$contract = [ordered]@{
    schemaVersion = 1
    recordType = 'WebPageStudioBackendContract'
    contractId = $ContractId
    mode = $Mode
    adapter = [ordered]@{ adapterId = $adapterId; version = '1.0.0'; executionKind = $executionKind }
    network = [ordered]@{ enabled = ($Mode -eq 'user-authorized-service'); apiBase = $ApiBase; allowlist = $allow; timeoutSeconds = $TimeoutSeconds; maxResponseBytes = 10485760 }
    retry = [ordered]@{ enabled = ($MaxAttempts -gt 0); maxAttempts = $MaxAttempts; backoffSeconds = $BackoffSeconds; maxBackoffSeconds = $MaxBackoffSeconds; retryableStatusCodes = @($RetryableStatusCodes | Select-Object -Unique); idempotentOnly = $true }
    cancellation = [ordered]@{ supported = $true; cancelOnTimeout = $true; graceSeconds = $CancellationGraceSeconds; checkpointPolicy = 'all' }
    redaction = [ordered]@{ enabled = $true; replacement = '[REDACTED]'; fieldNames = $requiredFields; headerNames = $requiredHeaders; queryParameters = $requiredQuery }
    operationPolicy = [ordered]@{ readOnly = $true; allowedMethods = @('GET', 'HEAD'); deniedMethods = @('POST', 'PUT', 'PATCH', 'DELETE', 'CONNECT', 'TRACE') }
    execution = [ordered]@{ status = 'not-run'; networkCalls = 0; runtimeStatus = 'runtime-not-run' }
    nonClaims = @('No backend process, network request, browser, Unity runtime, or service health was run by this contract entry.')
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) { $OutputPath = Join-Path $root "ES/Output/WebPageStudio/backend/$ContractId.json" }
$fullOutput = [IO.Path]::GetFullPath($OutputPath)
if (-not $fullOutput.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'OutputPath must remain under the project root.' }
if (Test-Path -LiteralPath $fullOutput) { throw "Refusing to overwrite existing backend contract: $fullOutput" }
$parent = Split-Path -Parent $fullOutput
if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
[IO.File]::WriteAllText($fullOutput, ($contract | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
$contract | ConvertTo-Json -Depth 12
