[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InputPath,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [Parameter(Mandatory = $true)][string]$ProjectRoot
)

$ErrorActionPreference = 'Stop'
$utf8Strict = New-Object System.Text.UTF8Encoding($false, $true)

function Get-FullPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { throw 'Path is empty.' }
    return [IO.Path]::GetFullPath($Path)
}

function Assert-Within([string]$Path, [string]$Root, [string]$Name) {
    $full = Get-FullPath $Path
    $rootFull = (Get-FullPath $Root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    if (-not $full.StartsWith($rootFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Name must remain under the project root."
    }
    return $full
}

function Assert-NoReparseTraversal([string]$Path, [string]$Root, [string]$Name) {
    $full = Get-FullPath $Path
    $rootFull = (Get-FullPath $Root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    if (-not $full.StartsWith($rootFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Name must remain under the managed root."
    }
    $ancestor = $rootFull
    while (-not [string]::IsNullOrWhiteSpace($ancestor)) {
        if (Test-Path -LiteralPath $ancestor) {
            $ancestorItem = Get-Item -LiteralPath $ancestor -Force
            if ($ancestorItem.LinkType -or ($ancestorItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "$Name cannot traverse a reparse point at the managed root or its parent."
            }
        }
        $parent = Split-Path -LiteralPath $ancestor -Parent
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $ancestor) { break }
        $ancestor = $parent
    }
    $relative = $full.Substring($rootFull.Length).TrimStart([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar))
    $current = $rootFull
    foreach ($segment in $relative.Split(@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar), [StringSplitOptions]::RemoveEmptyEntries)) {
        $current = Join-Path $current $segment
        if (-not (Test-Path -LiteralPath $current)) { break }
        $item = Get-Item -LiteralPath $current -Force
        if ($item.LinkType -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "$Name cannot traverse a reparse point."
        }
    }
    return $full
}

function Write-Utf8([string]$Path, [string]$Text) {
    $bytes = $utf8Strict.GetBytes($Text)
    $stream = New-Object IO.FileStream($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($bytes, 0, $bytes.Length) }
    finally { $stream.Dispose() }
}

function Redact-Text([string]$Text) {
    $result = if ($null -eq $Text) { '' } else { [string]$Text }
    foreach ($name in @('OPENAI_API_KEY', 'CODEX_API_KEY', 'AZURE_OPENAI_API_KEY', 'DEEPSEEK_API_KEY')) {
        $secret = [Environment]::GetEnvironmentVariable($name)
        if (-not [string]::IsNullOrEmpty($secret)) { $result = $result.Replace($secret, '[REDACTED]') }
    }
    return $result
}

function Get-OptionalProperty([object]$Object, [string]$Name) {
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Assert-NoReparseAncestors([string]$Path, [string]$Name) {
    $current = Get-FullPath $Path
    while (-not [string]::IsNullOrWhiteSpace($current)) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if ($item.LinkType -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "$Name cannot traverse a reparse point in its launcher path."
            }
        }
        $parent = Split-Path -LiteralPath $current -Parent
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $current) { break }
        $current = $parent
    }
}

function Get-Sha256([string]$Path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $stream = [IO.File]::OpenRead($Path)
        try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '').ToLowerInvariant() }
        finally { $stream.Dispose() }
    }
    finally { $sha.Dispose() }
}

function Send-Json([Diagnostics.Process]$Process, [object]$Message) {
    $line = $Message | ConvertTo-Json -Depth 20 -Compress
    $Process.StandardInput.WriteLine($line)
    $Process.StandardInput.Flush()
}

function Read-JsonLine([Diagnostics.Process]$Process, [int]$TimeoutMs) {
    $read = $Process.StandardOutput.ReadLineAsync()
    if (-not $read.Wait($TimeoutMs)) { throw 'Codex App Server response timed out.' }
    $line = $read.Result
    if ($null -eq $line) { throw 'Codex App Server closed stdout before a response.' }
    if ([string]::IsNullOrWhiteSpace($line)) { return $null }
    try { return ($line | ConvertFrom-Json) }
    catch { throw 'Codex App Server emitted invalid JSONL.' }
}

