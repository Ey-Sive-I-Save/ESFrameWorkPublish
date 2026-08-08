$ErrorActionPreference = 'Stop'

function Get-ESCodexMessageRoot([string]$StateRoot) {
    return Join-Path $StateRoot 'messages'
}

function Get-ESCodexMessagePaths([string]$StateRoot, [string]$MessageId) {
    $root = Get-ESCodexMessageRoot $StateRoot
    return [pscustomobject]@{
        requestPath = Join-Path (Join-Path $root 'requests') ($MessageId + '.json')
        statePath = Join-Path (Join-Path $root 'states') ($MessageId + '.json')
    }
}

function Write-ESCodexCreateOnlyJson([string]$Path, [object]$Value) {
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($Value | ConvertTo-Json -Depth 12))
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    try {
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    }
    finally { $stream.Dispose() }
}

function Write-ESCodexAtomicJson([string]$Path, [object]$Value) {
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
    $temporary = $Path + '.tmp-' + [Guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText($temporary, ($Value | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        $backup = $Path + '.bak-' + [Guid]::NewGuid().ToString('N')
        [IO.File]::Replace($temporary, $Path, $backup)
        if (Test-Path -LiteralPath $backup -PathType Leaf) { Remove-Item -LiteralPath $backup -Force }
    }
    else { [IO.File]::Move($temporary, $Path) }
}

function Get-ESCodexFileSha256([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return '' }
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose(); $stream.Dispose() }
}

function Get-ESCodexHookActivationPath([string]$StateRoot, [string]$RecordId) {
    return Join-Path (Join-Path $StateRoot 'hook-activations') ($RecordId + '.json')
}

function Write-ESCodexHookActivation([string]$StateRoot, [object]$Record, [string]$EventName, [string]$HookConfigPath, [string]$HookScriptPath) {
    $path = Get-ESCodexHookActivationPath $StateRoot ([string]$Record.recordId)
    $now = [DateTime]::UtcNow.ToString('o')
    $payload = [ordered]@{
        schemaVersion = 1
        recordId = [string]$Record.recordId
        sessionId = [string]$Record.sessionId
        responsibilityKey = [string]$Record.responsibilityKey
        lastEventName = $EventName
        observedUtc = $now
        hookConfigPath = $HookConfigPath
        hookConfigSha256 = Get-ESCodexFileSha256 $HookConfigPath
        hookScriptPath = $HookScriptPath
        hookScriptSha256 = Get-ESCodexFileSha256 $HookScriptPath
    }
    Write-ESCodexAtomicJson $path $payload
    return [pscustomobject]$payload
}

function Test-ESCodexHookActivation([string]$StateRoot, [string]$RecordId, [string]$SessionId, [string]$HookConfigPath, [string]$HookScriptPath, [int]$MaxAgeHours = 24) {
    $path = Get-ESCodexHookActivationPath $StateRoot $RecordId
    $result = [ordered]@{ path = $path; exists = $false; valid = $false; observedUtc = ''; reason = 'Missing' }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return [pscustomobject]$result }
    $result.exists = $true
    try { $receipt = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { $result.reason = 'InvalidJson'; return [pscustomobject]$result }
    $result.observedUtc = [string]$receipt.observedUtc
    if ([string]$receipt.recordId -ne $RecordId -or [string]$receipt.sessionId -ne $SessionId) { $result.reason = 'IdentityMismatch'; return [pscustomobject]$result }
    if ([string]$receipt.hookConfigSha256 -ne (Get-ESCodexFileSha256 $HookConfigPath)) { $result.reason = 'HookConfigDrift'; return [pscustomobject]$result }
    if ([string]$receipt.hookScriptSha256 -ne (Get-ESCodexFileSha256 $HookScriptPath)) { $result.reason = 'HookScriptDrift'; return [pscustomobject]$result }
    $observed = [DateTime]::MinValue
    if (-not [DateTime]::TryParse([string]$receipt.observedUtc, [ref]$observed) -or $observed.ToUniversalTime() -lt [DateTime]::UtcNow.AddHours(-$MaxAgeHours)) { $result.reason = 'Stale'; return [pscustomobject]$result }
    $result.valid = $true
    $result.reason = 'LoadedAndObserved'
    return [pscustomobject]$result
}

function Read-ESCodexMessage([string]$StateRoot, [string]$MessageId) {
    $paths = Get-ESCodexMessagePaths $StateRoot $MessageId
    if (-not (Test-Path -LiteralPath $paths.requestPath -PathType Leaf)) { return $null }
    $request = Get-Content -LiteralPath $paths.requestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $state = if (Test-Path -LiteralPath $paths.statePath -PathType Leaf) {
        Get-Content -LiteralPath $paths.statePath -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    else { $null }
    if ($null -eq $state) { throw "Message state is missing: $MessageId" }
    $expires = [DateTime]::MinValue
    $expired = [DateTime]::TryParse([string]$request.expiresUtc, [ref]$expires) -and $expires.ToUniversalTime() -le [DateTime]::UtcNow
    $effectiveStatus = if ($expired -and [string]$state.status -notin @('completed', 'failed', 'expired')) { 'expired' } else { [string]$state.status }
    return [pscustomobject][ordered]@{
        messageId = [string]$request.messageId
        idempotencyKey = [string]$request.idempotencyKey
        targetRecordId = [string]$request.targetRecordId
        targetSessionId = [string]$request.targetSessionId
        targetResponsibilityKey = [string]$request.targetResponsibilityKey
        requestKind = [string](Get-ESCodexPropertyValue $request 'requestKind' 'message')
        correlationId = [string](Get-ESCodexPropertyValue $request 'correlationId' [string]$request.messageId)
        expectsReply = [bool](Get-ESCodexPropertyValue $request 'expectsReply' $false)
        replyToRecordId = [string](Get-ESCodexPropertyValue $request 'replyToRecordId' '')
        replyToSessionId = [string](Get-ESCodexPropertyValue $request 'replyToSessionId' '')
        inReplyToMessageId = [string](Get-ESCodexPropertyValue $request 'inReplyToMessageId' '')
        priority = [string]$request.priority
        body = [string]$request.body
        createdUtc = [string]$request.createdUtc
        expiresUtc = [string]$request.expiresUtc
        deliveryMode = [string]$request.deliveryMode
        requestPath = [string]$paths.requestPath
        statePath = [string]$paths.statePath
        status = [string]$state.status
        effectiveStatus = $effectiveStatus
        expired = $expired
        stateRevision = [int]$state.revision
        statusUpdatedUtc = [string]$state.updatedUtc
        acceptedByRecordId = [string]$state.acceptedByRecordId
        note = [string]$state.note
    }
}

function Find-ESCodexMessages([string]$StateRoot, [string]$MessageId = '', [string]$IdempotencyKey = '', [string]$TargetRecordId = '', [string]$CorrelationId = '', [string]$InReplyToMessageId = '') {
    $requestRoot = Join-Path (Get-ESCodexMessageRoot $StateRoot) 'requests'
    if (-not (Test-Path -LiteralPath $requestRoot -PathType Container)) { return @() }
    $files = if ([string]::IsNullOrWhiteSpace($MessageId)) { @(Get-ChildItem -LiteralPath $requestRoot -File -Filter '*.json') } else { @(Get-Item -LiteralPath (Join-Path $requestRoot ($MessageId + '.json')) -ErrorAction SilentlyContinue) }
    return @($files | ForEach-Object {
            try { Read-ESCodexMessage $StateRoot $_.BaseName } catch { $null }
        } | Where-Object {
            $null -ne $_ -and
            ([string]::IsNullOrWhiteSpace($IdempotencyKey) -or [string]$_.idempotencyKey -eq $IdempotencyKey) -and
            ([string]::IsNullOrWhiteSpace($TargetRecordId) -or [string]$_.targetRecordId -eq $TargetRecordId) -and
            ([string]::IsNullOrWhiteSpace($CorrelationId) -or [string]$_.correlationId -eq $CorrelationId) -and
            ([string]::IsNullOrWhiteSpace($InReplyToMessageId) -or [string]$_.inReplyToMessageId -eq $InReplyToMessageId)
        } | Sort-Object createdUtc)
}

function Set-ESCodexMessageStatus([string]$StateRoot, [string]$MessageId, [string]$NewStatus, [int]$ExpectedStateRevision = -1, [string]$AcceptedByRecordId = '', [string]$Note = '') {
    $allowed = @{
        queued = @('accepted', 'failed', 'expired')
        accepted = @('turn_started', 'completed', 'failed', 'expired')
        turn_started = @('steered', 'completed', 'failed', 'expired')
        steered = @('completed', 'failed', 'expired')
        completed = @()
        failed = @()
        expired = @()
    }
    $mutex = [Threading.Mutex]::new($false, 'ESFrameworkCodexSessionMessageStateV1')
    $acquired = $false
    try {
        $acquired = $mutex.WaitOne(5000)
        if (-not $acquired) { throw 'Timed out waiting for the Codex message-state mutex.' }
        $message = Read-ESCodexMessage $StateRoot $MessageId
        if ($null -eq $message) { throw "Message was not found: $MessageId" }
        if ($ExpectedStateRevision -ge 0 -and [int]$message.stateRevision -ne $ExpectedStateRevision) {
            throw "Message state revision conflict. Expected $ExpectedStateRevision but found $([int]$message.stateRevision)."
        }
        $current = [string]$message.status
        if ($current -eq $NewStatus) { return $message }
        if ($NewStatus -notin @($allowed[$current])) { throw "Invalid message transition: $current -> $NewStatus" }
        $paths = Get-ESCodexMessagePaths $StateRoot $MessageId
        $state = [ordered]@{
            schemaVersion = 1
            messageId = $MessageId
            revision = [int]$message.stateRevision + 1
            status = $NewStatus
            updatedUtc = [DateTime]::UtcNow.ToString('o')
            acceptedByRecordId = if ([string]::IsNullOrWhiteSpace($AcceptedByRecordId)) { [string]$message.acceptedByRecordId } else { $AcceptedByRecordId }
            note = ($Note -replace '[\r\n]+', ' ').Trim()
        }
        Write-ESCodexAtomicJson $paths.statePath $state
        return Read-ESCodexMessage $StateRoot $MessageId
    }
    finally {
        if ($acquired) { $mutex.ReleaseMutex() }
        $mutex.Dispose()
    }
}
