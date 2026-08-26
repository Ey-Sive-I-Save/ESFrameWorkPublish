using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.Installer;
using HybridCLR.Editor.Settings;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditorInternal;
using UnityEngine;

namespace ES
{
    internal static class ESCodeModuleEditorIntegration
    {
        private static readonly string[] DefaultPatchAotAssemblies = { "mscorlib", "System", "System.Core" };
        private static readonly string[] FrameworkHotUpdateAssemblyDefinitionPaths =
        {
            "Assets/Plugins/ES/1_Design/ES_Design.asmdef",
            "Assets/Scripts/ESLogic/ES_Logic.asmdef",
            "Assets/Scripts/ESPlayer/ESPlayer.asmdef"
        };

        private static readonly (string AssemblyName, int LoadOrder)[] FrameworkHotUpdatePackages =
        {
            ("ES_Design", 1000),
            ("ES_Logic", 1100),
            ("ESPlayer", 1200)
        };

        [Serializable]
        private sealed class AssemblyDefinitionData
        {
            public string name = string.Empty;
        }

        public static bool IsInstalled
        {
            get
            {
                try
                {
                    var installer = new InstallerController();
                    return installer.HasInstalledHybridCLR()
                        && string.Equals(installer.PackageVersion, installer.InstalledLibil2cppVersion, StringComparison.Ordinal);
                }
                catch { return false; }
            }
        }

        public static void InstallForCurrentUnity()
        {
            EnsureSettings(null);
            EnsureHybridCLRInstalledAndCurrent();
        }

