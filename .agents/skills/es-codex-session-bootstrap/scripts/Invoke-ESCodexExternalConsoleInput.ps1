[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ClaimId,

    [Parameter(Mandatory = $true)]
    [int]$ExpectedCmdProcessId,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedCmdProcessStartedAtUtc,

    [string]$StateRoot = '',

    [switch]$ConsoleInputHelper
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$fixedProjectRoot = 'F:\aaProject\ESFrameWorkPublish'
$skillScriptsRoot = Join-Path $fixedProjectRoot '.agents\skills\es-codex-session-bootstrap\scripts'
$claimResponderPath = Join-Path $skillScriptsRoot 'Claim-ESCodexExternalTerminal.ps1'

function Assert-ExactGuid([string]$Value, [string]$Label) {
    $parsed = [Guid]::Empty
    if ([string]::IsNullOrWhiteSpace($Value) -or -not [Guid]::TryParse($Value.Trim(), [ref]$parsed)) {
        throw "$Label must be an exact UUID."
    }
    return $parsed.ToString()
}

function Get-ManagedStateRoot {
    if (-not [string]::IsNullOrWhiteSpace($StateRoot)) {
        return [IO.Path]::GetFullPath($StateRoot).TrimEnd('\')
    }
    $localApplicationData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    return (Join-Path $localApplicationData 'ESFramework\CodexSessions')
}

function Assert-ManagedPath([string]$Path, [string]$Root, [string]$Label) {
    $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    if (-not ($fullPath.Equals($fullRoot, [StringComparison]::OrdinalIgnoreCase) -or
            $fullPath.StartsWith($fullRoot + '\', [StringComparison]::OrdinalIgnoreCase))) {
        throw "$Label escaped its managed root."
    }
    $current = $fullPath
    while ($current.Length -ge $fullRoot.Length) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "$Label contains a reparse point."
            }
        }
        if ($current.Equals($fullRoot, [StringComparison]::OrdinalIgnoreCase)) { break }
        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent.Equals($current, [StringComparison]::OrdinalIgnoreCase)) { break }
        $current = $parent
    }
    return $fullPath
}

