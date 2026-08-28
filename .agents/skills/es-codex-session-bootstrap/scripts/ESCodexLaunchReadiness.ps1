$ErrorActionPreference = 'Stop'

function Get-ESCodexLaunchTextSha256([string]$Value) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes($Value)
        return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally { $sha.Dispose() }
}

function Get-ESCodexLaunchFileSha256([string]$Path) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
        $stream.Dispose()
    }
}

function Get-ESCodexLaunchReceiptPath([string]$ReceiptRoot, [string]$LaunchToken) {
    return Join-Path $ReceiptRoot ((Get-ESCodexLaunchTextSha256 $LaunchToken) + '.json')
}

function Find-ESCodexLaunchHistorySessionId([string]$HistoryPath, [string]$LaunchToken, [long]$StartedAtUnix) {
    if ([string]::IsNullOrWhiteSpace($HistoryPath) -or -not (Test-Path -LiteralPath $HistoryPath -PathType Leaf)) { return '' }
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            foreach ($line in (Get-Content -LiteralPath $HistoryPath -Tail 5000 -Encoding UTF8 -ErrorAction Stop)) {
                try {
                    $row = $line | ConvertFrom-Json
                    if ([string]$row.text -like ('*' + $LaunchToken + '*') -and [long]$row.ts -ge $StartedAtUnix) {
                        return [string]$row.session_id
                    }
                }
                catch {
                    # History is append-only and may contain unrelated/non-JSON lines.
                    Write-Verbose ("Ignoring malformed Codex history line while resolving launch session: " + $_.Exception.Message)
                }
            }
        }
        catch {
            # A concurrent Codex writer can briefly lock the global history file.
            # Session-id discovery is observational only; never block a New launch on it.
            Write-Verbose ("Codex history unavailable during launch observation (attempt $attempt): " + $_.Exception.Message)
            if ($attempt -lt 3) { Start-Sleep -Milliseconds 120 }
        }
    }
    return ''
}

function Get-ESCodexLaunchReadiness {
    param(
        [Parameter(Mandatory = $true)][string]$LaunchToken,
        [Parameter(Mandatory = $true)][string]$EnvelopePath,
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][string]$ReceiptRoot,
        [string]$HistoryPath = '',
        [long]$StartedAtUnix = 0,
        [string]$ExitMarkerPath = '',
        [string]$KnownSessionId = ''
    )

    $sessionId = $KnownSessionId
    $promptObserved = -not [string]::IsNullOrWhiteSpace($sessionId)
    $receiptPath = Get-ESCodexLaunchReceiptPath $ReceiptRoot $LaunchToken
    $contextAccepted = $false
    $failed = $false
    $failureReason = ''
    $exitCode = $null

    if (Test-Path -LiteralPath $receiptPath -PathType Leaf) {
        try {
            $receipt = Get-Content -LiteralPath $receiptPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $resolvedEnvelopePath = [IO.Path]::GetFullPath($EnvelopePath)
            $resolvedProjectRoot = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd('\')
            $receiptEnvelopePath = [IO.Path]::GetFullPath([string]$receipt.envelopePath)
            $receiptProjectRoot = [IO.Path]::GetFullPath([string]$receipt.projectRoot).TrimEnd('\')
            $envelopeExists = Test-Path -LiteralPath $resolvedEnvelopePath -PathType Leaf
            $envelopeHashValid = $envelopeExists -and [string]$receipt.envelopeSha256 -eq (Get-ESCodexLaunchFileSha256 $resolvedEnvelopePath)
            $contextAccepted = [int]$receipt.schemaVersion -eq 2 -and
                [string]$receipt.launchToken -eq $LaunchToken -and
                $receiptEnvelopePath.Equals($resolvedEnvelopePath, [StringComparison]::OrdinalIgnoreCase) -and
                $receiptProjectRoot.Equals($resolvedProjectRoot, [StringComparison]::OrdinalIgnoreCase) -and
                $envelopeHashValid
            if (-not $contextAccepted) {
                $failed = $true
                $failureReason = 'Acceptance receipt conflicts with the exact launch token, envelope, project root, or envelope hash.'
            }
            else {
                $promptObserved = $true
            }
        }
        catch {
            $receiptAgeSeconds = ([DateTime]::UtcNow - (Get-Item -LiteralPath $receiptPath).LastWriteTimeUtc).TotalSeconds
            if ($receiptAgeSeconds -ge 2) {
                $failed = $true
                $failureReason = 'Acceptance receipt is unreadable or invalid: ' + $_.Exception.Message
            }
        }
    }

    if (-not $contextAccepted -and -not $failed -and [string]::IsNullOrWhiteSpace($sessionId)) {
        $sessionId = Find-ESCodexLaunchHistorySessionId $HistoryPath $LaunchToken $StartedAtUnix
        $promptObserved = -not [string]::IsNullOrWhiteSpace($sessionId)
    }

    if (-not $contextAccepted -and -not $failed -and -not [string]::IsNullOrWhiteSpace($ExitMarkerPath) -and
        (Test-Path -LiteralPath $ExitMarkerPath -PathType Leaf)) {
        try {
            $marker = Get-Content -LiteralPath $ExitMarkerPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $exitCode = [int]$marker.exitCode
            if ([string]$marker.launchToken -ne $LaunchToken) {
                throw 'Exit marker launch token mismatch.'
            }
            $failureReason = 'Codex exited before launch-envelope acceptance (exit=' + $exitCode + ').'
        }
        catch {
            $failureReason = 'Codex exited before launch-envelope acceptance and its exit marker is invalid: ' + $_.Exception.Message
        }
        $failed = $true
    }

    $phase = if ($contextAccepted) { 'ContextAccepted' } elseif ($failed) { 'Failed' } elseif ($promptObserved) { 'PromptObserved' } else { 'TerminalStarted' }
    return [pscustomobject][ordered]@{
        launchPhase = $phase
        promptObserved = $promptObserved
        contextAccepted = $contextAccepted
        startupFailed = $failed
        failureReason = $failureReason
        sessionId = $sessionId
        acceptanceReceiptPath = if ($contextAccepted -or (Test-Path -LiteralPath $receiptPath -PathType Leaf)) { $receiptPath } else { '' }
        exitMarkerPath = $ExitMarkerPath
        exitCode = $exitCode
    }
}
