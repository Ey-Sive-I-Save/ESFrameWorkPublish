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

        /// <summary>
        /// 烘焙入口改为编辑器长任务。每帧只推进一个 Library、Consumer 或输出文件；
        /// AssetDatabase 的原子调用仍保持在主线程，避免额外线程同步和 GC 压力。
        /// </summary>
        public static ESEditorLongTask Bake()
        {
            var libraries = ESEditorSO.SOS.GetNewGroupOfType<ESAssetLibrary>().Where(item => item != null).OrderBy(item => item.LibFolderName, StringComparer.Ordinal).ToList();
            return ESEditorHandle.EnqueueLongTask(new ESAssetReferenceBakeLongTask(libraries));
        }

        private sealed class ESAssetReferenceBakeLongTask : ESEditorLongTask
        {
            private enum Phase { Catalogs, ValidateKeys, GameCore, WriteCatalogs, Finish }
            private readonly List<ESAssetLibrary> libraries;
            private readonly Dictionary<ESAssetLibrary, ESAssetLibraryCatalog> catalogs = new Dictionary<ESAssetLibrary, ESAssetLibraryCatalog>();
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
                            SetProgress(index, libraries.Count + consumers.Count + libraries.Count + 2, "分析资源库：" + library.Name);
                            catalogs.Add(library, CreateCatalog(library));
                            index++;
                            return ESEditorLongTaskStepResult.Continue;
                        }
                        index = 0;
                        phase = Phase.ValidateKeys;
                        return ESEditorLongTaskStepResult.Continue;
                    case Phase.ValidateKeys:
                        SetProgress(libraries.Count, libraries.Count + consumers.Count + libraries.Count + 2, "校验业务资源键");
                        ValidateBusinessKeys(catalogs.Values);
                        phase = Phase.GameCore;
                        return ESEditorLongTaskStepResult.Continue;
                    case Phase.GameCore:
                        if (index < consumers.Count)
                        {
                            ESAssetLibraryConsumer consumer = consumers[index];
                            SetProgress(libraries.Count + index, libraries.Count + consumers.Count + libraries.Count + 2, "同步游戏核心：" + consumer.Name);
                            gameCoreErrors.AddRange(SyncConsumerGameCoreCatalog(consumer, libraries));
                            index++;
                            return ESEditorLongTaskStepResult.Continue;
                        }
                        AssetDatabase.SaveAssets();
                        index = 0;
                        phase = Phase.WriteCatalogs;
                        return ESEditorLongTaskStepResult.Continue;
                    case Phase.WriteCatalogs:
                        if (index < libraries.Count)
                        {
                            ESAssetLibraryCatalog catalog = catalogs[libraries[index]];
                            SetProgress(libraries.Count + consumers.Count + index, libraries.Count + consumers.Count + libraries.Count + 2, "写入资源目录：" + catalog.libraryName);
                            ESAssetPipelineIO.WriteJson(Path.Combine(ESAssetPipelineIO.LibraryBakeFolder(catalog.libraryFolder), ESAssetPipelineIO.CatalogFileName), catalog);
                            index++;
                            return ESEditorLongTaskStepResult.Continue;
                        }
                        phase = Phase.Finish;
                        return ESEditorLongTaskStepResult.Continue;
                    default:
                        AssetDatabase.Refresh();
                        int errors = catalogs.Values.Sum(item => item.errors.Count) + gameCoreErrors.Count;
                        Debug.Log($"[ESRes][Bake] 完成 {catalogs.Count} 个资源库，错误 {errors}。输出：{ESAssetPipelineIO.BakeRoot}");
                        if (errors > 0)
                        {
                            SetFailure(new InvalidOperationException("[ESRes][Bake] 资产引用烘焙存在错误，请检查资源目录后再规划资源包。"));
                            return ESEditorLongTaskStepResult.Fail;
                        }
                        SetProgress(libraries.Count + consumers.Count + libraries.Count + 2, libraries.Count + consumers.Count + libraries.Count + 2, "烘焙完成");
                        return ESEditorLongTaskStepResult.Complete;
                }
            }
        }
        private static ESAssetLibraryCatalog CreateCatalog(ESAssetLibrary library)
        {
            var catalog = new ESAssetLibraryCatalog { libraryName = library.Name, libraryFolder = library.LibFolderName };
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
                    if (page.OB is ScriptableObject gameCoreCandidate && ESScriptableObjectClassification.GetClass(gameCoreCandidate) == ESScriptableObjectClass.GameCore)
                    {
                        catalog.warnings.Add($"IGameCoreSO 仅归属 Consumer 启动核心包，不写入 Library Catalog：{library.Name}/{page.Name} ({path})");
                        continue;
                    }
                    if (!ESAssetReferConfigKeySwitch.IsSupportedKind(actualKind))
                    {
                        catalog.errors.Add($"资产类型不支持运行时加载：{library.Name}/{page.Name}，类型={actualKind} ({path})");
                        continue;
                    }
                    if (page.Kind != actualKind)
                        catalog.errors.Add($"Page 类型与实际资产不一致：{library.Name}/{page.Name}，配置={page.Kind}，实际={actualKind} ({path})");
                    if (identity.IsSubAsset && !ValidateSubAssetSelector(catalog, library, page, path, identity)) continue;
                    catalog.assets.Add(new ESAssetCatalogEntry { identity = identity, assetPath = path, assetTypeName = page.OB.GetType().FullName, kind = actualKind.ToString(), enumKey = page.EnumKey,
                        stringKey = page.ResolveEffectiveStringKey(), libraryName = library.Name, libraryFolder = library.LibFolderName, pageName = page.Name, namedOption = page.namedOption.ToString(), subAssetName = identity.IsSubAsset ? page.OB.name : string.Empty });
                }
            }
            catalog.assets = catalog.assets.OrderBy(item => item.kind, StringComparer.Ordinal).ThenBy(item => item.enumKey).ThenBy(item => item.stringKey, StringComparer.Ordinal).ToList();
            return catalog;
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
                if (asset is ScriptableObject gameCoreCandidate && ESScriptableObjectClassification.GetClass(gameCoreCandidate) == ESScriptableObjectClass.GameCore)
                {
                    catalog.warnings.Add("文件夹内 IGameCoreSO 仅归属 Consumer 启动核心包，已跳过 Catalog：" + assetPath);
                    continue;
                }
                if (!ESAssetReferConfigKeySwitch.IsSupportedKind(kind))
                {
                    catalog.warnings.Add("文件夹内非可加载资产已跳过：" + assetPath);
                    continue;
                }
                if (catalog.assets.Any(item => string.Equals(item.assetPath, assetPath, StringComparison.Ordinal))) continue;
                catalog.assets.Add(new ESAssetCatalogEntry
                {
                    identity = identity,
                    assetPath = assetPath,
                    assetTypeName = asset.GetType().FullName,
                    kind = kind.ToString(),
                    libraryName = library.Name,
                    libraryFolder = library.LibFolderName,
                    pageName = page.Name,
                    namedOption = page.namedOption.ToString(),
                    isBusinessAsset = false
                });
            }
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
            foreach (ESAssetLibrary library in (consumer.ConsumerLibFolders ?? new List<ESAssetLibrary>()).Where(library => library != null && libraries.Contains(library)))
            foreach (ESAssetBook book in library.GetAllUseableBooks().Where(book => book?.pages != null))
            foreach (ESAssetPage page in book.pages.Where(page => page?.OB != null))
            {
                string pagePath = AssetDatabase.GetAssetPath(page.OB);
                if (AssetDatabase.IsValidFolder(pagePath))
                    foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { pagePath }))
                        AddGameCoreAsset(AssetDatabase.GUIDToAssetPath(guid), generated, paths);
                else
                    AddGameCoreAsset(pagePath, generated, paths);
            }
            foreach (ESAssetReferBase refer in consumer.ManualGameCoreAssets ?? new List<ESAssetReferBase>())
                if (refer != null && refer.IsValid)
                    AddGameCoreAsset(AssetDatabase.GUIDToAssetPath(refer.GUID), generated, paths);
            consumer.GameCoreAssets = generated.GroupBy(item => item.AssetIdentity).Select(group => group.First()).ToList();
            consumer.GameCoreValidationErrors = ValidateGameCoreDependencies(consumer, paths);
            EditorUtility.SetDirty(consumer);
            return consumer.GameCoreValidationErrors;
        }

        private static void AddGameCoreAsset(string path, List<ESAssetReferBase> destination, HashSet<string> paths)
        {
            if (string.IsNullOrWhiteSpace(path) || ESAssetPipelineIO.IsEditorOnly(path))
                return;
            var asset = AssetDatabase.LoadMainAssetAtPath(path) as ScriptableObject;
            if (asset == null || ESScriptableObjectClassification.GetClass(asset) != ESScriptableObjectClass.GameCore)
                return;
            ESPipelineAssetIdentity identity = ESAssetPipelineIO.GetIdentity(asset);
            if (!identity.IsValid)
                return;
            var refer = new ESAssetReferScriptableObject();
            refer.InitializeGeneratedReference(identity.guid, identity.localFileId, ESAssetReferKind.ScriptableObject, 0, string.Empty);
            destination.Add(refer);
            paths.Add(path);
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
                else if (identityOwner.isBusinessAsset && asset.isBusinessAsset) catalog.errors.Add($"资产身份冲突：{identityOwner.libraryName}/{identityOwner.pageName} 与 {asset.libraryName}/{asset.pageName}");
                if (asset.isBusinessAsset && asset.enumKey != 0) ValidateKey(catalog, enumKeys, asset.kind + "\n" + asset.enumKey, asset, "类型内 EnumKey");
                if (asset.isBusinessAsset && !string.IsNullOrEmpty(asset.stringKey)) ValidateKey(catalog, stringKeys, asset.kind + "\n" + asset.stringKey, asset, "类型内 StringKey");
            }
        }
        private static void ValidateKey(ESAssetLibraryCatalog catalog, Dictionary<string, ESAssetCatalogEntry> index, string key, ESAssetCatalogEntry asset, string label)
        {
            if (!index.TryGetValue(key, out var existing)) index.Add(key, asset); else if (!ReferenceEquals(existing, asset)) catalog.errors.Add($"{label} 冲突：{existing.libraryName}/{existing.pageName} 与 {asset.libraryName}/{asset.pageName}");
        }
    }
}
