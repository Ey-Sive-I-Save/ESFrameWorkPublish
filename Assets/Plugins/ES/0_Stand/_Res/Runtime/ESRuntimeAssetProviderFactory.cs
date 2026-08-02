using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace ES
{
    /// <summary>
    /// 资源加载模式在本次进程初始化时冻结。运行中即使编辑器修改了 Setting，资源链也不会悄悄切换后端。
    /// Domain Reload / 新进程会自然创建下一次会话。
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static class ESAssetRunModeSession
    {
        private static readonly object SyncRoot = new object();
        private static bool isLocked;
        private static ESAssetRunMode lockedMode;
        private static ESAssetRunMode lockedConfiguredMode;

        public static bool IsLocked
        {
            get { lock (SyncRoot) return isLocked; }
        }

        public static ESAssetRunMode Lock(ESGlobalResSetting settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            lock (SyncRoot)
            {
                if (!isLocked)
                {
                    lockedConfiguredMode = settings.AssetRunMode;
                    lockedMode = ResolveRuntimeMode(settings);
                    isLocked = true;
                }
                else if (lockedConfiguredMode != settings.AssetRunMode)
                {
                    throw new InvalidOperationException($"资源加载模式已在本次初始化锁定为 {lockedConfiguredMode}（本次有效模式为 {lockedMode}），不能切换到 {settings.AssetRunMode}。请重启运行环境后再切换。");
                }

                return lockedMode;
            }
        }

#if UNITY_EDITOR
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void ResetAfterEditorSession()
        {
            lock (SyncRoot)
            {
                if (ESAssets.IsReady)
                    throw new InvalidOperationException("资源 Provider 仍处于 Ready 状态，不能重置 Editor RunMode 会话。");
                isLocked = false;
                lockedMode = default;
                lockedConfiguredMode = default;
            }
        }
#endif

        private static ESAssetRunMode ResolveRuntimeMode(ESGlobalResSetting settings)
        {
#if UNITY_EDITOR
            if (settings.AssetRunMode == ESAssetRunMode.LocalBuild)
            {
                if (!HasBasicLocalEditorReleaseEntry(settings))
                {
                    const string message = "当前资源方案需要本地构建内容，但未找到基础本地发布入口文件（Root Manifest / Bundle Index）。";
                    Debug.LogError("[ESRes][RunMode] " + message);
                    if (Application.isBatchMode)
                        throw new InvalidOperationException(message + " 批处理模式禁止回退到 EditorDirect，以避免 CI 假通过。");
                    EditorUtility.DisplayDialog("ES 资源方案回退", message + "\n\n编辑器本次会话将临时回退到 EditorDirect；配置资产不会被修改。需要验证构建链时请先完成 LocalBuild。", "确定");
                    return ESAssetRunMode.EditorDirect;
                }
            }
#endif
#if !UNITY_EDITOR
            if (settings.AssetRunMode == ESAssetRunMode.EditorDirect || settings.AssetRunMode == ESAssetRunMode.EditorSimulateBuild)
            {
                Debug.LogWarning("[ESRes] Player 不支持 " + settings.AssetRunMode + "，已自动升级为 LocalBuild。");
                settings.AssetRunMode = ESAssetRunMode.LocalBuild;
            }
#endif
            return settings.AssetRunMode;
        }

#if UNITY_EDITOR
        private static bool HasBasicLocalEditorReleaseEntry(ESGlobalResSetting settings)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.Path_LocalBuildPlatform))
                return false;

            string platformRoot = Path.GetFullPath(settings.Path_LocalBuildPlatform);
            string rootManifest = Path.Combine(platformRoot, "ESAssetReleaseManifest.json");
            string bundleIndex = Path.Combine(platformRoot, "ESAssetReleaseBundleIndex.json");
            return Directory.Exists(platformRoot)
                && File.Exists(rootManifest)
                && File.Exists(bundleIndex)
                && new FileInfo(rootManifest).Length > 0
                && new FileInfo(bundleIndex).Length > 0;
        }
