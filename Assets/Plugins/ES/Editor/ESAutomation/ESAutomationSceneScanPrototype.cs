using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using EditorUtility = ES.ESDesignUtility.SafeEditor;

namespace ES
{
    /// <summary>
    /// 首个受管 Python 原型：C# 扫描当前已加载场景，Python 只分析快照并返回报告。
    /// 交互采用“检查点 -> 进程退出 -> C# 表单 -> 新进程继续”，不保留等待 Unity 的 Python 进程。
    /// </summary>
    public static class ESAutomationSceneScanPrototype
    {
        private const int ProtocolVersion = 1;
        private const string TaskId = "es.scene.scan";
        private const int TaskVersion = 1;
        private const string WorkerType = "Python";
        private const string WorkerId = "es.scene.scan.python";
        private const string WorkerVersion = "0.1.0";
        private const string StepId = "scene-scan.report-options";
        private const string OptionsSchemaHash = "4bbaa61e9bf8a2e2664d3b9cf98944711aa26d5c714e911044298193f08a14cb";
        private const string WorkerEntrypointHash = "fdfcd66352572015c05f69380ba0c12403b5917b06e29db333031ca133557d19";
        private const int WorkerTimeoutSeconds = 120;
        private const long MaxPromotedReportBytes = 16L * 1024L * 1024L;

        // Center、ESAdvancedDialog 与 AI Bridge 共用这份预注册描述；Worker 仍以 OptionsSchemaHash 做最终校验。
        private static readonly ESAutomationInputSchemaDescriptor ReportOptionsInputSchema = new ESAutomationInputSchemaDescriptor
        {
            stepId = StepId,
            schemaHash = OptionsSchemaHash,
            title = "场景扫描报告选项",
            summary = "Python 只分析 Unity 已导出的场景快照；这些选项不会修改场景、Asset 或发布物。",
            fields = new List<ESAutomationInputFieldDescriptor>
            {
                new ESAutomationInputFieldDescriptor
                {
                    fieldId = "includeInactive",
                    label = "包含未激活对象",
                    description = "关闭时排除 activeInHierarchy 为 false 的对象。",
                    valueType = "Boolean",
                    defaultValue = false,
                },
                new ESAutomationInputFieldDescriptor
                {
                    fieldId = "detailMode",
                    label = "报告粒度",
                    description = "summary 仅输出聚合统计；detailed 额外列出对象，最多 5000 项。",
                    valueType = "Choice",
                    defaultValue = "summary",
                    choices = new List<ESAutomationInputChoiceDescriptor>
                    {
                        new ESAutomationInputChoiceDescriptor { code = "summary", label = "摘要（聚合统计）" },
                        new ESAutomationInputChoiceDescriptor { code = "detailed", label = "详细（对象清单）" },
                    },
                },
                new ESAutomationInputFieldDescriptor
                {
                    fieldId = "topComponentCount",
                    label = "高频组件数量",
                    description = "报告中显示的组件类型 Top 数量。",
                    valueType = "Integer",
                    defaultValue = 10,
                    minimumInteger = 1,
                    maximumInteger = 50,
                },
            },
        };

        private static readonly Dictionary<string, RunningOperation> runningOperations = new Dictionary<string, RunningOperation>(StringComparer.Ordinal);
        private static bool updateSubscribed;

        private static bool initialized;

        internal static void InitializeForEditor()
        {
            if (initialized) return;
            initialized = true;
            try
            {
                // 注册与运行环境解耦：AI 必须能够发现任务，并在调用时稳定获得 EnvironmentUnavailable。
                // Worker/Schema 指纹和 Python 环境都只在实际执行前验证，不能因启动机缺 Python 而隐藏 Task。
                RegisterTaskAndAdapter();
            }
            catch (Exception exception)
            {
                initialized = false;
                Debug.LogError("[ESAutomation] 场景扫描原型未注册：" + exception.Message);
            }
        }

        [MenuItem(MenuItemPathDefine.AUTOMATION_CENTER_PATH + "扫描当前场景（Python 原型）", false, 210)]
        internal static void StartSceneScan()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("场景扫描", "请在非 PlayMode 下扫描当前场景。原型只导出编辑器中的瞬时场景快照。", "知道了");
                return;
            }

