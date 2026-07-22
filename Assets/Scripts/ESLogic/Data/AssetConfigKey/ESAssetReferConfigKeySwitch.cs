using System;
using UnityEngine;

namespace ES
{
    public interface IESAssetReferConfigData
    {
        UnityEngine.Object LoadedAssetObject { get; }
        bool LoadedAssetReady { get; }
        bool HasLoadedAsset { get; }
        void ClearLoadedAsset();
        void ResetRuntimeAssetState();
        void CopyRuntimeAssetStateFrom(IESAssetReferConfigData source);
    }

    [Serializable]
    public abstract class ESAssetReferConfigDataBase<TAsset> : IESAssetReferConfigData where TAsset : UnityEngine.Object
    {
        [NonSerialized] public TAsset loadedAsset;
        [NonSerialized] public bool loadedAssetReady;

        public TAsset LoadedAsset => loadedAsset;
        public UnityEngine.Object LoadedAssetObject => loadedAsset;
        public bool LoadedAssetReady => loadedAssetReady;
        public bool HasLoadedAsset => loadedAssetReady;

        public void SetLoadedAsset(TAsset asset)
        {
            loadedAsset = asset;
            loadedAssetReady = asset != null;
        }

        public void ClearLoadedAsset()
        {
            loadedAsset = null;
            loadedAssetReady = false;
        }

        public void ResetRuntimeAssetState()
        {
            ClearLoadedAsset();
        }

        public void CopyRuntimeAssetStateFrom(IESAssetReferConfigData source)
        {
            if (source == null)
                return;

            loadedAsset = source.LoadedAssetObject as TAsset;
            loadedAssetReady = source.LoadedAssetReady && loadedAsset != null;
        }
    }

    /// <summary>
    /// ESAssetRefer 配置键转换工具。
    /// 只负责类型和键值转换，不负责加载资源。
    /// </summary>
    public static class ESAssetReferConfigKeySwitch
    {
        public static ESAssetCategory ToCategory(ESAssetReferKind kind)
        {
            switch (kind)
            {
                case ESAssetReferKind.Prefab: return ESAssetCategory.Prefab;
                case ESAssetReferKind.Scene: return ESAssetCategory.Scene;
                case ESAssetReferKind.Sprite: return ESAssetCategory.Sprite;
                case ESAssetReferKind.SpriteAtlas: return ESAssetCategory.SpriteAtlas;
                case ESAssetReferKind.Texture2D:
                case ESAssetReferKind.Texture: return ESAssetCategory.Texture;
                case ESAssetReferKind.Material: return ESAssetCategory.Material;
                case ESAssetReferKind.Mesh: return ESAssetCategory.Mesh;
                case ESAssetReferKind.AnimationClip: return ESAssetCategory.Animation;
                case ESAssetReferKind.AnimatorController: return ESAssetCategory.AnimatorController;
                case ESAssetReferKind.Avatar: return ESAssetCategory.Avatar;
                case ESAssetReferKind.AudioClip: return ESAssetCategory.Audio;
                case ESAssetReferKind.VideoClip: return ESAssetCategory.Video;
                case ESAssetReferKind.TimelineAsset: return ESAssetCategory.Timeline;
                case ESAssetReferKind.PlayableAsset: return ESAssetCategory.Playable;
                case ESAssetReferKind.TerrainData: return ESAssetCategory.TerrainData;
                default: return ESAssetCategory.Other;
            }
        }

