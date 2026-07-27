using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public static class ESAssetReferenceBaker
    {
        public static void SyncConsumerGameCoreAssets(ESAssetLibraryConsumer consumer)
        {
            if (consumer == null) throw new ArgumentNullException(nameof(consumer));
            List<ESAssetLibrary> libraries = ESEditorSO.SOS.GetNewGroupOfType<ESAssetLibrary>()
                ?.Where(item => item != null).ToList() ?? new List<ESAssetLibrary>();
            List<string> errors = SyncConsumerGameCoreCatalog(consumer, libraries);
            AssetDatabase.SaveAssets();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        }

        public static bool TryAddManualGameCoreAsset(ESAssetLibraryConsumer consumer, UnityEngine.Object asset)
        {
            if (consumer == null || !(asset is ScriptableObject scriptableObject)
                || ESScriptableObjectClassification.GetClass(scriptableObject) != ESScriptableObjectClass.GameCore)
                return false;
            ESPipelineAssetIdentity identity = ESAssetPipelineIO.GetIdentity(asset);
            if (!identity.IsValid) return false;
            consumer.ManualGameCoreAssets ??= new List<ESAssetReferBase>();
            if (consumer.ManualGameCoreAssets.Any(item => item != null && item.AssetIdentity.Equals(new ESAssetIdentity(identity.guid, identity.localFileId))))
                return true;
            var refer = new ESAssetReferScriptableObject();
            refer.InitializeGeneratedReference(identity.guid, identity.localFileId, ESAssetReferKind.ScriptableObject, 0, string.Empty);
            consumer.ManualGameCoreAssets.Add(refer);
            EditorUtility.SetDirty(consumer);
            return true;
        }

        public static bool TryAddResidentAsset(ESAssetLibraryConsumer consumer, UnityEngine.Object asset, out string error)
        {
            error = string.Empty;
            if (consumer == null || asset == null)
            {
                error = "Consumer 或资产为空。";
                return false;
            }

            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrWhiteSpace(path) || ESAssetPipelineIO.IsEditorOnly(path, asset))
            {
                error = "脚本、EditorOnly 或无效资产不能作为启动常驻资产。";
                return false;
            }
            if (asset is SceneAsset)
            {
                error = "Scene 不能作为常驻对象加载，请使用场景加载流程。";
                return false;
            }
            if (asset is ScriptableObject scriptableObject
                && ESScriptableObjectClassification.GetClass(scriptableObject) == ESScriptableObjectClass.GameCore)
            {
                error = "IGameCoreSO 应放入 GameCoreAssets，不能重复放入 ResidentAssets。";
                return false;
            }

            ESPipelineAssetIdentity identity = ESAssetPipelineIO.GetIdentity(asset);
            if (!identity.IsValid)
            {
                error = "资产缺少有效 GUID/LocalFileId。";
                return false;
            }

            ESAssetPage page = null;
            foreach (ESAssetReferKind kind in Enum.GetValues(typeof(ESAssetReferKind)))
                if (kind != ESAssetReferKind.None
                    && ESAssetRegistry.TryGetByAssetIdentity(kind, identity.guid, identity.localFileId, out page))
                    break;
            if (page == null)
            {
                error = "资产尚未注册到 AssetLibrary，请先完成资源注册。";
                return false;
            }

            consumer.ResidentAssets ??= new List<ESAssetReferBase>();
            if (consumer.ResidentAssets.Any(item => item != null && item.AssetIdentity.Equals(new ESAssetIdentity(identity.guid, identity.localFileId))))
                return true;

            var refer = new ESAssetReferUnityObject();
            refer.InitializeGeneratedReference(identity.guid, identity.localFileId, page.Kind, page.EnumKey, page.EffectiveStringKey);
            consumer.ResidentAssets.Add(refer);
            EditorUtility.SetDirty(consumer);
            return true;
        }

        /// <summary>
        /// 烘焙入口改为编辑器长任务。每帧只推进一个 Library、Consumer 或输出文件；
        /// AssetDatabase 的原子调用仍保持在主线程，避免额外线程同步和 GC 压力。
        /// </summary>
        public static ESEditorLongTask Bake()
        {
            var libraries = ESEditorSO.SOS.GetNewGroupOfType<ESAssetLibrary>()
                .Where(item => item != null && item.ContainsBuild)
                .OrderBy(item => item.LibFolderName, StringComparer.Ordinal)
                .ToList();
            EnsureLibraryBundleCodes(libraries);
            return ESEditorHandle.EnqueueLongTask(new ESAssetReferenceBakeLongTask(libraries));
        }

        private static void EnsureLibraryBundleCodes(IReadOnlyCollection<ESAssetLibrary> libraries)
        {
            var owners = new Dictionary<string, ESAssetLibrary>(StringComparer.Ordinal);
            foreach (ESAssetLibrary library in libraries ?? Array.Empty<ESAssetLibrary>())
            {
                string assetPath = AssetDatabase.GetAssetPath(library);
                string libraryGuid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrWhiteSpace(libraryGuid))
                    throw new InvalidOperationException("[ESRes][Bake] Library 缺少稳定 Asset GUID：" + library.Name);

                string code = library.AssetBundleCode?.Trim().ToLowerInvariant() ?? string.Empty;
                if (string.IsNullOrEmpty(code))
                {
                    code = ESAssetBundleUtility.CreateAutomaticLibraryCode(library.Name, libraryGuid);
                    Undo.RecordObject(library, "Generate AssetBundle Code");
                    library.AssetBundleCode = code;
                    EditorUtility.SetDirty(library);
                    Debug.Log($"[ESRes][Bake][Naming] 已为 Library [{library.Name}] 固化 AB 短码：{code}", library);
                }
                if (!ESAssetBundleUtility.IsValidLibraryCode(code))
                    throw new InvalidOperationException($"[ESRes][Bake] Library [{library.Name}] 的 AB 短码无效：{library.AssetBundleCode}。仅允许 2~12 位 a-z、0-9、_。");
                if (owners.TryGetValue(code, out ESAssetLibrary existing))
                    throw new InvalidOperationException($"[ESRes][Bake] Library AB 短码全局冲突：{code}，Library=[{existing.Name}] 与 [{library.Name}]。");
                owners.Add(code, library);
            }
            AssetDatabase.SaveAssets();
        }

        private sealed class ESAssetReferenceBakeLongTask : ESEditorLongTask
        {
            private enum Phase { Catalogs, Graphs, ValidateKeys, GameCore, WriteOutputs, Finish }
            private readonly List<ESAssetLibrary> libraries;
            private readonly Dictionary<ESAssetLibrary, ESAssetLibraryCatalog> catalogs = new Dictionary<ESAssetLibrary, ESAssetLibraryCatalog>();
            private readonly Dictionary<ESAssetLibrary, ESAssetReferenceGraph> graphs = new Dictionary<ESAssetLibrary, ESAssetReferenceGraph>();
            private readonly List<ESAssetLibraryConsumer> consumers;
            private readonly List<string> gameCoreErrors = new List<string>();
            private Phase phase;
            private int index;

            public ESAssetReferenceBakeLongTask(List<ESAssetLibrary> libraries)
                : base("烘焙资产引用", "ES.ResourcePipeline", 10)
            {
                this.libraries = libraries ?? new List<ESAssetLibrary>();
                consumers = ESEditorSO.SOS.GetNewGroupOfType<ESAssetLibraryConsumer>()
                    ?.Where(item => item != null).OrderBy(item => item.ConsumerId, StringComparer.Ordinal).ToList() ?? new List<ESAssetLibraryConsumer>();
            }

            public override ESEditorLongTaskStepResult ProcessStep(ESEditorLongTaskContext context)
            {
                switch (phase)
                {
                    case Phase.Catalogs:
                        if (index < libraries.Count)
                        {
                            ESAssetLibrary library = libraries[index];
                            SetProgress(index, TotalSteps, "分析资源库：" + library.Name);
                            catalogs.Add(library, CreateCatalog(library));
                            index++;
                            return ESEditorLongTaskStepResult.Continue;
                        }
                        index = 0;
                        phase = Phase.Graphs;
                        return ESEditorLongTaskStepResult.Continue;
                    case Phase.Graphs:
                        if (index < libraries.Count)
                        {
                            ESAssetLibrary library = libraries[index];
                            SetProgress(libraries.Count + index, TotalSteps, "烘焙直接依赖图：" + library.Name);
                            graphs.Add(library, CreateReferenceGraph(catalogs[library], catalogs.Values));
                            index++;
                            return ESEditorLongTaskStepResult.Continue;
                        }
                        index = 0;
                        phase = Phase.ValidateKeys;
                        return ESEditorLongTaskStepResult.Continue;
                    case Phase.ValidateKeys:
                        SetProgress(libraries.Count * 2, TotalSteps, "校验业务资源键");
                        ValidateBusinessKeys(catalogs.Values);
                        phase = Phase.GameCore;
                        return ESEditorLongTaskStepResult.Continue;
                    case Phase.GameCore:
                        if (index < consumers.Count)
                        {
                            ESAssetLibraryConsumer consumer = consumers[index];
                            SetProgress(libraries.Count * 2 + 1 + index, TotalSteps, "同步游戏核心：" + consumer.Name);
                            gameCoreErrors.AddRange(SyncConsumerGameCoreCatalog(consumer, libraries));
                            index++;
                            return ESEditorLongTaskStepResult.Continue;
                        }
                        AssetDatabase.SaveAssets();
                        index = 0;
                        phase = Phase.WriteOutputs;
                        return ESEditorLongTaskStepResult.Continue;
                    case Phase.WriteOutputs:
                        if (index < libraries.Count)
                        {
                            ESAssetLibrary library = libraries[index];
                            ESAssetLibraryCatalog catalog = catalogs[library];
                            ESAssetReferenceGraph graph = graphs[library];
                            SetProgress(libraries.Count * 2 + consumers.Count + 1 + index, TotalSteps, "写入资源目录与引用图：" + catalog.libraryName);
                            string outputFolder = ESAssetPipelineIO.LibraryBakeFolder(catalog.libraryFolder);
                            ESAssetPipelineIO.WriteJson(Path.Combine(outputFolder, ESAssetPipelineIO.CatalogFileName), catalog);
                            ESAssetPipelineIO.WriteJson(Path.Combine(outputFolder, ESAssetPipelineIO.ReferenceGraphFileName), graph);
                            index++;
                            return ESEditorLongTaskStepResult.Continue;
                        }
                        phase = Phase.Finish;
                        return ESEditorLongTaskStepResult.Continue;
                    default:
                        AssetDatabase.Refresh();
                        int errors = catalogs.Values.Sum(item => item.errors.Count) + graphs.Values.Sum(item => item.errors.Count) + gameCoreErrors.Count;
                        int warnings = catalogs.Values.Sum(item => item.warnings.Count) + graphs.Values.Sum(item => item.warnings.Count);
                        Debug.Log($"[ESRes][Bake] 完成 {catalogs.Count} 个资源库与引用图，错误 {errors}，警告 {warnings}。输出：{ESAssetPipelineIO.BakeRoot}");
                        foreach (string warning in catalogs.Values.SelectMany(item => item.warnings)
                            .Concat(graphs.Values.SelectMany(item => item.warnings))
                            .Distinct(StringComparer.Ordinal))
                            Debug.LogWarning("[ESRes][Bake][Warning] " + warning);
                        if (errors > 0)
                        {
                            foreach (string error in catalogs.Values.SelectMany(item => item.errors)
                                .Concat(graphs.Values.SelectMany(item => item.errors))
                                .Concat(gameCoreErrors)
                                .Distinct(StringComparer.Ordinal))
                                Debug.LogError("[ESRes][Bake][Error] " + error);
                            SetFailure(new InvalidOperationException("[ESRes][Bake] 资产引用烘焙存在错误，请检查资源目录后再规划资源包。"));
                            return ESEditorLongTaskStepResult.Fail;
                        }
                        SetProgress(TotalSteps, TotalSteps, "烘焙完成");
                        return ESEditorLongTaskStepResult.Complete;
                }
            }

            private int TotalSteps => libraries.Count * 3 + consumers.Count + 2;
        }
        private static ESAssetLibraryCatalog CreateCatalog(ESAssetLibrary library)
        {
            string libraryAssetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(library));
            var catalog = new ESAssetLibraryCatalog { libraryName = library.Name, libraryFolder = library.LibFolderName,
                libraryBundleCode = library.AssetBundleCode, libraryAssetGuid = libraryAssetGuid };
            foreach (var book in library.GetAllUseableBooks().Where(item => item != null))
            {
                if (book.pages == null) continue;
                foreach (var page in book.pages.Where(item => item != null && item.OB != null))
                {
                    string path = AssetDatabase.GetAssetPath(page.OB);
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        AddFolderAssets(catalog, library, page, path);
                        continue;
                    }
                    if (ESAssetPipelineIO.IsEditorOnly(path, page.OB))
                    {
                        AddEditorOnlyExclusion(catalog, path, $"已排除 EditorOnly Page：{library.Name}/{page.Name} ({path})");
                        continue;
                    }
                    var identity = ESAssetPipelineIO.GetIdentity(page.OB);
                    if (!identity.IsValid) { catalog.errors.Add($"无效 GUID：{library.Name}/{page.Name} ({path})"); continue; }
                    if (!string.Equals(page.AssetGuid, identity.guid, StringComparison.Ordinal) || page.LocalFileId != identity.localFileId)
                        catalog.warnings.Add($"Page 持久化身份已过期；Catalog 使用当前对象身份：{library.Name}/{page.Name} ({path})");
                    ESAssetReferKind actualKind = ESAssetPage.DetermineKind(page.OB);
                    if (page.OB is ScriptableObject internalCandidate && ESScriptableObjectClassification.GetClass(internalCandidate) == ESScriptableObjectClass.Internal)
                    {
                        catalog.warnings.Add($"已排除 IInternalSO：{library.Name}/{page.Name} ({path})");
                        continue;
                    }
                    if (!ESAssetReferConfigKeySwitch.IsSupportedKind(actualKind))
                    {
                        catalog.warnings.Add($"资产类型不支持运行时加载，已跳过：{library.Name}/{page.Name}，类型={actualKind} ({path})");
                        continue;
                    }
                    if (page.Kind != actualKind)
                        catalog.warnings.Add($"Page 类型与实际资产不一致，已采用实际类型：{library.Name}/{page.Name}，配置={page.Kind}，实际={actualKind} ({path})");
                    if (identity.IsSubAsset && !ValidateSubAssetSelector(catalog, library, page, path, identity)) continue;
                    ESAssetCatalogEntry entry = new ESAssetCatalogEntry { identity = identity, assetPath = path, assetTypeName = page.OB.GetType().FullName, kind = actualKind.ToString(), enumKey = page.EnumKey,
                        stringKey = page.ResolveEffectiveStringKey(), libraryName = library.Name, libraryFolder = library.LibFolderName, libraryBundleCode = library.AssetBundleCode,
                        pageName = page.Name, namedOption = page.namedOption.ToString(), subAssetName = identity.IsSubAsset ? page.OB.name : string.Empty };
                    PopulateBundleFolderIdentity(entry, path, null);
                    catalog.assets.Add(entry);
                }
            }
            catalog.assets = catalog.assets.OrderBy(item => item.kind, StringComparer.Ordinal).ThenBy(item => item.enumKey).ThenBy(item => item.stringKey, StringComparer.Ordinal).ToList();
            return catalog;
        }

        private static ESAssetReferenceGraph CreateReferenceGraph(ESAssetLibraryCatalog catalog, IEnumerable<ESAssetLibraryCatalog> allCatalogs)
        {
            var graph = new ESAssetReferenceGraph
            {
                libraryName = catalog.libraryName,
                libraryFolder = catalog.libraryFolder,
                generatedUtc = DateTime.UtcNow.ToString("O")
            };
            var ownersByPath = (allCatalogs ?? Enumerable.Empty<ESAssetLibraryCatalog>())
                .SelectMany(item => (item.assets ?? new List<ESAssetCatalogEntry>())
                    .Where(asset => asset != null && !string.IsNullOrWhiteSpace(asset.assetPath))
                    .Select(asset => new { asset.assetPath, item.libraryFolder }))
                .GroupBy(item => item.assetPath, StringComparer.Ordinal)
                .ToDictionary(group => group.Key,
                    group => group.Select(item => item.libraryFolder).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToList(),
                    StringComparer.Ordinal);
            var nodes = new Dictionary<string, ESAssetReferenceNode>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);

            foreach (ESAssetCatalogEntry asset in catalog.assets ?? new List<ESAssetCatalogEntry>())
            {
                if (asset == null || asset.identity == null || !asset.identity.IsValid || string.IsNullOrWhiteSpace(asset.assetPath))
                {
                    graph.errors.Add("Catalog 包含无法建立引用图的根资产。Library=" + catalog.libraryFolder);
                    continue;
                }
                graph.roots.Add(new ESAssetReferenceRoot { identity = asset.identity, assetPath = asset.assetPath });
                AddReferenceNode(asset.assetPath, graph, nodes, visiting, ownersByPath);
            }

            graph.roots = graph.roots
                .GroupBy(item => item.identity.Key, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(item => item.assetPath, StringComparer.Ordinal)
                .ThenBy(item => item.identity.localFileId)
                .ToList();
            graph.nodes = nodes.Values.OrderBy(item => item.assetPath, StringComparer.Ordinal).ToList();
            graph.errors = graph.errors.Distinct(StringComparer.Ordinal).ToList();
            graph.warnings = graph.warnings.Distinct(StringComparer.Ordinal).ToList();
            return graph;
        }

        private static void AddReferenceNode(string assetPath, ESAssetReferenceGraph graph,
            Dictionary<string, ESAssetReferenceNode> nodes, HashSet<string> visiting,
            Dictionary<string, List<string>> ownersByPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || nodes.ContainsKey(assetPath))
                return;
            if (!visiting.Add(assetPath))
            {
                graph.errors.Add("资产引用存在循环：" + assetPath);
                return;
            }

            try
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                ESPipelineAssetIdentity identity = ESAssetPipelineIO.GetMainIdentity(assetPath);
                bool editorOnly = ESAssetPipelineIO.IsEditorOnly(assetPath, asset);
                string[] directDependencies = AssetDatabase.GetDependencies(assetPath, false)
                    .Where(path => !string.IsNullOrWhiteSpace(path) && !string.Equals(path, assetPath, StringComparison.Ordinal))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
                var node = new ESAssetReferenceNode
                {
                    identity = identity,
                    assetPath = assetPath,
                    assetTypeName = asset != null ? asset.GetType().FullName : string.Empty,
                    dependencyHash = AssetDatabase.GetAssetDependencyHash(assetPath).ToString(),
                    editorOnly = editorOnly,
                    markable = !editorOnly && identity.IsValid && assetPath.StartsWith("Assets/", StringComparison.Ordinal),
                    ownerLibraryFolders = ownersByPath.TryGetValue(assetPath, out List<string> owners) ? new List<string>(owners) : new List<string>(),
                    directDependencies = editorOnly ? new List<string>() : directDependencies.ToList()
                };
                nodes.Add(assetPath, node);

                if (editorOnly)
                {
                    graph.warnings.Add("引用图已标记 EditorOnly 依赖：" + assetPath);
                    return;
                }
                foreach (string dependencyPath in node.directDependencies)
                    AddReferenceNode(dependencyPath, graph, nodes, visiting, ownersByPath);
            }
            catch (Exception exception)
            {
                graph.errors.Add("引用图分析失败：" + assetPath + "，" + exception.Message);
            }
            finally
            {
                visiting.Remove(assetPath);
            }
        }

        private static bool ValidateSubAssetSelector(ESAssetLibraryCatalog catalog, ESAssetLibrary library, ESAssetPage page, string assetPath, ESPipelineAssetIdentity identity)
        {
            string selector = page.OB.name;
            string typeName = page.OB.GetType().FullName;
            if (string.IsNullOrWhiteSpace(selector) || string.IsNullOrWhiteSpace(typeName))
            {
                catalog.errors.Add($"[ESRes][SubAsset] 子资产缺少运行时选择信息：Library={library.Name}, Page={page.Name}, GUID={identity.guid}, LocalFileId={identity.localFileId}, Path={assetPath}, Selector={selector}, Type={typeName}");
                return false;
            }

            int matchCount = 0;
            foreach (UnityEngine.Object candidate in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (candidate == null || !string.Equals(candidate.name, selector, StringComparison.Ordinal)
                    || !string.Equals(candidate.GetType().FullName, typeName, StringComparison.Ordinal)) continue;
                ESPipelineAssetIdentity candidateIdentity = ESAssetPipelineIO.GetIdentity(candidate);
                if (candidateIdentity.IsSubAsset) matchCount++;
            }

            if (matchCount == 1) return true;
            catalog.errors.Add($"[ESRes][SubAsset] 子资产选择器不能唯一定位；请避免同文件内同名同类型子资产：Library={library.Name}, Page={page.Name}, GUID={identity.guid}, LocalFileId={identity.localFileId}, Path={assetPath}, Selector={selector}, Type={typeName}, MatchCount={matchCount}");
            return false;
        }
        private static void AddFolderAssets(ESAssetLibraryCatalog catalog, ESAssetLibrary library, ESAssetPage page, string folderPath)
        {
            catalog.warnings.Add($"Folder Page 作为构建集合展开，不使用其 EnumKey/StringKey：{library.Name}/{page.Name} ({folderPath})");
            foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { folderPath }))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(assetPath)) continue;
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (asset == null) continue;
                if (ESAssetPipelineIO.IsEditorOnly(assetPath, asset))
                {
                    AddEditorOnlyExclusion(catalog, assetPath, $"文件夹 Page 中已排除 EditorOnly 资产：{assetPath}");
                    continue;
                }
                var identity = ESAssetPipelineIO.GetIdentity(asset);
                if (!identity.IsValid) { catalog.warnings.Add("文件夹内资产无有效 GUID，已跳过：" + assetPath); continue; }
                ESAssetReferKind kind = ESAssetPage.DetermineKind(asset);
                if (asset is ScriptableObject internalCandidate && ESScriptableObjectClassification.GetClass(internalCandidate) == ESScriptableObjectClass.Internal)
                {
                    catalog.warnings.Add("文件夹内 IInternalSO 已跳过：" + assetPath);
                    continue;
                }
                if (!ESAssetReferConfigKeySwitch.IsSupportedKind(kind))
                {
                    catalog.warnings.Add("文件夹内非可加载资产已跳过：" + assetPath);
                    continue;
                }
                if (catalog.assets.Any(item => string.Equals(item.assetPath, assetPath, StringComparison.Ordinal))) continue;
                var entry = new ESAssetCatalogEntry
                {
                    identity = identity,
                    assetPath = assetPath,
                    assetTypeName = asset.GetType().FullName,
                    kind = kind.ToString(),
                    libraryName = library.Name,
                    libraryFolder = library.LibFolderName,
                    libraryBundleCode = library.AssetBundleCode,
                    pageName = page.Name,
                    namedOption = page.namedOption.ToString(),
                    isBusinessAsset = false
                };
                PopulateBundleFolderIdentity(entry, assetPath, folderPath);
                catalog.assets.Add(entry);
            }
        }

        private static void PopulateBundleFolderIdentity(ESAssetCatalogEntry entry, string assetPath, string folderPageRoot)
        {
            string normalizedPath = (assetPath ?? string.Empty).Replace('\\', '/');
            string parent = (Path.GetDirectoryName(normalizedPath) ?? "Assets").Replace('\\', '/');
            entry.parentFolderPath = parent;
            entry.parentFolderGuid = GetFolderIdentity(parent);

            string topLevel = GetTopLevelFolder(normalizedPath, folderPageRoot);
            entry.topLevelFolderPath = topLevel;
            entry.topLevelFolderGuid = GetFolderIdentity(topLevel);
        }

        private static string GetFolderIdentity(string folderPath)
        {
            if (string.Equals(folderPath, "Assets", StringComparison.Ordinal)) return "assets-root";
            string guid = AssetDatabase.AssetPathToGUID(folderPath);
            if (string.IsNullOrWhiteSpace(guid))
                throw new InvalidOperationException("[ESRes][Bake] 分包目录缺少稳定 .meta GUID：" + folderPath);
            return guid;
        }

        private static string GetTopLevelFolder(string assetPath, string folderPageRoot)
        {
            string root = string.IsNullOrWhiteSpace(folderPageRoot) ? "Assets" : folderPageRoot.Replace('\\', '/').TrimEnd('/');
            string parent = (Path.GetDirectoryName(assetPath) ?? root).Replace('\\', '/');
            if (!parent.StartsWith(root + "/", StringComparison.Ordinal)) return parent;
            string relative = parent.Substring(root.Length + 1);
            int slash = relative.IndexOf('/');
            string first = slash < 0 ? relative : relative.Substring(0, slash);
            string candidate = string.IsNullOrWhiteSpace(first) ? root : root + "/" + first;
            return AssetDatabase.IsValidFolder(candidate) ? candidate : root;
        }
        private static void AddEditorOnlyExclusion(ESAssetLibraryCatalog catalog, string path, string warning)
        {
            if (!string.IsNullOrEmpty(path) && !catalog.excludedEditorOnlyPaths.Contains(path)) catalog.excludedEditorOnlyPaths.Add(path);
            catalog.warnings.Add(warning);
        }

        private static List<string> SyncConsumerGameCoreCatalogs(IReadOnlyCollection<ESAssetLibrary> libraries)
        {
            var allErrors = new List<string>();
            var consumers = ESEditorSO.SOS.GetNewGroupOfType<ESAssetLibraryConsumer>()
                ?.Where(item => item != null).ToList() ?? new List<ESAssetLibraryConsumer>();
            foreach (ESAssetLibraryConsumer consumer in consumers)
            {
                allErrors.AddRange(SyncConsumerGameCoreCatalog(consumer, libraries));
            }
            AssetDatabase.SaveAssets();
            return allErrors;
        }

        private static List<string> SyncConsumerGameCoreCatalog(ESAssetLibraryConsumer consumer, IReadOnlyCollection<ESAssetLibrary> libraries)
        {
            consumer.EnsureStableIdentity();
            var generated = new List<ESAssetReferBase>();
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (ESAssetLibrary library in (consumer.ConsumerLibFolders ?? new List<ESAssetLibrary>()).Where(library => library != null && libraries.Contains(library)))
            foreach (ESAssetBook book in library.GetAllUseableBooks().Where(book => book?.pages != null))
            foreach (ESAssetPage page in book.pages.Where(page => page?.OB != null))
            {
                string pagePath = AssetDatabase.GetAssetPath(page.OB);
                if (AssetDatabase.IsValidFolder(pagePath))
                    foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { pagePath }))
                        AddGameCoreAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid), generated, identities, paths);
                else
                    AddGameCoreAsset(page.OB as ScriptableObject, generated, identities, paths);
            }
            foreach (ESAssetReferBase refer in consumer.ManualGameCoreAssets ?? new List<ESAssetReferBase>())
                if (refer != null && refer.IsValid)
                    AddGameCoreAsset(ResolveExactScriptableObject(refer.GUID, refer.LocalFileId), generated, identities, paths);
            ExpandGameCoreConfigKeyClosure(generated, identities, paths);
            consumer.GameCoreAssets = generated.GroupBy(item => item.AssetIdentity).Select(group => group.First()).ToList();
            consumer.GameCoreValidationErrors = ValidateGameCoreDependencies(consumer, paths);
            EditorUtility.SetDirty(consumer);
            return consumer.GameCoreValidationErrors;
        }

        private static void AddGameCoreAssetsAtPath(string path, List<ESAssetReferBase> destination, HashSet<string> identities, HashSet<string> paths)
        {
            if (string.IsNullOrWhiteSpace(path) || ESAssetPipelineIO.IsEditorOnly(path))
                return;
            foreach (UnityEngine.Object loaded in AssetDatabase.LoadAllAssetsAtPath(path))
                AddGameCoreAsset(loaded as ScriptableObject, destination, identities, paths);
        }

        private static void AddGameCoreAsset(ScriptableObject asset, List<ESAssetReferBase> destination, HashSet<string> identities, HashSet<string> paths)
        {
            if (asset == null || ESScriptableObjectClassification.GetClass(asset) != ESScriptableObjectClass.GameCore)
                return;
            ESPipelineAssetIdentity identity = ESAssetPipelineIO.GetIdentity(asset);
            if (!identity.IsValid || !identities.Add(identity.Key))
                return;
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrWhiteSpace(path) || ESAssetPipelineIO.IsEditorOnly(path)) return;
            paths.Add(path);
            var refer = new ESAssetReferScriptableObject();
            refer.InitializeGeneratedReference(identity.guid, identity.localFileId, ESAssetReferKind.ScriptableObject, 0, string.Empty);
            destination.Add(refer);
        }

        private static void ExpandGameCoreConfigKeyClosure(List<ESAssetReferBase> destination, HashSet<string> identities, HashSet<string> paths)
        {
            for (int index = 0; index < destination.Count; index++)
            {
                ESAssetReferBase refer = destination[index];
                ScriptableObject root = ResolveExactScriptableObject(refer.GUID, refer.LocalFileId);
                if (root == null) continue;
                var serializedObject = new SerializedObject(root);
                SerializedProperty iterator = serializedObject.GetIterator();
                bool enterChildren = true;
                while (iterator.Next(enterChildren))
                {
                    enterChildren = true;
                    if (!string.Equals(iterator.name, "definitionGuid", StringComparison.Ordinal)
                        || iterator.propertyType != SerializedPropertyType.String
                        || string.IsNullOrEmpty(iterator.stringValue))
                        continue;

                    string parentPath = iterator.propertyPath;
                    int separator = parentPath.LastIndexOf('.');
                    parentPath = separator >= 0 ? parentPath.Substring(0, separator + 1) : string.Empty;
                    SerializedProperty localFileId = serializedObject.FindProperty(parentPath + "definitionLocalFileId");
                    long fileId = localFileId != null ? localFileId.longValue : 0;
                    AddGameCoreAsset(ResolveExactScriptableObject(iterator.stringValue, fileId), destination, identities, paths);
                }
            }
        }

        private static ScriptableObject ResolveExactScriptableObject(string guid, long localFileId)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(path)) return null;
            foreach (UnityEngine.Object loaded in AssetDatabase.LoadAllAssetsAtPath(path))
                if (loaded is ScriptableObject asset
                    && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string candidateGuid, out long candidateLocalFileId)
                    && string.Equals(candidateGuid, guid, StringComparison.Ordinal)
                    && candidateLocalFileId == localFileId)
                    return asset;
            return null;
        }

        private static List<string> ValidateGameCoreDependencies(ESAssetLibraryConsumer consumer, HashSet<string> collectedPaths)
        {
            var errors = new List<string>();
            foreach (string rootPath in collectedPaths)
            foreach (string dependencyPath in AssetDatabase.GetDependencies(rootPath, true))
            {
                var dependency = AssetDatabase.LoadMainAssetAtPath(dependencyPath) as ScriptableObject;
                if (dependency == null || ESScriptableObjectClassification.GetClass(dependency) != ESScriptableObjectClass.GameCore
                    || collectedPaths.Contains(dependencyPath))
                    continue;
                errors.Add("Consumer [" + consumer.Name + "] 的 GameCore 依赖未收集："
                    + rootPath + " -> " + dependencyPath + "。请将该资产或其目录加入 Consumer 的必需 Library。");
            }
            return errors.Distinct(StringComparer.Ordinal).ToList();
        }

        private static void ValidateBusinessKeys(IEnumerable<ESAssetLibraryCatalog> catalogs)
        {
            var enumKeys = new Dictionary<string, ESAssetCatalogEntry>(StringComparer.Ordinal); var stringKeys = new Dictionary<string, ESAssetCatalogEntry>(StringComparer.Ordinal); var identities = new Dictionary<string, ESAssetCatalogEntry>(StringComparer.Ordinal);
            foreach (var catalog in catalogs) foreach (var asset in catalog.assets)
            {
                if (!identities.TryGetValue(asset.identity.Key, out var identityOwner)) identities.Add(asset.identity.Key, asset);
                else if (identityOwner.isBusinessAsset && asset.isBusinessAsset) catalog.warnings.Add($"资产身份重复，规划时将按同一物理资产去重：{identityOwner.libraryName}/{identityOwner.pageName} 与 {asset.libraryName}/{asset.pageName}");
                if (asset.isBusinessAsset && asset.enumKey != 0) ValidateKey(catalog, enumKeys, asset.kind + "\n" + asset.enumKey, asset, "类型内 EnumKey");
                if (asset.isBusinessAsset && !string.IsNullOrEmpty(asset.stringKey)) ValidateKey(catalog, stringKeys, asset.kind + "\n" + asset.stringKey, asset, "类型内 StringKey");
            }
        }
        private static void ValidateKey(ESAssetLibraryCatalog catalog, Dictionary<string, ESAssetCatalogEntry> index, string key, ESAssetCatalogEntry asset, string label)
        {
            if (!index.TryGetValue(key, out var existing))
            {
                index.Add(key, asset);
                return;
            }
            if (ReferenceEquals(existing, asset)) return;
            if (existing.identity != null && asset.identity != null
                && string.Equals(existing.identity.Key, asset.identity.Key, StringComparison.Ordinal))
            {
                catalog.warnings.Add($"{label} 对同一资产重复注册，构建时静默去重：{existing.libraryName}/{existing.pageName} 与 {asset.libraryName}/{asset.pageName}");
                return;
            }
            catalog.errors.Add($"{label} 指向不同资产，无法确定业务寻址：{existing.libraryName}/{existing.pageName} 与 {asset.libraryName}/{asset.pageName}");
        }
    }
}