            try
            {
                if (!ESAutomationSceneScanPythonAdapter.TryPrepareRuntime(out _, out string environmentReason))
                    throw new InvalidOperationException(environmentReason);
                EnsurePinnedContent();
                RegisterTaskAndAdapter();
                if (runningOperations.Count > 0)
                {
                    EditorUtility.DisplayDialog("场景扫描", "已有场景扫描 Worker 正在运行；请等待其到达检查点或结束。", "知道了");
                    return;
                }

                Scene scene = SceneManager.GetActiveScene();
                if (!scene.IsValid() || !scene.isLoaded)
                    throw new InvalidOperationException("当前没有可扫描的已加载 Active Scene。");

                ESAutomationSceneScanSession session = CreateSession(scene);
                SaveSession(session);
                StartWorkerStage(session);
                ShowNotification("场景扫描已导出快照，正在请求报告选项…");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("场景扫描无法启动", exception.Message, "关闭");
            }
        }

        [MenuItem(MenuItemPathDefine.AUTOMATION_CENTER_PATH + "继续待输入的场景扫描", false, 211)]
        internal static void ResumePendingSceneScan()
        {
            try
            {
                List<ESAutomationSceneScanSession> sessions = FindRecoverableSessions();
                if (sessions.Count == 0)
                {
                    EditorUtility.DisplayDialog("场景扫描", "没有可恢复的场景扫描检查点。", "关闭");
                    return;
                }
                if (sessions.Count > 1)
                {
                    EditorUtility.DisplayDialog("场景扫描", "发现多个可恢复检查点。为避免把输入交给错误 RunId，本原型不自动选择；请保留一个检查点后重试。", "关闭");
                    return;
                }

                ESAutomationSceneScanSession session = sessions[0];
                if (session.phase == "Running")
                {
                    ESAutomationStageResult result = ReadStageResult(session.stageResultPath);
                    HandleStageResult(session, result, result.exitCode, true);
                    return;
                }
                if (session.phase == "AwaitingInput")
                {
                    OpenOptionsDialog(session);
                    return;
                }
                throw new InvalidOperationException("检查点状态不可恢复：" + session.phase);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("场景扫描恢复失败", exception.Message, "关闭");
            }
        }

        private static void RegisterTaskAndAdapter()
        {
            if (!ESAutomationTaskRegistry.TryGet(TaskId, TaskVersion, out ESAutomationTaskContract existingContract))
            {
                ESAutomationTaskRegistry.Register(new ESAutomationTaskContract
                {
                    protocolVersion = ProtocolVersion,
                    taskId = TaskId,
                    version = TaskVersion,
                    worker = new ESAutomationWorkerRegistration
                    {
                        type = WorkerType,
                        workerId = WorkerId,
                        version = WorkerVersion,
                        entrypointHash = WorkerEntrypointHash,
                        enabled = true,
                    },
                    inputs = new List<string> { "scene-snapshot.json", "input-response.json" },
                    inputSchemaHash = OptionsSchemaHash,
                    readRoots = new List<string> { "ES/Automation/Temp" },
                    writeRoots = new List<string> { "ES/Automation/Temp" },
                    capabilities = new List<string> { "ReadArtifacts", "WriteTemp" },
                    timeoutSeconds = WorkerTimeoutSeconds,
                    supportsDryRun = true,
                    supportsRetry = false,
                    outputs = new List<string> { "scene-scan.json", "scene-scan.md", "result.json" },
                    acceptanceCriteria = new ESAutomationAcceptanceCriteria
                    {
                        freshnessPolicy = new ESAutomationFreshnessPolicy { maxAgeHours = 168, requireSourceHash = true, allowRuntimeNotRun = true },
                        criteria = new List<ESAutomationAcceptanceCriterion>
                        {
                            new ESAutomationAcceptanceCriterion
                            {
                                criterionId = "scene-scan.report-json",
                                verifierId = "es.scene.scan.promoted-output-hash",
                                description = "Promoted JSON report hash is fresh and bound to this run.",
                            },
                            new ESAutomationAcceptanceCriterion
                            {
                                criterionId = "scene-scan.report-markdown",
                                verifierId = "es.scene.scan.promoted-output-hash",
                                description = "Promoted Markdown report hash is fresh and bound to this run.",
                            },
                        },
                    },
                    performanceBudget = new ESAutomationPerformanceBudget
                    {
                        maxDurationSeconds = WorkerTimeoutSeconds,
                        maxOutputBytes = MaxPromotedReportBytes,
                        maxRetryCount = 0,
                        maxFindingCount = 5000,
                    },
                });
            }
            else
            {
                if (existingContract.worker == null || existingContract.worker.type != WorkerType || existingContract.worker.workerId != WorkerId || existingContract.worker.version != WorkerVersion || !HashEquals(existingContract.worker.entrypointHash, WorkerEntrypointHash))
                    throw new InvalidOperationException("已有同 TaskId 的场景扫描 Contract 与受信身份不一致。");
                if (!existingContract.worker.enabled)
                    throw new InvalidOperationException("已有同 TaskId 的场景扫描 Contract 未被本机 C# 注册表启用。");
                if (existingContract.acceptanceCriteria == null)
                {
                    existingContract.acceptanceCriteria = new ESAutomationAcceptanceCriteria
                    {
                        criteria = new List<ESAutomationAcceptanceCriterion>
                        {
                            new ESAutomationAcceptanceCriterion { criterionId = "scene-scan.report-json", verifierId = "es.scene.scan.promoted-output-hash" },
                            new ESAutomationAcceptanceCriterion { criterionId = "scene-scan.report-markdown", verifierId = "es.scene.scan.promoted-output-hash" },
                        },
                    };
                }
                existingContract.acceptanceCriteria.Validate();
                if (existingContract.performanceBudget == null)
                {
                    existingContract.performanceBudget = new ESAutomationPerformanceBudget
                    {
                        maxDurationSeconds = WorkerTimeoutSeconds,
                        maxOutputBytes = MaxPromotedReportBytes,
                        maxRetryCount = 0,
                        maxFindingCount = 5000,
                    };
                }
                existingContract.performanceBudget.Validate();
            }
            if (!ESAutomationProcessRunner.IsAdapterRegistered(WorkerType, WorkerId))
                ESAutomationProcessRunner.RegisterAdapter(new ESAutomationSceneScanPythonAdapter());
            if (!ESAutomationFacade.TryGetDescriptor(TaskId, TaskVersion, out _))
                ESAutomationFacade.Register(new SceneScanFacadeEndpoint());
        }

        private static ESAutomationTaskInvocationResult RunFromFacade(ESAutomationTaskInvocation invocation)
        {
            try
            {
                if (invocation == null) return ESAutomationTaskInvocationResult.Rejected("缺少场景扫描调用参数。");
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return ESAutomationTaskInvocationResult.Blocked("场景扫描只能在非 PlayMode 下运行。");
                if (!TryResolveFacadeOptions(invocation, out JObject options, out bool interactive, out string optionError))
                    return ESAutomationTaskInvocationResult.Rejected(optionError);
                Scene scene = SceneManager.GetActiveScene();
                if (!scene.IsValid() || !scene.isLoaded)
                    return ESAutomationTaskInvocationResult.Blocked("当前没有可扫描的已加载 Active Scene。");

                string runId = string.IsNullOrWhiteSpace(invocation.invocationId)
                    ? Guid.NewGuid().ToString("N") : invocation.invocationId;
                string invocationHash = ComputeFacadeInvocationHash(invocation, scene.path);
                string sessionPath = GetSessionPath(runId);
                if (File.Exists(sessionPath))
                {
                    ESAutomationSceneScanSession existing = ReadSession(runId);
                    if (!HashEquals(existing.invocationHash, invocationHash))
                        return ESAutomationTaskInvocationResult.Rejected(
                            "InvocationId 已绑定其他场景或输入，拒绝重复执行。");
                    return GetRunFromFacade(runId);
                }
                if (Directory.Exists(GetRunDirectory(runId)))
                    return ESAutomationTaskInvocationResult.Rejected(
                        "InvocationId 对应目录已存在但缺少有效 Session，拒绝猜测恢复。");
                if (!ESAutomationSceneScanPythonAdapter.TryPrepareRuntime(out _, out string pythonReason))
                    return ESAutomationTaskInvocationResult.Blocked(pythonReason, data: new JObject
                    {
                        ["failureCode"] = "EnvironmentUnavailable",
                        ["environmentLockPath"] = ESAutomationPythonEnvironment.ManagedRuntimeLockPath,
                    });
                EnsurePinnedContent();
                RegisterTaskAndAdapter();
                if (runningOperations.Count > 0)
                    return ESAutomationTaskInvocationResult.Blocked("已有场景扫描 Worker 正在运行；请先查询其 RunId。\n");

                ESAutomationSceneScanSession session = CreateSession(scene, runId, invocationHash);
                session.dryRun = invocation.dryRun;
                if (interactive)
                {
                    // 阶段 0 会由 Python 请求输入；C# 随后显示高级对话框，AI 也可通过同一 RunId 提交。
                    session.expectedGeneration = 0;
                }
                else
                {
                    session.expectedGeneration = 1;
                    WriteInputResponse(session, 0, true, options);
                }
                SaveSession(session);
                StartWorkerStage(session);
                return ESAutomationTaskInvocationResult.Accepted(
                    interactive ? "场景扫描已接受；Python 将在检查点请求高级输入。" : "场景扫描已接受，将直接使用规范化输入进入 Python 报告阶段。",
                    session.runId,
                    new JObject
                    {
                        ["phase"] = "Running",
                        ["interactive"] = interactive,
                        ["inputSchemaHash"] = OptionsSchemaHash,
                        ["scenePath"] = scene.path ?? string.Empty,
                    });
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return ESAutomationTaskInvocationResult.Failed(exception.Message);
            }
        }

        private static ESAutomationTaskInvocationResult GetRunFromFacade(string runId)
        {
            try
            {
                string sessionPath = GetSessionPath(runId);
                if (!File.Exists(sessionPath)) return ESAutomationTaskInvocationResult.NotFound("该 RunId 不属于场景扫描或临时记录已不存在。");
                ESAutomationSceneScanSession session = ReadSession(runId);
                var data = new JObject
                {
                    ["phase"] = session.phase,
                    ["expectedGeneration"] = session.expectedGeneration,
                    ["inputSchemaHash"] = OptionsSchemaHash,
                    ["reportDirectory"] = Path.Combine(ESAutomationPathPolicy.ReportsRoot, runId),
                };
                switch (session.phase)
                {
                    case "Completed":
                    case "DryRunCompleted":
                        return BuildCompletedFacadeResult(session, data);
                    case "Failed":
                        return ESAutomationTaskInvocationResult.Failed("场景扫描 Worker 或协议处理失败；请查看临时记录和 Unity Console。", runId);
                    case "Cancelled":
                        return new ESAutomationTaskInvocationResult { status = "Cancelled", message = "场景扫描已取消。", runId = runId, data = data };
                    case "AwaitingInput":
                        data["inputRequest"] = BuildPendingInputRequest(session);
                        return ESAutomationTaskInvocationResult.Blocked("场景扫描等待已注册的报告选项输入。", runId, data);
                    default:
                        return ESAutomationTaskInvocationResult.Accepted("场景扫描仍在运行。", runId, data);
                }
            }
            catch (Exception exception)
            {
                return ESAutomationTaskInvocationResult.Failed(exception.Message, runId);
            }
        }

        private static ESAutomationTaskInvocationResult BuildCompletedFacadeResult(
            ESAutomationSceneScanSession session, JObject data)
        {
            string reportDirectory = Path.Combine(ESAutomationPathPolicy.ReportsRoot, session.runId);
            string resultPath = Path.Combine(reportDirectory, "result.json");
            JObject root = ReadStrictObject(resultPath, new[]
            {
                "protocolVersion", "taskId", "taskVersion", "runId", "workerType", "workerId",
                "workerVersion", "entrypointHash", "status", "exitCode", "startedAtUtc",
                "retryCount", "finishedAtUtc", "inputManifestHash", "outputs", "outputHashes", "findings", "errors",
                "idempotencyKey", "executionSnapshot", "completionDecision", "traceReconciliation",
            }, "场景扫描最终结果");
            ESAutomationRunResult result = root.ToObject<ESAutomationRunResult>();
            result.Validate();
            VerifyPromotedReportDirectory(reportDirectory, result);
            data["exitCode"] = result.exitCode;
            data["outputs"] = new JArray(result.outputs.Select(name => Path.Combine(reportDirectory, name)));
            data["outputHashes"] = new JArray(result.outputHashes);
            data["findings"] = new JArray(result.findings);
            data["dryRun"] = string.Equals(result.status, "DryRun", StringComparison.Ordinal);
            return ESAutomationTaskInvocationResult.Completed(
                result.status == "DryRun" ? "场景扫描 DryRun 已完成。" : "场景扫描已完成。",
                session.runId, data);
        }

        private static ESAutomationTaskInvocationResult SubmitInputFromFacade(ESAutomationTaskInputSubmission submission)
        {
            try
            {
                string sessionPath = GetSessionPath(submission.runId);
                if (!File.Exists(sessionPath)) return ESAutomationTaskInvocationResult.NotFound("该 RunId 不属于场景扫描或临时记录已不存在。");
                ESAutomationSceneScanSession session = ReadSession(submission.runId);
                if (session.phase != "AwaitingInput" || session.expectedGeneration != 0)
                    return ESAutomationTaskInvocationResult.Blocked("该 RunId 当前不接受报告选项输入。", session.runId);
                if (submission.requestGeneration != 0 || submission.stepId != StepId || !HashEquals(submission.schemaHash, OptionsSchemaHash))
                    return ESAutomationTaskInvocationResult.Rejected("输入的 Generation、StepId 或 SchemaHash 已过期。");

                JObject values = submission.values ?? new JObject();
                if (submission.accepted)
                {
                    if (!TryValidateReportOptions(values, out string error)) return ESAutomationTaskInvocationResult.Rejected(error);
                }
                else if (values.Properties().GetEnumerator().MoveNext())
                {
                    return ESAutomationTaskInvocationResult.Rejected("取消输入不得携带业务字段。");
                }

                WriteInputResponse(session, 0, submission.accepted, values);
                session.expectedGeneration = 1;
                session.phase = "InputSubmitted";
                session.updatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
                SaveSession(session);
                StartWorkerStage(session);
                return ESAutomationTaskInvocationResult.Accepted("场景扫描输入已接受，Python 报告阶段已启动。", session.runId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return ESAutomationTaskInvocationResult.Failed(exception.Message, submission.runId);
            }
        }

        private static bool TryResolveFacadeOptions(ESAutomationTaskInvocation invocation, out JObject options, out bool interactive, out string error)
        {
            options = null;
            interactive = false;
            error = string.Empty;
            string preset = invocation.preset ?? string.Empty;
            if (string.IsNullOrEmpty(preset) || preset == "default")
            {
                if (invocation.input != null && invocation.input.Properties().GetEnumerator().MoveNext())
                {
                    error = "default 预设不接受额外 input；请使用 explicit 预设并提交完整 Schema 输入。";
                    return false;
                }
                options = BuildReportOptions(false, "summary", 10);
                return true;
            }
            if (preset == "interactive")
            {
                if (invocation.input != null && invocation.input.Properties().GetEnumerator().MoveNext())
                {
                    error = "interactive 预设不接受直接 input；请等待 Python 检查点后通过高级对话框或 submitInput 提交。";
                    return false;
                }
                interactive = true;
                return true;
            }
            if (preset != "explicit")
            {
                error = "未注册的场景扫描 Preset：" + preset;
                return false;
            }
            options = invocation.input ?? new JObject();
            return TryValidateReportOptions(options, out error);
        }

        private static JObject BuildPendingInputRequest(ESAutomationSceneScanSession session)
        {
            if (session == null || session.expectedGeneration != 0 || session.phase != "AwaitingInput")
                throw new InvalidOperationException("当前场景扫描会话没有可提交的输入检查点。");
            return new JObject
            {
                ["requestGeneration"] = session.expectedGeneration,
                ["stepId"] = StepId,
                ["schemaHash"] = OptionsSchemaHash,
                ["schema"] = JObject.FromObject(ReportOptionsInputSchema),
            };
        }

        private static JObject BuildReportOptions(bool includeInactive, string detailMode, int topComponentCount)
        {
            return new JObject
            {
                ["includeInactive"] = includeInactive,
                ["detailMode"] = detailMode,
                ["topComponentCount"] = topComponentCount,
            };
        }

        private static bool TryValidateReportOptions(JObject options, out string error)
        {
            error = string.Empty;
            if (options == null)
            {
                error = "报告选项不能为空。";
                return false;
            }
            var required = new HashSet<string>(new[] { "includeInactive", "detailMode", "topComponentCount" }, StringComparer.Ordinal);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            foreach (JProperty property in options.Properties()) actual.Add(property.Name);
            if (!actual.SetEquals(required))
            {
                error = "报告选项必须且只能包含 includeInactive、detailMode、topComponentCount。";
                return false;
            }
            if (options["includeInactive"].Type != JTokenType.Boolean)
            {
                error = "includeInactive 必须是布尔值。";
                return false;
            }
            if (options["detailMode"].Type != JTokenType.String || (string)options["detailMode"] != "summary" && (string)options["detailMode"] != "detailed")
            {
                error = "detailMode 必须是稳定值 summary 或 detailed。";
                return false;
            }
            if (options["topComponentCount"].Type != JTokenType.Integer)
            {
                error = "topComponentCount 必须是整数。";
                return false;
            }
            int count = (int)options["topComponentCount"];
            if (count < 1 || count > 50)
            {
                error = "topComponentCount 必须位于 1–50。";
                return false;
            }
            return true;
        }

        private static void WriteInputResponse(ESAutomationSceneScanSession session, int requestGeneration, bool accepted, JObject values)
        {
            WriteJsonAtomic(session.inputResponsePath, new ESAutomationInputResponse
            {
                protocolVersion = ProtocolVersion,
                runId = session.runId,
                requestGeneration = requestGeneration,
                stepId = StepId,
                schemaHash = OptionsSchemaHash,
                accepted = accepted,
                values = values ?? new JObject(),
            });
        }

        private static ESAutomationSceneScanSession CreateSession(Scene scene)
            => CreateSession(scene, Guid.NewGuid().ToString("N"), string.Empty);

        private static ESAutomationSceneScanSession CreateSession(Scene scene, string runId,
            string invocationHash)
        {
            string runDirectory = GetRunDirectory(runId);
            ESAutomationPathPolicy.EnsureWorkerWriteAllowed(runDirectory, new[] { ESAutomationPathPolicy.TempRoot });
            Directory.CreateDirectory(runDirectory);

            string snapshotPath = Path.Combine(runDirectory, "scene-snapshot.json");
            ESAutomationSceneSnapshot snapshot = CaptureSceneSnapshot(scene);
            WriteJsonAtomic(snapshotPath, snapshot);

            string timestamp = DateTimeOffset.UtcNow.ToString("O");
            return new ESAutomationSceneScanSession
            {
                protocolVersion = ProtocolVersion,
                runId = runId,
                invocationHash = invocationHash ?? string.Empty,
                taskId = TaskId,
                taskVersion = TaskVersion,
                workerType = WorkerType,
                workerId = WorkerId,
                workerVersion = WorkerVersion,
                entrypointHash = WorkerEntrypointHash,
                optionsSchemaHash = OptionsSchemaHash,
                dryRun = false,
                expectedGeneration = 0,
                phase = "Created",
                snapshotPath = snapshotPath,
                stageInputPath = Path.Combine(runDirectory, "stage-input.json"),
                stageResultPath = Path.Combine(runDirectory, "stage-result.json"),
                inputResponsePath = Path.Combine(runDirectory, "input-response.json"),
                workerOutputDirectory = Path.Combine(runDirectory, "WorkerOutput"),
                createdAtUtc = timestamp,
                updatedAtUtc = timestamp,
            };
        }

        private static ESAutomationSceneSnapshot CaptureSceneSnapshot(Scene scene)
        {
            var snapshot = new ESAutomationSceneSnapshot
            {
                protocolVersion = ProtocolVersion,
                capturedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                scene = new ESAutomationSceneIdentity
                {
                    name = scene.name ?? string.Empty,
                    path = scene.path ?? string.Empty,
                },
            };

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform rootTransform = root.transform;
                CaptureTransform(rootTransform, CreatePathSegment(rootTransform), 0, snapshot.objects);
            }
            return snapshot;
        }

        private static void CaptureTransform(Transform transform, string hierarchyPath, int depth, List<ESAutomationSceneObjectSnapshot> objects)
        {
            GameObject gameObject = transform.gameObject;
            var item = new ESAutomationSceneObjectSnapshot
            {
                hierarchyPath = hierarchyPath,
                activeSelf = gameObject.activeSelf,
                activeInHierarchy = gameObject.activeInHierarchy,
                layer = gameObject.layer,
                tag = gameObject.tag ?? string.Empty,
                isStatic = gameObject.isStatic,
                depth = depth,
            };
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                item.components.Add(component == null ? "<MissingScript>" : (component.GetType().FullName ?? component.GetType().Name));
            }
            objects.Add(item);

            for (int index = 0; index < transform.childCount; index++)
            {
                Transform child = transform.GetChild(index);
                CaptureTransform(child, hierarchyPath + "/" + CreatePathSegment(child), depth + 1, objects);
            }
        }

        private static string CreatePathSegment(Transform transform)
            => (transform.name ?? string.Empty) + "[" + transform.GetSiblingIndex() + "]";

        private static void StartWorkerStage(ESAutomationSceneScanSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            ValidateSession(session);
            if (session.expectedGeneration != 0 && session.expectedGeneration != 1)
                throw new InvalidOperationException("场景扫描只支持 generation 0 或 1。");
            if (runningOperations.ContainsKey(session.runId))
                throw new InvalidOperationException("该 RunId 已有运行中的 Worker。");

            DeleteStageResultIfPresent(session);
            ESAutomationSceneScanStageInput input = new ESAutomationSceneScanStageInput
            {
                protocolVersion = ProtocolVersion,
                runId = session.runId,
                generation = session.expectedGeneration,
                taskId = TaskId,
                taskVersion = TaskVersion,
                workerType = WorkerType,
                workerId = WorkerId,
                workerVersion = WorkerVersion,
                entrypointHash = WorkerEntrypointHash,
                stepId = StepId,
                optionsSchemaHash = OptionsSchemaHash,
                dryRun = session.dryRun,
                sceneSnapshotPath = session.snapshotPath,
                sceneSnapshotHash = ComputeFileSha256(session.snapshotPath),
                inputResponsePath = session.expectedGeneration == 0 ? string.Empty : session.inputResponsePath,
                workerOutputDirectory = session.workerOutputDirectory,
            };
            WriteJsonAtomic(session.stageInputPath, input);
            session.phase = "Running";
            session.updatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
            SaveSession(session);

            ESAutomationProcessExecution execution = null;
            try
            {
                execution = ESAutomationProcessRunner.Start(new ESAutomationProcessRequest
                {
                    taskId = TaskId,
                    taskVersion = TaskVersion,
                    runId = session.runId,
                    dryRun = session.dryRun,
                    inputContractPath = session.stageInputPath,
                });
            }
            catch
            {
                execution?.Dispose();
                session.phase = "Failed";
                session.updatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
                SaveSession(session);
                throw;
            }

            runningOperations.Add(session.runId, new RunningOperation
            {
                session = session,
                execution = execution,
            });
            if (!updateSubscribed)
            {
                updateSubscribed = true;
                EditorApplication.update += UpdateRunningOperations;
            }
        }

        private static void UpdateRunningOperations()
        {
            var completed = new List<RunningOperation>();
            foreach (RunningOperation operation in runningOperations.Values)
            {
                try
                {
                    if (operation.execution.EnforceTimeout(DateTimeOffset.UtcNow))
                    {
                        operation.execution.Terminate();
                        operation.session.phase = "Failed";
                        operation.session.updatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
                        SaveSession(operation.session);
                        Debug.LogError("[ESAutomation] 场景扫描 Worker 超时，已终止：" + operation.session.runId);
                        completed.Add(operation);
                        continue;
                    }
                    if (operation.execution.HasExited) completed.Add(operation);
                }
                catch (Exception exception)
                {
                    operation.session.phase = "Failed";
                    operation.session.updatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
                    SaveSession(operation.session);
                    Debug.LogException(exception);
                    completed.Add(operation);
                }
            }

            foreach (RunningOperation operation in completed)
            {
                runningOperations.Remove(operation.session.runId);
                int exitCode = -1;
                try
                {
                    operation.execution.TryGetExitCode(out exitCode);
                    operation.execution.Dispose();
                    if (operation.session.phase == "Failed" && exitCode == -1) continue;
                    ESAutomationStageResult result = ReadStageResult(operation.session.stageResultPath);
                    HandleStageResult(operation.session, result, exitCode, false);
                }
                catch (Exception exception)
                {
                    operation.session.phase = "Failed";
                    operation.session.updatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
                    SaveSession(operation.session);
                    Debug.LogException(exception);
                    EditorUtility.DisplayDialog("场景扫描失败", exception.Message, "关闭");
                }
            }

            if (runningOperations.Count == 0 && updateSubscribed)
            {
                updateSubscribed = false;
                EditorApplication.update -= UpdateRunningOperations;
            }
        }

        private static void HandleStageResult(ESAutomationSceneScanSession session, ESAutomationStageResult result, int observedExitCode, bool recovered)
        {
            ValidateSession(session);
            ValidateStageResult(result, session, observedExitCode);
            if (result.status == "NeedsInput")
            {
                if (session.expectedGeneration != 0 || result.exitCode != 30)
                    throw new InvalidOperationException("场景扫描的 NeedsInput 检查点不在注册的首阶段。");
                session.phase = "AwaitingInput";
                session.updatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
                SaveSession(session);
                EditorApplication.delayCall += () => OpenOptionsDialog(session);
                return;
            }
            if (result.status == "Completed")
            {
                if (session.expectedGeneration != 1 || result.exitCode != 0)
                    throw new InvalidOperationException("场景扫描 Completed 检查点不在注册的报告阶段。");
                string reportDirectory = PromoteReports(session);
                session.phase = session.dryRun ? "DryRunCompleted" : "Completed";
                session.updatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
                SaveSession(session);
                Debug.Log("[ESAutomation] 场景扫描报告已生成：" + reportDirectory);
                if (!recovered) EditorUtility.DisplayDialog("场景扫描完成", "报告已校验并写入：\n" + reportDirectory, "关闭");
                return;
            }
            if (result.status == "Cancelled")
            {
                if (session.expectedGeneration != 1 || result.exitCode != 20)
                    throw new InvalidOperationException("场景扫描的 Cancelled 检查点不在注册的报告阶段。");
                session.phase = "Cancelled";
                session.updatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
                SaveSession(session);
                if (!recovered) EditorUtility.DisplayDialog("场景扫描已取消", "未生成正式报告；原始快照仍保留在该 RunId 临时目录。", "关闭");
                return;
            }

            session.phase = "Failed";
            session.updatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
            SaveSession(session);
            string errors = result.errors.Count == 0 ? "Worker 未返回具体错误。" : string.Join("\n", result.errors.ToArray());
            throw new InvalidOperationException("Python Worker 失败：\n" + errors);
        }

        private static void OpenOptionsDialog(ESAutomationSceneScanSession sourceSession)
        {
            if (sourceSession == null) return;
            ESAutomationSceneScanSession session;
            try
            {
                session = ReadSession(sourceSession.runId);
                ValidateSession(session);
                if (session.phase != "AwaitingInput" || session.expectedGeneration != 0) return;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return;
            }

            var request = new ESAdvancedDialogRequest
            {
                dialogId = "es.automation.scene-scan.options",
                title = "场景扫描报告选项",
                message = "Python 将只分析 Unity 已导出的场景快照，不会读取 .unity YAML、修改场景或写入 Assets。",
                detail = "RunId: " + session.runId + "\n确认后会写入规范化 InputResponse.json，并启动一个新的 Python 阶段。",
                confirmText = "生成报告",
                cancelText = "取消扫描",
                owner = null,
                allowMainWorkspaceFallback = true,
            };
            request.AddToggle("includeInactive", "包含未激活对象", false).help = "关闭时，报告会排除 activeInHierarchy 为 false 的对象。";
            request.AddChoiceOptions("detailMode", "报告粒度", new[]
            {
                new ESAdvancedDialogChoiceOption("summary", "摘要（聚合统计）"),
                new ESAdvancedDialogChoiceOption("detailed", "详细（另列对象清单，最多 5000 项）"),
            }, "summary").help = "提交给 Python 的是稳定值 summary / detailed，不是中文显示文案。";
            request.AddText("topComponentCount", "高频组件数量", "10", true).help = "整数 1–50；控制报告中展示的组件类型数量。";
            request.validate = values =>
            {
                if (!int.TryParse(values.GetString("topComponentCount"), out int count) || count < 1 || count > 50)
                    return "“高频组件数量”必须是 1–50 的整数。";
                return string.Empty;
            };
            request.completed = result => SubmitOptions(session.runId, 0, OptionsSchemaHash, result);
            ESDialogService.Show(request);
        }

        private static void SubmitOptions(string runId, int requestGeneration, string schemaHash, ESAdvancedDialogResult result)
        {
            try
            {
                ESAutomationSceneScanSession session = ReadSession(runId);
                ValidateSession(session);
                if (session.phase != "AwaitingInput" || session.expectedGeneration != requestGeneration || !string.Equals(session.optionsSchemaHash, schemaHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("该对话框已过期；输入不会交给其他 RunId。请从 Automation 菜单重新恢复检查点。");

                var values = new JObject();
                bool accepted = result != null && result.accepted;
                if (accepted)
                {
                    int topComponentCount;
                    if (!int.TryParse(result.values.GetString("topComponentCount"), out topComponentCount) || topComponentCount < 1 || topComponentCount > 50)
                        throw new InvalidOperationException("高级对话框返回了不符合注册协议的高频组件数量。");
                    string detailMode = result.values.GetString("detailMode");
                    if (detailMode != "summary" && detailMode != "detailed")
                        throw new InvalidOperationException("高级对话框返回了未注册的报告粒度。" );
                    values["includeInactive"] = result.values.GetToggle("includeInactive");
                    values["detailMode"] = detailMode;
                    values["topComponentCount"] = topComponentCount;
                }

                WriteInputResponse(session, requestGeneration, accepted, values);
                session.expectedGeneration = 1;
                session.phase = "InputSubmitted";
                session.updatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
                SaveSession(session);
                StartWorkerStage(session);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("场景扫描输入未提交", exception.Message, "关闭");
            }
        }

        private static string PromoteReports(ESAutomationSceneScanSession session)
        {
            string reportsDirectory = Path.Combine(ESAutomationPathPolicy.ReportsRoot, session.runId);
            string promotionDirectory = Path.Combine(GetRunDirectory(session.runId), "Promotion");
            ESAutomationPathPolicy.EnsureWorkerWriteAllowed(reportsDirectory, new[] { ESAutomationPathPolicy.ReportsRoot });
            ESAutomationPathPolicy.EnsureWorkerWriteAllowed(promotionDirectory, new[] { ESAutomationPathPolicy.TempRoot });
            if (Directory.Exists(reportsDirectory)) throw new IOException("报告 RunId 已存在，拒绝覆盖：" + session.runId);
            if (Directory.Exists(promotionDirectory)) throw new IOException("报告临时提升目录已存在，拒绝覆盖：" + promotionDirectory);

            VerifySceneSnapshotUnchanged(session);
            string jsonReport = RequireWorkerOutputFile(session, "scene-scan.json");
            string markdownReport = RequireWorkerOutputFile(session, "scene-scan.md");
            ESAutomationPathPolicy.EnsureWorkerDirectory(promotionDirectory, new[] { ESAutomationPathPolicy.TempRoot });
            try
            {
                string promotedJsonPath = Path.Combine(promotionDirectory, "scene-scan.json");
                string promotedMarkdownPath = Path.Combine(promotionDirectory, "scene-scan.md");
                ESAutomationPathPolicy.CopyWorkerFileAtomic(jsonReport, promotedJsonPath,
                    new[] { GetRunDirectory(session.runId) }, new[] { ESAutomationPathPolicy.TempRoot });
                ESAutomationPathPolicy.CopyWorkerFileAtomic(markdownReport, promotedMarkdownPath,
                    new[] { GetRunDirectory(session.runId) }, new[] { ESAutomationPathPolicy.TempRoot });
                // Hash 必须基于已复制到提升目录的字节，而不是基于复制前的源文件。
                var outputHashes = new List<string>
                {
                    ComputeFileSha256(promotedJsonPath),
                    ComputeFileSha256(promotedMarkdownPath),
                };
                string completionCapturedAtUtc = DateTimeOffset.UtcNow.ToString("O");
                var finalResult = new ESAutomationRunResult
                {
                    protocolVersion = ProtocolVersion,
                    taskId = TaskId,
                    taskVersion = TaskVersion,
                    runId = session.runId,
                    workerType = WorkerType,
                    workerId = WorkerId,
                    workerVersion = WorkerVersion,
                    entrypointHash = WorkerEntrypointHash,
                    status = session.dryRun ? "DryRun" : "Passed",
                    exitCode = 0,
                    startedAtUtc = session.createdAtUtc,
                    finishedAtUtc = completionCapturedAtUtc,
                    inputManifestHash = ComputeFileSha256(session.stageInputPath),
                    outputs = new List<string> { "scene-scan.json", "scene-scan.md" },
                    outputHashes = outputHashes,
                    findings = new List<string> { session.dryRun ? "场景扫描 DryRun 报告已由 C# 校验后提升。" : "场景扫描报告已由 C# 校验后提升。" },
                    errors = new List<string>(),
                };
                finalResult.idempotencyKey = ESAutomationGovernance.ComputeIdempotencyKey(
                    TaskId, TaskVersion, finalResult.inputManifestHash, session.invocationHash);
                finalResult.completionDecision = new ESAutomationCompletionDecision
                {
                    runId = session.runId,
                    executionStatus = finalResult.status,
                    freshnessPolicy = new ESAutomationFreshnessPolicy { maxAgeHours = 168, requireSourceHash = true, allowRuntimeNotRun = true },
                    traceReconciled = true,
                    criterionResults = new List<ESAutomationCriterionResult>
                    {
                        new ESAutomationCriterionResult
                        {
                            criterionId = "scene-scan.report-json",
                            verifierId = "es.scene.scan.promoted-output-hash",
                            passed = ESAutomationWorkerRegistration.IsSha256(outputHashes[0]),
                            evidenceState = ESAutomationEvidenceState.Fresh,
                            evidenceHash = outputHashes[0],
                            evidenceBinding = new ESAutomationClaimEvidenceBinding
                            {
                                claimId = "scene-scan.report-json",
                                criterionId = "scene-scan.report-json",
                                evidenceHash = outputHashes[0],
                                sourceHash = WorkerEntrypointHash,
                                capturedAtUtc = completionCapturedAtUtc,
                            },
                        },
                        new ESAutomationCriterionResult
                        {
                            criterionId = "scene-scan.report-markdown",
                            verifierId = "es.scene.scan.promoted-output-hash",
                            passed = ESAutomationWorkerRegistration.IsSha256(outputHashes[1]),
                            evidenceState = ESAutomationEvidenceState.Fresh,
                            evidenceHash = outputHashes[1],
                            evidenceBinding = new ESAutomationClaimEvidenceBinding
                            {
                                claimId = "scene-scan.report-markdown",
                                criterionId = "scene-scan.report-markdown",
                                evidenceHash = outputHashes[1],
                                sourceHash = WorkerEntrypointHash,
                                capturedAtUtc = completionCapturedAtUtc,
                            },
                        },
                    },
                };
                finalResult.completionDecision.accepted = finalResult.completionDecision.CanAccept();
                finalResult.Validate();
                WriteJsonAtomic(Path.Combine(promotionDirectory, "result.json"), finalResult);
                ESAutomationPathPolicy.EnsureWorkerDirectory(ESAutomationPathPolicy.ReportsRoot, new[] { ESAutomationPathPolicy.ProjectRoot });
                Directory.Move(promotionDirectory, reportsDirectory);
                VerifyPromotedReportDirectory(reportsDirectory, finalResult);
                return reportsDirectory;
            }
            catch
            {
                // 失败时保留 RunId 临时证据，禁止用清理操作掩盖错误或误删其他运行记录。
                throw;
            }
        }

        private static void VerifySceneSnapshotUnchanged(ESAutomationSceneScanSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            JObject root = ReadStrictObject(session.stageInputPath, new[]
            {
                "protocolVersion", "runId", "generation", "taskId", "taskVersion", "workerType", "workerId",
                "workerVersion", "entrypointHash", "stepId", "optionsSchemaHash", "dryRun", "sceneSnapshotPath",
                "sceneSnapshotHash", "inputResponsePath", "workerOutputDirectory",
            }, "SceneScan StageInput");
            string expectedHash = root.Value<string>("sceneSnapshotHash") ?? string.Empty;
            string snapshotPath = root.Value<string>("sceneSnapshotPath") ?? string.Empty;
            if (!ESAutomationWorkerRegistration.IsSha256(expectedHash))
                throw new InvalidDataException("SceneScan StageInput snapshot hash is invalid.");
            ESAutomationPathPolicy.EnsureWorkerReadAllowed(snapshotPath, new[] { ESAutomationPathPolicy.TempRoot });
            if (!File.Exists(snapshotPath)) throw new FileNotFoundException("SceneScan snapshot is missing.", snapshotPath);
            string actualHash = ComputeFileSha256(snapshotPath);
            if (!HashEquals(expectedHash, actualHash))
                throw new InvalidDataException("SceneScan source snapshot drift detected; report promotion blocked.");
        }

        private static string RequireWorkerOutputFile(ESAutomationSceneScanSession session, string fileName)
        {
            if (fileName.IndexOf(Path.DirectorySeparatorChar) >= 0 || fileName.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
                throw new ArgumentException("输出文件名不得包含路径分隔符。", nameof(fileName));
            string path = Path.Combine(session.workerOutputDirectory, fileName);
            ESAutomationPathPolicy.EnsureWorkerReadAllowed(path, new[] { GetRunDirectory(session.runId) });
            if (!File.Exists(path)) throw new FileNotFoundException("Worker 未生成必需报告文件。", path);
            long length = new FileInfo(path).Length;
            if (length < 1 || length > MaxPromotedReportBytes)
                throw new IOException("Worker 报告文件大小不在允许范围内：" + fileName + "（" + length + " bytes）");
            return path;
        }

        private static void DeleteStageResultIfPresent(ESAutomationSceneScanSession session)
        {
            ESAutomationPathPolicy.EnsureWorkerWriteAllowed(session.stageResultPath, new[] { GetRunDirectory(session.runId) });
            ESAutomationPathPolicy.DeleteWorkerFile(session.stageResultPath, new[] { GetRunDirectory(session.runId) });
        }

        private static void SaveSession(ESAutomationSceneScanSession session)
        {
            ValidateSession(session);
            WriteJsonAtomic(GetSessionPath(session.runId), session);
        }

        private static ESAutomationSceneScanSession ReadSession(string runId)
        {
            string path = GetSessionPath(runId);
            JObject root = ReadStrictObject(path, new[]
            {
                "protocolVersion", "runId", "invocationHash", "taskId", "taskVersion", "workerType", "workerId", "workerVersion", "entrypointHash",
                "optionsSchemaHash", "dryRun", "expectedGeneration", "phase", "snapshotPath", "stageInputPath", "stageResultPath", "inputResponsePath",
                "workerOutputDirectory", "createdAtUtc", "updatedAtUtc",
            }, "场景扫描会话");
            ESAutomationSceneScanSession session = root.ToObject<ESAutomationSceneScanSession>();
            ValidateSession(session);
            return session;
        }

        private static ESAutomationStageResult ReadStageResult(string path)
        {
            JObject root = ReadStrictObject(path, new[]
            {
                "protocolVersion", "runId", "generation", "taskId", "taskVersion", "workerType", "workerId", "workerVersion", "entrypointHash",
                "status", "exitCode", "startedAtUtc", "finishedAtUtc", "stepId", "schemaHash", "errors",
            }, "Worker 阶段结果");
            ESAutomationStageResult result = root.ToObject<ESAutomationStageResult>();
            if (result.errors == null) throw new InvalidOperationException("Worker 阶段结果 errors 不得为 null。");
            foreach (string error in result.errors)
            {
                if (string.IsNullOrWhiteSpace(error) || error.Length > 4096)
                    throw new InvalidOperationException("Worker 阶段结果包含无效错误摘要。");
            }
            return result;
        }

        private static void ValidateSession(ESAutomationSceneScanSession session)
        {
            if (session == null) throw new InvalidOperationException("场景扫描会话为空。");
            if (session.protocolVersion != ProtocolVersion || session.taskId != TaskId || session.taskVersion != TaskVersion)
                throw new InvalidOperationException("场景扫描会话协议或任务身份不匹配。");
            if (!Guid.TryParseExact(session.runId, "N", out _)) throw new InvalidOperationException("场景扫描会话 RunId 无效。");
            if (!string.IsNullOrWhiteSpace(session.invocationHash)
                && !ESAutomationWorkerRegistration.IsSha256(session.invocationHash))
                throw new InvalidOperationException("场景扫描会话 InvocationHash 无效。");
            if (session.workerType != WorkerType || session.workerId != WorkerId || session.workerVersion != WorkerVersion || !HashEquals(session.entrypointHash, WorkerEntrypointHash))
                throw new InvalidOperationException("场景扫描会话 Worker 身份不匹配。");
            if (!HashEquals(session.optionsSchemaHash, OptionsSchemaHash)) throw new InvalidOperationException("场景扫描会话表单 SchemaHash 不匹配。");
            if (session.dryRun && session.phase == "Completed") throw new InvalidOperationException("DryRun 场景扫描不能标记为正式 Completed。" );
            if (!session.dryRun && session.phase == "DryRunCompleted") throw new InvalidOperationException("非 DryRun 场景扫描不能标记为 DryRunCompleted。" );
            if (session.expectedGeneration < 0 || session.expectedGeneration > 1) throw new InvalidOperationException("场景扫描会话 Generation 无效。");
            if (session.phase != "Created" && session.phase != "Running" && session.phase != "AwaitingInput" && session.phase != "InputSubmitted" && session.phase != "Completed" && session.phase != "DryRunCompleted" && session.phase != "Failed" && session.phase != "Cancelled")
                throw new InvalidOperationException("场景扫描会话状态无效：" + session.phase);
            RequireUtcTimestamp(session.createdAtUtc, "场景扫描会话创建时间");
            RequireUtcTimestamp(session.updatedAtUtc, "场景扫描会话更新时间");

            string runDirectory = GetRunDirectory(session.runId);
            RequireInsideRunDirectory(session.snapshotPath, runDirectory, "snapshotPath");
            RequireInsideRunDirectory(session.stageInputPath, runDirectory, "stageInputPath");
            RequireInsideRunDirectory(session.stageResultPath, runDirectory, "stageResultPath");
            RequireInsideRunDirectory(session.inputResponsePath, runDirectory, "inputResponsePath");
            RequireInsideRunDirectory(session.workerOutputDirectory, runDirectory, "workerOutputDirectory");
        }

        private static void ValidateStageResult(ESAutomationStageResult result, ESAutomationSceneScanSession session, int observedExitCode)
        {
            if (result == null) throw new InvalidOperationException("Worker 没有返回结构化阶段结果。");
            if (result.protocolVersion != ProtocolVersion || result.taskId != TaskId || result.taskVersion != TaskVersion)
                throw new InvalidOperationException("Worker 阶段结果协议或任务身份不匹配。");
            if (result.runId != session.runId || result.generation != session.expectedGeneration)
                throw new InvalidOperationException("Worker 阶段结果 RunId 或 Generation 已过期。");
            if (result.workerType != WorkerType || result.workerId != WorkerId || result.workerVersion != WorkerVersion || !HashEquals(result.entrypointHash, WorkerEntrypointHash))
                throw new InvalidOperationException("Worker 阶段结果身份与 TaskContract 不匹配。");
            if (result.stepId != StepId || !HashEquals(result.schemaHash, OptionsSchemaHash))
                throw new InvalidOperationException("Worker 阶段结果 StepId 或 SchemaHash 不匹配。");
            if (result.status != "NeedsInput" && result.status != "Completed" && result.status != "Failed" && result.status != "Cancelled")
                throw new InvalidOperationException("Worker 阶段结果状态无效：" + result.status);
            RequireUtcTimestamp(result.startedAtUtc, "Worker 阶段开始时间");
            RequireUtcTimestamp(result.finishedAtUtc, "Worker 阶段结束时间");
            if (observedExitCode != result.exitCode)
                throw new InvalidOperationException("Worker 进程退出码与结构化阶段结果不一致。");
            if (result.status == "NeedsInput" && result.exitCode != 30) throw new InvalidOperationException("NeedsInput 必须使用退出码 30。");
            if (result.status == "Completed" && result.exitCode != 0) throw new InvalidOperationException("Completed 必须使用退出码 0。");
            if (result.status == "Cancelled" && result.exitCode != 20) throw new InvalidOperationException("Cancelled 必须使用退出码 20。");
            if (result.status == "Failed" && result.exitCode != 10) throw new InvalidOperationException("Failed 必须使用退出码 10。");
        }

        private static List<ESAutomationSceneScanSession> FindRecoverableSessions()
        {
            var sessions = new List<ESAutomationSceneScanSession>();
            if (!Directory.Exists(ESAutomationPathPolicy.TempRoot)) return sessions;
            foreach (string directory in ESManagedFileIO.EnumerateDirectoriesSafely(ESAutomationPathPolicy.TempRoot))
            {
                string runId = Path.GetFileName(directory);
                if (!Guid.TryParseExact(runId, "N", out _)) continue;
                string sessionPath = Path.Combine(directory, "scene-scan-session.json");
                if (!File.Exists(sessionPath)) continue;
                try
                {
                    ESAutomationSceneScanSession session = ReadSession(runId);
                    if (session.phase == "AwaitingInput" || (session.phase == "Running" && File.Exists(session.stageResultPath))) sessions.Add(session);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[ESAutomation] 忽略无效场景扫描检查点 " + runId + "：" + exception.Message);
                }
            }
            return sessions;
        }

        private static JObject ReadStrictObject(string path, IEnumerable<string> expectedFields, string context)
        {
            if (!File.Exists(path)) throw new FileNotFoundException(context + " 不存在。", path);
            JObject root;
            try
            {
                root = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(context + " 不是合法 JSON：" + exception.Message);
            }

            var expected = new HashSet<string>(expectedFields, StringComparer.Ordinal);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            foreach (JProperty property in root.Properties()) actual.Add(property.Name);
            if (!actual.SetEquals(expected))
            {
                var details = new List<string>();
                foreach (string field in expected) if (!actual.Contains(field)) details.Add("缺少 " + field);
                foreach (string field in actual) if (!expected.Contains(field)) details.Add("未注册 " + field);
                throw new InvalidOperationException(context + " 字段不匹配：" + string.Join("；", details.ToArray()));
            }
            return root;
        }

        private static void RequireInsideRunDirectory(string path, string runDirectory, string field)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("场景扫描会话 " + field + " 为空。");
            if (!ESAutomationPathPolicy.IsWithin(path, new[] { runDirectory }))
                throw new InvalidOperationException("场景扫描会话 " + field + " 越出 RunId 临时目录。");
        }

        private static void RequireUtcTimestamp(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value) || !DateTimeOffset.TryParse(value, out _))
                throw new InvalidOperationException(field + " 无效。");
        }

        private static string GetRunDirectory(string runId)
        {
            if (!Guid.TryParseExact(runId, "N", out _)) throw new ArgumentException("RunId 必须是 N 格式 GUID。", nameof(runId));
            return Path.Combine(ESAutomationPathPolicy.TempRoot, runId);
        }

        private static string GetSessionPath(string runId) => Path.Combine(GetRunDirectory(runId), "scene-scan-session.json");

        private static void WriteJsonAtomic(string path, object value)
        {
            string normalized = ESAutomationPathPolicy.Normalize(path);
            string directory = Path.GetDirectoryName(normalized);
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("无法确定 JSON 目标目录。");
            ESAutomationPathPolicy.WriteWorkerTextAtomic(
                normalized,
                JsonConvert.SerializeObject(value, Formatting.Indented),
                new[] { directory });
        }

        private static void VerifyPromotedReportDirectory(string reportsDirectory, ESAutomationRunResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(reportsDirectory) || !Directory.Exists(reportsDirectory))
                throw new InvalidDataException("报告提升后目录不存在。");

            string[] expectedNames = { "result.json", "scene-scan.json", "scene-scan.md" };
            string[] actualNames = Directory.GetFiles(reportsDirectory, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (!actualNames.SequenceEqual(expectedNames.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal))
                throw new InvalidDataException("报告提升后文件集合不完整或包含未声明文件：" + reportsDirectory);

            string jsonHash = ComputeFileSha256(Path.Combine(reportsDirectory, "scene-scan.json"));
            string markdownHash = ComputeFileSha256(Path.Combine(reportsDirectory, "scene-scan.md"));
            if (result.outputHashes == null || result.outputHashes.Count != 2
                || !HashEquals(result.outputHashes[0], jsonHash)
                || !HashEquals(result.outputHashes[1], markdownHash))
                throw new InvalidDataException("报告最终目录哈希与结果记录不一致：" + reportsDirectory);
            if (ESAutomationTaskRegistry.TryGet(result.taskId, result.taskVersion, out ESAutomationTaskContract contract)
                && contract.performanceBudget != null)
            {
                long outputBytes = new FileInfo(Path.Combine(reportsDirectory, "scene-scan.json")).Length
                    + new FileInfo(Path.Combine(reportsDirectory, "scene-scan.md")).Length;
                if (outputBytes > contract.performanceBudget.maxOutputBytes)
                    throw new InvalidDataException("Scene scan outputs exceed PerformanceBudget maxOutputBytes.");
            }
            if (result.completionDecision == null || !result.completionDecision.CanAccept())
                throw new InvalidDataException("Scene scan report lacks a valid CompletionDecision.");
        }

        private static string ComputeFileSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) builder.Append(value.ToString("x2"));
                return builder.ToString();
            }
        }

        private static string ComputeFacadeInvocationHash(ESAutomationTaskInvocation invocation,
            string scenePath)
        {
            var identity = new JObject
            {
                ["taskId"] = invocation.taskId ?? string.Empty,
                ["taskVersion"] = invocation.taskVersion,
                ["preset"] = invocation.preset ?? string.Empty,
                ["dryRun"] = invocation.dryRun,
                ["scenePath"] = scenePath ?? string.Empty,
                ["input"] = invocation.input?.DeepClone() ?? new JObject(),
            };
            byte[] bytes = Encoding.UTF8.GetBytes(identity.ToString(Formatting.None));
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty)
                    .ToLowerInvariant();
        }

        private static bool HashEquals(string left, string right)
            => !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        private static void EnsurePinnedContent()
        {
            string workerPath = GetWorkerEntrypointPath();
            string schemaPath = GetOptionsSchemaPath();
            if (!File.Exists(workerPath)) throw new FileNotFoundException("受信 Python Worker 入口不存在。", workerPath);
            if (!File.Exists(schemaPath)) throw new FileNotFoundException("受信场景扫描表单 Schema 不存在。", schemaPath);
            if (ESManagedFileIO.ContainsExistingReparsePoint(workerPath) || ESManagedFileIO.ContainsExistingReparsePoint(schemaPath))
                throw new UnauthorizedAccessException("受信 Worker 或 Schema 路径不能穿过 junction 或 symlink。");
            if (!HashEquals(ComputeFileSha256(workerPath), WorkerEntrypointHash))
                throw new InvalidOperationException("Python Worker 入口指纹已变化；请审查版本并同步更新 TaskContract 指纹。" );
            if (!HashEquals(ComputeFileSha256(schemaPath), OptionsSchemaHash))
                throw new InvalidOperationException("场景扫描表单 Schema 指纹已变化；请审查版本并同步更新已注册 SchemaHash。" );
        }

        internal static string GetWorkerEntrypointPath()
            => Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "Workers", "Python", "es_scene_scan_worker.py");

        internal static string GetOptionsSchemaPath()
            => Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "Contracts", "es-scene-scan-report-options.schema.json");

        internal static string QuoteWindowsArgument(string value)
        {
            if (value == null) return "\"\"";
            if (value.Length > 0 && value.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return value;
            var result = new StringBuilder();
            result.Append('"');
            int backslashCount = 0;
            foreach (char character in value)
            {
                if (character == '\\')
                {
                    backslashCount++;
                    continue;
                }
                if (character == '"')
                {
                    result.Append('\\', backslashCount * 2 + 1);
                    result.Append('"');
                    backslashCount = 0;
                    continue;
                }
                result.Append('\\', backslashCount);
                backslashCount = 0;
                result.Append(character);
            }
            result.Append('\\', backslashCount * 2);
            result.Append('"');
            return result.ToString();
        }

        private static void ShowNotification(string message)
        {
            SceneView.lastActiveSceneView?.ShowNotification(new GUIContent(message));
            Debug.Log("[ESAutomation] " + message);
        }

        private sealed class SceneScanFacadeEndpoint : IESAutomationTaskEndpoint, IESAutomationContractBoundEndpoint
        {
            private static readonly ESAutomationTaskDescriptor descriptor = new ESAutomationTaskDescriptor
            {
                taskId = TaskId,
                taskVersion = TaskVersion,
                category = "场景/分析",
                displayName = "快速扫描当前场景",
                summary = "导出当前 Active Scene 快照并生成只读场景统计报告。",
                allowAiInvoke = true,
                allowInPlayMode = false,
                inputSchemaHash = OptionsSchemaHash,
                presets = new List<ESAutomationTaskPresetDescriptor>
                {
                    new ESAutomationTaskPresetDescriptor
                    {
                        presetId = "default",
                        label = "快速摘要",
                        summary = "不包含未激活对象；摘要；展示前 10 个组件类型。",
                    },
                    new ESAutomationTaskPresetDescriptor
                    {
                        presetId = "explicit",
                        label = "规范化输入",
                        summary = "必须提交已注册 Schema 的完整报告选项。",
                    },
                    new ESAutomationTaskPresetDescriptor
                    {
                        presetId = "interactive",
                        label = "请求高级输入",
                        summary = "Python 先进入检查点，再由 ESAdvancedDialog 或 AI submitInput 提交同一份类型化选项。",
                    },
                },
                inputSchemas = new List<ESAutomationInputSchemaDescriptor> { ReportOptionsInputSchema },
            };

            public ESAutomationTaskDescriptor Descriptor => descriptor;
            public ESAutomationInvocationRequirements DescribeInvocation(ESAutomationTaskInvocation invocation)
                => new ESAutomationInvocationRequirements
                {
                    worker = new ESAutomationWorkerRegistration
                    {
                        type = WorkerType,
                        workerId = WorkerId,
                        version = WorkerVersion,
                        entrypointHash = WorkerEntrypointHash,
                        enabled = true,
                    },
                    requiredCapabilities = ESAutomationCapability.ReadArtifacts | ESAutomationCapability.WriteTemp,
                    dryRun = invocation != null && invocation.dryRun,
                };
            public ESAutomationTaskInvocationResult Run(ESAutomationTaskInvocation invocation) => RunFromFacade(invocation);
            public ESAutomationTaskInvocationResult GetRun(string runId) => GetRunFromFacade(runId);
            public ESAutomationTaskInvocationResult SubmitInput(ESAutomationTaskInputSubmission submission) => SubmitInputFromFacade(submission);
        }

        private sealed class RunningOperation
        {
            public ESAutomationSceneScanSession session;
            public ESAutomationProcessExecution execution;
        }
    }

    internal sealed class ESAutomationSceneScanPrototypeInitializer : EditorInvoker_Level0
    {
        public override void InitInvoke()
        {
            ESAutomationSceneScanPrototype.InitializeForEditor();
        }
    }

    /// <summary>受信 Python Adapter：解释器路径仅来自本机环境配置，入口与参数均由 C# 固定。</summary>
    internal sealed class ESAutomationSceneScanPythonAdapter : IESAutomationWorkerAdapter
    {
        public string WorkerType => "Python";
        public string WorkerId => "es.scene.scan.python";

        public ProcessStartInfo CreateStartInfo(ESAutomationTaskContract contract, ESAutomationProcessRequest request)
        {
            if (contract == null || request == null) throw new ArgumentNullException(contract == null ? nameof(contract) : nameof(request));
            if (contract.worker == null || contract.worker.type != WorkerType || contract.worker.workerId != WorkerId)
                throw new InvalidOperationException("Python Adapter 不能执行其他 Worker 身份。");
            if (!TryPrepareRuntime(out ESAutomationPythonRuntime runtime, out string reason))
                throw new InvalidOperationException(reason);
            string interpreterPath = runtime.interpreterPath;

            string workerPath = ESAutomationSceneScanPrototype.GetWorkerEntrypointPath();
            string arguments = ESAutomationSceneScanPrototype.QuoteWindowsArgument(workerPath)
                + " --input " + ESAutomationSceneScanPrototype.QuoteWindowsArgument(request.inputContractPath)
                + " --stage-result " + ESAutomationSceneScanPrototype.QuoteWindowsArgument(Path.Combine(Path.GetDirectoryName(request.inputContractPath), "stage-result.json"));
            var startInfo = new ProcessStartInfo
            {
                FileName = interpreterPath,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(workerPath),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            startInfo.EnvironmentVariables["PYTHONDONTWRITEBYTECODE"] = "1";
            startInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
            return startInfo;
        }

        internal static bool TryGetConfiguredInterpreter(out string interpreterPath, out string reason)
        {
            if (!ESAutomationPythonEnvironment.TryResolve(out ESAutomationPythonRuntime runtime, out reason))
            {
                interpreterPath = string.Empty;
                return false;
            }
            interpreterPath = runtime.interpreterPath;
            reason = string.Empty;
            return true;
        }

        internal static bool TryPrepareRuntime(out ESAutomationPythonRuntime runtime, out string reason)
        {
            if (!ESAutomationPythonEnvironment.TryResolve(out runtime, out reason)) return false;
            return ESAutomationPythonEnvironment.TryValidateForExecution(runtime, out reason);
        }
    }

    [Serializable]
    internal sealed class ESAutomationSceneScanSession
    {
        public int protocolVersion;
        public string runId = string.Empty;
        public string invocationHash = string.Empty;
        public string taskId = string.Empty;
        public int taskVersion;
        public string workerType = string.Empty;
        public string workerId = string.Empty;
        public string workerVersion = string.Empty;
        public string entrypointHash = string.Empty;
        public string optionsSchemaHash = string.Empty;
        public bool dryRun;
        public int expectedGeneration;
        public string phase = string.Empty;
        public string snapshotPath = string.Empty;
        public string stageInputPath = string.Empty;
        public string stageResultPath = string.Empty;
        public string inputResponsePath = string.Empty;
        public string workerOutputDirectory = string.Empty;
        public string createdAtUtc = string.Empty;
        public string updatedAtUtc = string.Empty;
    }

    [Serializable]
    internal sealed class ESAutomationSceneScanStageInput
    {
        public int protocolVersion;
        public string runId = string.Empty;
        public int generation;
        public string taskId = string.Empty;
        public int taskVersion;
        public string workerType = string.Empty;
        public string workerId = string.Empty;
        public string workerVersion = string.Empty;
        public string entrypointHash = string.Empty;
        public string stepId = string.Empty;
        public string optionsSchemaHash = string.Empty;
        public bool dryRun;
        public string sceneSnapshotPath = string.Empty;
        public string sceneSnapshotHash = string.Empty;
        public string inputResponsePath = string.Empty;
        public string workerOutputDirectory = string.Empty;
    }

    [Serializable]
    internal sealed class ESAutomationInputResponse
    {
        public int protocolVersion;
        public string runId = string.Empty;
        public int requestGeneration;
        public string stepId = string.Empty;
        public string schemaHash = string.Empty;
        public bool accepted;
        public JObject values = new JObject();
    }

    [Serializable]
    internal sealed class ESAutomationStageResult
    {
        // Python 结果文件反序列化前保持非法值；ValidateStageResult 必须观察到 Worker 的实际写入值。
        public int protocolVersion = -1;
        public string runId = string.Empty;
        public int generation = -1;
        public string taskId = string.Empty;
        public int taskVersion = -1;
        public string workerType = string.Empty;
        public string workerId = string.Empty;
        public string workerVersion = string.Empty;
        public string entrypointHash = string.Empty;
        public string status = string.Empty;
        public int exitCode = -1;
        public string startedAtUtc = string.Empty;
        public string finishedAtUtc = string.Empty;
        public string stepId = string.Empty;
        public string schemaHash = string.Empty;
        public List<string> errors = new List<string>();
    }

    [Serializable]
    internal sealed class ESAutomationSceneSnapshot
    {
        public int protocolVersion;
        public string capturedAtUtc = string.Empty;
        public ESAutomationSceneIdentity scene = new ESAutomationSceneIdentity();
        public List<ESAutomationSceneObjectSnapshot> objects = new List<ESAutomationSceneObjectSnapshot>();
    }

    [Serializable]
    internal sealed class ESAutomationSceneIdentity
    {
        public string name = string.Empty;
        public string path = string.Empty;
    }

    [Serializable]
    internal sealed class ESAutomationSceneObjectSnapshot
    {
        public string hierarchyPath = string.Empty;
        public bool activeSelf;
        public bool activeInHierarchy;
        public int layer;
        public string tag = string.Empty;
        public bool isStatic;
        public int depth;
        public List<string> components = new List<string>();
    }
}
