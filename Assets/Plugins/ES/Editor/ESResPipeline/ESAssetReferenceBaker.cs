using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ES.EditorInternal;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public static class ESAssetReferenceBaker
    {
        private const string ResourcePipelineTaskKey = "ES.ResourcePipeline";

        internal static bool IsBakeActive
            => ESEditorHandle.TryGetActiveLongTaskByKey(ResourcePipelineTaskKey, out ESEditorLongTask active)
               && active is ESAssetReferenceBakeLongTask;

        /// <summary>
        /// 烘焙入口改为编辑器长任务。每帧只推进一个 Library 或输出文件；
        /// AssetDatabase 的原子调用仍保持在主线程，避免额外线程同步和 GC 压力。
        /// </summary>
        public static ESEditorLongTask Bake(Action<ESEditorLongTask> onFinished = null)
        {
            if (TryJoinActiveBake(ESAssetPipelineIO.BakeRoot, null, false, onFinished, out ESEditorLongTask joined))
                return joined;

            ESAssetPipelineIO.RefreshGlobalExcludedFolders();
            var libraries = ESEditorSO.GetGroupOfType<ESAssetLibrary>()
                .Where(item => item != null && item.ContainsBuild)
                .OrderBy(item => item.LibFolderName, StringComparer.Ordinal)
                .ToList();
            // EditorDirect 只生成编辑器 Catalog/ReferenceGraph。正式 AB 命名、Consumer
            // 快照和发布 ConfigKey 门禁由 Planner/Publisher 的独立入口执行。
            ESResourcePlanConfigKeySynchronizer.ValidateAllForBake();
            return EnqueueOrJoinBake(
                libraries, ESAssetPipelineIO.BakeRoot, null, false, onFinished);
        }

        internal static ESEditorLongTask BakeForCatalogRecovery(
            string transactionId, Action<ESEditorLongTask> onFinished = null)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
                throw new ArgumentException("Catalog 恢复事务 ID 不能为空。", nameof(transactionId));

            string outputRoot = ESAssetPipelineIO.RecoveryBakeRoot(transactionId);
            if (TryJoinActiveBake(outputRoot, transactionId, true, onFinished, out ESEditorLongTask joined))
                return joined;

            ESAssetPipelineIO.RefreshGlobalExcludedFolders();
            var libraries = ESEditorSO.GetGroupOfType<ESAssetLibrary>()
                .Where(item => item != null && item.ContainsBuild)
                .OrderBy(item => item.LibFolderName, StringComparer.Ordinal)
                .ToList();
            // EditorDirect 恢复只负责 Catalog/ReferenceGraph，不提前引入正式
            // LocalBuild/HotUpdate 的 AB 短码门禁；正式发布门禁仍由独立阶段负责。
            ESResourcePlanConfigKeySynchronizer.ValidateAllForBake();
            return EnqueueOrJoinBake(
                libraries, outputRoot, transactionId, true, onFinished);
        }

        private static bool TryJoinActiveBake(
            string outputRoot,
            string transactionId,
            bool writeCommitMarker,
            Action<ESEditorLongTask> onFinished,
            out ESEditorLongTask joined)
        {
            joined = null;
            if (!ESEditorHandle.TryGetActiveLongTaskByKey(ResourcePipelineTaskKey, out ESEditorLongTask active))
                return false;
            if (!(active is ESAssetReferenceBakeLongTask existing)
                || !existing.Matches(outputRoot, transactionId, writeCommitMarker))
            {
                throw new InvalidOperationException(
                    "ES.ResourcePipeline 已有不同类型或不同输出身份的任务；拒绝把 Bake 回调挂到无关任务。");
            }

            existing.AddFinishedCallback(onFinished);
            joined = existing;
            return true;
        }

        private static ESEditorLongTask EnqueueOrJoinBake(
            List<ESAssetLibrary> libraries,
            string outputRoot,
            string transactionId,
            bool writeCommitMarker,
            Action<ESEditorLongTask> onFinished)
        {
            if (ESEditorHandle.TryGetActiveLongTaskByKey(ResourcePipelineTaskKey, out ESEditorLongTask active))
            {
                if (!(active is ESAssetReferenceBakeLongTask existing)
                    || !existing.Matches(outputRoot, transactionId, writeCommitMarker))
                {
                    throw new InvalidOperationException(
                        "ES.ResourcePipeline 已有不同类型或不同输出身份的任务；拒绝把 Bake 回调挂到无关任务。");
                }

                existing.AddFinishedCallback(onFinished);
                return existing;
            }

            var task = new ESAssetReferenceBakeLongTask(
                libraries, outputRoot, transactionId, writeCommitMarker);
            task.AddFinishedCallback(onFinished);
            return ESEditorHandle.EnqueueLongTask(task);
        }

        internal static void ValidateSourceStateForBuild(IReadOnlyCollection<ESAssetLibrary> libraries)
        {
            // 不依赖任意窗口的前置检查。菜单、脚本和 CI 都必须获得同一份
            // 只读 ConfigKey、Consumer 与 Library 命名校验；源资产修复必须是显式动作。
            ESResourcePlanConfigKeySynchronizer.ValidateAllForBake();
            ValidateLibraryBundleCodes(libraries);
            ValidateConsumerGameCoreAssetsForBuild(libraries);
        }

        internal static string GetLibrarySourceRevision(ESAssetLibrary library)
        {
            string assetPath = AssetDatabase.GetAssetPath(library);
            if (library == null || string.IsNullOrEmpty(assetPath))
                return string.Empty;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string absolutePath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            string fileHash = File.Exists(absolutePath)
                ? ESResManifestIntegrity.ComputeFileSha256(absolutePath)
                : string.Empty;
            return AssetDatabase.GetAssetDependencyHash(assetPath) + ":" + fileHash;
        }

        private static void ValidateLibraryBundleCodes(IReadOnlyCollection<ESAssetLibrary> libraries)
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
                    throw new InvalidOperationException($"[ESRes][Bake] Library [{library.Name}] 未配置 AB 短码，请在 Library Inspector 中显式生成或填写后重试。");
                if (!ESAssetBundleUtility.IsValidLibraryCode(code))
                    throw new InvalidOperationException($"[ESRes][Bake] Library [{library.Name}] 的 AB 短码无效：{library.AssetBundleCode}。仅允许 2~12 位 a-z、0-9、_。");
                if (owners.TryGetValue(code, out ESAssetLibrary existing))
                    throw new InvalidOperationException($"[ESRes][Bake] Library AB 短码全局冲突：{code}，Library=[{existing.Name}] 与 [{library.Name}]。");
                owners.Add(code, library);
            }
        }

        private sealed class ESAssetReferenceBakeLongTask : ESEditorLongTask
        {
            private enum Phase { Catalogs, Graphs, ValidateKeys, WriteOutputs, Finish }
            private readonly List<ESAssetLibrary> libraries;
            private readonly Dictionary<ESAssetLibrary, ESAssetLibraryCatalog> catalogs = new Dictionary<ESAssetLibrary, ESAssetLibraryCatalog>();
            private readonly Dictionary<ESAssetLibrary, ESAssetReferenceGraph> graphs = new Dictionary<ESAssetLibrary, ESAssetReferenceGraph>();
            private readonly List<LibrarySourceState> sourceStates;
            private readonly string outputRoot;
            private readonly string transactionId;
            private readonly bool writeCommitMarker;
            private readonly string bakeGenerationUtc = DateTime.UtcNow.ToString("O");
            private Phase phase;
            private int index;

            public ESAssetReferenceBakeLongTask(
                List<ESAssetLibrary> libraries,
                string outputRoot,
                string transactionId,
                bool writeCommitMarker)
                : base("烘焙资产引用", ResourcePipelineTaskKey, 10)
            {
                this.libraries = libraries ?? new List<ESAssetLibrary>();
                this.outputRoot = Path.GetFullPath(outputRoot ?? throw new ArgumentNullException(nameof(outputRoot)));
                this.transactionId = transactionId;
                this.writeCommitMarker = writeCommitMarker;
                sourceStates = this.libraries.Select(LibrarySourceState.Capture).ToList();
            }

            public bool Matches(string candidateOutputRoot, string candidateTransactionId, bool candidateWriteCommitMarker)
            {
                return string.Equals(outputRoot, Path.GetFullPath(candidateOutputRoot ?? string.Empty), StringComparison.OrdinalIgnoreCase)
                       && string.Equals(transactionId ?? string.Empty, candidateTransactionId ?? string.Empty, StringComparison.Ordinal)
                       && writeCommitMarker == candidateWriteCommitMarker;
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
                            catalogs.Add(library, CreateCatalog(library, bakeGenerationUtc));
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
                        ValidateSourceStates();
                        ValidateBusinessKeys(catalogs.Values);
                        phase = Phase.WriteOutputs;
                        return ESEditorLongTaskStepResult.Continue;
                    case Phase.WriteOutputs:
                        ValidateSourceStates();
                        if (index < libraries.Count)
                        {
                            ESAssetLibrary library = libraries[index];
                            ESAssetLibraryCatalog catalog = catalogs[library];
                            ESAssetReferenceGraph graph = graphs[library];
                            SetProgress(libraries.Count * 2 + 1 + index, TotalSteps, "写入资源目录与引用图：" + catalog.libraryName);
                            string outputFolder = Path.Combine(outputRoot, ESAssetPipelineIO.SafeSegment(catalog.libraryFolder));
                            ESAssetPipelineIO.WriteJson(Path.Combine(outputFolder, ESAssetPipelineIO.CatalogFileName), catalog, true);
                            ESAssetPipelineIO.WriteJson(Path.Combine(outputFolder, ESAssetPipelineIO.ReferenceGraphFileName), graph, true);
                            index++;
                            return ESEditorLongTaskStepResult.Continue;
                        }
                        phase = Phase.Finish;
                        return ESEditorLongTaskStepResult.Continue;
                    default:
                        AssetDatabase.Refresh();
                        int errors = catalogs.Values.Sum(item => item.errors.Count) + graphs.Values.Sum(item => item.errors.Count);
                        int warnings = catalogs.Values.Sum(item => item.warnings.Count) + graphs.Values.Sum(item => item.warnings.Count);
                        Debug.Log($"[ESRes][Bake] 完成 {catalogs.Count} 个资源库与引用图，错误 {errors}，警告 {warnings}。输出：{outputRoot}");
                        foreach (string warning in catalogs.Values.SelectMany(item => item.warnings)
                            .Concat(graphs.Values.SelectMany(item => item.warnings))
                            .Distinct(StringComparer.Ordinal))
                            Debug.LogWarning("[ESRes][Bake][Warning] " + warning);
                        if (errors > 0)
                        {
                            foreach (string error in catalogs.Values.SelectMany(item => item.errors)
                                .Concat(graphs.Values.SelectMany(item => item.errors))
                                .Distinct(StringComparer.Ordinal))
                                Debug.LogError("[ESRes][Bake][Error] " + error);
                            SetFailure(new InvalidOperationException("[ESRes][Bake] 资产引用烘焙存在错误，请检查资源目录后再规划资源包。"));
                            return ESEditorLongTaskStepResult.Fail;
                        }
                        if (writeCommitMarker)
                        {
                            if (libraries.Count == 0)
                            {
                                SetFailure(new InvalidOperationException(
                                    "[ESRes][CatalogRecovery] 本次恢复烘焙没有 ContainsBuild 的资源库，不生成恢复提交标记。"));
                                return ESEditorLongTaskStepResult.Fail;
                            }

                            WriteCommitMarker();
                        }
                        SetProgress(TotalSteps, TotalSteps, "烘焙完成");
                        return ESEditorLongTaskStepResult.Complete;
                }
            }

            private void WriteCommitMarker()
            {
                var commit = new ESAssetCatalogBakeCommit
                {
                    transactionId = transactionId,
                    generatedUtc = DateTime.UtcNow.ToString("O"),
                    commitGeneration = DateTime.UtcNow.Ticks
                };

                foreach (ESAssetLibrary library in libraries)
                {
                    ESAssetLibraryCatalog catalog = catalogs[library];
                    string folder = Path.Combine(outputRoot, ESAssetPipelineIO.SafeSegment(catalog.libraryFolder));
                    AddCommitOutput(commit, catalog, folder, ESAssetPipelineIO.CatalogFileName, true);
                    AddCommitOutput(commit, catalog, folder, ESAssetPipelineIO.ReferenceGraphFileName, false);
                }

                string commitPath = Path.Combine(outputRoot, ESAssetPipelineIO.CatalogBakeCommitFileName);
                if (File.Exists(commitPath))
                    throw new InvalidOperationException(
                        "[ESRes][CatalogRecovery] 恢复提交标记已存在，拒绝覆盖同一 transaction 的既有提交。");
                ESAssetPipelineIO.WriteJsonCreateNew(commitPath, commit);
            }

            private void AddCommitOutput(
                ESAssetCatalogBakeCommit commit,
                ESAssetLibraryCatalog catalog,
                string folder,
                string fileName,
                bool isCatalog)
            {
                string path = Path.Combine(folder, fileName);
                if (!File.Exists(path))
                    throw new FileNotFoundException("Catalog 烘焙输出缺失，无法提交本次恢复事务。", path);

                string normalizedRoot = outputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                commit.outputs.Add(new ESAssetCatalogBakeOutput
                {
                    relativePath = path.Substring(normalizedRoot.Length + 1)
                        .Replace(Path.DirectorySeparatorChar, '/'),
                    libraryName = catalog.libraryName,
                    libraryFolder = catalog.libraryFolder,
                    outputKind = isCatalog
                        ? ESAssetPipelineIO.CatalogOutputKind
                        : ESAssetPipelineIO.ReferenceGraphOutputKind,
                    protocolVersion = isCatalog
                        ? ESAssetPipelineIO.CatalogFormatVersion
                        : ESAssetPipelineIO.ReferenceGraphFormatVersion,
                    commitGeneration = commit.commitGeneration,
                    size = new FileInfo(path).Length,
                    sha256 = ESResManifestIntegrity.ComputeFileSha256(path),
                    isCatalog = isCatalog
                });
            }

            private int TotalSteps => libraries.Count * 3 + 2;

            private void ValidateSourceStates()
            {
                for (int i = 0; i < sourceStates.Count; i++)
                    sourceStates[i].Validate();
            }

            private sealed class LibrarySourceState
            {
                private readonly ESAssetLibrary library;
                private readonly string assetPath;
                private readonly string assetGuid;
                private readonly string dependencyHash;
                private readonly string fileHash;

                private LibrarySourceState(
                    ESAssetLibrary library,
                    string assetPath,
                    string assetGuid,
                    string dependencyHash,
                    string fileHash)
                {
                    this.library = library;
                    this.assetPath = assetPath;
                    this.assetGuid = assetGuid;
                    this.dependencyHash = dependencyHash;
                    this.fileHash = fileHash;
                }

                public static LibrarySourceState Capture(ESAssetLibrary library)
                {
                    string path = AssetDatabase.GetAssetPath(library);
                    if (string.IsNullOrEmpty(path) || EditorUtility.IsDirty(library))
                        throw new InvalidOperationException("[ESRes][Bake] Library 必须先保存且具有稳定路径：" + (library != null ? library.Name : "<null>"));

                    return new LibrarySourceState(
                        library,
                        path,
                        AssetDatabase.AssetPathToGUID(path),
                        AssetDatabase.GetAssetDependencyHash(path).ToString(),
                        ComputeDiskHash(path));
                }

                public void Validate()
                {
                    bool changed = library == null
                                   || EditorUtility.IsDirty(library)
                                   || !string.Equals(AssetDatabase.GetAssetPath(library), assetPath, StringComparison.Ordinal)
                                   || !string.Equals(AssetDatabase.AssetPathToGUID(assetPath), assetGuid, StringComparison.OrdinalIgnoreCase)
                                   || !string.Equals(AssetDatabase.GetAssetDependencyHash(assetPath).ToString(), dependencyHash, StringComparison.Ordinal)
                                   || !string.Equals(ComputeDiskHash(assetPath), fileHash, StringComparison.Ordinal);
                    if (changed)
                    {
                        throw new InvalidOperationException(
                            "[ESRes][Bake] Bake 执行期间注册源发生变化，已中止输出：" + assetPath);
                    }
                }

                private static string ComputeDiskHash(string assetPath)
                {
                    string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
                    string absolutePath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
                    return File.Exists(absolutePath)
                        ? ESResManifestIntegrity.ComputeFileSha256(absolutePath)
                        : string.Empty;
                }
            }
        }
        private static ESAssetLibraryCatalog CreateCatalog(ESAssetLibrary library, string generatedUtc)
        {
            string libraryAssetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(library));
            var catalog = new ESAssetLibraryCatalog { libraryName = library.Name, libraryFolder = library.LibFolderName,
                libraryBundleCode = library.AssetBundleCode, libraryAssetGuid = libraryAssetGuid,
                librarySourceRevision = GetLibrarySourceRevision(library), generatedUtc = generatedUtc };
            foreach (var book in library.GetAllUseableBooks().Where(item => item != null))
            {
                if (book.pages == null) continue;
                foreach (var page in book.pages.Where(item => item != null && item.OB != null))
                {
                    string path = AssetDatabase.GetAssetPath(page.OB);
                    if (ESAssetPipelineIO.IsExcludedFolderPath(path))
                    {
                        AddFolderExclusion(catalog, path, $"已排除全局排除文件夹内容：{library.Name}/{page.Name} ({path})");
                        continue;
                    }
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
                generatedUtc = catalog.generatedUtc
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
            if (ESAssetPipelineIO.IsExcludedFolderPath(assetPath))
            {
                graph.errors.Add("资产位于全局排除目录，不能作为业务引用依赖：" + assetPath);
                return;
            }
            if (!visiting.Add(assetPath))
            {
                graph.errors.Add("资产引用存在循环：" + assetPath);
                return;
            }

            try
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                ESPipelineAssetIdentity identity = ESAssetPipelineIO.GetMainIdentity(assetPath);
                bool editorOnly = ESAssetPipelineIO.IsEditorOnly(assetPath, asset)
                    || ESAssetPipelineIO.IsExcludedFolderPath(assetPath);
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
                if (ESAssetPipelineIO.IsExcludedFolderPath(assetPath))
                {
                    AddFolderExclusion(catalog, assetPath, $"文件夹 Page 中已排除全局排除目录资产：{assetPath}");
                    continue;
                }
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

        private static void AddFolderExclusion(ESAssetLibraryCatalog catalog, string path, string warning)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/');
            if (!string.IsNullOrEmpty(normalized) && !catalog.excludedFolderPaths.Contains(normalized))
                catalog.excludedFolderPaths.Add(normalized);
            catalog.warnings.Add(warning);
        }

        internal static void ValidateConsumerGameCoreAssetsForBuild(IReadOnlyCollection<ESAssetLibrary> libraries)
        {
            var errors = new List<string>();
            foreach (ESAssetLibraryConsumer consumer in ESEditorSO.GetGroupOfType<ESAssetLibraryConsumer>()
                ?.Where(item => item != null).ToList() ?? new List<ESAssetLibraryConsumer>())
                errors.AddRange(ValidateConsumerGameCoreCatalog(consumer, libraries));
            if (errors.Count > 0)
                throw new InvalidOperationException("[ESRes][Build] Consumer GameCore 快照校验失败；请执行 Consumer 的‘同步并检查’后重试：\n"
                    + string.Join("\n", errors.Distinct(StringComparer.Ordinal)));
        }

        private static List<string> ValidateConsumerGameCoreCatalog(ESAssetLibraryConsumer consumer, IReadOnlyCollection<ESAssetLibrary> libraries)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(consumer.ConsumerId))
                errors.Add("Consumer [" + consumer.Name + "] 缺少稳定 ID，请先保存 Consumer 或显式补全 ID。");

            List<string> computedErrors = BuildConsumerGameCoreSnapshot(consumer, libraries, out List<ESAssetReferBase> generated);
            errors.AddRange(computedErrors);

            var expected = generated
                .Where(item => item != null && item.IsValid)
                .Select(GetReferenceIdentityKey)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToList();
            var actual = new List<string>();
            foreach (ESAssetReferBase refer in consumer.GameCoreAssets ?? new List<ESAssetReferBase>())
            {
                if (refer == null || !refer.IsValid)
                {
                    errors.Add("Consumer [" + consumer.Name + "] 的 GameCoreAssets 包含空或无效引用。");
                    continue;
                }
                actual.Add(GetReferenceIdentityKey(refer));
            }
            actual.Sort(StringComparer.Ordinal);
            if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
                errors.Add("Consumer [" + consumer.Name + "] 的 GameCoreAssets 快照已过期；请执行‘同步并检查’，Bake 不会自动改写 Consumer。");

            ValidateConsumerReferenceList(consumer, consumer.GameCoreAssets, errors, "GameCoreAssets");
            ValidateConsumerReferenceList(consumer, consumer.ManualGameCoreAssets, errors, "ManualGameCoreAssets");
            ValidateConsumerReferenceList(consumer, consumer.ResidentAssets, errors, "ResidentAssets");

            return errors.Distinct(StringComparer.Ordinal).ToList();
        }

        private static void ValidateConsumerReferenceList(
            ESAssetLibraryConsumer consumer,
            IEnumerable<ESAssetReferBase> references,
            List<string> errors,
            string fieldName)
        {
            if (consumer == null || references == null || errors == null)
                return;
            foreach (ESAssetReferBase refer in references)
            {
                if (refer == null || !refer.IsValid)
                    continue;
                string path = AssetDatabase.GUIDToAssetPath(refer.GUID);
                if (ESAssetPipelineIO.IsExcludedFolderPath(path))
                    errors.Add(
                        "Consumer [" + consumer.Name + "] 的 " + fieldName
                        + " 引用全局排除目录资产：" + path);
            }
        }

        internal static List<string> BuildConsumerGameCoreSnapshot(
            ESAssetLibraryConsumer consumer,
            IReadOnlyCollection<ESAssetLibrary> libraries,
            out List<ESAssetReferBase> generated)
        {
            if (consumer == null) throw new ArgumentNullException(nameof(consumer));
            libraries ??= Array.Empty<ESAssetLibrary>();
            generated = new List<ESAssetReferBase>();
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var identities = new HashSet<string>(StringComparer.Ordinal);
            var errors = new List<string>();
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
            {
                if (refer == null || !refer.IsValid)
                {
                    errors.Add("Consumer [" + consumer.Name + "] 的 ManualGameCoreAssets 包含空或无效引用。");
                    continue;
                }
                ScriptableObject manualAsset = ResolveExactScriptableObject(refer.GUID, refer.LocalFileId);
                if (manualAsset == null)
                {
                    errors.Add("Consumer [" + consumer.Name + "] 的 ManualGameCoreAssets 引用已丢失："
                        + refer.GUID + "#" + refer.LocalFileId + "。");
                    continue;
                }
                AddGameCoreAsset(manualAsset, generated, identities, paths);
            }
            var closureErrors = new List<string>();
            ExpandGameCoreConfigKeyClosure(generated, identities, paths, closureErrors);
            errors.AddRange(ValidateGameCoreDependencies(consumer, paths));
            errors.AddRange(closureErrors);
            errors.AddRange(ValidateCollectedItemGameCoreDefinitions(generated));
            return errors.Distinct(StringComparer.Ordinal).ToList();
        }

        private static string GetReferenceIdentityKey(ESAssetReferBase refer)
            => (refer?.GUID ?? string.Empty) + "#" + (refer?.LocalFileId ?? 0);

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

        private static void ExpandGameCoreConfigKeyClosure(
            List<ESAssetReferBase> destination,
            HashSet<string> identities,
            HashSet<string> paths,
            List<string> errors)
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
                        || iterator.propertyType != SerializedPropertyType.String)
                        continue;

                    string parentPath = iterator.propertyPath;
                    int separator = parentPath.LastIndexOf('.');
                    parentPath = separator >= 0 ? parentPath.Substring(0, separator + 1) : string.Empty;
                    SerializedProperty enumKey = serializedObject.FindProperty(parentPath + "enumKey");
                    SerializedProperty stringKey = serializedObject.FindProperty(parentPath + "stringKey");
                    if (enumKey != null && stringKey != null
                        && !ESConfigKeyMatch.IsConfigured(enumKey.intValue, stringKey.stringValue))
                    {
                        errors.Add("GameCore ConfigKey 未显式配置：" + root.name + "，属性 " + parentPath.TrimEnd('.')
                            + "。KeyName 仅供编辑器与策划使用，不能作为运行时回退键。");
                    }

                    if (string.IsNullOrEmpty(iterator.stringValue))
                        continue;

                    SerializedProperty localFileId = serializedObject.FindProperty(parentPath + "definitionLocalFileId");
                    long fileId = localFileId != null ? localFileId.longValue : 0;
                    ScriptableObject dependency = ResolveExactScriptableObject(iterator.stringValue, fileId);
                    if (dependency == null)
                    {
                        errors.Add("GameCore ConfigKey 引用丢失：GUID=" + iterator.stringValue + "，LocalFileId=" + fileId + "。");
                        continue;
                    }

                    if (ESScriptableObjectClassification.GetClass(dependency) != ESScriptableObjectClass.GameCore)
                    {
                        errors.Add("GameCore ConfigKey 指向的资产不是 GameCore 根：" + dependency.name + "。");
                        continue;
                    }

                    AddGameCoreAsset(dependency, destination, identities, paths);
                }
            }
        }

        private static List<string> ValidateCollectedItemGameCoreDefinitions(List<ESAssetReferBase> generated)
        {
            var errors = new List<string>();
            var items = new List<ItemDataInfo>();
            var visited = new HashSet<int>();
            for (int i = 0; i < generated.Count; i++)
            {
                ScriptableObject root = ResolveExactScriptableObject(generated[i].GUID, generated[i].LocalFileId);
                CollectItemDefinitions(root, items, visited);
            }

            var owners = new Dictionary<string, ItemDataInfo>(StringComparer.Ordinal);
            for (int i = 0; i < items.Count; i++)
            {
                ItemDataInfo item = items[i];
                if (!item.IsGameCoreRoot)
                    continue;

                ESItemDataValidationCode validation = item.ValidateConfiguration();
                if (validation != ESItemDataValidationCode.Valid)
                {
                    errors.Add("Item GameCore 配置无效：" + item.name + "，" + item.GetValidationMessage(validation));
                    continue;
                }

                if (!item.TryGetItemGameCoreKey(out ESItemConfigKey itemKey))
                {
                    errors.Add("Item GameCore 缺少基础 ItemKey：" + item.name);
                    continue;
                }

                string itemIdentity = "Item|" + (itemKey.EnumKeyInt != 0
                    ? "E:" + itemKey.EnumKeyInt
                    : "S:" + itemKey.StringKey);
                if (owners.TryGetValue(itemIdentity, out ItemDataInfo itemOwner) && itemOwner != item)
                    errors.Add("基础 ItemKey 重复：" + itemIdentity + "，资产为 " + DescribeItem(itemOwner) + " 与 " + DescribeItem(item));
                else
                    owners[itemIdentity] = item;

                if (!item.TryGetGameCoreKey(out IESConfigKey key))
                    continue;

                string identity = item.baseConfig.kind + "|" + (key.EnumKeyInt != 0
                    ? "E:" + key.EnumKeyInt
                    : "S:" + key.StringKey);
                if (owners.TryGetValue(identity, out ItemDataInfo owner) && owner != item)
                    errors.Add("Item GameCore Key 重复：" + identity + "，资产为 " + DescribeItem(owner) + " 与 " + DescribeItem(item));
                else
                    owners[identity] = item;
            }
            return errors;
        }

        private static string DescribeItem(ItemDataInfo item)
        {
            string path = AssetDatabase.GetAssetPath(item);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(item, out _, out long localFileId);
            return item.name + " [" + path + "#" + localFileId + "]";
        }

        private static void CollectItemDefinitions(ScriptableObject root, List<ItemDataInfo> destination, HashSet<int> visited)
        {
            if (root == null)
                return;
            if (root is ItemDataInfo item)
            {
                int id = item.GetInstanceID();
                if (visited.Add(id)) destination.Add(item);
                return;
            }
            if (root is ISoDataGroup group)
            {
                foreach (ISoDataInfo info in group.AllInfos)
                    if (info is ItemDataInfo groupItem)
                    {
                        int id = groupItem.GetInstanceID();
                        if (visited.Add(id)) destination.Add(groupItem);
                    }
            }
            if (root is ISoDataPack pack && pack.AllInfos != null)
            {
                foreach (object value in pack.AllInfos.Values)
                    if (value is ItemDataInfo packItem)
                    {
                        int id = packItem.GetInstanceID();
                        if (visited.Add(id)) destination.Add(packItem);
                    }
            }
        }

        private static ScriptableObject ResolveExactScriptableObject(string guid, long localFileId)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(path)) return null;
            foreach (UnityEngine.Object loaded in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(loaded is ScriptableObject asset)) continue;
                ESPipelineAssetIdentity candidate = ESAssetPipelineIO.GetIdentity(asset);
                if (string.Equals(candidate.guid, guid, StringComparison.Ordinal)
                    && candidate.localFileId == localFileId)
                    return asset;
            }
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
            if (AreEquivalentBusinessRegistrations(existing, asset))
            {
                catalog.warnings.Add($"{label} 对同一资产等价重复注册，构建时合并为一条：{existing.libraryName}/{existing.pageName} 与 {asset.libraryName}/{asset.pageName}");
                return;
            }
            catalog.errors.Add($"{label} 指向不同资产，无法确定业务寻址：{existing.libraryName}/{existing.pageName} 与 {asset.libraryName}/{asset.pageName}");
        }

        private static bool AreEquivalentBusinessRegistrations(
            ESAssetCatalogEntry left,
            ESAssetCatalogEntry right)
        {
            return left?.identity != null
                   && right?.identity != null
                   && string.Equals(left.identity.Key, right.identity.Key, StringComparison.Ordinal)
                   && string.Equals(left.kind, right.kind, StringComparison.Ordinal)
                   && left.enumKey == right.enumKey
                   && string.Equals(left.stringKey, right.stringKey, StringComparison.Ordinal)
                   && string.Equals(left.assetTypeName, right.assetTypeName, StringComparison.Ordinal);
        }
    }
}
