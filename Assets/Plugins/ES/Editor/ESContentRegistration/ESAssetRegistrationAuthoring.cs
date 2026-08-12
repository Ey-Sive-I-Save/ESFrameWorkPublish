using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ES
{
    internal static class ESAssetRegistrationAuthoring
    {
        internal static ESContentRegistrationResult Execute(ESContentRegistrationRequest request)
        {
            ESContentRegistrationResult result = ESContentRegistrationResult.Create(request);
            string assetPath = ESContentRegistrationAuthoring.NormalizeAssetPath(request.assetPath);
            string libraryPath = ESContentRegistrationAuthoring.NormalizeAssetPath(request.libraryPath);
            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(libraryPath))
                return ESContentRegistrationResult.Failure(request, "invalid_request", "assetPath 和 libraryPath 必须是 Assets/ 下的项目路径。");

            ESAssetLibrary library = AssetDatabase.LoadAssetAtPath<ESAssetLibrary>(libraryPath);
            if (library == null)
                return ESContentRegistrationResult.Failure(request, "not_found", "找不到 ESAssetLibrary：" + libraryPath);

            if (!TryResolveExactAsset(assetPath, request.expectedLocalFileId, out UnityEngine.Object asset, out string guid, out long localFileId, out string resolveError))
                return ESContentRegistrationResult.Failure(request, "not_found", resolveError);
            if (!ESContentRegistrationAuthoring.TryRequireGuid("Asset", request.expectedGuid, guid, request.commit, out string assetGuidError))
                return ESContentRegistrationResult.Failure(request, "identity_conflict", assetGuidError);
            if (request.commit && localFileId != request.expectedLocalFileId)
                return ESContentRegistrationResult.Failure(request, "identity_conflict", "Asset LocalFileId 不匹配。");

            if (AssetDatabase.IsValidFolder(assetPath)
                || asset is MonoScript
                || ESAssetPipelineIO.IsEditorOnly(assetPath, asset))
            {
                return ESContentRegistrationResult.Failure(request, "unsupported_asset", "文件夹、脚本、EditorOnly 或无效资产不能作为运行时业务资产注册。");
            }
            if (asset is ScriptableObject scriptableObject
                && ESScriptableObjectClassification.GetClass(scriptableObject) == ESScriptableObjectClass.GameCore)
            {
                return ESContentRegistrationResult.Failure(request, "wrong_pipeline", "GameCore SO 必须使用 register_gamecore，不能混入普通 AssetTable。");
            }

            ESAssetReferKind detectedKind = ESAssetPage.DetermineKind(asset);
            if (!TryResolveKind(request.assetKind, detectedKind, out ESAssetReferKind kind, out string kindError))
                return ESContentRegistrationResult.Failure(request, "invalid_request", kindError);
            if (!ESContentStringKeyRules.TryValidateStableKey(
                    request.keyMode,
                    request.enumKey,
                    request.stringKey,
                    out ESContentStableKeyMode resolvedMode,
                    out string keyError))
            {
                return ESContentRegistrationResult.Failure(request, "invalid_key", keyError);
            }

            string libraryGuid = AssetDatabase.AssetPathToGUID(libraryPath);
            string revision = ESContentRegistrationAuthoring.GetAssetRevision(libraryPath);
            result.assetPath = assetPath;
            result.guid = guid;
            result.sourceGuid = guid;
            result.libraryGuid = libraryGuid;
            result.localFileId = localFileId;
            result.assetKind = kind.ToString();
            result.enumKey = request.enumKey;
            result.stringKey = request.stringKey ?? string.Empty;
            result.targetRevision = revision;
            result.message = "KeyMode=" + resolvedMode + "；目标 Library=" + libraryPath + "。";

            if (!ESContentRegistrationAuthoring.TryRequireRevision(
                    "AssetLibrary",
                    request.expectedLibraryRevision,
                    revision,
                    request.commit,
                    out string revisionError))
            {
                return ESContentRegistrationResult.Failure(request, "concurrency_conflict", revisionError);
            }
            if (!ESContentRegistrationAuthoring.TryRequireCleanTarget(
                    "AssetLibrary",
                    library,
                    request.commit,
                    out string dirtyError))
            {
                return ESContentRegistrationResult.Failure(request, "target_dirty", dirtyError);
            }

            RegistrationScan scan = ScanRegistrations(
                library,
                guid,
                localFileId,
                kind,
                request.enumKey,
                request.stringKey);
            if (!string.IsNullOrEmpty(scan.error))
                return ESContentRegistrationResult.Failure(request, "registration_conflict", scan.error);

            if (scan.existingPage != null)
            {
                result.success = true;
                result.idempotent = true;
                result.status = "already_registered";
                result.message = "目标身份与双别名已在指定 Library 中一致注册。";
                return result;
            }

            ESAssetBook targetBook = library.GetDefaultBookByKind(kind);
            if (targetBook == null)
                return ESContentRegistrationResult.Failure(request, "unsupported_asset", "目标 Library 没有 Kind=" + kind + " 的正式 Book。");

            if (!request.commit)
            {
                result.success = true;
                result.status = "validated";
                result.changed = true;
                result.message = "注册预检通过；commit 时将新增一个持久 ESAssetPage。";
                return result;
            }

            string revisionImmediatelyBeforeWrite = ESContentRegistrationAuthoring.GetAssetRevision(libraryPath);
            if (!string.Equals(revisionImmediatelyBeforeWrite, revision, StringComparison.Ordinal))
                return ESContentRegistrationResult.Failure(request, "concurrency_conflict", "AssetLibrary 在预检后发生变化，拒绝写入。");

            var page = new ESAssetPage
            {
                Name = asset.name,
                OB = asset,
                Kind = kind,
                EnumKey = request.enumKey,
                StringKey = request.stringKey,
                SourceLibrary = libraryGuid,
                SourceBook = targetBook.Name
            };
            page.RefreshAssetIdentityEditor();

            Undo.RecordObject(library, "Register ES Asset Content");
            targetBook.pages.Add(page);
            library.MarkFastIndexDirty();
            EditorUtility.SetDirty(library);

            RegistrationScan postScan = ScanRegistrations(
                library,
                guid,
                localFileId,
                kind,
                request.enumKey,
                request.stringKey);
            if (!string.IsNullOrEmpty(postScan.error) || postScan.existingPage == null)
            {
                return RollBackAddedPage(
                    request,
                    library,
                    targetBook,
                    page,
                    false,
                    "postcondition_failed",
                    string.IsNullOrEmpty(postScan.error) ? "写入后未找到精确注册页。" : postScan.error);
            }

            bool registryRegistered = false;
            try
            {
                AssetDatabase.SaveAssetIfDirty(library);
                registryRegistered = ESAssetRegistry.RegisterAsset(page, libraryGuid, targetBook.Name, startOrderIndex: 0);
                if (!registryRegistered)
                    throw new InvalidOperationException("新增页已保存，但 Editor Registry 拒绝接收其快照。");
            }
            catch (Exception exception)
            {
                return RollBackAddedPage(request, library, targetBook, page, registryRegistered, "commit_failed", exception.Message);
            }
            result.success = true;
            result.changed = true;
            result.dryRun = false;
            result.status = "committed";
            result.targetRevision = ESContentRegistrationAuthoring.GetAssetRevision(libraryPath);
            result.changedPaths.Add(libraryPath);
            result.message = "普通资产已持久注册到 AssetLibrary；尚未冒充 Bake 或运行时加载验证。";
            return result;
        }

        internal static bool TryResolveExactAsset(
            string path,
            long requestedLocalFileId,
            out UnityEngine.Object asset,
            out string guid,
            out long localFileId,
            out string error)
        {
            asset = null;
            guid = AssetDatabase.AssetPathToGUID(path);
            localFileId = 0;
            if (string.IsNullOrEmpty(guid))
            {
                error = "资产路径没有有效 GUID：" + path;
                return false;
            }

            if (requestedLocalFileId == 0)
            {
                asset = AssetDatabase.LoadMainAssetAtPath(path);
            }
            else
            {
                foreach (UnityEngine.Object candidate in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (candidate == null
                        || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out string candidateGuid, out long candidateLocalId)
                        || !string.Equals(candidateGuid, guid, StringComparison.OrdinalIgnoreCase)
                        || candidateLocalId != requestedLocalFileId)
                    {
                        continue;
                    }
                    asset = candidate;
                    break;
                }
            }

            if (asset == null || !ESAssetPage.TryGetAssetIdentityEditor(asset, out string resolvedGuid, out localFileId))
            {
                error = "找不到指定 GUID/LocalFileId 的资产：" + path + "#" + requestedLocalFileId;
                return false;
            }
            if (!string.Equals(guid, resolvedGuid, StringComparison.OrdinalIgnoreCase))
            {
                error = "AssetDatabase 返回了不一致的资产 GUID。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryResolveKind(
            string requested,
            ESAssetReferKind detected,
            out ESAssetReferKind kind,
            out string error)
        {
            kind = detected;
            if (!string.IsNullOrWhiteSpace(requested)
                && !string.Equals(requested, "auto", StringComparison.OrdinalIgnoreCase))
            {
                if (!Enum.TryParse(requested, true, out kind))
                {
                    error = "未知 ESAssetReferKind：" + requested;
                    return false;
                }
                if (kind != detected)
                {
                    error = "请求 Kind=" + kind + " 与资产检测 Kind=" + detected + " 不一致。";
                    return false;
                }
            }
            if (kind == ESAssetReferKind.None || kind == ESAssetReferKind.Other)
            {
                error = "该资产类型没有正式 ES AssetTable 分类。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static RegistrationScan ScanRegistrations(
            ESAssetLibrary targetLibrary,
            string guid,
            long localFileId,
            ESAssetReferKind kind,
            int enumKey,
            string stringKey)
        {
            ESAssetPage identityMatch = null;
            foreach (string libraryGuid in AssetDatabase.FindAssets("t:" + nameof(ESAssetLibrary)))
            {
                string path = AssetDatabase.GUIDToAssetPath(libraryGuid);
                ESAssetLibrary library = AssetDatabase.LoadAssetAtPath<ESAssetLibrary>(path);
                if (library == null)
                    continue;

                foreach (ESAssetBook book in library.GetAllUseableBooks())
                foreach (ESAssetPage page in book.pages ?? new List<ESAssetPage>())
                {
                    if (page?.OB == null)
                        continue;
                    if (!ESAssetPage.TryGetAssetIdentityEditor(page.OB, out string pageGuid, out long pageLocalFileId))
                        continue;
                    bool sameIdentity = string.Equals(pageGuid, guid, StringComparison.OrdinalIgnoreCase)
                                        && pageLocalFileId == localFileId;
                    if (sameIdentity)
                    {
                        if (!ReferenceEquals(library, targetLibrary))
                            return RegistrationScan.Fail("同一资产身份已由其他 Library 持有：" + path);
                        if (page.Kind != kind
                            || page.EnumKey != enumKey
                            || !string.Equals(page.EffectiveStringKey, stringKey, StringComparison.Ordinal))
                        {
                            return RegistrationScan.Fail("同一资产身份已有不同 Kind/Key；注册入口禁止静默改名或迁移。");
                        }
                        identityMatch = page;
                        continue;
                    }

                    if (page.Kind != kind)
                        continue;
                    if (enumKey != 0 && page.EnumKey == enumKey)
                        return RegistrationScan.Fail("EnumKey 已被其他资产占用：" + enumKey);
                    if (!string.IsNullOrEmpty(stringKey)
                        && string.Equals(page.EffectiveStringKey, stringKey, StringComparison.Ordinal))
                    {
                        return RegistrationScan.Fail("StringKey 已被其他资产占用：" + stringKey);
                    }
                }
            }

            return new RegistrationScan { existingPage = identityMatch };
        }

        internal static bool TryValidateRegisteredAssetReference(
            string label,
            ESAssetReferKind kind,
            int enumKey,
            string stringKey,
            string guid,
            long localFileId,
            bool required,
            out string error)
        {
            bool configured = enumKey != 0 || !string.IsNullOrEmpty(stringKey);
            if (!configured)
            {
                error = required ? label + " 必须配置类型化 AssetKey。" : string.Empty;
                return !required;
            }
            if (string.IsNullOrEmpty(guid))
            {
                error = label + " 缺少不可变 Asset GUID；请通过 AssetKey Drawer 或正式注册入口选择资源。";
                return false;
            }

            foreach (string libraryGuid in AssetDatabase.FindAssets("t:" + nameof(ESAssetLibrary)))
            {
                string libraryPath = AssetDatabase.GUIDToAssetPath(libraryGuid);
                ESAssetLibrary library = AssetDatabase.LoadAssetAtPath<ESAssetLibrary>(libraryPath);
                if (library == null)
                    continue;

                foreach (ESAssetBook book in library.GetAllUseableBooks())
                foreach (ESAssetPage page in book.pages ?? new List<ESAssetPage>())
                {
                    if (page?.OB == null
                        || !ESAssetPage.TryGetAssetIdentityEditor(page.OB, out string pageGuid, out long pageLocalFileId))
                    {
                        continue;
                    }

                    bool sameIdentity = string.Equals(pageGuid, guid, StringComparison.OrdinalIgnoreCase)
                                        && pageLocalFileId == localFileId;
                    bool sameKey = page.Kind == kind
                                   && (enumKey == 0 || page.EnumKey == enumKey)
                                   && (string.IsNullOrEmpty(stringKey)
                                       || string.Equals(page.EffectiveStringKey, stringKey, StringComparison.Ordinal));
                    if (sameIdentity && sameKey)
                    {
                        error = string.Empty;
                        return true;
                    }
                    if (sameIdentity)
                    {
                        error = label + " 的 GUID/LocalFileId 已注册为不同 Kind 或 Key：" + libraryPath;
                        return false;
                    }
                    if (sameKey)
                    {
                        error = label + " 的类型化 AssetKey 已指向其他资产：" + libraryPath;
                        return false;
                    }
                }
            }

            error = label + " 尚未进入任何正式 ESAssetLibrary；GameCore 不能直接携带或旁路加载该资源。";
            return false;
        }

        private static ESContentRegistrationResult RollBackAddedPage(
            ESContentRegistrationRequest request,
            ESAssetLibrary library,
            ESAssetBook targetBook,
            ESAssetPage page,
            bool removeRegistrySnapshot,
            string status,
            string message)
        {
            targetBook.pages.Remove(page);
            library.MarkFastIndexDirty();
            if (removeRegistrySnapshot)
                ESAssetRegistry.RemoveAsset(page);
            EditorUtility.SetDirty(library);
            try
            {
                AssetDatabase.SaveAssetIfDirty(library);
            }
            catch (Exception rollbackException)
            {
                message += " 回滚落盘失败：" + rollbackException.Message;
            }
            return ESContentRegistrationResult.Failure(request, status, message);
        }

        private sealed class RegistrationScan
        {
            public ESAssetPage existingPage;
            public string error = string.Empty;

            public static RegistrationScan Fail(string error)
                => new RegistrationScan { error = error ?? string.Empty };
        }
    }
}