        public static string GetConfigKeyTypeName(ESAssetReferKind kind)
        {
            switch (kind)
            {
                case ESAssetReferKind.Prefab: return nameof(ESAssetReferPrefabConfigKey);
                case ESAssetReferKind.Scene: return nameof(ESAssetReferSceneConfigKey);
                case ESAssetReferKind.Sprite: return nameof(ESAssetReferSpriteConfigKey);
                case ESAssetReferKind.SpriteAtlas: return nameof(ESAssetReferSpriteAtlasConfigKey);
                case ESAssetReferKind.Texture2D: return nameof(ESAssetReferTexture2DConfigKey);
                case ESAssetReferKind.Texture: return nameof(ESAssetReferTextureConfigKey);
                case ESAssetReferKind.Material: return nameof(ESAssetReferMaterialConfigKey);
                case ESAssetReferKind.Mesh: return nameof(ESAssetReferMeshConfigKey);
                case ESAssetReferKind.AnimationClip: return nameof(ESAssetReferAnimationClipConfigKey);
                case ESAssetReferKind.AnimatorController: return nameof(ESAssetReferAnimatorControllerConfigKey);
                case ESAssetReferKind.Avatar: return nameof(ESAssetReferAvatarConfigKey);
                case ESAssetReferKind.AudioClip: return nameof(ESAssetReferAudioClipConfigKey);
                case ESAssetReferKind.VideoClip: return nameof(ESAssetReferVideoClipConfigKey);
                case ESAssetReferKind.TimelineAsset: return nameof(ESAssetReferTimelineAssetConfigKey);
                case ESAssetReferKind.PlayableAsset: return nameof(ESAssetReferPlayableAssetConfigKey);
                case ESAssetReferKind.TerrainData: return nameof(ESAssetReferTerrainDataConfigKey);
                default: return string.Empty;
            }
        }

        public static string GetConfigDataTypeName(ESAssetReferKind kind)
        {
            switch (kind)
            {
                case ESAssetReferKind.Prefab: return nameof(ESAssetReferPrefabConfigData);
                case ESAssetReferKind.Scene: return nameof(ESAssetReferSceneConfigData);
                case ESAssetReferKind.Sprite: return nameof(ESAssetReferSpriteConfigData);
                case ESAssetReferKind.SpriteAtlas: return nameof(ESAssetReferSpriteAtlasConfigData);
                case ESAssetReferKind.Texture2D: return nameof(ESAssetReferTexture2DConfigData);
                case ESAssetReferKind.Texture: return nameof(ESAssetReferTextureConfigData);
                case ESAssetReferKind.Material: return nameof(ESAssetReferMaterialConfigData);
                case ESAssetReferKind.Mesh: return nameof(ESAssetReferMeshConfigData);
                case ESAssetReferKind.AnimationClip: return nameof(ESAssetReferAnimationClipConfigData);
                case ESAssetReferKind.AnimatorController: return nameof(ESAssetReferAnimatorControllerConfigData);
                case ESAssetReferKind.Avatar: return nameof(ESAssetReferAvatarConfigData);
                case ESAssetReferKind.AudioClip: return nameof(ESAssetReferAudioClipConfigData);
                case ESAssetReferKind.VideoClip: return nameof(ESAssetReferVideoClipConfigData);
                case ESAssetReferKind.TimelineAsset: return nameof(ESAssetReferTimelineAssetConfigData);
                case ESAssetReferKind.PlayableAsset: return nameof(ESAssetReferPlayableAssetConfigData);
                case ESAssetReferKind.TerrainData: return nameof(ESAssetReferTerrainDataConfigData);
                default: return string.Empty;
            }
        }

        public static void ApplyPageKeyToResKey(ESAssetPage page, ESResKey resKey)
        {
            if (page == null || resKey == null)
            {
                return;
            }

            resKey.ConfigEnumKeyInt = page.EnumKey;
            resKey.ConfigStringKey = page.StringKey;

#if UNITY_EDITOR
            if (page.OB != null)
            {
                string assetPath = UnityEditor.AssetDatabase.GetAssetPath(page.OB);
                resKey.GUID = UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
                resKey.Path = assetPath;
            }
#endif
        }

        [Obsolete("Use ApplyPageKeyToResKey(ESAssetPage, ESResKey) instead.")]
        public static void ApplyPageKeyToResKey(ResPage page, ESResKey resKey)
        {
            ApplyPageKeyToResKey((ESAssetPage)page, resKey);
        }

        public static bool IsSupportedKind(ESAssetReferKind kind)
        {
            return kind != ESAssetReferKind.None &&
                   kind != ESAssetReferKind.Other &&
                   !string.IsNullOrEmpty(GetConfigKeyTypeName(kind));
        }
    }
}
