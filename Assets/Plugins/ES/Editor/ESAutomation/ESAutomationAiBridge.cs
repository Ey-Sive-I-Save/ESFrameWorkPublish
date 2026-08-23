using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

#if UNITY_INCLUDE_TESTS
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("ES_Design.ConfigKey.Tests")]
#endif

namespace ES
{
    /// <summary>
    /// AI 的受管自动化入口。外部 AI 只能通过固定 JSON 信封调用已注册任务；不能传递脚本、解释器、命令行或任意输出路径。
    /// 收件箱默认关闭，且只适合本机受信 AI。联网/多人环境必须在此桥之上另加鉴权与审批。
    /// </summary>
    public static class ESAutomationAiBridge
    {
        private const int ProtocolVersion = 1;
        private const string EnablePreferenceKey = "ES.Automation.AiBridge.Enabled";
        private const int MaxRequestBytes = 128 * 1024;
        private const int MaxRequestsPerEditorUpdate = 4;
        private const int ControlActionAuditVersion = 1;
        private const int MaxPendingSceneModificationApprovals = 32;
        private static readonly TimeSpan SceneModificationApprovalLifetime = TimeSpan.FromMinutes(5);
        private static readonly Regex RequestFileNamePattern = new Regex("^[a-fA-F0-9]{32}\\.request\\.json$", RegexOptions.Compiled);
        private static readonly Regex ActorIdPattern = new Regex("^[A-Za-z0-9._:-]{1,128}$", RegexOptions.Compiled);
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly ConcurrentQueue<string> queuedPaths = new ConcurrentQueue<string>();
        private static readonly HashSet<string> queuedPathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object queuedPathLock = new object();
        private static readonly object controlActionLock = new object();
        private static readonly object sceneApprovalLock = new object();
        private static readonly Dictionary<string, PendingSceneModificationApproval> pendingSceneModificationApprovals =
            new Dictionary<string, PendingSceneModificationApproval>(StringComparer.Ordinal);
#if UNITY_INCLUDE_TESTS
        private static readonly object testAuthorizationLock = new object();
        private static bool? testAuthorizationOverride;
        private static string testControlActionAuditDirectory;
#endif
        private static readonly ESAutomationAiControlActionContract GetCompilationStateContract =
            new ESAutomationAiControlActionContract("getUnityCompilationState", "EditorCompilation.Read", false);
        private static readonly ESAutomationAiControlActionContract SetAutoCompilationContract =
            new ESAutomationAiControlActionContract("setUnityAutoCompilation", "EditorCompilation.Control", false);
        private static readonly ESAutomationAiControlActionContract TriggerCompilationContract =
            new ESAutomationAiControlActionContract("triggerUnityCompilation", "EditorCompilation.Control", false);
        private static readonly ESAutomationAiControlActionContract ModifyActiveSceneContract =
            new ESAutomationAiControlActionContract("modifyActiveScene", "EditorScene.Modify", true);
        private static FileSystemWatcher watcher;
        private static bool updateSubscribed;
        private static volatile bool enabled;
        private static volatile bool rescanRequested;
        // 这是本次 Editor 会话的运行态门禁，绝不写回“用户已授权”的持久化设置。
        // PlayMode 默认暂停收件箱；受信的 Unity 主线程控制通道可以仅在本次 Play 中显式恢复。
        private static volatile bool playModeAutoSuspended;
        private static volatile bool trustedPlayModeListeningOverride;

        private static bool initialized;
        private static int editorMainThreadId;

        /// <summary>
        /// 仅由 AssemblyStream 的 Editor 主线程注册器调用。不能由外部桥接器或后台线程惰性初始化，
        /// 否则会把后台线程误登记为 Unity 主线程。
        /// </summary>
        internal static void InitializeForEditorMainThread()
        {
            int currentThreadId = Thread.CurrentThread.ManagedThreadId;
            int capturedThreadId = Interlocked.CompareExchange(ref editorMainThreadId, currentThreadId, 0);
            if (capturedThreadId != 0 && capturedThreadId != currentThreadId)
                throw new InvalidOperationException("ESAutomation AI Bridge 只能由 Unity Editor 主线程初始化。");
            EnsureInitializedOnEditorMainThread();
        }

        private static void EnsureInitializedOnEditorMainThread()
        {
            EnsureEditorMainThread();
            if (initialized) return;
            initialized = true;
            enabled = EditorPrefs.GetBool(EnablePreferenceKey, false);
            playModeAutoSuspended = EditorApplication.isPlayingOrWillChangePlaymode;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += EnsureStartedIfEnabled;
        }

        private static void EnsureEditorMainThread()
        {
            if (!TryEnsureEditorMainThread(out string reason))
                throw new InvalidOperationException(reason);
        }