function Assert-TargetCmdIdentity {
    $expectedStart = [DateTime]::MinValue
    if ($ExpectedCmdProcessId -le 0 -or
        -not [DateTime]::TryParse($ExpectedCmdProcessStartedAtUtc, [ref]$expectedStart)) {
        throw 'Selected CMD identity is incomplete or invalid.'
    }
    try {
        $cmd = Get-Process -Id $ExpectedCmdProcessId -ErrorAction Stop
        if ($cmd.HasExited -or -not $cmd.ProcessName.Equals('cmd', [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Selected process is no longer an active cmd.exe shell.'
        }
        if ($cmd.StartTime.ToUniversalTime().Ticks -ne $expectedStart.ToUniversalTime().Ticks) {
            throw 'Selected CMD PID was reused or its start identity changed.'
        }
        return $cmd
    }
    catch {
        throw "Selected CMD cannot be verified: $($_.Exception.Message)"
    }
}

function Assert-TargetShellInputIdle([int]$CmdProcessId) {
    $all = @(Get-CimInstance Win32_Process -OperationTimeoutSec 3 -ErrorAction Stop)
    $childrenByParent = @{}
    foreach ($process in $all) {
        $parentId = [int]$process.ParentProcessId
        if (-not $childrenByParent.ContainsKey($parentId)) {
            $childrenByParent[$parentId] = New-Object System.Collections.Generic.List[object]
        }
        $childrenByParent[$parentId].Add($process)
    }
    $queue = New-Object System.Collections.Generic.Queue[int]
    $queue.Enqueue($CmdProcessId)
    $visited = New-Object 'System.Collections.Generic.HashSet[int]'
    [void]$visited.Add($CmdProcessId)
    $blocking = New-Object System.Collections.Generic.List[string]
    while ($queue.Count -gt 0) {
        $parentId = $queue.Dequeue()
        # Avoid a PowerShell 5 dynamic-binder failure when the two branches
        # produce different generic/array types. Keep one explicit list type.
        $children = New-Object System.Collections.Generic.List[object]
        if ($childrenByParent.ContainsKey($parentId)) {
            foreach ($child in $childrenByParent[$parentId]) {
                [void]$children.Add($child)
            }
        }
        foreach ($child in $children) {
            $childId = [int]$child.ProcessId
            if (-not $visited.Add($childId)) { continue }
            $name = ([string]$child.Name).ToLowerInvariant()
            if ($name -ne 'conhost.exe') {
                $blocking.Add($child.Name + ' (PID ' + $childId + ')')
            }
            $queue.Enqueue($childId)
        }
    }
    if ($blocking.Count -gt 0) {
        $message = (
            'Automatic claim input was refused because the selected CMD is not at a shell prompt. ' +
            'Active descendants: ' + ($blocking -join ', ') +
            '. Return it to an idle CMD prompt and retry; no input was written.'
        )
        throw $message
    }
}

function Get-VerifiedClaimCommand([string]$NormalizedClaimId) {
    $stateRootPath = Get-ManagedStateRoot
    $claimsRoot = Assert-ManagedPath (Join-Path $stateRootPath 'external-claims') $stateRootPath 'External claim root'
    $claimDirectory = Assert-ManagedPath (Join-Path $claimsRoot $NormalizedClaimId) $claimsRoot 'External claim directory'
    $requestPath = Assert-ManagedPath (Join-Path $claimDirectory 'request.json') $claimDirectory 'External claim request'
    if (-not (Test-Path -LiteralPath $requestPath -PathType Leaf)) {
        throw 'The selected one-time claim request no longer exists.'
    }
    if (Test-Path -LiteralPath (Join-Path $claimDirectory 'response.json') -PathType Leaf) {
        throw 'The selected claim already has a response. Use claim verification instead of submitting input again.'
    }
    if (Test-Path -LiteralPath (Join-Path $claimDirectory 'cancel-receipt.json') -PathType Leaf) {
        throw 'The selected claim was cancelled and cannot receive automatic input.'
    }
    try { $request = Get-Content -LiteralPath $requestPath -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { throw "The selected claim request is not valid UTF-8 JSON: $($_.Exception.Message)" }
    if ([int]$request.schemaVersion -ne 2 -or -not ([string]$request.claimId).Equals($NormalizedClaimId, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The selected request is not the expected external-CMD claim.'
    }
    if (-not ([string]$request.projectRoot).Equals($fixedProjectRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The selected claim request is not bound to this ESFramework project.'
    }
    $requestedCmdStart = [DateTime]::MinValue
    $selectedCmdStart = [DateTime]::MinValue
    if ([int]$request.expectedCmdProcessId -ne $ExpectedCmdProcessId -or
        -not [DateTime]::TryParse([string]$request.expectedCmdProcessStartedAtUtc, [ref]$requestedCmdStart) -or
        -not [DateTime]::TryParse($ExpectedCmdProcessStartedAtUtc, [ref]$selectedCmdStart) -or
        $requestedCmdStart.ToUniversalTime().Ticks -ne $selectedCmdStart.ToUniversalTime().Ticks) {
        throw 'The selected claim no longer matches the chosen CMD identity.'
    }
    $expiresAt = [DateTime]::MinValue
    if (-not [DateTime]::TryParse([string]$request.expiresAtUtc, [ref]$expiresAt) -or
        $expiresAt.ToUniversalTime() -le [DateTime]::UtcNow) {
        throw 'The selected one-time claim has expired.'
    }
    $token = ([string]$request.claimToken).Trim().ToLowerInvariant()
    if ($token -notmatch '^[a-f0-9]{64}$') {
        throw 'The selected claim token is invalid.'
    }
    if (-not (Test-Path -LiteralPath $claimResponderPath -PathType Leaf)) {
        throw 'The fixed external claim responder script is unavailable.'
    }
    $stateArgument = if ([string]::IsNullOrWhiteSpace($StateRoot)) { '' } else {
        ' -StateRoot "' + $stateRootPath.Replace('"', '""') + '"'
    }
    $command = (
        'powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' +
        $claimResponderPath.Replace('"', '""') + '" -ClaimId ' + $NormalizedClaimId + ' -ClaimToken ' + $token +
        $stateArgument
    )
    return [pscustomobject]@{
        command = $command
        externalBindingId = [string]$request.externalBindingId
        responsePath = Join-Path $claimDirectory 'response.json'
    }
}

function ConvertTo-ExternalConsoleInputArgument([string]$Value) {
    if ($null -eq $Value) { return '""' }
    $builder = New-Object System.Text.StringBuilder
    [void]$builder.Append('"')
    $backslashCount = 0
    foreach ($character in $Value.ToCharArray()) {
        if ($character -eq [char]'\') {
            $backslashCount++
            continue
        }
        if ($character -eq [char]'"') {
            if ($backslashCount -gt 0) {
                [void]$builder.Append([string]::new([char]'\', $backslashCount * 2))
                $backslashCount = 0
            }
            [void]$builder.Append('\"')
            continue
        }
        if ($backslashCount -gt 0) {
            [void]$builder.Append([string]::new([char]'\', $backslashCount))
            $backslashCount = 0
        }
        [void]$builder.Append($character)
    }
    if ($backslashCount -gt 0) {
        [void]$builder.Append([string]::new([char]'\', $backslashCount * 2))
    }
    [void]$builder.Append('"')
    return $builder.ToString()
}

function Invoke-ExternalConsoleInputHelper {
    $arguments = @(
        '-NoLogo', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
        '-File', $PSCommandPath,
        '-ConsoleInputHelper',
        '-ClaimId', $ClaimId,
        '-ExpectedCmdProcessId', [string]$ExpectedCmdProcessId,
        '-ExpectedCmdProcessStartedAtUtc', $ExpectedCmdProcessStartedAtUtc
    )
    if (-not [string]::IsNullOrWhiteSpace($StateRoot)) {
        $arguments += @('-StateRoot', $StateRoot)
    }
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = (Get-Command powershell.exe -ErrorAction Stop).Source
    $startInfo.Arguments = (@($arguments | ForEach-Object {
                ConvertTo-ExternalConsoleInputArgument ([string]$_)
            }) -join ' ')
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.StandardOutputEncoding = [System.Text.UTF8Encoding]::new($false)
    $startInfo.StandardErrorEncoding = [System.Text.UTF8Encoding]::new($false)
    $helper = [System.Diagnostics.Process]::Start($startInfo)
    if ($null -eq $helper) { throw 'The external console-input helper could not start.' }
    $exitCode = -1
    try {
        $standardOutput = $helper.StandardOutput.ReadToEnd()
        $standardError = $helper.StandardError.ReadToEnd()
        $helper.WaitForExit()
        $exitCode = $helper.ExitCode
    }
    finally {
        $helper.Dispose()
    }
    $receiptText = if ($null -eq $standardOutput) { '' } else { [string]$standardOutput }
    $receiptText = $receiptText.Trim()
    if ($exitCode -ne 0) {
        $detail = if ([string]::IsNullOrWhiteSpace($standardError)) { $receiptText } else { $standardError.Trim() }
        throw "External console-input helper rejected the request: $detail"
    }
    if ([string]::IsNullOrWhiteSpace($receiptText)) {
        throw 'External console-input helper returned no submission receipt.'
    }
    try { return $receiptText | ConvertFrom-Json }
    catch { throw "External console-input helper returned invalid JSON: $($_.Exception.Message)" }
}

if (-not $ConsoleInputHelper) {
    Invoke-ExternalConsoleInputHelper
    return
}

$nativeSource = @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

public static class ESCodexExternalConsoleInputNative
{
    private const ushort KeyEvent = 0x0001;
    private const ushort VirtualKeyReturn = 0x000D;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    private struct CharUnion
    {
        [FieldOffset(0)] public char UnicodeChar;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct KeyEventRecord
    {
        [MarshalAs(UnmanagedType.Bool)] public bool KeyDown;
        public ushort RepeatCount;
        public ushort VirtualKeyCode;
        public ushort VirtualScanCode;
        public CharUnion Character;
        public uint ControlKeyState;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct InputRecord
    {
        public ushort EventType;
        public KeyEventRecord KeyEvent;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFileW(string fileName, uint desiredAccess, uint shareMode,
        IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteConsoleInputW(IntPtr consoleInput, InputRecord[] buffer,
        uint length, out uint written);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetConsoleProcessList(uint[] processList, uint processCount);

    public static void WriteVerifiedLine(int targetProcessId, string command)
    {
        if (targetProcessId <= 0) throw new ArgumentOutOfRangeException("targetProcessId");
        if (String.IsNullOrWhiteSpace(command)) throw new ArgumentException("Command is empty.", "command");
        if (command.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
            throw new ArgumentException("Command contains a control character.", "command");

        FreeConsole();
        if (!AttachConsole((uint)targetProcessId))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "AttachConsole failed for the selected CMD.");
        try
        {
            var processIds = new uint[64];
            uint count = GetConsoleProcessList(processIds, (uint)processIds.Length);
            bool targetFound = false;
            int currentProcessId = GetCurrentProcessId();
            for (int index = 0; index < count && index < processIds.Length; index++)
            {
                if (processIds[index] == (uint)targetProcessId)
                {
                    targetFound = true;
                    continue;
                }
                if (processIds[index] != (uint)currentProcessId)
                    throw new InvalidOperationException("The selected CMD shares its console input buffer with another process; automatic input was refused.");
            }
            if (!targetFound)
                throw new InvalidOperationException("The attached console no longer belongs to the selected CMD.");

            // PowerShell can inherit a redirected standard input pipe. Open the
            // attached console's real input buffer instead of trusting STDIN.
            IntPtr input = CreateFileW("CONIN$", GenericRead | GenericWrite, FileShareRead | FileShareWrite,
                IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (input == IntPtr.Zero || input == new IntPtr(-1))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "The selected CMD console has no writable input buffer.");
            try
            {
                var records = new List<InputRecord>((command.Length + 1) * 2);
                foreach (char character in command)
                    AppendKey(records, character, 0);
                AppendKey(records, '\r', VirtualKeyReturn);

                // A claim command is bounded and short. Submit its entire key
                // sequence in one API call so user keystrokes cannot interleave
                // between independently written command chunks.
                var batch = records.ToArray();
                uint written;
                bool writeSucceeded = WriteConsoleInputW(input, batch, (uint)batch.Length, out written);
                int lastError = Marshal.GetLastWin32Error();
                if (!writeSucceeded || written != batch.Length)
                    throw new Win32Exception(lastError,
                        "WriteConsoleInput failed before the one-time claim command was fully submitted " +
                        "(error=" + lastError + ", success=" + writeSucceeded + ", written=" + written +
                        ", expected=" + batch.Length + ").");
            }
            finally
            {
                CloseHandle(input);
            }
        }
        finally
        {
            FreeConsole();
        }
    }

    private static void AppendKey(List<InputRecord> records, char character, ushort virtualKey)
    {
        records.Add(CreateKeyEvent(character, virtualKey, true));
        records.Add(CreateKeyEvent(character, virtualKey, false));
    }

    private static InputRecord CreateKeyEvent(char character, ushort virtualKey, bool down)
    {
        return new InputRecord
        {
            EventType = KeyEvent,
            KeyEvent = new KeyEventRecord
            {
                KeyDown = down,
                RepeatCount = 1,
                VirtualKeyCode = virtualKey,
                VirtualScanCode = 0,
                Character = new CharUnion { UnicodeChar = character },
                ControlKeyState = 0
            }
        };
    }

    [DllImport("kernel32.dll")]
    private static extern int GetCurrentProcessId();
}
'@

$normalizedClaimId = Assert-ExactGuid $ClaimId 'ClaimId'
$claimMutex = [Threading.Mutex]::new($false, 'ESFrameworkCodexExternalClaim_' + $normalizedClaimId.Replace('-', ''))
$claimMutexAcquired = $false
$targetCmd = $null
$claim = $null
try {
    try { $claimMutexAcquired = $claimMutex.WaitOne(5000) }
    catch [Threading.AbandonedMutexException] { $claimMutexAcquired = $true }
    if (-not $claimMutexAcquired) {
        throw 'Timed out waiting for the exact external-claim mutex. No console input was written.'
    }

    # Cancel, Finalize and automatic input serialize on this exact ClaimId. The
    # revalidation and write are one critical section, so no second input can
    # pass a stale "no response" check and submit a duplicate command.
    $targetCmd = Assert-TargetCmdIdentity
    Assert-TargetShellInputIdle $targetCmd.Id
    $claim = Get-VerifiedClaimCommand $normalizedClaimId
    if ($null -eq ('ESCodexExternalConsoleInputNative' -as [type])) {
        Add-Type -TypeDefinition $nativeSource -Language CSharp
    }
    [ESCodexExternalConsoleInputNative]::WriteVerifiedLine($targetCmd.Id, $claim.command)
}
finally {
    if ($claimMutexAcquired) { $claimMutex.ReleaseMutex() }
    $claimMutex.Dispose()
}

$responseObserved = $false
$waitDeadlineUtc = [DateTime]::UtcNow.AddSeconds(12)
while ([DateTime]::UtcNow -lt $waitDeadlineUtc) {
    if (Test-Path -LiteralPath $claim.responsePath -PathType Leaf) {
        $responseObserved = $true
        break
    }
    Start-Sleep -Milliseconds 80
}

[pscustomobject][ordered]@{
    success = $true
    claimId = $normalizedClaimId
    externalBindingId = $claim.externalBindingId
    cmdProcessId = $targetCmd.Id
    cmdProcessStartedAtUtc = $targetCmd.StartTime.ToUniversalTime().ToString('o')
    submittedAtUtc = [DateTime]::UtcNow.ToString('o')
    responseObserved = $responseObserved
    detail = if ($responseObserved) {
        'The verified one-time claim command was written and the target CMD response was observed.'
    } else {
        'The verified one-time claim command was written, but the target CMD response was not observed before the bounded wait expired.'
    }
} | ConvertTo-Json -Compress
