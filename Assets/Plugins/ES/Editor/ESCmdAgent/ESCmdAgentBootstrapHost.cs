using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ES
{
    internal enum ESCmdAgentManagedOperationKind : byte
    {
        LaunchNew = 0,
        Resume = 1,
        SendMessage = 2,
        Close = 3,
        RefreshStatus = 4,
        RefreshMessageStatus = 5,
        ProbeBroker = 6,
        FocusTerminal = 7,
        BindResponsibility = 8,
        PrepareExternalClaim = 9,
        SubmitExternalClaimInput = 10,
        FinalizeExternalClaim = 11,
        CancelExternalClaim = 12
    }

    [Serializable]
    internal sealed class ESCmdAgentBootstrapRequest
    {
        public string mode = string.Empty;
        // This never routes a Codex conversation. It only reconnects a durable operation
        // directory to the already-persisted local workbench tab after reload/crash.
        public string localSessionId = string.Empty;
        public string projectPath = string.Empty;
        public string sessionId = string.Empty;
        public string recordId = string.Empty;
        public string taskKey = string.Empty;
        public string responsibilityKey = string.Empty;
        public string tabTitle = string.Empty;
        public string taskPrompt = string.Empty;
        public string messageBody = string.Empty;
        public string messageId = string.Empty;
        public string idempotencyKey = string.Empty;
        public string bindResponsibilityKey = string.Empty;
        public string externalClaimId = string.Empty;
        public string externalClaimBindingId = string.Empty;
        public int externalClaimExpectedCmdProcessId;
        public string externalClaimExpectedCmdProcessStartedAtUtc = string.Empty;
        public int externalClaimTtlSeconds = 300;
        public string terminalMode = "ProjectWindow";
        public int startupWaitSeconds = 60;
        public bool probeAppServer;
    }

    internal sealed class ESCmdAgentManagedOperation
    {
        public string sessionLocalId = string.Empty;
        public ESCmdAgentManagedOperationKind kind;
        public string operationDirectory = string.Empty;
        public string requestPath = string.Empty;
        public string resultPath = string.Empty;
        public string errorPath = string.Empty;
        public string requestedSessionId = string.Empty;
        public string requestedRecordId = string.Empty;
        public string requestedMessageId = string.Empty;
        public string requestedIdempotencyKey = string.Empty;
        public string requestedExternalClaimId = string.Empty;
        public Process process;
        public Task<string> outputTask;
        public Task<string> errorTask;
        public DateTime deadlineUtc;
        public bool exitQueued;
    }

    internal readonly struct ESCmdAgentManagedOperationEvent
    {
        public ESCmdAgentManagedOperation Operation { get; }
        public bool Success { get; }
        public string Json { get; }
        public string Error { get; }

        public ESCmdAgentManagedOperationEvent(ESCmdAgentManagedOperation operation, bool success,
            string json, string error)
        {
            Operation = operation;
            Success = success;
            Json = json ?? string.Empty;
            Error = error ?? string.Empty;
        }
    }

    [Serializable]
    internal sealed class ESCmdAgentBootstrapFailureEnvelope
    {
        public bool success;
        public string error = string.Empty;
    }

    /// <summary>
    /// Bridges the Editor window to the project-owned session launcher. The short-lived
    /// PowerShell process is only a launcher client; session identity remains in the
    /// launcher registry and is never inferred from this process ID.
    /// </summary>
    internal sealed class ESCmdAgentBootstrapHost : IDisposable
    {
        private const int MaxEventsPerFlush = 24;
        private const string BridgeFileName = "Invoke-ESCmdAgentBootstrap.ps1";
        private const string OperationOwnershipFileName = ".operation-owner.json";
        private static readonly Mutex BridgeWriteMutex = new Mutex(false,
            @"Local\ESFramework.CmdAgent.BootstrapBridge.V2");
        private readonly Dictionary<string, ESCmdAgentManagedOperation> activeBySession =
            new Dictionary<string, ESCmdAgentManagedOperation>(StringComparer.Ordinal);
        private readonly HashSet<string> startingSessionIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly ConcurrentQueue<ESCmdAgentManagedOperationEvent> completed =
            new ConcurrentQueue<ESCmdAgentManagedOperationEvent>();
        private readonly object gate = new object();
        private volatile bool disposed;

        public bool HasActiveOperations
        {
            get
            {
                lock (gate)
                    return activeBySession.Count > 0;
            }
        }

        public bool TryStart(ESCmdAgentSession session, ESCmdAgentManagedOperationKind kind,
            ESCmdAgentBootstrapRequest request, out ESCmdAgentManagedOperation operation, out string error)
        {
            operation = null;
            error = string.Empty;
            if (disposed)
            {
                error = "受管会话桥接器已经释放。";
                return false;
            }
            if (session == null || string.IsNullOrWhiteSpace(session.localId) || request == null)
            {
                error = "本地会话或受管请求不可用。";
                return false;
            }
            if (!IsSupported(kind))
            {
                error = "不支持的受管会话操作。";
                return false;
            }
            bool startReserved = false;
            lock (gate)
            {
                if (activeBySession.ContainsKey(session.localId) || startingSessionIds.Contains(session.localId))
                {
                    error = "当前会话已有正在执行的受管请求。";
                    return false;
                }
                startingSessionIds.Add(session.localId);
                startReserved = true;
            }

            try
            {
                string root = ESCmdAgentStateStore.OperationDirectory;
                string operationDirectory = CreateOwnedOperationDirectory(root);
                string bridgePath = EnsureBridge(root);
                string requestPath = Path.Combine(operationDirectory, "request.json");
                string resultPath = Path.Combine(operationDirectory, "result.json");
                string errorPath = Path.Combine(operationDirectory, "stderr.log");
                WriteCreateOnlyUtf8File(requestPath, JsonUtility.ToJson(request, true));

                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "
                            + QuoteArgument(bridgePath) + " -RequestPath " + QuoteArgument(requestPath)
                            + " -ResultPath " + QuoteArgument(resultPath)
                            + " -ErrorPath " + QuoteArgument(errorPath),
                        WorkingDirectory = request.projectPath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = new UTF8Encoding(false),
                        StandardErrorEncoding = new UTF8Encoding(false)
                    },
                    EnableRaisingEvents = false
                };
                operation = new ESCmdAgentManagedOperation
                {
                    sessionLocalId = session.localId,
                    kind = kind,
                    operationDirectory = operationDirectory,
                    requestPath = requestPath,
                    resultPath = resultPath,
                    errorPath = errorPath,
                    requestedSessionId = request.sessionId?.Trim() ?? string.Empty,
                    requestedRecordId = request.recordId?.Trim() ?? string.Empty,
                    requestedMessageId = request.messageId?.Trim() ?? string.Empty,
                    requestedIdempotencyKey = request.idempotencyKey?.Trim() ?? string.Empty,
                    requestedExternalClaimId = request.externalClaimId?.Trim() ?? string.Empty,
                    process = process,
                    deadlineUtc = DateTime.UtcNow.AddSeconds(GetOperationTimeoutSeconds(kind, request))
                };
                if (!process.Start())
                    throw new InvalidOperationException("PowerShell 拒绝启动受管 Session Bootstrap 请求。");
                operation.outputTask = process.StandardOutput.ReadToEndAsync();
                operation.errorTask = process.StandardError.ReadToEndAsync();
                lock (gate)
                {
                    if (disposed)
                        throw new ObjectDisposedException(nameof(ESCmdAgentBootstrapHost));
                    startingSessionIds.Remove(session.localId);
                    activeBySession.Add(session.localId, operation);
                }
                startReserved = false;
                return true;
            }
            catch (Exception exception)
            {
                if (startReserved)
                {
                    lock (gate)
                        startingSessionIds.Remove(session.localId);
                }
                try
                {
                    if (operation?.process != null && !operation.process.HasExited)
                        operation.process.Kill();
                }
                catch { }
                try { operation?.process?.Dispose(); } catch { }
                operation = null;
                error = "无法调用项目受管 Session Bootstrap：" + exception.GetBaseException().Message;
                return false;
            }
        }

        public int Flush(Action<ESCmdAgentManagedOperationEvent> receiver)
        {
            ESCmdAgentManagedOperation[] snapshot;
            lock (gate)
                snapshot = activeBySession.Values.ToArray();
            foreach (ESCmdAgentManagedOperation operation in snapshot)
                TryCollectCompleted(operation);

            int handled = 0;
            while (handled < MaxEventsPerFlush && completed.TryDequeue(out ESCmdAgentManagedOperationEvent item))
            {
                receiver?.Invoke(item);
                handled++;
            }
            return handled;
        }

        public bool IsActive(string sessionLocalId)
        {
            lock (gate)
                return activeBySession.ContainsKey(sessionLocalId ?? string.Empty);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            ESCmdAgentManagedOperation[] snapshot;
            lock (gate)
            {
                snapshot = activeBySession.Values.ToArray();
                activeBySession.Clear();
                startingSessionIds.Clear();
            }
            foreach (ESCmdAgentManagedOperation operation in snapshot)
            {
                try
                {
                    if (operation.process != null && !operation.process.HasExited)
                        operation.process.Kill();
                }
                catch { }
                finally { TryDisposeProcess(operation); }
            }
        }

        private void TryCollectCompleted(ESCmdAgentManagedOperation operation)
        {
            if (operation == null || operation.exitQueued || operation.process == null)
                return;
            bool completionClaimed = false;
            try
            {
                if (DateTime.UtcNow > operation.deadlineUtc)
                {
                    completionClaimed = TryClaimCompletion(operation);
                    if (!completionClaimed)
                        return;
                    try
                    {
                        if (!operation.process.HasExited)
                            operation.process.Kill();
                    }
                    catch { }
                    completed.Enqueue(new ESCmdAgentManagedOperationEvent(operation, false, string.Empty,
                        "受管 Session Bootstrap 操作超时，未把结果视为已完成。请打开操作日志并重新同步状态。"));
                    TryDisposeProcess(operation);
                    return;
                }
                if (!operation.process.HasExited || operation.outputTask == null || operation.errorTask == null
                    || !operation.outputTask.IsCompleted || !operation.errorTask.IsCompleted)
                    return;
                completionClaimed = TryClaimCompletion(operation);
                if (!completionClaimed)
                    return;
                // The bridge commits result.json before it prints the same envelope. Reading
                // the durable copy first keeps the operation recoverable across domain reload.
                string output = File.Exists(operation.resultPath)
                    ? ReadStrictUtf8File(operation.resultPath)
                    : operation.outputTask.GetAwaiter().GetResult() ?? string.Empty;
                string errors = File.Exists(operation.errorPath)
                    ? ReadStrictUtf8File(operation.errorPath)
                    : operation.errorTask.GetAwaiter().GetResult() ?? string.Empty;
                if (!File.Exists(operation.resultPath))
                    WriteCreateOnlyUtf8File(operation.resultPath, output);
                if (!File.Exists(operation.errorPath))
                    WriteCreateOnlyUtf8File(operation.errorPath, errors);
                bool success = operation.process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output);
                if (!success)
                {
                    string structuredFailure = TryReadStructuredFailure(output);
                    if (!string.IsNullOrWhiteSpace(structuredFailure))
                    {
                        errors = string.IsNullOrWhiteSpace(errors)
                            ? structuredFailure
                            : structuredFailure + "\n诊断输出：" + errors;
                    }
                    else if (string.IsNullOrWhiteSpace(errors))
                    {
                        errors = "受管 Session Bootstrap 退出码：" + operation.process.ExitCode + "。";
                    }
                }
                completed.Enqueue(new ESCmdAgentManagedOperationEvent(operation, success, output, errors));
                TryDisposeProcess(operation);
            }
            catch (Exception exception)
            {
                if (!completionClaimed)
                    completionClaimed = TryClaimCompletion(operation);
                if (completionClaimed)
                {
                    completed.Enqueue(new ESCmdAgentManagedOperationEvent(operation, false, string.Empty,
                        "读取受管 Session Bootstrap 结果失败：" + exception.GetBaseException().Message));
                    TryDisposeProcess(operation);
                }
            }
        }

        private bool TryClaimCompletion(ESCmdAgentManagedOperation operation)
        {
            lock (gate)
            {
                if (operation == null || operation.exitQueued)
                    return false;
                operation.exitQueued = true;
                activeBySession.Remove(operation.sessionLocalId);
                return true;
            }
        }

        private static bool IsSupported(ESCmdAgentManagedOperationKind kind)
        {
            return kind == ESCmdAgentManagedOperationKind.LaunchNew
                || kind == ESCmdAgentManagedOperationKind.Resume
                || kind == ESCmdAgentManagedOperationKind.SendMessage
                || kind == ESCmdAgentManagedOperationKind.Close
                || kind == ESCmdAgentManagedOperationKind.RefreshStatus
                || kind == ESCmdAgentManagedOperationKind.RefreshMessageStatus
                || kind == ESCmdAgentManagedOperationKind.ProbeBroker
                || kind == ESCmdAgentManagedOperationKind.FocusTerminal
                || kind == ESCmdAgentManagedOperationKind.BindResponsibility
                || kind == ESCmdAgentManagedOperationKind.PrepareExternalClaim
                || kind == ESCmdAgentManagedOperationKind.SubmitExternalClaimInput
                || kind == ESCmdAgentManagedOperationKind.FinalizeExternalClaim
                || kind == ESCmdAgentManagedOperationKind.CancelExternalClaim;
        }

        private static string TryReadStructuredFailure(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return string.Empty;
            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = lines.Length - 1; index >= 0; index--)
            {
                try
                {
                    ESCmdAgentBootstrapFailureEnvelope envelope = JsonUtility.FromJson<ESCmdAgentBootstrapFailureEnvelope>(
                        lines[index].Trim());
                    if (envelope != null && !envelope.success && !string.IsNullOrWhiteSpace(envelope.error))
                        return envelope.error.Trim();
                }
                catch { }
            }
            return string.Empty;
        }

        private static int GetOperationTimeoutSeconds(ESCmdAgentManagedOperationKind kind,
            ESCmdAgentBootstrapRequest request)
        {
            if (kind == ESCmdAgentManagedOperationKind.LaunchNew
                || kind == ESCmdAgentManagedOperationKind.Resume)
            {
                int startupWaitSeconds = request == null ? 60 : request.startupWaitSeconds;
                return Math.Max(75, startupWaitSeconds + 15);
            }
            return kind == ESCmdAgentManagedOperationKind.ProbeBroker ? 22
                : kind == ESCmdAgentManagedOperationKind.FocusTerminal ? 15
                : kind == ESCmdAgentManagedOperationKind.PrepareExternalClaim
                    || kind == ESCmdAgentManagedOperationKind.SubmitExternalClaimInput
                    || kind == ESCmdAgentManagedOperationKind.FinalizeExternalClaim
                    || kind == ESCmdAgentManagedOperationKind.CancelExternalClaim ? 20 : 25;
        }

        private static string CreateOwnedOperationDirectory(string root)
        {
            Directory.CreateDirectory(root);
            for (int attempt = 0; attempt < 8; attempt++)
            {
                string operationDirectory = Path.Combine(root, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff")
                    + "_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(operationDirectory);
                string ownershipPath = Path.Combine(operationDirectory, OperationOwnershipFileName);
                int processId;
                using (Process currentProcess = Process.GetCurrentProcess())
                    processId = currentProcess.Id;
                string ownershipJson = "{\"schemaVersion\":1,\"createdUtc\":\""
                    + DateTime.UtcNow.ToString("O") + "\",\"processId\":" + processId + "}";
                try
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(ownershipJson);
                    using (var owner = new FileStream(ownershipPath, FileMode.CreateNew, FileAccess.Write,
                               FileShare.None))
                    {
                        owner.Write(bytes, 0, bytes.Length);
                        owner.Flush(true);
                    }
                    return operationDirectory;
                }
                catch (IOException)
                {
                    // A matching random name is not ours. Preserve it for diagnosis and choose
                    // another create-only directory instead of sharing its request/result files.
                }
            }
            throw new IOException("无法创建唯一的受管 Session Bootstrap 操作目录。");
        }

        private static string EnsureBridge(string root)
        {
            Directory.CreateDirectory(root);
            string bridgePath = Path.Combine(root, BridgeFileName);
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string launcherPath = Path.Combine(projectRoot, ".agents", "skills", "es-codex-session-bootstrap",
                "scripts", "Start-ESCodexSession.ps1");
            if (!File.Exists(launcherPath))
                throw new FileNotFoundException("项目 Session Bootstrap 脚本不存在。", launcherPath);
            string script = BuildBridgeScript(launcherPath);
            string temporaryPath = bridgePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            bool mutexAcquired = false;
            bool temporaryOwned = false;
            try
            {
                try
                {
                    mutexAcquired = BridgeWriteMutex.WaitOne(5000);
                }
                catch (AbandonedMutexException)
                {
                    mutexAcquired = true;
                }
                if (!mutexAcquired)
                    throw new TimeoutException("等待共享 Session Bootstrap Bridge 写锁超时。");

                byte[] bytes = new UTF8Encoding(false).GetBytes(script);
                using (var temporary = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                           FileShare.None))
                {
                    temporaryOwned = true;
                    temporary.Write(bytes, 0, bytes.Length);
                    temporary.Flush(true);
                }
                if (!File.Exists(bridgePath))
                {
                    File.Move(temporaryPath, bridgePath);
                }
                else
                {
                    string existing = File.ReadAllText(bridgePath, new UTF8Encoding(false, true));
                    if (string.Equals(existing, script, StringComparison.Ordinal))
                    {
                        File.Delete(temporaryPath);
                    }
                    else
                    {
                        File.Replace(temporaryPath, bridgePath, null);
                    }
                }
            }
            catch (IOException) when (File.Exists(bridgePath))
            {
                // A non-cooperating writer may still race this process. Only accept its
                // completed artifact when its bytes exactly match this bridge contract.
                string existing = File.ReadAllText(bridgePath, new UTF8Encoding(false, true));
                if (!string.Equals(existing, script, StringComparison.Ordinal))
                    throw;
            }
            finally
            {
                try
                {
                    if (temporaryOwned && File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch { }
                if (mutexAcquired)
                    BridgeWriteMutex.ReleaseMutex();
            }
            string verified = File.ReadAllText(bridgePath, new UTF8Encoding(false, true));
            if (!string.Equals(verified, script, StringComparison.Ordinal))
                throw new InvalidDataException("Session Bootstrap Bridge 提交后内容校验失败。");
            return bridgePath;
        }

        private static void WriteCreateOnlyUtf8File(string path, string content)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(content ?? string.Empty);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private static string ReadStrictUtf8File(string path)
        {
            return File.ReadAllText(path, new UTF8Encoding(false, true));
        }

        private static string BuildBridgeScript(string launcherPath)
        {
            string escapedLauncher = launcherPath.Replace("'", "''");
            return @"# ESCmdAgent Bootstrap Bridge Contract: 5
param(
    [Parameter(Mandatory = $true)][string]$RequestPath,
    [Parameter(Mandatory = $true)][string]$ResultPath,
    [Parameter(Mandatory = $true)][string]$ErrorPath
)
$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
function Write-OperationArtifact([string]$Path, [string]$Content) {
    $requestDirectory = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($RequestPath))
    $artifactDirectory = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($Path))
    if (-not [string]::Equals($requestDirectory, $artifactDirectory, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Operation artifact escapes the request directory.'
    }
    if ([System.IO.File]::Exists($Path)) { throw 'Operation artifact already exists.' }
    $temporary = Join-Path $artifactDirectory ('.' + [System.IO.Path]::GetFileName($Path) + '.' + [System.Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [System.IO.File]::WriteAllText($temporary, $Content, [System.Text.UTF8Encoding]::new($false))
        [System.IO.File]::Move($temporary, $Path)
    }
    finally {
        if ([System.IO.File]::Exists($temporary)) { [System.IO.File]::Delete($temporary) }
    }
}
try {
    $request = Get-Content -Raw -LiteralPath $RequestPath -Encoding UTF8 | ConvertFrom-Json
    $supported = @('New', 'Resume', 'SendMessage', 'Close', 'Status', 'MessageStatus', 'BrokerStatus', 'Focus', 'BindResponsibility', 'PrepareExternalClaim', 'SubmitExternalClaimInput', 'FinalizeExternalClaim', 'CancelExternalClaim')
    if ($supported -notcontains [string]$request.mode) { throw 'Unsupported managed session mode.' }
    $parameters = @{ Mode = [string]$request.mode; ProjectPath = [string]$request.projectPath }
    if (-not [string]::IsNullOrWhiteSpace([string]$request.sessionId)) { $parameters.SessionId = [string]$request.sessionId }
    if (-not [string]::IsNullOrWhiteSpace([string]$request.recordId)) { $parameters.RecordId = [string]$request.recordId }
    if (-not [string]::IsNullOrWhiteSpace([string]$request.taskKey)) { $parameters.TaskKey = [string]$request.taskKey }
    if (-not [string]::IsNullOrWhiteSpace([string]$request.responsibilityKey)) { $parameters.ResponsibilityKey = [string]$request.responsibilityKey }
    if (-not [string]::IsNullOrWhiteSpace([string]$request.tabTitle)) { $parameters.TabTitle = [string]$request.tabTitle }
    if ([string]$request.mode -eq 'New') {
        $parameters.TaskPrompt = [string]$request.taskPrompt
        $parameters.TerminalMode = [string]$request.terminalMode
        $parameters.StartupWaitSeconds = [int]$request.startupWaitSeconds
    }
    if ([string]$request.mode -eq 'Resume') {
        $parameters.TerminalMode = [string]$request.terminalMode
        $parameters.StartupWaitSeconds = [int]$request.startupWaitSeconds
    }
    if ([string]$request.mode -eq 'SendMessage') {
        $parameters.MessageBody = [string]$request.messageBody
        $parameters.IdempotencyKey = [string]$request.idempotencyKey
    }
    if ([string]$request.mode -eq 'MessageStatus') {
        $parameters.MessageId = [string]$request.messageId
        $parameters.IdempotencyKey = [string]$request.idempotencyKey
    }
    if ([string]$request.mode -eq 'BindResponsibility') {
        $parameters.BindResponsibilityKey = [string]$request.bindResponsibilityKey
    }
    if ([string]$request.mode -eq 'PrepareExternalClaim' -or [string]$request.mode -eq 'SubmitExternalClaimInput') {
        $parameters.ExternalClaimId = [string]$request.externalClaimId
        $parameters.ExternalClaimBindingId = [string]$request.externalClaimBindingId
        $parameters.ExternalClaimExpectedCmdProcessId = [int]$request.externalClaimExpectedCmdProcessId
        $parameters.ExternalClaimExpectedCmdProcessStartedAtUtc = [string]$request.externalClaimExpectedCmdProcessStartedAtUtc
        $parameters.ExternalClaimTtlSeconds = [int]$request.externalClaimTtlSeconds
    }
    if ([string]$request.mode -eq 'FinalizeExternalClaim' -or [string]$request.mode -eq 'CancelExternalClaim') {
        $parameters.ExternalClaimId = [string]$request.externalClaimId
    }
    if ([string]$request.mode -eq 'BrokerStatus' -and [bool]$request.probeAppServer) {
        $parameters.ProbeAppServer = $true
    }
    $result = & '" + escapedLauncher + @"' @parameters
    $envelope = [pscustomobject]@{ success = $true; result = $result } | ConvertTo-Json -Depth 20 -Compress
    Write-OperationArtifact $ResultPath $envelope
    [Console]::Out.WriteLine($envelope)
}
catch {
    $message = $_.Exception.Message
    $envelope = [pscustomobject]@{ success = $false; error = $message } | ConvertTo-Json -Depth 8 -Compress
    try { Write-OperationArtifact $ErrorPath $message } catch { }
    try { Write-OperationArtifact $ResultPath $envelope } catch { }
    [Console]::Out.WriteLine($envelope)
    exit 1
}";
        }

        private static string QuoteArgument(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.IndexOf('"') >= 0)
                throw new InvalidOperationException("受管桥接路径不可用。" );
            return "\"" + value + "\"";
        }

        private static void TryDisposeProcess(ESCmdAgentManagedOperation operation)
        {
            try { operation?.process?.Dispose(); } catch { }
            if (operation != null)
                operation.process = null;
        }
    }
}
