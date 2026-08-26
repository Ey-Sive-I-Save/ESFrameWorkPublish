using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ES
{
    internal sealed class ESItemPrefabAuthoringRequest
    {
        public string label;
        public string definitionPath;
        public string prefabPath;
        public string prefabAssetKey;
        public Action<ItemDataInfo> configureNewDefinition;
        public Action<ItemDataInfo> validateDefinitionOwnership;
        public Action<ItemDataInfo> validateDefinitionBeforePrefab;
        public Action<ItemDataInfo> validateDefinition;
        public Func<ItemDataInfo, GameObject> buildNewPrefab;
        public Action<GameObject, ItemDataInfo> validatePrefab;
    }

    internal readonly struct ESItemPrefabAuthoringResult
    {
        public readonly ItemDataInfo definition;
        public readonly GameObject prefab;
        public readonly bool definitionCreated;
        public readonly bool prefabCreated;

        public ESItemPrefabAuthoringResult(
            ItemDataInfo definition,
            GameObject prefab,
            bool definitionCreated,
            bool prefabCreated)
        {
            this.definition = definition;
            this.prefab = prefab;
            this.definitionCreated = definitionCreated;
            this.prefabCreated = prefabCreated;
        }
    }

    /// <summary>
    /// Creates or validates one or more Item definitions and their owned prefabs.
    /// Project-wide conflicts that can be known from existing assets are rejected before writes.
    /// Each completed step is persisted so a later retry can continue without deleting assets.
    /// This is staged persistence, not an all-or-nothing asset transaction.
    /// Existing author values and prefab structure are validation-only.
    /// </summary>
    internal static class ESItemPrefabAuthoring
    {
        internal static ESItemPrefabAuthoringResult[] CreateOrValidate(
            string libraryPath,
            params ESItemPrefabAuthoringRequest[] requests)
        {
            EnsureEditorCanWrite();
            ESAssetLibrary library = RequireCleanLibrary(libraryPath);
            PreflightRequests(library, requests);

            for (int i = 0; i < requests.Length; i++)
            {
                EnsureParentFolder(requests[i].definitionPath);
                EnsureParentFolder(requests[i].prefabPath);
            }

            var results = new ESItemPrefabAuthoringResult[requests.Length];
            for (int i = 0; i < requests.Length; i++)
                results[i] = CreateOrValidateOne(libraryPath, library, requests[i]);
            return results;
        }

        private static void PreflightRequests(
            ESAssetLibrary library,
            ESItemPrefabAuthoringRequest[] requests)
        {
            if (requests == null || requests.Length == 0)
                throw new InvalidOperationException("武器作者请求不能为空。");

            var definitionPaths = new HashSet<string>(StringComparer.Ordinal);
            var prefabPaths = new HashSet<string>(StringComparer.Ordinal);
            var prefabKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < requests.Length; i++)
            {
                ESItemPrefabAuthoringRequest request = requests[i];
                ValidateRequest(request);
                if (!definitionPaths.Add(request.definitionPath))
                    throw new InvalidOperationException("作者请求包含重复 Definition 路径：" + request.definitionPath);
                if (!prefabPaths.Add(request.prefabPath))
                    throw new InvalidOperationException("作者请求包含重复 Prefab 路径：" + request.prefabPath);
                if (!prefabKeys.Add(request.prefabAssetKey))
                    throw new InvalidOperationException("作者请求包含重复 Prefab AssetKey：" + request.prefabAssetKey);
                PreflightOne(library, request);
            }
        }

        private static void ValidateRequest(ESItemPrefabAuthoringRequest request)
        {
            if (request == null)
                throw new InvalidOperationException("武器作者请求不能为空。");
            if (string.IsNullOrWhiteSpace(request.label))
                throw new InvalidOperationException("武器作者请求缺少可读名称。");
            RequireAssetPath(request.definitionPath, ".asset", request.label + " Definition");
            RequireAssetPath(request.prefabPath, ".prefab", request.label + " Prefab");
            if (string.IsNullOrWhiteSpace(request.prefabAssetKey))
                throw new InvalidOperationException(request.label + " 缺少 Prefab AssetKey。");
            if (request.configureNewDefinition == null
                || request.validateDefinitionOwnership == null
                || request.validateDefinitionBeforePrefab == null
                || request.validateDefinition == null
                || request.buildNewPrefab == null
                || request.validatePrefab == null)
            {
                throw new InvalidOperationException(request.label + " 作者请求缺少创建或验证入口。");
            }
        }

        private static void PreflightOne(ESAssetLibrary library, ESItemPrefabAuthoringRequest request)
        {
            Object definitionAsset = AssetDatabase.LoadMainAssetAtPath(request.definitionPath);
            if (definitionAsset != null && !(definitionAsset is ItemDataInfo))
                throw new InvalidOperationException(request.label + " Definition 路径已被其他资产类型占用：" + request.definitionPath);

            Object prefabAsset = AssetDatabase.LoadMainAssetAtPath(request.prefabPath);
            if (prefabAsset != null && !(prefabAsset is GameObject))
                throw new InvalidOperationException(request.label + " Prefab 路径已被其他资产类型占用：" + request.prefabPath);

            ItemDataInfo definition = definitionAsset as ItemDataInfo;
            GameObject prefab = prefabAsset as GameObject;
            RequireCleanTarget(definition, request.label + " Definition");
            RequireCleanTarget(prefab, request.label + " Prefab");

            if (definition != null)
            {
                request.validateDefinitionOwnership(definition);
                request.validateDefinitionBeforePrefab(definition);
            }
            if (prefab != null && definition == null)
                throw new InvalidOperationException(request.label + " 已存在 Prefab 但缺少对应 Definition，拒绝自动改绑。");
            if (prefab != null)
            {
                request.validatePrefab(prefab, definition);
                PreflightProjectRegistration(library, request, prefab);
                ValidateRecoverableBinding(library, request, definition, prefab);
            }
            else
            {
                PreflightProjectRegistration(library, request, null);
            }
        }

        private static void PreflightProjectRegistration(
            ESAssetLibrary targetLibrary,
            ESItemPrefabAuthoringRequest request,
            GameObject prefab)
        {
            bool hasPrefabIdentity = prefab != null;
            string prefabGuid = string.Empty;
            long prefabLocalFileId = 0;
            if (hasPrefabIdentity
                && !ESAssetPage.TryGetAssetIdentityEditor(prefab, out prefabGuid, out prefabLocalFileId))
            {
                throw new InvalidOperationException("无法读取 " + request.label + " Prefab 的稳定身份。");
            }

            string targetLibraryPath = AssetDatabase.GetAssetPath(targetLibrary);
            string[] libraryGuids = AssetDatabase.FindAssets("t:" + nameof(ESAssetLibrary));
            Array.Sort(libraryGuids, StringComparer.Ordinal);
            int targetExactMatchCount = 0;
            for (int libraryIndex = 0; libraryIndex < libraryGuids.Length; libraryIndex++)
            {
                string libraryPath = AssetDatabase.GUIDToAssetPath(libraryGuids[libraryIndex]);
                ESAssetLibrary library = AssetDatabase.LoadAssetAtPath<ESAssetLibrary>(libraryPath);
                if (library == null)
                    continue;

                foreach (ESAssetBook book in library.GetAllUseableBooks())
                {
                    if (book?.pages == null)
                        continue;

                    for (int pageIndex = 0; pageIndex < book.pages.Count; pageIndex++)
                    {
                        ESAssetPage page = book.pages[pageIndex];
                        if (page == null || ResolvePageKind(page) != ESAssetReferKind.Prefab)
                            continue;

                        bool sameKey = string.Equals(
                            page.EffectiveStringKey,
                            request.prefabAssetKey,
                            StringComparison.Ordinal);
                        bool sameIdentity = hasPrefabIdentity
                            && TryGetPageIdentity(page, out string pageGuid, out long pageLocalFileId)
                            && string.Equals(pageGuid, prefabGuid, StringComparison.OrdinalIgnoreCase)
                            && pageLocalFileId == prefabLocalFileId;
                        if (!sameKey && !sameIdentity)
                            continue;

                        bool isTargetLibrary = string.Equals(
                            libraryPath,
                            targetLibraryPath,
                            StringComparison.OrdinalIgnoreCase);
                        if (isTargetLibrary && hasPrefabIdentity && sameKey && sameIdentity)
                        {
                            targetExactMatchCount++;
                            if (targetExactMatchCount > 1)
                            {
                                throw new InvalidOperationException(
                                    request.label + " 项目级注册预检失败：目标 Library 内存在重复的 Prefab 身份和 AssetKey。Library="
                                    + targetLibraryPath + "。");
                            }
                            continue;
                        }

                        string conflict = sameKey && sameIdentity
                            ? "同一 Prefab 身份和 AssetKey 已由其他 Library 持有"
                            : sameKey
                                ? "Prefab AssetKey 已被其他资产占用"
                                : "同一 Prefab 身份已使用其他 AssetKey 注册";
                        throw new InvalidOperationException(
                            request.label + " 项目级注册预检失败：" + conflict
                            + "。Library=" + libraryPath
                            + "，目标 Library=" + targetLibraryPath + "。");
                    }
                }
            }
        }

        private static ESAssetReferKind ResolvePageKind(ESAssetPage page)
        {
            ESAssetReferKind kind = page.Kind;
            if ((kind == ESAssetReferKind.None || kind == ESAssetReferKind.Other)
                && page.OB != null)
            {
                kind = ESAssetPage.DetermineKind(page.OB);
            }
            return kind;
        }

        private static bool TryGetPageIdentity(
            ESAssetPage page,
            out string guid,
            out long localFileId)
        {
            if (page.OB != null
                && ESAssetPage.TryGetAssetIdentityEditor(page.OB, out guid, out localFileId))
            {
                return true;
            }

            guid = page.AssetGuid ?? string.Empty;
            localFileId = page.LocalFileId;
            return !string.IsNullOrEmpty(guid);
        }

        private static ESItemPrefabAuthoringResult CreateOrValidateOne(
            string libraryPath,
            ESAssetLibrary library,
            ESItemPrefabAuthoringRequest request)
        {
            ItemDataInfo definition = AssetDatabase.LoadAssetAtPath<ItemDataInfo>(request.definitionPath);
            bool definitionCreated = definition == null;
            if (definitionCreated)
            {
                definition = ScriptableObject.CreateInstance<ItemDataInfo>();
                request.configureNewDefinition(definition);
                request.validateDefinitionOwnership(definition);
                request.validateDefinitionBeforePrefab(definition);
                try
                {
                    AssetDatabase.CreateAsset(definition, request.definitionPath);
                }
                catch
                {
                    if (definition != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(definition)))
                        UnityEngine.Object.DestroyImmediate(definition);
                    throw;
                }
                AssetDatabase.SaveAssetIfDirty(definition);
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(request.prefabPath);
            bool prefabCreated = prefab == null;
            if (prefabCreated)
            {
                GameObject root = request.buildNewPrefab(definition);
                if (root == null)
                    throw new InvalidOperationException(request.label + " Prefab 构建入口返回空对象。");
                try
                {
                    request.validatePrefab(root, definition);
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, request.prefabPath);
                    if (prefab == null)
                        throw new InvalidOperationException("创建 " + request.label + " Prefab 失败：" + request.prefabPath);
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }

            request.validatePrefab(prefab, definition);
            BindAndPersistPrefabIdentity(request, definition, prefab);
            RegisterPrefab(libraryPath, library, request, prefab);
            request.validateDefinition(definition);

            return new ESItemPrefabAuthoringResult(
                definition,
                prefab,
                definitionCreated,
                prefabCreated);
        }

        private static void BindAndPersistPrefabIdentity(
            ESItemPrefabAuthoringRequest request,
            ItemDataInfo definition,
            GameObject prefab)
        {
            if (!ESAssetPage.TryGetAssetIdentityEditor(prefab, out string guid, out long localFileId))
                throw new InvalidOperationException("无法读取 " + request.label + " Prefab 的稳定身份。");

            definition.baseConfig ??= new ItemBaseConfig();
            definition.baseConfig.prefabKey ??= new ESAssetReferPrefabConfigKey();
            ESAssetReferPrefabConfigKey key = definition.baseConfig.prefabKey;
            EnsureKeyDoesNotConflict(request, key, guid, localFileId);
            if (KeyMatches(key, request.prefabAssetKey, guid, localFileId))
                return;

            key.stringKey = request.prefabAssetKey;
            key.SetAssetAuthority(guid, localFileId, typeof(GameObject).FullName, request.prefabPath);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
        }

        private static void RegisterPrefab(
            string libraryPath,
            ESAssetLibrary library,
            ESItemPrefabAuthoringRequest request,
            GameObject prefab)
        {
            if (!ESAssetPage.TryGetAssetIdentityEditor(prefab, out string guid, out long localFileId))
                throw new InvalidOperationException("无法读取 " + request.label + " Prefab 的稳定身份。");

            // Author callbacks and imports may have changed a Library after the initial preflight.
            // Recheck immediately before the idempotent return or registration commit.
            PreflightProjectRegistration(library, request, prefab);

            if (library.TryGetPageByStringKey(
                    ESAssetReferKind.Prefab,
                    request.prefabAssetKey,
                    out ESAssetPage page))
            {
                if (!PageMatches(page, request.prefabAssetKey, guid, localFileId))
                    throw new InvalidOperationException(request.label + " Prefab AssetKey 已注册到其他资产身份。");
                return;
            }

            var registration = new ESContentRegistrationRequest
            {
                action = ESContentRegistrationAction.RegisterAsset,
                commit = false,
                assetPath = request.prefabPath,
                libraryPath = libraryPath,
                expectedLocalFileId = localFileId,
                keyMode = ESContentStableKeyMode.StringOnly,
                stringKey = request.prefabAssetKey,
                assetKind = ESAssetReferKind.Prefab.ToString()
            };
            ESContentRegistrationResult preview = ESContentRegistrationAuthoring.Execute(registration);
            if (!preview.success)
            {
                throw new InvalidOperationException(
                    request.label + " Prefab 注册预检失败：" + preview.status + "，" + preview.message);
            }

            registration.requestId = preview.requestId;
            registration.commit = true;
            registration.expectedGuid = preview.guid;
            registration.expectedLocalFileId = preview.localFileId;
            registration.expectedLibraryRevision = preview.targetRevision;
            ESContentRegistrationResult result = ESContentRegistrationAuthoring.Execute(registration);
            if (!result.success)
            {
                throw new InvalidOperationException(
                    request.label + " Prefab 注册失败：" + result.status + "，" + result.message);
            }
        }

        private static void ValidateRecoverableBinding(
            ESAssetLibrary library,
            ESItemPrefabAuthoringRequest request,
            ItemDataInfo definition,
            GameObject prefab)
        {
            if (!ESAssetPage.TryGetAssetIdentityEditor(prefab, out string guid, out long localFileId))
                throw new InvalidOperationException("无法读取 " + request.label + " Prefab 的稳定身份。");
            ESAssetReferPrefabConfigKey key = definition?.baseConfig?.prefabKey;
            EnsureKeyDoesNotConflict(request, key, guid, localFileId);

            if (library.TryGetPageByStringKey(
                    ESAssetReferKind.Prefab,
                    request.prefabAssetKey,
                    out ESAssetPage page)
                && !PageMatches(page, request.prefabAssetKey, guid, localFileId))
            {
                throw new InvalidOperationException(request.label + " Prefab AssetKey 已注册到其他资产身份。");
            }
        }

        private static void EnsureKeyDoesNotConflict(
            ESItemPrefabAuthoringRequest request,
            ESAssetReferPrefabConfigKey key,
            string guid,
            long localFileId)
        {
            if (key == null)
                return;

            bool stringConflicts = !string.IsNullOrEmpty(key.StringKey)
                && !string.Equals(key.StringKey, request.prefabAssetKey, StringComparison.Ordinal);
            bool guidConflicts = !string.IsNullOrEmpty(key.guid)
                && !string.Equals(key.guid, guid, StringComparison.OrdinalIgnoreCase);
            bool fileIdConflicts = key.localFileId != 0 && key.localFileId != localFileId;
            if (stringConflicts || guidConflicts || fileIdConflicts)
                throw new InvalidOperationException(request.label + " Definition 已绑定其他 Prefab，拒绝静默改绑。");
        }

        private static bool KeyMatches(
            ESAssetReferPrefabConfigKey key,
            string assetKey,
            string guid,
            long localFileId)
        {
            return key != null
                && string.Equals(key.StringKey, assetKey, StringComparison.Ordinal)
                && string.Equals(key.guid, guid, StringComparison.OrdinalIgnoreCase)
                && key.localFileId == localFileId;
        }

        private static bool PageMatches(
            ESAssetPage page,
            string assetKey,
            string guid,
            long localFileId)
        {
            return page != null
                && string.Equals(page.EffectiveStringKey, assetKey, StringComparison.Ordinal)
                && string.Equals(page.AssetGuid, guid, StringComparison.OrdinalIgnoreCase)
                && page.LocalFileId == localFileId;
        }

        private static ESAssetLibrary RequireCleanLibrary(string libraryPath)
        {
            RequireAssetPath(libraryPath, ".asset", "AssetLibrary");
            ESAssetLibrary library = AssetDatabase.LoadAssetAtPath<ESAssetLibrary>(libraryPath);
            if (library == null)
                throw new InvalidOperationException("缺少目标 AssetLibrary：" + libraryPath);
            RequireCleanTarget(library, "AssetLibrary");
            return library;
        }

        private static void RequireCleanTarget(Object asset, string label)
        {
            if (asset != null && EditorUtility.IsDirty(asset))
                throw new InvalidOperationException(label + " 存在未保存修改，拒绝由作者工具代为保存。");
        }

        private static void EnsureEditorCanWrite()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Unity 正在 PlayMode 或准备切换，禁止生成武器作者资产。");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Unity 正在编译、域重载或导入，禁止生成武器作者资产。");
            if (ESContentRegistrationAuthoring.TryGetAuthoringWriteBlockReason(out string reason))
                throw new InvalidOperationException(reason);
        }

        private static void RequireAssetPath(string path, string extension, string label)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal)
                || !normalized.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(label + " 路径必须位于 Assets 且使用 " + extension + "：" + path);
            }
        }

        private static void EnsureParentFolder(string assetPath)
        {
            string normalized = assetPath.Replace('\\', '/');
            int separator = normalized.LastIndexOf('/');
            if (separator <= 0)
                throw new InvalidOperationException("资产路径缺少父目录：" + assetPath);

            string folderPath = normalized.Substring(0, separator);
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string parent = current;
                current += "/" + parts[i];
                if (AssetDatabase.IsValidFolder(current))
                    continue;
                if (AssetDatabase.LoadMainAssetAtPath(current) != null)
                    throw new InvalidOperationException("父目录路径已被资产占用：" + current);
                string guid = AssetDatabase.CreateFolder(parent, parts[i]);
                if (string.IsNullOrEmpty(guid))
                    throw new InvalidOperationException("创建作者目录失败：" + current);
            }
        }
    }
}
