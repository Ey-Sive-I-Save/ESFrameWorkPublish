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
