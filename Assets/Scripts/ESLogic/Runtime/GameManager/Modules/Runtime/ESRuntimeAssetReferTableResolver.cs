using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ES
{
    /// <summary>把 ESAssetRefer 的已解析业务键桥接到类型化 AssetTable。</summary>
    public sealed class ESRuntimeAssetReferTableResolver : IESAssetReferTableResolver
    {
        public IESAssetRuntimeProvider RuntimeProvider { get; }
        private readonly struct ReferKey : IEquatable<ReferKey>
        {
            public readonly ESAssetReferKind Kind;
            public readonly int EnumKey;
            public readonly string StringKey;
            public ReferKey(ESAssetReferKind kind, int enumKey, string stringKey) { Kind = kind; EnumKey = enumKey; StringKey = stringKey ?? string.Empty; }
            public bool Equals(ReferKey other) => Kind == other.Kind && EnumKey == other.EnumKey && string.Equals(StringKey, other.StringKey, StringComparison.Ordinal);
            public override bool Equals(object obj) => obj is ReferKey other && Equals(other);
            public override int GetHashCode() => ((int)Kind * 397) ^ EnumKey ^ StringComparer.Ordinal.GetHashCode(StringKey);
        }

        private readonly Dictionary<ReferKey, int> runtimeKeys = new Dictionary<ReferKey, int>();
        private readonly Dictionary<ReferKey, int> referenceCounts = new Dictionary<ReferKey, int>();

        public ESRuntimeAssetReferTableResolver(IESAssetRuntimeProvider runtimeProvider)
        {
            RuntimeProvider = runtimeProvider ?? throw new ArgumentNullException(nameof(runtimeProvider));
        }

        public bool CanResolve(ESAssetReferKind kind, int enumKey, string stringKey) => TryResolveRuntimeKey(kind, enumKey, stringKey, out _);

        public bool TryGetReady<T>(ESAssetReferKind kind, int enumKey, string stringKey, out T asset) where T : UnityEngine.Object
        {
            if (!TryResolveRuntimeKey(kind, enumKey, stringKey, out int runtimeKey))
            {
                asset = null;
                return false;
            }

            UnityEngine.Object value = null;
            switch (kind)
            {
                case ESAssetReferKind.Prefab: ESRuntimeDataAsset.Prefabs.TryGetReady(runtimeKey, out GameObject prefab); value = prefab; break;
                case ESAssetReferKind.Sprite: ESRuntimeDataAsset.Sprites.TryGetReady(runtimeKey, out Sprite sprite); value = sprite; break;
                case ESAssetReferKind.AudioClip: ESRuntimeDataAsset.AudioClips.TryGetReady(runtimeKey, out AudioClip audio); value = audio; break;
                case ESAssetReferKind.AnimationClip: ESRuntimeDataAsset.AnimationClips.TryGetReady(runtimeKey, out AnimationClip animation); value = animation; break;
                case ESAssetReferKind.AnimatorController: ESRuntimeDataAsset.AnimatorControllers.TryGetReady(runtimeKey, out RuntimeAnimatorController controller); value = controller; break;
                case ESAssetReferKind.Material: ESRuntimeDataAsset.Materials.TryGetReady(runtimeKey, out Material material); value = material; break;
                case ESAssetReferKind.Mesh: ESRuntimeDataAsset.Meshes.TryGetReady(runtimeKey, out Mesh mesh); value = mesh; break;
                case ESAssetReferKind.Texture: ESRuntimeDataAsset.Textures.TryGetReady(runtimeKey, out Texture texture); value = texture; break;
                case ESAssetReferKind.Texture2D: ESRuntimeDataAsset.Texture2Ds.TryGetReady(runtimeKey, out Texture2D texture2D); value = texture2D; break;
                case ESAssetReferKind.SpriteAtlas: ESRuntimeDataAsset.SpriteAtlases.TryGetReady(runtimeKey, out UnityEngine.U2D.SpriteAtlas atlas); value = atlas; break;
                case ESAssetReferKind.Avatar: ESRuntimeDataAsset.Avatars.TryGetReady(runtimeKey, out Avatar avatar); value = avatar; break;
                case ESAssetReferKind.PlayableAsset: ESRuntimeDataAsset.PlayableAssets.TryGetReady(runtimeKey, out UnityEngine.Playables.PlayableAsset playable); value = playable; break;
                case ESAssetReferKind.ScriptableObject: ESRuntimeDataAsset.ScriptableObjects.TryGetReady(runtimeKey, out ScriptableObject scriptableObject); value = scriptableObject; break;
                case ESAssetReferKind.TimelineAsset: ESRuntimeDataAsset.TimelineAssets.TryGetReady(runtimeKey, out UnityEngine.Object timeline); value = timeline; break;
                case ESAssetReferKind.VideoClip: ESRuntimeDataAsset.VideoClips.TryGetReady(runtimeKey, out UnityEngine.Video.VideoClip video); value = video; break;
                case ESAssetReferKind.TerrainData: ESRuntimeDataAsset.TerrainDatas.TryGetReady(runtimeKey, out TerrainData terrain); value = terrain; break;
            }

            asset = value as T;
            return asset != null;
        }

        public UniTask<T> LoadAsync<T>(ESAssetReferKind kind, int enumKey, string stringKey, System.Threading.CancellationToken cancellationToken) where T : UnityEngine.Object
        {
            switch (kind)
            {
                case ESAssetReferKind.Prefab: return LoadAsync<ESAssetReferPrefabConfigData, GameObject, T>(ESRuntimeDataAsset.Prefabs, kind, enumKey, stringKey, cancellationToken);
                case ESAssetReferKind.Sprite: return LoadAsync<ESAssetReferSpriteConfigData, Sprite, T>(ESRuntimeDataAsset.Sprites, kind, enumKey, stringKey, cancellationToken);
                case ESAssetReferKind.AudioClip: return LoadAsync<ESAssetReferAudioClipConfigData, AudioClip, T>(ESRuntimeDataAsset.AudioClips, kind, enumKey, stringKey, cancellationToken);
                case ESAssetReferKind.AnimationClip: return LoadAsync<ESAssetReferAnimationClipConfigData, AnimationClip, T>(ESRuntimeDataAsset.AnimationClips, kind, enumKey, stringKey, cancellationToken);
                case ESAssetReferKind.AnimatorController: return LoadAsync<ESAssetReferAnimatorControllerConfigData, RuntimeAnimatorController, T>(ESRuntimeDataAsset.AnimatorControllers, kind, enumKey, stringKey, cancellationToken);
                case ESAssetReferKind.Material: return LoadAsync<ESAssetReferMaterialConfigData, Material, T>(ESRuntimeDataAsset.Materials, kind, enumKey, stringKey, cancellationToken);
                case ESAssetReferKind.Mesh: return LoadAsync<ESAssetReferMeshConfigData, Mesh, T>(ESRuntimeDataAsset.Meshes, kind, enumKey, stringKey, cancellationToken);
                case ESAssetReferKind.Texture: return LoadAsync<ESAssetReferTextureConfigData, Texture, T>(ESRuntimeDataAsset.Textures, kind, enumKey, stringKey, cancellationToken);
                case ESAssetReferKind.Texture2D: return LoadAsync<ESAssetReferTexture2DConfigData, Texture2D, T>(ESRuntimeDataAsset.Texture2Ds, kind, enumKey, stringKey, cancellationToken);
                case ESAssetReferKind.SpriteAtlas: return LoadAsync<ESAssetReferSpriteAtlasConfigData, UnityEngine.U2D.SpriteAtlas, T>(ESRuntimeDataAsset.SpriteAtlases, kind, enumKey, stringKey, cancellationToken);
                case ESAssetReferKind.Avatar: return LoadAsync<ESAssetReferAvatarConfigData, Avatar, T>(ESRuntimeDataAsset.Avatars, kind, enumKey, stringKey, cancellationToken);
                case ESAssetReferKind.PlayableAsset: return LoadAsync<ESAssetReferPlayableAssetConfigData, UnityEngine.Playables.PlayableAsset, T>(ESRuntimeDataAsset.PlayableAssets, kind, enumKey, stringKey, cancellationToken);
                case ESAssetReferKind.ScriptableObject: return LoadAsync<ESAssetReferScriptableObjectConfigData, ScriptableObject, T>(ESRuntimeDataAsset.ScriptableObjects, kind, enumKey, stringKey, cancellationToken);
                case ESAssetReferKind.TimelineAsset: return LoadAsync<ESAssetReferTimelineAssetConfigData, UnityEngine.Object, T>(ESRuntimeDataAsset.TimelineAssets, kind, enumKey, stringKey, cancellationToken);
                case ESAssetReferKind.VideoClip: return LoadAsync<ESAssetReferVideoClipConfigData, UnityEngine.Video.VideoClip, T>(ESRuntimeDataAsset.VideoClips, kind, enumKey, stringKey, cancellationToken);
                case ESAssetReferKind.TerrainData: return LoadAsync<ESAssetReferTerrainDataConfigData, TerrainData, T>(ESRuntimeDataAsset.TerrainDatas, kind, enumKey, stringKey, cancellationToken);
                default: return UniTask.FromException<T>(new NotSupportedException("AssetTable does not support kind: " + kind));
            }
        }

        public void Release(ESAssetReferKind kind, int enumKey, string stringKey)
        {
            ReferKey key = new ReferKey(kind, enumKey, stringKey);
            if (!referenceCounts.TryGetValue(key, out int count)) return;
            if (--count > 0) { referenceCounts[key] = count; return; }
            referenceCounts.Remove(key);
            if (!runtimeKeys.TryGetValue(key, out int runtimeKey)) return;
            switch (kind)
            {
                case ESAssetReferKind.Prefab: ESRuntimeDataAsset.Prefabs.Release(runtimeKey); break;
                case ESAssetReferKind.Sprite: ESRuntimeDataAsset.Sprites.Release(runtimeKey); break;
                case ESAssetReferKind.AudioClip: ESRuntimeDataAsset.AudioClips.Release(runtimeKey); break;
                case ESAssetReferKind.AnimationClip: ESRuntimeDataAsset.AnimationClips.Release(runtimeKey); break;
                case ESAssetReferKind.AnimatorController: ESRuntimeDataAsset.AnimatorControllers.Release(runtimeKey); break;
                case ESAssetReferKind.Material: ESRuntimeDataAsset.Materials.Release(runtimeKey); break;
                case ESAssetReferKind.Mesh: ESRuntimeDataAsset.Meshes.Release(runtimeKey); break;
                case ESAssetReferKind.Texture: ESRuntimeDataAsset.Textures.Release(runtimeKey); break;
                case ESAssetReferKind.Texture2D: ESRuntimeDataAsset.Texture2Ds.Release(runtimeKey); break;
                case ESAssetReferKind.SpriteAtlas: ESRuntimeDataAsset.SpriteAtlases.Release(runtimeKey); break;
                case ESAssetReferKind.Avatar: ESRuntimeDataAsset.Avatars.Release(runtimeKey); break;
                case ESAssetReferKind.PlayableAsset: ESRuntimeDataAsset.PlayableAssets.Release(runtimeKey); break;
                case ESAssetReferKind.ScriptableObject: ESRuntimeDataAsset.ScriptableObjects.Release(runtimeKey); break;
                case ESAssetReferKind.TimelineAsset: ESRuntimeDataAsset.TimelineAssets.Release(runtimeKey); break;
                case ESAssetReferKind.VideoClip: ESRuntimeDataAsset.VideoClips.Release(runtimeKey); break;
                case ESAssetReferKind.TerrainData: ESRuntimeDataAsset.TerrainDatas.Release(runtimeKey); break;
            }
        }

        private async UniTask<TRequested> LoadAsync<TData, TAsset, TRequested>(ESAssetConfigKeyTable<TData, TAsset> table, ESAssetReferKind kind, int enumKey, string stringKey, System.Threading.CancellationToken token)
            where TData : ESAssetReferConfigDataBase<TAsset>
            where TAsset : UnityEngine.Object
            where TRequested : UnityEngine.Object
        {
            if (!TryGetRuntimeKey(table, kind, enumKey, stringKey, out ReferKey key, out int runtimeKey))
                throw new KeyNotFoundException("AssetTable key was not found: " + kind + "/" + enumKey + "/" + stringKey);
            var completion = new UniTaskCompletionSource<TRequested>();
            table.GetOrLoadAsync(runtimeKey, (asset, error) =>
            {
                if (!string.IsNullOrEmpty(error)) completion.TrySetException(new InvalidOperationException(error));
                else if (asset is TRequested typed) completion.TrySetResult(typed);
                else completion.TrySetException(new InvalidCastException("AssetTable type mismatch: " + typeof(TAsset).Name + " -> " + typeof(TRequested).Name));
            });
            TRequested result = await completion.Task.AttachExternalCancellation(token);
            referenceCounts.TryGetValue(key, out int count);
            referenceCounts[key] = count + 1;
            return result;
        }

        private bool TryResolveRuntimeKey(ESAssetReferKind kind, int enumKey, string stringKey, out int runtimeKey)
        {
            switch (kind)
            {
                case ESAssetReferKind.Prefab: return TryGetRuntimeKey(ESRuntimeDataAsset.Prefabs, kind, enumKey, stringKey, out _, out runtimeKey);
                case ESAssetReferKind.Sprite: return TryGetRuntimeKey(ESRuntimeDataAsset.Sprites, kind, enumKey, stringKey, out _, out runtimeKey);
                case ESAssetReferKind.AudioClip: return TryGetRuntimeKey(ESRuntimeDataAsset.AudioClips, kind, enumKey, stringKey, out _, out runtimeKey);
                case ESAssetReferKind.AnimationClip: return TryGetRuntimeKey(ESRuntimeDataAsset.AnimationClips, kind, enumKey, stringKey, out _, out runtimeKey);
                case ESAssetReferKind.AnimatorController: return TryGetRuntimeKey(ESRuntimeDataAsset.AnimatorControllers, kind, enumKey, stringKey, out _, out runtimeKey);
                case ESAssetReferKind.Material: return TryGetRuntimeKey(ESRuntimeDataAsset.Materials, kind, enumKey, stringKey, out _, out runtimeKey);
                case ESAssetReferKind.Mesh: return TryGetRuntimeKey(ESRuntimeDataAsset.Meshes, kind, enumKey, stringKey, out _, out runtimeKey);
                case ESAssetReferKind.Texture: return TryGetRuntimeKey(ESRuntimeDataAsset.Textures, kind, enumKey, stringKey, out _, out runtimeKey);
                case ESAssetReferKind.Texture2D: return TryGetRuntimeKey(ESRuntimeDataAsset.Texture2Ds, kind, enumKey, stringKey, out _, out runtimeKey);
                case ESAssetReferKind.SpriteAtlas: return TryGetRuntimeKey(ESRuntimeDataAsset.SpriteAtlases, kind, enumKey, stringKey, out _, out runtimeKey);
                case ESAssetReferKind.Avatar: return TryGetRuntimeKey(ESRuntimeDataAsset.Avatars, kind, enumKey, stringKey, out _, out runtimeKey);
                case ESAssetReferKind.PlayableAsset: return TryGetRuntimeKey(ESRuntimeDataAsset.PlayableAssets, kind, enumKey, stringKey, out _, out runtimeKey);
                case ESAssetReferKind.ScriptableObject: return TryGetRuntimeKey(ESRuntimeDataAsset.ScriptableObjects, kind, enumKey, stringKey, out _, out runtimeKey);
                case ESAssetReferKind.TimelineAsset: return TryGetRuntimeKey(ESRuntimeDataAsset.TimelineAssets, kind, enumKey, stringKey, out _, out runtimeKey);
                case ESAssetReferKind.VideoClip: return TryGetRuntimeKey(ESRuntimeDataAsset.VideoClips, kind, enumKey, stringKey, out _, out runtimeKey);
                case ESAssetReferKind.TerrainData: return TryGetRuntimeKey(ESRuntimeDataAsset.TerrainDatas, kind, enumKey, stringKey, out _, out runtimeKey);
                default: runtimeKey = 0; return false;
            }
        }

        private bool TryGetRuntimeKey<TData, TAsset>(ESAssetConfigKeyTable<TData, TAsset> table, ESAssetReferKind kind, int enumKey, string stringKey, out ReferKey key, out int runtimeKey)
            where TData : ESAssetReferConfigDataBase<TAsset>
            where TAsset : UnityEngine.Object
        {
            key = new ReferKey(kind, enumKey, stringKey);
            if (runtimeKeys.TryGetValue(key, out runtimeKey)) return true;
            if (enumKey != 0 && table.TryGet(enumKey, out _)) { runtimeKeys[key] = runtimeKey = enumKey; return true; }
            if (!string.IsNullOrEmpty(stringKey) && table.TryGetRuntimeKey(stringKey, out runtimeKey)) { runtimeKeys[key] = runtimeKey; return true; }
            return false;
        }
    }
}
