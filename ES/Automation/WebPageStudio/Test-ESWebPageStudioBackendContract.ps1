[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ContractPath,
    [string]$ReportPath = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$root = [IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\') + '\'
$full = (Resolve-Path -LiteralPath $ContractPath -ErrorAction Stop).Path
if (-not $full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'ContractPath must remain under the project root.' }

Import-Module (Join-Path (Get-Location) 'ES/Automation/Contracts/ESJsonSchemaLite.psm1') -Force
$contract = Get-Content -Raw -Encoding UTF8 -LiteralPath $full | ConvertFrom-Json -ErrorAction Stop
$schemaPath = Join-Path (Get-Location) 'ES/Automation/Contracts/es-web-page-studio-backend-v1.schema.json'
$schemaErrors = @(Test-ESJsonSchemaValue -SchemaPath $schemaPath -Value $contract)
$cases = [System.Collections.Generic.List[object]]::new()
function Add-Case([string]$name, [bool]$ok, [string]$detail) {
    $cases.Add([ordered]@{ case = $name; status = if ($ok) { 'passed' } else { 'failed' }; detail = $detail })
}
function Test-HostAllowlisted([string]$hostName, [string[]]$entries) {
    foreach ($entry in @($entries)) {
        $candidate = ([string]$entry).ToLowerInvariant()
        $hostValue = ([string]$hostName).ToLowerInvariant()
        if ($candidate -eq $hostValue -or ($candidate.StartsWith('*.') -and $hostValue.EndsWith($candidate.Substring(1)))) { return $true }
    }
    return $false
}

Add-Case 'schema' ($schemaErrors.Count -eq 0) $(if ($schemaErrors.Count) { $schemaErrors -join '; ' } else { 'backend contract matches schema' })
if ($schemaErrors.Count -eq 0) {
    $mode = [string]$contract.mode
    $kind = [string]$contract.adapter.executionKind
    $networkEnabled = [bool]$contract.network.enabled
    $apiBase = [string]$contract.network.apiBase
    $allowlist = @($contract.network.allowlist | ForEach-Object { [string]$_ })

    $modeOk = ($mode -eq 'mock-contract-only' -and $kind -eq 'contract-only' -and -not $networkEnabled) -or
              ($mode -eq 'local-adapter' -and $kind -eq 'local-process' -and -not $networkEnabled) -or
              ($mode -eq 'user-authorized-service' -and $kind -eq 'remote-http' -and $networkEnabled)
    Add-Case 'mode-adapter-binding' $modeOk 'mode selects its declared adapter execution kind'

    $networkOk = $false
    if (-not $networkEnabled) { $networkOk = [string]::IsNullOrWhiteSpace($apiBase) -and $allowlist.Count -eq 0 }
    else {
        $uri = $null
        $networkOk = [Uri]::TryCreate($apiBase, [UriKind]::Absolute, [ref]$uri) -and $uri.Scheme -in @('http', 'https') -and -not [string]::IsNullOrWhiteSpace($uri.Host) -and $allowlist.Count -gt 0 -and (Test-HostAllowlisted $uri.Host $allowlist)
    }
    Add-Case 'network-allowlist-boundary' $networkOk 'disabled modes have no endpoint; service endpoint is absolute and host-allowlisted'

    $timeoutOk = [int]$contract.network.timeoutSeconds -ge 1 -and [int]$contract.network.timeoutSeconds -le 300 -and [int]$contract.network.maxResponseBytes -ge 1024
    Add-Case 'timeout-budget' $timeoutOk 'request timeout and response-size budget are bounded'

    $retry = $contract.retry
    $retryOk = ([int]$retry.maxAttempts -ge 0 -and [int]$retry.maxAttempts -le 5 -and [double]$retry.backoffSeconds -le [double]$retry.maxBackoffSeconds -and [bool]$retry.idempotentOnly -and (([bool]$retry.enabled) -eq ([int]$retry.maxAttempts -gt 0)))
    Add-Case 'retry-policy' $retryOk 'retry count/backoff are bounded and retries remain idempotent-only'

    $cancel = $contract.cancellation
    Add-Case 'cancellation-policy' ([bool]$cancel.supported -and [bool]$cancel.cancelOnTimeout -and [int]$cancel.graceSeconds -ge 0 -and [int]$cancel.graceSeconds -le 30) 'cancellation is supported, timeout-triggered, and grace-bounded'

    $redaction = $contract.redaction
    $fieldNames = @($redaction.fieldNames | ForEach-Object { ([string]$_).ToLowerInvariant() })
    $headerNames = @($redaction.headerNames | ForEach-Object { ([string]$_).ToLowerInvariant() })
    $queryNames = @($redaction.queryParameters | ForEach-Object { ([string]$_).ToLowerInvariant() })
    $requiredFields = @('accesstoken', 'apikey', 'authorization', 'cookie', 'password', 'secret', 'token')
    $requiredHeaders = @('authorization', 'cookie', 'set-cookie', 'x-api-key')
    $requiredQuery = @('access_token', 'api_key', 'token', 'password', 'secret')
    $redactionOk = [bool]$redaction.enabled -and -not [string]::IsNullOrWhiteSpace([string]$redaction.replacement) -and (@($requiredFields | Where-Object { $fieldNames -notcontains $_ }).Count -eq 0) -and (@($requiredHeaders | Where-Object { $headerNames -notcontains $_ }).Count -eq 0) -and (@($requiredQuery | Where-Object { $queryNames -notcontains $_ }).Count -eq 0)
    Add-Case 'redaction-policy' $redactionOk 'sensitive fields, headers, and query parameters are covered by mandatory redaction'

    $operation = $contract.operationPolicy
    $readOnlyOk = [bool]$operation.readOnly -and (@($operation.allowedMethods | Where-Object { $_ -notin @('GET', 'HEAD') }).Count -eq 0) -and (@($operation.deniedMethods | Where-Object { $_ -in @('POST', 'PUT', 'PATCH', 'DELETE', 'CONNECT', 'TRACE') }).Count -eq 6)
    Add-Case 'read-only-operation' $readOnlyOk 'backend contract cannot mutate remote data'

    Add-Case 'no-execution-claim' ([string]$contract.execution.status -eq 'not-run' -and [int]$contract.execution.networkCalls -eq 0 -and [string]$contract.execution.runtimeStatus -eq 'runtime-not-run') 'contract validation does not execute backend or network'
}

$failed = @($cases | Where-Object { $_.status -eq 'failed' })
$sourceRefs = @('ES/Automation/WebPageStudio/Test-ESWebPageStudioBackendContract.ps1', 'ES/Automation/Contracts/es-web-page-studio-backend-v1.schema.json', $full.Substring($root.Length).Replace('\', '/'))
$sourceRefHashes = [ordered]@{}
foreach ($ref in $sourceRefs) {
    $refFull = if ([IO.Path]::IsPathRooted($ref)) { $ref } else { Join-Path $root $ref }
    if (Test-Path -LiteralPath $refFull -PathType Leaf) { $sourceRefHashes[$ref] = (Get-FileHash -LiteralPath $refFull -Algorithm SHA256).Hash.ToLowerInvariant() }
}
if ([string]::IsNullOrWhiteSpace($ReportPath)) { $ReportPath = Join-Path (Split-Path -Parent $full) 'backend-contract-validation.json' }
$reportFull = [IO.Path]::GetFullPath($ReportPath)
if (-not $reportFull.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'ReportPath must remain under the project root.' }
$report = [ordered]@{
    schemaVersion = 1
    validator = 'Test-ESWebPageStudioBackendContract'
    status = if ($failed.Count) { 'failed' } else { 'passed' }
    caseCount = $cases.Count
    passedCount = @($cases | Where-Object { $_.status -eq 'passed' }).Count
    failedCount = $failed.Count
    cases = @($cases)
    staticStatus = if ($failed.Count) { 'static-failed' } else { 'static-passed' }
    runtimeStatus = 'runtime-not-run'
    evidenceLevel = 'S1'
    capturedUtc = [DateTime]::UtcNow.ToString('o')
    authorizationKind = 'read-only'
    skillName = 'es-automation-worker-authoring'
    case = 'web-page-studio-backend-contract'
    receiptPath = $reportFull.Substring($root.Length).Replace('\', '/')
    sourceRefs = $sourceRefs
    sourceRefHashes = $sourceRefHashes
    claimsNotProven = @('backend service availability', 'network reachability', 'browser/Unity runtime', 'production security posture')
}
$reportParent = Split-Path -Parent $reportFull
if (-not (Test-Path -LiteralPath $reportParent)) { New-Item -ItemType Directory -Path $reportParent -Force | Out-Null }
[IO.File]::WriteAllText($reportFull, ($report | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
$report | ConvertTo-Json -Depth 20
if ($failed.Count) { exit 1 }
