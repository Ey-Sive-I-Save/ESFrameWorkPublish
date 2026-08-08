[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Message,
    [ValidateSet('P0', 'P1', 'P2', 'P3', 'P4')]
    [string]$Priority = 'P2',
    [string]$Source = 'codex-chat',
    [ValidateRange(1, 3600)]
    [int]$TimeToLiveSeconds = 60,
    [ValidateRange(0, 10)]
    [double]$WaitForPickupSeconds = 2,
    [string]$ProjectRoot,
    [string]$PersistentDataPath
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new()

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
}
else {
    $ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot)
}

$messageValue = $Message.Trim()
$sourceValue = $Source.Trim()
if ($messageValue.Length -gt 4096) { throw 'Message exceeds 4096 characters.' }
if ($sourceValue.Length -eq 0) { $sourceValue = 'codex-chat' }
if ($sourceValue.Length -gt 128) { throw 'Source exceeds 128 characters.' }

if ([string]::IsNullOrWhiteSpace($PersistentDataPath)) {
    $settingsPath = Join-Path $ProjectRoot 'ProjectSettings\ProjectSettings.asset'
    if (-not (Test-Path -LiteralPath $settingsPath -PathType Leaf)) {
        throw "ProjectSettings.asset not found: $settingsPath"
    }

    $companyName = $null
    $productName = $null
    foreach ($line in [IO.File]::ReadAllLines($settingsPath, [Text.Encoding]::UTF8)) {
        if ($line -match '^  companyName:\s*(.+)$') { $companyName = $Matches[1].Trim() }
        elseif ($line -match '^  productName:\s*(.+)$') { $productName = $Matches[1].Trim() }
    }
    if ([string]::IsNullOrWhiteSpace($companyName) -or [string]::IsNullOrWhiteSpace($productName)) {
        throw 'Cannot resolve companyName/productName from ProjectSettings.asset.'
    }

    $PersistentDataPath = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'AppData\LocalLow'
    $PersistentDataPath = Join-Path $PersistentDataPath $companyName
    $PersistentDataPath = Join-Path $PersistentDataPath $productName
}

$inboxPath = Join-Path ([IO.Path]::GetFullPath($PersistentDataPath)) 'ESAITest\prompt-inbox'
[IO.Directory]::CreateDirectory($inboxPath) | Out-Null

$promptId = [Guid]::NewGuid().ToString('N')
$createdUtcTicks = [DateTime]::UtcNow.Ticks
$fileStem = ('{0:D19}-{1}' -f $createdUtcTicks, $promptId)
$temporaryPath = Join-Path $inboxPath ($fileStem + '.tmp')
$envelopePath = Join-Path $inboxPath ($fileStem + '.json')
$receiptPath = Join-Path $inboxPath ($fileStem + '.consumed')

$envelope = [ordered]@{
    protocolVersion = 1
    promptId = $promptId
    message = $messageValue
    priority = $Priority
    source = $sourceValue
    timeToLiveSeconds = $TimeToLiveSeconds
    createdUtcTicks = $createdUtcTicks
}
$json = $envelope | ConvertTo-Json -Compress
[IO.File]::WriteAllText($temporaryPath, $json, [Text.UTF8Encoding]::new($false))
[IO.File]::Move($temporaryPath, $envelopePath)

$status = 'queued'
if ($WaitForPickupSeconds -gt 0) {
    $deadline = [DateTime]::UtcNow.AddSeconds($WaitForPickupSeconds)
    do {
        if (Test-Path -LiteralPath $receiptPath -PathType Leaf) {
            $status = 'picked_up'
            break
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
}

[pscustomobject]@{
    status = $status
    promptId = $promptId
    priority = $Priority
    source = $sourceValue
    timeToLiveSeconds = $TimeToLiveSeconds
    inboxPath = $inboxPath
    envelopePath = $envelopePath
    receiptPath = $receiptPath
} | ConvertTo-Json -Compress
