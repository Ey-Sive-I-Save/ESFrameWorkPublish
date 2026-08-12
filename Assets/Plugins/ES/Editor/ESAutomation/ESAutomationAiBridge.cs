using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

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
        private static readonly Regex RequestFileNamePattern = new Regex("^[a-fA-F0-9]{32}\\.request\\.json$", RegexOptions.Compiled);
        private static readonly Regex ActorIdPattern = new Regex("^[A-Za-z0-9._:-]{1,128}$", RegexOptions.Compiled);
        private static readonly ConcurrentQueue<string> queuedPaths = new ConcurrentQueue<string>();
        private static readonly HashSet<string> queuedPathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object queuedPathLock = new object();
        private static FileSystemWatcher watcher;
        private static bool updateSubscribed;
        private static volatile bool enabled;
        private static volatile bool rescanRequested;
        // 这是本次 Editor 会话的运行态门禁，绝不写回“用户已授权”的持久化设置。
        // PlayMode 默认暂停收件箱；受信的 Unity 主线程控制通道可以仅在本次 Play 中显式恢复。
        private static volatile bool playModeAutoSuspended;
        private static volatile bool trustedPlayModeListeningOverride;

        private static bool initialized;

        internal static void InitializeForEditor()
        {
            if (initialized) return;
            initialized = true;
            enabled = EditorPrefs.GetBool(EnablePreferenceKey, false);
            playModeAutoSuspended = EditorApplication.isPlayingOrWillChangePlaymode;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += EnsureStartedIfEnabled;
        }

        public static string RootDirectory => Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "AI");
        public static string InboxDirectory => Path.Combine(RootDirectory, "Inbox");
        public static string ProcessingDirectory => Path.Combine(RootDirectory, "Processing");
        public static string ArchiveDirectory => Path.Combine(RootDirectory, "Archive");
        public static string ResponseDirectory => Path.Combine(RootDirectory, "Responses");

        public static bool IsEnabled
        {
            get
            {
                InitializeForEditor();
                return enabled;
            }
            set
            {
                InitializeForEditor();
                enabled = value;
                EditorPrefs.SetBool(EnablePreferenceKey, value);
                if (value) EnsureStartedIfEnabled();
                else Stop();
            }
        }

        /// <summary>用户在本机 Editor 上作出的持久化授权。它与当前是否正在监听收件箱是两回事。</summary>
        public static bool IsUserAuthorized => IsEnabled;

        /// <summary>进入或退出 PlayMode 期间，收件箱是否按默认策略被临时暂停。</summary>
    public static bool IsAutoSuspendedForPlayMode
    {
        get
        {
            InitializeForEditor();
            return playModeAutoSuspended && !trustedPlayModeListeningOverride;
        }
    }

        /// <summary>当前是否确实在监听 Inbox。仅用于 Center 状态显示和受信宿主诊断。</summary>
    public static bool IsListening
    {
        get
        {
            InitializeForEditor();
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
            InitializeForEditor();
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
            InitializeForEditor();
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
            string requestId = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(requestPath));
            try
            {
                EnsureDirectories();
                EnsureBridgePath(requestPath, InboxDirectory);
                if (!File.Exists(requestPath)) return;
                if (new FileInfo(requestPath).Length > MaxRequestBytes)
                    throw new InvalidOperationException("AI 请求超过 128 KiB 限制。");

                // Inbox 的最终扩展名只能通过“临时文件写完后原子改名”产生。
                // FileSystemWatcher 可能在写入者关闭文件前收到事件，因此这里必须再次
                // 确认文件尺寸/时间戳稳定且 JSON 可解析；不稳定时留在 Inbox，等待下一轮重试，
                // 不能把半写请求移动到 Processing 后再伪装成业务拒绝。
                if (!TryReadStableRequest(requestPath, out string requestJson))
                {
                    QueuePath(requestPath);
                    return;
                }

                string processingPath = Path.Combine(ProcessingDirectory, Path.GetFileName(requestPath));
                EnsureBridgePath(processingPath, ProcessingDirectory);
                if (File.Exists(processingPath)) throw new IOException("AI 请求已在处理队列中：" + requestId);
                File.Move(requestPath, processingPath);

                string responsePath = Path.Combine(ResponseDirectory, requestId + ".response.json");
                ESAutomationAiResponse response;
                if (File.Exists(responsePath))
                    response = ESAutomationAiResponse.Rejected(requestId, string.Empty, "该 RequestId 已有响应，拒绝重复执行。");
                else
                    response = HandleRequest(requestJson, requestId);
                WriteResponse(responsePath, response);

                string archivePath = Path.Combine(ArchiveDirectory, Path.GetFileName(processingPath));
                if (!File.Exists(archivePath)) File.Move(processingPath, archivePath);
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
        }

        private static bool TryReadStableRequest(string path, out string requestJson)
        {
            requestJson = null;
            try
            {
                FileInfo before = new FileInfo(path);
                if (!before.Exists || before.Length <= 0 || before.Length > MaxRequestBytes)
                    return false;

                string content = File.ReadAllText(path, Encoding.UTF8);
                FileInfo after = new FileInfo(path);
                if (!after.Exists || before.Length != after.Length || before.LastWriteTimeUtc != after.LastWriteTimeUtc)
                    return false;

                // 完整 JSON 交给后续动作校验；稳定但格式错误的文件也必须进入
                // 正常拒绝/归档链，不能永远滞留 Inbox。
                try
                {
                    JObject.Parse(content);
                }
                catch (JsonException)
                {
                    requestJson = content;
                    return true;
                }
                requestJson = content;
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

        private static ESAutomationAiResponse HandleRequest(string requestJson, string expectedRequestId)
        {
            string requestId = expectedRequestId ?? string.Empty;
            string action = string.Empty;
            try
            {
                JObject root = JObject.Parse(requestJson ?? string.Empty);
                RequireExactProperties(root, new[] { "protocolVersion", "requestId", "actorId", "action", "payload" }, "AI 请求");
                if (ReadInteger(root, "protocolVersion") != ProtocolVersion) throw new InvalidOperationException("不支持的 AI 请求协议版本。");
                requestId = ReadRequestId(root, "requestId");
                if (!string.IsNullOrWhiteSpace(expectedRequestId) && !string.Equals(expectedRequestId, requestId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("请求文件名与 requestId 不一致。");
                string actorId = ReadString(root, "actorId");
                if (!ActorIdPattern.IsMatch(actorId)) throw new InvalidOperationException("actorId 必须是 1–128 位安全标识符，不能包含空格或路径字符。");
                action = ReadString(root, "action");
                if (root["payload"].Type != JTokenType.Object) throw new InvalidOperationException("payload 必须是对象。");
                JObject payload = (JObject)root["payload"];

                switch (action)
                {
                    case "listTasks":
                        RequireExactProperties(payload, Array.Empty<string>(), "listTasks payload");
                        return ESAutomationAiResponse.Completed(requestId, action, "已返回 AI 可调用任务与已注册内容提案入口。", string.Empty, new JObject
                        {
                            ["tasks"] = JArray.FromObject(ESAutomationFacade.CopyDescriptors()),
                            ["contentTypes"] = JArray.FromObject(ESAutomationContentIngress.CopyDescriptors()),
                        });
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
                    case "getUnityCompilationState":
                        RequireExactProperties(payload, Array.Empty<string>(), "getUnityCompilationState payload");
                        return ESAutomationAiResponse.Completed(requestId, action, "已返回 Unity 编译控制状态。", string.Empty, ESAutomationUnityEditorControl.GetCompilationState());
                    case "setUnityAutoCompilation":
                        return HandleSetUnityAutoCompilation(requestId, action, payload);
                    case "triggerUnityCompilation":
                        return HandleTriggerUnityCompilation(requestId, action, payload);
                    case "modifyActiveScene":
                        return HandleModifyActiveScene(requestId, action, payload);
                    default:
                        return ESAutomationAiResponse.Rejected(requestId, action, "未注册的 AI 自动化动作：" + action);
                }
            }
            catch (Exception exception)
            {
                return ESAutomationAiResponse.Rejected(requestId, action, exception.Message);
            }
        }

        private static ESAutomationAiResponse HandleRunTask(string requestId, string action, string actorId, JObject payload)
        {
            RequireExactProperties(payload, new[] { "taskId", "taskVersion", "preset", "input" }, "runTask payload");
            if (payload["input"].Type != JTokenType.Object) throw new InvalidOperationException("runTask.input 必须是对象。");
            ESAutomationTaskInvocationResult result = ESAutomationFacade.RunTask(new ESAutomationTaskInvocation
            {
                taskId = ReadString(payload, "taskId"),
                taskVersion = ReadInteger(payload, "taskVersion"),
                preset = ReadString(payload, "preset", allowEmpty: true),
                input = (JObject)payload["input"],
                fromAi = true,
                actorId = actorId,
            });
            return FromTaskResult(requestId, action, result);
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

        private static ESAutomationAiResponse HandleSetUnityAutoCompilation(string requestId, string action, JObject payload)
        {
            RequireExactProperties(payload, new[] { "enabled" }, "setUnityAutoCompilation payload");
            if (payload["enabled"].Type != JTokenType.Boolean) throw new InvalidOperationException("enabled 必须是布尔值。");
            JObject data = ESAutomationUnityEditorControl.SetAutoCompilation((bool)payload["enabled"]);
            return ESAutomationAiResponse.Completed(requestId, action, "已更新 Unity 自动编译策略。", string.Empty, data);
        }

        private static ESAutomationAiResponse HandleTriggerUnityCompilation(string requestId, string action, JObject payload)
        {
            RequireExactProperties(payload, new[] { "forceRefresh" }, "triggerUnityCompilation payload");
            if (payload["forceRefresh"].Type != JTokenType.Boolean) throw new InvalidOperationException("forceRefresh 必须是布尔值。");
            JObject data = ESAutomationUnityEditorControl.TriggerCompilation((bool)payload["forceRefresh"]);
            return ESAutomationAiResponse.Completed(requestId, action, "已请求 Unity 脚本编译。", string.Empty, data);
        }

        private static ESAutomationAiResponse HandleModifyActiveScene(string requestId, string action, JObject payload)
        {
            RequireExactProperties(payload, new[] { "scenePath", "operations", "save", "dryRun" }, "modifyActiveScene payload");
            if (payload["operations"].Type != JTokenType.Array) throw new InvalidOperationException("operations 必须是数组。");
            if (payload["save"].Type != JTokenType.Boolean) throw new InvalidOperationException("save 必须是布尔值。");
            if (payload["dryRun"].Type != JTokenType.Boolean) throw new InvalidOperationException("dryRun 必须是布尔值。");
            JObject data = ESAutomationUnityEditorControl.ModifyActiveScene(
                payload["scenePath"].Type == JTokenType.String ? (string)payload["scenePath"] : string.Empty,
                (JArray)payload["operations"],
                (bool)payload["save"],
                (bool)payload["dryRun"]);
            string receiptId = Guid.NewGuid().ToString("N");
            return ESAutomationAiResponse.Completed(requestId, action, "已完成受管 Active Scene 操作。", receiptId, data);
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
            string temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(response, Formatting.Indented), new UTF8Encoding(false));
                File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private static bool ShouldListen()
            => IsEnabled && (!playModeAutoSuspended || trustedPlayModeListeningOverride);

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
            var expected = new HashSet<string>(fields, StringComparer.Ordinal);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            foreach (JProperty property in value.Properties()) actual.Add(property.Name);
            if (actual.SetEquals(expected)) return;
            var details = new List<string>();
            foreach (string field in expected) if (!actual.Contains(field)) details.Add("缺少 " + field);
            foreach (string field in actual) if (!expected.Contains(field)) details.Add("未注册 " + field);
            throw new InvalidOperationException(context + " 字段不匹配：" + string.Join("；", details.ToArray()));
        }

        private static string ReadString(JObject value, string field, bool allowEmpty = false)
        {
            JToken token = value[field];
            if (token == null || token.Type != JTokenType.String || !allowEmpty && string.IsNullOrWhiteSpace((string)token))
                throw new InvalidOperationException(field + " 必须是" + (allowEmpty ? "字符串" : "非空字符串") + "。");
            return (string)token;
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

    internal sealed class ESAutomationAiBridgeInitializer : EditorInvoker_Level0
    {
        public override void InitInvoke()
        {
            ESAutomationAiBridge.InitializeForEditor();
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