        private static void EnsureHybridCLRInstalledAndCurrent()
        {
            var installer = new InstallerController();
            if (installer.GetCompatibleType() == InstallerController.CompatibleType.Incompatible)
                throw new InvalidOperationException("当前 Unity 版本不受代码热更运行环境支持：" + Application.unityVersion);

            if (!installer.HasInstalledHybridCLR())
            {
                Debug.Log("[ESHotUpdate] 首次准备代码热更运行环境，请稍候。");
                installer.InstallDefaultHybridCLR();
            }
            else if (!string.Equals(installer.PackageVersion, installer.InstalledLibil2cppVersion, StringComparison.Ordinal))
            {
                string normalizedInstalledVersion = installer.InstalledLibil2cppVersion?.Trim();
                if (string.Equals(installer.PackageVersion, normalizedInstalledVersion, StringComparison.Ordinal))
                {
                    // HybridCLR 8.12.0 的构建检查使用完全字符串比较；旧版本标记文件可能带换行。
                    // 仅规范化标记即可，不能因此重复下载和重装约 800MB 的本地 IL2CPP 环境。
                    installer.WriteLocalVersion();
                    Debug.Log("[ESHotUpdate] 已自动修复代码热更环境版本标记。版本 " + installer.PackageVersion + "。");
                }
                else
                {
                    Debug.LogWarning("[ESHotUpdate] 检测到代码热更运行环境版本变化，将自动更新。Installed="
                        + (normalizedInstalledVersion ?? "未安装") + ", Package=" + installer.PackageVersion);
                    installer.InstallDefaultHybridCLR();
                }
            }

            // Installer 自己的 Player 构建检查使用完全匹配，因此安装/修复后必须再次验证。
            var verified = new InstallerController();
            if (!verified.HasInstalledHybridCLR()
                || !string.Equals(verified.PackageVersion, verified.InstalledLibil2cppVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("代码热更运行环境自动准备失败。Package=" + verified.PackageVersion
                    + ", Installed=" + (verified.InstalledLibil2cppVersion ?? "未安装"));
            AssetDatabase.Refresh();
            Debug.Log("[ESHotUpdate] 代码热更运行环境准备完成，版本 " + verified.PackageVersion + "。");
        }

        public static void SetConsumerHotUpdateEnabled(ESAssetLibraryConsumer consumer, bool enabled)
        {
            if (consumer == null) throw new ArgumentNullException(nameof(consumer));
            Undo.RecordObject(consumer, "修改 Consumer 代码热更开关");
            consumer.EnableCodeHotUpdate = enabled;
            List<ESAssetLibraryConsumer> consumers = GetAllConsumers();
            if (!consumers.Contains(consumer)) consumers.Add(consumer);
            if (!enabled && consumer.CodePackages != null)
                consumer.CodePackages.RemoveAll(item => item != null
                    && item.ManagedByHybridCLR
                    && string.Equals(item.PackageKey, GetConsumerHotUpdatePackageKey(consumer), StringComparison.Ordinal));
            PrepareSettingsSafely(consumers, false);
            EditorUtility.SetDirty(consumer);
            SavePreparedSettings(consumers);
        }

        public static AssemblyDefinitionAsset GetConsumerAssemblyDefinition(ESAssetLibraryConsumer consumer)
        {
            if (consumer == null || string.IsNullOrEmpty(consumer.HotUpdateAssemblyDefinitionGuid)) return null;
            string path = AssetDatabase.GUIDToAssetPath(consumer.HotUpdateAssemblyDefinitionGuid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(path);
        }

        public static void SetConsumerAssemblyDefinition(ESAssetLibraryConsumer consumer, AssemblyDefinitionAsset definition)
        {
            if (consumer == null) throw new ArgumentNullException(nameof(consumer));
            Undo.RecordObject(consumer, "配置 Consumer 代码模块");
            consumer.EnsureStableIdentity();
            if (definition == null)
            {
                consumer.HotUpdateAssemblyDefinitionGuid = string.Empty;
                consumer.HotUpdateAssemblyName = string.Empty;
                consumer.HotUpdateSourceFolder = string.Empty;
            }
            else
            {
                string path = AssetDatabase.GetAssetPath(definition);
                AssemblyDefinitionData data = JsonUtility.FromJson<AssemblyDefinitionData>(definition.text);
                if (string.IsNullOrWhiteSpace(data?.name)) throw new InvalidOperationException("选择的代码模块无效。");
                consumer.HotUpdateAssemblyDefinitionGuid = AssetDatabase.AssetPathToGUID(path);
                consumer.HotUpdateAssemblyName = data.name.Trim();
                consumer.HotUpdateSourceFolder = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? string.Empty;
            }
            EditorUtility.SetDirty(consumer);
            List<ESAssetLibraryConsumer> consumers = GetAllConsumers();
            PrepareSettingsSafely(consumers, false);
            SavePreparedSettings(consumers);
        }

        public static void PrepareConsumerReleaseCode(
            IEnumerable<ESAssetLibraryConsumer> sourceConsumers,
            string platform)
        {
            List<ESAssetLibraryConsumer> consumers = (sourceConsumers ?? Enumerable.Empty<ESAssetLibraryConsumer>())
                .Where(item => item != null)
                .OrderBy(item => item.ConsumerId, StringComparer.Ordinal)
                .ToList();
            if (consumers.Count == 0)
                throw new InvalidOperationException("准备 Consumer 代码包前至少需要一个 Consumer。");

            string fingerprint = ComputeConsumerPreparationFingerprint(consumers);
            string markerPath = ESAssetPipelineIO.ConsumerReleasePreparationPath(platform);
            ESConsumerReleasePreparationMarker existing = null;
            if (File.Exists(markerPath))
                existing = ESAssetPipelineIO.ReadJson<ESConsumerReleasePreparationMarker>(markerPath);

            if (existing != null
                && existing.formatVersion == 1
                && string.Equals(existing.fingerprint, fingerprint, StringComparison.Ordinal))
            {
                Debug.Log("[ESCodeModule] Consumer 代码包已准备且指纹未变化，跳过重复准备。");
                return;
            }

            bool requiresCode = consumers.Any(consumer =>
                consumer.EnableCodeHotUpdate
                || (consumer.CodePackages?.Any(package => package != null && package.Enabled && package.ManagedByHybridCLR) ?? false));
            if (requiresCode)
            {
                GenerateAndSyncAll(consumers);
            }
            ESAssetConsumerBuildRevision.IncrementAllForBuild();

            string folder = Path.GetDirectoryName(markerPath);
            if (!string.IsNullOrWhiteSpace(folder))
                ESAssetPipelineIO.EnsureGeneratedDirectory(folder);
            ESAssetPipelineIO.WriteJson(markerPath, new ESConsumerReleasePreparationMarker
            {
                platform = platform,
                fingerprint = fingerprint,
                preparedUtc = DateTime.UtcNow.ToString("O"),
                consumerCount = consumers.Count
            }, true);
            Debug.Log("[ESCodeModule] Consumer 代码包准备完成：" + platform + "，Consumer=" + consumers.Count);
        }

        public static void ValidateConsumerReleasePrepared(
            IEnumerable<ESAssetLibraryConsumer> sourceConsumers,
            string platform)
        {
            List<ESAssetLibraryConsumer> consumers = (sourceConsumers ?? Enumerable.Empty<ESAssetLibraryConsumer>())
                .Where(item => item != null)
                .OrderBy(item => item.ConsumerId, StringComparer.Ordinal)
                .ToList();
            string fingerprint = ComputeConsumerPreparationFingerprint(consumers);
            string markerPath = ESAssetPipelineIO.ConsumerReleasePreparationPath(platform);
            if (!File.Exists(markerPath))
                throw new InvalidOperationException(
                    "Consumer 代码包尚未准备。请先执行“Consumer 代码包准备”，再发布资源包。");

            ESConsumerReleasePreparationMarker marker =
                ESAssetPipelineIO.ReadJson<ESConsumerReleasePreparationMarker>(markerPath);
            if (marker == null
                || marker.formatVersion != 1
                || !string.Equals(marker.platform, platform, StringComparison.Ordinal)
                || !string.Equals(marker.fingerprint, fingerprint, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Consumer 代码包准备标记已过期或平台不匹配。请重新执行“Consumer 代码包准备”。");
        }

        public static void GenerateAndSyncAll(IEnumerable<ESAssetLibraryConsumer> sourceConsumers)
        {
            List<ESAssetLibraryConsumer> consumers = (sourceConsumers ?? Enumerable.Empty<ESAssetLibraryConsumer>()).Where(item => item != null).ToList();
            if (consumers.Count == 0)
                throw new InvalidOperationException("生成代码热更包前至少需要一个 Consumer。");
            List<ESAssetLibraryConsumer> enabledConsumers = consumers.Where(item => item.EnableCodeHotUpdate).ToList();
            ESAssetLibraryConsumer frameworkConsumer = consumers.FirstOrDefault(item => item.IsTotalConsumer) ?? consumers[0];
            var consumerSnapshots = consumers.ToDictionary(item => item, item => EditorJsonUtility.ToJson(item));
            HybridCLRSettings settings = HybridCLRSettings.Instance;
            string settingsSnapshot = settings != null ? EditorJsonUtility.ToJson(settings) : string.Empty;
            try
            {
                PrepareSettingsSafely(consumers, true);
                EnsureHybridCLRInstalledAndCurrent();
                PrebuildCommand.GenerateAll();
                if (SyncPatchAotAssembliesFromGeneratedReferences())
                {
                    // 第一次生成得到热更新代码真实使用的 AOT 泛型程序集集合；同步设置后再生成一次，
                    // 确保所有需要补充元数据的 stripped AOT DLL 都真实产出并进入 Consumer。
                    PrebuildCommand.GenerateAll();
                }
                Dictionary<ESAssetLibraryConsumer, int> loadOrders = BuildConsumerLoadOrders(consumers);
                foreach (ESAssetLibraryConsumer consumer in consumers)
                {
                    SyncGeneratedPackages(consumer, ReferenceEquals(consumer, frameworkConsumer), true, loadOrders[consumer]);
                    EditorUtility.SetDirty(consumer);
                }
                SavePreparedSettings(consumers);
                Debug.Log("[ESCodeModule] 已准备框架默认热更程序集 ES_Design、ES_Logic、ESPlayer（不包含 Assembly-CSharp），附加 Consumer 代码模块 "
                    + enabledConsumers.Count + " 个。");
            }
            catch
            {
                foreach (KeyValuePair<ESAssetLibraryConsumer, string> snapshot in consumerSnapshots)
                {
                    if (snapshot.Key == null || string.IsNullOrEmpty(snapshot.Value))
                        continue;
                    EditorJsonUtility.FromJsonOverwrite(snapshot.Value, snapshot.Key);
                    EditorUtility.SetDirty(snapshot.Key);
                }
                if (settings != null && !string.IsNullOrEmpty(settingsSnapshot))
                {
                    EditorJsonUtility.FromJsonOverwrite(settingsSnapshot, settings);
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssetIfDirty(settings);
                }
                throw;
            }
        }

        private static string ComputeConsumerPreparationFingerprint(
            IEnumerable<ESAssetLibraryConsumer> sourceConsumers)
        {
            var builder = new StringBuilder();
            foreach (ESAssetLibraryConsumer consumer in (sourceConsumers ?? Enumerable.Empty<ESAssetLibraryConsumer>())
                .Where(item => item != null)
                .OrderBy(item => item.ConsumerId, StringComparer.Ordinal))
            {
                builder.Append(consumer.ConsumerId ?? string.Empty).Append('\n');
                builder.Append(consumer.IsTotalConsumer).Append('\n');
                builder.Append(consumer.EnableCodeHotUpdate).Append('\n');
                builder.Append(consumer.HotUpdateAssemblyDefinitionGuid ?? string.Empty).Append('\n');
                builder.Append(consumer.HotUpdateAssemblyName ?? string.Empty).Append('\n');
                builder.Append(consumer.HotUpdateSourceFolder ?? string.Empty).Append('\n');
                foreach (ESConsumerCodePackageConfig package in (consumer.CodePackages ?? new List<ESConsumerCodePackageConfig>())
                    .Where(item => item != null)
                    .OrderBy(item => item.PackageKey, StringComparer.Ordinal))
                {
                    builder.Append(package.Enabled).Append('|');
                    builder.Append(package.Kind).Append('|');
                    builder.Append(package.PackageKey ?? string.Empty).Append('|');
                    builder.Append(package.SourcePath ?? string.Empty).Append('|');
                    builder.Append(package.RequiredAtBoot).Append('|');
                    builder.Append(package.LoadOrder).Append('|');
                    builder.Append(package.ManagedByHybridCLR).Append('|');
                    builder.Append(package.Notes ?? string.Empty).Append('\n');
                }
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        public static void OpenConsumerCodeFolder(ESAssetLibraryConsumer consumer)
        {
            RequireConsumerAssemblyDefinition(consumer);
            EditorUtility.RevealInFinder(Path.GetFullPath(consumer.HotUpdateSourceFolder));
        }

        public static string ValidateConsumerInEditor(ESAssetLibraryConsumer consumer)
        {
            if (consumer == null || !consumer.EnableCodeHotUpdate) throw new InvalidOperationException("当前 Consumer 未启用代码热更。");
            RequireConsumerAssemblyDefinition(consumer);
            PrepareSettingsSafely(GetAllConsumers(), true);
            bool assemblyLoaded = AppDomain.CurrentDomain.GetAssemblies()
                .Any(item => string.Equals(item.GetName().Name, consumer.HotUpdateAssemblyName, StringComparison.Ordinal));
            if (!assemblyLoaded)
                throw new InvalidOperationException("代码模块尚未完成编译，请等待脚本刷新后重试。");
            return "检查通过：代码模块已编译，关联关系与加载顺序正常。";
        }

        private static void PrepareSettingsSafely(
            IEnumerable<ESAssetLibraryConsumer> sourceConsumers,
            bool requireAllConfigured)
        {
            List<ESAssetLibraryConsumer> consumers = (sourceConsumers ?? Enumerable.Empty<ESAssetLibraryConsumer>())
                .Where(item => item != null)
                .Distinct()
                .ToList();
            var consumerSnapshots = consumers.ToDictionary(item => item, item => EditorJsonUtility.ToJson(item));
            HybridCLRSettings settings = HybridCLRSettings.Instance;
            string settingsSnapshot = settings != null ? EditorJsonUtility.ToJson(settings) : string.Empty;
            try
            {
                PrepareSettings(consumers, requireAllConfigured);
            }
            catch
            {
                foreach (KeyValuePair<ESAssetLibraryConsumer, string> snapshot in consumerSnapshots)
                {
                    if (snapshot.Key == null || string.IsNullOrEmpty(snapshot.Value))
                        continue;
                    EditorJsonUtility.FromJsonOverwrite(snapshot.Value, snapshot.Key);
                    EditorUtility.SetDirty(snapshot.Key);
                }
                if (settings != null && !string.IsNullOrEmpty(settingsSnapshot))
                {
                    EditorJsonUtility.FromJsonOverwrite(settingsSnapshot, settings);
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssetIfDirty(settings);
                }
                throw;
            }
        }

        private static void PrepareSettings(IEnumerable<ESAssetLibraryConsumer> consumers, bool requireAllConfigured)
        {
            var definitions = new List<AssemblyDefinitionAsset>();
            foreach (ESAssetLibraryConsumer consumer in consumers.Where(item => item != null && item.EnableCodeHotUpdate))
            {
                AssemblyDefinitionAsset definition = GetConsumerAssemblyDefinition(consumer);
                if (definition == null)
                {
                    if (requireAllConfigured) throw new InvalidOperationException("Consumer“" + consumer.Name + "”尚未选择代码模块。");
                    continue;
                }
                RefreshConsumerAssemblyInfo(consumer, definition);
                if (!definitions.Contains(definition)) definitions.Add(definition);
            }
            EnsureSettings(definitions);
        }

        private static AssemblyDefinitionAsset RequireConsumerAssemblyDefinition(ESAssetLibraryConsumer consumer)
        {
            AssemblyDefinitionAsset definition = GetConsumerAssemblyDefinition(consumer);
            if (definition == null) throw new InvalidOperationException("Consumer“" + consumer?.Name + "”尚未选择有效的代码模块。");
            RefreshConsumerAssemblyInfo(consumer, definition);
            return definition;
        }

        private static void RefreshConsumerAssemblyInfo(ESAssetLibraryConsumer consumer, AssemblyDefinitionAsset definition)
        {
            string path = AssetDatabase.GetAssetPath(definition);
            AssemblyDefinitionData data = JsonUtility.FromJson<AssemblyDefinitionData>(definition.text);
            if (string.IsNullOrWhiteSpace(data?.name)) throw new InvalidOperationException("选择的代码模块无效。");
            string assemblyName = data.name.Trim();
            string sourceFolder = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? string.Empty;
            if (string.Equals(consumer.HotUpdateAssemblyName, assemblyName, StringComparison.Ordinal)
                && string.Equals(consumer.HotUpdateSourceFolder, sourceFolder, StringComparison.Ordinal)) return;
            Undo.RecordObject(consumer, "同步 Consumer 代码模块信息");
            consumer.HotUpdateAssemblyName = assemblyName;
            consumer.HotUpdateSourceFolder = sourceFolder;
            EditorUtility.SetDirty(consumer);
        }

        private static void EnsureSettings(IReadOnlyList<AssemblyDefinitionAsset> definitions)
        {
            HybridCLRSettings settings = HybridCLRSettings.Instance;
            Undo.RecordObject(settings, "更新 HybridCLR 设置");
            settings.enable = true;
            settings.hybridclrRepoURL = "https://github.com/focus-creative-games/hybridclr.git";
            settings.il2cppPlusRepoURL = "https://github.com/focus-creative-games/il2cpp_plus.git";
            var mergedDefinitions = new List<AssemblyDefinitionAsset>();
            foreach (string path in FrameworkHotUpdateAssemblyDefinitionPaths)
            {
                AssemblyDefinitionAsset definition = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(path);
                if (definition == null)
                    throw new InvalidOperationException("框架默认热更程序集定义不存在：" + path);
                mergedDefinitions.Add(definition);
            }
            if (definitions != null)
                foreach (AssemblyDefinitionAsset definition in definitions)
                    if (definition != null && !mergedDefinitions.Contains(definition))
                        mergedDefinitions.Add(definition);
            settings.hotUpdateAssemblyDefinitions = mergedDefinitions.ToArray();
            // Assembly-CSharp 不作为热更程序集。默认脚本必须迁移到明确的 asmdef 后再由 Consumer 选择。
            settings.hotUpdateAssemblies = Array.Empty<string>();
            if (settings.patchAOTAssemblies == null || settings.patchAOTAssemblies.Length == 0)
                settings.patchAOTAssemblies = DefaultPatchAotAssemblies;
            EnsureIl2CppBackend();
            HybridCLRSettings.Save();
        }

        private static void EnsureIl2CppBackend()
        {
            BuildTarget activeTarget = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup targetGroup = BuildPipeline.GetBuildTargetGroup(activeTarget);
            if (targetGroup == BuildTargetGroup.Unknown)
                throw new InvalidOperationException("无法确定当前活动平台的 BuildTargetGroup，不能启用 HybridCLR IL2CPP。");

            if (PlayerSettings.GetScriptingBackend(targetGroup) == ScriptingImplementation.IL2CPP)
                return;

            PlayerSettings.SetScriptingBackend(targetGroup, ScriptingImplementation.IL2CPP);
            Debug.Log("[ESHotUpdate] 已自动将 " + targetGroup + " 的脚本后端设置为 IL2CPP。HybridCLR 正式 Player 热更必须使用 IL2CPP。");
        }

        internal static void SynchronizeMethodBridgeDevelopmentFlag(BuildTarget target)
        {
            if (!HybridCLRSettings.Instance.enable)
                return;

            string methodBridgePath = Path.Combine(SettingsUtil.GeneratedCppDir, "MethodBridge.cpp");
            if (!File.Exists(methodBridgePath))
                return;

            Match match = Regex.Match(File.ReadAllText(methodBridgePath), @"// DEVELOPMENT=(\d)");
            if (!match.Success)
                return;

            int generatedFlag = int.Parse(match.Groups[1].Value);
            int requestedFlag = EditorUserBuildSettings.development ? 1 : 0;
            if (generatedFlag == requestedFlag)
                return;

            Debug.Log("[ESHotUpdate][Build] 检测到 Development Build 模式变化，正在自动同步热更桥接代码。"
                + " Generated=" + generatedFlag + ", Requested=" + requestedFlag);

            // Development 切换会同时影响热更 DLL 编译宏和 MethodBridge 模板；这里只重建必要产物，
            // 不重复执行耗时的 StripAOT Player 构建。
            CompileDllCommand.CompileDll(target, EditorUserBuildSettings.development);
            MethodBridgeGeneratorCommand.GenerateMethodBridgeAndReversePInvokeWrapper(target);

            Match verified = Regex.Match(File.ReadAllText(methodBridgePath), @"// DEVELOPMENT=(\d)");
            if (!verified.Success || int.Parse(verified.Groups[1].Value) != requestedFlag)
                throw new BuildFailedException("[ESHotUpdate][Build] MethodBridge Development 标志自动同步失败。请检查 HybridCLR 生成目录。");

            Debug.Log("[ESHotUpdate][Build] 热更桥接代码已同步，可继续构建 Player。Development=" + requestedFlag);
        }

        private static bool SyncPatchAotAssembliesFromGeneratedReferences()
        {
            string generatedPath = Path.Combine(Application.dataPath, "HybridCLRGenerate", "AOTGenericReferences.cs");
            if (!File.Exists(generatedPath))
                throw new FileNotFoundException("HybridCLR 未生成 AOTGenericReferences.cs，无法确认补充元数据清单。", generatedPath);

            string content = File.ReadAllText(generatedPath);
            int listStart = content.IndexOf("PatchedAOTAssemblyList", StringComparison.Ordinal);
            int listEnd = listStart < 0 ? -1 : content.IndexOf("};", listStart, StringComparison.Ordinal);
            if (listStart < 0 || listEnd < 0)
                throw new InvalidDataException("AOTGenericReferences.cs 缺少 PatchedAOTAssemblyList。");

            string listContent = content.Substring(listStart, listEnd - listStart);
            var required = new List<string>(DefaultPatchAotAssemblies);
            foreach (Match match in Regex.Matches(listContent, "\\\"([^\\\"]+)\\.dll\\\""))
            {
                string assemblyName = match.Groups[1].Value;
                if (!required.Contains(assemblyName, StringComparer.Ordinal))
                    required.Add(assemblyName);
            }

            if (required.Count == 0)
                throw new InvalidDataException("HybridCLR 生成的 AOT 补充元数据清单为空。");

            HybridCLRSettings settings = HybridCLRSettings.Instance;
            string[] current = settings.patchAOTAssemblies ?? Array.Empty<string>();
            if (current.SequenceEqual(required, StringComparer.Ordinal)) return false;
            settings.patchAOTAssemblies = required.ToArray();
            HybridCLRSettings.Save();
            Debug.Log("[ESHotUpdate] 已按 AOTGenericReferences 自动同步补充元数据程序集：" + string.Join(", ", required));
            return true;
        }

        private static void SyncGeneratedPackages(ESAssetLibraryConsumer consumer, bool includeFrameworkPackages, bool includeAotMetadata, int consumerLoadOrder)
        {
            consumer.CodePackages ??= new List<ESConsumerCodePackageConfig>();
            consumer.CodePackages.RemoveAll(item => item != null && item.ManagedByHybridCLR);

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            int loadOrder = 0;
            if (includeFrameworkPackages && includeAotMetadata)
            {
                string aotRoot = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);
                foreach (string assemblyName in SettingsUtil.HybridCLRSettings.patchAOTAssemblies ?? Array.Empty<string>())
                    consumer.CodePackages.Add(CreatePackage("es.aot." + assemblyName, ESConsumerCodePackageKind.AotMetadata, Path.Combine(aotRoot, assemblyName + ".dll"), loadOrder++));

                string hotUpdateRoot = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
                foreach ((string assemblyName, int frameworkLoadOrder) in FrameworkHotUpdatePackages)
                    consumer.CodePackages.Add(CreatePackage("es.hot.framework." + assemblyName.ToLowerInvariant(),
                        ESConsumerCodePackageKind.HotUpdateAssembly,
                        Path.Combine(hotUpdateRoot, assemblyName + ".dll"), frameworkLoadOrder));
            }
            if (!consumer.EnableCodeHotUpdate) return;
            if (FrameworkHotUpdatePackages.Any(item => string.Equals(item.AssemblyName, consumer.HotUpdateAssemblyName, StringComparison.Ordinal)))
                return;
            string consumerHotUpdateRoot = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
            consumer.CodePackages.Add(CreatePackage(GetConsumerHotUpdatePackageKey(consumer), ESConsumerCodePackageKind.HotUpdateAssembly,
                Path.Combine(consumerHotUpdateRoot, consumer.HotUpdateAssemblyName + ".dll"), 2000 + consumerLoadOrder,
                // 普通 Consumer 代码按需加载，避免 TotalConsumer 启动时递归加载全部业务 DLL。
                // 总 Consumer 自身代码仍在启动阶段加载；用户手工配置的非托管代码包不受此默认策略影响。
                consumer.IsTotalConsumer));
        }

        private static string GetConsumerHotUpdatePackageKey(ESAssetLibraryConsumer consumer)
        {
            return "es.hot.consumer." + consumer.ConsumerId;
        }

        private static Dictionary<ESAssetLibraryConsumer, int> BuildConsumerLoadOrders(IReadOnlyList<ESAssetLibraryConsumer> consumers)
        {
            var result = new Dictionary<ESAssetLibraryConsumer, int>();
            var visiting = new HashSet<ESAssetLibraryConsumer>();
            int nextOrder = 0;
            foreach (ESAssetLibraryConsumer consumer in consumers.OrderBy(item => item.ConsumerId, StringComparer.Ordinal))
                Visit(consumer);
            return result;

            void Visit(ESAssetLibraryConsumer consumer)
            {
                if (result.ContainsKey(consumer)) return;
                if (!visiting.Add(consumer)) throw new InvalidOperationException("Consumer 代码依赖存在循环：" + consumer.Name);
                foreach (ESAssetLibraryConsumer dependency in (consumer.RequiredConsumers ?? new List<ESAssetLibraryConsumer>())
                    .Where(item => item != null).OrderBy(item => item.ConsumerId, StringComparer.Ordinal))
                    Visit(dependency);
                visiting.Remove(consumer);
                result.Add(consumer, nextOrder++);
            }
        }

        private static ESConsumerCodePackageConfig CreatePackage(string key, ESConsumerCodePackageKind kind, string sourcePath, int loadOrder, bool requiredAtBoot = true)
        {
            return new ESConsumerCodePackageConfig
            {
                Enabled = true,
                PackageKey = key,
                Kind = kind,
                SourcePath = ToProjectRelativePath(sourcePath),
                RequiredAtBoot = requiredAtBoot,
                ManagedByHybridCLR = true,
                LoadOrder = loadOrder,
                Notes = "由 ES 代码热更系统自动维护"
            };
        }

        private static List<ESAssetLibraryConsumer> GetAllConsumers()
        {
            return ESEditorSO.GetGroupOfType<ESAssetLibraryConsumer>()?.Where(item => item != null).ToList()
                ?? new List<ESAssetLibraryConsumer>();
        }

        private static void SavePreparedSettings(IEnumerable<ESAssetLibraryConsumer> consumers)
        {
            foreach (ESAssetLibraryConsumer consumer in (consumers ?? Enumerable.Empty<ESAssetLibraryConsumer>())
                .Where(item => item != null)
                .Distinct())
            {
                AssetDatabase.SaveAssetIfDirty(consumer);
            }
            AssetDatabase.SaveAssetIfDirty(HybridCLRSettings.Instance);
        }

        private static string ToProjectRelativePath(string path)
        {
            string fullPath = Path.GetFullPath(path).Replace('\\', '/');
            string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/').TrimEnd('/') + "/";
            return fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase) ? fullPath.Substring(projectRoot.Length) : fullPath;
        }
    }

    /// <summary>
    /// HybridCLR 的 MethodBridge.cpp 会记录生成时的 Development 标志。
    /// 开发者在 Unity 面板切换 Development Build 后，自动同步这个轻量标志，
    /// 避免被 HybridCLR 的 CheckSettings 以“请手动 Generate/All”阻断。
    /// 完整代码/AOT 生成仍由代码包构建流程负责；内部 StripAOT 临时构建不会触发此同步。
    /// </summary>
    internal sealed class ESHybridCLRDevelopmentFlagSynchronizer : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (EditorUserBuildSettings.buildScriptsOnly)
                return;

            ESCodeModuleEditorIntegration.SynchronizeMethodBridgeDevelopmentFlag(report.summary.platform);
        }
    }
}
