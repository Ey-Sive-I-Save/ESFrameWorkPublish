[CmdletBinding()]
param(
    [ValidateRange(1, 30)]
    [int]$TimeoutSeconds = 10
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)

$codexCommand = Get-Command codex -ErrorAction SilentlyContinue
if ($null -eq $codexCommand) { throw 'Codex CLI was not found on PATH.' }
$codexCmdPath = Join-Path (Split-Path -Parent $codexCommand.Source) 'codex.cmd'
if (-not (Test-Path -LiteralPath $codexCmdPath -PathType Leaf)) { throw "Codex CMD launcher was not found: $codexCmdPath" }

$startInfo = [Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $env:ComSpec
$startInfo.Arguments = '/d /s /c ""' + $codexCmdPath + '" app-server --stdio"'
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.RedirectStandardInput = $true
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
$startInfo.StandardErrorEncoding = [Text.UTF8Encoding]::new($false)
$process = [Diagnostics.Process]::new()
$process.StartInfo = $startInfo
$started = $false
$responses = @()
try {
    $started = $process.Start()
    if (-not $started) { throw 'Codex app-server probe process did not start.' }
    $initialize = [ordered]@{
        method = 'initialize'
        id = 1
        params = [ordered]@{
            clientInfo = [ordered]@{ name = 'es_session_broker_probe'; title = 'ES Session Broker Probe'; version = '1.0.0' }
            capabilities = [ordered]@{ experimentalApi = $false; optOutNotificationMethods = @('item/agentMessage/delta') }
        }
    } | ConvertTo-Json -Compress -Depth 8
    $process.StandardInput.WriteLine($initialize)
    $process.StandardInput.Flush()

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $initialized = $false
    $loadedList = $null
    while ([DateTime]::UtcNow -lt $deadline -and -not $initialized) {
        $task = $process.StandardOutput.ReadLineAsync()
        $remaining = [Math]::Max(1, [int]($deadline - [DateTime]::UtcNow).TotalMilliseconds)
        if (-not $task.Wait($remaining)) { break }
        $line = [string]$task.Result
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $row = $line | ConvertFrom-Json
        $responses += $row
        if ([int]$row.id -eq 1 -and $null -ne $row.result) { $initialized = $true }
        elseif ([int]$row.id -eq 1 -and $null -ne $row.error) { throw "App-server initialize failed: $($row.error.message)" }
    }
    if (-not $initialized) { throw 'Timed out waiting for app-server initialize response.' }
    $process.StandardInput.WriteLine((@{ method = 'initialized'; params = @{} } | ConvertTo-Json -Compress))
    $process.StandardInput.WriteLine((@{ method = 'thread/loaded/list'; id = 2; params = @{} } | ConvertTo-Json -Compress))
    $process.StandardInput.Flush()
    while ([DateTime]::UtcNow -lt $deadline -and $null -eq $loadedList) {
        $task = $process.StandardOutput.ReadLineAsync()
        $remaining = [Math]::Max(1, [int]($deadline - [DateTime]::UtcNow).TotalMilliseconds)
        if (-not $task.Wait($remaining)) { break }
        $line = [string]$task.Result
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $row = $line | ConvertFrom-Json
        $responses += $row
        if ([int]$row.id -eq 2 -and $null -ne $row.result) { $loadedList = $row.result }
        elseif ([int]$row.id -eq 2 -and $null -ne $row.error) { throw "thread/loaded/list failed: $($row.error.message)" }
    }
    if ($null -eq $loadedList) { throw 'Timed out waiting for thread/loaded/list response.' }
    [pscustomobject][ordered]@{
        probeVersion = 1
        codexCli = $codexCmdPath
        appServerStdioSupported = $true
        initialized = $true
        threadLoadedListSupported = $true
        loadedThreadIds = @(if ($null -ne $loadedList.data) { $loadedList.data } elseif ($null -ne $loadedList.threadIds) { $loadedList.threadIds } else { @() })
        responseCount = $responses.Count
        directExistingTuiInjectionProven = $false
    }
}
finally {
    if ($started -and -not $process.HasExited) {
        try { $process.StandardInput.Close() } catch { }
        if (-not $process.WaitForExit(1000)) { try { $process.Kill() } catch { } }
    }
    $process.Dispose()
}