#endif
    }

    internal abstract class ESRuntimeAssetProviderBase : IESAssetRuntimeProvider, IESRuntimeAssetOperationTracker
    {
        private readonly ESRuntimeAssetLoader loader;

        protected ESRuntimeAssetProviderBase(ESRuntimeAssetLoader runtimeLoader)
        {
            loader = runtimeLoader ?? throw new ArgumentNullException(nameof(runtimeLoader));
        }

        public UniTask<ESRuntimeAssetHandle<T>> LoadMainAssetAsync<T>(ESAssetIdentity id, System.Threading.CancellationToken cancellationToken = default) where T : UnityEngine.Object
            => loader.LoadMainAssetAsync<T>(id, cancellationToken);
        public UniTask<ESRuntimeAssetHandle<T>> LoadSubAssetAsync<T>(ESAssetIdentity id, System.Threading.CancellationToken cancellationToken = default) where T : UnityEngine.Object
            => loader.LoadSubAssetAsync<T>(id, cancellationToken);
        public UniTask<ESRuntimeSceneHandle> LoadSceneAsync(ESAssetIdentity id, LoadSceneMode mode = LoadSceneMode.Single, System.Threading.CancellationToken cancellationToken = default)
            => loader.LoadSceneAsync(id, mode, cancellationToken);
        public bool TryGetLoaded<T>(ESAssetIdentity id, out T asset) where T : UnityEngine.Object => loader.TryGetLoaded(id, out asset);
        public bool TryGetStatus(ESAssetIdentity id, out ESRuntimeAssetLoadStatus status) => loader.TryGetStatus(id, out status);
        public void Release(ESAssetIdentity id) => loader.Release(id);
        public UniTask<ESRuntimeUnusedAssetUnloadResult> UnloadZeroReferenceAssetsAsync(System.Threading.CancellationToken cancellationToken = default)
            => loader.UnloadZeroReferenceAssetsAsync(cancellationToken);
        public UniTask<ESRuntimeUnusedAssetBundleUnloadResult> UnloadZeroReferenceAssetBundlesAtSafePointAsync(System.Threading.CancellationToken cancellationToken = default)
            => loader.UnloadZeroReferenceAssetBundlesAtSafePointAsync(cancellationToken);
        public void UnloadAllAtSafePoint() => loader.UnloadAllAtSafePoint();
        public void Dispose() => loader.Dispose();
        public bool HasPendingOperations => loader.HasPendingOperations;
        public UniTask WaitForPendingOperationsAsync(System.Threading.CancellationToken cancellationToken = default)
            => loader.WaitForPendingOperationsAsync(cancellationToken);
    }

    internal sealed class ESRuntimeAssetBundleRuntimeProvider : ESRuntimeAssetProviderBase
    {
        public ESRuntimeAssetBundleRuntimeProvider(ESGlobalAssetRuntimeMap runtimeMap, ESRuntimeRetryPolicy retryPolicy, bool allowRemoteFallback)
            : base(new ESRuntimeAssetLoader(runtimeMap, new ESRuntimeAssetBundleProvider(allowRemoteFallback), retryPolicy)) { }
    }

#if UNITY_EDITOR
    internal sealed class ESRuntimeEditorDirectRuntimeProvider : ESRuntimeAssetProviderBase
    {
        public ESRuntimeEditorDirectRuntimeProvider(ESGlobalAssetRuntimeMap runtimeMap, ESRuntimeRetryPolicy retryPolicy, bool requireRuntimeMapIdentity = false)
            : base(new ESRuntimeAssetLoader(runtimeMap, null, retryPolicy, new ESRuntimeEditorDirectAssetProvider(), requireRuntimeMapIdentity)) { }
    }
#endif

    /// <summary>框架装配入口。业务代码使用 ESAssetRefer 或 ResourcePlan，不直接创建 Provider。</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static class ESAssetRuntimeProviderFactory
    {
        public static IESAssetRuntimeProvider Create(ESGlobalAssetRuntimeMap runtimeMap, ESGlobalResSetting settings, ESRuntimeRetryPolicy retryPolicy)
        {
            if (runtimeMap == null) throw new ArgumentNullException(nameof(runtimeMap));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            ESAssetRunMode runMode = ESAssetRunModeSession.Lock(settings);
            switch (runMode)
            {
                case ESAssetRunMode.EditorDirect:
#if UNITY_EDITOR
                    return new ESRuntimeEditorDirectRuntimeProvider(runtimeMap, retryPolicy);
#else
                    throw new PlatformNotSupportedException("EditorDirect is only available in the Unity Editor.");
#endif
                case ESAssetRunMode.EditorSimulateBuild:
#if UNITY_EDITOR
                    return new ESRuntimeEditorDirectRuntimeProvider(runtimeMap, retryPolicy, true);
#else
                    throw new PlatformNotSupportedException("EditorSimulateBuild is only available in the Unity Editor.");
#endif
                case ESAssetRunMode.LocalBuild:
                    return new ESRuntimeAssetBundleRuntimeProvider(runtimeMap, retryPolicy, false);
                case ESAssetRunMode.HotUpdate:
                    return new ESRuntimeAssetBundleRuntimeProvider(runtimeMap, retryPolicy, true);
                default:
                    throw new ArgumentOutOfRangeException(nameof(runMode), runMode, "Unsupported ES asset run mode.");
            }
        }
    }

}
