#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using ES.Editor;

namespace ES
{
    /// <summary>
    /// Current-Editor Automation endpoint for ScreenSpec v3 materialization.
    /// The endpoint owns request admission and run records; ESUIGameScreenMaterializer
    /// remains the only implementation that creates Prefabs, Fixture Scenes and evidence.
    /// </summary>
    internal static class ESUIAutomationMaterializer
    {
        private const string TaskId = "es.ui.materialize-screen";
        private const int TaskVersion = 1;
        private const string WorkerType = "Other";
        private const string WorkerId = "es.ui.materializer.editor";
        private const string WorkerVersion = "1.0.0";
        // Stable identity for this fixed in-process endpoint. It is deliberately not a
        // generic script runner hash and changes whenever the endpoint contract changes.
        private const string WorkerEntrypointHash = "6d63f61a6d64ac9ef1fd6ef2a38b11e64b4370a6bc3b11d1670db4f5bb70ec19";
        private const string InputSchemaHash = "a6d0645f70fcd2f6a67e41aeaf2ed9cb6d70b4ba1c08f6cc9c5e4c52adf50bd2";
        private const string ContractPath = ".agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md";
        private const string DefaultSpecPath = "Assets/UI/Contracts/ArenaMobaLobby.screen-spec.v3.json";
        private const string DefaultEvidenceRoot = "ES/UIEvidence/arena-moba-lobby";
        private static readonly string[] DefaultProfiles = { "wide", "narrow" };
        private static readonly string[] DefaultStates = { "default", "selected", "disabled", "loading", "error", "long-content" };
        private static readonly Dictionary<string, ESAutomationTaskInvocationResult> runs =
            new Dictionary<string, ESAutomationTaskInvocationResult>(StringComparer.Ordinal);
        private static bool initialized;

