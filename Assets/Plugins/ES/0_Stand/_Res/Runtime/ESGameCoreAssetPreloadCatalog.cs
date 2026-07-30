using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ES
{
    [Serializable]
    public sealed class ESGameCoreAssetPreloadReport
    {
        public int requestedCount;
        public int loadedCount;
        public int skippedCount;
        public int failedCount;
        public List<string> errors = new List<string>();
        public bool Success => failedCount == 0;
    }

    /// <summary>
    /// 项目启动期的核心资产预热清单。预热结果进入 ESAssets 全局驻留缓存，
    /// 不会因技能、UI 或 Owner 生命周期被卸载；只随资源安全点统一清理。
    /// </summary>
    [CreateAssetMenu(fileName = "ESGameCoreAssetPreloadCatalog", menuName = "【ES】/资源与发布/运行时配置/GameCore 预加载目录")]
    public sealed class ESGameCoreAssetPreloadCatalog : ScriptableObject
    {
        [SerializeReference] public List<ESAssetReferBase> assets = new List<ESAssetReferBase>();
        [SerializeReference, HideInInspector] public List<ESAssetReferBase> generatedAssets = new List<ESAssetReferBase>();
        [Tooltip("任意核心资源预热失败时是否立即中断启动。")]
        public bool failFast = true;

        public async UniTask<ESGameCoreAssetPreloadReport> PreloadAsync(CancellationToken cancellationToken = default)
        {
            var report = new ESGameCoreAssetPreloadReport();
            foreach (ESAssetReferBase refer in EnumerateAssets())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (refer == null || !refer.IsValid || !refer.SupportsGameCorePreload)
                {
                    report.skippedCount++;
                    continue;
                }

                report.requestedCount++;
                try
                {
                    await refer.PreloadAsync(cancellationToken);
                    report.loadedCount++;
                }
                catch (Exception exception)
                {
                    report.failedCount++;
                    report.errors.Add(refer.AssetBaseType.Name + " / " + refer.GUID + " : " + exception.Message);
                    if (failFast) throw;
                }
            }
            return report;
        }

        public void ReplaceGeneratedAssets(IEnumerable<ESAssetReferBase> values)
        {
            generatedAssets.Clear();
            var identities = new HashSet<ESAssetIdentity>();
            foreach (ESAssetReferBase refer in values ?? Array.Empty<ESAssetReferBase>())
                if (refer != null && refer.IsValid && identities.Add(refer.AssetIdentity))
                    generatedAssets.Add(refer);
        }

        private IEnumerable<ESAssetReferBase> EnumerateAssets()
        {
            var identities = new HashSet<ESAssetIdentity>();
            foreach (ESAssetReferBase refer in assets)
                if (refer != null && identities.Add(refer.AssetIdentity))
                    yield return refer;
            foreach (ESAssetReferBase refer in generatedAssets)
                if (refer != null && identities.Add(refer.AssetIdentity))
                    yield return refer;
        }
    }
}
