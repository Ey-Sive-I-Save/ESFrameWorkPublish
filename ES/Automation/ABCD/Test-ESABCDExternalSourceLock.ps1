[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$SourceLockPath = 'Documentation/AIKnowledge/ExternalSources/agent-mechanism-source-lock.v1.json',
    [string]$SchemaPath = 'ES/Automation/Contracts/es-agent-mechanism-source-lock-v1.schema.json',
    [string]$ReportPath = 'ES/Output/StaticReplay/es-abcd-external-source-lock.json',
    [switch]$VerifyNetwork,
    [ValidateRange(1, 120)][int]$NetworkTimeoutSeconds = 15,
    [ValidateRange(1024, 16777216)][int]$MaxResponseBytes = 4194304,
    [ValidateRange(0, 10)][int]$MaxRedirects = 5
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $scriptDirectory = Split-Path -Parent $PSCommandPath
    $ProjectRoot = (Resolve-Path (Join-Path $scriptDirectory '..\..\..')).Path
}
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
Import-Module (Join-Path $root 'ES/Automation/Contracts/ESJsonSchemaLite.psm1') -Force
Import-Module (Join-Path $root 'ES/Automation/ABCD/ESABCDEvidence.psm1') -Force

function Read-StrictUtf8Json([string]$Path) {
    $raw = [Text.UTF8Encoding]::new($false, $true).GetString([IO.File]::ReadAllBytes($Path))
    return ($raw | ConvertFrom-Json -ErrorAction Stop)
}

function Get-SourceLockHashInput($Value) {
    $copy = [ordered]@{}
    foreach ($property in $Value.PSObject.Properties) {
        if ($property.Name -ne 'sourceSetHash') { $copy[$property.Name] = $property.Value }
    }
    return $copy
}

function Get-BytesHash([byte[]]$Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

$allowedHosts = @('alignment.anthropic.com', 'export.arxiv.org', 'raw.githubusercontent.com')

function Assert-ExternalUri([uri]$Uri) {
    if ($null -eq $Uri -or -not $Uri.IsAbsoluteUri -or $Uri.Scheme -cne 'https') { throw 'EXTERNAL_URL_HTTPS_REQUIRED' }
    if (-not [string]::IsNullOrWhiteSpace($Uri.UserInfo)) { throw 'EXTERNAL_URL_USERINFO_FORBIDDEN' }
    if ($Uri.Port -ne 443) { throw 'EXTERNAL_URL_PORT_FORBIDDEN' }
    if ($allowedHosts -cnotcontains $Uri.IdnHost.ToLowerInvariant()) { throw "EXTERNAL_HOST_NOT_ALLOWED:$($Uri.IdnHost)" }
}

function Assert-NetworkDeadline([DateTime]$StartedUtc, [int]$TimeoutSeconds) {
    if (([DateTime]::UtcNow - $StartedUtc).TotalSeconds -ge $TimeoutSeconds) { throw 'EXTERNAL_NETWORK_TIMEOUT' }
}

function ConvertTo-StrictUtf8Hash([byte[]]$Bytes) {
    try { $text = [Text.UTF8Encoding]::new($false, $true).GetString($Bytes) }
    catch { throw 'EXTERNAL_CONTENT_INVALID_UTF8' }
    return Get-BytesHash ([Text.UTF8Encoding]::new($false).GetBytes($text))
}

function Assert-ResponseSize([long]$ContentLength, [long]$ObservedLength, [int]$MaximumBytes) {
    if ($ContentLength -gt $MaximumBytes -or $ObservedLength -gt $MaximumBytes) { throw 'EXTERNAL_RESPONSE_TOO_LARGE' }
}

function Get-NetworkContentBytes([uri]$InitialUri) {
    Add-Type -AssemblyName System.Net.Http
    $handler = [Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $client = [Net.Http.HttpClient]::new($handler)
    $client.Timeout = [Threading.Timeout]::InfiniteTimeSpan
    $startedUtc = [DateTime]::UtcNow
    $currentUri = $InitialUri
    try {
        for ($redirectNo = 0; $redirectNo -le $MaxRedirects; $redirectNo++) {
            Assert-ExternalUri $currentUri
            Assert-NetworkDeadline $startedUtc $NetworkTimeoutSeconds
            $remaining = [Math]::Max(1, $NetworkTimeoutSeconds - [int]([DateTime]::UtcNow - $startedUtc).TotalSeconds)
            $cts = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds($remaining))
            $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get, $currentUri)
            [void]$request.Headers.UserAgent.ParseAdd('ESFramework-source-lock-validator/1.0')
            $response = $null
            try {
                try { $response = $client.SendAsync($request, [Net.Http.HttpCompletionOption]::ResponseHeadersRead, $cts.Token).GetAwaiter().GetResult() }
                catch [OperationCanceledException] { throw 'EXTERNAL_NETWORK_TIMEOUT' }
                $status = [int]$response.StatusCode
                if ($status -ge 300 -and $status -lt 400) {
                    if ($redirectNo -ge $MaxRedirects) { throw 'EXTERNAL_REDIRECT_LIMIT_EXCEEDED' }
                    $location = $response.Headers.Location
                    if ($null -eq $location) { throw 'EXTERNAL_REDIRECT_LOCATION_REQUIRED' }
                    $nextUri = if ($location.IsAbsoluteUri) { $location } else { [uri]::new($currentUri, $location) }
                    Assert-ExternalUri $nextUri
                    $currentUri = $nextUri
                    continue
                }
                if (-not $response.IsSuccessStatusCode) { throw "EXTERNAL_HTTP_STATUS:$status" }
                $declaredLength = if ($response.Content.Headers.ContentLength.HasValue) { [long]$response.Content.Headers.ContentLength.Value } else { -1 }
                Assert-ResponseSize $declaredLength 0 $MaxResponseBytes
                $stream = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
                $memory = [IO.MemoryStream]::new()
                try {
                    $buffer = [byte[]]::new(16384)
                    while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                        Assert-NetworkDeadline $startedUtc $NetworkTimeoutSeconds
                        Assert-ResponseSize $declaredLength ($memory.Length + $read) $MaxResponseBytes
                        $memory.Write($buffer, 0, $read)
                    }
                    return $memory.ToArray()
                } finally { $memory.Dispose(); $stream.Dispose() }
            } finally {
                if ($null -ne $response) { $response.Dispose() }
                $request.Dispose(); $cts.Dispose()
            }
        }
        throw 'EXTERNAL_REDIRECT_LIMIT_EXCEEDED'
    } finally { $client.Dispose(); $handler.Dispose() }
}