        internal static void InitializeForEditor()
        {
            if (initialized) return;
            initialized = true;
            try
            {
                if (!ESAutomationTaskRegistry.TryGet(TaskId, TaskVersion, out _))
                {
                    ESAutomationTaskRegistry.Register(new ESAutomationTaskContract
                    {
                        protocolVersion = 1,
                        taskId = TaskId,
                        version = TaskVersion,
                        worker = CreateWorker(),
                        inputs = new List<string>(),
                        readRoots = new List<string> { "Assets/UI/Contracts" },
                        writeRoots = new List<string>
                        {
                            "Assets/UI/Prefabs/Generated",
                            "Assets/UI/Scenes/Generated",
                            "ES/UIEvidence",
                        },
                        capabilities = new List<string> { "MaterializeUI" },
                        inputSchemaHash = InputSchemaHash,
                        timeoutSeconds = 600,
                        supportsDryRun = true,
                        supportsRetry = false,
                        outputs = new List<string> { "prefab", "fixture-scene", "evidence", "result.json" },
                    });
                }
                else
                {
                    ESAutomationTaskContract contract = GetContract();
                    if (!SameWorker(contract.worker, CreateWorker())
                        || !contract.capabilities.SequenceEqual(new[] { "MaterializeUI" }, StringComparer.Ordinal)
                        || !string.Equals(contract.inputSchemaHash, InputSchemaHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("已有 UI Materializer TaskContract 与受信身份不一致。");
                }
                if (!ESAutomationFacade.TryGetDescriptor(TaskId, TaskVersion, out _))
                    ESAutomationFacade.Register(new FacadeEndpoint());
            }
            catch
            {
                initialized = false;
                throw;
            }
        }

        private static ESAutomationTaskContract GetContract()
        {
            if (!ESAutomationTaskRegistry.TryGet(TaskId, TaskVersion, out ESAutomationTaskContract contract))
                throw new InvalidOperationException("UI Materializer TaskContract 尚未注册。");
            return contract;
        }

        private static ESAutomationWorkerRegistration CreateWorker()
            => new ESAutomationWorkerRegistration
            {
                type = WorkerType,
                workerId = WorkerId,
                version = WorkerVersion,
                entrypointHash = WorkerEntrypointHash,
                enabled = true,
            };

        private static bool SameWorker(ESAutomationWorkerRegistration left, ESAutomationWorkerRegistration right)
            => left != null && right != null
                && left.type == right.type && left.workerId == right.workerId
                && left.version == right.version
                && string.Equals(left.entrypointHash, right.entrypointHash, StringComparison.OrdinalIgnoreCase);

        private static readonly ESAutomationTaskDescriptor descriptor = new ESAutomationTaskDescriptor
        {
            taskId = TaskId,
            taskVersion = TaskVersion,
            category = "UI/物化",
            displayName = "物化 ScreenSpec v3 UI",
            summary = "在当前 Unity Editor 主线程生成受限 UI Prefab、Fixture Scene 和视觉证据。",
            allowAiInvoke = true,
            allowInPlayMode = false,
            inputSchemaHash = InputSchemaHash,
            presets = new List<ESAutomationTaskPresetDescriptor>
            {
                new ESAutomationTaskPresetDescriptor { presetId = "default", label = "Arena MOBA Lobby", summary = "使用项目内受信大厅 ScreenSpec 与固定证据矩阵。" },
                new ESAutomationTaskPresetDescriptor { presetId = "explicit", label = "显式 ScreenSpec", summary = "仅允许 Contracts 下的 ScreenSpec v3 和受限输出根。" },
            },
        };

        private sealed class FacadeEndpoint : IESAutomationTaskEndpoint, IESAutomationContractBoundEndpoint
        {
            public ESAutomationTaskDescriptor Descriptor => descriptor;

            public ESAutomationInvocationRequirements DescribeInvocation(ESAutomationTaskInvocation invocation)
            {
                ResolvedRequest request = ResolveRequest(invocation);
                return new ESAutomationInvocationRequirements
                {
                    worker = CreateWorker(),
                    requiredCapabilities = ESAutomationCapability.MaterializeUI,
                    dryRun = invocation != null && invocation.dryRun,
                    readPaths = new List<string> { request.specAbsolutePath },
                    writePaths = new List<string>
                    {
                        request.prefabAbsolutePath,
                        request.fixtureSceneAbsolutePath,
                        request.evidenceAbsolutePath,
                    },
                    inputManifestHash = request.specHash,
                };
            }

            public ESAutomationTaskInvocationResult Run(ESAutomationTaskInvocation invocation)
                => RunMaterializer(invocation);

            public ESAutomationTaskInvocationResult GetRun(string runId)
            {
                lock (runs)
                    return runs.TryGetValue(runId ?? string.Empty, out ESAutomationTaskInvocationResult result)
                        ? result : ESAutomationTaskInvocationResult.NotFound("UI Materializer RunId 不存在或已被域重载清除。");
            }

            public ESAutomationTaskInvocationResult SubmitInput(ESAutomationTaskInputSubmission submission)
                => ESAutomationTaskInvocationResult.NotFound("UI Materializer 是同步任务，不接受输入检查点。");
        }

        private sealed class ResolvedRequest
        {
            public string specPath;
            public string specAbsolutePath;
            public string specJson;
            public string specHash;
            public string evidenceRoot;
            public string evidenceAbsolutePath;
            public string prefabAbsolutePath;
            public string fixtureSceneAbsolutePath;
            public string[] profiles;
            public string[] states;
        }

        private static ESAutomationTaskInvocationResult RunMaterializer(ESAutomationTaskInvocation invocation)
        {
            string runId = invocation?.invocationId ?? string.Empty;
            try
            {
                ResolvedRequest request = ResolveRequest(invocation);
                string contractHash = ComputeSha256(Path.Combine(ESAutomationPathPolicy.ProjectRoot,
                    ContractPath.Replace('/', Path.DirectorySeparatorChar)));
                string materializerResult = ESUIGameScreenMaterializer.ExecuteAuthoringJsonCore(
                    request.specJson, request.profiles, request.states, invocation.dryRun,
                    contractHash, runId, 1, request.evidenceRoot, request.specHash);
                JObject data = new JObject
                {
                    ["taskId"] = TaskId,
                    ["runId"] = runId,
                    ["specPath"] = request.specPath,
                    ["specHash"] = request.specHash,
                    ["contractHash"] = contractHash,
                    ["evidenceRoot"] = request.evidenceRoot,
                    ["profiles"] = new JArray(request.profiles),
                    ["states"] = new JArray(request.states),
                    ["visualAcceptance"] = "not-claimed",
                    ["materializer"] = JObject.Parse(materializerResult),
                };
                ESAutomationTaskInvocationResult result = invocation.dryRun
                    ? new ESAutomationTaskInvocationResult { status = ESAutomationRunStatus.DryRun, message = "UI ScreenSpec 预检通过，未写入 Unity 资产。", runId = runId, data = data }
                    : ESAutomationTaskInvocationResult.Completed("UI ScreenSpec 已在当前 Unity Editor 主线程物化。", runId, data);
                lock (runs) runs[runId] = result;
                return result;
            }
            catch (Exception exception)
            {
                ESAutomationTaskInvocationResult result = ESAutomationTaskInvocationResult.Failed(
                    "UI Materializer 执行失败：" + exception.Message, runId);
                lock (runs) runs[runId] = result;
                return result;
            }
        }

        private static ResolvedRequest ResolveRequest(ESAutomationTaskInvocation invocation)
        {
            if (invocation == null) throw new InvalidDataException("缺少 UI Materializer Invocation。");
            JObject input = invocation.input ?? new JObject();
            if (invocation.preset != "default" && invocation.preset != "explicit")
                throw new InvalidDataException("UI Materializer preset 仅支持 default 或 explicit。");
            RequireExact(input, new[] { "specPath", "evidenceRoot", "profiles", "states" });
            string specPath = invocation.preset == "default" && input.Count == 0
                ? DefaultSpecPath : ReadSafeString(input, "specPath");
            string evidenceRoot = invocation.preset == "default" && input.Count == 0
                ? DefaultEvidenceRoot : ReadEvidenceRoot(input);
            string[] profiles = invocation.preset == "default" && input.Count == 0
                ? DefaultProfiles : ReadIds(input, "profiles");
            string[] states = invocation.preset == "default" && input.Count == 0
                ? DefaultStates : ReadIds(input, "states");
            if (!specPath.StartsWith("Assets/UI/Contracts/", StringComparison.Ordinal)
                || !specPath.EndsWith(".screen-spec.v3.json", StringComparison.Ordinal)
                || specPath.Contains("..") || Path.IsPathRooted(specPath)
                || Path.GetFileName(specPath) != specPath.Substring("Assets/UI/Contracts/".Length))
                throw new InvalidDataException("specPath 必须是 Contracts 根下的 .screen-spec.v3.json 文件名。");
            string specAbsolutePath = ESAutomationPathPolicy.Normalize(specPath);
            ESAutomationPathPolicy.EnsureWorkerReadAllowed(specAbsolutePath, new[] { Path.Combine(ESAutomationPathPolicy.ProjectRoot, "Assets/UI/Contracts") });
            if (!File.Exists(specAbsolutePath)) throw new FileNotFoundException("ScreenSpec 不存在。", specPath);
            string specJson = File.ReadAllText(specAbsolutePath, new UTF8Encoding(false, true));
            JObject spec = JObject.Parse(specJson);
            string prefabPath = ReadGeneratedPath(spec, "prefabPath", "Assets/UI/Prefabs/Generated/", ".prefab");
            string fixturePath = ReadGeneratedPath(spec, "fixtureScenePath", "Assets/UI/Scenes/Generated/", ".unity");
            return new ResolvedRequest
            {
                specPath = specPath,
                specAbsolutePath = specAbsolutePath,
                specJson = specJson,
                specHash = ComputeSha256(specAbsolutePath),
                evidenceRoot = evidenceRoot,
                evidenceAbsolutePath = ESAutomationPathPolicy.Normalize(evidenceRoot),
                prefabAbsolutePath = ESAutomationPathPolicy.Normalize(prefabPath),
                fixtureSceneAbsolutePath = ESAutomationPathPolicy.Normalize(fixturePath),
                profiles = profiles,
                states = states,
            };
        }

        private static string ReadGeneratedPath(JObject spec, string property, string prefix, string extension)
        {
            string value = spec.Value<string>(property) ?? string.Empty;
            if (!value.StartsWith(prefix, StringComparison.Ordinal) || !value.EndsWith(extension, StringComparison.Ordinal)
                || value.Contains("..") || Path.IsPathRooted(value))
                throw new InvalidDataException(property + " 必须位于受控 UI Generated 根目录。");
            return value;
        }

        private static string ReadEvidenceRoot(JObject input)
        {
            string value = ReadSafeString(input, "evidenceRoot");
            if (!System.Text.RegularExpressions.Regex.IsMatch(value, "^ES/UIEvidence/[a-z0-9][a-z0-9-]{0,63}$"))
                throw new InvalidDataException("evidenceRoot 必须是 ES/UIEvidence 下的安全单层目录。");
            return value;
        }

        private static string[] ReadIds(JObject input, string property)
        {
            JArray array = input[property] as JArray;
            if (array == null || array.Count == 0 || array.Count > 16)
                throw new InvalidDataException(property + " 必须是 1 至 16 个字符串 ID。");
            var result = new List<string>();
            foreach (JToken item in array)
            {
                string value = item.Type == JTokenType.String ? item.Value<string>() : string.Empty;
                if (!System.Text.RegularExpressions.Regex.IsMatch(value ?? string.Empty, "^[a-z][a-z0-9-]{0,63}$"))
                    throw new InvalidDataException(property + " 含有非法 ID：" + value);
                if (!result.Contains(value, StringComparer.Ordinal)) result.Add(value);
            }
            return result.ToArray();
        }

        private static string ReadSafeString(JObject input, string property)
        {
            string value = input[property]?.Type == JTokenType.String ? input.Value<string>(property) : string.Empty;
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException(property + " 不能为空。");
            return value.Replace('\\', '/').Trim();
        }

        private static void RequireExact(JObject input, IEnumerable<string> optionalFields)
        {
            var allowed = new HashSet<string>(optionalFields, StringComparer.Ordinal);
            foreach (JProperty property in input.Properties())
                if (!allowed.Contains(property.Name)) throw new InvalidDataException("UI Materializer 输入含未知字段：" + property.Name);
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        internal sealed class Initializer : EditorInvoker_Level0
        {
            public override void InitInvoke() => InitializeForEditor();
        }
    }
}
#endif
