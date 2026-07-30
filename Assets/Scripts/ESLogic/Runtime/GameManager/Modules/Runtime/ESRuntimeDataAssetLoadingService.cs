using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// RuntimeData 分类 AssetTable 的唯一装配点。
    /// Boot 在 Manifest 和当前 RunMode Provider 准备完成后调用 Initialize；业务层永远只访问 ESRuntimeDataAsset 的分类 Table。
    /// </summary>
    /// <summary>GameManager 的框架装配服务；业务不直接初始化或切换资源后端。</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public sealed class ESRuntimeDataAssetLoadingService : IDisposable
    {
        private IESAssetRuntimeProvider runtimeProvider;
        private ESRuntimeAssetCatalog assetCatalog;
        private bool initialized;

        public bool IsInitialized => initialized;
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public IESAssetRuntimeProvider RuntimeBackend => runtimeProvider;

        public void Initialize(ESGlobalAssetRuntimeMap manifest, IESRuntimeAssetBundleProvider provider, ESRuntimeRetryPolicy retryPolicy)
        {
            Initialize(new ESRuntimeAssetLoader(manifest, provider, retryPolicy));
        }

        public void Initialize(IESAssetRuntimeProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (ReferenceEquals(provider, runtimeProvider))
                return;
            if (runtimeProvider != null && ESAssets.HasPendingOperations)
                throw new InvalidOperationException("[ESRes][Provider] 当前仍有资源请求，运行时重初始化必须使用 InitializeAsync。");

            ESAssets.BeginProviderTransition();
            try
            {
                QuiesceScopesAndPlans(runtimeProvider != null);
                DisposeCurrentProviderCore();
                AttachProvider(provider);
            }
            catch
            {
                if (ReferenceEquals(provider, runtimeProvider))
                    DisposeCurrentProviderCore();
                else
                    provider.Dispose();
                throw;
            }
            finally
            {
                ESAssets.EndProviderTransition();
            }
        }

        /// <summary>
        /// 运行中切换 Catalog/Provider 的唯一安全入口：停止新请求，释放旧计划与 Scope，
        /// 等待旧 Provider 收尾后重置所有 AssetTable Loader，再装配新 Provider。
        /// </summary>
        public UniTask InitializeAsync(IESAssetRuntimeProvider provider, CancellationToken cancellationToken = default)
            => InitializeAsync(provider, null, cancellationToken);

        internal async UniTask InitializeAsync(IESAssetRuntimeProvider provider, Action rebuildTablesBeforeAttach, CancellationToken cancellationToken)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (ReferenceEquals(provider, runtimeProvider))
                return;

            ESAssets.BeginProviderTransition();
            try
            {
                QuiesceScopesAndPlans(runtimeProvider != null);
                if (runtimeProvider != null)
                    await ESAssets.WaitForPendingOperationsAsync(cancellationToken);
                DisposeCurrentProviderCore();
                rebuildTablesBeforeAttach?.Invoke();
                AttachProvider(provider);
            }
            catch
            {
                if (ReferenceEquals(provider, runtimeProvider))
                    DisposeCurrentProviderCore();
                else
                    provider.Dispose();
                throw;
            }
            finally
            {
                ESAssets.EndProviderTransition();
            }
        }

        public void Dispose()
        {
            if (!initialized && runtimeProvider == null)
                return;

            ESAssets.BeginProviderTransition();
            try
            {
                QuiesceScopesAndPlans(false);
                DisposeCurrentProviderCore();
            }
            finally
            {
                ESAssets.EndProviderTransition();
            }
        }

        /// <summary>同步兼容入口，仅用于已确认完全静默的 Bootstrap/退出阶段；场景切换请使用异步版本。</summary>
        public void UnloadAllAssetsAtSafePoint()
        {
            if (!initialized || runtimeProvider == null)
                return;
            if (ESAssets.HasPendingOperations)
                throw new InvalidOperationException("[ESRes][SafePoint] 当前仍有资源请求，请使用 UnloadAllAssetsAtSafePointAsync。");

            bool loadersReset = false;
            ESAssets.BeginProviderTransition();
            try
            {
                QuiesceScopesAndPlans(false);
                ESRuntimeDataAsset.ResetAllAssetLoaders();
                loadersReset = true;
                ESAssets.UnloadAllAtSafePoint();
            }
            finally
            {
                if (loadersReset && runtimeProvider != null)
                    BindAllTables(runtimeProvider);
                ESAssets.EndProviderTransition();
            }
        }

        /// <summary>推荐的全量资源安全点：先等待 Provider/Scope 收尾，再清理表缓存并卸载。</summary>
        public async UniTask UnloadAllAssetsAtSafePointAsync(CancellationToken cancellationToken = default)
        {
            if (!initialized || runtimeProvider == null)
                return;

            bool loadersReset = false;
            ESAssets.BeginProviderTransition();
            try
            {
                QuiesceScopesAndPlans(false);
                await ESAssets.WaitForPendingOperationsAsync(cancellationToken);
                ESRuntimeDataAsset.ResetAllAssetLoaders();
                loadersReset = true;
                await ESAssets.UnloadAllAtSafePointAsync(cancellationToken);
            }
            finally
            {
                if (loadersReset && runtimeProvider != null)
                    BindAllTables(runtimeProvider);
                ESAssets.EndProviderTransition();
            }
        }

        /// <summary>由 Consumer GameCore 重新注入完成后调用，恢复 Provider 切换前仍在进入状态的目标。</summary>
        internal UniTask RestoreResourcePlansAfterGameCoreAsync(CancellationToken cancellationToken = default)
        {
            ESResourcePlanRuntimeService plans = ESGameManager.ResourcePlans;
            return plans == null
                ? UniTask.CompletedTask
                : plans.RestoreAfterProviderTransitionAsync(cancellationToken);
        }

        private static void QuiesceScopesAndPlans(bool preserveTargetsForProviderTransition)
        {
            // GameCore 表保存的是 Consumer GameCore SO 的强引用。全量安全点或
            // Provider 切换必须先清表，再释放 Scope/Provider，不能留下旧对象。
            ESRuntimeDataGameCore.ResetForResourceTransition();
            ESResourcePlanRuntimeService plans = ESGameManager.ResourcePlans;
            if (preserveTargetsForProviderTransition)
                plans?.SuspendForProviderTransition();
            else
                plans?.Dispose();
            ESAssets.ResetScopesForProviderTransition();
        }

        private void DisposeCurrentProviderCore()
        {
            ESRuntimeDataAsset.ResetAllAssetLoaders();
            ESRuntimeAssetCatalog.Deactivate(assetCatalog);
            assetCatalog = null;
            ESAssets.DetachRuntimeBackend(runtimeProvider);
            runtimeProvider?.Dispose();
            runtimeProvider = null;
            initialized = false;
        }

        private void AttachProvider(IESAssetRuntimeProvider provider)
        {
            runtimeProvider = provider;
            BindAllTables(runtimeProvider);
            assetCatalog = new ESRuntimeAssetCatalog();
            ESRuntimeAssetCatalog.Activate(assetCatalog);
            ESAssets.AttachRuntimeBackend(runtimeProvider);
            initialized = true;
        }

        private static void BindAllTables(IESAssetRuntimeProvider provider)
        {
            ESRuntimeDataAsset.Prefabs.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferPrefabConfigData, GameObject>(provider));
            ESRuntimeDataAsset.Sprites.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferSpriteConfigData, Sprite>(provider));
            ESRuntimeDataAsset.AudioClips.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferAudioClipConfigData, AudioClip>(provider));
            ESRuntimeDataAsset.AnimationClips.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferAnimationClipConfigData, AnimationClip>(provider));
            ESRuntimeDataAsset.AnimatorControllers.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferAnimatorControllerConfigData, RuntimeAnimatorController>(provider));
            ESRuntimeDataAsset.Materials.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferMaterialConfigData, Material>(provider));
            ESRuntimeDataAsset.Meshes.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferMeshConfigData, Mesh>(provider));
            ESRuntimeDataAsset.Scenes.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferSceneConfigData, UnityEngine.Object>(provider));
            ESRuntimeDataAsset.Textures.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferTextureConfigData, Texture>(provider));
            ESRuntimeDataAsset.Texture2Ds.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferTexture2DConfigData, Texture2D>(provider));
            ESRuntimeDataAsset.SpriteAtlases.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferSpriteAtlasConfigData, UnityEngine.U2D.SpriteAtlas>(provider));
            ESRuntimeDataAsset.Avatars.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferAvatarConfigData, Avatar>(provider));
            ESRuntimeDataAsset.PlayableAssets.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferPlayableAssetConfigData, UnityEngine.Playables.PlayableAsset>(provider));
            ESRuntimeDataAsset.ScriptableObjects.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferScriptableObjectConfigData, ScriptableObject>(provider));
            ESRuntimeDataAsset.TimelineAssets.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferTimelineAssetConfigData, UnityEngine.Object>(provider));
            ESRuntimeDataAsset.VideoClips.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferVideoClipConfigData, UnityEngine.Video.VideoClip>(provider));
            ESRuntimeDataAsset.TerrainDatas.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferTerrainDataConfigData, TerrainData>(provider));
        }
    }
}