function Get-NetworkContentHash($Reference) {
    $uri = [uri]([string]$Reference.url)
    Assert-ExternalUri $uri
    $bytes = Get-NetworkContentBytes $uri
    if ([string]$Reference.hashMode -eq 'raw-bytes') { return Get-BytesHash $bytes }
    if ([string]$Reference.hashMode -eq 'utf8-decoded-body') { return ConvertTo-StrictUtf8Hash $bytes }
    throw "EXTERNAL_HASH_MODE_INVALID:$($Reference.hashMode)"
}

$results = [Collections.Generic.List[object]]::new()
function Invoke-Case([string]$Id, [scriptblock]$Body) {
    try { & $Body; [void]$results.Add([pscustomobject][ordered]@{case=$Id;status='passed';finding=$null}) }
    catch { [void]$results.Add([pscustomobject][ordered]@{case=$Id;status='failed';finding=$_.Exception.Message}) }
}

$sourceLockFull = Join-Path $root $SourceLockPath
$schemaFull = Join-Path $root $SchemaPath
$lock = Read-StrictUtf8Json $sourceLockFull
$requiredIds = @(
    'aflow',
    'auditbench',
    'autogen-magentic-one',
    'dspy-gepa',
    'envtrustbench',
    'graph-of-thoughts',
    'inspect-ai',
    'langgraph',
    'petri',
    'reflexion',
    'swe-agent-aci',
    'tree-of-thoughts'
)

