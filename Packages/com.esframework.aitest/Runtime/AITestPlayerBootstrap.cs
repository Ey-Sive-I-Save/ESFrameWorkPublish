using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace ESFramework.ESAITest
{
    public static class ESAITestPlayerBootstrap
    {
        private const string PlanArgument = "-esAITestPlan";
        private const string InboxArgument = "-esAITestInbox";
        private const string DirectESTESTArgument = "-esTest";
        private const string AutonomyArgument = "-esAITestAutonomy";
        private const string ExistingAutonomyArgument = "-esAITestAutonomyExisting";
        private const string AutonomyPrepareArgument = "-esAITestAutonomyPrepare";
        private const string QuitArgument = "-esAITestQuit";
        private static GameObject activeHost;

        public static ESAITestRunner ActiveRunner { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void TryStartFromCommandLine()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            string planPath = ReadArgumentValue(arguments, PlanArgument);
            bool inboxRequested = HasArgument(arguments, InboxArgument);
            bool directESTESTRequested = HasArgument(arguments, DirectESTESTArgument);
            bool autonomyRequested = HasArgument(arguments, AutonomyArgument);
            string autonomyGoal = ReadArgumentValue(arguments, AutonomyArgument);
            bool existingAutonomyRequested = HasArgument(arguments, ExistingAutonomyArgument);
            string existingAutonomyGoal = ReadArgumentValue(arguments, ExistingAutonomyArgument);
            bool autonomyPrepareRequested = HasArgument(arguments, AutonomyPrepareArgument);
            string autonomyPrepareGoal = ReadArgumentValue(arguments, AutonomyPrepareArgument);
            if (string.IsNullOrWhiteSpace(planPath) && inboxRequested)
            {
                planPath = ReadArgumentValue(arguments, InboxArgument);
                if (string.IsNullOrWhiteSpace(planPath))
                    planPath = Path.Combine(Application.persistentDataPath, "ESAITest", "inbox", "plan.json");
            }

            int requestedStartCount = (string.IsNullOrWhiteSpace(planPath) ? 0 : 1)
                + (directESTESTRequested ? 1 : 0)
                + (autonomyRequested ? 1 : 0)
                + (existingAutonomyRequested ? 1 : 0)
                + (autonomyPrepareRequested ? 1 : 0);
            if (requestedStartCount > 0 && !CanActivateAcceptanceRuntime())
            {
                Debug.LogError("[ESAITest] 生产策略拒绝启动：仅允许 Editor、DevelopmentBuild 或显式 ES_AITEST_ACCEPTANCE 构建通过参数激活。");
                return;
            }
            if (requestedStartCount > 1)
            {
                Debug.LogError("[ESAITest] 启动参数互斥：一次只能使用 -esAITestPlan/-esAITestInbox、-esTest、-esAITestAutonomy、-esAITestAutonomyExisting 或 -esAITestAutonomyPrepare 之一。");
                return;
            }
            if ((autonomyRequested && string.IsNullOrWhiteSpace(autonomyGoal))
                || (existingAutonomyRequested && string.IsNullOrWhiteSpace(existingAutonomyGoal))
                || (autonomyPrepareRequested && string.IsNullOrWhiteSpace(autonomyPrepareGoal)))
            {
                Debug.LogError("[ESAITest] 自主参数必须提供非空目标文本。");
                return;
            }
            if (requestedStartCount == 0)
                return;

            bool forceQuit = HasArgument(arguments, QuitArgument);
            string error;
            bool started;
            if (autonomyRequested)
            {
                started = TryStartAutonomyWithExternalAi(autonomyGoal, forceQuit, out error);
            }
            else if (existingAutonomyRequested)
            {
                started = TryStartAutonomyUsingExistingAi(existingAutonomyGoal, forceQuit, out error);
            }
            else if (autonomyPrepareRequested)
            {
                started = TryPrepareAutonomyExternalAi(autonomyPrepareGoal, out string preparationPath, out error);
                if (started)
                    Debug.Log("[ESAITest] 已准备外部测试 AI（未启动 Runner/进程/回合）：" + preparationPath);
            }
            else
            {
                started = string.IsNullOrWhiteSpace(planPath)
                    ? TryStartESTEST(forceQuit, out error)
                    : TryStartFromPath(planPath, forceQuit, out error);
            }
            if (!started)
            {
                Debug.LogError("[ESAITest] 启动失败：" + error);
                if (forceQuit)
                    Application.Quit(2);
            }
        }

        private static bool CanActivateAcceptanceRuntime()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || ES_AITEST_ACCEPTANCE
            return true;
#else
            return false;
#endif
        }

        public static bool TryStartFromPath(string planPath, bool forceQuit, out string error)
        {
            error = string.Empty;
            try
            {
                string json = File.ReadAllText(Path.GetFullPath(planPath ?? string.Empty));
                ESAITestRequestDto request = JsonUtility.FromJson<ESAITestRequestDto>(json);
                if (request == null)
                    throw new InvalidDataException("计划 JSON 无法反序列化。");
                request.quitOnComplete |= forceQuit;
                return TryStartRequest(request, out error);
            }
            catch (Exception exception)
            {
                error = exception.ToString();
                return false;
            }
        }

        public static bool TryStartESTEST(out string error)
        {
            return TryStartESTEST(false, out error);
        }

        public static bool TryStartESTEST(string planPath, out string error)
        {
            return TryStartESTEST(planPath, false, out error);
        }

        public static bool TryStartESTEST(string planPath, bool forceQuit, out string error)
        {
            return string.IsNullOrWhiteSpace(planPath)
                ? TryStartESTEST(forceQuit, out error)
                : TryStartFromPath(planPath, forceQuit, out error);
        }

        public static bool TryStartESTEST(ESAITestRequestDto request, out string error)
        {
            return TryStartRequest(request, out error);
        }

        public static bool TryStartESTEST(bool forceQuit, out string error)
        {
            string runId = "ESTEST-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            return TryStartRequest(new ESAITestRequestDto
            {
                protocolVersion = ESAITestProtocol.CurrentVersion,
                runId = runId,
                quitOnComplete = forceQuit,
                plan = new ESAITestPlanDto
                {
                    protocolVersion = ESAITestProtocol.CurrentVersion,
                    planId = "ESTEST.Direct",
                    totalTimeoutSeconds = 30f,
                    steps = new[]
                    {
                        new ESAITestStepDto
                        {
                            protocolVersion = ESAITestProtocol.CurrentVersion,
                            stepId = "estest-ai-simulate-publish",
                            operation = ESAITestProtocol.OperationAct,
                            capabilityId = "unity.prompt",
                            command = "prompt.publish",
                            timeoutSeconds = 5f,
                            arguments = new[]
                            {
                                new ESAITestArgumentDto { key = "message", value = "ESTEST 已由 AI 直接模拟 Publish，请优先确认提示链与注意力上下文。" },
                                new ESAITestArgumentDto { key = "priority", value = "P1" },
                                new ESAITestArgumentDto { key = "source", value = "direct-estest-baseline" },
                                new ESAITestArgumentDto { key = "ttlSeconds", value = "20" },
                            },
                        },
                        new ESAITestStepDto
                        {
                            protocolVersion = ESAITestProtocol.CurrentVersion,
                            stepId = "estest-ai-observe-published-prompt",
                            operation = ESAITestProtocol.OperationSee,
                            capabilityId = "unity.observe",
                            command = "attention.snapshot",
                            timeoutSeconds = 5f,
                            pollIntervalSeconds = 0.1f,
                            arguments = new[]
                            {
                                new ESAITestArgumentDto { key = "attention", value = "adaptive" },
                            },
                        },
                    },
                },
            }, out error);
        }

        public static bool TryStartAutonomy(string goal, out string error)
        {
            return TryStartAutonomyInternal(goal, false, false, out error);
        }

        /// <summary>
        /// Starts one bounded autonomous Run and automatically launches the locally trusted
        /// external AI agent. The executable is deliberately resolved from the operator's
        /// ESAITEST_AUTONOMY_AGENT_PATH environment variable, never from a plan or prompt.
        /// </summary>
        public static bool TryStartAutonomyWithExternalAi(string goal, out string error)
        {
            return TryStartAutonomyWithExternalAi(goal, false, out error);
        }

        public static bool TryStartAutonomyWithExternalAi(string goal, bool forceQuit, out string error)
        {
            if (!ESAITestAutonomyExternalBridgeEnvironment.TryResolve(out _, out error))
                return false;
            return TryStartAutonomyInternal(goal, true, true, forceQuit, out error);
        }

        /// <summary>
        /// Reuses an already running/managed external Agent through the same file bridge. This
        /// creates only the Runner; it never starts another process.
        /// </summary>
        public static bool TryStartAutonomyUsingExistingAi(string goal, out string error)
        {
            return TryStartAutonomyUsingExistingAi(goal, false, out error);
        }

        public static bool TryStartAutonomyUsingExistingAi(string goal, bool forceQuit, out string error)
        {
            return TryStartAutonomyInternal(goal, false, true, forceQuit, out error);
        }

        /// <summary>
        /// Validates the configured Agent and writes a preparation record only. No Runner,
        /// external process, request queue or AI turn is started.
        /// </summary>
        public static bool TryPrepareAutonomyExternalAi(string goal, out string preparationPath, out string error)
        {
            preparationPath = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(goal))
            {
                error = "自主准备 goal 不能为空。";
                return false;
            }
            if (!ESAITestAutonomyExternalBridgeEnvironment.TryResolve(out string executablePath, out error))
                return false;

            try
            {
                string preparationId = "AUTONOMY-PREPARED-"
                    + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-"
                    + Guid.NewGuid().ToString("N").Substring(0, 8);
                string directory = Path.Combine(Application.persistentDataPath, "ESAITest", "autonomy", "prepared", preparationId);
                Directory.CreateDirectory(directory);
                preparationPath = Path.Combine(directory, "preparation.json");
                ESAITestAutonomyPreparationDto preparation = new ESAITestAutonomyPreparationDto
                {
                    preparationId = preparationId,
                    goal = goal.Trim(),
                    executableName = Path.GetFileName(executablePath),
                    executableSha256 = Environment.GetEnvironmentVariable(
                        ESAITestAutonomyExternalBridgeEnvironment.AgentSha256EnvironmentVariable) ?? string.Empty,
                    createdUtcTicks = DateTime.UtcNow.Ticks,
                };
                string temporaryPath = preparationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temporaryPath, JsonUtility.ToJson(preparation, true), new UTF8Encoding(false));
                    File.Move(temporaryPath, preparationPath);
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                return true;
            }
            catch (Exception exception)
            {
                error = "写入自主 Agent 准备记录失败：" + exception.Message;
                return false;
            }
        }

        private static bool TryStartAutonomyInternal(
            string goal,
            bool autoLaunchExternalAi,
            bool useExternalBridge,
            out string error)
        {
            return TryStartAutonomyInternal(goal, autoLaunchExternalAi, useExternalBridge, false, out error);
        }

        private static bool TryStartAutonomyInternal(
            string goal,
            bool autoLaunchExternalAi,
            bool useExternalBridge,
            bool forceQuit,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(goal))
            {
                error = "自主会话 goal 不能为空。";
                return false;
            }

            string runId = "AUTONOMY-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")
                + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            return TryStartRequest(new ESAITestRequestDto
            {
                protocolVersion = ESAITestProtocol.CurrentVersion,
                runId = runId,
                quitOnComplete = forceQuit,
                plan = new ESAITestPlanDto
                {
                    protocolVersion = ESAITestProtocol.CurrentVersion,
                    planId = "AI.Autonomy",
                    totalTimeoutSeconds = 300f,
                    steps = new[]
                    {
                        new ESAITestStepDto
                        {
                            protocolVersion = ESAITestProtocol.CurrentVersion,
                            stepId = "autonomy-bootstrap-see",
                            operation = ESAITestProtocol.OperationSee,
                            capabilityId = "unity.observe",
                            command = "attention.snapshot",
                            timeoutSeconds = 5f,
                            pollIntervalSeconds = 0.1f,
                            arguments = new[]
                            {
                                new ESAITestArgumentDto { key = "attention", value = "adaptive" },
                            },
                        },
                    },
                },
                autonomy = new ESAITestAutonomyConfigDto
                {
                    protocolVersion = ESAITestProtocol.CurrentVersion,
                    goal = goal.Trim(),
                    maxTurns = 64,
                    maxDurationSeconds = 300f,
                    maxConsecutiveFailures = 3,
                    allowExploration = true,
                    requireBusinessVerification = true,
                    externalBridge = useExternalBridge
                        ? new ESAITestAutonomyExternalBridgeConfigDto
                        {
                            protocolVersion = ESAITestProtocol.CurrentVersion,
                            autoLaunch = autoLaunchExternalAi,
                            launcherId = ESAITestAutonomyExternalBridgeConfigDto.EnvironmentLauncherId,
                            startupTimeoutSeconds = 15f,
                            heartbeatTimeoutSeconds = 20f,
                        }
                        : null,
                },
            }, out error);
        }

        public static bool TryStartRequest(ESAITestRequestDto request, out string error)
        {
            if (ActiveRunner != null)
            {
                error = "已有 ESAITest Runner 正在运行。";
                return false;
            }

            if (!CanActivateAcceptanceRuntime())
            {
                error = "生产策略拒绝启动：仅允许 Editor、DevelopmentBuild 或显式 ES_AITEST_ACCEPTANCE 构建激活。";
                return false;
            }

            activeHost = new GameObject("ESAITest Player Runner");
            UnityEngine.Object.DontDestroyOnLoad(activeHost);
            ActiveRunner = activeHost.AddComponent<ESAITestRunner>();
            ESAITestRuntimeDashboard dashboard = activeHost.AddComponent<ESAITestRuntimeDashboard>();
            dashboard.Bind(ActiveRunner);
            bool quitOnComplete = request != null && request.quitOnComplete;
            ActiveRunner.Completed += completed => OnCompleted(activeHost, completed, quitOnComplete);
            ActiveRunner.Begin(request);
            error = string.Empty;
            return true;
        }

        public static bool RequestCancel()
        {
            if (ActiveRunner == null || !ActiveRunner.IsRunning)
                return false;
            ActiveRunner.Cancel();
            return true;
        }

        public static bool SubmitAutonomyDecision(ESAITestAutonomyDecisionDto decision, out string error)
        {
            if (ActiveRunner == null)
            {
                error = "当前没有活动 ESAITest Runner。";
                return false;
            }
            return ActiveRunner.SubmitAutonomyDecision(decision, out error);
        }

        /// <summary>
        /// 将一次自然语言请求路由到受限 ESAITest 授权入口。未知、冲突或缺参文本不会执行。
        /// </summary>
        public static bool TryExecuteNaturalLanguage(
            string input,
            out ESAITestNaturalLanguageExecutionResultDto result,
            out string error)
        {
            return ESAITestNaturalLanguageRouter.TryExecute(input, out result, out error);
        }

        public static bool TryExecuteNaturalLanguage(
            string input,
            string requestId,
            out ESAITestNaturalLanguageExecutionResultDto result,
            out string error)
        {
            return ESAITestNaturalLanguageRouter.TryExecute(input, requestId, out result, out error);
        }

        private static void OnCompleted(GameObject host, ESAITestResultDto result, bool quitOnComplete)
        {
            try
            {
                string path = ESAITestReportWriter.WriteToPersistentData(result);
                Debug.Log("[ESAITest] 结果已写入：" + path);
            }
            catch (Exception exception)
            {
                ESAITestReportWriter.MarkReportWriteFailed(result, exception);
                Debug.LogError("[ESAITest] 结果落盘失败：" + exception);
            }

            if (ReferenceEquals(host, activeHost))
            {
                ActiveRunner = null;
                activeHost = null;
            }
            if (quitOnComplete)
            {
                UnityEngine.Object.Destroy(host);
                Application.Quit(result.exitCode);
            }
            else
            {
                UnityEngine.Object.Destroy(host);
            }
        }

        private static bool HasArgument(string[] arguments, string name)
        {
            for (int i = 0; i < arguments.Length; i++)
                if (string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static string ReadArgumentValue(string[] arguments, string name)
        {
            for (int i = 0; i < arguments.Length; i++)
            {
                if (!string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (i + 1 >= arguments.Length || arguments[i + 1].StartsWith("-", StringComparison.Ordinal))
                    return null;
                return arguments[i + 1];
            }
            return null;
        }
    }
}
