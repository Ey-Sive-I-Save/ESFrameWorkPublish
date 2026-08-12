using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace ES
{
    internal enum ESEditorCatalogRecoveryAction
    {
        ContinueDegraded,
        RetryAfterBake,
        StopForConfiguration
    }

    internal enum ESEditorCatalogRecoveryState
    {
        None,
        BakeRequested,
        Baking,
        AwaitingValidation,
        AwaitingPlayModeReentry,
        ReentryInProgress,
        Failed
    }

    internal sealed class ESEditorCatalogRecoveryReport
    {
        internal readonly List<ESRuntimeCatalog> catalogs = new List<ESRuntimeCatalog>();
        internal readonly List<string> failures = new List<string>();
        internal readonly List<string> blockingFailures = new List<string>();
        internal int discoveredFileCount;
        internal int expectedBusinessEntryCount;
        internal int injectedBusinessEntryCount;
        internal string sourceTransactionId = string.Empty;
        internal long sourceCommitGeneration;
        internal string catalogSetFingerprint = string.Empty;

        internal bool HasFailures => failures.Count > 0;
        internal bool HasBlockingFailures => blockingFailures.Count > 0;
        internal bool CanContinueDegraded => HasFailures && !HasBlockingFailures;

        internal void AddFailure(string message)
        {
            AddFailure(message, true);
        }

        internal void AddDegradableFailure(string message)
        {
            AddFailure(message, false);
        }

        private void AddFailure(string message, bool blocking)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;
            if (!failures.Contains(message))
                failures.Add(message);
            if (blocking && !blockingFailures.Contains(message))
                blockingFailures.Add(message);
        }

        internal string BuildMessage()
        {
            string summary = discoveredFileCount == 0
                ? "首次 EditorDirect 会话未发现可用的 Editor Catalog。"
                : "EditorDirect 会话发现 Catalog 不完整或无法注入。";
            if (failures.Count == 0)
            {
                string count = expectedBusinessEntryCount > 0
                    ? "\n候选业务资源：" + expectedBusinessEntryCount + "，已提交：" + injectedBusinessEntryCount + "。"
                    : string.Empty;
                return summary + count;
            }
            string omitted = failures.Count > 8 ? "\n另有 " + (failures.Count - 8) + " 项，请查看 Console。" : string.Empty;
            return summary + "\n\n" + string.Join("\n", failures.Take(8)) + omitted;
        }
    }

    internal static class ESEditorResourceSessionPrompt
    {
        private const string CatalogRecoveryPromptShownKey = "ES.EditorDirect.CatalogRecoveryPromptShown";
        private const string CatalogRecoveryBakeRequestedKey = "ES.EditorDirect.CatalogRecoveryBakeRequested";
        private const string CatalogRecoveryOpenConfigRequestedKey = "ES.EditorDirect.CatalogRecoveryOpenConfigRequested";
        private const string CatalogRecoverySettingsPathKey = "ES.EditorDirect.CatalogRecoverySettingsPath";
        private const string CatalogRecoveryStateKey = "ES.EditorDirect.CatalogRecoveryState";
        private const string CatalogRecoveryTransactionIdKey = "ES.EditorDirect.CatalogRecoveryTransactionId";
        private const string CatalogRecoveryOutputRootKey = "ES.EditorDirect.CatalogRecoveryOutputRoot";
        private const string CatalogRecoveryCatalogGenerationKey = "ES.EditorDirect.CatalogRecoveryCatalogGeneration";
        private const string CatalogRecoveryCommitGenerationKey = "ES.EditorDirect.CatalogRecoveryCommitGeneration";
        private const string CatalogRecoveryCatalogFingerprintKey = "ES.EditorDirect.CatalogRecoveryCatalogFingerprint";
        private const string CatalogRecoveryConfigTableGenerationKey = "ES.EditorDirect.CatalogRecoveryConfigTableGeneration";
        private const string CatalogRecoveryDomainEpochKey = "ES.EditorDirect.CatalogRecoveryDomainEpoch";
        private const string CatalogRecoveryFailureMessageKey = "ES.EditorDirect.CatalogRecoveryFailureMessage";
        private const string CatalogRecoveryFailureDialogShownKey = "ES.EditorDirect.CatalogRecoveryFailureDialogShown";
        private const string ResourcePipelineTaskKey = "ES.ResourcePipeline";
        private static bool handledThisPlaySession;
        private static bool missingConfigPromptShown;
        private static bool recoveryBakeStarted;
        private static bool recoveryValidationStarted;
        private static bool recoverySessionStartScheduled;
        private static bool recoveryFailureDialogScheduled;
        private static readonly string CurrentDomainEpoch = Guid.NewGuid().ToString("N");

        internal static void Register()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            ESConfigKeyDiagnostics.MissingKey -= OnMissingConfigKey;
            ESConfigKeyDiagnostics.MissingKey += OnMissingConfigKey;
            ESEditorResourceSessionBootstrap.InitializationCompleted -= OnSessionInitializationCompleted;
            ESEditorResourceSessionBootstrap.InitializationCompleted += OnSessionInitializationCompleted;
            AssemblyReloadEvents.beforeAssemblyReload -= ESEditorResourceSessionBootstrap.DisposeCurrent;
            AssemblyReloadEvents.beforeAssemblyReload += ESEditorResourceSessionBootstrap.DisposeCurrent;
            ScheduleRecoveryContinuationAfterReload();
        }

        private static ESEditorCatalogRecoveryState GetRecoveryState()
        {
            int value = SessionState.GetInt(CatalogRecoveryStateKey, (int)ESEditorCatalogRecoveryState.None);
            if (Enum.IsDefined(typeof(ESEditorCatalogRecoveryState), value))
                return (ESEditorCatalogRecoveryState)value;
            return SessionState.GetBool(CatalogRecoveryBakeRequestedKey, false)
                ? ESEditorCatalogRecoveryState.BakeRequested
                : ESEditorCatalogRecoveryState.None;
        }

        private static void SetRecoveryState(ESEditorCatalogRecoveryState state)
        {
            SessionState.SetInt(CatalogRecoveryStateKey, (int)state);
            SessionState.SetBool(CatalogRecoveryBakeRequestedKey,
                state == ESEditorCatalogRecoveryState.BakeRequested
                || state == ESEditorCatalogRecoveryState.Baking);
        }

        private static bool IsRecoveryReentryPending()
        {
            ESEditorCatalogRecoveryState state = GetRecoveryState();
            return state == ESEditorCatalogRecoveryState.AwaitingPlayModeReentry
                || state == ESEditorCatalogRecoveryState.ReentryInProgress;
        }

        internal static bool IsRecoveryReentryActive
            => GetRecoveryState() == ESEditorCatalogRecoveryState.ReentryInProgress;

        private static void ClearRecoveryTransaction()
        {
            SetRecoveryState(ESEditorCatalogRecoveryState.None);
            SessionState.SetBool(CatalogRecoveryBakeRequestedKey, false);
            SessionState.SetBool(CatalogRecoveryOpenConfigRequestedKey, false);
            SessionState.SetBool(CatalogRecoveryFailureDialogShownKey, false);
            SessionState.SetString(CatalogRecoveryFailureMessageKey, string.Empty);
            SessionState.SetString(CatalogRecoverySettingsPathKey, string.Empty);
            SessionState.SetString(CatalogRecoveryTransactionIdKey, string.Empty);
            SessionState.SetString(CatalogRecoveryOutputRootKey, string.Empty);
            SessionState.SetString(CatalogRecoveryCatalogGenerationKey, string.Empty);
            SessionState.SetString(CatalogRecoveryCommitGenerationKey, string.Empty);
            SessionState.SetString(CatalogRecoveryCatalogFingerprintKey, string.Empty);
            SessionState.SetString(CatalogRecoveryConfigTableGenerationKey, string.Empty);
            SessionState.SetString(CatalogRecoveryDomainEpochKey, string.Empty);
        }

        private static string RecoveryTransactionId
            => SessionState.GetString(CatalogRecoveryTransactionIdKey, string.Empty);

        private static string RecoveryOutputRoot
            => SessionState.GetString(CatalogRecoveryOutputRootKey, string.Empty);

        private static void Schedule(EditorApplication.CallbackFunction callback)
        {
            if (callback == null)
                return;
            EditorApplication.delayCall -= callback;
            EditorApplication.delayCall += callback;
        }

        private static bool HasCommittedRecoveryOutput()
        {
            string transactionId = RecoveryTransactionId;
            string outputRoot = RecoveryOutputRoot;
            if (string.IsNullOrWhiteSpace(transactionId) || string.IsNullOrWhiteSpace(outputRoot))
                return false;
            try
            {
                return File.Exists(ESAssetPipelineIO.RecoveryBakeCommitPath(transactionId));
            }
            catch
            {
                return false;
            }
        }

        private static void ScheduleRecoveryContinuationAfterReload()
        {
            if (SessionState.GetBool(CatalogRecoveryOpenConfigRequestedKey, false))
            {
                Schedule(OpenRequestedCatalogConfiguration);
                return;
            }

            switch (GetRecoveryState())
            {
                case ESEditorCatalogRecoveryState.BakeRequested:
                    Schedule(StartRequestedCatalogBake);
                    break;
                case ESEditorCatalogRecoveryState.Baking:
                    // A domain reload destroys the old long-task instance. A committed
                    // marker proves this transaction finished; otherwise re-queue the
                    // same transaction instead of scanning mutable legacy output.
                    if (HasCommittedRecoveryOutput())
                    {
                        SetRecoveryState(ESEditorCatalogRecoveryState.AwaitingValidation);
                        Schedule(ValidateRequestedCatalogBake);
                    }
                    else
                    {
                        SetRecoveryState(ESEditorCatalogRecoveryState.BakeRequested);
                        Schedule(StartRequestedCatalogBake);
                    }
                    break;
                case ESEditorCatalogRecoveryState.AwaitingValidation:
                    Schedule(ValidateRequestedCatalogBake);
                    break;
                case ESEditorCatalogRecoveryState.AwaitingPlayModeReentry:
                    Schedule(EnterRecoveryPlayMode);
                    break;
                case ESEditorCatalogRecoveryState.ReentryInProgress:
                    if (EditorApplication.isPlaying)
                        Schedule(StartRecoverySession);
                    else if (!EditorApplication.isPlayingOrWillChangePlaymode)
                        MarkRecoveryFailed("EditorDirect 恢复会话在 Domain Reload 后未能进入 PlayMode；已停止自动重试。", false);
                    break;
            }
        }

        internal static ESEditorCatalogRecoveryAction HandleCatalogRecovery(
            ESGlobalResSetting settings, ESEditorCatalogRecoveryReport report)
        {
            if (report == null || !report.HasFailures)
                return ESEditorCatalogRecoveryAction.ContinueDegraded;
            if (Application.isBatchMode)
                throw new InvalidOperationException("[ESRes][EditorSession] " + report.BuildMessage()
                    + " 批处理模式禁止在 PlayMode 内请求编辑器烘焙。");
            if (SessionState.GetBool(CatalogRecoveryPromptShownKey, false))
            {
                if (report.HasBlockingFailures)
                {
                    Debug.LogError("[ESRes][CatalogRecovery] 本次 PlayMode 已显示过恢复提示，但随后发现 Catalog 完整性故障；禁止降级并停止本次运行。\n"
                        + report.BuildMessage());
                    StopForCatalogIntegrityFailure();
                    return ESEditorCatalogRecoveryAction.StopForConfiguration;
                }
                Debug.LogWarning("[ESRes][CatalogRecovery] 本次 PlayMode 已询问过恢复操作；忽略重复缺失并继续使用降级模式。\n"
                    + report.BuildMessage());
                return ESEditorCatalogRecoveryAction.ContinueDegraded;
            }

            SessionState.SetBool(CatalogRecoveryPromptShownKey, true);
            if (report.HasBlockingFailures)
                return HandleBlockingCatalogFailure(settings, report);

            int choice = EditorUtility.DisplayDialogComplex(
                "ES EditorDirect Catalog 恢复",
                report.BuildMessage()
                    + "\n\n继续运行不会自动填充 ConfigKey/ConfigData；对应功能将保持未配置。"
                    + "\n烘焙必须退出 PlayMode，在 EditMode 完成后再重新进入 PlayMode。",
                "烘焙并重试",
                "继续运行",
                "打开资源配置");
            if (choice == 0)
            {
                RequestCatalogBakeAndRestart(settings);
                return ESEditorCatalogRecoveryAction.RetryAfterBake;
            }
            if (choice == 2)
            {
                RequestCatalogConfigurationOpen(settings);
                return ESEditorCatalogRecoveryAction.StopForConfiguration;
            }

            Debug.LogWarning("[ESRes][CatalogRecovery] 用户选择继续使用降级模式；ConfigKey/ConfigData 不可用。\n"
                + report.BuildMessage());
            return ESEditorCatalogRecoveryAction.ContinueDegraded;
        }

        private static ESEditorCatalogRecoveryAction HandleBlockingCatalogFailure(
            ESGlobalResSetting settings,
            ESEditorCatalogRecoveryReport report)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "ES EditorDirect Catalog 完整性失败",
                report.BuildMessage()
                    + "\n\n该问题可能导致稳定 Key 指向错误资产，禁止以降级模式继续。",
                "烘焙并重试",
                "停止本次运行",
                "打开资源配置");
            if (choice == 0)
            {
                RequestCatalogBakeAndRestart(settings);
                return ESEditorCatalogRecoveryAction.RetryAfterBake;
            }
            if (choice == 2)
            {
                RequestCatalogConfigurationOpen(settings);
                return ESEditorCatalogRecoveryAction.StopForConfiguration;
            }

            Debug.LogError("[ESRes][CatalogRecovery] Catalog 完整性失败，已停止本次 PlayMode。\n"
                + report.BuildMessage());
            StopForCatalogIntegrityFailure();
            return ESEditorCatalogRecoveryAction.StopForConfiguration;
        }

        private static void StopForCatalogIntegrityFailure()
        {
            ESEditorResourceSessionBootstrap.DisposeCurrent();
            Schedule(ExitPlayModeForCatalogRecovery);
        }

        private static void RequestCatalogBakeAndRestart(ESGlobalResSetting settings)
        {
            ESEditorCatalogRecoveryState currentState = GetRecoveryState();
            if (currentState == ESEditorCatalogRecoveryState.BakeRequested
                || currentState == ESEditorCatalogRecoveryState.Baking
                || currentState == ESEditorCatalogRecoveryState.AwaitingValidation
                || currentState == ESEditorCatalogRecoveryState.AwaitingPlayModeReentry
                || currentState == ESEditorCatalogRecoveryState.ReentryInProgress)
            {
                Debug.LogWarning("[ESRes][CatalogRecovery] 已有一次恢复事务正在处理，忽略重复的烘焙请求。State=" + currentState);
                return;
            }

            ReportRecoveryTransactionStoragePressure();
            SetRecoveryState(ESEditorCatalogRecoveryState.BakeRequested);
            SessionState.SetBool(CatalogRecoveryBakeRequestedKey, true);
            SessionState.SetBool(CatalogRecoveryOpenConfigRequestedKey, false);
            SessionState.SetBool(CatalogRecoveryFailureDialogShownKey, false);
            SessionState.SetString(CatalogRecoveryFailureMessageKey, string.Empty);
            SessionState.SetString(CatalogRecoveryCatalogGenerationKey, string.Empty);
            SessionState.SetString(CatalogRecoveryCommitGenerationKey, string.Empty);
            SessionState.SetString(CatalogRecoveryCatalogFingerprintKey, string.Empty);
            SessionState.SetString(CatalogRecoveryConfigTableGenerationKey, string.Empty);
            SessionState.SetString(CatalogRecoveryDomainEpochKey, string.Empty);
            string transactionId = Guid.NewGuid().ToString("N");
            SessionState.SetString(CatalogRecoveryTransactionIdKey, transactionId);
            SessionState.SetString(CatalogRecoveryOutputRootKey,
                ESAssetPipelineIO.RecoveryBakeRoot(transactionId));
            SessionState.SetString(CatalogRecoverySettingsPathKey,
                settings == null ? string.Empty : AssetDatabase.GetAssetPath(settings));
            ESEditorResourceSessionBootstrap.DisposeCurrent();
            Schedule(ExitPlayModeForCatalogRecovery);
        }

        private static void ReportRecoveryTransactionStoragePressure()
        {
            string recoveryRoot = Path.Combine(ESAssetPipelineIO.BakeRoot, ".Recovery");
            if (!Directory.Exists(recoveryRoot))
                return;

            try
            {
                ESManagedFileIO.EnsureNoNestedReparsePoints(recoveryRoot);
                int transactionCount = Directory.EnumerateDirectories(
                    recoveryRoot,
                    "*",
                    SearchOption.TopDirectoryOnly).Count();
                long totalBytes = ESManagedFileIO.EnumerateFilesSafely(recoveryRoot)
                    .Select(path => new FileInfo(path).Length)
                    .Sum();
                if (transactionCount >= 16 || totalBytes >= 512L * 1024L * 1024L)
                {
                    Debug.LogWarning("[ESRes][CatalogRecovery] 已保留 " + transactionCount
                        + " 个恢复事务，诊断产物约 " + (totalBytes / (1024d * 1024d)).ToString("F1")
                        + " MiB。恢复流程不会自动删除成功、失败、当前活跃或孤儿事务；"
                        + "需要独立的显式确认清理流程后再移除。");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("[ESRes][CatalogRecovery] 无法只读检查恢复事务目录：" + exception.Message);
            }
        }

        private static void RequestCatalogConfigurationOpen(ESGlobalResSetting settings)
        {
            SessionState.SetBool(CatalogRecoveryBakeRequestedKey, false);
            SessionState.SetBool(CatalogRecoveryOpenConfigRequestedKey, true);
            SessionState.SetString(CatalogRecoveryTransactionIdKey, string.Empty);
            SessionState.SetString(CatalogRecoveryOutputRootKey, string.Empty);
            SessionState.SetString(CatalogRecoveryCatalogGenerationKey, string.Empty);
            SessionState.SetString(CatalogRecoveryCommitGenerationKey, string.Empty);
            SessionState.SetString(CatalogRecoveryCatalogFingerprintKey, string.Empty);
            SessionState.SetString(CatalogRecoveryConfigTableGenerationKey, string.Empty);
            SessionState.SetString(CatalogRecoveryDomainEpochKey, string.Empty);
            SetRecoveryState(ESEditorCatalogRecoveryState.None);
            SessionState.SetString(CatalogRecoverySettingsPathKey,
                settings == null ? string.Empty : AssetDatabase.GetAssetPath(settings));
            ESEditorResourceSessionBootstrap.DisposeCurrent();
            Schedule(ExitPlayModeForCatalogRecovery);
        }

        private static void ExitPlayModeForCatalogRecovery()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.isPlaying = false;
        }

        private static void StartRequestedCatalogBake()
        {
            if (recoveryBakeStarted || GetRecoveryState() != ESEditorCatalogRecoveryState.BakeRequested)
                return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isBatchMode)
                return;

            if (ESEditorHandle.IsSimpleTaskKeyActive(ResourcePipelineTaskKey)
                || ESEditorHandle.IsLongTaskKeyActive(ResourcePipelineTaskKey))
            {
                FinishRequestedCatalogBake(null,
                    new InvalidOperationException("已有资源管线任务正在运行，Catalog 恢复烘焙未启动。请等待当前任务结束后重试。"));
                return;
            }

            recoveryBakeStarted = true;
            SetRecoveryState(ESEditorCatalogRecoveryState.Baking);
            string settingsPath = SessionState.GetString(CatalogRecoverySettingsPathKey, string.Empty);
            ESGlobalResSetting settings = !string.IsNullOrWhiteSpace(settingsPath)
                ? AssetDatabase.LoadAssetAtPath<ESGlobalResSetting>(settingsPath)
                : ESGlobalResSetting.Instance;
            if (settings == null)
            {
                FinishRequestedCatalogBake(null, new InvalidOperationException("未找到 ESGlobalResSetting，无法执行 Catalog 烘焙。"));
                return;
            }

            try
            {
                string transactionId = RecoveryTransactionId;
                if (string.IsNullOrWhiteSpace(transactionId))
                {
                    FinishRequestedCatalogBake(null,
                        new InvalidOperationException("Catalog 恢复事务缺少 transaction ID。"));
                    return;
                }

                ESAssetReferenceBaker.BakeForCatalogRecovery(
                    transactionId,
                    task => FinishRequestedCatalogBake(task));
            }
            catch (Exception exception)
            {
                FinishRequestedCatalogBake(null, exception);
            }
        }

        private static void FinishRequestedCatalogBake(ESEditorLongTask task, Exception immediateError = null)
        {
            recoveryBakeStarted = false;
            ESEditorLongTaskStatus status = task?.Status ?? ESEditorLongTaskStatus.Failed;
            Exception error = immediateError ?? task?.LastError;
            if (status != ESEditorLongTaskStatus.Succeeded || error != null)
            {
                string stateLabel = status == ESEditorLongTaskStatus.Cancelled ? "已取消" : "失败";
                string message = error == null ? "Catalog 烘焙" + stateLabel + "，状态=" + status : error.Message;
                MarkRecoveryFailed("Catalog 烘焙" + stateLabel + "：" + message, true);
                return;
            }

            // Task completion is not Catalog readiness. Validation must happen in EditMode
            // after the Bake output has been refreshed and injected successfully.
            SetRecoveryState(ESEditorCatalogRecoveryState.AwaitingValidation);
            Schedule(ValidateRequestedCatalogBake);
        }

        private static void ValidateRequestedCatalogBake()
        {
            if (recoveryValidationStarted
                || GetRecoveryState() != ESEditorCatalogRecoveryState.AwaitingValidation
                || EditorApplication.isPlayingOrWillChangePlaymode
                || Application.isBatchMode)
                return;

            recoveryValidationStarted = true;
            try
            {
                AssetDatabase.Refresh();
                string transactionId = RecoveryTransactionId;
                string outputRoot = RecoveryOutputRoot;
                ESEditorCatalogRecoveryReport report =
                    ESEditorResourceSessionBootstrap.DiscoverEditorRuntimeCatalogs(outputRoot, transactionId);
                if (report.HasFailures)
                {
                    MarkRecoveryFailed("Bake 已完成，但 EditMode Catalog 复核失败：\n" + report.BuildMessage(), true);
                    return;
                }

                if (!ESEditorResourceSessionBootstrap.TryBuildEditorCatalogTables(report, out string error))
                {
                    MarkRecoveryFailed("Bake 已完成，但 ConfigKey/ConfigData 复核失败：\n" + error, true);
                    return;
                }

                long configTableGeneration = ESRuntimeDataAsset.AssetConfigTableGeneration;
                if (report.sourceCommitGeneration <= 0
                    || string.IsNullOrWhiteSpace(report.catalogSetFingerprint)
                    || !ESRuntimeDataAsset.IsCurrentEditorCatalogCommit(
                        report.catalogSetFingerprint,
                        configTableGeneration))
                {
                    MarkRecoveryFailed(
                        "Bake 已完成，但 Catalog 指纹与正式 ConfigKey/ConfigData 提交状态不一致。",
                        true);
                    return;
                }

                if (!ESEditorResourceSessionBootstrap.TryVerifyCommittedCatalogArtifacts(
                        outputRoot,
                        transactionId,
                        report.sourceCommitGeneration,
                        report.catalogSetFingerprint,
                        out string artifactError))
                {
                    MarkRecoveryFailed(
                        "Bake 已完成，但 ConfigKey/ConfigData 提交后的事务产物复核失败：\n" + artifactError,
                        true);
                    return;
                }

                SessionState.SetString(
                    CatalogRecoveryCatalogGenerationKey,
                    ESRuntimeDataAsset.EditorCatalogCommitGeneration.ToString());
                SessionState.SetString(
                    CatalogRecoveryCommitGenerationKey,
                    report.sourceCommitGeneration.ToString());
                SessionState.SetString(
                    CatalogRecoveryCatalogFingerprintKey,
                    report.catalogSetFingerprint);
                SessionState.SetString(
                    CatalogRecoveryConfigTableGenerationKey,
                    configTableGeneration.ToString());
                SessionState.SetString(CatalogRecoveryDomainEpochKey, CurrentDomainEpoch);
                // EditMode 复核只产生重入证据，不拥有恢复 Provider。保留稳定外壳与
                // generation，但在新会话按同一 transaction 重新提交前拒绝业务查询。
                ESRuntimeDataAsset.InvalidateAssetConfigTableBinding();
                SetRecoveryState(ESEditorCatalogRecoveryState.AwaitingPlayModeReentry);
                Debug.Log("[ESRes][CatalogRecovery] Bake 与 EditMode Catalog 复核通过，准备执行一次 EditorDirect 会话重建。");
                Schedule(EnterRecoveryPlayMode);
            }
            catch (Exception exception)
            {
                MarkRecoveryFailed("Bake 后 Catalog 复核异常：" + exception.Message, true);
            }
            finally
            {
                recoveryValidationStarted = false;
            }
        }

        private static void EnterRecoveryPlayMode()
        {
            if (GetRecoveryState() != ESEditorCatalogRecoveryState.AwaitingPlayModeReentry)
                return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isBatchMode)
                return;
            SetRecoveryState(ESEditorCatalogRecoveryState.ReentryInProgress);
            EditorApplication.isPlaying = true;
        }

        private static void StartRecoverySession()
        {
            if (!EditorApplication.isPlaying || !IsRecoveryReentryPending() || recoverySessionStartScheduled)
                return;
            recoverySessionStartScheduled = true;
            try
            {
                string settingsPath = SessionState.GetString(CatalogRecoverySettingsPathKey, string.Empty);
                ESGlobalResSetting settings = !string.IsNullOrWhiteSpace(settingsPath)
                    ? AssetDatabase.LoadAssetAtPath<ESGlobalResSetting>(settingsPath)
                    : ESGlobalResSetting.Instance;
                if (settings == null)
                {
                    MarkRecoveryFailed("EditorDirect 恢复重建找不到 ESGlobalResSetting。", true);
                    return;
                }

                if (ESAssets.IsReady)
                {
                    MarkRecoveryFailed(
                        "EditorDirect 恢复重建发现外部资源会话已经 Ready；不能把旧会话当成本次恢复事务的成功证据。",
                        true);
                    return;
                }

                if (ESResManager.Instance != null)
                {
                    MarkRecoveryFailed("EditorDirect 恢复重建发现已有 ESResManager，但资源 Provider 尚未 Ready；已停止自动重试。", true);
                    return;
                }

                string transactionId = RecoveryTransactionId;
                if (!ESEditorResourceSessionBootstrap.CreateForRecovery(settings, transactionId))
                    MarkRecoveryFailed("EditorDirect 恢复重建未能创建临时资源会话；已停止自动重试。", true);
            }
            finally
            {
                recoverySessionStartScheduled = false;
            }
        }

        private static void OnSessionInitializationCompleted(
            ESEditorResourceSessionBootstrap session, bool succeeded, string error)
        {
            if (GetRecoveryState() != ESEditorCatalogRecoveryState.ReentryInProgress)
                return;
            string transactionId = RecoveryTransactionId;
            string expectedFingerprint = SessionState.GetString(
                CatalogRecoveryCatalogFingerprintKey,
                string.Empty);
            string expectedDomainEpoch = SessionState.GetString(
                CatalogRecoveryDomainEpochKey,
                string.Empty);
            bool crossedDomainReload = !string.Equals(
                expectedDomainEpoch,
                CurrentDomainEpoch,
                StringComparison.Ordinal);
            long expectedCatalogGeneration = 0;
            long expectedCommitGeneration = 0;
            long expectedConfigTableGeneration = 0;
            bool recoveryEvidenceParsed = long.TryParse(
                    SessionState.GetString(CatalogRecoveryCatalogGenerationKey, string.Empty),
                    out expectedCatalogGeneration)
                && long.TryParse(
                    SessionState.GetString(CatalogRecoveryCommitGenerationKey, string.Empty),
                    out expectedCommitGeneration)
                && long.TryParse(
                    SessionState.GetString(CatalogRecoveryConfigTableGenerationKey, string.Empty),
                    out expectedConfigTableGeneration)
                && expectedCommitGeneration > 0
                && expectedConfigTableGeneration > 0
                && !string.IsNullOrWhiteSpace(expectedDomainEpoch)
                && !string.IsNullOrWhiteSpace(expectedFingerprint);
            bool sessionEvidenceMatched = recoveryEvidenceParsed
                && session != null
                // EditMode validation commits one generation; the reentered session
                // must commit the same transaction's Catalog again and therefore move
                // both process-local generations forward.
                && (crossedDomainReload
                    ? session.EditorCatalogCommitGeneration > 0
                        && session.EditorConfigTableGeneration > 0
                    : session.EditorCatalogCommitGeneration > expectedCatalogGeneration
                        && session.EditorConfigTableGeneration > expectedConfigTableGeneration)
                && session.RecoveryCommitGeneration == expectedCommitGeneration
                && string.Equals(
                    session.EditorCatalogFingerprint,
                    expectedFingerprint,
                    StringComparison.OrdinalIgnoreCase)
                && session.EditorConfigTableGeneration == ESRuntimeDataAsset.AssetConfigTableGeneration
                && ESRuntimeDataAsset.IsCurrentEditorCatalogCommit(
                    session.EditorCatalogFingerprint,
                    session.EditorConfigTableGeneration);
            if (session == null
                || !session.IsOwnedByRecoveryTransaction(transactionId)
                || !ReferenceEquals(session, ESEditorResourceSessionBootstrap.Current)
                || session.OwnedProviderGeneration != ESAssets.RuntimeBackendGeneration
                || !session.EditorCatalogValidated
                || session.EditorDirectCatalogDegraded
                || !sessionEvidenceMatched
                || !ESAssets.IsReady)
            {
                MarkRecoveryFailed(
                    "EditorDirect 恢复会话完成回调不属于本次 Catalog 恢复事务，或仍处于 Catalog 降级状态；已拒绝伪成功。",
                    true);
                return;
            }

            if (succeeded)
            {
                Debug.Log("[ESRes][CatalogRecovery] EditorDirect 恢复会话重建成功，恢复事务完成。");
                ClearRecoveryTransaction();
                return;
            }

            MarkRecoveryFailed("EditorDirect 恢复会话重建失败：" + (string.IsNullOrWhiteSpace(error) ? "未知错误。" : error), true);
        }

        private static void MarkRecoveryFailed(string message, bool stopPlayMode)
        {
            ESRuntimeDataAsset.InvalidateAssetConfigTableBinding();
            SetRecoveryState(ESEditorCatalogRecoveryState.Failed);
            SessionState.SetBool(CatalogRecoveryBakeRequestedKey, false);
            SessionState.SetBool(CatalogRecoveryOpenConfigRequestedKey, false);
            SessionState.SetString(CatalogRecoveryCatalogGenerationKey, string.Empty);
            SessionState.SetString(CatalogRecoveryCommitGenerationKey, string.Empty);
            SessionState.SetString(CatalogRecoveryCatalogFingerprintKey, string.Empty);
            SessionState.SetString(CatalogRecoveryConfigTableGenerationKey, string.Empty);
            SessionState.SetString(CatalogRecoveryDomainEpochKey, string.Empty);
            SessionState.SetString(CatalogRecoveryFailureMessageKey, message ?? "EditorDirect Catalog 恢复失败。\n");
            if (stopPlayMode)
                Schedule(ExitPlayModeForCatalogRecovery);
            if (!recoveryFailureDialogScheduled)
            {
                recoveryFailureDialogScheduled = true;
                Schedule(ShowRecoveryFailureDialog);
            }
        }

        private static void ShowRecoveryFailureDialog()
        {
            recoveryFailureDialogScheduled = false;
            if (GetRecoveryState() != ESEditorCatalogRecoveryState.Failed
                || SessionState.GetBool(CatalogRecoveryFailureDialogShownKey, false)
                || Application.isBatchMode)
                return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Schedule(ShowRecoveryFailureDialog);
                return;
            }

            SessionState.SetBool(CatalogRecoveryFailureDialogShownKey, true);
            string message = SessionState.GetString(CatalogRecoveryFailureMessageKey, "EditorDirect Catalog 恢复失败。\n");
            string settingsPath = SessionState.GetString(CatalogRecoverySettingsPathKey, string.Empty);
            bool openConfiguration = EditorUtility.DisplayDialog(
                "ES EditorDirect Catalog 恢复已停止",
                message + "\n\n已停止自动重试，不会再次循环进入 PlayMode。请修复配置后手动重新运行。",
                "打开资源配置",
                "保持编辑模式");
            if (openConfiguration)
            {
                ClearRecoveryTransaction();
                OpenCatalogConfiguration(settingsPath);
            }
        }

        private static void OpenRequestedCatalogConfiguration()
        {
            if (!SessionState.GetBool(CatalogRecoveryOpenConfigRequestedKey, false)
                || EditorApplication.isPlayingOrWillChangePlaymode || Application.isBatchMode)
                return;
            string settingsPath = SessionState.GetString(CatalogRecoverySettingsPathKey, string.Empty);
            ClearRecoveryTransaction();
            OpenCatalogConfiguration(settingsPath);
        }

        private static void OpenCatalogConfiguration(string settingsPath)
        {
            ESResWindow.TryOpenWindow();
            ESGlobalResSetting settings = !string.IsNullOrWhiteSpace(settingsPath)
                ? AssetDatabase.LoadAssetAtPath<ESGlobalResSetting>(settingsPath)
                : ESGlobalResSetting.Instance;
            if (settings == null)
                return;
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        private static void OnMissingConfigKey(string scope, string description)
        {
            if (missingConfigPromptShown || !EditorApplication.isPlaying || !ESAssets.IsReady || Application.isBatchMode)
                return;
            if (!ESAssetRunModeSession.TryGetLockedModes(out _, out ESAssetRunMode effectiveMode)
                || effectiveMode != ESAssetRunMode.EditorDirect)
                return;
            missingConfigPromptShown = true;
            Schedule(() =>
            {
                if (!EditorApplication.isPlaying
                    || !ESAssetRunModeSession.TryGetLockedModes(out _, out ESAssetRunMode scheduledMode)
                    || scheduledMode != ESAssetRunMode.EditorDirect)
                    return;
                var report = new ESEditorCatalogRecoveryReport();
                report.AddDegradableFailure("检测到 ConfigKey/ConfigData 未注入当前运行表。Scope=" + scope + "，" + description);
                string diagnostic = ESEditorResourceSessionBootstrap.EditorDirectCatalogDiagnostic;
                if (!string.IsNullOrWhiteSpace(diagnostic))
                    report.AddDegradableFailure(diagnostic);
                HandleCatalogRecovery(ESGlobalResSetting.Instance, report);
            });
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                ESEditorResourceSessionBootstrap.DisposeCurrent();
                handledThisPlaySession = false;
                if (!IsRecoveryReentryPending())
                {
                    // ExitingEditMode starts a new user-initiated PlayMode attempt.
                    // Automatic Catalog recovery reentry keeps the original prompt
                    // decision so a repeated missing key cannot reopen the dialog.
                    missingConfigPromptShown = false;
                    SessionState.SetBool(CatalogRecoveryPromptShownKey, false);
                }
                return;
            }

            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                ESEditorResourceSessionBootstrap.DisposeCurrent();
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    ScheduleRecoveryContinuationAfterReload();
                }
                return;
            }

            if (state != PlayModeStateChange.EnteredPlayMode || handledThisPlaySession)
                return;

            handledThisPlaySession = true;
            if (IsRecoveryReentryPending())
                Schedule(StartRecoverySession);
            else
                Schedule(PromptIfNeeded);
        }

        private static void PromptIfNeeded()
        {
            if (!EditorApplication.isPlaying || ESAssets.IsReady || ESResManager.Instance != null
                || ESEditorResourceSessionBootstrap.IsActive)
                return;

            if (IsRecoveryReentryPending())
            {
                StartRecoverySession();
                return;
            }

            ESEditorCatalogRecoveryState recoveryState = GetRecoveryState();
            if (recoveryState == ESEditorCatalogRecoveryState.BakeRequested
                || recoveryState == ESEditorCatalogRecoveryState.Baking
                || recoveryState == ESEditorCatalogRecoveryState.AwaitingValidation)
                return;

            ESGlobalResSetting settings = ESGlobalResSetting.Instance;
            if (settings == null)
            {
                Debug.LogError("[ESRes][EditorSession] 未找到 ESGlobalResSetting GlobalData，无法建立临时资源会话。");
                return;
            }

            if (Application.isBatchMode)
            {
                if (HasCommandLineArgument("-esInitializeTemporaryResourceSession"))
                {
                    ESEditorResourceSessionBootstrap.Create(settings);
                    return;
                }
                throw new InvalidOperationException(
                    "[ESRes][EditorSession] 批处理 PlayMode 未配置正式资源 Bootstrap。"
                    + "如需显式创建临时资源会话，请传入 -esInitializeTemporaryResourceSession；否则失败关闭。");
            }

            int choice = EditorUtility.DisplayDialogComplex(
                "ES 临时资源会话",
                "当前 PlayMode 场景没有新版资源会话，且资源 Provider 尚未初始化。\n\n"
                + "可以使用项目 GlobalData 创建仅属于本次 PlayMode 的临时资源会话。该操作不会修改或弄脏当前 Scene。",
                "初始化本次资源会话",
                "本次不初始化",
                "打开全局资源配置");

            if (choice == 0)
                ESEditorResourceSessionBootstrap.Create(settings);
            else if (choice == 2)
            {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            }
        }

        private static bool HasCommandLineArgument(string expected)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; i++)
                if (string.Equals(arguments[i], expected, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }

    internal sealed class ESEditorResourceSessionAssemblyStreamInitializer : EditorInvoker_Level0
    {
        public override void InitInvoke()
        {
            ESEditorResourceSessionPrompt.Register();
        }
    }

    internal sealed class ESEditorResourceSessionBootstrap
    {
        private static ESEditorResourceSessionBootstrap current;

        internal static event Action<ESEditorResourceSessionBootstrap, bool, string> InitializationCompleted;

        private ESGlobalResSetting settings;
        private CancellationTokenSource cancellation;
        private bool runModeSessionTouched;
        private bool destroyed;
        private ESGameManager createdGameManager;
        private ESRuntimeDataModule runtimeData;
        private IESAssetRuntimeProvider providerBeforeSession;
        private IESAssetRuntimeProvider ownedProvider;
        private int ownedProviderGeneration;
        private ESGlobalAssetRuntimeMap temporaryRuntimeMap;
        private bool editorCatalogDegraded;
        private bool editorCatalogValidated;
        private long editorCatalogCommitGeneration;
        private long editorConfigTableGeneration;
        private long recoveryCommitGeneration;
        private string editorCatalogFingerprint = string.Empty;
        private string editorCatalogDiagnostic = string.Empty;
        private string recoveryTransactionId;

        internal static bool IsActive => current != null && !current.destroyed;
        internal static ESEditorResourceSessionBootstrap Current => current;
        internal static bool IsEditorDirectCatalogDegraded
            => current != null && !current.destroyed && current.editorCatalogDegraded;
        internal static string EditorDirectCatalogDiagnostic
            => current == null || current.destroyed ? string.Empty : current.editorCatalogDiagnostic ?? string.Empty;
        internal bool EditorDirectCatalogDegraded => editorCatalogDegraded;
        internal bool EditorCatalogValidated => editorCatalogValidated;
        internal long EditorCatalogCommitGeneration => editorCatalogCommitGeneration;
        internal long EditorConfigTableGeneration => editorConfigTableGeneration;
        internal long RecoveryCommitGeneration => recoveryCommitGeneration;
        internal string EditorCatalogFingerprint => editorCatalogFingerprint;
        internal int OwnedProviderGeneration => ownedProviderGeneration;

        internal bool IsOwnedByRecoveryTransaction(string transactionId)
            => !string.IsNullOrWhiteSpace(transactionId)
                && string.Equals(recoveryTransactionId, transactionId, StringComparison.Ordinal);

        internal static void Create(ESGlobalResSetting globalSettings)
        {
            CreateInternal(globalSettings, null);
        }

        internal static bool CreateForRecovery(ESGlobalResSetting globalSettings, string transactionId)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
                return false;
            return CreateInternal(globalSettings, transactionId);
        }

        private static bool CreateInternal(ESGlobalResSetting globalSettings, string transactionId)
        {
            if (globalSettings == null) throw new ArgumentNullException(nameof(globalSettings));
            if (ESAssets.IsReady || ESResManager.Instance != null
                || IsActive)
                return false;

            var bootstrap = new ESEditorResourceSessionBootstrap
            {
                settings = globalSettings,
                recoveryTransactionId = transactionId
            };
            current = bootstrap;
            bootstrap.BeginAsync().Forget();
            return true;
        }

        internal static void DisposeCurrent()
        {
            ESEditorResourceSessionBootstrap bootstrap = current;
            current = null;
            bootstrap?.Dispose();
        }

        private async UniTaskVoid BeginAsync()
        {
            cancellation = new CancellationTokenSource();
            try
            {
                runtimeData = EnsureRuntimeDataModule();
                providerBeforeSession = runtimeData.ExistingAssetLoadingService?.RuntimeBackend;
                if (providerBeforeSession != null)
                    throw new InvalidOperationException("[ESRes][EditorSession] 资源 Provider 正在已有会话或切换流程中，不能创建第二个临时会话。");
                ESAssetRunMode effectiveMode = ESAssetRunModeSession.Lock(settings);
                runModeSessionTouched = true;
                ESRuntimeDataAsset.InvalidateAssetConfigTableBinding();
                switch (effectiveMode)
                {
                    case ESAssetRunMode.EditorDirect:
                    {
                        temporaryRuntimeMap = ScriptableObject.CreateInstance<ESGlobalAssetRuntimeMap>();
                        temporaryRuntimeMap.hideFlags = HideFlags.HideAndDontSave;
                        runtimeData.InitializeAssetLoadingForRunMode(temporaryRuntimeMap, settings, ESRuntimeRetryPolicy.Default);
                        CaptureOwnedProvider();
                        bool catalogSessionReady = await InitializeEditorCatalogsAndGameCoreAsync();
                        if (!catalogSessionReady)
                        {
                            if (!destroyed)
                                NotifyInitializationCompleted(false, editorCatalogDiagnostic);
                            Dispose();
                            return;
                        }
                        CaptureOwnedProvider();
                        break;
                    }
                    case ESAssetRunMode.LocalBuild:
                    case ESAssetRunMode.HotUpdate:
                    {
                        ESRuntimeReleaseDownloadResult result = await ESRuntimeReleaseBootstrap.InitializeAsync(settings, cancellation.Token);
                        cancellation.Token.ThrowIfCancellationRequested();
                        await runtimeData.InitializeAssetLoadingFromReleaseResultAsync(settings, result, cancellation.Token);
                        CaptureOwnedProvider();
                        break;
                    }
                    case ESAssetRunMode.EditorSimulateBuild:
                    {
                        bool hasLocalRelease = Directory.Exists(settings.Path_LocalBuildPlatform)
                            && File.Exists(Path.Combine(settings.Path_LocalBuildPlatform, "ESAssetReleaseManifest.json"))
                            && File.Exists(Path.Combine(settings.Path_LocalBuildPlatform, "ESAssetReleaseBundleIndex.json"));
                        ESAssetRunMode metadataSource = hasLocalRelease ? ESAssetRunMode.LocalBuild : ESAssetRunMode.HotUpdate;
                        var downloader = new ESRuntimeReleaseDownloader(settings, metadataSource);
                        ESRuntimeReleaseDownloadResult result = await downloader.DownloadEditorSimulationMetadataAsync(cancellation.Token);
                        cancellation.Token.ThrowIfCancellationRequested();
                        await runtimeData.InitializeAssetLoadingFromReleaseResultAsync(settings, result, cancellation.Token);
                        CaptureOwnedProvider();
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (destroyed)
                {
                    DisposeOwnedSession();
                    return;
                }

                if (effectiveMode == ESAssetRunMode.EditorDirect && editorCatalogDegraded)
                    Debug.LogWarning("[ESRes][EditorSession] 临时资源会话已进入 Catalog 降级模式。"
                        + "直接 ESAssetRefer 可用，ConfigKey/ConfigData 保持未配置。ConfiguredMode="
                        + settings.AssetRunMode + ", EffectiveMode=" + effectiveMode);
                else
                    Debug.Log("[ESRes][EditorSession] 临时资源会话初始化完成。ConfiguredMode=" + settings.AssetRunMode
                        + ", EffectiveMode=" + effectiveMode);
                NotifyInitializationCompleted(true, null);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (!destroyed)
                    NotifyInitializationCompleted(false, exception.Message);
                if (!destroyed && !Application.isBatchMode && !ESEditorResourceSessionPrompt.IsRecoveryReentryActive)
                    EditorUtility.DisplayDialog("ES 临时资源会话初始化失败", exception.Message, "确定");
                Dispose();
            }
        }

        private void NotifyInitializationCompleted(bool succeeded, string error)
        {
            try { InitializationCompleted?.Invoke(this, succeeded, error); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private ESRuntimeDataModule EnsureRuntimeDataModule()
        {
            if (ESGameManager.RuntimeData == null && ESGameManager.Instance == null)
            {
                var gameManagerObject = new GameObject("ESGameManager (Editor Resource Session)");
                gameManagerObject.hideFlags = HideFlags.HideAndDontSave;
                gameManagerObject.SetActive(false);
                createdGameManager = gameManagerObject.AddComponent<ESGameManager>();
                createdGameManager.autoCreateCommandModule = false;
                createdGameManager.autoCreateInputModule = false;
                createdGameManager.autoCreateRuntimeDataModule = true;
                createdGameManager.autoCreateGameObjectPoolModule = false;
                createdGameManager.autoCreateAudioModule = false;
                createdGameManager.autoCreateCameraModule = false;
                createdGameManager.autoCreatePhysicsQueryModule = false;
                createdGameManager.autoCreateLODModule = false;
                createdGameManager.dontDestroyOnLoad = true;
                gameManagerObject.SetActive(true);
            }
            else if (ESGameManager.RuntimeData == null && ESGameManager.Instance != null)
            {
                ESGameManager.GetOrCreateModule<ESRuntimeDataModule>();
                ESGameManager.RefreshStaticCache();
            }
            return ESGameManager.RuntimeData
                ?? throw new InvalidOperationException("[ESRes][EditorSession] ESGameManager 未能创建 ESRuntimeDataModule。");
        }

        private void CaptureOwnedProvider()
        {
            ownedProvider = runtimeData?.ExistingAssetLoadingService?.RuntimeBackend;
            ownedProviderGeneration = ESAssets.RuntimeBackendGeneration;
            if (ownedProvider == null)
                throw new InvalidOperationException("[ESRes][EditorSession] 资源服务初始化完成但未绑定 Provider。");
        }

        private async UniTask<bool> InitializeEditorCatalogsAndGameCoreAsync()
        {
            editorCatalogValidated = false;
            ESEditorCatalogRecoveryReport report = string.IsNullOrWhiteSpace(recoveryTransactionId)
                ? DiscoverEditorRuntimeCatalogs()
                : DiscoverEditorRuntimeCatalogs(
                    ESAssetPipelineIO.RecoveryBakeRoot(recoveryTransactionId),
                    recoveryTransactionId);
            if (report.HasFailures)
            {
                if (!ResolveEditorCatalogFailure(report))
                    return false;
            }
            else
            {
                if (!TryBuildEditorCatalogTables(report, out string catalogBuildError))
                {
                    report.AddFailure(catalogBuildError);
                    if (!ResolveEditorCatalogFailure(report))
                        return false;
                }
                else
                {
                    editorCatalogValidated = true;
                    editorCatalogCommitGeneration = ESRuntimeDataAsset.EditorCatalogCommitGeneration;
                    editorConfigTableGeneration = ESRuntimeDataAsset.AssetConfigTableGeneration;
                    editorCatalogFingerprint = report.catalogSetFingerprint;
                    recoveryCommitGeneration = report.sourceCommitGeneration;
                }
            }

            string[] catalogGuids = AssetDatabase.FindAssets("t:ESGameCoreAssetPreloadCatalog");
            var gameCoreReferences = new List<ESRuntimeConsumerGameCoreReference>();
            var identities = new HashSet<ESAssetIdentity>();
            for (int i = 0; i < catalogGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(catalogGuids[i]);
                ESGameCoreAssetPreloadCatalog catalog = AssetDatabase.LoadAssetAtPath<ESGameCoreAssetPreloadCatalog>(path);
                if (catalog == null) continue;
                foreach (ESAssetReferBase refer in catalog.assets)
                {
                    if (refer == null || !refer.IsValid || !refer.SupportsGameCorePreload || !identities.Add(refer.AssetIdentity))
                        continue;
                    gameCoreReferences.Add(new ESRuntimeConsumerGameCoreReference
                    {
                        guid = refer.AssetIdentity.Guid,
                        localFileId = refer.AssetIdentity.LocalFileId
                    });
                }
                // generatedAssets 属于 GameCore 预热输入，不是 Editor Catalog 的
                // ConfigKey/ConfigData 行；它们只进入 GameCore preload 列表，不能混入
                // Catalog 注入数量，否则会把两个不同权威层的计数混在一起。
                foreach (ESAssetReferBase refer in catalog.generatedAssets)
                {
                    if (refer == null || !refer.IsValid || !refer.SupportsGameCorePreload || !identities.Add(refer.AssetIdentity))
                        continue;
                    gameCoreReferences.Add(new ESRuntimeConsumerGameCoreReference
                    {
                        guid = refer.AssetIdentity.Guid,
                        localFileId = refer.AssetIdentity.LocalFileId
                    });
                }
            }

            // A standalone GameCore preload catalog is optional. The normal editor source
            // of truth is the baked Consumer data; using it here keeps EditorDirect zero-config
            // for projects that already have Consumer GameCore ownership.
            foreach (ESAssetLibraryConsumer consumer in ESEditorSO.GetGroupOfType<ESAssetLibraryConsumer>() ?? new List<ESAssetLibraryConsumer>())
            {
                AddEditorGameCoreReferences(consumer?.GameCoreAssets, gameCoreReferences, identities);
                AddEditorGameCoreReferences(consumer?.ManualGameCoreAssets, gameCoreReferences, identities);
            }

            if (gameCoreReferences.Count > 0)
                await runtimeData.PreloadGameCoreAssetsAsync(gameCoreReferences, cancellation.Token);
            if (!string.IsNullOrWhiteSpace(recoveryTransactionId))
            {
                bool tableBindingCurrent = ESRuntimeDataAsset.IsCurrentEditorCatalogCommit(
                    editorCatalogFingerprint,
                    editorConfigTableGeneration);
                bool artifactsCurrent = TryVerifyCommittedCatalogArtifacts(
                    ESAssetPipelineIO.RecoveryBakeRoot(recoveryTransactionId),
                    recoveryTransactionId,
                    recoveryCommitGeneration,
                    editorCatalogFingerprint,
                    out string artifactError);
                if (!tableBindingCurrent || !artifactsCurrent)
                {
                    editorCatalogDiagnostic = "恢复会话在 GameCore 预热后发现 Catalog 事务产物或 ConfigTable 绑定已变化："
                        + (tableBindingCurrent ? artifactError : "ConfigTable 不再属于当前恢复提交。 " + artifactError);
                    return false;
                }
            }
            return true;
        }

        private bool ResolveEditorCatalogFailure(ESEditorCatalogRecoveryReport report)
        {
            editorCatalogDiagnostic = report?.BuildMessage() ?? "EditorDirect Catalog 不可用。";
            // EditorDirect 的 Provider 不依赖 Catalog，但缺失 Catalog 仍必须由用户
            // 明确选择是否降级。候选表失败时保留之前已提交的正式表，避免“先清空
            // 再失败”破坏上一份可用映射；本会话仍不得把降级状态作为成功证据。
            editorCatalogDegraded = true;
            editorCatalogValidated = false;
            editorCatalogCommitGeneration = 0;
            editorConfigTableGeneration = 0;
            recoveryCommitGeneration = 0;
            editorCatalogFingerprint = string.Empty;
            ESRuntimeDataAsset.InvalidateAssetConfigTableBinding();
            if (!report.CanContinueDegraded)
            {
                Debug.LogError("[ESRes][EditorSession] EditorDirect Catalog 完整性失败，禁止建立降级会话："
                    + editorCatalogDiagnostic);
                ESEditorResourceSessionPrompt.HandleCatalogRecovery(settings, report);
                return false;
            }

            Debug.LogWarning("[ESRes][EditorSession] EditorDirect 进入 Catalog 降级模式："
                + editorCatalogDiagnostic + "\n直接 ESAssetRefer 仍可使用。");
            ESEditorCatalogRecoveryAction action = ESEditorResourceSessionPrompt.HandleCatalogRecovery(
                settings,
                report);
            return action == ESEditorCatalogRecoveryAction.ContinueDegraded;
        }

        internal static bool TryBuildEditorCatalogTables(ESEditorCatalogRecoveryReport report, out string error)
        {
            error = string.Empty;
            if (report == null)
            {
                error = "EditorDirect Catalog 复核报告为空。";
                return false;
            }
            if (report.HasFailures)
            {
                error = report.BuildMessage();
                return false;
            }

            try
            {
                if (!ESRuntimeDataAsset.TryValidateAssetConfigTablesFromCatalogs(
                    report.catalogs,
                    out ESAssetCatalogBuildValidation validation,
                    out string validationError))
                {
                    error = validationError;
                    return false;
                }

                if (!ESRuntimeDataAsset.CommitValidatedAssetConfigTablesFromCatalogs(
                    report.catalogs,
                    validation,
                    report.catalogSetFingerprint,
                    out int injectedEntries,
                    out string commitError))
                {
                    error = commitError;
                    return false;
                }

                report.expectedBusinessEntryCount = validation.expectedBusinessEntries;
                report.injectedBusinessEntryCount = injectedEntries;
                if (!ESRuntimeDataAsset.IsCurrentEditorCatalogCommit(
                    report.catalogSetFingerprint,
                    ESRuntimeDataAsset.AssetConfigTableGeneration))
                {
                    error = "ConfigKey/ConfigData 提交后 Catalog 指纹或表 generation 不一致。";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                report.AddFailure("ConfigKey/ConfigData 注入失败：" + exception.Message);
                error = report.BuildMessage();
                return false;
            }
        }

        private static void AddEditorGameCoreReferences(IEnumerable<ESAssetReferBase> source,
            List<ESRuntimeConsumerGameCoreReference> destination, HashSet<ESAssetIdentity> identities)
        {
            foreach (ESAssetReferBase refer in source ?? Array.Empty<ESAssetReferBase>())
            {
                if (refer == null || !refer.IsValid || !refer.SupportsGameCorePreload)
                    continue;
                AddEditorGameCoreReference(refer.AssetIdentity.Guid, refer.AssetIdentity.LocalFileId, destination, identities);
            }
        }

        private static void AddEditorGameCoreReference(string guid, long localFileId,
            List<ESRuntimeConsumerGameCoreReference> destination, HashSet<ESAssetIdentity> identities)
        {
            if (string.IsNullOrEmpty(guid) || !identities.Add(new ESAssetIdentity(guid, localFileId)))
                return;
            destination.Add(new ESRuntimeConsumerGameCoreReference { guid = guid, localFileId = localFileId });
        }

        internal static ESEditorCatalogRecoveryReport DiscoverEditorRuntimeCatalogs()
        {
            return DiscoverEditorRuntimeCatalogs(ESAssetPipelineIO.BakeRoot, string.Empty);
        }

        internal static ESEditorCatalogRecoveryReport DiscoverEditorRuntimeCatalogs(
            string outputRoot, string transactionId)
        {
            var report = new ESEditorCatalogRecoveryReport();
            var paths = new List<string>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(transactionId))
            {
                report.sourceTransactionId = transactionId;
                if (!TryGetCommittedCatalogPaths(outputRoot, transactionId, paths, seenPaths, report))
                    return report;
            }
            else
            {
                AddCatalogFiles(outputRoot, paths, seenPaths, true);
            }
            report.discoveredFileCount = paths.Count;
            if (paths.Count == 0)
            {
                report.AddDegradableFailure("未找到 ESAssetLibraryCatalog.json；请先执行“烘焙引用”。");
                return report;
            }
            var libraries = new Dictionary<string, string>(StringComparer.Ordinal);
            var sourcePairFingerprints = new List<string>();
            string discoveredBakeGeneration = null;
            foreach (string path in paths)
            {
                try
                {
                    ESRuntimeCatalog catalog = ESAssetPipelineIO.ReadJson<ESRuntimeCatalog>(path);
                    if (catalog == null)
                        throw new InvalidDataException("Catalog JSON 为空：" + path);
                    if (catalog.formatVersion != ESAssetPipelineIO.CatalogFormatVersion)
                        throw new InvalidDataException("Catalog 协议版本不匹配：" + path + "，Version=" + catalog.formatVersion);
                    if (string.IsNullOrWhiteSpace(catalog.libraryFolder)
                        || string.IsNullOrWhiteSpace(catalog.libraryName))
                        throw new InvalidDataException("Catalog 缺少 libraryFolder/libraryName：" + path);
                    string libraryKey = catalog.libraryFolder;
                    string graphPath = Path.Combine(
                        Path.GetDirectoryName(path) ?? string.Empty,
                        ESAssetPipelineIO.ReferenceGraphFileName);
                    if (!File.Exists(graphPath))
                        throw new FileNotFoundException("Catalog 缺少同次烘焙的 ReferenceGraph。", graphPath);
                    ESAssetReferenceGraph graph = ESAssetPipelineIO.ReadJson<ESAssetReferenceGraph>(graphPath);
                    if (!ValidateCatalogGraphPair(
                        catalog,
                        graph,
                        catalog.libraryName,
                        catalog.libraryFolder,
                        path,
                        report))
                        continue;
                    if (discoveredBakeGeneration == null)
                        discoveredBakeGeneration = catalog.generatedUtc;
                    else if (!string.Equals(discoveredBakeGeneration, catalog.generatedUtc, StringComparison.Ordinal))
                    {
                        report.AddFailure("Editor Catalog 集合混用了不同 Bake generation；请重新执行完整烘焙。");
                        continue;
                    }
                    string signature = JsonConvert.SerializeObject(catalog);
                    if (libraries.TryGetValue(libraryKey, out string existingSignature))
                    {
                        report.AddFailure("同名 Library 出现重复 Editor Catalog：" + libraryKey
                            + (string.Equals(existingSignature, signature, StringComparison.Ordinal)
                                ? "。即使内容相同也只能保留一个权威输出。"
                                : "，且重复内容冲突。请重新 Bake 或显式处理旧输出。"));
                        continue;
                    }
                    libraries.Add(libraryKey, signature);
                    report.catalogs.Add(catalog);
                    sourcePairFingerprints.Add(libraryKey + "|"
                        + ESResManifestIntegrity.ComputeFileSha256(path) + "|"
                        + ESResManifestIntegrity.ComputeFileSha256(graphPath));
                }
                catch (Exception exception)
                {
                    report.AddFailure("Catalog 读取失败：" + path + "，" + exception.Message);
                }
            }
            if (report.catalogs.Count == 0 && report.failures.Count == 0)
                report.AddFailure("未发现可用的 Editor Catalog。");
            if (!report.HasFailures && string.IsNullOrWhiteSpace(report.catalogSetFingerprint))
                report.catalogSetFingerprint = sourcePairFingerprints.Count > 0
                    ? ESResManifestIntegrity.ComputeFileSha256FromText(string.Join(
                        "\n",
                        sourcePairFingerprints.OrderBy(item => item, StringComparer.Ordinal)))
                    : ComputeCatalogContentFingerprint(report.catalogs);
            return report;
        }

        private static string ComputeCatalogContentFingerprint(IEnumerable<ESRuntimeCatalog> catalogs)
        {
            string canonical = string.Join("\n", (catalogs ?? Enumerable.Empty<ESRuntimeCatalog>())
                .Where(item => item != null)
                .OrderBy(item => item.libraryFolder ?? item.libraryName, StringComparer.Ordinal)
                .Select(item => (item.libraryFolder ?? string.Empty) + "\n"
                    + (item.libraryName ?? string.Empty) + "\n"
                    + JsonConvert.SerializeObject(item, Formatting.None)));
            return string.IsNullOrEmpty(canonical)
                ? string.Empty
                : ESResManifestIntegrity.ComputeFileSha256FromText(canonical);
        }

        private sealed class CommittedCatalogPair
        {
            internal string libraryName = string.Empty;
            internal string libraryFolder = string.Empty;
            internal ESAssetCatalogBakeOutput catalogOutput;
            internal ESAssetCatalogBakeOutput graphOutput;
            internal string catalogPath = string.Empty;
            internal string graphPath = string.Empty;
        }

        private static bool TryGetCommittedCatalogPaths(
            string outputRoot,
            string transactionId,
            List<string> paths,
            HashSet<string> seenPaths,
            ESEditorCatalogRecoveryReport report)
        {
            if (string.IsNullOrWhiteSpace(outputRoot))
            {
                report.AddFailure("Catalog 恢复事务没有记录专属输出目录。");
                return false;
            }

            string commitPath;
            string expectedRoot;
            try
            {
                commitPath = ESAssetPipelineIO.RecoveryBakeCommitPath(transactionId);
                string normalizedRoot = Path.GetFullPath(outputRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                expectedRoot = Path.GetFullPath(ESAssetPipelineIO.RecoveryBakeRoot(transactionId))
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!string.Equals(normalizedRoot, expectedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    report.AddFailure("Catalog 恢复事务输出目录与 transaction ID 不匹配。");
                    return false;
                }
                ESManagedFileIO.EnsureNoNestedReparsePoints(expectedRoot);
            }
            catch (Exception exception)
            {
                report.AddFailure("Catalog 恢复事务输出目录无效：" + exception.Message);
                return false;
            }

            if (!File.Exists(commitPath))
            {
                report.AddFailure("本次 Catalog 恢复烘焙没有提交标记，拒绝使用旧 Baked 产物。");
                return false;
            }

            try
            {
                ESAssetCatalogBakeCommit commit = ESAssetPipelineIO.ReadJson<ESAssetCatalogBakeCommit>(commitPath);
                if (commit == null
                    || commit.formatVersion != ESAssetPipelineIO.CatalogBakeCommitFormatVersion
                    || commit.commitGeneration <= 0
                    || string.IsNullOrWhiteSpace(commit.generatedUtc)
                    || !string.Equals(commit.transactionId, transactionId, StringComparison.Ordinal))
                {
                    report.AddFailure("Catalog 恢复提交标记无效、协议过期或不属于本次 transaction ID。");
                    return false;
                }

                var pairs = new Dictionary<string, CommittedCatalogPair>(StringComparer.Ordinal);
                var seenRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var committedAbsolutePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ESAssetCatalogBakeOutput output in commit.outputs ?? new List<ESAssetCatalogBakeOutput>())
                {
                    string normalizedRelativePath = output?.relativePath?.Trim().Replace('\\', '/') ?? string.Empty;
                    if (output == null || string.IsNullOrWhiteSpace(normalizedRelativePath)
                        || !seenRelativePaths.Add(normalizedRelativePath))
                    {
                        report.AddFailure("Catalog 恢复提交标记包含重复或空输出路径。");
                        continue;
                    }

                    bool catalogKind = string.Equals(
                        output.outputKind,
                        ESAssetPipelineIO.CatalogOutputKind,
                        StringComparison.Ordinal);
                    bool graphKind = string.Equals(
                        output.outputKind,
                        ESAssetPipelineIO.ReferenceGraphOutputKind,
                        StringComparison.Ordinal);
                    int expectedProtocol = catalogKind
                        ? ESAssetPipelineIO.CatalogFormatVersion
                        : ESAssetPipelineIO.ReferenceGraphFormatVersion;
                    string expectedFileName = catalogKind
                        ? ESAssetPipelineIO.CatalogFileName
                        : ESAssetPipelineIO.ReferenceGraphFileName;
                    if ((!catalogKind && !graphKind)
                        || output.isCatalog != catalogKind
                        || output.commitGeneration != commit.commitGeneration
                        || output.protocolVersion != expectedProtocol
                        || string.IsNullOrWhiteSpace(output.libraryFolder)
                        || string.IsNullOrWhiteSpace(output.libraryName))
                    {
                        report.AddFailure("Catalog 恢复提交标记包含无效的输出类型、协议或 generation："
                            + normalizedRelativePath);
                        continue;
                    }

                    string expectedRelativePath = ESAssetPipelineIO.SafeSegment(output.libraryFolder)
                        + "/" + expectedFileName;
                    if (!string.Equals(normalizedRelativePath, expectedRelativePath, StringComparison.OrdinalIgnoreCase))
                    {
                        report.AddFailure("Catalog 恢复输出路径与 Library/输出类型不匹配："
                            + normalizedRelativePath + "，Expected=" + expectedRelativePath);
                        continue;
                    }

                    string path = ESAssetPipelineIO.ResolveGeneratedRelativePath(expectedRoot, normalizedRelativePath);
                    if (!File.Exists(path))
                    {
                        report.AddFailure("本次 Catalog 恢复输出缺失：" + normalizedRelativePath);
                        continue;
                    }

                    FileInfo info = new FileInfo(path);
                    if (info.Length != output.size
                        || !string.Equals(
                            ESResManifestIntegrity.ComputeFileSha256(path),
                            output.sha256,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        report.AddFailure("本次 Catalog 恢复输出 Hash/Size 校验失败：" + normalizedRelativePath);
                        continue;
                    }

                    committedAbsolutePaths.Add(Path.GetFullPath(path));
                    if (!pairs.TryGetValue(output.libraryFolder, out CommittedCatalogPair pair))
                    {
                        pair = new CommittedCatalogPair
                        {
                            libraryName = output.libraryName,
                            libraryFolder = output.libraryFolder
                        };
                        pairs.Add(output.libraryFolder, pair);
                    }
                    else if (!string.Equals(pair.libraryName, output.libraryName, StringComparison.Ordinal))
                    {
                        report.AddFailure("Catalog/Graph 提交中的同一 LibraryFolder 对应不同 LibraryName："
                            + output.libraryFolder);
                        continue;
                    }

                    if (catalogKind)
                    {
                        if (pair.catalogOutput != null)
                            report.AddFailure("同一 Library 提交了重复 Catalog：" + output.libraryFolder);
                        else
                        {
                            pair.catalogOutput = output;
                            pair.catalogPath = path;
                        }
                    }
                    else
                    {
                        if (pair.graphOutput != null)
                            report.AddFailure("同一 Library 提交了重复 ReferenceGraph：" + output.libraryFolder);
                        else
                        {
                            pair.graphOutput = output;
                            pair.graphPath = path;
                        }
                    }
                }

                if (pairs.Count == 0 || committedAbsolutePaths.Count != pairs.Count * 2)
                    report.AddFailure("本次 Catalog 恢复提交没有形成完整的 Catalog/Graph 成对集合。");

                string committedBakeGeneration = null;
                foreach (CommittedCatalogPair pair in pairs.Values)
                {
                    if (pair.catalogOutput == null || pair.graphOutput == null)
                    {
                        report.AddFailure("Library 缺少 Catalog 或 ReferenceGraph：" + pair.libraryFolder);
                        continue;
                    }

                    ESRuntimeCatalog catalog = ESAssetPipelineIO.ReadJson<ESRuntimeCatalog>(pair.catalogPath);
                    ESAssetReferenceGraph graph = ESAssetPipelineIO.ReadJson<ESAssetReferenceGraph>(pair.graphPath);
                    if (!ValidateCatalogGraphPair(catalog, graph, pair.libraryName, pair.libraryFolder, pair.catalogPath, report))
                        continue;
                    if (committedBakeGeneration == null)
                        committedBakeGeneration = catalog.generatedUtc;
                    else if (!string.Equals(committedBakeGeneration, catalog.generatedUtc, StringComparison.Ordinal))
                    {
                        report.AddFailure("恢复提交混用了不同 Bake generation：" + pair.libraryFolder);
                        continue;
                    }

                    if (seenPaths.Add(pair.catalogPath))
                        paths.Add(pair.catalogPath);
                }

                foreach (string generatedPath in ESManagedFileIO.EnumerateFilesSafely(expectedRoot, ESAssetPipelineIO.CatalogFileName)
                    .Concat(ESManagedFileIO.EnumerateFilesSafely(expectedRoot, ESAssetPipelineIO.ReferenceGraphFileName)))
                {
                    if (!committedAbsolutePaths.Contains(Path.GetFullPath(generatedPath)))
                        report.AddFailure("恢复事务目录包含提交标记未声明的 Catalog/Graph：" + generatedPath);
                }

                if (report.failures.Count == 0)
                {
                    report.sourceCommitGeneration = commit.commitGeneration;
                    report.catalogSetFingerprint = ComputeCommittedOutputFingerprint(commit);
                }
                return report.failures.Count == 0;
            }
            catch (Exception exception)
            {
                report.AddFailure("Catalog 恢复提交标记读取失败：" + exception.Message);
                return false;
            }
        }

        internal static bool TryVerifyCommittedCatalogArtifacts(
            string outputRoot,
            string transactionId,
            long expectedCommitGeneration,
            string expectedFingerprint,
            out string error)
        {
            var validationReport = new ESEditorCatalogRecoveryReport
            {
                sourceTransactionId = transactionId
            };
            var paths = new List<string>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool valid = TryGetCommittedCatalogPaths(
                outputRoot,
                transactionId,
                paths,
                seenPaths,
                validationReport);
            if (!valid
                || validationReport.sourceCommitGeneration != expectedCommitGeneration
                || string.IsNullOrWhiteSpace(expectedFingerprint)
                || !string.Equals(
                    validationReport.catalogSetFingerprint,
                    expectedFingerprint,
                    StringComparison.OrdinalIgnoreCase))
            {
                error = validationReport.HasFailures
                    ? validationReport.BuildMessage()
                    : "Catalog 恢复事务 generation 或 fingerprint 在提交后发生变化。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool ValidateCatalogGraphPair(
            ESRuntimeCatalog catalog,
            ESAssetReferenceGraph graph,
            string expectedLibraryName,
            string expectedLibraryFolder,
            string catalogPath,
            ESEditorCatalogRecoveryReport report)
        {
            if (catalog == null || graph == null)
            {
                report.AddFailure("Catalog/ReferenceGraph JSON 为空：" + catalogPath);
                return false;
            }
            if (catalog.formatVersion != ESAssetPipelineIO.CatalogFormatVersion
                || graph.formatVersion != ESAssetPipelineIO.ReferenceGraphFormatVersion)
            {
                report.AddFailure("Catalog/ReferenceGraph 协议版本不匹配：" + expectedLibraryFolder);
                return false;
            }
            if (!string.Equals(catalog.libraryName, expectedLibraryName, StringComparison.Ordinal)
                || !string.Equals(graph.libraryName, expectedLibraryName, StringComparison.Ordinal)
                || !string.Equals(catalog.libraryFolder, expectedLibraryFolder, StringComparison.Ordinal)
                || !string.Equals(graph.libraryFolder, expectedLibraryFolder, StringComparison.Ordinal))
            {
                report.AddFailure("Catalog/ReferenceGraph Library 身份不一致：" + expectedLibraryFolder);
                return false;
            }
            if (string.IsNullOrWhiteSpace(catalog.generatedUtc)
                || !string.Equals(catalog.generatedUtc, graph.generatedUtc, StringComparison.Ordinal))
            {
                report.AddFailure("Catalog/ReferenceGraph 不属于同一次 Library 烘焙：" + expectedLibraryFolder);
                return false;
            }
            var catalogRoots = new HashSet<string>((catalog.assets ?? new List<ESRuntimeCatalogEntry>())
                .Where(item => item?.identity != null && item.identity.IsValid)
                .Select(item => item.identity.guid + ":" + item.identity.localFileId), StringComparer.Ordinal);
            var graphRoots = new HashSet<string>((graph.roots ?? new List<ESAssetReferenceRoot>())
                .Where(item => item?.identity != null && item.identity.IsValid)
                .Select(item => item.identity.guid + ":" + item.identity.localFileId), StringComparer.Ordinal);
            if (!catalogRoots.SetEquals(graphRoots))
            {
                report.AddFailure("Catalog/ReferenceGraph 根资源集合不一致：" + expectedLibraryFolder);
                return false;
            }
            return true;
        }

        private static string ComputeCommittedOutputFingerprint(ESAssetCatalogBakeCommit commit)
        {
            string canonical = (commit.transactionId ?? string.Empty) + "\n"
                + commit.commitGeneration + "\n"
                + (commit.generatedUtc ?? string.Empty) + "\n"
                + string.Join("\n", (commit.outputs ?? new List<ESAssetCatalogBakeOutput>())
                    .Where(item => item != null)
                    .OrderBy(item => item.libraryFolder, StringComparer.Ordinal)
                    .ThenBy(item => item.outputKind, StringComparer.Ordinal)
                    .Select(item => (item.libraryFolder ?? string.Empty) + "|"
                        + (item.libraryName ?? string.Empty) + "|"
                        + (item.outputKind ?? string.Empty) + "|"
                        + (item.relativePath ?? string.Empty).Replace('\\', '/') + "|"
                        + item.protocolVersion + "|" + item.commitGeneration + "|"
                        + item.size + "|" + (item.sha256 ?? string.Empty).ToLowerInvariant()));
            return ESResManifestIntegrity.ComputeFileSha256FromText(canonical);
        }

        internal static void ClearEditorConfigTables()
        {
            if (ESRuntimeDataAsset.HasPendingAssetLoads)
                throw new InvalidOperationException("[ESRes][CatalogRecovery] 当前仍有 ConfigKey 资源加载请求，"
                    + "无法安全清空不完整配置表；EditorDirect 会话拒绝继续初始化。");
            ESRuntimeDataAsset.ClearAssetConfigTables();
        }

        private static void AddCatalogFiles(string root, List<string> paths, HashSet<string> seenPaths, bool skipRecoveryOutputs)
        {
            if (string.IsNullOrWhiteSpace(root)) return;
            string fullRoot = Path.GetFullPath(root);
            if (!Directory.Exists(fullRoot)) return;
            foreach (string path in ESManagedFileIO.EnumerateFilesSafely(fullRoot, "ESAssetLibraryCatalog.json"))
            {
                if (skipRecoveryOutputs
                    && path.IndexOf(Path.DirectorySeparatorChar + ".Recovery" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (seenPaths.Add(path)) paths.Add(path);
            }
        }

        private void Dispose()
        {
            if (destroyed)
                return;
            destroyed = true;
            if (ReferenceEquals(current, this))
                current = null;
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = null;
            DisposeOwnedSession();
        }

        private void DisposeOwnedSession()
        {
            if (ownedProvider == null && runModeSessionTouched && providerBeforeSession == null
                && runtimeData != null && ESResManager.Instance == null)
            {
                IESAssetRuntimeProvider candidate = runtimeData.ExistingAssetLoadingService?.RuntimeBackend;
                if (candidate != null)
                {
                    ownedProvider = candidate;
                    ownedProviderGeneration = ESAssets.RuntimeBackendGeneration;
                }
            }

            if (ownedProvider != null)
            {
                if (runtimeData != null
                    && ReferenceEquals(runtimeData.ExistingAssetLoadingService?.RuntimeBackend, ownedProvider)
                    && ESAssets.RuntimeBackendGeneration == ownedProviderGeneration)
                    runtimeData.ExistingAssetLoadingService.Dispose();
                ownedProvider = null;
            }
            if (temporaryRuntimeMap != null)
            {
                UnityEngine.Object.Destroy(temporaryRuntimeMap);
                temporaryRuntimeMap = null;
            }
            if (runModeSessionTouched && !ESAssets.IsReady)
            {
                runModeSessionTouched = false;
                ESAssetRunModeSession.ResetAfterEditorSession();
            }

            if (createdGameManager != null && ReferenceEquals(ESGameManager.Instance, createdGameManager))
            {
                UnityEngine.Object.Destroy(createdGameManager.gameObject);
                createdGameManager = null;
            }
        }
    }
}