Invoke-Case 'strict-schema' {
    $errors = @(Test-ESJsonSchemaValue -SchemaPath $schemaFull -Value $lock)
    if ($errors.Count -gt 0) { throw ($errors -join '; ') }
}
Invoke-Case 'required-source-coverage' {
    $actual = @($lock.sources | ForEach-Object { [string]$_.sourceId } | Sort-Object)
    if (($actual -join '|') -cne ($requiredIds -join '|')) { throw 'EXTERNAL_SOURCE_SET_MISMATCH' }
}
Invoke-Case 'immutable-repository-bindings' {
    foreach ($source in @($lock.sources)) {
        if ([string]$source.sourceType -eq 'repository') {
            if ([string]$source.commitSha -notmatch '^[a-f0-9]{40}$' -or [string]::IsNullOrWhiteSpace([string]$source.repository)) { throw "REPOSITORY_BINDING_INVALID:$($source.sourceId)" }
            foreach ($reference in @($source.contentRefs)) {
                if ([string]$reference.url -match 'raw\.githubusercontent\.com' -and [string]$reference.url -notmatch [regex]::Escape([string]$source.commitSha)) { throw "MUTABLE_RAW_URL:$($source.sourceId)" }
            }
        } elseif ($null -ne $source.commitSha -or $null -ne $source.repository) { throw "NON_REPOSITORY_COMMIT_BINDING:$($source.sourceId)" }
    }
}
Invoke-Case 'network-source-boundary' {
    foreach ($source in @($lock.sources)) {
        foreach ($reference in @($source.contentRefs)) { Assert-ExternalUri ([uri]([string]$reference.url)) }
    }
}
Invoke-Case 'network-policy-negative-fixtures' {
    $rejected = 0
    foreach ($fixture in @(
        { Assert-ExternalUri ([uri]'http://raw.githubusercontent.com/owner/repo/file') },
        { Assert-ExternalUri ([uri]'https://raw.githubusercontent.com.evil.example/owner/repo/file') },
        { Assert-ExternalUri ([uri]'https://example.org/redirect-target') },
        { Assert-NetworkDeadline ([DateTime]::UtcNow.AddSeconds(-2)) 1 },
        { Assert-ResponseSize 2049 0 2048 },
        { Assert-ResponseSize -1 2049 2048 },
        { ConvertTo-StrictUtf8Hash ([byte[]](0xC3,0x28)) | Out-Null }
    )) {
        try { & $fixture } catch { $rejected++; continue }
        throw 'NETWORK_POLICY_NEGATIVE_FIXTURE_ACCEPTED'
    }
    if ($rejected -ne 7) { throw 'NETWORK_POLICY_NEGATIVE_COVERAGE_INCOMPLETE' }
}
Invoke-Case 'license-boundary' {
    foreach ($source in @($lock.sources)) {
        if ([string]$source.licenseSpdx -eq 'NOASSERTION') {
            if ([string]$source.reusePolicy -cne 'reference-only-license-unverified') { throw "UNVERIFIED_LICENSE_REUSE:$($source.sourceId)" }
        } elseif ([string]$source.reusePolicy -cne 'mechanism-reference-only') { throw "SOURCE_COPY_POLICY_INVALID:$($source.sourceId)" }
    }
}
Invoke-Case 'adoption-authority-boundary' {
    if ([string]$lock.authority -cne 'external-design-input' -or [string]$lock.trustBoundary -cne 'untrusted-until-es-validated') { throw 'EXTERNAL_AUTHORITY_BOUNDARY_INVALID' }
    if (@($lock.nonClaims) -notcontains 'no external implementation code is copied by this source lock') { throw 'SOURCE_COPY_NONCLAIM_MISSING' }
}
Invoke-Case 'source-set-hash' {
    $actual = Get-ESABCDEvidenceHash (Get-SourceLockHashInput $lock)
    if ([string]$lock.sourceSetHash -cne $actual) { throw 'EXTERNAL_SOURCE_SET_HASH_MISMATCH' }
}
if ($VerifyNetwork) {
    Invoke-Case 'network-content-reverification' {
        $mismatches = [Collections.Generic.List[string]]::new()
        foreach ($source in @($lock.sources)) {
            foreach ($reference in @($source.contentRefs)) {
                try {
                    $actual = Get-NetworkContentHash $reference
                    if ($actual -cne [string]$reference.sha256) { [void]$mismatches.Add("EXTERNAL_CONTENT_HASH_MISMATCH:$($source.sourceId):$($reference.hashMode):$($reference.sha256):${actual}:$($reference.url)") }
                } catch { [void]$mismatches.Add("EXTERNAL_CONTENT_REVERIFY_FAILED:$($source.sourceId):$($reference.hashMode):$($reference.url):$($_.Exception.Message)") }
            }
        }
        if ($mismatches.Count -gt 0) { throw ($mismatches -join ';') }
    }
}

