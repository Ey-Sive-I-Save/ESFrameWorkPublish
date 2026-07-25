using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.Installer;
using HybridCLR.Editor.Settings;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace ES
{
    internal static class ESCodeModuleEditorIntegration
    {
        private static readonly string[] DefaultPatchAotAssemblies = { "mscorlib", "System", "System.Core" };

        [Serializable]
        private sealed class AssemblyDefinitionData
        {
            public string name = string.Empty;
        }

        public static bool IsInstalled
        {
            get
            {
                try { return new InstallerController().HasInstalledHybridCLR(); }
                catch { return false; }
            }
        }

        public static void InstallForCurrentUnity()
        {
            EnsureSettings(null);
            var installer = new InstallerController();
            if (installer.GetCompatibleType() == InstallerController.CompatibleType.Incompatible)
                throw new InvalidOperationException("当前 Unity 版本不受代码热更运行环境支持：" + Application.unityVersion);
            if (!installer.HasInstalledHybridCLR()) installer.InstallDefaultHybridCLR();
            AssetDatabase.Refresh();
            Debug.Log("[ESHotUpdate] 代码热更运行环境安装完成，版本 " + installer.PackageVersion + "。");
        }

        public static void SetConsumerHotUpdateEnabled(ESAssetLibraryConsumer consumer, bool enabled)
        {
            if (consumer == null) throw new ArgumentNullException(nameof(consumer));
            consumer.EnableCodeHotUpdate = enabled;
            List<ESAssetLibraryConsumer> consumers = GetAllConsumers();
            if (!consumers.Contains(consumer)) consumers.Add(consumer);
            if (!enabled && consumer.CodePackages != null)
                consumer.CodePackages.RemoveAll(item => item != null && item.ManagedByHybridCLR && item.Kind == ESConsumerCodePackageKind.HotUpdateAssembly);
            if (!consumers.Any(item => item.EnableCodeHotUpdate))
                foreach (ESAssetLibraryConsumer item in consumers)
                    if ((item.CodePackages?.RemoveAll(package => package != null && package.ManagedByHybridCLR) ?? 0) > 0)
                        EditorUtility.SetDirty(item);
            PrepareSettings(consumers, false);
            EditorUtility.SetDirty(consumer);
            AssetDatabase.SaveAssets();
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
            PrepareSettings(GetAllConsumers(), false);
            AssetDatabase.SaveAssets();
        }

        public static void GenerateAndSyncAll(IEnumerable<ESAssetLibraryConsumer> sourceConsumers)
        {
            List<ESAssetLibraryConsumer> consumers = (sourceConsumers ?? Enumerable.Empty<ESAssetLibraryConsumer>()).Where(item => item != null).ToList();
            List<ESAssetLibraryConsumer> enabledConsumers = consumers.Where(item => item.EnableCodeHotUpdate).ToList();
            if (enabledConsumers.Count == 0)
            {
                PrepareSettings(consumers, false);
                foreach (ESAssetLibraryConsumer consumer in consumers)
                {
                    if (consumer.CodePackages == null || consumer.CodePackages.RemoveAll(item => item != null && item.ManagedByHybridCLR) == 0) continue;
                    EditorUtility.SetDirty(consumer);
                }
                AssetDatabase.SaveAssets();
                return;
            }

            PrepareSettings(consumers, true);
            if (!IsInstalled) InstallForCurrentUnity();
            PrebuildCommand.GenerateAll();
            Dictionary<ESAssetLibraryConsumer, int> loadOrders = BuildConsumerLoadOrders(consumers);
            foreach (ESAssetLibraryConsumer consumer in consumers)
            {
                SyncGeneratedPackages(consumer, true, loadOrders[consumer]);
                EditorUtility.SetDirty(consumer);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[ESCodeModule] 已准备 " + enabledConsumers.Count + " 个 Consumer 的代码模块。");
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
            PrepareSettings(GetAllConsumers(), true);
            bool assemblyLoaded = AppDomain.CurrentDomain.GetAssemblies()
                .Any(item => string.Equals(item.GetName().Name, consumer.HotUpdateAssemblyName, StringComparison.Ordinal));
            if (!assemblyLoaded)
                throw new InvalidOperationException("代码模块尚未完成编译，请等待脚本刷新后重试。");
            return "检查通过：代码模块已编译，关联关系与加载顺序正常。";
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
            consumer.HotUpdateAssemblyName = assemblyName;
            consumer.HotUpdateSourceFolder = sourceFolder;
            EditorUtility.SetDirty(consumer);
        }

        private static void EnsureSettings(IReadOnlyList<AssemblyDefinitionAsset> definitions)
        {
            HybridCLRSettings settings = HybridCLRSettings.Instance;
            settings.enable = true;
            settings.hybridclrRepoURL = "https://github.com/focus-creative-games/hybridclr.git";
            settings.il2cppPlusRepoURL = "https://github.com/focus-creative-games/il2cpp_plus.git";
            if (definitions != null)
            {
                settings.hotUpdateAssemblyDefinitions = definitions.ToArray();
                settings.hotUpdateAssemblies = Array.Empty<string>();
            }
            if (settings.patchAOTAssemblies == null || settings.patchAOTAssemblies.Length == 0)
                settings.patchAOTAssemblies = DefaultPatchAotAssemblies;
            HybridCLRSettings.Save();
        }

        private static void SyncGeneratedPackages(ESAssetLibraryConsumer consumer, bool includeAotMetadata, int consumerLoadOrder)
        {
            consumer.CodePackages ??= new List<ESConsumerCodePackageConfig>();
            consumer.CodePackages.RemoveAll(item => item != null && item.ManagedByHybridCLR);

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            int loadOrder = 0;
            if (consumer.IsTotalConsumer && includeAotMetadata)
            {
                string aotRoot = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);
                foreach (string assemblyName in SettingsUtil.HybridCLRSettings.patchAOTAssemblies ?? Array.Empty<string>())
                    consumer.CodePackages.Add(CreatePackage("es.aot." + assemblyName, ESConsumerCodePackageKind.AotMetadata, Path.Combine(aotRoot, assemblyName + ".dll"), loadOrder++));
            }
            if (!consumer.EnableCodeHotUpdate) return;
            string hotUpdateRoot = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
            consumer.CodePackages.Add(CreatePackage("es.hot." + consumer.ConsumerId, ESConsumerCodePackageKind.HotUpdateAssembly,
                Path.Combine(hotUpdateRoot, consumer.HotUpdateAssemblyName + ".dll"), 1000 + consumerLoadOrder));
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

        private static ESConsumerCodePackageConfig CreatePackage(string key, ESConsumerCodePackageKind kind, string sourcePath, int loadOrder)
        {
            return new ESConsumerCodePackageConfig
            {
                Enabled = true,
                PackageKey = key,
                Kind = kind,
                SourcePath = ToProjectRelativePath(sourcePath),
                RequiredAtBoot = true,
                ManagedByHybridCLR = true,
                LoadOrder = loadOrder,
                Notes = "由 ES 代码热更系统自动维护"
            };
        }

        private static List<ESAssetLibraryConsumer> GetAllConsumers()
        {
            return ESEditorSO.SOS.GetNewGroupOfType<ESAssetLibraryConsumer>()?.Where(item => item != null).ToList()
                ?? new List<ESAssetLibraryConsumer>();
        }

        private static string ToProjectRelativePath(string path)
        {
            string fullPath = Path.GetFullPath(path).Replace('\\', '/');
            string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/').TrimEnd('/') + "/";
            return fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase) ? fullPath.Substring(projectRoot.Length) : fullPath;
        }
    }
}