function Get-CodexCmdPath {
    $configured = [Environment]::GetEnvironmentVariable('ES_CODEX_CLI_PATH')
    $candidate = $configured
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        $command = Get-Command codex.cmd -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $command) { $candidate = [string]$command.Source }
    }
    if ([string]::IsNullOrWhiteSpace($candidate) -or -not [IO.Path]::IsPathRooted($candidate)) {
        throw 'Codex CLI requires an absolute ES_CODEX_CLI_PATH or a discovered codex.cmd.'
    }
    if ($candidate -match '["&|<>^%!\r\n]') { throw 'Codex CLI path contains shell metacharacters.' }
    $candidate = Get-FullPath $candidate
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw 'Codex CLI launcher does not exist.' }
    if (-not [string]::Equals([IO.Path]::GetFileName($candidate), 'codex.cmd', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Only the fixed codex.cmd launcher is accepted.'
    }
    Assert-NoReparseAncestors $candidate 'Codex CLI launcher'
    $item = Get-Item -LiteralPath $candidate -Force
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Codex CLI launcher cannot be a reparse point.' }
    return $candidate
}

function Add-Event([Collections.Generic.List[object]]$Events, [object]$Message) {
    if ($Events.Count -ge 200) { return }
    $json = Redact-Text ($Message | ConvertTo-Json -Depth 20 -Compress)
    $eventJson = $json
    if ($json.Length -gt 8192) {
        $eventJson = ([pscustomobject]@{ truncated = $true; preview = $json.Substring(0, 8192) } | ConvertTo-Json -Compress)
    }
    if ($script:codexEventBytes -ge 800000) { return }
    if (($script:codexEventBytes + $eventJson.Length) -gt 800000) {
        $remaining = 800000 - $script:codexEventBytes
        if ($remaining -gt 128) {
            $previewLength = [Math]::Min(1024, $remaining - 96)
            $eventJson = ([pscustomobject]@{ truncated = $true; preview = $json.Substring(0, [Math]::Min($previewLength, $json.Length)) } | ConvertTo-Json -Compress)
        }
        else { return }
    }
    if (($script:codexEventBytes + $eventJson.Length) -gt 800000) { return }
    [void]$Events.Add(($eventJson | ConvertFrom-Json))
    $script:codexEventBytes += $eventJson.Length
}

$root = Get-FullPath $ProjectRoot
$managedRunRoot = Join-Path $root 'ES\Automation\Runs\CodexAppServer'
$commandPath = Join-Path $root 'Assets\Plugins\ES\AICommands\CodexAppServerHarness受管开发_AI命令.md'
$inputFull = Assert-Within $InputPath $managedRunRoot 'InputPath'
$outputFull = Assert-Within $OutputDirectory $managedRunRoot 'OutputDirectory'
Assert-NoReparseTraversal $inputFull $managedRunRoot 'InputPath' | Out-Null
Assert-NoReparseTraversal $outputFull $managedRunRoot 'OutputDirectory' | Out-Null
Assert-NoReparseTraversal $commandPath $root 'CommandPath' | Out-Null
if (-not (Test-Path -LiteralPath $commandPath -PathType Leaf)) { throw 'CommandPath does not exist.' }
if (-not (Test-Path -LiteralPath $inputFull -PathType Leaf)) { throw 'InputPath does not exist.' }
New-Item -ItemType Directory -Force -Path $outputFull | Out-Null
Assert-NoReparseTraversal $outputFull $managedRunRoot 'OutputDirectory' | Out-Null
$resultPath = Join-Path $outputFull 'codex-app-server-result.json'
Assert-NoReparseTraversal $resultPath $managedRunRoot 'ResultPath' | Out-Null
if (Test-Path -LiteralPath $resultPath) { throw 'ResultPath already exists; refusing to overwrite a prior result.' }
$started = [DateTimeOffset]::UtcNow
$script:codexEventBytes = 0
$events = New-Object 'Collections.Generic.List[object]'
$errors = New-Object 'Collections.Generic.List[string]'
$finalText = New-Object Text.StringBuilder
$process = $null
$stderrTask = $null
$result = [ordered]@{
    schemaVersion = 1; taskId = 'es.codex.app-server'; taskVersion = 1; runId = ''; inputManifestHash = ''; providerDeclaration = 'es-codex'
    workerId = 'es.codex.app-server'; workerVersion = '1.0.0'; operation = ''; status = 'Failed'; exitCode = 1
    brainPlanHash = ''; commandId = 'codex.appserver.execute'; commandHash = ''; taskContractHash = ''; invocationId = ''; idempotencyKey = ''
    threadId = ''; sessionId = ''; turnId = ''; codexProcessId = 0; finalMessage = ''; events = @(); errors = @()
    runtimeStatus = 'runtime-not-run'; approvalRequests = 0; mutationApplied = $false; networkCalled = $false
    completionDecision = $null
    startedAtUtc = $started.ToString('O'); finishedAtUtc = ''; claimsNotProven = @('ES business completion and source acceptance remain outside Codex Harness.')
}

