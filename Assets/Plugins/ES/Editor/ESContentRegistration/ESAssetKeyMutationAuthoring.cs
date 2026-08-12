using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    internal static class ESAssetKeyMutationAuthoring
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
            if (!ESAssetRegistrationAuthoring.TryResolveExactAsset(
                    assetPath,
                    request.expectedLocalFileId,
                    out UnityEngine.Object asset,
                    out string guid,
                    out long localFileId,
                    out string resolveError))
            {
                return ESContentRegistrationResult.Failure(request, "not_found", resolveError);
            }

            if (!ESContentRegistrationAuthoring.TryRequireGuid("Asset", request.expectedGuid, guid, request.commit, out string guidError))
                return ESContentRegistrationResult.Failure(request, "identity_conflict", guidError);
            if (request.commit && localFileId != request.expectedLocalFileId)
                return ESContentRegistrationResult.Failure(request, "identity_conflict", "Asset LocalFileId 不匹配。");

            ESAssetReferKind kind = ESAssetPage.DetermineKind(asset);
            if (kind == ESAssetReferKind.None || kind == ESAssetReferKind.Other)
                return ESContentRegistrationResult.Failure(request, "unsupported_asset", "该资产类型没有正式 ES AssetTable 分类。");
            if (!ESContentStringKeyRules.TryValidateStableKey(
                    request.keyMode,
                    request.enumKey,
                    request.stringKey,
                    out ESContentStableKeyMode resolvedMode,
                    out string keyError))
            {
                return ESContentRegistrationResult.Failure(request, "invalid_key", keyError);
            }

            if (!TryFindRegisteredPage(library, guid, localFileId, out ESAssetBook book, out ESAssetPage page, out string findError))
                return ESContentRegistrationResult.Failure(request, "not_found", findError);
            if (page.Kind != kind)
                return ESContentRegistrationResult.Failure(request, "identity_conflict", "注册 Page 的 Kind 与当前资产类型不一致。");

            string revision = ESContentRegistrationAuthoring.GetAssetRevision(libraryPath);
            result.assetPath = assetPath;
            result.guid = guid;
            result.sourceGuid = guid;
            result.libraryGuid = AssetDatabase.AssetPathToGUID(libraryPath);
            result.localFileId = localFileId;
            result.assetKind = kind.ToString();
            result.currentEnumKey = page.EnumKey;
            result.currentStringKey = page.EffectiveStringKey;
            result.enumKey = request.enumKey;
            result.stringKey = request.stringKey ?? string.Empty;
            result.targetRevision = revision;

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
            if (request.commit && !request.hasExpectedCurrentKey)
            {
                return ESContentRegistrationResult.Failure(
                    request,
                    "invalid_request",
                    "UpdateAssetKey commit 必须显式提供 hasExpectedCurrentKey=true 和预检返回的当前 Key。");
            }
            if (request.commit
                && (page.EnumKey != request.expectedCurrentEnumKey
                    || !string.Equals(page.EffectiveStringKey, request.expectedCurrentStringKey ?? string.Empty, StringComparison.Ordinal)))
            {
                return ESContentRegistrationResult.Failure(request, "concurrency_conflict", "Asset 当前 Key 与预检结果不一致。");
            }
            if (!TryValidateGlobalKeyAvailability(library, page, kind, request.enumKey, request.stringKey, out string conflictError))
                return ESContentRegistrationResult.Failure(request, "key_conflict", conflictError);

            bool wouldChange = page.EnumKey != request.enumKey
                               || !string.Equals(page.EffectiveStringKey, request.stringKey, StringComparison.Ordinal);
            if (!request.commit)
            {
                result.success = true;
                result.changed = wouldChange;
                result.idempotent = !wouldChange;
                result.status = wouldChange ? "validated" : "already_registered";
                result.message = wouldChange
                    ? "Key 迁移预检通过；KeyMode=" + resolvedMode + "。commit 将只修改目标 Library。"
                    : "资产稳定 Key 已与请求一致。";
                return result;
            }

            if (!wouldChange)
            {
                result.success = true;
                result.idempotent = true;
                result.dryRun = false;
                result.status = "already_registered";
                result.message = "资产稳定 Key 已与请求一致。";
                return result;
            }
            if (!string.Equals(ESContentRegistrationAuthoring.GetAssetRevision(libraryPath), revision, StringComparison.Ordinal))
                return ESContentRegistrationResult.Failure(request, "concurrency_conflict", "AssetLibrary 在预检后发生变化，拒绝写入。");

            int oldEnumKey = page.EnumKey;
            string oldStringKey = page.StringKey;
            try
            {
                Undo.RecordObject(library, "Update ES Asset Stable Key");
                page.EnumKey = request.enumKey;
                page.StringKey = request.stringKey ?? string.Empty;
                library.MarkFastIndexDirty();
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssetIfDirty(library);
                library.InjectToAssetRegistryEditor();

                if (!TryFindRegisteredPage(library, guid, localFileId, out _, out ESAssetPage persisted, out string postError)
                    || persisted.EnumKey != request.enumKey
                    || !string.Equals(persisted.EffectiveStringKey, request.stringKey, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(string.IsNullOrEmpty(postError) ? "Key 写入后置条件失败。" : postError);
                }
            }
            catch (Exception exception)
            {
                page.EnumKey = oldEnumKey;
                page.StringKey = oldStringKey;
                library.MarkFastIndexDirty();
                EditorUtility.SetDirty(library);
                string rollbackError = string.Empty;
                try
                {
                    AssetDatabase.SaveAssetIfDirty(library);
                    library.InjectToAssetRegistryEditor();
                }
                catch (Exception rollbackException)
                {
                    rollbackError = " 回滚落盘失败：" + rollbackException.Message;
                }
                return ESContentRegistrationResult.Failure(request, "commit_failed", exception.Message + rollbackError);
            }

            result.success = true;
            result.changed = true;
            result.dryRun = false;
            result.status = "committed";
            result.targetRevision = ESContentRegistrationAuthoring.GetAssetRevision(libraryPath);
            result.changedPaths.Add(libraryPath);
            result.warnings.Add("稳定 Key 已迁移；Bake 会拒绝仍缓存旧别名的下游引用，提交发布前必须完成引用同步。");
            result.message = "资产稳定 Key 已通过统一事务更新；尚未冒充 Bake 或运行时验证。";
            return result;
        }

        private static bool TryFindRegisteredPage(
            ESAssetLibrary targetLibrary,
            string guid,
            long localFileId,
            out ESAssetBook targetBook,
            out ESAssetPage targetPage,
            out string error)
        {
            targetBook = null;
            targetPage = null;
            foreach (string libraryGuid in AssetDatabase.FindAssets("t:" + nameof(ESAssetLibrary)))
            {
                string path = AssetDatabase.GUIDToAssetPath(libraryGuid);
                ESAssetLibrary library = AssetDatabase.LoadAssetAtPath<ESAssetLibrary>(path);
                if (library == null)
                    continue;
                foreach (ESAssetBook book in library.GetAllUseableBooks())
                foreach (ESAssetPage page in book.pages ?? new List<ESAssetPage>())
                {
                    if (page?.OB == null
                        || !ESAssetPage.TryGetAssetIdentityEditor(page.OB, out string pageGuid, out long pageLocalFileId)
                        || !string.Equals(pageGuid, guid, StringComparison.OrdinalIgnoreCase)
                        || pageLocalFileId != localFileId)
                    {
                        continue;
                    }

                    if (!ReferenceEquals(library, targetLibrary))
                    {
                        error = "同一资产身份由其他 Library 持有：" + path;
                        return false;
                    }
                    if (targetPage != null)
                    {
                        error = "目标 Library 内存在重复资产身份，必须先修复数据完整性。";
                        return false;
                    }
                    targetBook = book;
                    targetPage = page;
                }
            }

            error = targetPage == null ? "目标资产尚未通过统一入口注册。" : string.Empty;
            return targetPage != null;
        }

        private static bool TryValidateGlobalKeyAvailability(
            ESAssetLibrary targetLibrary,
            ESAssetPage targetPage,
            ESAssetReferKind kind,
            int enumKey,
            string stringKey,
            out string error)
        {
            foreach (string libraryGuid in AssetDatabase.FindAssets("t:" + nameof(ESAssetLibrary)))
            {
                ESAssetLibrary library = AssetDatabase.LoadAssetAtPath<ESAssetLibrary>(AssetDatabase.GUIDToAssetPath(libraryGuid));
                if (library == null)
                    continue;
                foreach (ESAssetBook book in library.GetAllUseableBooks())
                foreach (ESAssetPage page in book.pages ?? new List<ESAssetPage>())
                {
                    if (page == null || (ReferenceEquals(library, targetLibrary) && ReferenceEquals(page, targetPage)) || page.Kind != kind)
                        continue;
                    if (enumKey != 0 && page.EnumKey == enumKey)
                    {
                        error = "EnumKey 已被其他资产占用：" + enumKey;
                        return false;
                    }
                    if (!string.IsNullOrEmpty(stringKey)
                        && string.Equals(page.EffectiveStringKey, stringKey, StringComparison.Ordinal))
                    {
                        error = "StringKey 已被其他资产占用：" + stringKey;
                        return false;
                    }
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
