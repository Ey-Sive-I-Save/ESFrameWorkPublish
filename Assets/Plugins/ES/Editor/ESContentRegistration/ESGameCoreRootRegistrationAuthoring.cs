using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ES
{
    internal static class ESGameCoreRootRegistrationAuthoring
    {
        internal static ESContentRegistrationResult Execute(ESContentRegistrationRequest request)
        {
            string sourcePath = ESContentRegistrationAuthoring.NormalizeAssetPath(request.gameCorePath);
            string consumerPath = ESContentRegistrationAuthoring.NormalizeAssetPath(request.consumerPath);
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(consumerPath))
                return ESContentRegistrationResult.Failure(request, "invalid_request", "gameCorePath 和 consumerPath 都必须是 Assets/ 下的项目路径。");

            if (!ESAssetRegistrationAuthoring.TryResolveExactAsset(
                    sourcePath,
                    request.expectedLocalFileId,
                    out UnityEngine.Object resolved,
                    out string sourceGuid,
                    out long localFileId,
                    out string resolveError)
                || !(resolved is ScriptableObject source)
                || ESScriptableObjectClassification.GetClass(source) != ESScriptableObjectClass.GameCore)
            {
                return ESContentRegistrationResult.Failure(
                    request,
                    "unsupported_gamecore",
                    string.IsNullOrEmpty(resolveError) ? "源资产必须是正式 GameCore ScriptableObject。" : resolveError);
            }

            ESAssetLibraryConsumer consumer = AssetDatabase.LoadAssetAtPath<ESAssetLibraryConsumer>(consumerPath);
            if (consumer == null)
                return ESContentRegistrationResult.Failure(request, "not_found", "找不到 ESAssetLibraryConsumer：" + consumerPath);

            string consumerGuid = AssetDatabase.AssetPathToGUID(consumerPath);
            bool sourceGuidValid = ESContentRegistrationAuthoring.TryRequireGuid(
                "GameCore", request.expectedSourceGuid, sourceGuid, request.commit, out string sourceGuidError);
            bool consumerGuidValid = ESContentRegistrationAuthoring.TryRequireGuid(
                "Consumer", request.expectedConsumerGuid, consumerGuid, request.commit, out string consumerGuidError);
            if (!sourceGuidValid || !consumerGuidValid)
            {
                return ESContentRegistrationResult.Failure(
                    request,
                    "identity_conflict",
                    !string.IsNullOrEmpty(sourceGuidError) ? sourceGuidError : consumerGuidError);
            }
            if (request.commit && localFileId != request.expectedLocalFileId)
                return ESContentRegistrationResult.Failure(request, "identity_conflict", "GameCore LocalFileId 不匹配。");

            string sourceRevision = ESContentRegistrationAuthoring.GetAssetRevision(sourcePath);
            string consumerRevision = ESContentRegistrationAuthoring.GetAssetRevision(consumerPath);
            bool sourceRevisionValid = ESContentRegistrationAuthoring.TryRequireRevision(
                "GameCore", request.expectedSourceRevision, sourceRevision, request.commit, out string sourceRevisionError);
            bool consumerRevisionValid = ESContentRegistrationAuthoring.TryRequireRevision(
                "Consumer", request.expectedConsumerRevision, consumerRevision, request.commit, out string consumerRevisionError);
            if (!sourceRevisionValid || !consumerRevisionValid)
            {
                return ESContentRegistrationResult.Failure(
                    request,
                    "concurrency_conflict",
                    !string.IsNullOrEmpty(sourceRevisionError) ? sourceRevisionError : consumerRevisionError);
            }
            bool sourceClean = ESContentRegistrationAuthoring.TryRequireCleanTarget(
                "GameCore", source, request.commit, out string sourceDirtyError);
            bool consumerClean = ESContentRegistrationAuthoring.TryRequireCleanTarget(
                "Consumer", consumer, request.commit, out string consumerDirtyError);
            if (!sourceClean || !consumerClean)
            {
                return ESContentRegistrationResult.Failure(
                    request,
                    "target_dirty",
                    !string.IsNullOrEmpty(sourceDirtyError) ? sourceDirtyError : consumerDirtyError);
            }

            ESAssetIdentity identity = new ESAssetIdentity(sourceGuid, localFileId);
            bool rootAlreadyLinked = ContainsIdentity(consumer.ManualGameCoreAssets, identity);
            if (!TryBuildPreviewSnapshot(consumer, identity, rootAlreadyLinked, out List<ESAssetReferBase> generated, out List<string> errors))
            {
                ESContentRegistrationResult failure = ESContentRegistrationResult.Failure(
                    request,
                    "validation_failed",
                    string.Join("\n", errors));
                failure.errors = errors;
                return failure;
            }

            List<ESAssetReferBase> normalized = Normalize(generated);
            bool snapshotMatches = ReferencesEqual(consumer.GameCoreAssets, normalized)
                                   && (consumer.GameCoreValidationErrors == null || consumer.GameCoreValidationErrors.Count == 0);
            bool wouldChange = !rootAlreadyLinked || !snapshotMatches || string.IsNullOrEmpty(consumer.ConsumerId);
            var result = ESContentRegistrationResult.Create(request);
            result.assetPath = sourcePath;
            result.guid = sourceGuid;
            result.sourceGuid = sourceGuid;
            result.consumerGuid = consumerGuid;
            result.localFileId = localFileId;
            result.sourceRevision = sourceRevision;
            result.consumerRevision = consumerRevision;
            result.message = "Consumer GameCore 快照包含 " + normalized.Count + " 个根/依赖资产。";

            if (!request.commit)
            {
                result.success = true;
                result.changed = wouldChange;
                result.idempotent = !wouldChange;
                result.status = wouldChange ? "validated" : "already_registered";
                return result;
            }
            if (!wouldChange)
            {
                result.success = true;
                result.idempotent = true;
                result.dryRun = false;
                result.status = "already_registered";
                return result;
            }
            if (!string.Equals(ESContentRegistrationAuthoring.GetAssetRevision(sourcePath), sourceRevision, StringComparison.Ordinal)
                || !string.Equals(ESContentRegistrationAuthoring.GetAssetRevision(consumerPath), consumerRevision, StringComparison.Ordinal))
            {
                return ESContentRegistrationResult.Failure(request, "concurrency_conflict", "GameCore 或 Consumer 在预检后发生变化，拒绝写入。");
            }

            List<ESAssetReferBase> oldManual = consumer.ManualGameCoreAssets != null
                ? new List<ESAssetReferBase>(consumer.ManualGameCoreAssets)
                : null;
            List<ESAssetReferBase> oldGenerated = consumer.GameCoreAssets != null
                ? new List<ESAssetReferBase>(consumer.GameCoreAssets)
                : null;
            List<string> oldErrors = consumer.GameCoreValidationErrors != null
                ? new List<string>(consumer.GameCoreValidationErrors)
                : null;
            string oldConsumerId = consumer.ConsumerId;
            try
            {
                Undo.RecordObject(consumer, "Register ES GameCore Root");
                consumer.ManualGameCoreAssets ??= new List<ESAssetReferBase>();
                if (!rootAlreadyLinked)
                    consumer.ManualGameCoreAssets.Add(CreateReference(identity));
                consumer.GameCoreAssets = normalized;
                consumer.GameCoreValidationErrors = new List<string>();
                consumer.EnsureStableIdentity();
                EditorUtility.SetDirty(consumer);
                AssetDatabase.SaveAssetIfDirty(consumer);

                if (!ContainsIdentity(consumer.ManualGameCoreAssets, identity)
                    || !ReferencesEqual(consumer.GameCoreAssets, normalized))
                {
                    throw new InvalidOperationException("GameCore Root 写入后置条件失败。");
                }
            }
            catch (Exception exception)
            {
                consumer.ManualGameCoreAssets = oldManual;
                consumer.GameCoreAssets = oldGenerated;
                consumer.GameCoreValidationErrors = oldErrors;
                consumer.ConsumerId = oldConsumerId;
                EditorUtility.SetDirty(consumer);
                string rollbackError = string.Empty;
                try { AssetDatabase.SaveAssetIfDirty(consumer); }
                catch (Exception rollbackException) { rollbackError = " 回滚落盘失败：" + rollbackException.Message; }
                return ESContentRegistrationResult.Failure(request, "commit_failed", exception.Message + rollbackError);
            }

            result.success = true;
            result.changed = true;
            result.dryRun = false;
            result.status = "committed";
            result.consumerRevision = ESContentRegistrationAuthoring.GetAssetRevision(consumerPath);
            result.changedPaths.Add(consumerPath);
            return result;
        }

        private static bool TryBuildPreviewSnapshot(
            ESAssetLibraryConsumer consumer,
            ESAssetIdentity identity,
            bool alreadyLinked,
            out List<ESAssetReferBase> generated,
            out List<string> errors)
        {
            ESAssetLibraryConsumer preview = UnityEngine.Object.Instantiate(consumer);
            preview.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                preview.ManualGameCoreAssets = consumer.ManualGameCoreAssets != null
                    ? new List<ESAssetReferBase>(consumer.ManualGameCoreAssets)
                    : new List<ESAssetReferBase>();
                if (!alreadyLinked)
                    preview.ManualGameCoreAssets.Add(CreateReference(identity));
                List<ESAssetLibrary> libraries = ESEditorSO.GetGroupOfType<ESAssetLibrary>()
                    ?.Where(item => item != null).ToList() ?? new List<ESAssetLibrary>();
                errors = ESAssetReferenceBaker.BuildConsumerGameCoreSnapshot(preview, libraries, out generated);
                return errors.Count == 0;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preview);
            }
        }

        private static ESAssetReferBase CreateReference(ESAssetIdentity identity)
        {
            var refer = new ESAssetReferScriptableObject();
            refer.InitializeGeneratedReference(identity.Guid, identity.LocalFileId, ESAssetReferKind.ScriptableObject, 0, string.Empty);
            return refer;
        }

        private static List<ESAssetReferBase> Normalize(IEnumerable<ESAssetReferBase> source)
        {
            return (source ?? Enumerable.Empty<ESAssetReferBase>())
                .Where(item => item != null && item.IsValid)
                .GroupBy(item => item.AssetIdentity)
                .Select(group => group.First())
                .OrderBy(item => item.GUID, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.LocalFileId)
                .ToList();
        }

        internal static bool ReferencesEqual(IEnumerable<ESAssetReferBase> left, IEnumerable<ESAssetReferBase> right)
        {
            ESAssetIdentity[] a = Normalize(left).Select(item => item.AssetIdentity).ToArray();
            ESAssetIdentity[] b = Normalize(right).Select(item => item.AssetIdentity).ToArray();
            return a.SequenceEqual(b);
        }

        private static bool ContainsIdentity(IEnumerable<ESAssetReferBase> source, ESAssetIdentity identity)
            => (source ?? Enumerable.Empty<ESAssetReferBase>()).Any(item => item != null && item.AssetIdentity.Equals(identity));
    }
}
