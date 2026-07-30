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
            var libraries = ESEditorSO.GetGroupOfType<ESAssetLibrary>().Where(item => item != null && item.ContainsBuild).OrderBy(item => item.LibFolderName, StringComparer.Ordinal).ToList();
            var catalogs = new List<ESAssetLibraryCatalog>();
            var graphNodes = new Dictionary<string, Dictionary<string, ESAssetReferenceNode>>(StringComparer.Ordinal);
            var bakeWarnings = new List<string>();
            foreach (var library in libraries)
            {
                string folder = ESAssetPipelineIO.LibraryBakeFolder(library.LibFolderName);
                var catalog = ESAssetPipelineIO.ReadJson<ESAssetLibraryCatalog>(Path.Combine(folder, ESAssetPipelineIO.CatalogFileName));
                if (catalog == null || catalog.formatVersion != 3)
                    throw new InvalidDataException("[ESRes][Plan] Catalog 命名协议已过期，请重新烘焙：" + library.LibFolderName);
                if (!string.Equals(catalog.libraryBundleCode, library.AssetBundleCode, StringComparison.Ordinal)
                    || !ESAssetBundleUtility.IsValidLibraryCode(catalog.libraryBundleCode)
                    || string.IsNullOrWhiteSpace(catalog.libraryAssetGuid))
                    throw new InvalidDataException("[ESRes][Plan] Catalog LibraryCode/GUID 无效或已变化，请重新烘焙：" + library.LibFolderName);
                if (catalog.errors.Count > 0) throw new InvalidOperationException($"Library [{catalog.libraryName}] 的烘焙结果包含错误。");
                if (catalog.warnings != null) bakeWarnings.AddRange(catalog.warnings);
                var graph = ESAssetPipelineIO.ReadJson<ESAssetReferenceGraph>(Path.Combine(folder, ESAssetPipelineIO.ReferenceGraphFileName));
                if (graph.warnings != null) bakeWarnings.AddRange(graph.warnings);
                Dictionary<string, ESAssetReferenceNode> nodeIndex = ValidateReferenceGraph(catalog, graph);
                catalogs.Add(catalog);
                graphNodes.Add(catalog.libraryFolder, nodeIndex);
            }

            var plan = new ESAssetBundleBuildPlan { platform = platform, generatedUtc = DateTime.UtcNow.ToString("O") };
            plan.warnings.AddRange(bakeWarnings.Distinct(StringComparer.Ordinal));
            var assetList = new ESAssetBundleAssetList { platform = platform };
            var assignmentByPath = new Dictionary<string, ESAssetBundleAssignment>(StringComparer.Ordinal);
            var dependencyUsages = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var dependencyIdentities = new Dictionary<string, ESPipelineAssetIdentity>(StringComparer.Ordinal);
            var dependencyTypes = new Dictionary<string, string>(StringComparer.Ordinal);
            var collectedGameCorePaths = GetCollectedGameCorePaths();
            var editorOnlyPaths = new HashSet<string>(catalogs.SelectMany(item => item.excludedEditorOnlyPaths ?? new List<string>()), StringComparer.Ordinal);
            var businessPaths = new HashSet<string>(catalogs.SelectMany(item => item.assets).Where(item => item.isBusinessAsset).Select(item => item.assetPath), StringComparer.Ordinal);

            foreach (var catalog in catalogs)
            {
                Dictionary<string, ESAssetReferenceNode> nodeIndex = graphNodes[catalog.libraryFolder];
                foreach (var asset in catalog.assets)
                {
                    // 仍保留在普通 Catalog/Assets 中用于业务寻址；已被 Consumer 收集的 GameCore
                    // 只由专用启动核心包承载，避免同一物理资产重复进入两个 AB。
                    if (collectedGameCorePaths.Contains(asset.assetPath))
                        continue;
                    if (ESAssetPipelineIO.IsEditorOnly(asset.assetPath))
                    {
                        editorOnlyPaths.Add(asset.assetPath);
                        plan.warnings.Add("跳过并清理 EditorOnly 资产的 AB 标签：" + asset.assetPath);
                        continue;
                    }
                    if (!asset.isBusinessAsset && businessPaths.Contains(asset.assetPath)) continue;
                    string bundleKey = GetRootBundleKey(catalog, asset);
                    AddAssignment(plan, assignmentByPath, asset.assetPath, asset.identity, bundleKey, catalog.libraryFolder, asset.isBusinessAsset);
                    ESAssetBundleAssignment effectiveAssignment = assignmentByPath[asset.assetPath];
                    bundleKey = effectiveAssignment.assetBundleKey;
                    if (!assetList.assets.Any(item => item.identity != null && asset.identity != null
                        && string.Equals(item.identity.Key, asset.identity.Key, StringComparison.Ordinal)))
                        assetList.assets.Add(new ESAssetBundleAssetEntry { identity = asset.identity, assetBundleKey = bundleKey, internalName = asset.assetPath, kind = asset.kind, typeName = asset.assetTypeName,
                            ownerLibrary = effectiveAssignment.ownerLibrary, isBusinessAsset = asset.isBusinessAsset, subAssetName = asset.subAssetName });
                    foreach (ESAssetReferenceNode dependency in EnumerateTransitiveDependencies(nodeIndex, asset.assetPath))
                    {
                        string dependencyPath = dependency.assetPath;
                        if (dependency.editorOnly)
                        {
                            editorOnlyPaths.Add(dependencyPath);
                            continue;
                        }
                        if (!dependency.markable || dependency.identity == null || !dependency.identity.IsValid)
                        {
                            plan.warnings.Add("Skipped non-markable Unity planning dependency: " + dependencyPath);
                            continue;
                        }
                        if (!dependencyUsages.TryGetValue(dependencyPath, out var usages)) dependencyUsages.Add(dependencyPath, usages = new HashSet<string>(StringComparer.Ordinal));
                        usages.Add(bundleKey);
                        dependencyIdentities[dependencyPath] = dependency.identity;
                        dependencyTypes[dependencyPath] = dependency.assetTypeName;
                    }
                }
            }

            AddConsumerGameCoreAssignments(plan, assetList, assignmentByPath);

            foreach (var usage in dependencyUsages.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                if (assignmentByPath.ContainsKey(usage.Key)) continue;
                if (usage.Value.Count == 1) continue;
                ESPipelineAssetIdentity sharedIdentity = dependencyIdentities[usage.Key];
                string bundleKey = ESAssetBundleUtility.CreateSpecialBundleKey("shared", Path.GetFileNameWithoutExtension(usage.Key),
                    "shared|" + sharedIdentity.guid + ":" + sharedIdentity.localFileId);
                const string owner = "__shared";
                plan.warnings.Add($"共享依赖独立成包：{usage.Key}，被 {usage.Value.Count} 个 AB 使用。");
                AddAssignment(plan, assignmentByPath, usage.Key, dependencyIdentities[usage.Key], bundleKey, owner, false);
                assetList.assets.Add(new ESAssetBundleAssetEntry { identity = dependencyIdentities[usage.Key], assetBundleKey = bundleKey, internalName = usage.Key,
                    typeName = string.IsNullOrWhiteSpace(dependencyTypes[usage.Key]) ? typeof(UnityEngine.Object).FullName : dependencyTypes[usage.Key], ownerLibrary = owner, isBusinessAsset = false });
            }

            plan.assignments = assignmentByPath.Values.OrderBy(item => item.assetBundleKey, StringComparer.Ordinal).ThenBy(item => item.assetPath, StringComparer.Ordinal).ToList();
            assetList.assets = assetList.assets.OrderBy(item => item.assetBundleKey, StringComparer.Ordinal).ThenBy(item => item.internalName, StringComparer.Ordinal).ThenBy(item => item.identity.localFileId).ToList();
            ValidateBundleKeys(plan);
            AddRenameWarnings(previousPlan, plan);
            if (plan.errors.Count > 0) throw new InvalidOperationException(string.Join("\n", plan.errors));
            ApplyManagedLabels(previousPlan, plan, editorOnlyPaths);
            ESAssetPipelineIO.WriteJson(previousPlanPath, plan);
            ESAssetPipelineIO.WriteJson(Path.Combine(outputFolder, ESAssetPipelineIO.AssetListFileName), assetList);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ESAssetBundleBuildPlanner] 规划 {plan.assignments.Count} 个资产，{plan.warnings.Count} 条警告。输出：{outputFolder}");
        }

        private static HashSet<string> GetCollectedGameCorePaths()
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            IEnumerable<ESAssetLibraryConsumer> consumers = ESEditorSO.GetGroupOfType<ESAssetLibraryConsumer>()
                ?? Enumerable.Empty<ESAssetLibraryConsumer>();
            foreach (ESAssetLibraryConsumer consumer in consumers)
            foreach (ESAssetReferBase refer in consumer?.GameCoreAssets ?? new List<ESAssetReferBase>())
            {
                if (refer == null || !refer.IsValid) continue;
                string path = AssetDatabase.GUIDToAssetPath(refer.GUID);
                if (!string.IsNullOrWhiteSpace(path)) paths.Add(path);
            }
            return paths;
        }

        private static Dictionary<string, ESAssetReferenceNode> ValidateReferenceGraph(ESAssetLibraryCatalog catalog, ESAssetReferenceGraph graph)
        {
            if (graph == null || graph.formatVersion != ESAssetPipelineIO.ReferenceGraphFormatVersion)
                throw new InvalidDataException("[ESRes][Plan] 引用图协议无效，请重新烘焙：" + catalog.libraryFolder);
            if (!string.Equals(graph.libraryFolder, catalog.libraryFolder, StringComparison.Ordinal)
                || !string.Equals(graph.libraryName, catalog.libraryName, StringComparison.Ordinal))
                throw new InvalidDataException("[ESRes][Plan] Catalog 与引用图 Library 身份不一致：" + catalog.libraryFolder);
            if (graph.errors != null && graph.errors.Count > 0)
                throw new InvalidDataException("[ESRes][Plan] 引用图包含错误：" + string.Join("\n", graph.errors));

            var nodes = new Dictionary<string, ESAssetReferenceNode>(StringComparer.Ordinal);
            foreach (ESAssetReferenceNode node in graph.nodes ?? new List<ESAssetReferenceNode>())
            {
                if (node == null || string.IsNullOrWhiteSpace(node.assetPath) || !nodes.TryAdd(node.assetPath, node))
                    throw new InvalidDataException("[ESRes][Plan] 引用图包含空路径或重复节点：" + catalog.libraryFolder);
                ESPipelineAssetIdentity currentIdentity = ESAssetPipelineIO.GetMainIdentity(node.assetPath);
                if (node.identity == null || node.identity.localFileId != 0
                    || !string.Equals(node.identity.guid, currentIdentity.guid, StringComparison.Ordinal))
                    throw new InvalidDataException("[ESRes][Plan] 引用图资产身份已变化，请重新烘焙：" + node.assetPath);
                UnityEngine.Object currentAsset = AssetDatabase.LoadMainAssetAtPath(node.assetPath);
                bool currentEditorOnly = ESAssetPipelineIO.IsEditorOnly(node.assetPath, currentAsset);
                bool currentMarkable = !currentEditorOnly && currentIdentity.IsValid && node.assetPath.StartsWith("Assets/", StringComparison.Ordinal);
                if (node.editorOnly != currentEditorOnly || node.markable != currentMarkable)
                    throw new InvalidDataException("[ESRes][Plan] 引用图资产分类已变化，请重新烘焙：" + node.assetPath);
                string currentHash = AssetDatabase.GetAssetDependencyHash(node.assetPath).ToString();
                if (string.IsNullOrWhiteSpace(node.dependencyHash) || !string.Equals(node.dependencyHash, currentHash, StringComparison.Ordinal))
                    throw new InvalidDataException("[ESRes][Plan] 资产依赖已变化，请重新烘焙：" + node.assetPath);
            }
            foreach (ESAssetReferenceNode node in nodes.Values)
                foreach (string dependencyPath in node.directDependencies ?? new List<string>())
                    if (!nodes.ContainsKey(dependencyPath))
                        throw new InvalidDataException("[ESRes][Plan] 引用图缺少依赖节点：" + node.assetPath + " -> " + dependencyPath);

            var expectedRoots = new HashSet<string>((catalog.assets ?? new List<ESAssetCatalogEntry>())
                .Where(item => item != null && item.identity != null)
                .Select(item => item.identity.Key + "\n" + item.assetPath), StringComparer.Ordinal);
            var actualRoots = new HashSet<string>((graph.roots ?? new List<ESAssetReferenceRoot>())
                .Where(item => item != null && item.identity != null)
                .Select(item => item.identity.Key + "\n" + item.assetPath), StringComparer.Ordinal);
            if (!expectedRoots.SetEquals(actualRoots))
                throw new InvalidDataException("[ESRes][Plan] Catalog 根资产与引用图不一致，请重新烘焙：" + catalog.libraryFolder);
            foreach (ESAssetReferenceRoot root in graph.roots ?? new List<ESAssetReferenceRoot>())
            {
                if (!nodes.ContainsKey(root.assetPath))
                    throw new InvalidDataException("[ESRes][Plan] 引用图根节点缺失：" + root.assetPath);
                if (!IdentityExistsAtPath(root.assetPath, root.identity))
                    throw new InvalidDataException("[ESRes][Plan] 根资产 GUID/LocalFileId 已变化，请重新烘焙：" + root.assetPath);
            }
            return nodes;
        }

        private static bool IdentityExistsAtPath(string assetPath, ESPipelineAssetIdentity identity)
        {
            if (identity == null || !identity.IsValid || string.IsNullOrWhiteSpace(assetPath)) return false;
            if (!identity.IsSubAsset)
                return string.Equals(AssetDatabase.AssetPathToGUID(assetPath), identity.guid, StringComparison.Ordinal);
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                if (asset != null
                    && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localFileId)
                    && string.Equals(guid, identity.guid, StringComparison.Ordinal)
                    && localFileId == identity.localFileId)
                    return true;
            return false;
        }

        private static List<ESAssetReferenceNode> EnumerateTransitiveDependencies(Dictionary<string, ESAssetReferenceNode> nodes, string rootPath)
        {
            if (!nodes.TryGetValue(rootPath, out ESAssetReferenceNode root))
                throw new InvalidDataException("[ESRes][Plan] 引用图中找不到根资产：" + rootPath);

            var result = new List<ESAssetReferenceNode>();
            var visited = new HashSet<string>(StringComparer.Ordinal) { rootPath };
            var stack = new Stack<string>((root.directDependencies ?? new List<string>()).Reverse<string>());
            while (stack.Count > 0)
            {
                string path = stack.Pop();
                if (!visited.Add(path)) continue;
                if (!nodes.TryGetValue(path, out ESAssetReferenceNode node))
                    throw new InvalidDataException("[ESRes][Plan] 引用图遍历遇到缺失节点：" + path);
                result.Add(node);
                if (node.editorOnly) continue;
                List<string> dependencies = node.directDependencies ?? new List<string>();
                for (int i = dependencies.Count - 1; i >= 0; i--)
                    stack.Push(dependencies[i]);
            }
            return result;
        }

        private static void AddAssignment(ESAssetBundleBuildPlan plan, Dictionary<string, ESAssetBundleAssignment> index, string path, ESPipelineAssetIdentity identity, string bundleKey, string owner, bool business)
        {
            ESAssetBundleUtility.RequireValidAssetBundleKey(bundleKey);
            if (index.TryGetValue(path, out var existing))
            {
                if (!string.Equals(existing.assetBundleKey, bundleKey, StringComparison.Ordinal))
                    plan.warnings.Add($"同一物理资产被多个 Library 使用，沿用首个确定归属：{path} -> {existing.assetBundleKey}，忽略 {bundleKey}");
                existing.isBusinessAsset |= business;
                return;
            }
            index.Add(path, new ESAssetBundleAssignment { assetPath = path, identity = identity, assetBundleKey = bundleKey, ownerLibrary = owner, isBusinessAsset = business });
        }

        private static void AddConsumerGameCoreAssignments(ESAssetBundleBuildPlan plan, ESAssetBundleAssetList assetList, Dictionary<string, ESAssetBundleAssignment> assignmentByPath)
        {
            var consumers = ESEditorSO.GetGroupOfType<ESAssetLibraryConsumer>()
                ?.Where(item => item != null && item.GameCoreAssets != null).OrderBy(item => item.ConsumerId, StringComparer.Ordinal)
                ?? Enumerable.Empty<ESAssetLibraryConsumer>();
            foreach (ESAssetLibraryConsumer consumer in consumers)
            {
                if (string.IsNullOrWhiteSpace(consumer.ConsumerId))
                    throw new InvalidOperationException("GameCore Consumer 缺少稳定 ID：" + consumer.Name);

                string owner = ESAssetPipelineIO.GameCoreLibraryFolder(consumer.ConsumerId);
                string gameCoreCode = "gc_" + ESAssetBundleUtility.StableHash(consumer.ConsumerId, 6);
                string bundleKey = ESAssetBundleUtility.CreateSpecialBundleKey(gameCoreCode, "core", "gamecore|" + consumer.ConsumerId);
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

        private static string GetRootBundleKey(ESAssetLibraryCatalog catalog, ESAssetCatalogEntry asset)
        {
            if (asset == null || asset.identity == null || !asset.identity.IsValid)
                throw new InvalidDataException("[ESRes][Plan] Catalog 包含无效命名身份：" + catalog.libraryFolder);
            if (!string.Equals(asset.libraryBundleCode, catalog.libraryBundleCode, StringComparison.Ordinal))
                throw new InvalidDataException("[ESRes][Plan] Catalog Entry 的 LibraryCode 不一致：" + asset.assetPath);

            if (asset.namedOption == ABNamedOption.UseParentPath.ToString())
                return ESAssetBundleUtility.CreateGroupBundleKey(catalog.libraryBundleCode, catalog.libraryAssetGuid,
                    asset.parentFolderPath, asset.namedOption, asset.parentFolderGuid);
            if (asset.namedOption == ABNamedOption.UsePageFolder.ToString())
                return ESAssetBundleUtility.CreateGroupBundleKey(catalog.libraryBundleCode, catalog.libraryAssetGuid,
                    asset.topLevelFolderPath, asset.namedOption, asset.topLevelFolderGuid);

            string hint = asset.namedOption == ABNamedOption.UsePageName.ToString()
                ? asset.pageName
                : Path.GetFileNameWithoutExtension(asset.assetPath);
            return ESAssetBundleUtility.CreateAssetBundleKey(catalog.libraryBundleCode, catalog.libraryAssetGuid,
                asset.parentFolderPath, asset.kind, hint, asset.identity.guid, asset.identity.localFileId);
        }

        private static void ValidateBundleKeys(ESAssetBundleBuildPlan plan)
        {
            var owners = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (IGrouping<string, ESAssetBundleAssignment> group in (plan.assignments ?? new List<ESAssetBundleAssignment>())
                .GroupBy(item => item.assetBundleKey, StringComparer.Ordinal))
            {
                ESAssetBundleUtility.RequireValidAssetBundleKey(group.Key);
                string[] groupOwners = group.Select(item => item.ownerLibrary).Distinct(StringComparer.Ordinal).ToArray();
                if (groupOwners.Length != 1)
                    throw new InvalidDataException("[ESRes][Plan] BundleKey 跨归属冲突：" + group.Key + " -> " + string.Join(",", groupOwners));
                if (!owners.TryAdd(group.Key, groupOwners[0]))
                    throw new InvalidDataException("[ESRes][Plan] BundleKey 重复：" + group.Key);
            }
        }

        private static void AddRenameWarnings(ESAssetBundleBuildPlan previous, ESAssetBundleBuildPlan current)
        {
            if (previous?.assignments == null) return;
            var oldByPath = previous.assignments.Where(item => item != null && !string.IsNullOrWhiteSpace(item.assetPath))
                .GroupBy(item => item.assetPath, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (ESAssetBundleAssignment assignment in current.assignments ?? new List<ESAssetBundleAssignment>())
                if (oldByPath.TryGetValue(assignment.assetPath, out ESAssetBundleAssignment old)
                    && !string.Equals(old.assetBundleKey, assignment.assetBundleKey, StringComparison.Ordinal))
                    current.warnings.Add($"[ESRes][Plan][Rename] {old.assetBundleKey} -> {assignment.assetBundleKey} | {assignment.assetPath}");
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
