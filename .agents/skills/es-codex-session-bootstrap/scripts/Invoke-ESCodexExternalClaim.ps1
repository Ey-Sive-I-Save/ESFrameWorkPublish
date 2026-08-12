[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Prepare', 'Respond', 'Finalize', 'Cancel')]
    [string]$Action,

    [string]$ClaimId = '',

    [string]$ClaimToken = '',

    [string]$SessionId = '',

    [string]$ExternalBindingId = '',

    [int]$ExpectedCmdProcessId = 0,

    [string]$ExpectedCmdProcessStartedAtUtc = '',

    [string]$TaskKey = '',

    [string]$ResponsibilityKey = '',

    [string]$TabTitle = '',

    [ValidateRange(60, 600)]
    [int]$TtlSeconds = 300,

    [string]$StateRoot = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

. (Join-Path $PSScriptRoot 'ESCodexSessionState.ps1')

$fixedProjectRoot = 'F:\aaProject\ESFrameWorkPublish'
$skillDirectory = Split-Path -Parent $PSScriptRoot
$skillsDirectory = Split-Path -Parent $skillDirectory
$agentsDirectory = Split-Path -Parent $skillsDirectory
$installedProjectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $agentsDirectory)).TrimEnd('\')
if (-not $installedProjectRoot.Equals($fixedProjectRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "External CMD claim script is not installed under the fixed ESFramework root: $fixedProjectRoot"
}

function Get-ESCodexExternalClaimStateRoot {
    if (-not [string]::IsNullOrWhiteSpace($StateRoot)) {
        return [IO.Path]::GetFullPath($StateRoot).TrimEnd('\')
    }
    return (Get-ESCodexLocalStateRoot).TrimEnd('\')
}

function Get-ESCodexExternalClaimHash([string]$Path) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    $hash = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($hash.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $hash.Dispose()
        $stream.Dispose()
    }
}

function New-ESCodexExternalClaimToken {
    $bytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) }
    finally { $rng.Dispose() }
    return ([BitConverter]::ToString($bytes)).Replace('-', '').ToLowerInvariant()
}

function Assert-ESCodexExternalClaimGuid([string]$Value, [string]$Label) {
    $parsed = [Guid]::Empty
    if ([string]::IsNullOrWhiteSpace($Value) -or -not [Guid]::TryParse($Value.Trim(), [ref]$parsed)) {
        throw "$Label must be an exact UUID."
    }
    return $parsed.ToString()
}

function Assert-ESCodexExternalClaimCmdIdentity(
    [int]$ProcessId,
    [string]$StartedAtUtc,
    [string]$Label
) {
    if ($ProcessId -le 0) { throw "$Label CMD process id must be positive." }
    $expectedStart = [DateTime]::MinValue
    if ([string]::IsNullOrWhiteSpace($StartedAtUtc) -or -not [DateTime]::TryParse($StartedAtUtc, [ref]$expectedStart)) {
        throw "$Label CMD process start time is invalid."
    }
    try {
        $cmd = Get-Process -Id $ProcessId -ErrorAction Stop
        if ($cmd.HasExited -or -not $cmd.ProcessName.Equals('cmd', [StringComparison]::OrdinalIgnoreCase)) {
            throw "$Label process is not an active cmd.exe process."
        }
        if ($cmd.StartTime.ToUniversalTime().Ticks -ne $expectedStart.ToUniversalTime().Ticks) {
            throw "$Label CMD PID was reused or its start identity changed."
        }
        return [pscustomobject]@{
            processId = $cmd.Id
            startedAtUtc = $expectedStart.ToUniversalTime().ToString('o')
        }
    }
    catch {
        throw "$Label CMD process cannot be verified: $($_.Exception.Message)"
    }
}

function Assert-ESCodexExternalClaimToken([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -notmatch '^[a-f0-9]{64}$') {
        throw 'ClaimToken must be a 256-bit lowercase hexadecimal token.'
    }
    return $Value.Trim().ToLowerInvariant()
}

function Assert-ESCodexExternalClaimSafeExistingPath([string]$Path, [string]$Root) {
    $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    if (-not ($fullPath.Equals($fullRoot, [StringComparison]::OrdinalIgnoreCase) -or
            $fullPath.StartsWith($fullRoot + '\', [StringComparison]::OrdinalIgnoreCase))) {
        throw "External claim path escaped its managed root: $Path"
    }

    $current = $fullRoot
    if (Test-Path -LiteralPath $current) {
        if (((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "External claim managed root cannot be a reparse point: $current"
        }
    }
    $relative = $fullPath.Substring($fullRoot.Length).TrimStart('\')
    foreach ($segment in @($relative -split '\\')) {
        if ([string]::IsNullOrWhiteSpace($segment)) { continue }
        $current = Join-Path $current $segment
        if (-not (Test-Path -LiteralPath $current)) { break }
        if (((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "External claim path contains a reparse point: $current"
        }
    }
}

function Get-ESCodexExternalClaimDirectory([string]$Root, [string]$Id) {
    $cleanId = Assert-ESCodexExternalClaimGuid $Id 'ClaimId'
    $path = [IO.Path]::GetFullPath((Join-Path $Root $cleanId))
    Assert-ESCodexExternalClaimSafeExistingPath $path $Root
    return $path
}

function Write-ESCodexExternalClaimCreateOnly([string]$Path, [object]$Value) {
    $json = if ($Value -is [string]) { [string]$Value } else { $Value | ConvertTo-Json -Depth 12 }
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($json)
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    }
    finally { $stream.Dispose() }
}

function Write-ESCodexExternalClaimAtomicCreateOnly([string]$Directory, [string]$Name, [object]$Value) {
    $target = Join-Path $Directory $Name
    Assert-ESCodexExternalClaimSafeExistingPath $target $Directory
    $temporary = Join-Path $Directory ($Name + '.tmp-' + [Guid]::NewGuid().ToString('N'))
    $temporaryOwned = $false
    try {
        Write-ESCodexExternalClaimCreateOnly $temporary $Value
        $temporaryOwned = $true
        [IO.File]::Move($temporary, $target)
        $temporaryOwned = $false
    }
    finally {
        if ($temporaryOwned -and (Test-Path -LiteralPath $temporary -PathType Leaf)) {
            Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue
        }
    }
    return $target
}

function Read-ESCodexExternalClaimJson([string]$Path, [string]$Root) {
    Assert-ESCodexExternalClaimSafeExistingPath $Path $Root
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "External claim artifact was not found: $Path" }
    try { return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { throw "External claim artifact is invalid JSON: $Path`n$($_.Exception.Message)" }
}

function Get-ESCodexExternalClaimProperty([object]$Object, [string]$Name) {
    return [string](Get-ESCodexPropertyValue $Object $Name '')
}

function Test-ESCodexExternalClaimExpiration([object]$Request) {
    $expiresText = Get-ESCodexExternalClaimProperty $Request 'expiresAtUtc'
    $expiresAt = [DateTime]::MinValue
    if (-not [DateTime]::TryParse($expiresText, [ref]$expiresAt)) { throw 'Claim request expiration is invalid.' }
    if ($expiresAt.ToUniversalTime() -lt [DateTime]::UtcNow) { throw 'Claim request expired. Create a fresh claim; do not reuse this token.' }
}

function Assert-ESCodexExternalClaimRequest([object]$Request, [string]$ExpectedId = '', [string]$ExpectedToken = '', [switch]$AllowExpired) {
    $schemaVersion = [int](Get-ESCodexPropertyValue $Request 'schemaVersion' 0)
    if ($schemaVersion -ne 1 -and $schemaVersion -ne 2) { throw 'Unsupported external claim request schema.' }
    $claimId = Assert-ESCodexExternalClaimGuid (Get-ESCodexExternalClaimProperty $Request 'claimId') 'Request ClaimId'
    $token = Assert-ESCodexExternalClaimToken (Get-ESCodexExternalClaimProperty $Request 'claimToken')
    $sessionId = ''
    $externalBindingId = ''
    $expectedCmd = $null
    if ($schemaVersion -eq 1) {
        $sessionId = Assert-ESCodexExternalClaimGuid (Get-ESCodexExternalClaimProperty $Request 'requestedSessionId') 'Request SessionId'
    }
    else {
        if (-not [string]::IsNullOrWhiteSpace((Get-ESCodexExternalClaimProperty $Request 'requestedSessionId'))) {
            throw 'External CMD binding requests cannot claim a Codex SessionId.'
        }
        $externalBindingId = Assert-ESCodexExternalClaimGuid (Get-ESCodexExternalClaimProperty $Request 'externalBindingId') 'External CMD binding identity'
        $expectedCmdProcessId = [int](Get-ESCodexPropertyValue $Request 'expectedCmdProcessId' 0)
        $expectedCmdProcessStartedAtUtc = Get-ESCodexExternalClaimProperty $Request 'expectedCmdProcessStartedAtUtc'
        if ($AllowExpired) {
            $parsedExpectedStart = [DateTime]::MinValue
            if ($expectedCmdProcessId -le 0 -or [string]::IsNullOrWhiteSpace($expectedCmdProcessStartedAtUtc) -or
                -not [DateTime]::TryParse($expectedCmdProcessStartedAtUtc, [ref]$parsedExpectedStart)) {
                throw 'External CMD binding request has an invalid selected CMD identity.'
            }
            $expectedCmd = [pscustomobject]@{
                processId = $expectedCmdProcessId
                startedAtUtc = $parsedExpectedStart.ToUniversalTime().ToString('o')
            }
        }
        else {
            $expectedCmd = Assert-ESCodexExternalClaimCmdIdentity `
                $expectedCmdProcessId `
                $expectedCmdProcessStartedAtUtc `
                'Selected external'
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($ExpectedId) -and -not $claimId.Equals($ExpectedId, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Claim request identity does not match the requested ClaimId.'
    }
    if (-not [string]::IsNullOrWhiteSpace($ExpectedToken) -and -not $token.Equals($ExpectedToken, [StringComparison]::Ordinal)) {
        throw 'Claim request token does not match.'
    }
    if (-not (Get-ESCodexExternalClaimProperty $Request 'projectRoot').Equals($fixedProjectRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Claim request project root is not the fixed ESFramework root.'
    }
    if (-not $AllowExpired) { Test-ESCodexExternalClaimExpiration $Request }
    return [pscustomobject]@{
        schemaVersion = $schemaVersion
        claimId = $claimId
        claimToken = $token
        sessionId = $sessionId
        externalBindingId = $externalBindingId
        expectedCmdProcessId = if ($null -eq $expectedCmd) { 0 } else { [int]$expectedCmd.processId }
        expectedCmdProcessStartedAtUtc = if ($null -eq $expectedCmd) { '' } else { [string]$expectedCmd.startedAtUtc }
    }
}

function Assert-ESCodexExternalClaimResponse([object]$Request, [object]$Response, [string]$RequestHash) {
    $identity = Assert-ESCodexExternalClaimRequest $Request
    $responseSchemaVersion = [int](Get-ESCodexPropertyValue $Response 'schemaVersion' 0)
    if ($responseSchemaVersion -ne $identity.schemaVersion -or ($responseSchemaVersion -ne 1 -and $responseSchemaVersion -ne 2)) {
        throw 'External claim response schema does not match its request.'
    }
    if (-not (Get-ESCodexExternalClaimProperty $Response 'claimId').Equals($identity.claimId, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Claim response ClaimId does not match its request.'
    }
    if (-not (Get-ESCodexExternalClaimProperty $Response 'claimToken').Equals($identity.claimToken, [StringComparison]::Ordinal)) {
        throw 'Claim response token does not match its request.'
    }
    if ($identity.schemaVersion -eq 1) {
        if (-not (Get-ESCodexExternalClaimProperty $Response 'requestedSessionId').Equals($identity.sessionId, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Claim response SessionId does not match its request.'
        }
    }
    elseif (-not (Get-ESCodexExternalClaimProperty $Response 'externalBindingId').Equals($identity.externalBindingId, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Claim response external CMD binding identity does not match its request.'
    }
    if (-not (Get-ESCodexExternalClaimProperty $Response 'requestSha256').Equals($RequestHash, [StringComparison]::Ordinal)) {
        throw 'Claim response is not bound to the current request bytes.'
    }
    if (-not (Get-ESCodexExternalClaimProperty $Response 'projectRoot').Equals($fixedProjectRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Claim response project root is not the fixed ESFramework root.'
    }
    $cmdProcessId = [int](Get-ESCodexPropertyValue $Response 'cmdProcessId' 0)
    if ($cmdProcessId -le 0) { throw 'Claim response does not identify a CMD process.' }
    $expectedStarted = Get-ESCodexExternalClaimProperty $Response 'cmdProcessStartedAtUtc'
    $expectedStart = [DateTime]::MinValue
    if (-not [DateTime]::TryParse($expectedStarted, [ref]$expectedStart)) { throw 'Claim response CMD start time is invalid.' }
    try {
        $cmd = Get-Process -Id $cmdProcessId -ErrorAction Stop
        if ($cmd.HasExited -or -not $cmd.ProcessName.Equals('cmd', [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Claimed CMD process is no longer an active cmd.exe process.'
        }
        if ($cmd.StartTime.ToUniversalTime().Ticks -ne $expectedStart.ToUniversalTime().Ticks) {
            throw 'Claimed CMD PID was reused or its start identity changed.'
        }
    }
    catch {
        throw "Claimed CMD process cannot be verified: $($_.Exception.Message)"
    }
    if ($identity.schemaVersion -eq 2 -and ($cmdProcessId -ne $identity.expectedCmdProcessId -or
            $expectedStart.ToUniversalTime().Ticks -ne ([DateTime]::Parse($identity.expectedCmdProcessStartedAtUtc)).ToUniversalTime().Ticks)) {
        throw 'Claim response was produced by a different CMD than the user-selected candidate.'
    }
    return [pscustomobject]@{
        schemaVersion = $identity.schemaVersion
        claimId = $identity.claimId
        sessionId = $identity.sessionId
        externalBindingId = $identity.externalBindingId
        expectedCmdProcessId = $identity.expectedCmdProcessId
        expectedCmdProcessStartedAtUtc = $identity.expectedCmdProcessStartedAtUtc
        cmdProcessId = $cmdProcessId
        cmdProcessStartedAtUtc = $expectedStart.ToUniversalTime().ToString('o')
    }
}

function New-ESCodexExternalClaimDirectory([string]$ClaimsRoot, [string]$RequestedClaimId = '') {
    [void][IO.Directory]::CreateDirectory($ClaimsRoot)
    Assert-ESCodexExternalClaimSafeExistingPath $ClaimsRoot $ClaimsRoot
    if (-not [string]::IsNullOrWhiteSpace($RequestedClaimId)) {
        $claimId = Assert-ESCodexExternalClaimGuid $RequestedClaimId 'ClaimId'
        $directory = Join-Path $ClaimsRoot $claimId
        if (Test-Path -LiteralPath $directory) {
            throw "External claim directory already exists: $directory"
        }
        [void][IO.Directory]::CreateDirectory($directory)
        Assert-ESCodexExternalClaimSafeExistingPath $directory $ClaimsRoot
        Write-ESCodexExternalClaimCreateOnly (Join-Path $directory '.claim-owner.json') ([ordered]@{
                schemaVersion = 1; claimId = $claimId; createdUtc = [DateTime]::UtcNow.ToString('o'); processId = $PID
            })
        return [pscustomobject]@{ claimId = $claimId; directory = $directory }
    }
    for ($attempt = 0; $attempt -lt 12; $attempt++) {
        $claimId = [Guid]::NewGuid().ToString()
        $directory = Join-Path $ClaimsRoot $claimId
        [void][IO.Directory]::CreateDirectory($directory)
        try {
            Assert-ESCodexExternalClaimSafeExistingPath $directory $ClaimsRoot
            Write-ESCodexExternalClaimCreateOnly (Join-Path $directory '.claim-owner.json') ([ordered]@{
                    schemaVersion = 1; claimId = $claimId; createdUtc = [DateTime]::UtcNow.ToString('o'); processId = $PID
                })
            return [pscustomobject]@{ claimId = $claimId; directory = $directory }
        }
        catch [IO.IOException] {
            # The directory or owner marker belongs to another writer. Preserve it and retry.
        }
    }
    throw 'Could not allocate a unique external CMD claim directory.'
}

function New-ESCodexExternalClaimPrepareResult([object]$Identity, [object]$Request, [string]$Directory, [string]$RequestPath) {
    $claimScript = Join-Path $PSScriptRoot 'Claim-ESCodexExternalTerminal.ps1'
    if (-not (Test-Path -LiteralPath $claimScript -PathType Leaf)) { throw "External claim responder was not found: $claimScript" }
    $stateArgument = if ([string]::IsNullOrWhiteSpace($StateRoot)) { '' } else {
        ' -StateRoot "' + (Get-ESCodexExternalClaimStateRoot).Replace('"', '""') + '"'
    }
    $command = 'powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + $claimScript.Replace('"', '""') + '" -ClaimId ' + $Identity.claimId + ' -ClaimToken ' + $Identity.claimToken + $stateArgument
    return [pscustomobject][ordered]@{
        mode = 'PrepareExternalClaim'
        claimState = 'Prepared'
        claimId = $Identity.claimId
        requestedSessionId = $Identity.sessionId
        externalBindingId = $Identity.externalBindingId
        claimDirectory = $Directory
        requestPath = $RequestPath
        requestSha256 = Get-ESCodexExternalClaimHash $RequestPath
        expiresAtUtc = [string]$Request.expiresAtUtc
        command = $command
        requiresTargetCmdExecution = $true
        capabilities = @('StatusRefresh')
        deniedCapabilities = @('SendMessage', 'Focus', 'Close', 'Resume', 'InputInjection')
    }
}

function Find-ESCodexPendingExternalClaim([string]$ClaimsRoot, [string]$RequestedSessionId) {
    Assert-ESCodexExternalClaimSafeExistingPath $ClaimsRoot $ClaimsRoot
    if (-not (Test-Path -LiteralPath $ClaimsRoot -PathType Container)) { return $null }
    $directories = @(Get-ChildItem -LiteralPath $ClaimsRoot -Directory -Force | Select-Object -First 513)
    if ($directories.Count -gt 512) {
        throw 'External claim root exceeds the bounded pending-claim scan limit. Preserve the artifacts and repair them explicitly.'
    }
    foreach ($directory in $directories) {
        $candidateId = [Guid]::Empty
        if (-not [Guid]::TryParse($directory.Name, [ref]$candidateId)) { continue }
        Assert-ESCodexExternalClaimSafeExistingPath $directory.FullName $ClaimsRoot
        $requestPath = Join-Path $directory.FullName 'request.json'
        if (-not (Test-Path -LiteralPath $requestPath -PathType Leaf)) { continue }
        $request = Read-ESCodexExternalClaimJson $requestPath $ClaimsRoot
        $requestedSessionText = Get-ESCodexExternalClaimProperty $request 'requestedSessionId'
        $requestedSession = [Guid]::Empty
        if (-not [Guid]::TryParse($requestedSessionText, [ref]$requestedSession) -or
            -not $requestedSession.ToString().Equals($RequestedSessionId, [StringComparison]::OrdinalIgnoreCase)) {
            continue
        }
        $expiresText = Get-ESCodexExternalClaimProperty $request 'expiresAtUtc'
        $expiresAt = [DateTime]::MinValue
        if (-not [DateTime]::TryParse($expiresText, [ref]$expiresAt)) {
            throw "External claim request for the exact SessionId has an invalid expiration: $requestPath"
        }
        if ($expiresAt.ToUniversalTime() -lt [DateTime]::UtcNow) {
            continue
        }
        $identity = Assert-ESCodexExternalClaimRequest $request $candidateId.ToString()
        return [pscustomobject]@{ identity = $identity; request = $request; directory = $directory.FullName; requestPath = $requestPath }
    }
    return $null
}

function Invoke-ESCodexExternalClaimPrepare([string]$ClaimsRoot) {
    $hasSessionId = -not [string]::IsNullOrWhiteSpace($SessionId)
    $hasExternalBinding = -not [string]::IsNullOrWhiteSpace($ExternalBindingId)
    if ($hasSessionId -eq $hasExternalBinding) {
        throw 'PrepareExternalClaim requires exactly one identity: SessionId for a known Codex conversation or ExternalBindingId for an existing CMD.'
    }
    $sessionId = if ($hasSessionId) { Assert-ESCodexExternalClaimGuid $SessionId 'SessionId' } else { '' }
    $externalBindingId = if ($hasExternalBinding) {
        Assert-ESCodexExternalClaimGuid $ExternalBindingId 'External CMD binding identity'
    } else { '' }
    $expectedCmd = if ($hasExternalBinding) {
        Assert-ESCodexExternalClaimCmdIdentity $ExpectedCmdProcessId $ExpectedCmdProcessStartedAtUtc 'Selected external'
    } else { $null }
    $identityKey = if ($hasSessionId) { $sessionId } else { $externalBindingId }
    $prepareMutex = [Threading.Mutex]::new($false, 'ESFrameworkCodexExternalClaimPrepare_' + $identityKey.Replace('-', ''))
    $prepareMutexAcquired = $false
    try {
        try { $prepareMutexAcquired = $prepareMutex.WaitOne(5000) }
        catch [Threading.AbandonedMutexException] { $prepareMutexAcquired = $true }
        if (-not $prepareMutexAcquired) { throw 'Timed out waiting for the exact external-claim preparation mutex.' }

        $requestedClaimId = if ([string]::IsNullOrWhiteSpace($ClaimId)) { '' } else { Assert-ESCodexExternalClaimGuid $ClaimId 'ClaimId' }
        if (-not [string]::IsNullOrWhiteSpace($requestedClaimId)) {
            $existingDirectory = Get-ESCodexExternalClaimDirectory $ClaimsRoot $requestedClaimId
            $existingRequestPath = Join-Path $existingDirectory 'request.json'
            if (Test-Path -LiteralPath $existingRequestPath -PathType Leaf) {
                $existingRequest = Read-ESCodexExternalClaimJson $existingRequestPath $ClaimsRoot
                $existingIdentity = Assert-ESCodexExternalClaimRequest $existingRequest $requestedClaimId
                if ($hasSessionId -and -not $existingIdentity.sessionId.Equals($sessionId, [StringComparison]::OrdinalIgnoreCase)) {
                    throw 'Existing external claim request belongs to a different SessionId.'
                }
                if ($hasExternalBinding -and -not $existingIdentity.externalBindingId.Equals($externalBindingId, [StringComparison]::OrdinalIgnoreCase)) {
                    throw 'Existing external claim request belongs to a different external CMD binding.'
                }
                return New-ESCodexExternalClaimPrepareResult $existingIdentity $existingRequest $existingDirectory $existingRequestPath
            }
            if (Test-Path -LiteralPath $existingDirectory -PathType Container) {
                throw 'External claim directory exists without a request. Preserve it for diagnosis; do not reuse it.'
            }
        }
        if ($hasSessionId) {
            $pending = Find-ESCodexPendingExternalClaim $ClaimsRoot $sessionId
            if ($null -ne $pending) {
                throw 'A non-expired external claim already exists for this exact SessionId. Reuse its ClaimId after reload or wait for expiry; do not create a competing target-CMD command.'
            }
        }
        $claim = New-ESCodexExternalClaimDirectory $ClaimsRoot $requestedClaimId
        $token = New-ESCodexExternalClaimToken
        $created = [DateTime]::UtcNow
        $safeTaskKey = if ($null -eq $TaskKey) { '' } else { $TaskKey.Trim() }
        $safeResponsibilityKey = if ($null -eq $ResponsibilityKey) { '' } else { $ResponsibilityKey.Trim() }
        $safeTabTitle = if ($null -eq $TabTitle) { '' } else { $TabTitle.Trim() }
        $request = [ordered]@{
            schemaVersion = if ($hasSessionId) { 1 } else { 2 }
            claimId = $claim.claimId
            claimToken = $token
            projectRoot = $fixedProjectRoot
            requestedSessionId = $sessionId
            externalBindingId = $externalBindingId
            expectedCmdProcessId = if ($null -eq $expectedCmd) { 0 } else { [int]$expectedCmd.processId }
            expectedCmdProcessStartedAtUtc = if ($null -eq $expectedCmd) { '' } else { [string]$expectedCmd.startedAtUtc }
            taskKey = $safeTaskKey
            responsibilityKey = $safeResponsibilityKey
            tabTitle = $safeTabTitle
            createdUtc = $created.ToString('o')
            expiresAtUtc = $created.AddSeconds($TtlSeconds).ToString('o')
            preparedByProcessId = $PID
        }
        $requestPath = Join-Path $claim.directory 'request.json'
        # Target CMD must never observe a partially written request file.
        Write-ESCodexExternalClaimAtomicCreateOnly $claim.directory 'request.json' $request | Out-Null
        $verified = Read-ESCodexExternalClaimJson $requestPath $ClaimsRoot
        $identity = Assert-ESCodexExternalClaimRequest $verified $claim.claimId $token
        return New-ESCodexExternalClaimPrepareResult $identity $verified $claim.directory $requestPath
    }
    finally {
        if ($prepareMutexAcquired) { $prepareMutex.ReleaseMutex() }
        $prepareMutex.Dispose()
    }
}

function Invoke-ESCodexExternalClaimRespond([string]$ClaimsRoot) {
    $claimId = Assert-ESCodexExternalClaimGuid $ClaimId 'ClaimId'
    $token = Assert-ESCodexExternalClaimToken $ClaimToken
    $directory = Get-ESCodexExternalClaimDirectory $ClaimsRoot $claimId
    $requestPath = Join-Path $directory 'request.json'
    $request = Read-ESCodexExternalClaimJson $requestPath $ClaimsRoot
    $identity = Assert-ESCodexExternalClaimRequest $request $claimId $token
    $parent = Get-CimInstance Win32_Process -Filter "ProcessId=$([int]$PID)" -OperationTimeoutSec 2 -ErrorAction Stop
    $cmdProcessId = [int]$parent.ParentProcessId
    if ($cmdProcessId -le 0) { throw 'External CMD claim must be executed from a cmd.exe shell.' }
    $cmd = Get-Process -Id $cmdProcessId -ErrorAction Stop
    if ($cmd.HasExited -or -not $cmd.ProcessName.Equals('cmd', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'External CMD claim must be executed by a PowerShell child of the target cmd.exe shell.'
    }
    if ($identity.schemaVersion -eq 2 -and ($cmd.Id -ne $identity.expectedCmdProcessId -or
            $cmd.StartTime.ToUniversalTime().Ticks -ne ([DateTime]::Parse($identity.expectedCmdProcessStartedAtUtc)).ToUniversalTime().Ticks)) {
        throw 'This one-time command was executed in a different CMD than the selected candidate. Return to the selected CMD and retry, or cancel and choose the correct CMD.'
    }
    $response = [ordered]@{
        schemaVersion = $identity.schemaVersion
        claimId = $identity.claimId
        claimToken = $identity.claimToken
        requestedSessionId = $identity.sessionId
        externalBindingId = $identity.externalBindingId
        projectRoot = $fixedProjectRoot
        requestSha256 = Get-ESCodexExternalClaimHash $requestPath
        respondedUtc = [DateTime]::UtcNow.ToString('o')
        cmdProcessId = $cmd.Id
        cmdProcessStartedAtUtc = $cmd.StartTime.ToUniversalTime().ToString('o')
        responderProcessId = $PID
    }
    $responsePath = Write-ESCodexExternalClaimAtomicCreateOnly $directory 'response.json' $response
    return [pscustomobject][ordered]@{
        mode = 'RespondExternalClaim'; claimState = 'Responded'; claimId = $identity.claimId
        requestedSessionId = $identity.sessionId; externalBindingId = $identity.externalBindingId
        responsePath = $responsePath; cmdProcessId = $cmd.Id
    }
}

function Invoke-ESCodexExternalClaimFinalize([string]$ClaimsRoot) {
    $claimId = Assert-ESCodexExternalClaimGuid $ClaimId 'ClaimId'
    $directory = Get-ESCodexExternalClaimDirectory $ClaimsRoot $claimId
    $mutex = [Threading.Mutex]::new($false, 'ESFrameworkCodexExternalClaim_' + $claimId.Replace('-', ''))
    $acquired = $false
    try {
        try { $acquired = $mutex.WaitOne(5000) }
        catch [Threading.AbandonedMutexException] { $acquired = $true }
        if (-not $acquired) { throw 'Timed out waiting for the exact external claim mutex.' }

        $requestPath = Join-Path $directory 'request.json'
        $responsePath = Join-Path $directory 'response.json'
        $receiptPath = Join-Path $directory 'finalize-receipt.json'
        $cancelPath = Join-Path $directory 'cancel-receipt.json'
        $request = Read-ESCodexExternalClaimJson $requestPath $ClaimsRoot
        $identity = Assert-ESCodexExternalClaimRequest $request $claimId
        $requestHash = Get-ESCodexExternalClaimHash $requestPath
        if (Test-Path -LiteralPath $cancelPath -PathType Leaf) {
            $cancelReceipt = Read-ESCodexExternalClaimJson $cancelPath $ClaimsRoot
            if ((Get-ESCodexExternalClaimProperty $cancelReceipt 'claimId') -ne $identity.claimId -or
                (Get-ESCodexExternalClaimProperty $cancelReceipt 'requestSha256') -ne $requestHash) {
                throw 'External claim cancellation receipt does not match this exact request.'
            }
            throw 'External claim was cancelled before finalization; create a fresh claim.'
        }
        $response = Read-ESCodexExternalClaimJson $responsePath $ClaimsRoot
        $verified = Assert-ESCodexExternalClaimResponse $request $response $requestHash
        $responseHash = Get-ESCodexExternalClaimHash $responsePath

        if (Test-Path -LiteralPath $receiptPath -PathType Leaf) {
            $receipt = Read-ESCodexExternalClaimJson $receiptPath $ClaimsRoot
            if ((Get-ESCodexExternalClaimProperty $receipt 'claimId') -ne $verified.claimId -or
                (Get-ESCodexExternalClaimProperty $receipt 'requestSha256') -ne $requestHash -or
                (Get-ESCodexExternalClaimProperty $receipt 'responseSha256') -ne $responseHash) {
                throw 'Existing external claim receipt does not match this exact request and response.'
            }
            return [pscustomobject]$receipt
        }

        $registryPath = Join-Path (Get-ESCodexExternalClaimStateRoot) 'sessions.json'
        $argument = [pscustomobject]@{
            request = $request; verified = $verified; directory = $directory; requestHash = $requestHash; responseHash = $responseHash
        }
        $record = Invoke-ESCodexRegistryUpdate $registryPath {
            param($registry, $arg)
            $sameClaim = @($registry.sessions | Where-Object { [string]$_.externalClaimId -eq [string]$arg.verified.claimId })
            if ($sameClaim.Count -gt 1) { throw 'Registry contains duplicate external claim identities.' }
            if ($sameClaim.Count -eq 1) {
                $existing = $sameClaim[0]
                if ([string]$existing.sessionId -ne [string]$arg.verified.sessionId -or
                    [string]$existing.externalClaimBindingId -ne [string]$arg.verified.externalBindingId -or
                    [int]$existing.externalClaimProcessId -ne [int]$arg.verified.cmdProcessId -or
                    [string]$existing.externalClaimDirectory -ne [string]$arg.directory) {
                    throw 'Registry external claim identity conflicts with its immutable response.'
                }
                return $existing
            }
            if (-not [string]::IsNullOrWhiteSpace([string]$arg.verified.sessionId)) {
                $sessionMatches = @($registry.sessions | Where-Object { [string]$_.sessionId -eq [string]$arg.verified.sessionId })
                if ($sessionMatches.Count -gt 0) {
                    throw 'This exact SessionId is already registered. External claims never steal, merge, or replace existing session authority.'
                }
            }
            if (-not [string]::IsNullOrWhiteSpace([string]$arg.verified.externalBindingId)) {
                $bindingMatches = @($registry.sessions | Where-Object {
                        [string]$_.externalClaimBindingId -eq [string]$arg.verified.externalBindingId
                    })
                if ($bindingMatches.Count -gt 0) {
                    throw 'This external CMD binding identity is already registered. It cannot be merged or reused.'
                }
            }
            $requestTaskKey = [string]$arg.request.taskKey
            $requestResponsibilityKey = [string]$arg.request.responsibilityKey
            $requestTabTitle = [string]$arg.request.tabTitle
            $identityKey = if ([string]::IsNullOrWhiteSpace([string]$arg.verified.sessionId)) {
                [string]$arg.verified.externalBindingId
            } else {
                [string]$arg.verified.sessionId
            }
            $newRecord = [pscustomobject][ordered]@{
                identityVersion = 1
                sessionId = [string]$arg.verified.sessionId
                projectKey = 'ESFramework'
                projectRoot = $fixedProjectRoot
                responsibilityKey = if ([string]::IsNullOrWhiteSpace($requestResponsibilityKey)) { 'external-cmd' } else { $requestResponsibilityKey }
                taskKey = if ([string]::IsNullOrWhiteSpace($requestTaskKey)) { 'external-claim:' + $identityKey } else { $requestTaskKey }
                tabTitle = if ([string]::IsNullOrWhiteSpace($requestTabTitle)) { 'External CMD ' + $identityKey.Substring(0, 8) } else { $requestTabTitle }
                terminalMode = 'ExternalClaim'
                processId = [int]$arg.verified.cmdProcessId
                lifecycleStatus = 'ClaimedExternal'
                contextAccepted = $false
                externalClaimId = [string]$arg.verified.claimId
                externalClaimBindingId = [string]$arg.verified.externalBindingId
                externalClaimState = 'ClaimedExternal'
                externalClaimDirectory = [string]$arg.directory
                externalClaimRequestSha256 = [string]$arg.requestHash
                externalClaimResponseSha256 = [string]$arg.responseHash
                externalClaimProcessId = [int]$arg.verified.cmdProcessId
                externalClaimProcessStartedAtUtc = [string]$arg.verified.cmdProcessStartedAtUtc
                externalClaimExpectedCmdProcessId = [int]$arg.verified.expectedCmdProcessId
                externalClaimExpectedCmdProcessStartedAtUtc = [string]$arg.verified.expectedCmdProcessStartedAtUtc
                externalClaimAcceptedAtUtc = [DateTime]::UtcNow.ToString('o')
                availability = 'Unknown'
            }
            return AddOrUpdate-ESCodexSessionRecord $registry $newRecord
        } -Argument $argument

        $receipt = [ordered]@{
            schemaVersion = 2
            mode = 'FinalizeExternalClaim'
            claimState = 'ClaimedExternal'
            claimId = $verified.claimId
            requestedSessionId = $verified.sessionId
            externalBindingId = $verified.externalBindingId
            recordId = [string]$record.recordId
            cmdProcessId = $verified.cmdProcessId
            cmdProcessStartedAtUtc = $verified.cmdProcessStartedAtUtc
            requestSha256 = $requestHash
            responseSha256 = $responseHash
            claimDirectory = $directory
            finalizedUtc = [DateTime]::UtcNow.ToString('o')
            capabilities = @('StatusRefresh')
            deniedCapabilities = @('SendMessage', 'Focus', 'Close', 'Resume', 'InputInjection')
        }
        try { Write-ESCodexExternalClaimAtomicCreateOnly $directory 'finalize-receipt.json' $receipt | Out-Null }
        catch [IO.IOException] {
            $existingReceipt = Read-ESCodexExternalClaimJson $receiptPath $ClaimsRoot
            if ((Get-ESCodexExternalClaimProperty $existingReceipt 'claimId') -ne $verified.claimId -or
                (Get-ESCodexExternalClaimProperty $existingReceipt 'recordId') -ne [string]$record.recordId) {
                throw 'A competing finalizer created an incompatible claim receipt.'
            }
            $receipt = $existingReceipt
        }
        return [pscustomobject]$receipt
    }
    finally {
        if ($acquired) { $mutex.ReleaseMutex() }
        $mutex.Dispose()
    }
}

function Invoke-ESCodexExternalClaimCancel([string]$ClaimsRoot) {
    $claimId = Assert-ESCodexExternalClaimGuid $ClaimId 'ClaimId'
    $directory = Get-ESCodexExternalClaimDirectory $ClaimsRoot $claimId
    $mutex = [Threading.Mutex]::new($false, 'ESFrameworkCodexExternalClaim_' + $claimId.Replace('-', ''))
    $acquired = $false
    try {
        try { $acquired = $mutex.WaitOne(5000) }
        catch [Threading.AbandonedMutexException] { $acquired = $true }
        if (-not $acquired) { throw 'Timed out waiting for the exact external claim mutex.' }

        $requestPath = Join-Path $directory 'request.json'
        $responsePath = Join-Path $directory 'response.json'
        $finalizePath = Join-Path $directory 'finalize-receipt.json'
        $cancelPath = Join-Path $directory 'cancel-receipt.json'
        $request = Read-ESCodexExternalClaimJson $requestPath $ClaimsRoot
        $identity = Assert-ESCodexExternalClaimRequest $request $claimId '' -AllowExpired
        $requestHash = Get-ESCodexExternalClaimHash $requestPath

        if (Test-Path -LiteralPath $finalizePath -PathType Leaf) {
            throw 'External claim is already finalized; it cannot be cancelled after ownership was committed.'
        }
        if (Test-Path -LiteralPath $cancelPath -PathType Leaf) {
            $existing = Read-ESCodexExternalClaimJson $cancelPath $ClaimsRoot
            if ((Get-ESCodexExternalClaimProperty $existing 'claimId') -ne $identity.claimId -or
                (Get-ESCodexExternalClaimProperty $existing 'requestSha256') -ne $requestHash) {
                throw 'Existing external claim cancellation receipt does not match this exact request.'
            }
            return [pscustomobject]$existing
        }

        $receipt = [ordered]@{
            schemaVersion = 2
            mode = 'CancelExternalClaim'
            claimState = 'Cancelled'
            claimId = $identity.claimId
            requestedSessionId = $identity.sessionId
            externalBindingId = $identity.externalBindingId
            requestSha256 = $requestHash
            cancelledUtc = [DateTime]::UtcNow.ToString('o')
            claimDirectory = $directory
            responseObserved = Test-Path -LiteralPath $responsePath -PathType Leaf
            capabilities = @()
            deniedCapabilities = @('StatusRefresh', 'SendMessage', 'Focus', 'Close', 'Resume', 'InputInjection')
        }
        Write-ESCodexExternalClaimAtomicCreateOnly $directory 'cancel-receipt.json' $receipt | Out-Null
        return [pscustomobject]$receipt
    }
    finally {
        if ($acquired) { $mutex.ReleaseMutex() }
        $mutex.Dispose()
    }
}

$stateRoot = Get-ESCodexExternalClaimStateRoot
$claimsRoot = Join-Path $stateRoot 'external-claims'
switch ($Action) {
    'Prepare' { Invoke-ESCodexExternalClaimPrepare $claimsRoot; break }
    'Respond' { Invoke-ESCodexExternalClaimRespond $claimsRoot; break }
    'Finalize' { Invoke-ESCodexExternalClaimFinalize $claimsRoot; break }
    'Cancel' { Invoke-ESCodexExternalClaimCancel $claimsRoot; break }
}
