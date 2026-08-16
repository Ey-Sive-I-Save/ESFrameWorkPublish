using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Editor-only content registration facade. MCP, menus and tests share this entry;
    /// runtime code never writes back to authoring assets.
    /// </summary>
    public static class ESContentRegistrationAuthoring
    {
        private const string RequestStatePrefix = "ES.ContentRegistration.Request.";
        private const string BakeStatePrefix = "ES.ContentRegistration.Bake.";
        private static readonly object Gate = new object();
        private static readonly HashSet<string> BakeCompletionSubscriptions = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> AcceptedPreflights = new Dictionary<string, string>(StringComparer.Ordinal);
        private static Mutex heldBakeProcessGate;
        private static string heldBakeTaskId = string.Empty;
        private static bool bakeLifecycleSubscribed;
        private static int editorMainThreadId;

        [InitializeOnLoadMethod]
        private static void CaptureEditorMainThread()
        {
            editorMainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        [Serializable]
        private sealed class StoredRequest
        {
            public string fingerprint = string.Empty;
            public ESContentRegistrationResult result;
        }

        [Serializable]
        private sealed class StoredBake
        {
            public string runId = string.Empty;
            public List<string> requestIds = new List<string>();
            public string editorTaskId = string.Empty;
            public string status = string.Empty;
            public string message = string.Empty;
        }

        public static ESContentRegistrationResult Execute(ESContentRegistrationRequest request)
        {
            if (request == null)
                return ESContentRegistrationResult.Failure(null, "invalid_request", "注册请求不能为空。");
            if (editorMainThreadId == 0 || Thread.CurrentThread.ManagedThreadId != editorMainThreadId)
            {
                return ESContentRegistrationResult.Failure(
                    request,
                    "editor_thread_required",
                    "内容注册只能在 Unity Editor 主线程执行；调用方必须切回 Editor 主线程后重试。");
            }

            lock (Gate)
            {
                if (request.action == ESContentRegistrationAction.Status)
                    return GetStatus(request);

                if (!TryValidateEditorState(out string editorError))
                    return ESContentRegistrationResult.Failure(request, "editor_busy", editorError);

                string fingerprint = ComputeFingerprint(request);
                string preflightFingerprint = ComputePreflightFingerprint(request);
                string preflightRequestId = request.requestId;
                if (request.commit)
                {
                    if (!ESContentStringKeyRules.TryValidateRequestId(request.requestId, out string requestError))
                        return ESContentRegistrationResult.Failure(request, "invalid_request", requestError);
                    if (TryGetStoredRequest(request.requestId, out StoredRequest stored))
                    {
                        if (!string.Equals(stored.fingerprint, fingerprint, StringComparison.Ordinal))
                            return ESContentRegistrationResult.Failure(
                                request,
                                "idempotency_conflict",
                                "同一 requestId 已用于不同请求；拒绝复用幂等键。");

                        ESContentRegistrationResult replay = Clone(stored.result);
                        replay.idempotent = true;
                        return replay;
                    }
                    if (!AcceptedPreflights.TryGetValue(request.requestId, out string acceptedFingerprint)
                        || !string.Equals(acceptedFingerprint, preflightFingerprint, StringComparison.Ordinal))
                    {
                        return ESContentRegistrationResult.Failure(
                            request,
                            "preview_required",
                            "提交前必须在当前 Unity Editor 进程执行 commit=false 预检，并回传预检结果中的同一 requestId；Domain Reload 或输入变化后必须重新预检。");
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(preflightRequestId))
                        preflightRequestId = CreatePreflightRequestId();
                    if (!ESContentStringKeyRules.TryValidateRequestId(preflightRequestId, out string requestError))
                        return ESContentRegistrationResult.Failure(request, "invalid_request", requestError);
                    if (TryGetStoredRequest(preflightRequestId, out _))
                    {
                        return ESContentRegistrationResult.Failure(
                            request,
                            "idempotency_conflict",
                            "该 requestId 已完成提交，不能重新用作预检身份。");
                    }
                    if (AcceptedPreflights.TryGetValue(preflightRequestId, out string existingFingerprint)
                        && !string.Equals(existingFingerprint, preflightFingerprint, StringComparison.Ordinal))
                    {
                        return ESContentRegistrationResult.Failure(
                            request,
                            "idempotency_conflict",
                            "同一 requestId 已用于不同预检请求；拒绝复用预检身份。");
                    }
                }

                bool joinsLocalBake = request.commit
                                      && request.action == ESContentRegistrationAction.Bake
                                      && TryGetHeldBakeTask(out _);
                if (request.commit
                    && request.action != ESContentRegistrationAction.Bake
                    && (TryGetHeldBakeTask(out _) || ESAssetReferenceBaker.IsBakeActive))
                {
                    return ESContentRegistrationResult.Failure(
                        request,
                        "bake_in_progress",
                        "资源 Bake 正在读取注册源；完成或取消前禁止提交新的内容注册写入。");
                }

                bool ownsProcessGate = false;
                Mutex processGate = null;
                try
                {
                    if (request.commit
                        && !joinsLocalBake
                        && !TryEnterProcessGate(out processGate, out ownsProcessGate, out string gateError))
                        return ESContentRegistrationResult.Failure(request, "registration_busy", gateError);

                    if (request.commit)
                        AcceptedPreflights.Remove(request.requestId);
                    ESContentRegistrationResult result = ExecuteCore(request);
                    if (!request.commit && result.success)
                    {
                        result.requestId = preflightRequestId;
                        AcceptedPreflights[preflightRequestId] = preflightFingerprint;
                    }
                    if (request.commit
                        && request.action == ESContentRegistrationAction.Bake
                        && result.success
                        && string.Equals(result.status, "pending", StringComparison.Ordinal)
                        && ownsProcessGate)
                    {
                        HoldBakeProcessGate(processGate, result.runId);
                        processGate = null;
                        ownsProcessGate = false;
                    }
                    if (request.commit && result.success)
                    {
                        StoreRequest(request.requestId, fingerprint, result);
                        NotifyEditorContentIndexes(request, result);
                    }
                    return result;
                }
                catch (Exception exception)
                {
                    return ESContentRegistrationResult.Failure(request, "failed", exception.Message);
                }
                finally
                {
                    if (ownsProcessGate)
                        processGate?.ReleaseMutex();
                    processGate?.Dispose();
                }
            }
        }

        /// <summary>
        /// 人工启动正式 Bake 的统一入口。预检与提交使用同一 requestId，实际执行仍只经过 Execute。
        /// </summary>
        public static ESContentRegistrationResult ExecuteBakeWithConfirmation()
        {
            ESContentRegistrationResult preview = Execute(new ESContentRegistrationRequest
            {
                action = ESContentRegistrationAction.Bake,
                commit = false
            });
            if (preview == null || !preview.success)
                return preview;

            if (!EditorUtility.DisplayDialog(
                    "启动资源引用 Bake",
                    "预检已通过。Bake 将写入 ES/ResourcePipeline/Baked，并在任务完成前冻结注册源。是否继续？",
                    "启动 Bake",
                    "取消"))
            {
                return null;
            }

            return Execute(new ESContentRegistrationRequest
            {
                action = ESContentRegistrationAction.Bake,
                requestId = preview.requestId,
                commit = true
            });
        }

        public static bool TryGetAuthoringWriteBlockReason(out string reason)
        {
            if (editorMainThreadId == 0 || Thread.CurrentThread.ManagedThreadId != editorMainThreadId)
            {
                reason = "内容作者态写入只能在 Unity Editor 主线程执行。";
                return true;
            }

            lock (Gate)
            {
                if (!TryValidateEditorState(out reason))
                    return true;
                if (TryGetHeldBakeTask(out _) || ESAssetReferenceBaker.IsBakeActive)
                {
                    reason = "资源 Bake 正在读取作者源；完成或取消前禁止修改 Library、GameCore 或 Consumer。";
                    return true;
                }
            }

            reason = string.Empty;
            return false;
        }

        public static string GetAssetRevision(UnityEngine.Object asset)
        {
            if (asset == null)
                return string.Empty;
            return GetAssetRevision(AssetDatabase.GetAssetPath(asset));
        }

        public static string GetAssetRevision(string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath);
            if (string.IsNullOrEmpty(assetPath))
                return string.Empty;

            string dependencyHash = AssetDatabase.GetAssetDependencyHash(assetPath).ToString();
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string absolutePath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            string fileHash = File.Exists(absolutePath) ? ComputeFileSha256(absolutePath) : string.Empty;
            return dependencyHash + ":" + fileHash;
        }

        internal static string NormalizeAssetPath(string assetPath)
        {
            string normalized = (assetPath ?? string.Empty).Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) && !string.Equals(normalized, "Assets", StringComparison.Ordinal))
                return string.Empty;
            return normalized;
        }

        internal static bool TryRequireRevision(
            string label,
            string expected,
            string actual,
            bool commit,
            out string error)
        {
            if (!commit)
            {
                error = string.Empty;
                return true;
            }
            if (string.IsNullOrEmpty(expected))
            {
                error = label + " 缺少 expected revision；请先 dryRun/inspect，再用返回的 revision 提交。";
                return false;
            }
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                error = label + " 已被其他编辑改动；expected=" + expected + "，actual=" + actual + "。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal static bool TryRequireGuid(
            string label,
            string expected,
            string actual,
            bool commit,
            out string error)
        {
            if (!commit && string.IsNullOrEmpty(expected))
            {
                error = string.Empty;
                return true;
            }
            if (string.IsNullOrEmpty(expected))
            {
                error = label + " 缺少 expected GUID；commit 必须绑定不可变资产身份。";
                return false;
            }
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                error = label + " GUID 不匹配；expected=" + expected + "，actual=" + actual + "。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal static bool TryRequireCleanTarget(
            string label,
            UnityEngine.Object target,
            bool commit,
            out string error)
        {
            if (!commit || target == null || !EditorUtility.IsDirty(target))
            {
                error = string.Empty;
                return true;
            }

            error = label + " 存在尚未保存的本地编辑；请先保存并重新 inspect，避免注册提交顺带落盘其他人的改动。";
            return false;
        }

        private static ESContentRegistrationResult ExecuteCore(ESContentRegistrationRequest request)
        {
            switch (request.action)
            {
                case ESContentRegistrationAction.RegisterAsset:
                    return ESAssetRegistrationAuthoring.Execute(request);
                case ESContentRegistrationAction.UpdateAssetKey:
                    return ESAssetKeyMutationAuthoring.Execute(request);
                case ESContentRegistrationAction.RegisterGameCore:
                    return ESGameCoreRegistrationAuthoring.Execute(request);
                case ESContentRegistrationAction.RegisterGameCoreRoot:
                    return ESGameCoreRootRegistrationAuthoring.Execute(request);
                case ESContentRegistrationAction.Synchronize:
                    return SynchronizeConsumer(request);
                case ESContentRegistrationAction.Bake:
                    return StartBake(request);
                case ESContentRegistrationAction.Inspect:
                case ESContentRegistrationAction.Validate:
                    if (!string.IsNullOrEmpty(request.libraryPath))
                        return ESAssetRegistrationAuthoring.Execute(request);
                    if (!string.IsNullOrEmpty(request.groupPath))
                        return ESGameCoreRegistrationAuthoring.Execute(request);
                    return ESContentRegistrationResult.Failure(
                        request,
                        "invalid_request",
                        "inspect/validate 必须提供 libraryPath 或 groupPath。");
                default:
                    return ESContentRegistrationResult.Failure(request, "invalid_request", "不支持的注册动作。");
            }
        }

        private static ESContentRegistrationResult SynchronizeConsumer(ESContentRegistrationRequest request)
        {
            string path = NormalizeAssetPath(request.consumerPath);
            ESAssetLibraryConsumer consumer = AssetDatabase.LoadAssetAtPath<ESAssetLibraryConsumer>(path);
            if (consumer == null)
                return ESContentRegistrationResult.Failure(request, "not_found", "找不到 ESAssetLibraryConsumer：" + request.consumerPath);

            string guid = AssetDatabase.AssetPathToGUID(path);
            string revision = GetAssetRevision(path);
            var result = ESContentRegistrationResult.Create(request);
            result.assetPath = path;
            result.guid = guid;
            result.consumerGuid = guid;
            result.consumerRevision = revision;
            bool guidValid = TryRequireGuid("Consumer", request.expectedConsumerGuid, guid, request.commit, out string guidError);
            bool revisionValid = TryRequireRevision("Consumer", request.expectedConsumerRevision, revision, request.commit, out string revisionError);
            if (!guidValid || !revisionValid)
            {
                return ESContentRegistrationResult.Failure(
                    request,
                    "concurrency_conflict",
                    !string.IsNullOrEmpty(guidError) ? guidError : revisionError);
            }
            if (!TryRequireCleanTarget("Consumer", consumer, request.commit, out string dirtyError))
                return ESContentRegistrationResult.Failure(request, "target_dirty", dirtyError);

            List<ESAssetLibrary> libraries = ESEditorSO.GetGroupOfType<ESAssetLibrary>()
                ?.Where(item => item != null).ToList() ?? new List<ESAssetLibrary>();
            List<string> errors = ESAssetReferenceBaker.BuildConsumerGameCoreSnapshot(
                consumer,
                libraries,
                out List<ESAssetReferBase> generated);
            generated = generated
                .Where(entry => entry != null && entry.IsValid)
                .GroupBy(entry => entry.AssetIdentity)
                .Select(entries => entries.First())
                .OrderBy(entry => entry.GUID, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.LocalFileId)
                .ToList();
            errors = errors.Distinct(StringComparer.Ordinal).OrderBy(entry => entry, StringComparer.Ordinal).ToList();
            bool wouldChange = string.IsNullOrEmpty(consumer.ConsumerId)
                               || !ESGameCoreRootRegistrationAuthoring.ReferencesEqual(consumer.GameCoreAssets, generated)
                               || !StringListsEqual(consumer.GameCoreValidationErrors, errors);
            result.changed = wouldChange;
            result.idempotent = !wouldChange;
            result.message = "Consumer GameCore 快照包含 " + generated.Count + " 个根/依赖资产。";
            result.errors.AddRange(errors);
            if (errors.Count > 0)
            {
                result.status = "validation_failed";
                return result;
            }

            if (!request.commit)
            {
                result.success = true;
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

            if (!string.Equals(GetAssetRevision(path), revision, StringComparison.Ordinal))
                return ESContentRegistrationResult.Failure(request, "concurrency_conflict", "Consumer 在预检后发生变化，拒绝写入。");

            List<ESAssetReferBase> oldAssets = consumer.GameCoreAssets != null
                ? new List<ESAssetReferBase>(consumer.GameCoreAssets)
                : null;
            List<string> oldErrors = consumer.GameCoreValidationErrors != null
                ? new List<string>(consumer.GameCoreValidationErrors)
                : null;
            string oldConsumerId = consumer.ConsumerId;
            try
            {
                Undo.RecordObject(consumer, "Synchronize Consumer GameCore Assets");
                consumer.EnsureStableIdentity();
                consumer.GameCoreAssets = generated;
                consumer.GameCoreValidationErrors = errors;
                EditorUtility.SetDirty(consumer);
                AssetDatabase.SaveAssetIfDirty(consumer);
                if (!ESGameCoreRootRegistrationAuthoring.ReferencesEqual(consumer.GameCoreAssets, generated)
                    || !StringListsEqual(consumer.GameCoreValidationErrors, errors))
                {
                    throw new InvalidOperationException("Consumer 同步写入后置条件失败。");
                }
            }
            catch (Exception exception)
            {
                consumer.GameCoreAssets = oldAssets;
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
            result.consumerRevision = GetAssetRevision(path);
            result.changedPaths.Add(path);
            return result;
        }

        private static bool StringListsEqual(IEnumerable<string> left, IEnumerable<string> right)
        {
            string[] a = (left ?? Enumerable.Empty<string>()).OrderBy(entry => entry, StringComparer.Ordinal).ToArray();
            string[] b = (right ?? Enumerable.Empty<string>()).OrderBy(entry => entry, StringComparer.Ordinal).ToArray();
            return a.SequenceEqual(b, StringComparer.Ordinal);
        }

        private static void NotifyEditorContentIndexes(
            ESContentRegistrationRequest request,
            ESContentRegistrationResult result)
        {
            if (request == null || result == null)
                return;
            if (request.action != ESContentRegistrationAction.RegisterAsset
                && request.action != ESContentRegistrationAction.UpdateAssetKey)
                return;

            string libraryPath = NormalizeAssetPath(request.libraryPath);
            if (!string.IsNullOrEmpty(libraryPath))
                ES.EditorInternal.ESAssetCatalogKeyPicker.NotifyLibraryChanged(libraryPath);
        }

        private static ESContentRegistrationResult StartBake(ESContentRegistrationRequest request)
        {
            if (!request.commit)
            {
                ESContentRegistrationResult preview = ESContentRegistrationResult.Create(request);
                preview.success = true;
                preview.status = "validated";
                preview.message = "Bake 会写入 ES/ResourcePipeline/Baked；请使用 commit=true 和 requestId 显式启动。";
                return preview;
            }

            ESEditorLongTask task = ESAssetReferenceBaker.Bake();
            if (!TryGetStoredBake(task.Id, out StoredBake run))
            {
                run = new StoredBake
                {
                    runId = task.Id,
                    editorTaskId = task.Id,
                    status = task.Status.ToString(),
                    message = "资源 Catalog/ReferenceGraph Bake 已入队；菜单调用不代表完成。"
                };
            }
            run.requestIds ??= new List<string>();
            if (!run.requestIds.Contains(request.requestId))
                run.requestIds.Add(request.requestId);
            run.status = task.Status.ToString();
            run.message = "资源 Catalog/ReferenceGraph Bake 已入队；菜单调用不代表完成。";
            StoreBake(run);

            if (BakeCompletionSubscriptions.Add(task.Id))
                task.AddFinishedCallback(CompleteBake);

            ESContentRegistrationResult result = ESContentRegistrationResult.Create(request);
            result.success = true;
            result.changed = true;
            result.dryRun = false;
            result.status = "pending";
            result.runId = task.Id;
            result.message = run.message;
            return result;
        }

        private static void CompleteBake(ESEditorLongTask task)
        {
            lock (Gate)
            {
                try
                {
                    string runId = task?.Id ?? string.Empty;
                    if (!TryGetStoredBake(runId, out StoredBake run))
                    {
                        run = new StoredBake
                        {
                            runId = runId,
                            editorTaskId = runId,
                            requestIds = new List<string>()
                        };
                    }
                    run.status = task?.Status.ToString() ?? "Failed";
                    run.message = task?.LastError?.Message ?? "Bake 已完成。";
                    StoreBake(run);

                    foreach (string requestId in run.requestIds ?? new List<string>())
                    {
                        if (string.IsNullOrEmpty(requestId)
                            || !TryGetStoredRequest(requestId, out StoredRequest stored))
                        {
                            continue;
                        }

                        ESContentRegistrationResult final = Clone(stored.result);
                        final.status = task != null && task.Status == ESEditorLongTaskStatus.Succeeded
                            ? "succeeded"
                            : task != null && task.Status == ESEditorLongTaskStatus.Cancelled
                                ? "cancelled"
                                : "failed";
                        final.success = string.Equals(final.status, "succeeded", StringComparison.Ordinal);
                        final.message = run.message;
                        if (!final.success && !string.IsNullOrEmpty(run.message))
                            final.errors.Add(run.message);
                        StoreRequest(requestId, stored.fingerprint, final);
                    }
                    if (task != null && task.Status == ESEditorLongTaskStatus.Succeeded)
                        ES.EditorInternal.ESAssetCatalogKeyPicker.NotifyCatalogsChanged();
                }
                finally
                {
                    if (task != null)
                        BakeCompletionSubscriptions.Remove(task.Id);
                    ReleaseHeldBakeProcessGate(task?.Id);
                }
            }
        }

        private static ESContentRegistrationResult GetStatus(ESContentRegistrationRequest request)
        {
            if (!string.IsNullOrEmpty(request.runId) && TryGetStoredBake(request.runId, out StoredBake run))
            {
                if (!string.IsNullOrEmpty(request.requestId)
                    && !(run.requestIds ?? new List<string>()).Contains(request.requestId))
                {
                    return ESContentRegistrationResult.Failure(
                        request,
                        "run_request_mismatch",
                        "requestId 不属于指定 Bake runId；拒绝返回其他请求的运行状态。");
                }

                var result = ESContentRegistrationResult.Create(request);
                result.runId = run.runId;
                result.requestId = !string.IsNullOrEmpty(request.requestId)
                    ? request.requestId
                    : run.requestIds?.FirstOrDefault() ?? string.Empty;
                result.status = run.status;
                result.message = run.message;
                result.success = string.Equals(run.status, ESEditorLongTaskStatus.Succeeded.ToString(), StringComparison.Ordinal)
                                 || string.Equals(run.status, "succeeded", StringComparison.Ordinal);

                ESEditorLongTask active = null;
                bool hasActiveTask = ESEditorHandle.TryGetLongTask(run.editorTaskId, out active);
                if (!result.success
                    && (string.Equals(run.status, "Queued", StringComparison.Ordinal)
                        || string.Equals(run.status, "Running", StringComparison.Ordinal))
                    && !hasActiveTask)
                {
                    result.status = "interrupted";
                    result.message = "Bake 的 Editor 长任务已丢失，通常由 Domain Reload 或 Editor 重启导致；不能报告为完成。";
                    run.status = result.status;
                    run.message = result.message;
                    StoreBake(run);
                }
                else if (active != null)
                {
                    result.status = active.Status.ToString();
                    result.message = active.Progress.Message;
                }
                return result;
            }

            if (!string.IsNullOrEmpty(request.requestId) && TryGetStoredRequest(request.requestId, out StoredRequest stored))
            {
                ESContentRegistrationResult replay = Clone(stored.result);
                replay.idempotent = true;
                return replay;
            }

            return ESContentRegistrationResult.Failure(request, "not_found", "找不到对应的 runId 或 requestId。" );
        }

        private static bool TryValidateEditorState(out string error)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                error = "PlayMode 或 PlayMode 切换期间禁止修改注册源资产。";
                return false;
            }
            if (EditorApplication.isCompiling)
            {
                error = "Unity 正在编译脚本，拒绝注册写入。";
                return false;
            }
            if (EditorApplication.isUpdating || AssetDatabase.IsAssetImportWorkerProcess())
            {
                error = "AssetDatabase 正在刷新/导入，拒绝注册写入。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryEnterProcessGate(out Mutex mutex, out bool owns, out string error)
        {
            mutex = null;
            owns = false;
            try
            {
                string projectIdentity = ComputeSha256(Path.GetFullPath(Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath));
                mutex = new Mutex(false, "Local\\ES.ContentRegistration." + projectIdentity.Substring(0, 24));
                try { owns = mutex.WaitOne(0); }
                catch (AbandonedMutexException) { owns = true; }
                error = owns ? string.Empty : "另一 Unity 进程正在修改本项目的 ES 内容注册目标。";
                return owns;
            }
            catch (Exception exception)
            {
                mutex?.Dispose();
                mutex = null;
                error = "无法建立跨进程注册门禁：" + exception.Message;
                return false;
            }
        }

        private static bool TryGetHeldBakeTask(out ESEditorLongTask task)
        {
            task = null;
            if (heldBakeProcessGate == null || string.IsNullOrEmpty(heldBakeTaskId))
                return false;

            if (ESEditorHandle.TryGetLongTask(heldBakeTaskId, out task) && task != null && !task.IsFinished)
                return true;

            ReleaseHeldBakeProcessGate();
            task = null;
            return false;
        }

        private static void HoldBakeProcessGate(Mutex processGate, string taskId)
        {
            if (processGate == null || string.IsNullOrEmpty(taskId))
                throw new InvalidOperationException("无法将跨进程门禁移交给无身份的 Bake 任务。");
            if (heldBakeProcessGate != null)
                throw new InvalidOperationException("本进程已有 Bake 生命周期门禁。");

            heldBakeProcessGate = processGate;
            heldBakeTaskId = taskId;
            if (!bakeLifecycleSubscribed)
            {
                AssemblyReloadEvents.beforeAssemblyReload += ReleaseHeldBakeProcessGateForLifecycle;
                EditorApplication.quitting += ReleaseHeldBakeProcessGateForLifecycle;
                bakeLifecycleSubscribed = true;
            }
        }

        private static void ReleaseHeldBakeProcessGate(string expectedTaskId = null)
        {
            if (heldBakeProcessGate == null
                || (!string.IsNullOrEmpty(expectedTaskId)
                    && !string.Equals(expectedTaskId, heldBakeTaskId, StringComparison.Ordinal)))
            {
                return;
            }

            Mutex gate = heldBakeProcessGate;
            heldBakeProcessGate = null;
            heldBakeTaskId = string.Empty;
            if (bakeLifecycleSubscribed)
            {
                AssemblyReloadEvents.beforeAssemblyReload -= ReleaseHeldBakeProcessGateForLifecycle;
                EditorApplication.quitting -= ReleaseHeldBakeProcessGateForLifecycle;
                bakeLifecycleSubscribed = false;
            }

            try
            {
                gate.ReleaseMutex();
            }
            catch (ApplicationException exception)
            {
                Debug.LogError("[ES Content Registration] 释放 Bake 跨进程门禁失败：" + exception.Message);
            }
            finally
            {
                gate.Dispose();
            }
        }

        private static void ReleaseHeldBakeProcessGateForLifecycle()
            => ReleaseHeldBakeProcessGate();

        private static string ComputeFingerprint(ESContentRegistrationRequest request)
            => ComputeSha256(JsonUtility.ToJson(request));

        private static string CreatePreflightRequestId()
        {
            string requestId;
            do
            {
                requestId = "preflight-" + Guid.NewGuid().ToString("N");
            }
            while (AcceptedPreflights.ContainsKey(requestId) || TryGetStoredRequest(requestId, out _));
            return requestId;
        }

        private static string ComputeSha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        private static string ComputePreflightFingerprint(ESContentRegistrationRequest request)
        {
            var semantic = JsonUtility.FromJson<ESContentRegistrationRequest>(JsonUtility.ToJson(request));
            semantic.commit = false;
            semantic.requestId = string.Empty;
            semantic.runId = string.Empty;
            semantic.expectedGuid = string.Empty;
            semantic.expectedLibraryRevision = string.Empty;
            semantic.expectedSourceGuid = string.Empty;
            semantic.expectedGroupGuid = string.Empty;
            semantic.expectedConsumerGuid = string.Empty;
            semantic.expectedSourceRevision = string.Empty;
            semantic.expectedGroupRevision = string.Empty;
            semantic.expectedConsumerRevision = string.Empty;
            semantic.expectedCurrentEnumKey = 0;
            semantic.expectedCurrentStringKey = string.Empty;
            semantic.hasExpectedCurrentKey = false;
            return ComputeSha256(JsonUtility.ToJson(semantic));
        }

        private static string ComputeFileSha256(string path)
        {
            using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        private static ESContentRegistrationResult Clone(ESContentRegistrationResult result)
            => result == null ? null : JsonUtility.FromJson<ESContentRegistrationResult>(JsonUtility.ToJson(result));

        private static bool TryGetStoredRequest(string requestId, out StoredRequest stored)
        {
            string json = SessionState.GetString(RequestStatePrefix + requestId, string.Empty);
            stored = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<StoredRequest>(json);
            return stored?.result != null;
        }

        private static void StoreRequest(string requestId, string fingerprint, ESContentRegistrationResult result)
        {
            var stored = new StoredRequest { fingerprint = fingerprint, result = Clone(result) };
            SessionState.SetString(RequestStatePrefix + requestId, JsonUtility.ToJson(stored));
        }

        private static bool TryGetStoredBake(string runId, out StoredBake stored)
        {
            string json = SessionState.GetString(BakeStatePrefix + runId, string.Empty);
            stored = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<StoredBake>(json);
            return stored != null;
        }

        private static void StoreBake(StoredBake run)
        {
            if (run != null && !string.IsNullOrEmpty(run.runId))
                SessionState.SetString(BakeStatePrefix + run.runId, JsonUtility.ToJson(run));
        }
    }
}
