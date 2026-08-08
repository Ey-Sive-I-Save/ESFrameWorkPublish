using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ES
{
    [Flags]
    public enum ESAutomationCapability
    {
        None = 0,
        ReadArtifacts = 1 << 0,
        WriteReports = 1 << 1,
        WriteAssets = 1 << 2,
        Delete = 1 << 3,
        Upload = 1 << 4,
        Publish = 1 << 5,
        WriteTemp = 1 << 6,
    }

    [Serializable]
    public sealed class ESAutomationWorkerRegistration
    {
        public string type = string.Empty;
        public string workerId = string.Empty;
        public string version = string.Empty;
        public string entrypointHash = string.Empty;

        // 启用状态属于 C# Editor 本地受信注册表，不属于跨语言 TaskContract JSON。
        [JsonIgnore]
        public bool enabled;

        public void Validate()
        {
            if (type != "Python" && type != "PowerShell" && type != "DotNet" && type != "Other")
                throw new InvalidOperationException("Worker 类型不受支持：" + type);
            if (string.IsNullOrWhiteSpace(workerId) || string.IsNullOrWhiteSpace(version))
                throw new InvalidOperationException("Worker 必须有稳定 ID 和版本。");
            if (!IsSha256(entrypointHash))
                throw new InvalidOperationException("Worker 必须声明 64 位 SHA-256 入口指纹。");
        }

        internal static bool IsSha256(string value)
            => !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, "^[a-fA-F0-9]{64}$");
    }

    [Serializable]
    public sealed class ESAutomationTaskContract
    {
        public const int MaximumTimeoutSeconds = 7200;

        public int protocolVersion = 1;
        public string taskId = string.Empty;
        public int version = 1;
        public ESAutomationWorkerRegistration worker = new ESAutomationWorkerRegistration();
        public List<string> inputs = new List<string>();
        public List<string> readRoots = new List<string>();
        public List<string> writeRoots = new List<string>();
        // 与 JSON Schema 保持字符串数组一致；Flags 只在 C# 受信层按需解析。
        public List<string> capabilities = new List<string>();
        public int timeoutSeconds = 600;
        public bool supportsDryRun = true;
        public List<string> outputs = new List<string>();

        public void Validate()
        {
            if (protocolVersion != 1) throw new InvalidOperationException("不支持的 Automation 任务协议版本。");
            if (string.IsNullOrWhiteSpace(taskId) || !Regex.IsMatch(taskId, "^es\\.[a-z0-9]+(?:\\.[a-z0-9-]+)+$")) throw new InvalidOperationException("TaskId 必须符合 es.<domain>.<name> 的稳定命名。");
            if (version < 1 || timeoutSeconds < 1 || timeoutSeconds > MaximumTimeoutSeconds)
                throw new InvalidOperationException("任务版本和超时必须位于 1–7200 秒范围内。");
            if (worker == null) throw new InvalidOperationException("TaskContract 缺少 Worker 声明。");
            worker.Validate();

            ESAutomationCapability resolvedCapabilities = ResolveCapabilities();
            if ((resolvedCapabilities & ESAutomationCapability.WriteAssets) != 0)
                throw new InvalidOperationException("受管 Worker 不得声明 Unity Assets 写权限。");
            if ((resolvedCapabilities & (ESAutomationCapability.Delete | ESAutomationCapability.Upload | ESAutomationCapability.Publish)) != 0)
                throw new InvalidOperationException("管理骨架阶段禁止注册删除、上传或发布 Worker。");
            if ((resolvedCapabilities & ESAutomationCapability.ReadArtifacts) != 0 && (readRoots == null || readRoots.Count == 0))
                throw new InvalidOperationException("ReadArtifacts 任务必须声明 ReadRoots。");
            if ((resolvedCapabilities & (ESAutomationCapability.WriteReports | ESAutomationCapability.WriteTemp)) != 0 && (writeRoots == null || writeRoots.Count == 0))
                throw new InvalidOperationException("WriteReports 或 WriteTemp 任务必须声明 WriteRoots。");
            foreach (string root in readRoots ?? Enumerable.Empty<string>()) ESAutomationPathPolicy.EnsureDeclaredReadRoot(root);
            foreach (string root in writeRoots ?? Enumerable.Empty<string>()) ESAutomationPathPolicy.EnsureDeclaredWriteRoot(root);
        }

        public ESAutomationCapability ResolveCapabilities()
        {
            ESAutomationCapability result = ESAutomationCapability.None;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string capability in capabilities ?? Enumerable.Empty<string>())
            {
                if (!seen.Add(capability ?? string.Empty)) throw new InvalidOperationException("TaskContract 包含重复能力：" + capability);
                switch (capability)
                {
                    case "ReadArtifacts": result |= ESAutomationCapability.ReadArtifacts; break;
                    case "WriteReports": result |= ESAutomationCapability.WriteReports; break;
                    case "WriteAssets": result |= ESAutomationCapability.WriteAssets; break;
                    case "Delete": result |= ESAutomationCapability.Delete; break;
                    case "Upload": result |= ESAutomationCapability.Upload; break;
                    case "Publish": result |= ESAutomationCapability.Publish; break;
                    case "WriteTemp": result |= ESAutomationCapability.WriteTemp; break;
                    default: throw new InvalidOperationException("TaskContract 包含未知能力：" + capability);
                }
            }
            return result;
        }
    }

    [Serializable]
    public sealed class ESAutomationRunRecord
    {
        public int protocolVersion = 1;
        public string runId = Guid.NewGuid().ToString("N");
        public string taskId = string.Empty;
        public int taskVersion;
        public string operatorId = string.Empty;
        public string gitCommit = string.Empty;
        public string unityVersion = Application.unityVersion;
        public string workerType = string.Empty;
        public string workerId = string.Empty;
        public string workerVersion = string.Empty;
        public string entrypointHash = string.Empty;
        public string inputManifestHash = string.Empty;
        public string status = "Created";
        public int exitCode = -1;
        public string startedAtUtc = string.Empty;
        public string finishedAtUtc = string.Empty;
        public List<string> outputHashes = new List<string>();
        public List<string> findings = new List<string>();
        public List<string> errors = new List<string>();
    }

    [Serializable]
    public sealed class ESAutomationRunResult
    {
        public int protocolVersion = 1;
        public string taskId = string.Empty;
        public int taskVersion;
        public string runId = string.Empty;
        public string workerType = string.Empty;
        public string workerId = string.Empty;
        public string workerVersion = string.Empty;
        public string entrypointHash = string.Empty;
        public string status = "Blocked";
        public int exitCode = -1;
        public string startedAtUtc = string.Empty;
        public string finishedAtUtc = string.Empty;
        public string inputManifestHash = string.Empty;
        public List<string> outputs = new List<string>();
        public List<string> outputHashes = new List<string>();
        public List<string> findings = new List<string>();
        public List<string> errors = new List<string>();

        public void Validate()
        {
            if (protocolVersion != 1) throw new InvalidOperationException("不支持的 Automation 结果协议版本。");
            if (string.IsNullOrWhiteSpace(taskId) || !Regex.IsMatch(taskId, "^es\\.[a-z0-9]+(?:\\.[a-z0-9-]+)+$"))
                throw new InvalidOperationException("RunResult 的 TaskId 无效。");
            if (taskVersion < 1) throw new InvalidOperationException("RunResult 的任务版本无效。");
            if (!Guid.TryParseExact(runId, "N", out _)) throw new InvalidOperationException("RunResult 的 RunId 必须是 N 格式 GUID。");
            new ESAutomationWorkerRegistration
            {
                type = workerType,
                workerId = workerId,
                version = workerVersion,
                entrypointHash = entrypointHash,
            }.Validate();
            if (status != "Passed" && status != "Failed" && status != "Blocked" && status != "Cancelled" && status != "DryRun")
                throw new InvalidOperationException("RunResult 的状态无效：" + status);
            if (string.IsNullOrWhiteSpace(startedAtUtc) || string.IsNullOrWhiteSpace(finishedAtUtc))
                throw new InvalidOperationException("RunResult 必须记录开始和结束 UTC 时间。");
            if (!DateTimeOffset.TryParse(startedAtUtc, out _) || !DateTimeOffset.TryParse(finishedAtUtc, out _))
                throw new InvalidOperationException("RunResult 的 UTC 时间格式无效。");
            if (!ESAutomationWorkerRegistration.IsSha256(inputManifestHash))
                throw new InvalidOperationException("RunResult 必须记录输入 Manifest SHA-256。");
            if (outputs == null || outputHashes == null || findings == null || errors == null)
                throw new InvalidOperationException("RunResult 的集合字段不得为 null。");
            if (outputs.Count != outputHashes.Count)
                throw new InvalidOperationException("RunResult 的输出路径与输出 Hash 数量不一致。");
            foreach (string outputHash in outputHashes)
            {
                if (!ESAutomationWorkerRegistration.IsSha256(outputHash))
                    throw new InvalidOperationException("RunResult 包含无效输出 SHA-256。");
            }
            foreach (string output in outputs)
            {
                if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("RunResult 包含空输出路径。");
            }
        }
    }

    /// <summary>
    /// Python 运行时的受管解析结果。环境变量是本机显式覆盖；项目运行时必须由锁文件和二进制指纹共同识别。
    /// 不回退到 PATH、py launcher 或 Windows Store 别名，避免不同机器静默切换解释器。
    /// </summary>
    public sealed class ESAutomationPythonRuntime
    {
        public string source = string.Empty;
        public string runtimeId = string.Empty;
        public string interpreterPath = string.Empty;
        public string expectedPythonVersion = string.Empty;
        public string expectedInterpreterSha256 = string.Empty;
        public string expectedRuntimeContentSha256 = string.Empty;
        public string detectedPythonVersion = string.Empty;
        public string environmentFingerprint = string.Empty;
    }

    [Serializable]
    internal sealed class ESAutomationPythonRuntimeLock
    {
        // 锁文件反序列化前保持非法值，避免未读取锁文件时被误判为合法协议。
        public int protocolVersion = -1;
        public string runtimeId = string.Empty;
        public string pythonVersion = string.Empty;
        public string interpreterRelativePath = string.Empty;
        public string interpreterSha256 = string.Empty;
        public string runtimeContentSha256 = string.Empty;
        public string requirementsLockRelativePath = string.Empty;
        public string requirementsLockSha256 = string.Empty;
    }

    /// <summary>
    /// 受管 Python 环境解析器。当前 Worker 只使用标准库，但仍锁定解释器；未来第三方依赖必须连同 requirements lock 一起锁定。
    /// </summary>
    public static class ESAutomationPythonEnvironment
    {
        private const string OverrideVariableName = "ES_AUTOMATION_PYTHON";
        private const string LockFileName = "python-runtime.lock.json";
        private const int ProbeTimeoutMilliseconds = 3000;
        private static readonly Regex PythonVersionPattern = new Regex("^[0-9]+\\.[0-9]+(?:\\.[0-9]+)?$", RegexOptions.Compiled);
        private static readonly Regex PythonProbePattern = new Regex("^Python\\s+(3\\.[0-9]+(?:\\.[0-9]+)?)\\s*$", RegexOptions.Compiled | RegexOptions.Multiline);

        public static string ManagedRuntimeRoot => Path.Combine(ESAutomationPathPolicy.ProjectRoot, "ES", "Automation", "Environments", "Python");
        public static string ManagedRuntimeLockPath => Path.Combine(ManagedRuntimeRoot, LockFileName);

        /// <summary>只做轻量路径/锁文件解析，适合 Center OnGUI。启动 Worker 前必须再调用 TryValidateForExecution。</summary>
        public static bool TryResolve(out ESAutomationPythonRuntime runtime, out string reason)
        {
            string overridePath = Environment.GetEnvironmentVariable(OverrideVariableName);
            if (!string.IsNullOrWhiteSpace(overridePath)) return TryResolveLocalOverride(overridePath, out runtime, out reason);
            return TryResolveManagedRuntime(out runtime, out reason);
        }

        /// <summary>启动前的强校验：确认解释器文件指纹，并实际探测 Python 3 版本。</summary>
        public static bool TryValidateForExecution(ESAutomationPythonRuntime runtime, out string reason)
        {
            if (runtime == null || string.IsNullOrWhiteSpace(runtime.interpreterPath))
            {
                reason = "EnvironmentUnavailable：没有可验证的 Python 运行时。";
                return false;
            }
            if (!File.Exists(runtime.interpreterPath))
            {
                reason = "EnvironmentUnavailable：Python 解释器文件不存在。";
                return false;
            }

            string actualHash;
            try
            {
                actualHash = ComputeFileSha256(runtime.interpreterPath);
            }
            catch (Exception exception)
            {
                reason = "EnvironmentUnavailable：无法读取 Python 解释器指纹：" + exception.Message;
                return false;
            }
            if (!string.IsNullOrWhiteSpace(runtime.expectedInterpreterSha256)
                && !string.Equals(actualHash, runtime.expectedInterpreterSha256, StringComparison.OrdinalIgnoreCase))
            {
                reason = "EnvironmentUnavailable：项目受管 Python 的 SHA-256 与锁文件不匹配。";
                return false;
            }
            string runtimeFingerprint = actualHash;
            if (!string.IsNullOrWhiteSpace(runtime.expectedRuntimeContentSha256))
            {
                try
                {
                    string runtimeRoot = Path.GetDirectoryName(runtime.interpreterPath);
                    runtimeFingerprint = ComputeRuntimeContentSha256(runtimeRoot);
                }
                catch (Exception exception)
                {
                    reason = "EnvironmentUnavailable：无法读取受管 Python Runtime 指纹：" + exception.Message;
                    return false;
                }
                if (!string.Equals(runtimeFingerprint, runtime.expectedRuntimeContentSha256, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "EnvironmentUnavailable：项目受管 Python Runtime 内容指纹与锁文件不匹配。";
                    return false;
                }
            }
            if (!TryProbePython3(runtime.interpreterPath, out string detectedVersion, out reason)) return false;
            if (!string.IsNullOrWhiteSpace(runtime.expectedPythonVersion)
                && !VersionMatches(detectedVersion, runtime.expectedPythonVersion))
            {
                reason = "EnvironmentUnavailable：Python 版本与锁文件不匹配，期望 " + runtime.expectedPythonVersion + "，实际 " + detectedVersion + "。";
                return false;
            }

            runtime.detectedPythonVersion = detectedVersion;
            runtime.environmentFingerprint = runtimeFingerprint;
            reason = string.Empty;
            return true;
        }

        private static bool TryResolveLocalOverride(string overridePath, out ESAutomationPythonRuntime runtime, out string reason)
        {
            runtime = null;
            if (!Path.IsPathRooted(overridePath))
            {
                reason = "EnvironmentUnavailable：ES_AUTOMATION_PYTHON 必须是 Python 3 python.exe 的绝对路径。";
                return false;
            }
            string fullPath = Path.GetFullPath(overridePath);
            if (!File.Exists(fullPath) || !string.Equals(Path.GetExtension(fullPath), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                reason = "EnvironmentUnavailable：ES_AUTOMATION_PYTHON 必须指向现有的 python.exe。";
                return false;
            }
            runtime = new ESAutomationPythonRuntime
            {
                source = "local-override",
                runtimeId = OverrideVariableName,
                interpreterPath = fullPath,
            };
            reason = string.Empty;
            return true;
        }

        private static bool TryResolveManagedRuntime(out ESAutomationPythonRuntime runtime, out string reason)
        {
            runtime = null;
            if (!File.Exists(ManagedRuntimeLockPath))
            {
                reason = "EnvironmentUnavailable：未配置 " + OverrideVariableName + "，且项目受管运行时锁文件不存在。请部署 ES/Automation/Environments/Python/python-runtime.lock.json 与其锁定的 Runtime/python.exe。";
                return false;
            }

            ESAutomationPythonRuntimeLock runtimeLock;
            try
            {
                JObject root = JObject.Parse(File.ReadAllText(ManagedRuntimeLockPath, Encoding.UTF8));
                RequireExactProperties(root, new[]
                {
                    "protocolVersion", "runtimeId", "pythonVersion", "interpreterRelativePath", "interpreterSha256",
                    "runtimeContentSha256",
                    "requirementsLockRelativePath", "requirementsLockSha256",
                }, "Python 运行时锁文件");
                runtimeLock = root.ToObject<ESAutomationPythonRuntimeLock>();
                ValidateRuntimeLock(runtimeLock);
            }
            catch (Exception exception)
            {
                reason = "EnvironmentUnavailable：项目 Python 运行时锁文件无效：" + exception.Message;
                return false;
            }

            try
            {
                string interpreterPath = ResolveManagedRelativePath(runtimeLock.interpreterRelativePath);
                if (!File.Exists(interpreterPath))
                {
                    reason = "EnvironmentUnavailable：锁定的项目 Python 解释器不存在：" + runtimeLock.interpreterRelativePath;
                    return false;
                }
                if (!string.IsNullOrWhiteSpace(runtimeLock.requirementsLockRelativePath))
                {
                    string requirementsPath = ResolveManagedRelativePath(runtimeLock.requirementsLockRelativePath);
                    if (!File.Exists(requirementsPath))
                    {
                        reason = "EnvironmentUnavailable：锁定的 Python 依赖文件不存在：" + runtimeLock.requirementsLockRelativePath;
                        return false;
                    }
                    if (!string.Equals(ComputeFileSha256(requirementsPath), runtimeLock.requirementsLockSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        reason = "EnvironmentUnavailable：Python 依赖锁文件 SHA-256 不匹配。";
                        return false;
                    }
                }
                runtime = new ESAutomationPythonRuntime
                {
                    source = "project-managed",
                    runtimeId = runtimeLock.runtimeId,
                    interpreterPath = interpreterPath,
                    expectedPythonVersion = runtimeLock.pythonVersion,
                    expectedInterpreterSha256 = runtimeLock.interpreterSha256,
                    expectedRuntimeContentSha256 = runtimeLock.runtimeContentSha256,
                };
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                reason = "EnvironmentUnavailable：项目受管 Python 路径无效：" + exception.Message;
                return false;
            }
        }

        private static void ValidateRuntimeLock(ESAutomationPythonRuntimeLock runtimeLock)
        {
            if (runtimeLock == null || runtimeLock.protocolVersion != 1 || string.IsNullOrWhiteSpace(runtimeLock.runtimeId))
                throw new InvalidOperationException("protocolVersion 或 runtimeId 无效。");
            if (!PythonVersionPattern.IsMatch(runtimeLock.pythonVersion ?? string.Empty))
                throw new InvalidOperationException("pythonVersion 必须是 x.y 或 x.y.z。\n");
            if (!IsSafeRelativePath(runtimeLock.interpreterRelativePath) || !runtimeLock.interpreterRelativePath.EndsWith("python.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("interpreterRelativePath 必须是指向 python.exe 的安全相对路径。");
            if (!ESAutomationWorkerRegistration.IsSha256(runtimeLock.interpreterSha256))
                throw new InvalidOperationException("interpreterSha256 必须是 64 位 SHA-256。\n");
            if (!ESAutomationWorkerRegistration.IsSha256(runtimeLock.runtimeContentSha256))
                throw new InvalidOperationException("runtimeContentSha256 必须是 64 位 SHA-256。\n");
            bool hasRequirementsPath = !string.IsNullOrWhiteSpace(runtimeLock.requirementsLockRelativePath);
            bool hasRequirementsHash = !string.IsNullOrWhiteSpace(runtimeLock.requirementsLockSha256);
            if (hasRequirementsPath != hasRequirementsHash)
                throw new InvalidOperationException("requirements 锁文件路径与 SHA-256 必须同时存在或同时为空。\n");
            if (hasRequirementsPath && (!IsSafeRelativePath(runtimeLock.requirementsLockRelativePath) || !ESAutomationWorkerRegistration.IsSha256(runtimeLock.requirementsLockSha256)))
                throw new InvalidOperationException("requirements 锁文件配置无效。\n");
        }

        private static string ResolveManagedRelativePath(string relativePath)
        {
            if (!IsSafeRelativePath(relativePath)) throw new InvalidOperationException("路径必须是安全相对路径。");
            string fullPath = Path.GetFullPath(Path.Combine(ManagedRuntimeRoot, relativePath));
            if (!ESAutomationPathPolicy.IsWithin(fullPath, new[] { ManagedRuntimeRoot }))
                throw new UnauthorizedAccessException("路径越出受管 Python 根目录。");
            return fullPath;
        }

        private static bool IsSafeRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) return false;
            foreach (string segment in path.Replace('\\', '/').Split('/'))
            {
                if (string.IsNullOrWhiteSpace(segment) || segment == "." || segment == "..") return false;
            }
            return true;
        }

        private static bool TryProbePython3(string interpreterPath, out string version, out string reason)
        {
            version = string.Empty;
            try
            {
                using (ESManagedEditorProcess process = ESManagedEditorProcessRunner.StartPythonProbe(
                    interpreterPath, ESAutomationPathPolicy.ProjectRoot, 3))
                {
                    if (!process.WaitForExit(ProbeTimeoutMilliseconds))
                    {
                        process.Terminate();
                        reason = "EnvironmentUnavailable：Python --version 探测超时。";
                        return false;
                    }
                    string standardOutput = process.ReadStandardOutputToEnd();
                    string standardError = process.ReadStandardErrorToEnd();
                    string output = (standardOutput + "\n" + standardError).Trim();
                    Match match = PythonProbePattern.Match(output);
                    if (!process.TryGetExitCode(out int exitCode) || exitCode != 0 || !match.Success)
                    {
                        reason = "EnvironmentUnavailable：解释器未返回 Python 3 版本信息。";
                        return false;
                    }
                    version = match.Groups[1].Value;
                    reason = string.Empty;
                    return true;
                }
            }
            catch (Exception exception)
            {
                reason = "EnvironmentUnavailable：无法执行 Python --version：" + exception.Message;
                return false;
            }
        }

        private static bool VersionMatches(string detected, string expected)
        {
            string[] detectedParts = (detected ?? string.Empty).Split('.');
            string[] expectedParts = (expected ?? string.Empty).Split('.');
            if (expectedParts.Length < 2 || detectedParts.Length < expectedParts.Length) return false;
            for (int index = 0; index < expectedParts.Length; index++)
            {
                if (!string.Equals(detectedParts[index], expectedParts[index], StringComparison.Ordinal)) return false;
            }
            return true;
        }

        private static string ComputeFileSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) builder.Append(value.ToString("x2"));
                return builder.ToString();
            }
        }

        private static string ComputeRuntimeContentSha256(string runtimeRoot)
        {
            if (string.IsNullOrWhiteSpace(runtimeRoot) || !Directory.Exists(runtimeRoot))
                throw new DirectoryNotFoundException("受管 Python Runtime 根目录不存在。");
            string normalizedRoot = Path.GetFullPath(runtimeRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string rootPrefix = normalizedRoot + Path.DirectorySeparatorChar;
            string[] files = new List<string>(ESManagedFileIO.EnumerateFilesSafely(normalizedRoot, "*")).ToArray();
            Array.Sort(files, StringComparer.Ordinal);
            var manifest = new StringBuilder(files.Length * 96);
            foreach (string file in files)
            {
                if (!ESAutomationPathPolicy.IsWithin(file, new[] { normalizedRoot }))
                    throw new UnauthorizedAccessException("受管 Python Runtime 包含越界文件。");
                if (!file.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("受管 Python Runtime 包含非根内文件。");
                string relativePath = file.Substring(rootPrefix.Length).Replace('\\', '/');
                manifest.Append(relativePath).Append('\n').Append(ComputeFileSha256(file)).Append('\n');
            }
            byte[] bytes = new UTF8Encoding(false).GetBytes(manifest.ToString());
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) builder.Append(value.ToString("x2"));
                return builder.ToString();
            }
        }

        private static void RequireExactProperties(JObject value, IEnumerable<string> fields, string context)
        {
            var expected = new HashSet<string>(fields, StringComparer.Ordinal);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            foreach (JProperty property in value.Properties()) actual.Add(property.Name);
            if (actual.SetEquals(expected)) return;
            throw new InvalidOperationException(context + " 字段必须与受管协议完全一致。\n");
        }
    }

    public static class ESAutomationTaskRegistry
    {
        private static readonly Dictionary<string, ESAutomationTaskContract> tasks = new Dictionary<string, ESAutomationTaskContract>(StringComparer.Ordinal);

        public static IReadOnlyCollection<ESAutomationTaskContract> Tasks => tasks.Values;

        public static void Register(ESAutomationTaskContract contract)
        {
            if (contract == null) throw new ArgumentNullException(nameof(contract));
            contract.Validate();
            string key = contract.taskId + "@" + contract.version;
            if (tasks.ContainsKey(key)) throw new InvalidOperationException("重复注册 Automation 任务：" + key);
            tasks.Add(key, contract);
        }

        public static bool TryGet(string taskId, int version, out ESAutomationTaskContract contract)
            => tasks.TryGetValue(taskId + "@" + version, out contract);
    }

    public sealed class ESAutomationProcessRequest
    {
        public string taskId = string.Empty;
        public int taskVersion;
        public string runId = string.Empty;
        public bool dryRun = true;
        public string inputContractPath = string.Empty;
    }

    /// <summary>
    /// 一次受管 Worker 进程的唯一所有者。
    ///
    /// 任务 Endpoint 只负责自身的阶段协议与结果文件；解释器启动、进程句柄、超时判断和终止统一由
    /// 执行器负责。它不把 stdout/stderr 当作业务协议，正式结果必须仍由受签名的结构化文件返回。
    /// </summary>
    internal static class ESAutomationRunReservation
    {
        private static readonly object sync = new object();
        private static readonly HashSet<string> activeRunIds = new HashSet<string>(StringComparer.Ordinal);

        public static bool Reserve(string runId)
        {
            if (string.IsNullOrWhiteSpace(runId)) return false;
            lock (sync) return activeRunIds.Add(runId);
        }

        public static void Release(string runId)
        {
            if (string.IsNullOrWhiteSpace(runId)) return;
            lock (sync) activeRunIds.Remove(runId);
        }
    }

    public sealed class ESAutomationProcessExecution : IESManagedProcessHandle
    {
        private readonly string runId;
        private readonly object lifecycleSync = new object();
        private Process process;
        private readonly DateTimeOffset startedAtUtc;
        private readonly int timeoutSeconds;
        private bool terminationRequested;
        private readonly IDisposable processTreeScope;
        private readonly Task<string> standardOutputTask;
        private readonly Task<string> standardErrorTask;

        internal ESAutomationProcessExecution(Process process, int timeoutSeconds, string runId)
        {
            this.process = process ?? throw new ArgumentNullException(nameof(process));
            this.runId = runId ?? string.Empty;
            if (timeoutSeconds < 1 || timeoutSeconds > ESAutomationTaskContract.MaximumTimeoutSeconds)
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "Worker 任务超时必须位于 1–7200 秒范围内。");
            this.timeoutSeconds = timeoutSeconds;
            startedAtUtc = DateTimeOffset.UtcNow;
            processTreeScope = ESManagedProcessTree.Attach(process);
            if (process.StartInfo.RedirectStandardOutput)
                standardOutputTask = ESManagedProcessOutput.ReadToEndBoundedAsync(process.StandardOutput);
            if (process.StartInfo.RedirectStandardError)
                standardErrorTask = ESManagedProcessOutput.ReadToEndBoundedAsync(process.StandardError);
            ESManagedProcessRegistry.Register(this);
        }

        public DateTimeOffset StartedAtUtc => startedAtUtc;
        public int TimeoutSeconds => timeoutSeconds;
        public bool TerminationRequested => terminationRequested;
        public bool HasJobObject => processTreeScope != null;

        public bool HasExited
        {
            get
            {
                lock (lifecycleSync)
                {
                    if (process == null) return true;
                    return process.HasExited;
                }
            }
        }

        public bool HasTimedOut(DateTimeOffset nowUtc)
            => !HasExited && nowUtc - startedAtUtc > TimeSpan.FromSeconds(timeoutSeconds);

        public bool TryGetExitCode(out int exitCode)
        {
            exitCode = -1;
            Process current;
            lock (lifecycleSync) current = process;
            if (current == null || !current.HasExited) return false;
            exitCode = current.ExitCode;
            return true;
        }

        public bool WaitForExit(int milliseconds)
        {
            Process current;
            lock (lifecycleSync) current = process;
            return current != null && current.WaitForExit(milliseconds);
        }

        public string ReadStandardOutputToEnd()
            => ESManagedProcessOutput.GetResult(standardOutputTask, "stdout");

        public string ReadStandardErrorToEnd()
            => ESManagedProcessOutput.GetResult(standardErrorTask, "stderr");

        public bool EnforceTimeout(DateTimeOffset nowUtc)
        {
            if (!HasTimedOut(nowUtc)) return false;
            Terminate();
            return true;
        }

        /// <summary>
        /// 终止受管 Worker 及其可能创建的子进程。
        /// Windows 使用固定系统 taskkill 的 /T /F 进程树边界；其它平台回退到当前进程终止。
        /// 终止失败仍会抛出，让上层把结果标记为“终止未确认”，不能伪装成已取消。
        /// </summary>
        public void Terminate()
        {
            lock (lifecycleSync)
            {
                if (process == null || process.HasExited) return;
                terminationRequested = true;
                ESManagedProcessTree.Terminate(process, processTreeScope);
            }
        }

        public void Dispose()
        {
            lock (lifecycleSync)
            {
                if (process == null) return;
                if (!process.HasExited) Terminate();
                ESManagedProcessRegistry.Unregister(this);
                ESAutomationRunReservation.Release(runId);
                processTreeScope?.Dispose();
                process.Dispose();
                process = null;
            }
        }
    }

    /// <summary>
    /// 只有 C# Editor 代码可注册的受信 Worker 适配器。
    /// 调用方不能提交 executable 或任意命令行；适配器必须自行解析固定入口与环境。
    /// </summary>
    public interface IESAutomationWorkerAdapter
    {
        string WorkerType { get; }
        string WorkerId { get; }
        ProcessStartInfo CreateStartInfo(ESAutomationTaskContract contract, ESAutomationProcessRequest request);
    }

    public static class ESAutomationProcessRunner
    {
        private static readonly Dictionary<string, IESAutomationWorkerAdapter> adapters = new Dictionary<string, IESAutomationWorkerAdapter>(StringComparer.Ordinal);

        public static void RegisterAdapter(IESAutomationWorkerAdapter adapter)
        {
            if (adapter == null || string.IsNullOrWhiteSpace(adapter.WorkerType) || string.IsNullOrWhiteSpace(adapter.WorkerId))
                throw new ArgumentException("受信 WorkerAdapter 必须具备类型和稳定 ID。", nameof(adapter));
            string key = AdapterKey(adapter.WorkerType, adapter.WorkerId);
            if (adapters.ContainsKey(key)) throw new InvalidOperationException("重复注册 WorkerAdapter：" + key);
            adapters.Add(key, adapter);
        }

        public static bool IsAdapterRegistered(string workerType, string workerId)
            => adapters.ContainsKey(AdapterKey(workerType, workerId));

        public static ProcessStartInfo CreateStartInfo(ESAutomationProcessRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!ESAutomationTaskRegistry.TryGet(request.taskId, request.taskVersion, out ESAutomationTaskContract contract))
                throw new InvalidOperationException("未注册的 Automation 任务：" + request.taskId + "@" + request.taskVersion);
            if (contract.worker == null || !contract.worker.enabled)
                throw new InvalidOperationException("Worker 未被 C# Editor 显式启用：" + request.taskId);
            if (string.IsNullOrWhiteSpace(request.runId) || string.IsNullOrWhiteSpace(request.inputContractPath))
                throw new InvalidOperationException("受管运行请求必须具备 RunId 和结构化输入路径。");
            if (!Guid.TryParseExact(request.runId, "N", out _))
                throw new InvalidOperationException("受管运行请求的 RunId 必须是 N 格式 GUID。");
            ESAutomationPathPolicy.EnsureWorkerReadAllowed(request.inputContractPath, contract.readRoots);
            if (!IsAdapterRegistered(contract.worker.type, contract.worker.workerId))
                throw new NotSupportedException("没有已注册的受信 WorkerAdapter：" + AdapterKey(contract.worker.type, contract.worker.workerId));

            return adapters[AdapterKey(contract.worker.type, contract.worker.workerId)].CreateStartInfo(contract, request);
        }

        /// <summary>
        /// 只执行已注册 TaskContract 对应的受信 Adapter。调用方不能提供解释器、脚本或命令行；
        /// Adapter 生成的启动信息还会在此处接受 shell、文件和工作目录的最终约束检查。
        /// </summary>
        public static ESAutomationProcessExecution Start(ESAutomationProcessRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!ESAutomationTaskRegistry.TryGet(request.taskId, request.taskVersion, out ESAutomationTaskContract contract))
                throw new InvalidOperationException("受管 Worker 启动前找不到对应 TaskContract。");
            if (!Guid.TryParseExact(request.runId, "N", out _))
                throw new InvalidOperationException("受管 Worker 启动的 RunId 必须是 N 格式 GUID。");
            if (!ESAutomationRunReservation.Reserve(request.runId))
                throw new InvalidOperationException("同一 RunId 已有受管 Worker 运行，拒绝并发重复启动：" + request.runId);

            Process process = null;
            bool started = false;
            try
            {
                ProcessStartInfo startInfo = CreateStartInfo(request);
                ValidateTrustedStartInfo(startInfo);
                process = new Process { StartInfo = startInfo };
                started = process.Start();
                if (!started) throw new InvalidOperationException("受管 Worker 未能启动。");
                return new ESAutomationProcessExecution(process, contract.timeoutSeconds, request.runId);
            }
            catch
            {
                Exception cleanupFailure = null;
                try
                {
                    if (process != null && started && !process.HasExited) ESManagedProcessTree.Terminate(process, null);
                }
                catch (Exception exception) { cleanupFailure = exception; }
                ESAutomationRunReservation.Release(request.runId);
                process?.Dispose();
                if (cleanupFailure != null)
                    throw new AggregateException("受管 Worker 启动后初始化失败，且进程树终止未确认。", cleanupFailure);
                throw;
            }
        }

        private static void ValidateTrustedStartInfo(ProcessStartInfo startInfo)
        {
            if (startInfo == null) throw new InvalidOperationException("受信 WorkerAdapter 未返回启动信息。");
            if (startInfo.UseShellExecute) throw new InvalidOperationException("受管 Worker 禁止通过 Shell 执行。");
            if (string.IsNullOrWhiteSpace(startInfo.FileName)) throw new InvalidOperationException("受管 Worker 缺少可执行入口。");
            if (!startInfo.RedirectStandardOutput || !startInfo.RedirectStandardError)
                throw new InvalidOperationException("受管 Worker 必须同时重定向 stdout/stderr，由框架统一异步排空并限额采集。");

            string executablePath = Path.GetFullPath(startInfo.FileName);
            if (!File.Exists(executablePath)) throw new FileNotFoundException("受管 Worker 可执行入口不存在。", executablePath);
            if (ESManagedFileIO.ContainsExistingReparsePoint(executablePath))
                throw new UnauthorizedAccessException("受管 Worker 可执行入口不能穿过 junction 或 symlink。");
            startInfo.FileName = executablePath;
            if (string.IsNullOrWhiteSpace(startInfo.WorkingDirectory))
                throw new InvalidOperationException("受管 Worker 必须显式声明项目内工作目录。");
            string workingDirectory = Path.GetFullPath(startInfo.WorkingDirectory);
            if (!Directory.Exists(workingDirectory)) throw new DirectoryNotFoundException("受管 Worker 工作目录不存在：" + workingDirectory);
            if (!ESAutomationPathPolicy.IsWithin(workingDirectory, new[] { ESAutomationPathPolicy.ProjectRoot }))
                throw new UnauthorizedAccessException("受管 Worker 工作目录必须位于项目根目录内。");
            if (ESManagedFileIO.ContainsExistingReparsePoint(workingDirectory))
                throw new UnauthorizedAccessException("受管 Worker 工作目录不能穿过 junction 或 symlink。");
            startInfo.WorkingDirectory = workingDirectory;
        }

        public static void RejectUnregisteredExecution()
            => throw new InvalidOperationException("管理级阶段不执行未注册 Worker；先完成受信 WorkerAdapter、入口指纹、环境版本和权限审查。");

        private static string AdapterKey(string workerType, string workerId) => workerType + "@" + workerId;
    }

    internal sealed class ESAutomationManagedProcessReloadGuard : EditorInvoker_Level0
    {
        public override void InitInvoke()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= TerminateAllBeforeReload;
            AssemblyReloadEvents.beforeAssemblyReload += TerminateAllBeforeReload;
            EditorApplication.quitting -= TerminateAllBeforeReload;
            EditorApplication.quitting += TerminateAllBeforeReload;
        }

        private static void TerminateAllBeforeReload()
        {
            try
            {
                ESManagedProcessRegistry.TerminateAll();
            }
            catch (Exception exception)
            {
                Debug.LogError("[ESAutomation] ReloadDomain 前无法确认所有受管进程已终止：" + exception);
            }
        }
    }

    public static class ESAutomationPathPolicy
    {
        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        public static string ReportsRoot => Path.Combine(ProjectRoot, "ES", "Automation", "Reports");
        public static string TempRoot => Path.Combine(ProjectRoot, "ES", "Automation", "Temp");

        public static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("路径不能为空。", nameof(path));
            if (ContainsParentTraversal(path)) throw new ArgumentException("路径不得包含 .. 段。", nameof(path));
            string candidate = Path.IsPathRooted(path) ? path : Path.Combine(ProjectRoot, path);
            string normalized = Path.GetFullPath(candidate);
            EnsureNoExistingReparsePoint(normalized);
            string root = Path.GetPathRoot(normalized);
            return string.Equals(root, normalized, StringComparison.OrdinalIgnoreCase)
                ? normalized
                : normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static bool IsWithin(string path, IEnumerable<string> roots)
        {
            string candidate = Normalize(path);
            foreach (string root in roots ?? Enumerable.Empty<string>())
            {
                string normalizedRoot = Normalize(root);
                if (string.Equals(candidate, normalizedRoot, StringComparison.OrdinalIgnoreCase)) return true;
                string rootWithSeparator = normalizedRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                    ? normalizedRoot
                    : normalizedRoot + Path.DirectorySeparatorChar;
                if (candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static void EnsureWorkerWriteAllowed(string path, IEnumerable<string> writeRoots)
        {
            string normalized = Normalize(path);
            if (!IsWithin(normalized, new[] { ProjectRoot })) throw new UnauthorizedAccessException("Worker 写入路径必须位于项目根目录内。");
            if (IsWithin(normalized, ProtectedWriteRoots)) throw new InvalidOperationException("Worker 禁止写入受保护目录：" + normalized);
            if (!IsWithin(normalized, writeRoots)) throw new UnauthorizedAccessException("路径不在任务 WriteRoots 内：" + normalized);
        }

        public static void EnsureWorkerReadAllowed(string path, IEnumerable<string> readRoots)
        {
            string normalized = Normalize(path);
            if (!IsWithin(normalized, new[] { ProjectRoot })) throw new UnauthorizedAccessException("Worker 读取路径必须位于项目根目录内。");
            if (!IsWithin(normalized, readRoots)) throw new UnauthorizedAccessException("路径不在任务 ReadRoots 内：" + normalized);
        }

        public static void EnsureWorkerDirectory(string path, IEnumerable<string> writeRoots)
        {
            EnsureWorkerWriteAllowed(path, writeRoots);
            Directory.CreateDirectory(Normalize(path));
        }

        public static void DeleteWorkerFile(string path, IEnumerable<string> writeRoots)
        {
            string normalized = Normalize(path);
            if (!IsWithin(normalized, writeRoots))
                throw new UnauthorizedAccessException("删除路径不在 Worker WriteRoots 内：" + normalized);
            if (File.Exists(normalized)) File.Delete(normalized);
        }

        public static void DeleteWorkerDirectory(string path, IEnumerable<string> writeRoots)
        {
            string normalized = Normalize(path);
            if (!IsWithin(normalized, writeRoots))
                throw new UnauthorizedAccessException("删除目录不在 Worker WriteRoots 内：" + normalized);
            if (!Directory.Exists(normalized)) return;
            string managedRoot = IsWithin(normalized, new[] { ReportsRoot }) ? ReportsRoot : TempRoot;
            ESManagedFileIO.DeleteDirectory(normalized, managedRoot);
        }

        public static void CopyWorkerFileAtomic(string sourcePath, string destinationPath,
            IEnumerable<string> readRoots, IEnumerable<string> writeRoots)
        {
            string source = Normalize(sourcePath);
            string destination = Normalize(destinationPath);
            if (!IsWithin(source, readRoots)) throw new UnauthorizedAccessException("复制源不在 Worker ReadRoots 内：" + source);
            if (!IsWithin(destination, writeRoots)) throw new UnauthorizedAccessException("复制目标不在 Worker WriteRoots 内：" + destination);
            EnsureWorkerDirectory(Path.GetDirectoryName(destination), writeRoots);
            var allowedRoots = (readRoots ?? Enumerable.Empty<string>()).Concat(writeRoots ?? Enumerable.Empty<string>()).ToArray();
            ESManagedFileIO.CopyFileAtomic(source, destination, allowedRoots);
        }

        public static void WriteWorkerTextAtomic(string path, string text, IEnumerable<string> writeRoots)
        {
            string normalized = Normalize(path);
            if (!IsWithin(normalized, writeRoots))
                throw new UnauthorizedAccessException("写入路径不在 Worker WriteRoots 内：" + normalized);
            EnsureWorkerDirectory(Path.GetDirectoryName(normalized), writeRoots);
            ESManagedFileIO.WriteTextAtomic(normalized, text ?? string.Empty, new UTF8Encoding(false), (writeRoots ?? Enumerable.Empty<string>()).ToArray());
        }

        public static void EnsureDeclaredReadRoot(string root)
        {
            string normalized = Normalize(root);
            if (!IsWithin(normalized, new[] { ProjectRoot }))
                throw new UnauthorizedAccessException("ReadRoots 必须位于项目根目录内：" + normalized);
        }

        public static void EnsureDeclaredWriteRoot(string root)
        {
            string normalized = Normalize(root);
            if (!IsWithin(normalized, new[] { ReportsRoot, TempRoot }))
                throw new UnauthorizedAccessException("管理骨架阶段的 WriteRoots 只能位于 ES/Automation/Reports 或 ES/Automation/Temp：" + normalized);
        }

        private static IEnumerable<string> ProtectedWriteRoots
        {
            get
            {
                yield return Application.dataPath;
                yield return Path.Combine(ProjectRoot, "Packages");
                yield return Path.Combine(ProjectRoot, "ProjectSettings");
                yield return Path.Combine(ProjectRoot, "Library");
                yield return Path.Combine(ProjectRoot, "Temp");
                yield return Path.Combine(ProjectRoot, ".git");
            }
        }

        private static bool ContainsParentTraversal(string path)
        {
            foreach (string segment in path.Replace('\\', '/').Split('/'))
            {
                if (segment == "..") return true;
            }
            return false;
        }

        private static void EnsureNoNestedReparsePoint(string directory)
            => ESManagedFileIO.EnsureNoNestedReparsePoints(directory);

        private static void EnsureNoExistingReparsePoint(string path)
        {
            string root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root)) return;
            string current = root;
            string relative = path.Substring(root.Length);
            foreach (string segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                if (string.IsNullOrEmpty(segment)) continue;
                current = Path.Combine(current, segment);
                if (!Directory.Exists(current) && !File.Exists(current)) break;
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new UnauthorizedAccessException("自动化路径不得穿过 junction 或 symlink：" + current);
            }
        }
    }

    public static class ESAutomationReportCenter
    {
        public static string WriteJson(ESAutomationRunResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.Validate();

            string directory = Path.Combine(ESAutomationPathPolicy.ReportsRoot, result.runId);
            string temporaryDirectory = Path.Combine(ESAutomationPathPolicy.TempRoot, result.runId);
            if (Directory.Exists(temporaryDirectory)) throw new IOException("Automation 临时 RunId 目录已存在，拒绝复用：" + result.runId);
            ESAutomationPathPolicy.EnsureWorkerWriteAllowed(directory, new[] { ESAutomationPathPolicy.ReportsRoot });
            ESAutomationPathPolicy.EnsureWorkerWriteAllowed(temporaryDirectory, new[] { ESAutomationPathPolicy.TempRoot });
            if (Directory.Exists(directory)) throw new IOException("Automation 报告 RunId 已存在：" + result.runId);

            try
            {
                ESAutomationPathPolicy.EnsureWorkerDirectory(temporaryDirectory, new[] { ESAutomationPathPolicy.TempRoot });
                string temporaryPath = Path.Combine(temporaryDirectory, "result.json");
                ESAutomationPathPolicy.WriteWorkerTextAtomic(temporaryPath,
                    JsonConvert.SerializeObject(result, Formatting.Indented), new[] { ESAutomationPathPolicy.TempRoot });
                string stagedHash = ComputeSha256(temporaryPath);
                ESAutomationPathPolicy.EnsureWorkerDirectory(ESAutomationPathPolicy.ReportsRoot, new[] { ESAutomationPathPolicy.ProjectRoot });
                Directory.Move(temporaryDirectory, directory);
                string finalPath = Path.Combine(directory, "result.json");
                if (!File.Exists(finalPath) || !string.Equals(stagedHash, ComputeSha256(finalPath), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Automation 报告移动后哈希校验失败：" + finalPath);
                return finalPath;
            }
            catch
            {
                ESAutomationPathPolicy.DeleteWorkerDirectory(temporaryDirectory, new[] { ESAutomationPathPolicy.TempRoot });
                throw;
            }
        }

        private static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) builder.Append(value.ToString("x2"));
                return builder.ToString();
            }
        }
    }

    public static class ESAutomationReleaseGate
    {
        public static bool IsPublishAllowed(ESAutomationRunResult result, out string reason)
        {
            if (result == null) { reason = "缺少运行结果。"; return false; }
            try
            {
                result.Validate();
            }
            catch (Exception exception)
            {
                reason = "Automation 结果协议无效：" + exception.Message;
                return false;
            }
            if (!ESAutomationTaskRegistry.TryGet(result.taskId, result.taskVersion, out ESAutomationTaskContract contract))
            {
                reason = "Automation 结果不属于当前注册任务。";
                return false;
            }
            if (contract.worker == null || !contract.worker.enabled)
            {
                reason = "Automation 结果对应的 Worker 未被 C# Editor 显式启用。";
                return false;
            }
            if (contract.worker.type != result.workerType || contract.worker.workerId != result.workerId || contract.worker.version != result.workerVersion || !string.Equals(contract.worker.entrypointHash, result.entrypointHash, StringComparison.OrdinalIgnoreCase))
            {
                reason = "Automation 结果的 Worker 身份与受信注册不一致。";
                return false;
            }
            if (!string.Equals(result.status, "Passed", StringComparison.OrdinalIgnoreCase)) { reason = "Automation 任务未通过：" + result.status; return false; }
            if (result.exitCode != 0) { reason = "Automation 退出码非 0。"; return false; }
            if (result.errors != null && result.errors.Count > 0) { reason = "Automation 报告包含错误。"; return false; }
            reason = string.Empty;
            return true;
        }
    }

    public sealed class ESAutomationCenterWindow : EditorWindow
    {
        [MenuItem(MenuItemPathDefine.AUTOMATION_PATH + "自动化中心")]
        private static void Open() => GetWindow<ESAutomationCenterWindow>("ES 自动化中心");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("ES 自动化中心", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("C# Editor 是任务权限、路径策略、运行记录和发布门禁的权威入口。仅已注册、入口指纹固定且有受信 Adapter 的 Worker 可执行。", MessageType.Info);
            EditorGUILayout.LabelField("已注册任务", ESAutomationTaskRegistry.Tasks.Count.ToString());
            EditorGUILayout.LabelField("报告目录", ESAutomationPathPolicy.ReportsRoot);
            EditorGUILayout.LabelField("Worker 写 Assets", "禁止");
            EditorGUILayout.LabelField("发布门禁", "失败或缺少结构化报告时阻止");
            EditorGUILayout.LabelField("受管进程", ESManagedProcessRegistry.ActiveCount + " 个（ReloadDomain 前统一终止）");

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("已注册原型", EditorStyles.boldLabel);
            bool hasConfiguredPython = ESAutomationSceneScanPythonAdapter.TryGetConfiguredInterpreter(out _, out string pythonReason);
            EditorGUILayout.HelpBox(
                "执行器：Python 3 · es.scene.scan.python@0.1.0\n"
                + "入口：ES/Automation/Workers/Python/es_scene_scan_worker.py\n"
                + "场景扫描只导出当前 Active Scene 的规范化快照。Python 到达 NeedsInput 检查点后退出，由 C# 高级对话框收集固定报告选项。",
                MessageType.None);
            if (!hasConfiguredPython)
                EditorGUILayout.HelpBox(pythonReason + " 不会使用 PATH、py launcher 或 Windows Store 的 python 占位别名。", MessageType.Warning);
            else
                EditorGUILayout.LabelField("Python 环境", "已解析（启动时会复核 Python 3 版本与受管环境指纹）");
            if (GUILayout.Button("验证 Python 环境", GUILayout.Height(22f)))
            {
                if (ESAutomationSceneScanPythonAdapter.TryPrepareRuntime(out ESAutomationPythonRuntime runtime, out string validationReason))
                    EditorUtility.DisplayDialog("Python 环境可用", "来源：" + runtime.source + "\n运行时：" + runtime.runtimeId + "\n版本：" + runtime.detectedPythonVersion + "\n指纹：" + runtime.environmentFingerprint, "关闭");
                else
                    EditorUtility.DisplayDialog("Python 环境不可用", validationReason, "关闭");
            }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || !hasConfiguredPython))
            {
                if (GUILayout.Button("扫描当前场景（Python 原型）", GUILayout.Height(24f)))
                    ESAutomationSceneScanPrototype.StartSceneScan();
            }
            if (GUILayout.Button("继续待输入的场景扫描", GUILayout.Height(22f)))
                ESAutomationSceneScanPrototype.ResumePendingSceneScan();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("快速任务", EditorStyles.boldLabel);
            Rect quickTaskRect = GUILayoutUtility.GetRect(new GUIContent("选择自动化任务…"), GUI.skin.button, GUILayout.Height(24f));
            if (GUI.Button(quickTaskRect, "选择自动化任务…")) OpenQuickTaskDropdown(quickTaskRect);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("AI 直接调用", EditorStyles.boldLabel);
            bool aiBridgeEnabled = EditorGUILayout.Toggle("授权本机 AI 请求收件箱", ESAutomationAiBridge.IsUserAuthorized);
            if (aiBridgeEnabled != ESAutomationAiBridge.IsUserAuthorized) ESAutomationAiBridge.IsEnabled = aiBridgeEnabled;
            EditorGUILayout.HelpBox(
                aiBridgeEnabled
                    ? ESAutomationAiBridge.ListeningStateDescription + " 受信 AI 仅可通过 ES/Automation/AI/Inbox/*.request.json 调用白名单任务；Unity 会将结构化响应写入 Responses。"
                    : "未授权：AI 请求收件箱默认关闭。首次授权后仍只能调用已注册且 allowAiInvoke 的任务。",
                aiBridgeEnabled ? MessageType.Info : MessageType.Warning);
            EditorGUILayout.LabelField("监听状态", ESAutomationAiBridge.IsListening ? "监听中" : "未监听", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("AI 收件箱", ESAutomationAiBridge.InboxDirectory, EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("复制 AI 调用样例", GUILayout.Height(22f))) CopyAiRequestExample();
        }

        private static void OpenQuickTaskDropdown(Rect anchorRect)
        {
            var entries = new List<ESSearchDropdown.Entry>();
            foreach (ESAutomationTaskDescriptor descriptor in ESAutomationFacade.CopyDescriptors())
            {
                ESAutomationTaskPresetDescriptor defaultPreset = null;
                foreach (ESAutomationTaskPresetDescriptor preset in descriptor.presets)
                {
                    if (preset != null && preset.presetId == "default")
                    {
                        defaultPreset = preset;
                        break;
                    }
                }
                if (defaultPreset == null)
                {
                    entries.Add(ESSearchDropdown.Entry.Disabled(descriptor.displayName, descriptor.category, "该任务没有无输入快速预设。"));
                    continue;
                }

                ESAutomationTaskDescriptor capturedDescriptor = descriptor;
                ESAutomationTaskPresetDescriptor capturedPreset = defaultPreset;
                entries.Add(ESSearchDropdown.Entry.Item(
                    capturedDescriptor.displayName,
                    () => RunQuickTask(capturedDescriptor, capturedPreset),
                    capturedDescriptor.category,
                    subtitle: capturedPreset.summary,
                    tooltip: capturedDescriptor.summary,
                    keywords: capturedDescriptor.taskId,
                    badge: capturedDescriptor.allowAiInvoke ? "AI" : "人工"));
            }
            if (entries.Count == 0) entries.Add(ESSearchDropdown.Entry.Disabled("没有已注册的快速任务"));
            ESSearchDropdown.Open(anchorRect, "快速自动化任务", entries, minimumWindowSize: new Vector2(620f, 340f));
        }

        private static void RunQuickTask(ESAutomationTaskDescriptor descriptor, ESAutomationTaskPresetDescriptor preset)
        {
            ESAutomationTaskInvocationResult result = ESAutomationFacade.RunTask(new ESAutomationTaskInvocation
            {
                taskId = descriptor.taskId,
                taskVersion = descriptor.taskVersion,
                preset = preset.presetId,
                input = new JObject(),
                fromAi = false,
                actorId = "editor.user",
            });
            if (result.status == "Accepted")
            {
                Debug.Log("[ESAutomation] 快速任务已接受：" + descriptor.taskId + " / RunId=" + result.runId);
                SceneView.lastActiveSceneView?.ShowNotification(new GUIContent("已启动：" + descriptor.displayName));
                return;
            }
            EditorUtility.DisplayDialog("自动化任务未启动", result.message, "关闭");
        }

        private static void CopyAiRequestExample()
        {
            EditorGUIUtility.systemCopyBuffer = "{\n"
                + "  \"protocolVersion\": 1,\n"
                + "  \"requestId\": \"<32位GUID，不含连字符>\",\n"
                + "  \"actorId\": \"codex.local\",\n"
                + "  \"action\": \"runTask\",\n"
                + "  \"payload\": {\n"
                + "    \"taskId\": \"es.scene.scan\",\n"
                + "    \"taskVersion\": 1,\n"
                + "    \"preset\": \"default\",\n"
                + "    \"input\": {}\n"
                + "  }\n"
                + "}";
            ShowNotification("AI 调用样例已复制；请替换 requestId 后以 .request.json 原子提交到 Inbox。");
        }

        private static void ShowNotification(string message)
        {
            SceneView.lastActiveSceneView?.ShowNotification(new GUIContent(message));
            Debug.Log("[ESAutomation] " + message);
        }
    }
}