        private static bool TryEnsureEditorMainThread(out string reason)
        {
            int capturedThreadId = Volatile.Read(ref editorMainThreadId);
            if (capturedThreadId == 0)
            {
                reason = "AI Bridge 尚未由 AssemblyStream 在 Unity Editor 主线程完成初始化。";
                return false;
            }
            if (Thread.CurrentThread.ManagedThreadId != capturedThreadId)
            {
                reason = "AI Bridge 控制请求只能在 Unity Editor 主线程执行；后台线程不得直接调用 ExecuteJson。";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public static string RootDirectory => Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "AI");
        public static string InboxDirectory => Path.Combine(RootDirectory, "Inbox");
        public static string ProcessingDirectory => Path.Combine(RootDirectory, "Processing");
        public static string ArchiveDirectory => Path.Combine(RootDirectory, "Archive");
        public static string ResponseDirectory => Path.Combine(RootDirectory, "Responses");
        internal static string ControlActionAuditDirectory
        {
            get
            {
#if UNITY_INCLUDE_TESTS
                lock (testAuthorizationLock)
                {
                    if (!string.IsNullOrWhiteSpace(testControlActionAuditDirectory))
                        return testControlActionAuditDirectory;
                }
#endif
                return Path.Combine(ESAutomationPathPolicy.RunsRoot, "ControlActions");
            }
        }

        public static bool IsEnabled
        {
            get
            {
                EnsureInitializedOnEditorMainThread();
                return enabled;
            }
            internal set
            {
                EnsureInitializedOnEditorMainThread();
                enabled = value;
                EditorPrefs.SetBool(EnablePreferenceKey, value);
                if (value) EnsureStartedIfEnabled();
                else
                {
                    if (ESAutomationUnityEditorControl.TryRestoreAiOwnedAutoCompilation(out string owner))
                        Debug.Log("[ESAutomation] AI Bridge 已关闭，已恢复由 " + owner + " 设置的自动 Unity 编译。");
                    Stop();
                }
            }
        }

        /// <summary>用户在本机 Editor 上作出的持久化授权。它与当前是否正在监听收件箱是两回事。</summary>
        public static bool IsUserAuthorized
        {
            get
            {
#if UNITY_INCLUDE_TESTS
                lock (testAuthorizationLock)
                {
                    if (testAuthorizationOverride.HasValue) return testAuthorizationOverride.Value;
                }
#endif
                return IsEnabled;
            }
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>仅供受信测试程序集临时验证授权门禁；不会修改 EditorPrefs、监听器或用户待批准计划。</summary>
        internal static IDisposable Internal_BeginTestAuthorizationScope(bool authorized)
        {
            lock (testAuthorizationLock)
            {
                bool? previous = testAuthorizationOverride;
                testAuthorizationOverride = authorized;
                return new TestAuthorizationScope(previous);
            }
        }

        /// <summary>仅供受信测试程序集隔离控制动作审计；不会写入正式 Runs/ControlActions。</summary>
        internal static IDisposable Internal_BeginTestControlActionAuditScope()
        {
            string directory = Path.Combine(
                ESAutomationPathPolicy.TempRoot,
                "Tests",
                "AiBridgeControlActions",
                Guid.NewGuid().ToString("N"));
            lock (testAuthorizationLock)
            {
                string previous = testControlActionAuditDirectory;
                testControlActionAuditDirectory = directory;
                return new TestControlActionAuditScope(directory, previous);
            }
        }

        internal static string Internal_CreateTestSceneModificationApproval(out string auditPath)
        {
            string requestId = Guid.NewGuid().ToString("N");
            ControlActionAuditScope audit = BeginControlActionAudit(requestId, "test.actor",
                ModifyActiveSceneContract, new JObject());
            var plan = new ESAutomationSceneModificationPlan("Assets/Tests/Approval.unity", false,
                new List<ESAutomationSceneModificationOperation>());
            PendingSceneModificationApproval approval = CreatePendingSceneModificationApproval(
                "test.actor", plan, audit);
            if (!TryUpdatePendingApprovalAudit(approval, "AwaitingUserApproval", "system",
                    string.Empty, string.Empty, out string reason))
                throw new InvalidOperationException(reason);
            RegisterPendingSceneModificationApproval(approval);
            auditPath = audit.AuditPath;
            return approval.ApprovalId;
        }

        internal static bool Internal_RemoveTestSceneModificationApproval(string approvalId)
        {
            lock (sceneApprovalLock)
                return !string.IsNullOrWhiteSpace(approvalId)
                    && pendingSceneModificationApprovals.Remove(approvalId);
        }
#endif

        /// <summary>进入或退出 PlayMode 期间，收件箱是否按默认策略被临时暂停。</summary>
        public static bool IsAutoSuspendedForPlayMode
        {
            get
            {
                EnsureInitializedOnEditorMainThread();
                return playModeAutoSuspended && !trustedPlayModeListeningOverride;
            }
        }

        /// <summary>当前是否确实在监听 Inbox。仅用于 Center 状态显示和受信宿主诊断。</summary>
        public static bool IsListening
        {
            get
            {
                EnsureInitializedOnEditorMainThread();
                return watcher != null && watcher.EnableRaisingEvents && updateSubscribed;
            }
        }

        public static string ListeningStateDescription
        {
            get
            {
                if (!IsEnabled) return "未授权：本机 AI 收件箱已关闭。";
                if (IsAutoSuspendedForPlayMode) return "PlayMode 已自动暂停监听；用户授权仍保留，退出 Play 后会自动恢复。";
                if (EditorApplication.isPlaying && trustedPlayModeListeningOverride) return "PlayMode 中：已由受信 Unity 控制通道为本次会话临时恢复监听。";
                return IsListening ? "编辑模式监听中。" : "正在准备监听。";
            }
        }

        /// <summary>
        /// 仅供已经由宿主鉴权的 UnityMCP 或同等 Unity 主线程桥接器调用。
        /// 该方法不能开启首次用户授权，也不会把覆盖状态持久化到下一次 PlayMode。
        /// 外部进程不能通过 Inbox 调用它，避免关闭的收件箱自行开启的循环授权问题。
        /// </summary>
        public static bool TrySetTrustedPlayModeListening(bool shouldListen, out string reason)
        {
            if (!TryEnsureEditorMainThread(out reason)) return false;
            EnsureInitializedOnEditorMainThread();
            if (!IsEnabled)
            {
                reason = "本机 AI 收件箱尚未获得用户授权；请先在【ES】/自动化与开发/自动化中心启用。";
                return false;
            }
            if (!EditorApplication.isPlaying)
            {
                reason = "仅能在已进入 PlayMode 后设置本次临时监听；编辑模式会自动监听。";
                return false;
            }

            trustedPlayModeListeningOverride = shouldListen;
            EnsureStartedIfEnabled();
            reason = shouldListen
                ? "已在本次 PlayMode 临时恢复 AI 收件箱监听；仅 allowInPlayMode 任务可执行。"
                : "已在本次 PlayMode 暂停 AI 收件箱监听。";
            return true;
        }

        /// <summary>供同一 Unity Editor 主线程中的受信桥接器调用。外部进程必须使用 Inbox/Responses 文件协议。</summary>
        public static string ExecuteJson(string requestJson)
        {
            if (!TryEnsureEditorMainThread(out string threadReason))
                return JsonConvert.SerializeObject(ESAutomationAiResponse.Rejected(string.Empty, string.Empty, threadReason), Formatting.Indented);
            EnsureInitializedOnEditorMainThread();
            if (!TryValidateDirectRequestSize(requestJson, out string sizeReason))
                return JsonConvert.SerializeObject(ESAutomationAiResponse.Rejected(string.Empty, string.Empty, sizeReason), Formatting.Indented);
            ESAutomationAiResponse response = HandleRequest(requestJson, null);
            return JsonConvert.SerializeObject(response, Formatting.Indented);
        }

        public static string GetRequestExamplePath() => Path.Combine(RootDirectory, "README.md");

        // UnityMCP 若只支持 ExecuteMenuItem，可使用这两个固定菜单；它们与公开 API 共用同一门禁。
        [MenuItem(MenuItemPathDefine.AUTOMATION_AI_CONTROL_PATH + "PlayMode 临时恢复收件箱监听")]
        private static void ResumePlayModeListeningFromMenu()
        {
            if (!TrySetTrustedPlayModeListening(true, out string reason)) Debug.LogWarning("[ESAutomation] " + reason);
            else Debug.Log("[ESAutomation] " + reason);
        }

        [MenuItem(MenuItemPathDefine.AUTOMATION_AI_CONTROL_PATH + "PlayMode 临时恢复收件箱监听", true)]
        private static bool ValidateResumePlayModeListeningFromMenu()
            => IsEnabled && EditorApplication.isPlaying && IsAutoSuspendedForPlayMode;

        [MenuItem(MenuItemPathDefine.AUTOMATION_AI_CONTROL_PATH + "PlayMode 暂停收件箱监听")]
        private static void PausePlayModeListeningFromMenu()
        {
            if (!TrySetTrustedPlayModeListening(false, out string reason)) Debug.LogWarning("[ESAutomation] " + reason);
            else Debug.Log("[ESAutomation] " + reason);
        }

        [MenuItem(MenuItemPathDefine.AUTOMATION_AI_CONTROL_PATH + "PlayMode 暂停收件箱监听", true)]
        private static bool ValidatePausePlayModeListeningFromMenu()
            => IsEnabled && EditorApplication.isPlaying && trustedPlayModeListeningOverride;

        private static void EnsureStartedIfEnabled()
        {
            if (!ShouldListen())
            {
                Stop();
                return;
            }
            try
            {
                EnsureDirectories();
                if (watcher == null)
                {
                    watcher = new FileSystemWatcher(InboxDirectory, "*.request.json")
                    {
                        IncludeSubdirectories = false,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size,
                        EnableRaisingEvents = true,
                    };
                    watcher.Created += OnWatcherFileEvent;
                    watcher.Renamed += OnWatcherRenamed;
                    watcher.Error += OnWatcherError;
                }
                if (!updateSubscribed)
                {
                    updateSubscribed = true;
                    EditorApplication.update += ProcessQueuedRequests;
                }
                foreach (string requestPath in Directory.GetFiles(InboxDirectory, "*.request.json", SearchOption.TopDirectoryOnly)) QueuePath(requestPath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Stop();
            }
        }

        private static void Stop()
        {
            // Stop 是授权撤销、PlayMode 切换和监听故障的共同收口点；
            // 不保留本次 Play 的受信覆盖，避免重新启用 Bridge 后静默恢复监听。
            trustedPlayModeListeningOverride = false;
            if (updateSubscribed)
            {
                updateSubscribed = false;
                EditorApplication.update -= ProcessQueuedRequests;
            }
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Created -= OnWatcherFileEvent;
                watcher.Renamed -= OnWatcherRenamed;
                watcher.Error -= OnWatcherError;
                watcher.Dispose();
                watcher = null;
            }
            lock (queuedPathLock) queuedPathSet.Clear();
            while (queuedPaths.TryDequeue(out _)) { }
            rescanRequested = false;
            ClearPendingSceneModificationApprovals("Bridge 停止、PlayMode 切换或 Domain Reload 使待批准场景计划失效。");
        }

        private static void OnWatcherFileEvent(object sender, FileSystemEventArgs arguments) => QueuePath(arguments.FullPath);
        private static void OnWatcherRenamed(object sender, RenamedEventArgs arguments) => QueuePath(arguments.FullPath);
        private static void OnWatcherError(object sender, ErrorEventArgs arguments) => rescanRequested = true;

        private static void QueuePath(string path)
        {
            if (!ShouldListen() || string.IsNullOrWhiteSpace(path)) return;
            string fileName = Path.GetFileName(path);
            if (!RequestFileNamePattern.IsMatch(fileName)) return;
            lock (queuedPathLock)
            {
                if (!queuedPathSet.Add(path)) return;
                queuedPaths.Enqueue(path);
            }
        }

        private static void ProcessQueuedRequests()
        {
            if (!ShouldListen())
            {
                Stop();
                return;
            }
            if (rescanRequested)
            {
                rescanRequested = false;
                try
                {
                    foreach (string requestPath in Directory.GetFiles(InboxDirectory, "*.request.json", SearchOption.TopDirectoryOnly)) QueuePath(requestPath);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
            for (int index = 0; index < MaxRequestsPerEditorUpdate && queuedPaths.TryDequeue(out string requestPath); index++)
            {
                lock (queuedPathLock) queuedPathSet.Remove(requestPath);
                ProcessRequestFile(requestPath);
            }
        }

        private static void ProcessRequestFile(string requestPath)
        {
            // 文件名允许大小写十六进制，但 requestId 的身份不区分大小写；
            // 统一响应路径，避免同一逻辑 ID 的非控制请求绕过既有响应。
            string requestId = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(requestPath))
                .ToLowerInvariant();
            string processingPath = null;
            bool movedToProcessing = false;
            try
            {
                EnsureDirectories();
                EnsureBridgePath(requestPath, InboxDirectory);
                if (!File.Exists(requestPath)) return;
                // Inbox 的最终扩展名只能通过“临时文件写完后原子改名”产生。
                // FileSystemWatcher 可能在写入者关闭文件前收到事件，因此这里必须再次
                // 确认文件尺寸/时间戳稳定。稳定但空、过大或非 UTF-8 的文件会被正常拒绝并归档；
                // 只有仍在写入的文件才留在 Inbox 等待下一轮，避免永久重试坏输入。
                if (!TryReadStableRequest(requestPath, out string requestJson, out string terminalRejection))
                {
                    QueuePath(requestPath);
                    return;
                }

                processingPath = Path.Combine(ProcessingDirectory, Path.GetFileName(requestPath));
                EnsureBridgePath(processingPath, ProcessingDirectory);
                if (File.Exists(processingPath)) throw new IOException("AI 请求已在处理队列中：" + requestId);
                File.Move(requestPath, processingPath);
                movedToProcessing = true;

                string responsePath = Path.Combine(ResponseDirectory, requestId + ".response.json");
                if (!File.Exists(responsePath))
                {
                    ESAutomationAiResponse response = !string.IsNullOrWhiteSpace(terminalRejection)
                        ? ESAutomationAiResponse.Rejected(requestId, string.Empty, terminalRejection)
                        : HandleRequest(requestJson, requestId);
                    WriteResponse(responsePath, response);
                }

                ArchiveProcessedRequest(processingPath);
                movedToProcessing = false;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                try
                {
                    if (IsValidRequestId(requestId))
                    {
                        string responsePath = Path.Combine(ResponseDirectory, requestId + ".response.json");
                        if (!File.Exists(responsePath)) WriteResponse(responsePath, ESAutomationAiResponse.Failed(requestId, string.Empty, exception.Message));
                    }
                }
                catch (Exception responseException)
                {
                    Debug.LogException(responseException);
                }
            }
            finally
            {
                // 只收口本次成功移入 Processing 的文件；若响应或首次归档失败，不能让它
                // 永久占据处理目录，也不能误动另一个并发请求留下的同名文件。
                if (movedToProcessing && !string.IsNullOrWhiteSpace(processingPath)
                    && File.Exists(processingPath))
                {
                    try
                    {
                        ArchiveProcessedRequest(processingPath);
                    }
                    catch (Exception archiveException)
                    {
                        Debug.LogException(archiveException);
                    }
                }
            }
        }

        private static bool TryReadStableRequest(string path, out string requestJson, out string terminalRejection)
        {
            requestJson = null;
            terminalRejection = string.Empty;
            try
            {
                FileInfo before = new FileInfo(path);
                if (!before.Exists)
                    return false;

                if (before.Length > MaxRequestBytes)
                {
                    FileInfo tooLargeAfter = new FileInfo(path);
                    if (!tooLargeAfter.Exists
                        || before.Length != tooLargeAfter.Length
                        || before.LastWriteTimeUtc != tooLargeAfter.LastWriteTimeUtc)
                        return false;
                    terminalRejection = "AI 请求超过 128 KiB 限制。";
                    return true;
                }

                byte[] bytes = File.ReadAllBytes(path);
                FileInfo after = new FileInfo(path);
                if (!after.Exists
                    || before.Length != after.Length
                    || before.LastWriteTimeUtc != after.LastWriteTimeUtc
                    || bytes.LongLength != before.Length)
                    return false;

                if (bytes.Length == 0)
                {
                    terminalRejection = "AI 请求不能为空。";
                    return true;
                }

                try
                {
                    requestJson = StrictUtf8.GetString(bytes);
                }
                catch (DecoderFallbackException)
                {
                    terminalRejection = "AI 请求必须是严格 UTF-8 JSON，不能包含无效编码字节。";
                    return true;
                }
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static void ArchiveProcessedRequest(string processingPath)
        {
            EnsureBridgePath(processingPath, ProcessingDirectory);
            string fileName = Path.GetFileName(processingPath);
            string archivePath = Path.Combine(ArchiveDirectory, fileName);
            if (File.Exists(archivePath))
            {
                // 原始归档不可覆盖。相同 RequestId 的重复输入不再触发业务处理，
                // 但仍保留独立文件以便人工审计，而不是永久卡在 Processing。
                string replayFileName = Path.GetFileNameWithoutExtension(fileName)
                    + ".replay-" + Guid.NewGuid().ToString("N") + ".json";
                archivePath = Path.Combine(ArchiveDirectory, replayFileName);
            }
            EnsureBridgePath(archivePath, ArchiveDirectory);
            File.Move(processingPath, archivePath);
        }

        private static bool TryValidateDirectRequestSize(string requestJson, out string reason)
        {
            try
            {
                if (StrictUtf8.GetByteCount(requestJson ?? string.Empty) <= MaxRequestBytes)
                {
                    reason = string.Empty;
                    return true;
                }
                reason = "AI 直接请求超过 128 KiB 限制。";
                return false;
            }
            catch (EncoderFallbackException)
            {
                reason = "AI 直接请求包含无效 Unicode 字符，不能编码为严格 UTF-8。";
                return false;
            }
        }

        private static ESAutomationAiResponse HandleRequest(string requestJson, string expectedRequestId)
        {
            string requestId = string.IsNullOrWhiteSpace(expectedRequestId)
                ? string.Empty
                : expectedRequestId.ToLowerInvariant();
            string action = string.Empty;
            try
            {
                JObject root = JObject.Parse(requestJson ?? string.Empty);
                RequireExactProperties(root, new[] { "protocolVersion", "requestId", "actorId", "action", "payload" }, "AI 请求");
                if (ReadInteger(root, "protocolVersion") != ProtocolVersion) throw new InvalidOperationException("不支持的 AI 请求协议版本。");
                string envelopeRequestId = ReadRequestId(root, "requestId");
                if (!string.IsNullOrWhiteSpace(expectedRequestId)
                    && !string.Equals(expectedRequestId, envelopeRequestId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("请求文件名与 requestId 不一致。");
                requestId = envelopeRequestId;
                string actorId = ReadString(root, "actorId");
                if (!ActorIdPattern.IsMatch(actorId)) throw new InvalidOperationException("actorId 必须是 1–128 位安全标识符，不能包含空格或路径字符。");
                action = ReadString(root, "action");
                if (root["payload"].Type != JTokenType.Object) throw new InvalidOperationException("payload 必须是对象。");
                JObject payload = (JObject)root["payload"];
                if (!IsUserAuthorized)
                    return ESAutomationAiResponse.Rejected(requestId, action,
                        "AI Bridge 尚未获得当前用户授权；请在【ES】/自动化与开发/自动化中心启用本机 AI 请求收件箱。");

                if (TryGetControlActionContract(action, out ESAutomationAiControlActionContract controlAction))
                    return ExecuteControlAction(requestId, action, actorId, payload, controlAction);

                switch (action)
                {
                    case "listTasks":
                        RequireExactProperties(payload, Array.Empty<string>(), "listTasks payload");
                        return ESAutomationAiResponse.Completed(requestId, action, "已返回 AIBrain 编排合同、AI 可调用任务与已注册内容提案入口；生产力面请调用 listCapabilities。", string.Empty, new JObject
                        {
                            ["brain"] = new JObject
                            {
                                ["contractVersion"] = ESAIBrainCoordinator.ContractVersion,
                                ["knowledgeIndex"] = ESAIBrainCoordinator.KnowledgeIndexPath,
                                ["skillsRoot"] = ESAIBrainCoordinator.ProjectSkillsRoot,
                                ["discoveryAction"] = "listCapabilities",
                                ["planningAction"] = "planTask",
                                ["execution"] = "runTask 必须先经 AIBrain 计划与权威门禁",
                            },
                            ["tasks"] = JArray.FromObject(ESAutomationFacade.CopyDescriptors()),
                            ["contentTypes"] = JArray.FromObject(ESAutomationContentIngress.CopyDescriptors()),
                        });
                    case "listCapabilities":
                        return HandleListCapabilities(requestId, action, payload);
                    case "planTask":
                        return HandlePlanTask(requestId, action, actorId, payload);
                    case "runTask":
                        return HandleRunTask(requestId, action, actorId, payload);
                    case "getRun":
                        return HandleGetRun(requestId, action, payload);
                    case "cancelRun":
                        return HandleCancelRun(requestId, action, actorId, payload);
                    case "submitInput":
                        return HandleSubmitInput(requestId, action, actorId, payload);
                    case "submitContentProposal":
                        return HandleContentProposal(requestId, action, actorId, payload);
                    default:
                        return ESAutomationAiResponse.Rejected(requestId, action, "未注册的 AI 自动化动作：" + action);
                }
            }
            catch (Exception exception)
            {
                return ESAutomationAiResponse.Rejected(requestId, action, exception.Message);
            }
        }

        /// <summary>
        /// Unity 控制面不是可动态注册的 Worker。这里只接受固定的能力合同，并在任何动作前创建不可覆盖审计记录。
        /// 审计写入失败时不会执行动作；完成审计失败时也不会把可能已经发生的副作用伪装为成功。
        /// </summary>
        private static ESAutomationAiResponse ExecuteControlAction(
            string requestId,
            string action,
            string actorId,
            JObject payload,
            ESAutomationAiControlActionContract contract)
        {
            lock (controlActionLock)
            {
                ControlActionAuditScope audit = BeginControlActionAudit(requestId, actorId, contract, payload);
                ESAutomationAiResponse response;
                try
                {
                    switch (contract.Action)
                    {
                        case "getUnityCompilationState":
                            RequireExactProperties(payload, Array.Empty<string>(), "getUnityCompilationState payload");
                            response = ESAutomationAiResponse.Completed(requestId, action,
                                "已返回 Unity 编译控制状态。", string.Empty,
                                ESAutomationUnityEditorControl.GetCompilationState());
                            break;
                        case "setUnityAutoCompilation":
                            response = HandleSetUnityAutoCompilation(requestId, action, actorId, payload);
                            break;
                        case "triggerUnityCompilation":
                            response = HandleTriggerUnityCompilation(requestId, action, payload);
                            break;
                        case "modifyActiveScene":
                            response = HandleModifyActiveScene(requestId, action, actorId, payload, audit);
                            break;
                        default:
                            response = ESAutomationAiResponse.Rejected(requestId, action,
                                "控制动作合同没有可执行处理器：" + contract.Action);
                            break;
                    }
                }
                catch (Exception exception)
                {
                    response = ESAutomationAiResponse.Rejected(requestId, action, exception.Message);
                }

                try
                {
                    CompleteControlActionAudit(audit, response);
                    return response;
                }
                catch (Exception auditException)
                {
                    return CreateControlAuditFailureResponse(requestId, action, response, auditException);
                }
            }
        }

        private static ESAutomationAiResponse HandleRunTask(string requestId, string action, string actorId, JObject payload)
        {
            ESAIBrainRequest brainRequest = CreateBrainRequest(requestId, actorId, payload, "runTask");
            ESAutomationTaskInvocationResult result = ESAIBrainCoordinator.Run(
                brainRequest, out ESAIBrainPlan plan);
            return FromTaskResultWithPlan(requestId, action, result, plan);
        }

        private static ESAutomationAiResponse HandleListCapabilities(string requestId, string action, JObject payload)
        {
            RequireExactProperties(payload, Array.Empty<string>(), new[] { "routeKeys" }, "listCapabilities payload");
            List<string> routeKeys = payload["routeKeys"] == null
                ? new List<string>() : ReadStringArray(payload, "routeKeys");
            ESAIBrainProductionSurface surface = ESAIBrainCoordinator.DescribeProductionSurface(routeKeys);
            return ESAutomationAiResponse.Completed(requestId, action,
                "已返回 Skills、AIWarnings、Knowledge、AICommand、CLI 和 MCP 生产力面。",
                string.Empty, JObject.FromObject(surface));
        }

        private static ESAutomationAiResponse HandlePlanTask(string requestId, string action,
            string actorId, JObject payload)
        {
            ESAIBrainRequest brainRequest = CreateBrainRequest(requestId, actorId, payload, "planTask");
            ESAIBrainPlan plan = ESAIBrainCoordinator.Plan(brainRequest);
            return FromBrainPlan(requestId, action, plan);
        }

        private static ESAIBrainRequest CreateBrainRequest(string requestId, string actorId,
            JObject payload, string context)
        {
            RequireExactProperties(payload, new[]
                { "objective", "routeKeys", "commandId", "taskId", "taskVersion", "preset", "input" },
                new[] { "skillNames", "dryRun" }, context + " payload");
            if (payload["routeKeys"].Type != JTokenType.Array)
                throw new InvalidOperationException(context + ".routeKeys 必须是数组。");
            if (payload["skillNames"] != null && payload["skillNames"].Type != JTokenType.Array)
                throw new InvalidOperationException(context + ".skillNames 必须是数组。");
            if (payload["input"].Type != JTokenType.Object) throw new InvalidOperationException(context + ".input 必须是对象。");
            bool dryRun = false;
            if (payload["dryRun"] != null)
            {
                if (payload["dryRun"].Type != JTokenType.Boolean)
                    throw new InvalidOperationException("dryRun 必须是布尔值。");
                dryRun = (bool)payload["dryRun"];
            }
            ESAIBrainRequest brainRequest = new ESAIBrainRequest
            {
                objective = ReadString(payload, "objective"),
                routeKeys = ReadStringArray(payload, "routeKeys"),
                commandId = ReadString(payload, "commandId"),
                skillNames = payload["skillNames"] == null
                    ? new List<string>() : ReadStringArray(payload, "skillNames"),
                taskId = ReadString(payload, "taskId"),
                taskVersion = ReadInteger(payload, "taskVersion"),
                preset = ReadString(payload, "preset", allowEmpty: true),
                input = (JObject)payload["input"],
                fromAi = true,
                dryRun = dryRun,
                actorId = actorId,
                invocationId = requestId,
            };
            return brainRequest;
        }

        private static ESAutomationAiResponse HandleGetRun(string requestId, string action, JObject payload)
        {
            RequireExactProperties(payload, new[] { "runId" }, "getRun payload");
            ESAutomationTaskInvocationResult result = ESAutomationFacade.GetRun(ReadRequestId(payload, "runId"), true);
            return FromTaskResult(requestId, action, result);
        }

        private static ESAutomationAiResponse HandleCancelRun(string requestId, string action,
            string actorId, JObject payload)
        {
            RequireExactProperties(payload, new[] { "runId" }, "cancelRun payload");
            ESAutomationTaskInvocationResult result = ESAutomationFacade.CancelRun(
                ReadRequestId(payload, "runId"), actorId, true);
            return FromTaskResult(requestId, action, result);
        }

        private static ESAutomationAiResponse HandleSubmitInput(string requestId, string action, string actorId, JObject payload)
        {
            RequireExactProperties(payload, new[] { "runId", "requestGeneration", "stepId", "schemaHash", "accepted", "values" }, "submitInput payload");
            if (payload["values"].Type != JTokenType.Object) throw new InvalidOperationException("submitInput.values 必须是对象。");
            if (payload["accepted"].Type != JTokenType.Boolean) throw new InvalidOperationException("submitInput.accepted 必须是布尔值。");
            ESAutomationTaskInvocationResult result = ESAutomationFacade.SubmitInput(new ESAutomationTaskInputSubmission
            {
                runId = ReadRequestId(payload, "runId"),
                requestGeneration = ReadInteger(payload, "requestGeneration"),
                stepId = ReadString(payload, "stepId"),
                schemaHash = ReadSha256(payload, "schemaHash"),
                accepted = (bool)payload["accepted"],
                values = (JObject)payload["values"],
                fromAi = true,
                actorId = actorId,
            });
            return FromTaskResult(requestId, action, result);
        }

        private static ESAutomationAiResponse HandleContentProposal(string requestId, string action, string actorId, JObject payload)
        {
            RequireExactProperties(payload, new[] { "contentType", "contentVersion", "schemaHash", "payload" }, "submitContentProposal payload");
            if (payload["payload"].Type != JTokenType.Object) throw new InvalidOperationException("内容提案 payload 必须是对象。");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return ESAutomationAiResponse.Rejected(requestId, action, "PlayMode 中禁止提交内容提案或变更 Unity 资产。");
            ESAutomationContentProposalResult result = ESAutomationContentIngress.Submit(new ESAutomationContentProposal
            {
                requestId = requestId,
                actorId = actorId,
                contentType = ReadString(payload, "contentType"),
                contentVersion = ReadInteger(payload, "contentVersion"),
                schemaHash = ReadSha256(payload, "schemaHash"),
                payload = (JObject)payload["payload"],
            });
            return new ESAutomationAiResponse
            {
                protocolVersion = ProtocolVersion,
                requestId = requestId,
                action = action,
                status = result.status,
                message = result.message,
                receiptId = result.receiptId,
                data = result.data ?? new JObject(),
            };
        }

        private static ESAutomationAiResponse HandleSetUnityAutoCompilation(
            string requestId, string action, string actorId, JObject payload)
        {
            RequireExactProperties(payload, new[] { "enabled" }, "setUnityAutoCompilation payload");
            if (payload["enabled"].Type != JTokenType.Boolean) throw new InvalidOperationException("enabled 必须是布尔值。");
            JObject data = ESAutomationUnityEditorControl.SetAutoCompilationFromAi((bool)payload["enabled"], actorId);
            return ESAutomationAiResponse.Completed(requestId, action, "已更新 Unity 自动编译策略。", string.Empty, data);
        }

        private static ESAutomationAiResponse HandleTriggerUnityCompilation(string requestId, string action, JObject payload)
        {
            RequireExactProperties(payload, new[] { "forceRefresh" }, "triggerUnityCompilation payload");
            if (payload["forceRefresh"].Type != JTokenType.Boolean) throw new InvalidOperationException("forceRefresh 必须是布尔值。");
            JObject data = ESAutomationUnityEditorControl.TriggerCompilation((bool)payload["forceRefresh"]);
            return ESAutomationAiResponse.Completed(requestId, action, "已请求 Unity 脚本编译。", string.Empty, data);
        }

        private static ESAutomationAiResponse HandleModifyActiveScene(
            string requestId,
            string action,
            string actorId,
            JObject payload,
            ControlActionAuditScope audit)
        {
            if (payload["dryRun"] == null || payload["dryRun"].Type != JTokenType.Boolean)
                throw new InvalidOperationException("dryRun 必须是布尔值。");
            bool dryRun = (bool)payload["dryRun"];
            RequireExactProperties(payload,
                dryRun
                    ? new[] { "scenePath", "operations", "save", "dryRun" }
                    : new[] { "scenePath", "operations", "save", "dryRun", "approvalId" },
                "modifyActiveScene payload");
            if (payload["operations"].Type != JTokenType.Array) throw new InvalidOperationException("operations 必须是数组。");
            if (payload["save"].Type != JTokenType.Boolean) throw new InvalidOperationException("save 必须是布尔值。");
            string scenePath = ReadString(payload, "scenePath");
            JArray operations = (JArray)payload["operations"];
            bool save = (bool)payload["save"];

            if (dryRun)
            {
                if (!CanCreatePendingSceneModificationApproval(out string capacityReason))
                    return ESAutomationAiResponse.Rejected(requestId, action, capacityReason);
                ESAutomationSceneModificationPlan plan = ESAutomationUnityEditorControl.PrepareActiveSceneModification(
                    scenePath, operations, save);
                PendingSceneModificationApproval pendingApproval = CreatePendingSceneModificationApproval(actorId, plan, audit);
                JObject data = plan.CreateResponseData(true, false);
                data["approvalRequired"] = true;
                data["approvalId"] = pendingApproval.ApprovalId;
                data["approvalExpiresUtc"] = pendingApproval.ExpiresAtUtc.ToString("O");
                data["planFingerprint"] = pendingApproval.PlanFingerprint;
                return ESAutomationAiResponse.Completed(requestId, action,
                    "场景计划已验证，等待用户在自动化中心批准一次；批准后必须使用新的 requestId、相同计划和 approvalId 提交 dryRun=false。",
                    pendingApproval.ApprovalId, data);
            }

            string approvalId = ReadRequestId(payload, "approvalId");
            audit.ApprovalId = approvalId;
            if (!TryGetApprovedSceneModificationApproval(approvalId, actorId,
                    out PendingSceneModificationApproval approval, out string approvalReason))
                return ESAutomationAiResponse.Rejected(requestId, action, approvalReason);

            audit.Record["approvalPlanFingerprint"] = approval.PlanFingerprint;

            ESAutomationSceneModificationPlan submittedPlan = ESAutomationUnityEditorControl.PrepareActiveSceneModification(
                scenePath, operations, save);
            string submittedFingerprint = ComputeJsonSha256(submittedPlan.CreateFingerprintPayload());
            if (!string.Equals(approval.PlanFingerprint, submittedFingerprint, StringComparison.Ordinal))
                return ESAutomationAiResponse.Rejected(requestId, action,
                    "场景计划与已批准内容不一致；请重新 dryRun 并取得新的用户批准。");
            if (!TryConsumeApprovedSceneModificationApproval(approval, requestId, out approvalReason))
                return ESAutomationAiResponse.Rejected(requestId, action, approvalReason);

            try
            {
                JObject data = ESAutomationUnityEditorControl.ApplyPreparedSceneModification(approval.Plan);
                data["approvalId"] = approvalId;
                return ESAutomationAiResponse.Completed(requestId, action,
                    "已执行一次性批准的 Active Scene 场景计划。", approvalId, data);
            }
            catch (Exception exception)
            {
                return ESAutomationAiResponse.Failed(requestId, action,
                    "已消费场景批准，但应用计划失败。未能确认全部操作已生效；请检查 Undo 和当前 Active Scene。原因：" + exception.Message);
            }
        }

        private static ESAutomationAiResponse FromTaskResult(string requestId, string action, ESAutomationTaskInvocationResult result)
        {
            return new ESAutomationAiResponse
            {
                protocolVersion = ProtocolVersion,
                requestId = requestId,
                action = action,
                status = result.status,
                message = result.message,
                runId = result.runId,
                data = result.data ?? new JObject(),
            };
        }

        private static ESAutomationAiResponse FromTaskResultWithPlan(string requestId, string action,
            ESAutomationTaskInvocationResult result, ESAIBrainPlan plan)
        {
            ESAutomationAiResponse response = FromTaskResult(requestId, action, result);
            response.data["brainPlan"] = CreateBrainPlanSummary(plan);
            return response;
        }

        private static ESAutomationAiResponse FromBrainPlan(string requestId, string action,
            ESAIBrainPlan plan)
        {
            bool ready = plan != null && plan.IsRunnable;
            return new ESAutomationAiResponse
            {
                protocolVersion = ProtocolVersion,
                requestId = requestId,
                action = action,
                status = ready ? "Completed" : "Blocked",
                message = ready ? "AIBrain 只读计划已通过，可在用户确认后调用 runTask。"
                    : "AIBrain 只读计划被门禁阻断。",
                data = new JObject { ["brainPlan"] = CreateBrainPlanSummary(plan) },
            };
        }

        private static JObject CreateBrainPlanSummary(ESAIBrainPlan plan)
        {
            if (plan == null) return new JObject { ["status"] = "Invalid" };
            return JObject.FromObject(new
            {
                planId = plan.planId,
                planHash = plan.planHash,
                status = plan.status,
                blockers = plan.blockers,
                knowledge = plan.knowledge.Select(item => item.knowledgeId).ToArray(),
                warnings = plan.warnings.Select(item => item.projectPath).ToArray(),
                command = plan.command?.id ?? string.Empty,
                skills = plan.skills.Select(item => new
                {
                    item.name,
                    item.tier,
                    item.maturity,
                    item.delivery,
                    item.evidenceLevel,
                    item.riskClass,
                    item.governanceHash,
                    item.requiresBrainPlan,
                    item.allowDirectExecution,
                    item.writePolicy,
                    item.discoveryState,
                    item.planEligibility,
                    item.runtimeEligibility,
                    item.reviewRequired,
                }).ToArray(),
                workflow = plan.workflow?.workflowId ?? string.Empty,
                task = plan.task == null ? string.Empty : plan.task.taskId + "@" + plan.task.taskVersion,
            });
        }

        /// <summary>供自动化中心展示和批准；返回快照，不暴露待批准计划的可变内部对象。</summary>
        internal static int PendingSceneModificationApprovalCapacity => MaxPendingSceneModificationApprovals;

        internal static IReadOnlyList<ESAutomationSceneModificationApprovalInfo> CopyPendingSceneModificationApprovals()
        {
            lock (sceneApprovalLock)
            {
                PurgeExpiredSceneModificationApprovals(DateTimeOffset.UtcNow);
                var snapshots = new List<ESAutomationSceneModificationApprovalInfo>(pendingSceneModificationApprovals.Count);
                foreach (PendingSceneModificationApproval approval in pendingSceneModificationApprovals.Values)
                    snapshots.Add(approval.CreateInfo());
                snapshots.Sort((left, right) => left.CreatedAtUtc.CompareTo(right.CreatedAtUtc));
                return snapshots;
            }
        }

        internal static bool TryApproveSceneModification(string approvalId, out string reason)
        {
            if (!IsValidRequestId(approvalId))
            {
                reason = "批准 ID 必须是 N 格式 GUID。";
                return false;
            }

            lock (sceneApprovalLock)
            {
                PurgeExpiredSceneModificationApprovals(DateTimeOffset.UtcNow);
                if (!pendingSceneModificationApprovals.TryGetValue(approvalId, out PendingSceneModificationApproval approval))
                {
                    reason = "待批准场景计划不存在、已过期或已被消费。";
                    return false;
                }
                if (approval.Status != PendingSceneModificationApprovalStatus.AwaitingUserApproval)
                {
                    reason = "该场景计划当前不能批准：" + approval.Status;
                    return false;
                }
                if (!TryUpdatePendingApprovalAudit(approval, "Approved", "editor.user", string.Empty,
                        string.Empty, out reason))
                    return false;
                approval.Status = PendingSceneModificationApprovalStatus.Approved;
                reason = string.Empty;
                return true;
            }
        }

        internal static bool TryRejectSceneModification(string approvalId, out string reason)
        {
            if (!IsValidRequestId(approvalId))
            {
                reason = "批准 ID 必须是 N 格式 GUID。";
                return false;
            }

            lock (sceneApprovalLock)
            {
                PurgeExpiredSceneModificationApprovals(DateTimeOffset.UtcNow);
                if (!pendingSceneModificationApprovals.TryGetValue(approvalId, out PendingSceneModificationApproval approval))
                {
                    reason = "待批准场景计划不存在、已过期或已被消费。";
                    return false;
                }
                if (approval.Status != PendingSceneModificationApprovalStatus.AwaitingUserApproval)
                {
                    reason = "该场景计划当前不能拒绝：" + approval.Status;
                    return false;
                }
                if (!TryUpdatePendingApprovalAudit(approval, "RejectedByUser", "editor.user", string.Empty,
                        string.Empty, out reason))
                    return false;
                pendingSceneModificationApprovals.Remove(approvalId);
                reason = string.Empty;
                return true;
            }
        }

        internal static bool TryRevokeSceneModificationApproval(string approvalId, out string reason)
        {
            if (!IsValidRequestId(approvalId))
            {
                reason = "批准 ID 必须是 N 格式 GUID。";
                return false;
            }

            lock (sceneApprovalLock)
            {
                PurgeExpiredSceneModificationApprovals(DateTimeOffset.UtcNow);
                if (!pendingSceneModificationApprovals.TryGetValue(approvalId,
                        out PendingSceneModificationApproval approval))
                {
                    reason = "已批准场景计划不存在、已过期或已被消费。";
                    return false;
                }
                if (approval.Status != PendingSceneModificationApprovalStatus.Approved)
                {
                    reason = "该场景计划当前不能撤销批准：" + approval.Status;
                    return false;
                }
                if (!TryUpdatePendingApprovalAudit(approval, "RevokedByUser", "editor.user",
                        string.Empty, string.Empty, out reason))
                    return false;
                pendingSceneModificationApprovals.Remove(approvalId);
                reason = string.Empty;
                return true;
            }
        }

        /// <summary>
        /// AIBrain 只读取这个宿主能力投影。它描述 Unity 主线程 MCP/等价桥接能做什么，
        /// 不伪造外部 MCP 已连接；真实连接状态仍由宿主握手证据决定。
        /// </summary>
        internal static List<ESAIBrainCapabilityBinding> CopyMcpCapabilitiesForBrain()
        {
            string status = !initialized ? "Unavailable"
                : !enabled ? "Unauthorized"
                : ShouldListen() ? "Available" : "Suspended";
            return new List<ESAIBrainCapabilityBinding>
            {
                new ESAIBrainCapabilityBinding
                {
                    id = "mcp.unity-editor",
                    kind = "MCP",
                    status = status,
                    displayName = "Unity Editor 主线程桥接",
                    summary = "由受信宿主调用 AI Bridge 的固定 Unity 控制动作；不接受任意脚本、路径或进程参数。",
                    authority = "ESAutomationAiBridge / Unity host",
                    requiresUserAuthorization = true,
                    capabilities = new List<string>
                    {
                        "getUnityCompilationState",
                        "setUnityAutoCompilation",
                        "triggerUnityCompilation",
                        "modifyActiveScene",
                    },
                },
            };
        }

        private static bool TryGetControlActionContract(string action, out ESAutomationAiControlActionContract contract)
        {
            switch (action)
            {
                case "getUnityCompilationState":
                    contract = GetCompilationStateContract;
                    return true;
                case "setUnityAutoCompilation":
                    contract = SetAutoCompilationContract;
                    return true;
                case "triggerUnityCompilation":
                    contract = TriggerCompilationContract;
                    return true;
                case "modifyActiveScene":
                    contract = ModifyActiveSceneContract;
                    return true;
                default:
                    contract = null;
                    return false;
            }
        }

        private static ControlActionAuditScope BeginControlActionAudit(
            string requestId,
            string actorId,
            ESAutomationAiControlActionContract contract,
            JObject payload)
        {
            if (contract == null) throw new ArgumentNullException(nameof(contract));
            ESAutomationPathPolicy.EnsureWorkerDirectory(
                ControlActionAuditDirectory,
                new[] { GetControlActionAuditWriteRoot() });

            string auditPath = Path.Combine(ControlActionAuditDirectory, requestId + ".json");
            if (File.Exists(auditPath))
                throw new InvalidOperationException("该 RequestId 已存在控制动作审计记录，拒绝重放：" + requestId);

            var scope = new ControlActionAuditScope(auditPath, requestId, actorId, contract);
            scope.Record = new JObject
            {
                ["protocolVersion"] = ControlActionAuditVersion,
                ["requestId"] = requestId,
                ["runId"] = string.Empty,
                ["actorId"] = actorId,
                ["action"] = contract.Action,
                ["capability"] = contract.Capability,
                ["requiresHumanApproval"] = contract.RequiresHumanApproval,
                ["inputSha256"] = ComputeJsonSha256(payload),
                ["approvalId"] = string.Empty,
                ["approvalPlanFingerprint"] = string.Empty,
                ["approvalDecisionActor"] = string.Empty,
                ["executionRequestId"] = string.Empty,
                ["approvalInvalidationReason"] = string.Empty,
                ["status"] = "Started",
                ["createdUtc"] = DateTimeOffset.UtcNow.ToString("O"),
                ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
                ["resultSha256"] = string.Empty,
                ["error"] = string.Empty,
            };
            ESManagedFileIO.WriteTextAtomicCreateNew(
                auditPath,
                scope.Record.ToString(Formatting.Indented),
                new UTF8Encoding(false),
                ControlActionAuditDirectory);
            return scope;
        }

        private static void CompleteControlActionAudit(ControlActionAuditScope audit, ESAutomationAiResponse response)
        {
            if (audit == null) throw new ArgumentNullException(nameof(audit));
            if (response == null) throw new ArgumentNullException(nameof(response));

            string status = string.IsNullOrWhiteSpace(audit.FinalStatus) ? response.status : audit.FinalStatus;
            audit.Record["status"] = status;
            audit.Record["approvalId"] = audit.ApprovalId ?? string.Empty;
            audit.Record["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O");
            audit.Record["resultSha256"] = ComputeJsonSha256(new JObject
            {
                ["status"] = response.status ?? string.Empty,
                ["message"] = response.message ?? string.Empty,
                ["runId"] = response.runId ?? string.Empty,
                ["receiptId"] = response.receiptId ?? string.Empty,
                ["data"] = response.data?.DeepClone() ?? new JObject(),
            });
            audit.Record["error"] = string.Equals(response.status, "Completed", StringComparison.Ordinal)
                ? string.Empty
                : response.message ?? string.Empty;
            ESManagedFileIO.WriteTextAtomic(
                audit.AuditPath,
                audit.Record.ToString(Formatting.Indented),
                new UTF8Encoding(false),
                GetControlActionAuditWriteRoot());

            if (audit.PendingSceneModificationApproval != null)
                RegisterPendingSceneModificationApproval(audit.PendingSceneModificationApproval);
        }

        private static ESAutomationAiResponse CreateControlAuditFailureResponse(
            string requestId,
            string action,
            ESAutomationAiResponse original,
            Exception auditException)
        {
            var data = original?.data == null ? new JObject() : (JObject)original.data.DeepClone();
            data["auditCompletionFailed"] = true;
            data["originalStatus"] = original?.status ?? string.Empty;
            return new ESAutomationAiResponse
            {
                protocolVersion = ProtocolVersion,
                requestId = requestId ?? string.Empty,
                action = action ?? string.Empty,
                status = "Failed",
                message = "控制动作的结果审计写入失败；动作可能已发生，不能报告成功。原因：" + auditException.Message,
                data = data,
            };
        }

        private static PendingSceneModificationApproval CreatePendingSceneModificationApproval(
            string actorId,
            ESAutomationSceneModificationPlan plan,
            ControlActionAuditScope audit)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (audit == null) throw new ArgumentNullException(nameof(audit));
            string approvalId = Guid.NewGuid().ToString("N");
            string planFingerprint = ComputeJsonSha256(plan.CreateFingerprintPayload());
            var approval = new PendingSceneModificationApproval(
                approvalId,
                actorId,
                plan,
                planFingerprint,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.Add(SceneModificationApprovalLifetime),
                audit);
            audit.ApprovalId = approvalId;
            audit.Record["approvalPlanFingerprint"] = planFingerprint;
            audit.Record["scenePath"] = plan.ScenePath;
            audit.Record["operationCount"] = plan.Operations.Count;
            audit.Record["saveRequested"] = plan.SaveRequested;
            audit.FinalStatus = "AwaitingUserApproval";
            audit.PendingSceneModificationApproval = approval;
            return approval;
        }

        private static bool CanCreatePendingSceneModificationApproval(out string reason)
        {
            lock (sceneApprovalLock)
            {
                PurgeExpiredSceneModificationApprovals(DateTimeOffset.UtcNow);
                if (pendingSceneModificationApprovals.Count < MaxPendingSceneModificationApprovals)
                {
                    reason = string.Empty;
                    return true;
                }
                reason = "待批准场景计划已达到 " + MaxPendingSceneModificationApprovals
                    + " 条上限；请在自动化中心批准、撤销或等待现有计划过期后再提交。";
                return false;
            }
        }

        private static void RegisterPendingSceneModificationApproval(PendingSceneModificationApproval approval)
        {
            if (approval == null) throw new ArgumentNullException(nameof(approval));
            lock (sceneApprovalLock)
            {
                PurgeExpiredSceneModificationApprovals(DateTimeOffset.UtcNow);
                if (pendingSceneModificationApprovals.ContainsKey(approval.ApprovalId))
                    throw new InvalidOperationException("重复的场景批准 ID：" + approval.ApprovalId);
                pendingSceneModificationApprovals.Add(approval.ApprovalId, approval);
            }
        }

        private static bool TryGetApprovedSceneModificationApproval(
            string approvalId,
            string actorId,
            out PendingSceneModificationApproval approval,
            out string reason)
        {
            lock (sceneApprovalLock)
            {
                PurgeExpiredSceneModificationApprovals(DateTimeOffset.UtcNow);
                if (!pendingSceneModificationApprovals.TryGetValue(approvalId, out approval))
                {
                    reason = "场景批准不存在、已过期、已撤销或已被消费。";
                    return false;
                }
                if (!string.Equals(approval.ActorId, actorId, StringComparison.Ordinal))
                {
                    reason = "approvalId 只允许由创建该场景计划的 actorId 使用。";
                    return false;
                }
                if (approval.Status != PendingSceneModificationApprovalStatus.Approved)
                {
                    reason = approval.Status == PendingSceneModificationApprovalStatus.AwaitingUserApproval
                        ? "场景计划尚未获得用户批准。"
                        : "场景计划当前不能执行：" + approval.Status;
                    return false;
                }
                reason = string.Empty;
                return true;
            }
        }

        private static bool TryConsumeApprovedSceneModificationApproval(
            PendingSceneModificationApproval approval,
            string executionRequestId,
            out string reason)
        {
            if (approval == null)
            {
                reason = "场景批准不存在。";
                return false;
            }

            lock (sceneApprovalLock)
            {
                PurgeExpiredSceneModificationApprovals(DateTimeOffset.UtcNow);
                if (!pendingSceneModificationApprovals.TryGetValue(approval.ApprovalId, out PendingSceneModificationApproval current)
                    || !ReferenceEquals(current, approval))
                {
                    reason = "场景批准已失效、撤销或被其他请求消费。";
                    return false;
                }
                if (current.Status != PendingSceneModificationApprovalStatus.Approved)
                {
                    reason = "场景批准当前不能消费：" + current.Status;
                    return false;
                }
                if (!TryUpdatePendingApprovalAudit(current, "Consumed", "ai.execution", executionRequestId,
                        string.Empty, out reason))
                    return false;
                pendingSceneModificationApprovals.Remove(current.ApprovalId);
                reason = string.Empty;
                return true;
            }
        }

        private static bool TryUpdatePendingApprovalAudit(
            PendingSceneModificationApproval approval,
            string status,
            string decisionActor,
            string executionRequestId,
            string invalidationReason,
            out string reason)
        {
            try
            {
                if (!File.Exists(approval.Audit.AuditPath))
                    throw new FileNotFoundException("场景批准的初始审计记录不存在。", approval.Audit.AuditPath);
                JObject updatedRecord = (JObject)(approval.Audit.Record?.DeepClone() ?? new JObject());
                updatedRecord["status"] = status;
                updatedRecord["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O");
                updatedRecord["approvalDecisionActor"] = decisionActor ?? string.Empty;
                updatedRecord["executionRequestId"] = executionRequestId ?? string.Empty;
                updatedRecord["approvalInvalidationReason"] = invalidationReason ?? string.Empty;
                updatedRecord["error"] = string.Empty;
                ESManagedFileIO.WriteTextAtomic(
                    approval.Audit.AuditPath,
                    updatedRecord.ToString(Formatting.Indented),
                    new UTF8Encoding(false),
                    GetControlActionAuditWriteRoot());
                approval.Audit.Record = updatedRecord;
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                reason = "无法更新场景批准审计，拒绝改变批准状态：" + exception.Message;
                return false;
            }
        }

        private static void PurgeExpiredSceneModificationApprovals(DateTimeOffset nowUtc)
        {
            var expiredIds = new List<string>();
            foreach (KeyValuePair<string, PendingSceneModificationApproval> pair in pendingSceneModificationApprovals)
            {
                if (pair.Value.ExpiresAtUtc > nowUtc) continue;
                if (!TryUpdatePendingApprovalAudit(pair.Value, "Expired", "system", string.Empty,
                        string.Empty, out string reason))
                    Debug.LogWarning("[ESAutomation] 场景批准已过期，但无法更新其审计状态：" + reason);
                expiredIds.Add(pair.Key);
            }
            foreach (string approvalId in expiredIds) pendingSceneModificationApprovals.Remove(approvalId);
        }

        private static void ClearPendingSceneModificationApprovals(string reason)
        {
            lock (sceneApprovalLock)
            {
                foreach (PendingSceneModificationApproval approval in pendingSceneModificationApprovals.Values)
                {
                    if (!TryUpdatePendingApprovalAudit(approval, "Invalidated", "system", string.Empty,
                            reason, out string auditReason))
                        Debug.LogWarning("[ESAutomation] 无法记录场景批准失效：" + auditReason);
                }
                pendingSceneModificationApprovals.Clear();
            }
        }

        private static string ComputeJsonSha256(JToken token)
        {
            string text = token?.ToString(Formatting.None) ?? string.Empty;
            byte[] bytes = StrictUtf8.GetBytes(text);
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) builder.Append(value.ToString("x2"));
                return builder.ToString();
            }
        }

        private static string GetControlActionAuditWriteRoot()
        {
#if UNITY_INCLUDE_TESTS
            lock (testAuthorizationLock)
            {
                if (!string.IsNullOrWhiteSpace(testControlActionAuditDirectory))
                    return ESAutomationPathPolicy.TempRoot;
            }
#endif
            return ESAutomationPathPolicy.RunsRoot;
        }

        private sealed class ESAutomationAiControlActionContract
        {
            public ESAutomationAiControlActionContract(string action, string capability, bool requiresHumanApproval)
            {
                Action = action ?? string.Empty;
                Capability = capability ?? string.Empty;
                RequiresHumanApproval = requiresHumanApproval;
            }

            public string Action { get; }
            public string Capability { get; }
            public bool RequiresHumanApproval { get; }
        }

        private sealed class ControlActionAuditScope
        {
            public ControlActionAuditScope(
                string auditPath,
                string requestId,
                string actorId,
                ESAutomationAiControlActionContract contract)
            {
                AuditPath = auditPath ?? string.Empty;
                RequestId = requestId ?? string.Empty;
                ActorId = actorId ?? string.Empty;
                Contract = contract ?? throw new ArgumentNullException(nameof(contract));
            }

            public string AuditPath { get; }
            public string RequestId { get; }
            public string ActorId { get; }
            public ESAutomationAiControlActionContract Contract { get; }
            public JObject Record { get; set; }
            public string ApprovalId { get; set; } = string.Empty;
            public string FinalStatus { get; set; } = string.Empty;
            public PendingSceneModificationApproval PendingSceneModificationApproval { get; set; }
        }

        private enum PendingSceneModificationApprovalStatus
        {
            AwaitingUserApproval,
            Approved,
        }

        private sealed class PendingSceneModificationApproval
        {
            public PendingSceneModificationApproval(
                string approvalId,
                string actorId,
                ESAutomationSceneModificationPlan plan,
                string planFingerprint,
                DateTimeOffset createdAtUtc,
                DateTimeOffset expiresAtUtc,
                ControlActionAuditScope audit)
            {
                ApprovalId = approvalId ?? string.Empty;
                ActorId = actorId ?? string.Empty;
                Plan = plan ?? throw new ArgumentNullException(nameof(plan));
                PlanFingerprint = planFingerprint ?? string.Empty;
                CreatedAtUtc = createdAtUtc;
                ExpiresAtUtc = expiresAtUtc;
                Audit = audit ?? throw new ArgumentNullException(nameof(audit));
            }

            public string ApprovalId { get; }
            public string ActorId { get; }
            public ESAutomationSceneModificationPlan Plan { get; }
            public string PlanFingerprint { get; }
            public DateTimeOffset CreatedAtUtc { get; }
            public DateTimeOffset ExpiresAtUtc { get; }
            public ControlActionAuditScope Audit { get; }
            public PendingSceneModificationApprovalStatus Status { get; set; } = PendingSceneModificationApprovalStatus.AwaitingUserApproval;

            public ESAutomationSceneModificationApprovalInfo CreateInfo()
            {
                return new ESAutomationSceneModificationApprovalInfo(
                    ApprovalId,
                    ActorId,
                    Status.ToString(),
                    CreatedAtUtc,
                    ExpiresAtUtc,
                    PlanFingerprint,
                    Plan.CreateResponseData(true, false));
            }
        }

#if UNITY_INCLUDE_TESTS
        private sealed class TestAuthorizationScope : IDisposable
        {
            private readonly bool? previous;
            private bool disposed;

            public TestAuthorizationScope(bool? previous)
            {
                this.previous = previous;
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                lock (testAuthorizationLock) testAuthorizationOverride = previous;
            }
        }

        private sealed class TestControlActionAuditScope : IDisposable
        {
            private readonly string directory;
            private readonly string previousDirectory;
            private bool disposed;

            public TestControlActionAuditScope(string directory, string previousDirectory)
            {
                this.directory = directory ?? string.Empty;
                this.previousDirectory = previousDirectory ?? string.Empty;
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                lock (testAuthorizationLock) testControlActionAuditDirectory = previousDirectory;
                if (string.IsNullOrWhiteSpace(directory)) return;
                try
                {
                    ESManagedFileIO.DeleteDirectory(directory, ESAutomationPathPolicy.TempRoot);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[ESAutomation] 无法清理测试控制动作审计目录：" + exception.Message);
                }
            }
        }
#endif

        private static void EnsureDirectories()
        {
            EnsureBridgePath(RootDirectory, ESAutomationPathPolicy.ProjectRoot);
            ESAutomationPathPolicy.EnsureWorkerDirectory(InboxDirectory, new[] { RootDirectory });
            ESAutomationPathPolicy.EnsureWorkerDirectory(ProcessingDirectory, new[] { RootDirectory });
            ESAutomationPathPolicy.EnsureWorkerDirectory(ArchiveDirectory, new[] { RootDirectory });
            ESAutomationPathPolicy.EnsureWorkerDirectory(ResponseDirectory, new[] { RootDirectory });
        }

        private static void EnsureBridgePath(string path, string allowedRoot)
        {
            if (!ESAutomationPathPolicy.IsWithin(path, new[] { allowedRoot }))
                throw new UnauthorizedAccessException("AI Bridge 路径越出允许根目录：" + path);
        }

        private static void WriteResponse(string path, ESAutomationAiResponse response)
        {
            EnsureBridgePath(path, ResponseDirectory);
            if (File.Exists(path)) throw new IOException("AI Bridge 响应已存在，拒绝覆盖：" + path);
            ESManagedFileIO.WriteTextAtomicCreateNew(
                path,
                JsonConvert.SerializeObject(response, Formatting.Indented),
                new UTF8Encoding(false),
                RootDirectory);
        }

        // FileSystemWatcher 的 Created/Renamed 回调在线程池执行，不能通过 IsEnabled
        // 触发 Editor 主线程门禁；enabled 已由主线程写入且声明为 volatile，可安全读取。
        private static bool ShouldListen()
            => enabled && (!playModeAutoSuspended || trustedPlayModeListeningOverride);

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                case PlayModeStateChange.EnteredPlayMode:
                case PlayModeStateChange.ExitingPlayMode:
                    playModeAutoSuspended = true;
                    trustedPlayModeListeningOverride = false;
                    Stop();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    playModeAutoSuspended = false;
                    trustedPlayModeListeningOverride = false;
                    EnsureStartedIfEnabled();
                    break;
            }
        }

        private static void RequireExactProperties(JObject value, IEnumerable<string> fields, string context)
        {
            RequireExactProperties(value, fields, Array.Empty<string>(), context);
        }

        private static void RequireExactProperties(JObject value, IEnumerable<string> requiredFields,
            IEnumerable<string> optionalFields, string context)
        {
            var required = new HashSet<string>(requiredFields, StringComparer.Ordinal);
            var optional = new HashSet<string>(optionalFields, StringComparer.Ordinal);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            foreach (JProperty property in value.Properties()) actual.Add(property.Name);
            var allowed = new HashSet<string>(required, StringComparer.Ordinal);
            allowed.UnionWith(optional);
            bool valid = required.IsSubsetOf(actual) && actual.IsSubsetOf(allowed);
            if (valid) return;
            var details = new List<string>();
            foreach (string field in required) if (!actual.Contains(field)) details.Add("缺少 " + field);
            foreach (string field in actual)
                if (!required.Contains(field) && !optional.Contains(field)) details.Add("未注册 " + field);
            throw new InvalidOperationException(context + " 字段不匹配：" + string.Join("；", details.ToArray()));
        }

        private static string ReadString(JObject value, string field, bool allowEmpty = false)
        {
            JToken token = value[field];
            if (token == null || token.Type != JTokenType.String || !allowEmpty && string.IsNullOrWhiteSpace((string)token))
                throw new InvalidOperationException(field + " 必须是" + (allowEmpty ? "字符串" : "非空字符串") + "。");
            return (string)token;
        }

        private static List<string> ReadStringArray(JObject value, string field)
        {
            JToken token = value[field];
            if (token == null || token.Type != JTokenType.Array)
                throw new InvalidOperationException(field + " 必须是字符串数组。");
            var result = new List<string>();
            foreach (JToken item in token)
            {
                if (item == null || item.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)item))
                    throw new InvalidOperationException(field + " 只能包含非空字符串。");
                result.Add((string)item);
            }
            return result;
        }

