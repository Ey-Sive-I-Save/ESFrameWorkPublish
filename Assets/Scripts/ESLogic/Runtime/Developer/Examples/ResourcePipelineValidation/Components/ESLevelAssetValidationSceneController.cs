using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    /// <summary>验收场景控制器：换关时释放旧计划、场景租约及零引用 AB。</summary>
    public sealed class ESLevelAssetValidationSceneController : MonoBehaviour
    {
        private const string LogTag = "[ESRes][Validation]";
        private const int MaxEventLogCount = 48;
        private ESLevelAssetValidationLevel activeLevel;
        private ESRuntimeSceneHandle activeScene;
        private bool hasActiveScene;
        private bool busy;
        private string report = "等待从 GameCoreTable 读取验收关卡。";
        private Vector2 scroll;
        private readonly List<string> eventLog = new List<string>(MaxEventLogCount);
        private ESResourcePlanReport lastPlanReport;
        private ESRuntimeUnusedAssetBundleUnloadResult lastUnloadResult;
        private bool hasUnloadResult;
        private int operationId;

        private void Awake()
        {
            // 入口场景只是常驻控制壳。真正画面由 Additive 关卡场景提供；禁用壳场景相机，
            // 避免与关卡 Main Camera 同时渲染造成重复画面和额外 RenderLoop。
            int disabledCameraCount = 0;
            int disabledAudioListenerCount = 0;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    camera.enabled = false;
                    disabledCameraCount++;
                }
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                {
                    listener.enabled = false;
                    disabledAudioListenerCount++;
                }
            }
            AddEvent("入口壳初始化：禁用 Camera=" + disabledCameraCount + "，AudioListener=" + disabledAudioListenerCount + "。");
        }

        private void Start()
        {
            AddEvent("启动验收控制器，准备读取 GameCoreTable。");
            EnterLevelAsync(0, this.GetCancellationTokenOnDestroy()).Forget();
        }

        public async UniTask EnterLevelAsync(int targetIndex, CancellationToken token)
        {
            if (busy) return;
            if (!ESLevelAssetValidationGameCoreTable.TryGet(out ESLevelAssetValidationGameCore gameCore))
            {
                report = "[FAIL] GameCoreTable 未注入。请先下载并预热包含 ESLevelAssetValidationGameCore 的 Consumer。";
                AddEvent(report);
                return;
            }
            if (targetIndex < 0 || targetIndex >= gameCore.levels.Count)
            {
                report = "[FAIL] 目标关卡不存在：" + targetIndex;
                AddEvent(report);
                return;
            }

            busy = true;
            int currentOperation = ++operationId;
            AddEvent("开始进入关卡 #" + (targetIndex + 1) + "（操作 " + currentOperation + "）。");
            try
            {
                await LeaveActiveLevelAsync(token);
                activeLevel = gameCore.levels[targetIndex];
                if (activeLevel.resourcePlan != null && ESGameManager.ResourcePlans != null)
                {
                    ESResourcePlanReport planReport = await activeLevel.resourcePlan.PrepareAsync(token);
                    lastPlanReport = planReport;
                    AddPlanEvent("资源计划完成", planReport);
                    if (planReport.RequiredFailureCount > 0)
                        throw new InvalidOperationException("资源计划存在必需资源失败：" + planReport.RequiredFailureCount);
                }
                if (activeLevel.scene == null || !activeLevel.scene.IsValid)
                    throw new InvalidOperationException("关卡场景未配置。");
                activeScene = await activeLevel.scene.LoadAsync(LoadSceneMode.Additive, token);
                hasActiveScene = true;
                SceneManager.SetActiveScene(activeScene.Scene);
                report = "[PASS] 进入 " + activeLevel.levelName + "：已加载独立场景和本关 ResourcePlan。";
                AddEvent(report + " Scene=" + activeScene.Scene.name + ", Valid=" + activeScene.Scene.IsValid()
                    + ", AssetId=" + activeLevel.scene.AssetIdentity);
            }
            catch (OperationCanceledException)
            {
                report = "[CANCELED] 进入关卡等待已取消。";
                AddEvent(report);
            }
            catch (Exception exception)
            {
                report = "[FAIL] 进入关卡失败：" + exception.Message;
                AddEvent(report);
                Debug.LogException(exception, this);
            }
            finally { busy = false; }
        }

        private async UniTask LeaveActiveLevelAsync(CancellationToken token)
        {
            if (activeLevel == null) return;
            string leavingLevelName = activeLevel.levelName;
            AddEvent("开始离开关卡：" + leavingLevelName + "。");
            // SceneManager cannot cancel an unload AsyncOperation once it has been created.
            // Teardown therefore always runs to quiescence with CancellationToken.None;
            // the caller's token is checked only after the scene lease, Plan and safe point
            // have been settled. Releasing any of those earlier would race the Unity unload.
            if (hasActiveScene && activeScene.Scene.IsValid() && activeScene.Scene.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(activeScene.Scene);
                if (unload == null)
                    throw new InvalidOperationException("Unity 未能创建验收场景卸载任务：" + activeScene.Scene.name);
                await unload.ToUniTask();
            }
            if (hasActiveScene)
            {
                activeScene.Dispose();
                hasActiveScene = false;
            }
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            if (activeLevel.resourcePlan != null && ESGameManager.ResourcePlans != null)
            {
                lastPlanReport = await activeLevel.resourcePlan.ReleaseAsync(CancellationToken.None);
                AddPlanEvent("资源计划释放完成", lastPlanReport);
                if (lastPlanReport.State == ESResourcePlanState.Failed)
                    throw new InvalidOperationException("资源计划释放失败：" + leavingLevelName);
            }
            ESRuntimeUnusedAssetBundleUnloadResult result = await ESAssets.UnloadReleasedAssetBundlesAtSafePointAsync(CancellationToken.None);
            lastUnloadResult = result;
            hasUnloadResult = true;
            report = "[PASS] 离开 " + leavingLevelName + "：卸载 AB=" + result.UnloadedAssetBundleCount
                + "，清除对象缓存=" + result.EvictedCachedAssetCount + "。";
            AddEvent(report);
            activeLevel = null;
            if (token.IsCancellationRequested)
            {
                report = "[CANCELED] 已完成离开 " + leavingLevelName + " 的实际清理，但调用方等待已取消。";
                AddEvent(report);
                throw new OperationCanceledException(token);
            }
        }

        private void OnGUI()
        {
            float width = Mathf.Min(860f, Screen.width - 40f);
            float height = Mathf.Min(700f, Screen.height - 40f);
            GUILayout.BeginArea(new Rect(20f, 20f, width, height), GUI.skin.box);
            GUILayout.Label("ES 关卡资源卸载验收：方块 / 球体 / 圆柱");
            GUILayout.Label(LogTag + "  " + (busy ? "BUSY（任务执行中）" : "READY"));
            GUILayout.Label("当前关卡：" + (activeLevel == null ? "无" : activeLevel.levelName));
            bool gameCoreReady = ESLevelAssetValidationGameCoreTable.TryGet(out _);
            bool loadingReady = ESGameManager.RuntimeData != null && ESGameManager.RuntimeData.AssetLoadingService != null
                && ESGameManager.RuntimeData.AssetLoadingService.IsInitialized;
            GUILayout.Label("GameCoreTable：" + (gameCoreReady ? "已注入" : "未注入")
                + "    AssetLoadingService：" + (loadingReady ? "已初始化" : "未初始化"));
            GUILayout.Label("Scene：" + (hasActiveScene && activeScene.Scene.IsValid() && activeScene.Scene.isLoaded
                ? activeScene.Scene.name + "（Loaded）" : "未加载"));
            GUI.enabled = !busy;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("方块关")) EnterLevelAsync(0, this.GetCancellationTokenOnDestroy()).Forget();
            if (GUILayout.Button("球体关")) EnterLevelAsync(1, this.GetCancellationTokenOnDestroy()).Forget();
            if (GUILayout.Button("圆柱关")) EnterLevelAsync(2, this.GetCancellationTokenOnDestroy()).Forget();
            if (GUILayout.Button("重进当前关"))
            {
                int index = GetActiveLevelIndex();
                if (index >= 0) EnterLevelAsync(index, this.GetCancellationTokenOnDestroy()).Forget();
            }
            if (GUILayout.Button("离开当前关")) LeaveCurrentLevelAsync().Forget();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("安全点卸载")) UnloadAtSafePointAsync().Forget();
            if (GUILayout.Button("复制完整报告")) GUIUtility.systemCopyBuffer = BuildFullReport();
            GUILayout.EndHorizontal();
            DrawPlanSummary();
            scroll = GUILayout.BeginScrollView(scroll, GUI.skin.box);
            GUILayout.Label(report);
            for (int i = 0; i < eventLog.Count; i++) GUILayout.Label(eventLog[i]);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private int GetActiveLevelIndex()
        {
            if (!ESLevelAssetValidationGameCoreTable.TryGet(out ESLevelAssetValidationGameCore gameCore) || activeLevel == null) return -1;
            return gameCore.levels.IndexOf(activeLevel);
        }

        private async UniTaskVoid LeaveCurrentLevelAsync()
        {
            if (busy || activeLevel == null) return;
            busy = true;
            try { await LeaveActiveLevelAsync(this.GetCancellationTokenOnDestroy()); }
            catch (OperationCanceledException)
            {
                report = "[CANCELED] 离开关卡等待已取消。";
                AddEvent(report);
            }
            catch (Exception exception) { report = "[FAIL] 离开关卡失败：" + exception.Message; AddEvent(report); Debug.LogException(exception, this); }
            finally { busy = false; }
        }

        private async UniTaskVoid UnloadAtSafePointAsync()
        {
            if (busy) return;
            busy = true;
            try
            {
                lastUnloadResult = await ESAssets.UnloadReleasedAssetBundlesAtSafePointAsync(this.GetCancellationTokenOnDestroy());
                hasUnloadResult = true;
                report = "[PASS] 安全点卸载完成：AB=" + lastUnloadResult.UnloadedAssetBundleCount
                    + "，对象缓存=" + lastUnloadResult.EvictedCachedAssetCount + "。";
                AddEvent(report);
            }
            catch (Exception exception) { report = "[FAIL] 安全点卸载失败：" + exception.Message; AddEvent(report); Debug.LogException(exception, this); }
            finally { busy = false; }
        }

        private void DrawPlanSummary()
        {
            if (lastPlanReport == null) { GUILayout.Label("最近资源计划：暂无"); return; }
            GUILayout.Label("最近资源计划：" + lastPlanReport.State + " | 总数 " + lastPlanReport.TotalCount
                + " | 成功 " + lastPlanReport.SuccessCount + " | 失败 " + lastPlanReport.FailureCount
                + " | 必需失败 " + lastPlanReport.RequiredFailureCount + " | 可选待定 " + lastPlanReport.OptionalPendingCount
                + " | 保留 " + lastPlanReport.RetainCount + " | 后台完成 " + lastPlanReport.IsBackgroundComplete);
            if (hasUnloadResult)
                GUILayout.Label("最近安全卸载：AB " + lastUnloadResult.UnloadedAssetBundleCount
                    + " | 对象缓存 " + lastUnloadResult.EvictedCachedAssetCount);
            if (lastPlanReport.Errors == null) return;
            foreach (ESResourcePlanError error in lastPlanReport.Errors)
                GUILayout.Label("  [" + (error.Required ? "Required" : "Optional") + "][" + error.Kind + "] " + error.Key + "：" + error.Message);
        }

        private void AddPlanEvent(string prefix, ESResourcePlanReport planReport)
        {
            if (planReport == null) { AddEvent(prefix + "：无报告"); return; }
            AddEvent(prefix + "：State=" + planReport.State + ", Total=" + planReport.TotalCount
                + ", Success=" + planReport.SuccessCount + ", Failure=" + planReport.FailureCount
                + ", RequiredFailure=" + planReport.RequiredFailureCount + ", OptionalPending=" + planReport.OptionalPendingCount
                + ", Retain=" + planReport.RetainCount);
            foreach (ESResourcePlanError error in planReport.Errors)
                AddEvent("  [" + (error.Required ? "Required" : "Optional") + "][" + error.Kind + "][" + error.Key + "] " + error.Message);
        }

        private void AddEvent(string message)
        {
            string line = DateTime.Now.ToString("HH:mm:ss.fff") + " " + message;
            if (eventLog.Count >= MaxEventLogCount) eventLog.RemoveAt(0);
            eventLog.Add(line);
            Debug.Log(LogTag + " " + message, this);
        }

        private string BuildFullReport()
        {
            var builder = new StringBuilder(4096);
            builder.AppendLine(LogTag + " FULL REPORT");
            builder.AppendLine("CurrentLevel=" + (activeLevel == null ? "<none>" : activeLevel.levelName));
            if (activeLevel != null && activeLevel.scene != null) builder.AppendLine("SceneAssetId=" + activeLevel.scene.AssetIdentity);
            builder.AppendLine("Busy=" + busy + ", GameCoreTable=" + ESLevelAssetValidationGameCoreTable.TryGet(out _));
            bool loadingReady = ESGameManager.RuntimeData != null && ESGameManager.RuntimeData.AssetLoadingService.IsInitialized;
            builder.AppendLine("AssetLoadingService=" + loadingReady + ", SceneValid=" + (hasActiveScene && activeScene.Scene.IsValid())
                + ", SceneLoaded=" + (hasActiveScene && activeScene.Scene.IsValid() && activeScene.Scene.isLoaded));
            builder.AppendLine(report);
            if (lastPlanReport != null)
            {
                builder.AppendLine("Plan=" + lastPlanReport.State + ", Total=" + lastPlanReport.TotalCount + ", Success=" + lastPlanReport.SuccessCount + ", Failure=" + lastPlanReport.FailureCount + ", RequiredFailure=" + lastPlanReport.RequiredFailureCount + ", OptionalPending=" + lastPlanReport.OptionalPendingCount + ", Retain=" + lastPlanReport.RetainCount + ", BackgroundComplete=" + lastPlanReport.IsBackgroundComplete);
                foreach (ESResourcePlanError error in lastPlanReport.Errors)
                    builder.AppendLine("PlanError=[" + (error.Required ? "Required" : "Optional") + "][" + error.Kind + "][" + error.Key + "] " + error.Message);
            }
            if (hasUnloadResult) builder.AppendLine("UnloadBundles=" + lastUnloadResult.UnloadedAssetBundleCount + ", EvictedAssets=" + lastUnloadResult.EvictedCachedAssetCount);
            foreach (string line in eventLog) builder.AppendLine(line);
            return builder.ToString();
        }
    }
}