$failed = @($results | Where-Object status -eq 'failed')
$sourceRefs = @(
    'ES/Automation/ABCD/Test-ESABCDExternalSourceLock.ps1',
    'ES/Automation/ABCD/ESABCDEvidence.psm1',
    'ES/Automation/Contracts/es-agent-mechanism-source-lock-v1.schema.json',
    'Documentation/AIKnowledge/ExternalSources/agent-mechanism-source-lock.v1.json'
)
$sourceHashes = [ordered]@{}
foreach ($sourceRef in $sourceRefs) { $sourceHashes[$sourceRef] = (Get-FileHash -LiteralPath (Join-Path $root $sourceRef) -Algorithm SHA256).Hash.ToLowerInvariant() }
$evidenceContractPath = Join-Path $root 'ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json'
$evidenceContractHash = (Get-FileHash -LiteralPath $evidenceContractPath -Algorithm SHA256).Hash.ToLowerInvariant()
$planHash = Get-ESABCDEvidenceHash ([ordered]@{validator='Test-ESABCDExternalSourceLock';sourceRefHashes=$sourceHashes;verifyNetwork=[bool]$VerifyNetwork;sourceSetHash=[string]$lock.sourceSetHash})
$userInstructionHash = if ($VerifyNetwork) {
    Get-ESABCDEvidenceHash ([ordered]@{ operation = 'VerifyNetwork'; sourceLockPath = $SourceLockPath; schemaPath = $SchemaPath; reportPath = $ReportPath; networkTimeoutSeconds = $NetworkTimeoutSeconds; maxResponseBytes = $MaxResponseBytes; maxRedirects = $MaxRedirects })
} else { $null }
$report = [ordered]@{
    schemaVersion = 1
    validator = 'Test-ESABCDExternalSourceLock'
    status = if ($failed.Count) { 'failed' } else { 'passed' }
    caseCount = $results.Count
    passedCount = @($results | Where-Object status -eq 'passed').Count
    failedCount = $failed.Count
    cases = @($results)
    staticStatus = if ($failed.Count) { 'static-failed' } else { 'static-passed' }
    runtimeStatus = 'runtime-not-run'
    networkStatus = if ($VerifyNetwork -and -not $failed.Count) { 'network-verified' } elseif ($VerifyNetwork) { 'network-failed' } else { 'network-not-run' }
    networkPolicy = [ordered]@{ allowedHosts = @($allowedHosts); requireHttps = $true; timeoutSeconds = $NetworkTimeoutSeconds; maxResponseBytes = $MaxResponseBytes; maxRedirects = $MaxRedirects; utf8DecodedBody = 'strict-utf8-decode-then-encode' }
    networkReferenceCount = @($lock.sources.contentRefs).Count
    evidenceLevel = 'S1'
    capturedUtc = [DateTime]::UtcNow.ToString('o')
    authorizationKind = if ($VerifyNetwork) { 'current-user-direct' } else { 'read-only' }
    planHash = $planHash
    evidenceContractId = 'es.skill-evidence-receipt'
    evidenceContractHash = $evidenceContractHash
    skillName = 'es-agent-mechanism-replication'
    case = 'external-source-lock'
    receiptPath = $ReportPath.Replace('\','/')
    sourceRefs = $sourceRefs
    sourceRefHashes = $sourceHashes
    toolId = 'es-abcd-external-source-lock-validator'
    unityVersion = 'not-run'
    sourceSetHash = [string]$lock.sourceSetHash
    claimsNotProven = @('external sources prove ES implementation correctness','Unity/Worker/host Runtime','external authority certification')
}
if ($VerifyNetwork) {
    $report.userInstructionHash = $userInstructionHash
    $report.authorizedOperations = @('read-external-source-lock','verify-network-content-hashes')
    $report.authorizedPaths = @($SourceLockPath,$SchemaPath,$ReportPath)
}
$fullReport = Join-Path $root $ReportPath
New-Item -ItemType Directory -Path (Split-Path $fullReport) -Force | Out-Null
[IO.File]::WriteAllText($fullReport, ($report | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
$report | ConvertTo-Json -Depth 20
if ($failed.Count) { exit 1 }
