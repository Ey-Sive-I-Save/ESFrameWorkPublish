using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ESFramework.ESAITest;
using UnityEditor;
using UnityEngine;

namespace ES.Editor
{
    public static class ESAITestReportCollector
    {
        private static readonly string[] ReportFileNames =
        {
            "result.json",
            "summary.md",
            "request.json",
            "manifest.json",
        };

        private static ESAITestEditorServices services;

        internal static ESAITestEditorServices Services => services;

        internal static void Attach(ESAITestEditorServices injectedServices)
        {
            services = injectedServices;
        }

        [MenuItem(ES.MenuItemPathDefine.AUTOMATION_PATH + "ESAITest/收集 Player 报告")]
        private static void CollectFromMenu()
        {
            if (services == null)
            {
                Debug.LogError("[ESAITest] 报告收集服务尚未由 AssemblyStream 注入。");
                return;
            }

            string source = EditorUtility.OpenFilePanel("选择 ESAITest Player result.json", string.Empty, "json");
            if (string.IsNullOrWhiteSpace(source))
                return;

            try
            {
                string destination = services.CollectReport(source);
                EditorUtility.RevealInFinder(destination);
                Debug.Log("[ESAITest] 可信报告已收集：" + destination);
            }
            catch (Exception exception)
            {
                Debug.LogError("[ESAITest] 报告收集失败：" + exception);
            }
        }

        [MenuItem(ES.MenuItemPathDefine.AUTOMATION_PATH + "ESAITest/直接启动 ESTEST")]
        private static void DirectStartESTESTFromMenu()
        {
            if (services == null)
            {
                Debug.LogError("[ESAITest] ESTEST 启动服务尚未由 AssemblyStream 注入。");
                return;
            }

            if (!services.StartESTEST(out string error))
                Debug.LogError("[ESAITest] ESTEST 直接启动失败：" + error);
        }

        [MenuItem(ES.MenuItemPathDefine.AUTOMATION_PATH + "ESAITest/中断当前 ESTEST")]
        private static void CancelActiveESTESTFromMenu()
        {
            if (services == null)
            {
                Debug.LogError("[ESAITest] ESTEST 取消服务尚未由 AssemblyStream 注入。");
                return;
            }

            if (!services.CancelActiveRun())
                Debug.LogWarning("[ESAITest] 当前没有可取消的 ESTEST Run。");
        }

