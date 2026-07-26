using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    /// <summary>
    /// 资源加载模式在本次进程初始化时冻结。运行中即使编辑器修改了 Setting，资源链也不会悄悄切换后端。
    /// Domain Reload / 新进程会自然创建下一次会话。
    /// </summary>
    public static class ESAssetRunModeSession
    {
        private static readonly object SyncRoot = new object();
        private static bool isLocked;
        private static ESAssetRunMode lockedMode;

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
                    lockedMode = ResolveRuntimeMode(settings);
                    isLocked = true;
                }
                else if (lockedMode != settings.AssetRunMode)
                {
                    throw new InvalidOperationException($"资源加载模式已在本次初始化锁定为 {lockedMode}，不能切换到 {settings.AssetRunMode}。请重启运行环境后再切换。");
                }

                return lockedMode;
            }
        }

        private static ESAssetRunMode ResolveRuntimeMode(ESGlobalResSetting settings)
        {
#if !UNITY_EDITOR
            if (settings.AssetRunMode == ESAssetRunMode.EditorDirect || settings.AssetRunMode == ESAssetRunMode.EditorSimulateBuild)
            {
                Debug.LogWarning("[ESRes] Player 不支持 " + settings.AssetRunMode + "，已自动升级为 LocalBuild。");
                settings.AssetRunMode = ESAssetRunMode.LocalBuild;
            }
#endif
            return settings.AssetRunMode;
        }
    }

    internal abstract class ESRuntimeAssetProviderBase : IESAssetRuntimeProvider
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
    }

    internal sealed class ESRuntimeAssetBundleRuntimeProvider : ESRuntimeAssetProviderBase
    {
        public ESRuntimeAssetBundleRuntimeProvider(ESGlobalAssetRuntimeMap runtimeMap, ESRuntimeRetryPolicy retryPolicy, bool allowRemoteFallback)
            : base(new ESRuntimeAssetLoader(runtimeMap, new ESRuntimeAssetBundleProvider(allowRemoteFallback), retryPolicy)) { }
    }

#if UNITY_EDITOR
    internal sealed class ESRuntimeEditorDirectRuntimeProvider : ESRuntimeAssetProviderBase
    {
        public ESRuntimeEditorDirectRuntimeProvider(ESGlobalAssetRuntimeMap runtimeMap, ESRuntimeRetryPolicy retryPolicy)
            : base(new ESRuntimeAssetLoader(runtimeMap, null, retryPolicy, new ESRuntimeEditorDirectAssetProvider())) { }
    }
#endif

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
                case ESAssetRunMode.LocalBuild:
                    return new ESRuntimeAssetBundleRuntimeProvider(runtimeMap, retryPolicy, false);
                case ESAssetRunMode.HotUpdate:
                    return new ESRuntimeAssetBundleRuntimeProvider(runtimeMap, retryPolicy, true);
                default:
                    throw new ArgumentOutOfRangeException(nameof(runMode), runMode, "Unsupported ES asset run mode.");
            }
        }
    }

    /// <summary>兼容旧的新版调用点；新代码请使用 ESAssetRuntimeProviderFactory。</summary>
    [Obsolete("Use ESAssetRuntimeProviderFactory.")]
    public static class ESRuntimeAssetProviderFactory
    {
        public static IESAssetRuntimeProvider Create(ESGlobalAssetRuntimeMap runtimeMap, ESGlobalResSetting settings, ESRuntimeRetryPolicy retryPolicy)
            => ESAssetRuntimeProviderFactory.Create(runtimeMap, settings, retryPolicy);
    }
}
