using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ES
{
    internal static class ESRuntimeDataBootstrapBridge
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            EnsureRegistered();
        }

        // HybridCLR 程序集是在 Player 启动阶段之后动态载入的，Unity 不保证此时再次派发
        // RuntimeInitializeOnLoadMethod。由 Stand 启动层在 Assembly.Load 后显式调用该入口。
        public static void EnsureRegistered()
        {
            ESResBootstrapRuntimeBridge.Register(InitializeAsync);
        }

        private static async UniTask InitializeAsync(ESGlobalResSetting settings, ESRuntimeReleaseDownloadResult result, CancellationToken cancellationToken)
        {
            if (ESGameManager.RuntimeData == null && ESGameManager.Instance == null)
                new GameObject("ESGameManager", typeof(ESGameManager));
            ESRuntimeDataModule runtimeData = ESGameManager.RuntimeData;
            if (runtimeData == null) throw new InvalidOperationException("ESGameManager 未能创建 ESRuntimeDataModule。");
            await runtimeData.InitializeAssetLoadingFromReleaseResultAsync(settings, result, cancellationToken);
        }
    }
}