        private static int ReadInteger(JObject value, string field)
        {
            JToken token = value[field];
            if (token == null || token.Type != JTokenType.Integer) throw new InvalidOperationException(field + " 必须是整数。");
            return (int)token;
        }

        private static string ReadRequestId(JObject value, string field)
        {
            string requestId = ReadString(value, field).ToLowerInvariant();
            if (!IsValidRequestId(requestId)) throw new InvalidOperationException(field + " 必须是 N 格式 GUID。");
            return requestId;
        }

        private static string ReadSha256(JObject value, string field)
        {
            string hash = ReadString(value, field);
            if (!ESAutomationWorkerRegistration.IsSha256(hash)) throw new InvalidOperationException(field + " 必须是 64 位 SHA-256。");
            return hash.ToLowerInvariant();
        }

        private static bool IsValidRequestId(string value) => Guid.TryParseExact(value, "N", out _);
    }

    /// <summary>自动化中心使用的待批准场景计划只读快照，不包含任何活的 Unity 对象引用。</summary>
    internal sealed class ESAutomationSceneModificationApprovalInfo
    {
        private readonly JObject planData;

        internal ESAutomationSceneModificationApprovalInfo(
            string approvalId,
            string actorId,
            string status,
            DateTimeOffset createdAtUtc,
            DateTimeOffset expiresAtUtc,
            string planFingerprint,
            JObject planData)
        {
            ApprovalId = approvalId ?? string.Empty;
            ActorId = actorId ?? string.Empty;
            Status = status ?? string.Empty;
            CreatedAtUtc = createdAtUtc;
            ExpiresAtUtc = expiresAtUtc;
            PlanFingerprint = planFingerprint ?? string.Empty;
            this.planData = (JObject)(planData?.DeepClone() ?? new JObject());
        }