        public static string Collect(string sourceResultPath)
        {
            string source = Path.GetFullPath(sourceResultPath ?? string.Empty);
            if (!File.Exists(source))
                throw new FileNotFoundException("ESAITest result.json 不存在。", source);
            if (!string.Equals(Path.GetFileName(source), "result.json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("只能收集 ESAITest Run 目录中的 result.json。");

            ESAITestResultDto result = JsonUtility.FromJson<ESAITestResultDto>(File.ReadAllText(source));
            if (result == null || result.protocolVersion != ESAITestProtocol.CurrentVersion || string.IsNullOrWhiteSpace(result.runId))
                throw new InvalidDataException("ESAITest 报告协议或 runId 无效。");

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string reportsRoot = Path.Combine(projectRoot, "ES", "Automation", "Reports", "ESAITest");
            string tempRoot = Path.Combine(projectRoot, "ES", "Automation", "Temp", "ESAITest");
            string runSegment = SanitizeSegment(result.runId);
            string destinationDirectory = Path.Combine(reportsRoot, runSegment);
            string temporaryDirectory = Path.Combine(tempRoot, runSegment + "." + Guid.NewGuid().ToString("N"));
            EnsureDirectoryUnderRoot(reportsRoot, projectRoot);
            EnsureDirectoryUnderRoot(tempRoot, projectRoot);
            EnsureDirectoryUnderRoot(destinationDirectory, reportsRoot);
            EnsureDirectoryUnderRoot(temporaryDirectory, tempRoot);
            if (Directory.Exists(destinationDirectory))
                throw new IOException("目标 RunId 报告已存在，拒绝覆盖：" + destinationDirectory);

            string sourceDirectory = Path.GetDirectoryName(source);
            if (string.IsNullOrEmpty(sourceDirectory))
                throw new InvalidDataException("无法解析 ESAITest 源报告目录。");

            ValidateArtifacts(result, sourceDirectory);

            string sourceRunSegment = Path.GetFileName(sourceDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!string.Equals(sourceRunSegment, SanitizeSegment(result.runId), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("result.json 所在目录与报告 runId 不一致，拒绝收集。");

            try
            {
                for (int i = 0; i < ReportFileNames.Length; i++)
                {
                    string fileName = ReportFileNames[i];
                    string sourceFile = Path.Combine(sourceDirectory, fileName);
                    if (!File.Exists(sourceFile))
                        throw new InvalidDataException("ESAITest Run 报告不完整，缺少：" + fileName);
                    CopyReportFile(sourceFile, Path.Combine(temporaryDirectory, fileName));
                }

                string sourceArtifacts = Path.Combine(sourceDirectory, "artifacts");
                if (Directory.Exists(sourceArtifacts))
                    CopyDirectory(sourceArtifacts, Path.Combine(temporaryDirectory, "artifacts"));

                Directory.Move(temporaryDirectory, destinationDirectory);
            }
            catch
            {
                DeleteReportDirectory(temporaryDirectory, tempRoot);
                throw;
            }

            return Path.Combine(destinationDirectory, "result.json");
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            EnsureDirectoryUnderRoot(sourceDirectory, sourceDirectory);
            EnsureNoNestedReparsePoints(sourceDirectory);
            EnsureDirectoryUnderRoot(destinationDirectory, Path.GetDirectoryName(destinationDirectory) ?? destinationDirectory);
            foreach (string directory in ESManagedFileIO.EnumerateDirectoriesSafely(sourceDirectory))
            {
                string relative = directory.Substring(sourceDirectory.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                EnsureDirectoryUnderRoot(Path.Combine(destinationDirectory, relative), destinationDirectory);
            }

            foreach (string file in ESManagedFileIO.EnumerateFilesSafely(sourceDirectory, "*"))
            {
                string relative = file.Substring(sourceDirectory.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string destination = Path.Combine(destinationDirectory, relative);
                string parent = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(parent))
                    EnsureDirectoryUnderRoot(parent, destinationDirectory);
                CopyReportFile(file, destination);
            }
        }

        private static void ValidateArtifacts(ESAITestResultDto result, string sourceDirectory)
        {
            ESAITestArtifactDto[] artifacts = result.artifacts ?? Array.Empty<ESAITestArtifactDto>();
            var declared = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < artifacts.Length; i++)
            {
                ESAITestArtifactDto artifact = artifacts[i];
                if (artifact == null || string.IsNullOrWhiteSpace(artifact.relativePath))
                    throw new InvalidDataException("Artifact Manifest 包含空条目或空路径。");

                string relative = artifact.relativePath.Replace('/', Path.DirectorySeparatorChar);
                if (Path.IsPathRooted(relative)
                    || !relative.StartsWith("artifacts" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("Artifact 路径必须位于 artifacts/：" + artifact.relativePath);
                }

                string fullPath = Path.GetFullPath(Path.Combine(sourceDirectory, relative));
                string artifactsRoot = Path.GetFullPath(Path.Combine(sourceDirectory, "artifacts"));
                string rootPrefix = artifactsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Artifact 路径越过报告目录边界：" + artifact.relativePath);
                if (!declared.Add(fullPath))
                    throw new InvalidDataException("Artifact Manifest 重复声明：" + artifact.relativePath);
                if (!File.Exists(fullPath))
                    throw new InvalidDataException("Artifact 文件不存在：" + artifact.relativePath);

                if (ContainsExistingReparsePoint(sourceDirectory, fullPath))
                    throw new InvalidDataException("Artifact 文件不能位于 junction/symlink：" + artifact.relativePath);
                var info = new FileInfo(fullPath);
                if (info.Length != artifact.byteLength)
                    throw new InvalidDataException("Artifact 字节数不一致：" + artifact.relativePath);
                string actualHash = ComputeSha256(fullPath);
                if (!string.Equals(actualHash, artifact.sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Artifact SHA-256 不一致：" + artifact.relativePath);
            }

            string artifactsDirectory = Path.Combine(sourceDirectory, "artifacts");
            if (!Directory.Exists(artifactsDirectory))
                return;
            if (ContainsExistingReparsePoint(sourceDirectory, artifactsDirectory))
                throw new InvalidDataException("artifacts/ 目录不能位于 junction/symlink。");
            EnsureNoNestedReparsePoints(artifactsDirectory);
            int fileCount = 0;
            foreach (string ignored in ESManagedFileIO.EnumerateFilesSafely(artifactsDirectory, "*"))
                fileCount++;
            if (fileCount != declared.Count)
                throw new InvalidDataException("artifacts/ 文件数量与 Artifact Manifest 不一致。");
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        private static void EnsureDirectoryUnderRoot(string path, string root)
        {
            string candidate = Path.GetFullPath(path);
            string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(candidate, rootFull, StringComparison.OrdinalIgnoreCase)
                && !candidate.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("ESAITest 报告路径越出受管目录：" + path);
            if (ContainsExistingReparsePoint(rootFull, candidate))
                throw new UnauthorizedAccessException("ESAITest 报告路径不能穿过 junction/symlink：" + path);
            Directory.CreateDirectory(candidate);
        }

        private static void CopyReportFile(string source, string destination)
        {
            string sourceFull = Path.GetFullPath(source);
            string sourceRoot = Path.GetDirectoryName(sourceFull) ?? sourceFull;
            if (!File.Exists(sourceFull) || ContainsExistingReparsePoint(sourceRoot, sourceFull))
                throw new InvalidDataException("ESAITest 报告源文件无效或位于 junction/symlink：" + source);
            if (File.Exists(destination))
                throw new IOException("ESAITest 报告目标文件已存在：" + destination);
            string destinationRoot = Path.GetDirectoryName(Path.GetFullPath(destination)) ?? destination;
            ESManagedFileIO.CopyFileAtomic(sourceFull, destination, sourceRoot, destinationRoot);
        }

        private static void DeleteReportDirectory(string path, string root)
        {
            string candidate = Path.GetFullPath(path);
            string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(candidate)) return;
            if (!candidate.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("ESAITest 临时目录清理越界：" + path);
            if (ContainsExistingReparsePoint(rootFull, candidate))
                throw new UnauthorizedAccessException("ESAITest 临时目录不能穿过 junction/symlink：" + path);
            ESManagedFileIO.DeleteDirectory(candidate, root);
        }

        internal static bool ContainsExistingReparsePoint(string root, string candidate)
        {
            string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if ((Directory.Exists(rootFull) || File.Exists(rootFull))
                && (File.GetAttributes(rootFull) & FileAttributes.ReparsePoint) != 0)
                return true;
            string current = rootFull;
            string relative = Path.GetFullPath(candidate).Substring(rootFull.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (string segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                if (string.IsNullOrEmpty(segment)) continue;
                current = Path.Combine(current, segment);
                if (!Directory.Exists(current) && !File.Exists(current)) break;
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            }
            return false;
        }

        private static void EnsureNoNestedReparsePoints(string directory)
            => ESManagedFileIO.EnsureNoNestedReparsePoints(directory);

        private static string SanitizeSegment(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            char[] characters = value.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
                if (Array.IndexOf(invalid, characters[i]) >= 0)
                    characters[i] = '_';
            string result = new string(characters);
            if (string.IsNullOrWhiteSpace(result) || result == "." || result == "..")
                throw new InvalidDataException("runId 不能作为安全目录名。");
            return result;
        }
    }

    public sealed class ESAITestEditorServices
    {
        private const string PendingPlanKey = "ESAITest.PendingPlanPath";
        private const string PendingStartKey = "ESAITest.PendingStart";
        private const string PendingDirectESTESTKey = "ESAITest.PendingDirectESTEST";

        public void Initialize()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            if (EditorApplication.isPlaying)
                TryStartPendingPlan();
        }

        public bool StartPlan(string planPath, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(planPath) || !File.Exists(planPath))
            {
                error = "请选择存在的 ESAITest 计划 JSON。";
                return false;
            }

            string fullPath = Path.GetFullPath(planPath);
            if (EditorApplication.isPlaying)
                return ESAITestPlayerBootstrap.TryStartFromPath(fullPath, false, out error);

            SessionState.SetString(PendingPlanKey, fullPath);
            SessionState.SetBool(PendingStartKey, true);
            SessionState.SetBool(PendingDirectESTESTKey, false);
            EditorApplication.EnterPlaymode();
            return true;
        }

        public bool StartESTEST(out string error)
        {
            error = string.Empty;
            if (EditorApplication.isPlaying)
                return ESAITestPlayerBootstrap.TryStartESTEST(out error);

            SessionState.SetString(PendingPlanKey, string.Empty);
            SessionState.SetBool(PendingStartKey, true);
            SessionState.SetBool(PendingDirectESTESTKey, true);
            EditorApplication.EnterPlaymode();
            return true;
        }

        public bool CancelActiveRun()
        {
            return ESAITestPlayerBootstrap.RequestCancel();
        }

        public string CollectReport(string sourceResultPath)
        {
            return ESAITestReportCollector.Collect(sourceResultPath);
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                TryStartPendingPlan();
        }

        private static void TryStartPendingPlan()
        {
            if (!SessionState.GetBool(PendingStartKey, false))
                return;

            string path = SessionState.GetString(PendingPlanKey, string.Empty);
            bool directESTEST = SessionState.GetBool(PendingDirectESTESTKey, false);
            SessionState.SetBool(PendingStartKey, false);
            SessionState.SetBool(PendingDirectESTESTKey, false);
            SessionState.EraseString(PendingPlanKey);
            string error;
            bool started = directESTEST
                ? ESAITestPlayerBootstrap.TryStartESTEST(out error)
                : ESAITestPlayerBootstrap.TryStartFromPath(path, false, out error);
            if (!started)
                Debug.LogError("[ESAITest] PlayMode 计划启动失败：" + error);
        }
    }

    public sealed class ESAITestEditorAssemblyStreamRegistration
        : EditorRegister_FOR_Singleton<ESAITestEditorServices>
    {
        public override int Order => EditorRegisterOrder.Level2.GetHashCode();

        public override void Handle(ESAITestEditorServices singleton)
        {
            ESAITestReportCollector.Attach(singleton);
            singleton.Initialize();
        }
    }

    public sealed class ESAITestControlCenterWindow : EditorWindow
    {
        private string planPath;
        private Vector2 scroll;
        private bool attributedManifestExpanded = true;

        [MenuItem(ES.MenuItemPathDefine.AUTOMATION_PATH + "ESAITest/控制中心")]
        private static void Open()
        {
            GetWindow<ESAITestControlCenterWindow>("ESAITest Control Center");
        }

        private void OnEnable()
        {
            planPath = SessionState.GetString("ESAITest.ControlCenter.PlanPath", string.Empty);
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            SessionState.SetString("ESAITest.ControlCenter.PlanPath", planPath ?? string.Empty);
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("ESAITest 商业验收控制中心", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("AI 可直接启动内建 ESTEST 基线，也可选择明确 JSON 计划。进入 PlayMode 后统一由 Player Runner 执行；运行中可从本窗口或 GameView 驾驶台安全中断。", MessageType.Info);

            ESAITestEditorServices services = GetServices();
            using (new EditorGUI.DisabledScope(services == null || ESAITestPlayerBootstrap.ActiveRunner != null))
            {
                if (GUILayout.Button("AI 直接启动 ESTEST", GUILayout.Height(38f)))
                {
                    if (!services.StartESTEST(out string directError))
                        Debug.LogError("[ESAITest] ESTEST 直接启动失败：" + directError);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("计划入口", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            planPath = EditorGUILayout.TextField("Plan JSON", planPath);
            if (GUILayout.Button("选择", GUILayout.Width(64f)))
            {
                string selected = EditorUtility.OpenFilePanel("选择 ESAITest Plan", string.Empty, "json");
                if (!string.IsNullOrEmpty(selected))
                    planPath = selected;
            }
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(planPath)))
            {
                if (GUILayout.Button(EditorApplication.isPlaying ? "在当前 PlayMode 启动" : "进入 PlayMode 并启动", GUILayout.Height(34f)))
                {
                    ESAITestEditorServices service = GetServices();
                    string error;
                    if (service == null)
                    {
                        error = "AssemblyStream 服务未注入。";
                    }
                    else if (service.StartPlan(planPath, out error))
                    {
                        error = null;
                    }

                    if (!string.IsNullOrEmpty(error))
                        Debug.LogError("[ESAITest] 启动失败：" + error);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("运行状态", EditorStyles.boldLabel);
            ESAITestRunner runner = ESAITestPlayerBootstrap.ActiveRunner;
            if (runner == null)
            {
                EditorGUILayout.HelpBox(EditorApplication.isPlaying ? "当前没有 ESAITest Run。" : "尚未进入 PlayMode。", MessageType.None);
            }
            else
            {
                EditorGUILayout.LabelField("RunId", runner.RunId);
                EditorGUILayout.LabelField("PlanId", runner.PlanId);
                EditorGUILayout.LabelField("进度", runner.CompletedStepCount + " / " + runner.TotalStepCount);
                EditorGUILayout.LabelField("当前 Step", runner.CurrentStepId);
                EditorGUILayout.LabelField("当前操作", runner.CurrentOperation);
                EditorGUILayout.LabelField("耗时", runner.ElapsedSeconds.ToString("F2") + " 秒");
                EditorGUILayout.HelpBox(runner.CurrentMessage ?? string.Empty, MessageType.Info);
                using (new EditorGUI.DisabledScope(!runner.IsRunning || runner.CancellationRequested))
                {
                    GUI.backgroundColor = new Color(1f, 0.42f, 0.42f);
                    if (GUILayout.Button("安全中断当前 Run", GUILayout.Height(32f)))
                        GetServices()?.CancelActiveRun();
                    GUI.backgroundColor = Color.white;
                }
            }

            EditorGUILayout.Space();
            DrawAttributedCapabilityManifest();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("其他入口", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel("-esAITestPlan <absolute-plan-path> -esAITestQuit", EditorStyles.textField, GUILayout.Height(20f));
            EditorGUILayout.SelectableLabel("-esAITestInbox [optional-plan-path]", EditorStyles.textField, GUILayout.Height(20f));
            EditorGUILayout.SelectableLabel("-esTest [-esAITestQuit]", EditorStyles.textField, GUILayout.Height(20f));
            EditorGUILayout.SelectableLabel("$es-start-estest / ESAITest_直接启动ESTEST_AI命令.md", EditorStyles.textField, GUILayout.Height(20f));
            EditorGUILayout.EndScrollView();
        }

        private void DrawAttributedCapabilityManifest()
        {
            attributedManifestExpanded = EditorGUILayout.Foldout(
                attributedManifestExpanded,
                "AssemblyStream 三类能力清单（Editor Authoring）",
                true);
            if (!attributedManifestExpanded)
                return;

            ESAITestAttributedCapabilityManifestDto manifest =
                ESAITestAttributedCapabilityRegistry.GetManifestSnapshot();
            MessageType messageType = manifest.rejectedCount > 0 ? MessageType.Warning : MessageType.Info;
            EditorGUILayout.HelpBox(
                "该清单只证明 Editor AssemblyStream 已发现并校验源码声明；Player Runtime 仍只使用显式 Capability Provider，不执行全程序集反射。",
                messageType);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("发现", manifest.discoveredCount.ToString());
            EditorGUILayout.LabelField("接受", manifest.acceptedCount.ToString());
            EditorGUILayout.LabelField("拒绝", manifest.rejectedCount.ToString());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ToUse", manifest.toUseCount.ToString());
            EditorGUILayout.LabelField("ToSee", manifest.toSeeCount.ToString());
            EditorGUILayout.LabelField("ToVerify", manifest.toVerifyCount.ToString());
            EditorGUILayout.EndHorizontal();

            string summary = ESAITestAttributedCapabilityRegistry.BuildDenseSummary();
            int lineCount = Mathf.Clamp(summary.Split('\n').Length, 8, 28);
            EditorGUILayout.SelectableLabel(
                summary,
                EditorStyles.textArea,
                GUILayout.MinHeight(lineCount * EditorGUIUtility.singleLineHeight));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("复制 Manifest JSON"))
                EditorGUIUtility.systemCopyBuffer = ESAITestAttributedCapabilityRegistry.GetManifestJson(true);
            if (GUILayout.Button("显式导出 Manifest..."))
                ExportAttributedCapabilityManifest();
            EditorGUILayout.EndHorizontal();
        }

        private static void ExportAttributedCapabilityManifest()
        {
            string path = EditorUtility.SaveFilePanel(
                "导出 ESAITest AssemblyStream 能力清单",
                string.Empty,
                "esaitest-attributed-capabilities.json",
                "json");
            if (string.IsNullOrWhiteSpace(path))
                return;

            WriteSelectedTextAtomic(path, ESAITestAttributedCapabilityRegistry.GetManifestJson(true));
            EditorUtility.RevealInFinder(path);
        }

        private static void WriteSelectedTextAtomic(string path, string text)
        {
            string full = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(full);
            if (string.IsNullOrEmpty(directory)
                || ESAITestReportCollector.ContainsExistingReparsePoint(directory, full))
                throw new UnauthorizedAccessException("ESAITest Manifest 输出路径不安全：" + path);
            ESManagedFileIO.WriteTextAtUserSelectedPath(full, text ?? string.Empty, new UTF8Encoding(false));
        }

        private static ESAITestEditorServices GetServices()
        {
            return ESAITestReportCollector.Services;
        }
    }
}
