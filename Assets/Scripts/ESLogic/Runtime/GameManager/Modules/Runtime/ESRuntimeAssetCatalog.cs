using System;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Current typed-AssetTable catalog: resolves a stable ConfigKey to AssetIdentity.
    /// It is a catalog lookup only: it never loads, retains, releases, or owns an asset.
    /// All runtime asset and AssetBundle reference accounting belongs to ESAssets' runtime backend.
    /// </summary>
    public sealed class ESRuntimeAssetCatalog
    {
        public static ESRuntimeAssetCatalog Current { get; private set; }

        internal static void Activate(ESRuntimeAssetCatalog catalog) => Current = catalog ?? throw new ArgumentNullException(nameof(catalog));
        internal static void Deactivate(ESRuntimeAssetCatalog catalog)
        {
            if (ReferenceEquals(Current, catalog))
                Current = null;
        }

        public bool TryResolveAssetIdentity(ESAssetReferKind kind, int enumKey, string stringKey, out ESAssetIdentity identity)
        {
            switch (kind)
            {
                case ESAssetReferKind.Prefab: return TryResolveAssetIdentity(ESRuntimeDataAsset.Prefabs, enumKey, stringKey, out identity);
                case ESAssetReferKind.Scene: return TryResolveAssetIdentity(ESRuntimeDataAsset.Scenes, enumKey, stringKey, out identity);
                case ESAssetReferKind.Sprite: return TryResolveAssetIdentity(ESRuntimeDataAsset.Sprites, enumKey, stringKey, out identity);
                case ESAssetReferKind.AudioClip: return TryResolveAssetIdentity(ESRuntimeDataAsset.AudioClips, enumKey, stringKey, out identity);
                case ESAssetReferKind.AnimationClip: return TryResolveAssetIdentity(ESRuntimeDataAsset.AnimationClips, enumKey, stringKey, out identity);
                case ESAssetReferKind.AnimatorController: return TryResolveAssetIdentity(ESRuntimeDataAsset.AnimatorControllers, enumKey, stringKey, out identity);
                case ESAssetReferKind.Material: return TryResolveAssetIdentity(ESRuntimeDataAsset.Materials, enumKey, stringKey, out identity);
                case ESAssetReferKind.Mesh: return TryResolveAssetIdentity(ESRuntimeDataAsset.Meshes, enumKey, stringKey, out identity);
                case ESAssetReferKind.Texture: return TryResolveAssetIdentity(ESRuntimeDataAsset.Textures, enumKey, stringKey, out identity);
                case ESAssetReferKind.Texture2D: return TryResolveAssetIdentity(ESRuntimeDataAsset.Texture2Ds, enumKey, stringKey, out identity);
                case ESAssetReferKind.SpriteAtlas: return TryResolveAssetIdentity(ESRuntimeDataAsset.SpriteAtlases, enumKey, stringKey, out identity);
                case ESAssetReferKind.Avatar: return TryResolveAssetIdentity(ESRuntimeDataAsset.Avatars, enumKey, stringKey, out identity);
                case ESAssetReferKind.PlayableAsset: return TryResolveAssetIdentity(ESRuntimeDataAsset.PlayableAssets, enumKey, stringKey, out identity);
                case ESAssetReferKind.ScriptableObject: return TryResolveAssetIdentity(ESRuntimeDataAsset.ScriptableObjects, enumKey, stringKey, out identity);
                case ESAssetReferKind.TimelineAsset: return TryResolveAssetIdentity(ESRuntimeDataAsset.TimelineAssets, enumKey, stringKey, out identity);
                case ESAssetReferKind.VideoClip: return TryResolveAssetIdentity(ESRuntimeDataAsset.VideoClips, enumKey, stringKey, out identity);
                case ESAssetReferKind.TerrainData: return TryResolveAssetIdentity(ESRuntimeDataAsset.TerrainDatas, enumKey, stringKey, out identity);
                default:
                    identity = default;
                    return false;
            }
        }

        private static bool TryResolveAssetIdentity<TData, TAsset>(
            ESAssetConfigKeyTable<TData, TAsset> table, int enumKey, string stringKey, out ESAssetIdentity identity)
            where TData : ESAssetReferConfigDataBase<TAsset>
            where TAsset : UnityEngine.Object
        {
            identity = default;
            if (table == null || !table.TryGetRuntimeKey(enumKey, stringKey, out int tableKey))
                return false;

            if (!table.TryGet(tableKey, out TData configData) || configData == null)
                return false;

            identity = new ESAssetIdentity(configData.AssetGuid, configData.AssetLocalFileId);
            return identity.IsValid;
        }
    }
}