        internal string ApprovalId { get; }
        internal string ActorId { get; }
        internal string Status { get; }
        internal DateTimeOffset CreatedAtUtc { get; }
        internal DateTimeOffset ExpiresAtUtc { get; }
        internal string PlanFingerprint { get; }
        internal JObject CreatePlanData() => (JObject)planData.DeepClone();
    }

    internal sealed class ESAutomationAiBridgeInitializer : EditorInvoker_Level0
    {
        public override void InitInvoke()
        {
            ESAutomationAiBridge.InitializeForEditorMainThread();
        }
    }

    [Serializable]
    public sealed class ESAutomationAiResponse
    {
        public int protocolVersion = 1;
        public string requestId = string.Empty;
        public string action = string.Empty;
        public string status = "Rejected";
        public string message = string.Empty;
        public string runId = string.Empty;
        public string receiptId = string.Empty;
        public JObject data = new JObject();

        public static ESAutomationAiResponse Completed(string requestId, string action, string message, string runId, JObject data)
            => new ESAutomationAiResponse { requestId = requestId ?? string.Empty, action = action ?? string.Empty, status = "Completed", message = message ?? string.Empty, runId = runId ?? string.Empty, data = data ?? new JObject() };

        public static ESAutomationAiResponse Rejected(string requestId, string action, string message)
            => new ESAutomationAiResponse { requestId = requestId ?? string.Empty, action = action ?? string.Empty, status = "Rejected", message = message ?? string.Empty };

        public static ESAutomationAiResponse Failed(string requestId, string action, string message)
            => new ESAutomationAiResponse { requestId = requestId ?? string.Empty, action = action ?? string.Empty, status = "Failed", message = message ?? string.Empty };
    }
}