try {
    $request = $utf8Strict.GetString([IO.File]::ReadAllBytes($inputFull)) | ConvertFrom-Json
    foreach ($required in @('projectRoot','providerDeclaration','workerId','workerVersion','taskId','runId','dryRun','operation','prompt','brainPlanHash','commandId','commandHash','taskContractHash','invocationId')) {
        if ($null -eq $request.$required) { throw "Missing request field: $required" }
    }
    if (-not [string]::Equals([string]$request.projectRoot, $root, [StringComparison]::OrdinalIgnoreCase)) { throw 'Request projectRoot does not match worker project root.' }
    if ($request.providerDeclaration -ne 'es-codex' -or $request.workerId -ne 'es.codex.app-server' -or $request.workerVersion -ne '1.0.0' -or $request.taskId -ne 'es.codex.app-server') { throw 'Request identity does not match the fixed Codex App Server contract.' }
    if ([string]$request.runId -notmatch '^[0-9a-f]{32}$') { throw 'RunId is not a lowercase N-format GUID.' }
    if ([string]$request.invocationId -ne [string]$request.runId) { throw 'InvocationId must bind exactly to RunId.' }
    if ([string]$request.brainPlanHash -notmatch '^[a-f0-9]{64}$') { throw 'brainPlanHash is not a lowercase SHA-256.' }
    if ([string]$request.commandId -ne 'codex.appserver.execute') { throw 'Request commandId does not match the fixed Codex AICommand.' }
    if ([string]$request.commandHash -notmatch '^[a-f0-9]{64}$') { throw 'commandHash is not a lowercase SHA-256.' }
    if (-not [string]::Equals((Get-Sha256 $commandPath), [string]$request.commandHash, [StringComparison]::OrdinalIgnoreCase)) { throw 'Request commandHash does not match the fixed ES AICommand source.' }
    if ([string]$request.taskContractHash -notmatch '^[a-f0-9]{64}$') { throw 'taskContractHash is not a lowercase SHA-256.' }
    $requestThreadId = Get-OptionalProperty $request 'threadId'
    $requestModel = Get-OptionalProperty $request 'model'
    $requestIdempotencyKey = Get-OptionalProperty $request 'idempotencyKey'
    if ($requestIdempotencyKey -and [string]$requestIdempotencyKey -notmatch '^[A-Za-z0-9._:-]{1,160}$') { throw 'idempotencyKey format is invalid.' }
    if ([string]$request.prompt.Length -gt 12000) { throw 'Prompt exceeds the contract limit.' }
    $operation = [string]$request.operation
    if ($operation -notin @('dry-run','check-local','start-thread','turn')) { throw 'Unsupported Codex App Server operation.' }
    if ($operation -eq 'turn' -and [string]::IsNullOrWhiteSpace([string]$requestThreadId)) { throw 'turn requires an exact threadId.' }
    if ($requestThreadId -and [string]$requestThreadId -notmatch '^[A-Za-z0-9._:-]{1,160}$') { throw 'threadId format is invalid.' }
    if ($requestModel -and [string]$requestModel -notmatch '^[A-Za-z0-9._:-]{1,96}$') { throw 'model format is invalid.' }
    $result.runId = [string]$request.runId
    $result.inputManifestHash = Get-Sha256 $inputFull
    $result.operation = $operation
    $result.brainPlanHash = [string]$request.brainPlanHash
    $result.commandId = [string]$request.commandId
    $result.commandHash = [string]$request.commandHash
    $result.taskContractHash = [string]$request.taskContractHash
    $result.invocationId = [string]$request.invocationId
    $result.idempotencyKey = [string]$requestIdempotencyKey

    if ($request.dryRun -or $operation -eq 'dry-run') {
        $result.status = 'DryRun'; $result.runtimeStatus = 'runtime-not-run'; $result.exitCode = 0
    }
    else {
        $codexCmd = Get-CodexCmdPath
        $cmdExe = Join-Path ([Environment]::GetFolderPath('System')) 'cmd.exe'
        if (-not (Test-Path -LiteralPath $cmdExe -PathType Leaf)) { throw 'Windows cmd.exe is unavailable.' }
        $psi = New-Object Diagnostics.ProcessStartInfo
        $psi.FileName = $cmdExe
        $psi.Arguments = '/d /s /c ""' + $codexCmd + '" app-server --stdio"'
        $psi.WorkingDirectory = $root
        $psi.UseShellExecute = $false
        $psi.CreateNoWindow = $true
        $psi.RedirectStandardInput = $true
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.StandardOutputEncoding = $utf8Strict
        $psi.StandardErrorEncoding = $utf8Strict
        $process = New-Object Diagnostics.Process
        $process.StartInfo = $psi
        if (-not $process.Start()) { throw 'Codex App Server did not start.' }
        $result.codexProcessId = $process.Id
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $result.runtimeStatus = 'runtime-executed'
        Send-Json $process ([ordered]@{ id = 1; method = 'initialize'; params = [ordered]@{ clientInfo = [ordered]@{ name = 'esframework'; title = 'ESFramework Harness'; version = '1.0.0' } } })
        Send-Json $process ([ordered]@{ method = 'initialized'; params = [ordered]@{} })
        $nextId = 2
        if ($operation -eq 'check-local') {
            $deadline = [Diagnostics.Stopwatch]::StartNew()
            while ($true) {
                $remaining = 30000 - [int]$deadline.ElapsedMilliseconds
                if ($remaining -le 0) { throw 'Codex App Server initialize handshake timed out.' }
                $message = Read-JsonLine $process $remaining
                if ($null -eq $message) { continue }
                Add-Event $events $message
                if ($message.error) { throw ([string]$message.error.message) }
                if ($message.id -eq 1) {
                    Send-Json $process ([ordered]@{ id = 2; method = 'thread/loaded/list'; params = [ordered]@{} })
                    continue
                }
                if ($message.id -eq 2) { $result.status = 'Passed'; $result.exitCode = 0; break }
                if ([string]$message.method -match 'requestApproval|requestUserInput|elicitation|permissions') { $result.approvalRequests++; throw 'Codex requested a permission or approval; ES denied by default and stopped the run.' }
            }
        }
        else {
            $threadMethod = if ($operation -eq 'turn') { 'thread/resume' } else { 'thread/start' }
            $threadParams = [ordered]@{}
            if ($operation -eq 'turn') { $threadParams.threadId = [string]$requestThreadId }
            else { $threadParams.cwd = $root; $threadParams.approvalPolicy = 'never'; $threadParams.sandbox = 'read-only'; $threadParams.serviceName = 'esframework-harness'; if ($requestModel) { $threadParams.model = [string]$requestModel } }
            # A thread start/resume is the first provider-facing request. Record
            # that an external network/provider call was attempted; this is not
            # a claim that the provider succeeded or that ES accepted the output.
            $result.networkCalled = $true
            Send-Json $process ([ordered]@{ id = $nextId; method = $threadMethod; params = $threadParams })
            $nextId++
            $turnSent = $false
            $deadline = [Diagnostics.Stopwatch]::StartNew()
            while ($true) {
                $remaining = 120000 - [int]$deadline.ElapsedMilliseconds
                if ($remaining -le 0) { throw 'Codex App Server thread/turn timed out.' }
                $message = Read-JsonLine $process $remaining
                if ($null -eq $message) { continue }
                Add-Event $events $message
                if ($message.error) { throw ([string]$message.error.message) }
                if ($message.id -eq 2 -and $message.result.thread) {
                    $result.threadId = [string]$message.result.thread.id
                    $result.sessionId = [string]$message.result.thread.sessionId
                    if ([string]::IsNullOrWhiteSpace($result.threadId)) { throw 'Codex App Server returned an empty thread identity.' }
                    if ($operation -eq 'turn' -and -not [string]::Equals($result.threadId, [string]$requestThreadId, [StringComparison]::Ordinal)) { throw 'Codex App Server resume returned a different thread identity.' }
                    if ([string]::IsNullOrWhiteSpace([string]$request.prompt)) { $result.status = 'Passed'; $result.exitCode = 0; break }
                    $turnParams = [ordered]@{ threadId = $result.threadId; input = @([ordered]@{ type = 'text'; text = [string]$request.prompt }); cwd = $root; approvalPolicy = 'never'; sandboxPolicy = [ordered]@{ type = 'readOnly'; access = [ordered]@{ type = 'restricted'; includePlatformDefaults = $false; readableRoots = @($root) } } }
                    if ($requestModel) { $turnParams.model = [string]$requestModel }
                    Send-Json $process ([ordered]@{ id = 3; method = 'turn/start'; params = $turnParams })
                    $turnSent = $true
                    continue
                }
                if ($message.id -eq 3 -and $message.result.turn) { $result.turnId = [string]$message.result.turn.id; continue }
                if ([string]$message.method -eq 'item/agentMessage/delta') {
                    $delta = [string]$message.params.delta
                    if ($delta) { [void]$finalText.Append($delta) }
                    continue
                }
                if ([string]$message.method -eq 'turn/completed') {
                    if ($message.params.turn) {
                        $result.turnId = [string]$message.params.turn.id
                        $turnStatus = [string]$message.params.turn.status
                        if ($message.params.turn.error -and $message.params.turn.error.message) {
                            [void]$errors.Add((Redact-Text ([string]$message.params.turn.error.message)))
                        }
                        if ($turnStatus -eq 'completed') {
                            $result.status = 'Passed'; $result.exitCode = 0
                        }
                        elseif ($turnStatus -eq 'interrupted') {
                            $result.status = 'Cancelled'; $result.exitCode = 20
                        }
                        else {
                            $result.status = 'Failed'; $result.exitCode = 1
                        }
                    }
                    else { $result.status = 'Failed'; $result.exitCode = 1 }
                    break
                }
                if ([string]$message.method -match 'requestApproval|requestUserInput|elicitation|permissions') { $result.approvalRequests++; throw 'Codex requested a permission or approval; ES denied by default and stopped the run.' }
            }
        }
        try { $process.StandardInput.Close() } catch { }
        if (-not $process.WaitForExit(5000)) { try { $process.Kill() } catch { } }
    }
}
catch {
    $errorText = Redact-Text ([string]$_.Exception.Message)
    [void]$errors.Add($errorText.Substring(0, [Math]::Min(2000, $errorText.Length)))
    if ($result.approvalRequests -gt 0) { $result.status = 'Blocked'; $result.exitCode = 2 } else { $result.status = 'Failed'; $result.exitCode = 1 }
    if ($process -and -not $process.HasExited) { try { $process.Kill() } catch { } }
}
finally {
    $final = Redact-Text ([string]$finalText.ToString())
    $result.finalMessage = $final.Substring(0, [Math]::Min(32000, $final.Length))
    if ($stderrTask) {
        try {
            if ($stderrTask.Wait(2000)) {
                $stderrText = Redact-Text ([string]$stderrTask.Result)
                if (-not [string]::IsNullOrWhiteSpace($stderrText)) {
                    [void]$errors.Add(('Codex stderr: ' + $stderrText).Substring(0, [Math]::Min(8192, ('Codex stderr: ' + $stderrText).Length)))
                }
            }
        }
        catch { }
    }
    $result.events = @($events)
    $result.errors = @($errors)
    $result.finishedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    Write-Utf8 $resultPath (($result | ConvertTo-Json -Depth 30) + "`n")
    if ($process) { $process.Dispose() }
}
exit ([int]$result.exitCode)
