using System;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// RuntimeData 分类 AssetTable 的唯一装配点。
    /// Boot 在 Manifest 和当前 RunMode Provider 准备完成后调用 Initialize；业务层永远只访问 ESRuntimeDataAsset 的分类 Table。
    /// </summary>
    public sealed class ESRuntimeDataAssetLoadingService : IDisposable
    {
        private IESAssetRuntimeProvider runtimeProvider;
        private ESRuntimeAssetReferTableResolver assetReferTableResolver;
        private bool initialized;

        public bool IsInitialized => initialized;
        public IESAssetRuntimeProvider RuntimeProvider => runtimeProvider;

        public void Initialize(ESGlobalAssetRuntimeMap manifest, IESRuntimeAssetBundleProvider provider, ESRuntimeRetryPolicy retryPolicy)
        {
            Initialize(new ESRuntimeAssetLoader(manifest, provider, retryPolicy));
        }

        public void Initialize(IESAssetRuntimeProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            Dispose();
            runtimeProvider = provider;
            BindAllTables(runtimeProvider);
            assetReferTableResolver = new ESRuntimeAssetReferTableResolver(runtimeProvider);
            ESAssetReferTableResolver.Register(assetReferTableResolver);
            initialized = true;
        }


        public void Dispose()
        {
            if (!initialized && runtimeProvider == null)
                return;

            ESRuntimeDataAsset.ClearAllPendingAssetLoads();
            ESRuntimeDataAsset.ClearAllLoadedAssets();
            ESAssetReferTableResolver.Clear(assetReferTableResolver);
            runtimeProvider?.Dispose();
            runtimeProvider = null;
            initialized = false;
        }

        /// <summary>场景切换、回登录等资源安全点使用；运行中不会被普通 Refer/Owner 自动调用。</summary>
        public void UnloadAllAssetsAtSafePoint()
        {
            if (!initialized || runtimeProvider == null)
                return;

            ESRuntimeDataAsset.ClearAllPendingAssetLoads();
            ESRuntimeDataAsset.ClearAllLoadedAssets();
            ESAssets.UnloadAllAtSafePoint();
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
