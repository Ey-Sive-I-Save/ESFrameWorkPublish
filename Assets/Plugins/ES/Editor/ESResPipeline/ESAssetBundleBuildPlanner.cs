using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public static class ESAssetBundleBuildPlanner
    {
        public static void PlanAndMark()
        {
            string platform = ESAssetPipelineIO.PlatformName;
            string outputFolder = ESAssetPipelineIO.PlanRoot(platform);
            string previousPlanPath = Path.Combine(outputFolder, ESAssetPipelineIO.PlanFileName);
            ESAssetBundleBuildPlan previousPlan = File.Exists(previousPlanPath) ? ESAssetPipelineIO.ReadJson<ESAssetBundleBuildPlan>(previousPlanPath) : null;
            var libraries = ESEditorSO.SOS.GetNewGroupOfType<ESAssetLibrary>().Where(item => item != null && item.ContainsBuild).OrderBy(item => item.LibFolderName, StringComparer.Ordinal).ToList();
            var catalogs = new List<ESAssetLibraryCatalog>();
            foreach (var library in libraries)
            {
                string folder = ESAssetPipelineIO.LibraryBakeFolder(library.LibFolderName);
                var catalog = ESAssetPipelineIO.ReadJson<ESAssetLibraryCatalog>(Path.Combine(folder, ESAssetPipelineIO.CatalogFileName));
                if (catalog.errors.Count > 0) throw new InvalidOperationException($"Library [{catalog.libraryName}] 的烘焙结果包含错误。");
                catalogs.Add(catalog);
            }

            var plan = new ESAssetBundleBuildPlan { platform = platform, generatedUtc = DateTime.UtcNow.ToString("O") };
            var assetList = new ESAssetBundleAssetList { platform = platform };
            var assignmentByPath = new Dictionary<string, ESAssetBundleAssignment>(StringComparer.Ordinal);
            var dependencyUsages = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var dependencyIdentities = new Dictionary<string, ESPipelineAssetIdentity>(StringComparer.Ordinal);
            var editorOnlyPaths = new HashSet<string>(catalogs.SelectMany(item => item.excludedEditorOnlyPaths ?? new List<string>()), StringComparer.Ordinal);
            var businessPaths = new HashSet<string>(catalogs.SelectMany(item => item.assets).Where(item => item.isBusinessAsset).Select(item => item.assetPath), StringComparer.Ordinal);

            foreach (var catalog in catalogs)
            {
                foreach (var asset in catalog.assets)
                {
                    if (ESAssetPipelineIO.IsEditorOnly(asset.assetPath))
                    {
                        editorOnlyPaths.Add(asset.assetPath);
                        plan.warnings.Add("跳过并清理 EditorOnly 资产的 AB 标签：" + asset.assetPath);
                        continue;
                    }
                    if (!asset.isBusinessAsset && businessPaths.Contains(asset.assetPath)) continue;
                    string bundleKey = GetRootBundleKey(asset);
                    AddAssignment(plan, assignmentByPath, asset.assetPath, asset.identity, bundleKey, catalog.libraryFolder, asset.isBusinessAsset);
                    assetList.assets.Add(new ESAssetBundleAssetEntry { identity = asset.identity, assetBundleKey = bundleKey, internalName = asset.assetPath, kind = asset.kind, typeName = asset.assetTypeName,
                        ownerLibrary = catalog.libraryFolder, isBusinessAsset = asset.isBusinessAsset, subAssetName = asset.subAssetName });
                    foreach (string dependencyPath in AssetDatabase.GetDependencies(asset.assetPath, true)
                        .Where(path => !string.Equals(path, asset.assetPath, StringComparison.Ordinal))
                        .Distinct(StringComparer.Ordinal))
                    {
                        if (ESAssetPipelineIO.IsEditorOnly(dependencyPath))
                        {
                            editorOnlyPaths.Add(dependencyPath);
                            continue;
                        }
                        var dependencyIdentity = ESAssetPipelineIO.GetMainIdentity(dependencyPath);
                        if (!dependencyIdentity.IsValid || !dependencyPath.StartsWith("Assets/", StringComparison.Ordinal))
                        {
                            plan.warnings.Add("Skipped non-markable Unity planning dependency: " + dependencyPath);
                            continue;
                        }
                        if (!dependencyUsages.TryGetValue(dependencyPath, out var usages)) dependencyUsages.Add(dependencyPath, usages = new HashSet<string>(StringComparer.Ordinal));
                        usages.Add(bundleKey);
                        dependencyIdentities[dependencyPath] = dependencyIdentity;
                    }
                }
            }

            AddConsumerGameCoreAssignments(plan, assetList, assignmentByPath);

            foreach (var usage in dependencyUsages.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                if (assignmentByPath.ContainsKey(usage.Key)) continue;
                if (usage.Value.Count == 1) continue;
                string guid = dependencyIdentities[usage.Key].guid;
                string bundleKey = ESAssetBundleUtility.ToSafeAssetBundleKey("__shared/" + (guid.Length > 12 ? guid.Substring(0, 12) : guid));
                const string owner = "__shared";
                plan.warnings.Add($"共享依赖独立成包：{usage.Key}，被 {usage.Value.Count} 个 AB 使用。");
                AddAssignment(plan, assignmentByPath, usage.Key, dependencyIdentities[usage.Key], bundleKey, owner, false);
                assetList.assets.Add(new ESAssetBundleAssetEntry { identity = dependencyIdentities[usage.Key], assetBundleKey = bundleKey, internalName = usage.Key,
                    typeName = (AssetDatabase.GetMainAssetTypeAtPath(usage.Key) ?? typeof(UnityEngine.Object)).FullName, ownerLibrary = owner, isBusinessAsset = false });
            }

            plan.assignments = assignmentByPath.Values.OrderBy(item => item.assetBundleKey, StringComparer.Ordinal).ThenBy(item => item.assetPath, StringComparer.Ordinal).ToList();
            assetList.assets = assetList.assets.OrderBy(item => item.assetBundleKey, StringComparer.Ordinal).ThenBy(item => item.internalName, StringComparer.Ordinal).ThenBy(item => item.identity.localFileId).ToList();
            if (plan.errors.Count > 0) throw new InvalidOperationException(string.Join("\n", plan.errors));
            ApplyManagedLabels(previousPlan, plan, editorOnlyPaths);
            ESAssetPipelineIO.WriteJson(previousPlanPath, plan);
            ESAssetPipelineIO.WriteJson(Path.Combine(outputFolder, ESAssetPipelineIO.AssetListFileName), assetList);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ESAssetBundleBuildPlanner] 规划 {plan.assignments.Count} 个资产，{plan.warnings.Count} 条警告。输出：{outputFolder}");
        }

        private static void AddAssignment(ESAssetBundleBuildPlan plan, Dictionary<string, ESAssetBundleAssignment> index, string path, ESPipelineAssetIdentity identity, string bundleKey, string owner, bool business)
        {
            if (index.TryGetValue(path, out var existing))
            {
                if (!string.Equals(existing.assetBundleKey, bundleKey, StringComparison.Ordinal)) plan.errors.Add($"同一资产被规划到多个 AB：{path} -> {existing.assetBundleKey} / {bundleKey}");
                existing.isBusinessAsset |= business;
                return;
            }
            index.Add(path, new ESAssetBundleAssignment { assetPath = path, identity = identity, assetBundleKey = bundleKey, ownerLibrary = owner, isBusinessAsset = business });
        }

        private static void AddConsumerGameCoreAssignments(ESAssetBundleBuildPlan plan, ESAssetBundleAssetList assetList, Dictionary<string, ESAssetBundleAssignment> assignmentByPath)
        {
            var consumers = ESEditorSO.SOS.GetNewGroupOfType<ESAssetLibraryConsumer>()
                ?.Where(item => item != null && item.GameCoreAssets != null).OrderBy(item => item.ConsumerId, StringComparer.Ordinal)
                ?? Enumerable.Empty<ESAssetLibraryConsumer>();
            foreach (ESAssetLibraryConsumer consumer in consumers)
            {
                if (string.IsNullOrWhiteSpace(consumer.ConsumerId))
                    throw new InvalidOperationException("GameCore Consumer 缺少稳定 ID：" + consumer.Name);

                string owner = ESAssetPipelineIO.GameCoreLibraryFolder(consumer.ConsumerId);
                string bundleKey = ESAssetBundleUtility.ToSafeAssetBundleKey(owner + "/core");
                foreach (ESAssetReferBase refer in consumer.GameCoreAssets)
                {
                    if (refer == null || !refer.IsValid)
                        continue;
                    string path = AssetDatabase.GUIDToAssetPath(refer.GUID);
                    if (string.IsNullOrWhiteSpace(path) || ESAssetPipelineIO.IsEditorOnly(path))
                        throw new InvalidOperationException("GameCore 资产无效或不可发布：Consumer=" + consumer.Name + ", GUID=" + refer.GUID);
                    UnityEngine.Object asset = refer.LocalFileId == 0 ? AssetDatabase.LoadMainAssetAtPath(path) : FindSubAsset(path, refer.GUID, refer.LocalFileId);
                    if (!(asset is ScriptableObject) || ESScriptableObjectClassification.GetClass((ScriptableObject)asset) != ESScriptableObjectClass.GameCore)
                        throw new InvalidOperationException("GameCore 清单包含非 IGameCoreSO 资产：Consumer=" + consumer.Name + ", Path=" + path);

                    var identity = new ESPipelineAssetIdentity { guid = refer.GUID, localFileId = refer.LocalFileId };
                    if (assignmentByPath.TryGetValue(path, out ESAssetBundleAssignment existing)
                        && !string.Equals(existing.assetBundleKey, bundleKey, StringComparison.Ordinal))
                        throw new InvalidOperationException("同一 IGameCoreSO 不能归属多个 Consumer 启动核心包：Path=" + path
                            + ", Existing=" + existing.ownerLibrary + ", Consumer=" + consumer.Name);
                    AddAssignment(plan, assignmentByPath, path, identity, bundleKey, owner, true);
                    assetList.assets.Add(new ESAssetBundleAssetEntry
                    {
                        identity = identity,
                        assetBundleKey = bundleKey,
                        internalName = path,
                        kind = ESAssetReferKind.ScriptableObject.ToString(),
                        typeName = asset.GetType().FullName,
                        ownerLibrary = owner,
                        isBusinessAsset = true,
                        subAssetName = identity.IsSubAsset ? asset.name : string.Empty
                    });
                }
            }
        }

        private static UnityEngine.Object FindSubAsset(string path, string guid, long localFileId)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string candidateGuid, out long candidateId)
                    && string.Equals(candidateGuid, guid, StringComparison.Ordinal) && candidateId == localFileId)
                    return asset;
            return null;
        }

        private static string GetRootBundleKey(ESAssetCatalogEntry asset)
        {
            string raw;
            if (asset.namedOption == ABNamedOption.UsePageName.ToString()) raw = asset.pageName;
            else if (asset.namedOption == ABNamedOption.UseParentPath.ToString() || asset.namedOption == ABNamedOption.UsePageFolder.ToString()) raw = Path.GetDirectoryName(asset.assetPath);
            else raw = asset.assetPath;
            return ESAssetBundleUtility.ToSafeAssetBundleKey(asset.libraryFolder + "/" + raw);
        }

        private static void ApplyManagedLabels(ESAssetBundleBuildPlan previous, ESAssetBundleBuildPlan current, IEnumerable<string> editorOnlyPaths)
        {
            var currentPaths = new HashSet<string>(current.assignments.Select(item => item.assetPath), StringComparer.Ordinal);
            if (previous != null)
            foreach (var old in previous.assignments)
            {
                if (currentPaths.Contains(old.assetPath)) continue;
                var importer = AssetImporter.GetAtPath(old.assetPath);
                if (importer != null && string.Equals(importer.assetBundleName, old.assetBundleKey, StringComparison.Ordinal)) ClearBundleLabel(importer);
            }
            foreach (string path in editorOnlyPaths.Distinct(StringComparer.Ordinal))
            {
                var importer = AssetImporter.GetAtPath(path);
                if (importer != null && (!string.IsNullOrEmpty(importer.assetBundleName) || !string.IsNullOrEmpty(importer.assetBundleVariant))) ClearBundleLabel(importer);
            }
            foreach (var assignment in current.assignments)
            {
                var importer = AssetImporter.GetAtPath(assignment.assetPath);
                if (importer == null) throw new InvalidOperationException("AssetImporter 不存在：" + assignment.assetPath);
                if (!string.Equals(importer.assetBundleName, assignment.assetBundleKey, StringComparison.Ordinal)) importer.assetBundleName = assignment.assetBundleKey;
                if (!string.IsNullOrEmpty(importer.assetBundleVariant)) importer.assetBundleVariant = string.Empty;
            }
            AssetDatabase.RemoveUnusedAssetBundleNames();
        }
        private static void ClearBundleLabel(AssetImporter importer)
        {
            importer.assetBundleName = string.Empty;
            importer.assetBundleVariant = string.Empty;
        }
    }
}
