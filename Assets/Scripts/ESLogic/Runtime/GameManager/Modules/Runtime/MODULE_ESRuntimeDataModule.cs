using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
    public static class ESRuntimeDataGameCore
    {
        public static readonly ESBuffConfigKeyTable Buffs = new ESBuffConfigKeyTable(128);
        public static readonly ESShotConfigKeyTable Shots = new ESShotConfigKeyTable(128);
        public static readonly ESMonsterConfigKeyTable Monsters = new ESMonsterConfigKeyTable(128);
        public static readonly ESNpcConfigKeyTable Npcs = new ESNpcConfigKeyTable(128);
        public static readonly ESWeaponConfigKeyTable Weapons = new ESWeaponConfigKeyTable(64);
        public static readonly ESSkillConfigKeyTable Skills = new ESSkillConfigKeyTable(128);
        public static readonly ESAudioCueConfigKeyTable AudioCues = new ESAudioCueConfigKeyTable(128);
        public static readonly ESVfxConfigKeyTable Vfx = new ESVfxConfigKeyTable(128);
        public static readonly ESActionConfigKeyTable Actions = new ESActionConfigKeyTable(64);
        public static readonly ESSkillTrackConfigKeyTable SkillTracks = new ESSkillTrackConfigKeyTable(128);

        public static void BeginBuild(bool clear)
        {
            ESAudioGameCoreTable.NotifyCatalogBuildStarted();
            Buffs.BeginBuild(clear);
            Shots.BeginBuild(clear);
            Monsters.BeginBuild(clear);
            Npcs.BeginBuild(clear);
            Weapons.BeginBuild(clear);
            Skills.BeginBuild(clear);
            AudioCues.BeginBuild(clear);
            Vfx.BeginBuild(clear);
            Actions.BeginBuild(clear);
            SkillTracks.BeginBuild(clear);
            ESActionPresentationMappingTable.BeginBuild(clear);
        }

        public static void EndBuild()
        {
            EndBuild(true);
        }

        internal static void EndBuild(bool audioCueCatalogReady)
        {
            Buffs.EndBuild();
            Shots.EndBuild();
            Monsters.EndBuild();
            Npcs.EndBuild();
            Weapons.EndBuild();
            Skills.EndBuild();
            AudioCues.EndBuild();
            Vfx.EndBuild();
            Actions.EndBuild();
            SkillTracks.EndBuild();
            ESActionPresentationMappingTable.EndBuild();
            if (audioCueCatalogReady)
                ESAudioGameCoreTable.NotifyCatalogBuildCompleted();
            else
                ESAudioGameCoreTable.NotifyCatalogUnavailable();
        }

        /// <summary>
        /// 仅在 Consumer/Provider 生命周期切换和全量资源安全点调用：先断开静态表
        /// 对旧 GameCore SO 的引用，随后由对应 Scope 归还底层 Handle。
        /// </summary>
        public static void ResetForResourceTransition()
        {
            if (Buffs.IsBuilding || Shots.IsBuilding || Monsters.IsBuilding || Npcs.IsBuilding
                || Weapons.IsBuilding || Skills.IsBuilding || AudioCues.IsBuilding || Vfx.IsBuilding
                || Actions.IsBuilding || SkillTracks.IsBuilding
                || ESActionPresentationMappingTable.IsBuilding)
                throw new InvalidOperationException("[ESGameCore] 正在构建 GameCore 表，不能执行资源生命周期切换。");

            BeginBuild(true);
            // ResetForResourceTransition clears a former catalog before the next Consumer
            // GameCore injection begins. It must not be advertised as usable merely because the
            // empty table has finished its internal clear transaction.
            EndBuild(false);
        }
    }

    [Serializable]
    public struct ESAssetAutoRegisterReport
    {
        public int libraryCount;
        public int normalizedPageCount;
        public int registeredPageCount;
        public int conflictCount;
        public string conflictReport;

        public override string ToString()
        {
            return "[ESAssetAutoRegister]"
                + " libraries=" + libraryCount
                + ", normalizedPages=" + normalizedPageCount
                + ", registeredPages=" + registeredPageCount
                + ", conflicts=" + conflictCount
                + (string.IsNullOrEmpty(conflictReport) ? string.Empty : "\n" + conflictReport);
        }
    }

    public struct ESAssetCatalogBuildValidation
    {
        public int sourceBusinessEntries;
        public int expectedBusinessEntries;
        public int candidateEntries;
        public int equivalentDuplicateCount;
        public string equivalentDuplicateReport;
        public int conflictCount;
        public string conflictReport;

        public bool IsValid => expectedBusinessEntries > 0
            && candidateEntries == expectedBusinessEntries
            && conflictCount == 0;
    }

    internal readonly struct ESAssetConfigGenerationRecord
    {
        public readonly ESAssetReferKind Kind;
        public readonly ESAssetConfigRecord Record;

        public ESAssetConfigGenerationRecord(ESAssetReferKind kind, in ESAssetConfigRecord record)
        {
            Kind = kind;
            Record = record;
        }
    }

    /// <summary>
    /// One immutable Asset ConfigTable generation. Tables and Loader handles are never shared
    /// with another generation; only reader accounting and retirement state remain mutable.
    /// </summary>
    internal sealed class ESAssetConfigTableGenerationState
    {
        private readonly object lifetimeSync = new object();
        private readonly Action<ESAssetConfigTableGenerationState, Exception> reclaimed;
        private ESAssetConfigGenerationRecord[] records = Array.Empty<ESAssetConfigGenerationRecord>();
        private int readerCount;
        private bool buildCompleted;
        private bool published;
        private bool retired;
        private bool reclaimStarted;
        private bool reclaimedCompleted;

        internal readonly ESAssetConfigKeyTable<ESAssetReferPrefabConfigData, GameObject> Prefabs = new ESAssetConfigKeyTable<ESAssetReferPrefabConfigData, GameObject>(256, "Asset.Prefab");
        internal readonly ESAssetConfigKeyTable<ESAssetReferSpriteConfigData, Sprite> Sprites = new ESAssetConfigKeyTable<ESAssetReferSpriteConfigData, Sprite>(256, "Asset.Sprite");
        internal readonly ESAssetConfigKeyTable<ESAssetReferAudioClipConfigData, AudioClip> AudioClips = new ESAssetConfigKeyTable<ESAssetReferAudioClipConfigData, AudioClip>(256, "Asset.AudioClip");
        internal readonly ESAssetConfigKeyTable<ESAssetReferAnimationClipConfigData, AnimationClip> AnimationClips = new ESAssetConfigKeyTable<ESAssetReferAnimationClipConfigData, AnimationClip>(256, "Asset.AnimationClip");
        internal readonly ESAssetConfigKeyTable<ESAssetReferAnimatorControllerConfigData, RuntimeAnimatorController> AnimatorControllers = new ESAssetConfigKeyTable<ESAssetReferAnimatorControllerConfigData, RuntimeAnimatorController>(128, "Asset.AnimatorController");
        internal readonly ESAssetConfigKeyTable<ESAssetReferMaterialConfigData, Material> Materials = new ESAssetConfigKeyTable<ESAssetReferMaterialConfigData, Material>(256, "Asset.Material");
        internal readonly ESAssetConfigKeyTable<ESAssetReferMeshConfigData, Mesh> Meshes = new ESAssetConfigKeyTable<ESAssetReferMeshConfigData, Mesh>(256, "Asset.Mesh");
        internal readonly ESAssetConfigKeyTable<ESAssetReferSceneConfigData, UnityEngine.Object> Scenes = new ESAssetConfigKeyTable<ESAssetReferSceneConfigData, UnityEngine.Object>(64, "Asset.Scene");
        internal readonly ESAssetConfigKeyTable<ESAssetReferTextureConfigData, Texture> Textures = new ESAssetConfigKeyTable<ESAssetReferTextureConfigData, Texture>(128, "Asset.Texture");
        internal readonly ESAssetConfigKeyTable<ESAssetReferTexture2DConfigData, Texture2D> Texture2Ds = new ESAssetConfigKeyTable<ESAssetReferTexture2DConfigData, Texture2D>(128, "Asset.Texture2D");
        internal readonly ESAssetConfigKeyTable<ESAssetReferSpriteAtlasConfigData, UnityEngine.U2D.SpriteAtlas> SpriteAtlases = new ESAssetConfigKeyTable<ESAssetReferSpriteAtlasConfigData, UnityEngine.U2D.SpriteAtlas>(64, "Asset.SpriteAtlas");
        internal readonly ESAssetConfigKeyTable<ESAssetReferAvatarConfigData, Avatar> Avatars = new ESAssetConfigKeyTable<ESAssetReferAvatarConfigData, Avatar>(64, "Asset.Avatar");
        internal readonly ESAssetConfigKeyTable<ESAssetReferPlayableAssetConfigData, UnityEngine.Playables.PlayableAsset> PlayableAssets = new ESAssetConfigKeyTable<ESAssetReferPlayableAssetConfigData, UnityEngine.Playables.PlayableAsset>(64, "Asset.PlayableAsset");
        internal readonly ESAssetConfigKeyTable<ESAssetReferScriptableObjectConfigData, ScriptableObject> ScriptableObjects = new ESAssetConfigKeyTable<ESAssetReferScriptableObjectConfigData, ScriptableObject>(128, "Asset.ScriptableObject");
        internal readonly ESAssetConfigKeyTable<ESAssetReferTimelineAssetConfigData, UnityEngine.Object> TimelineAssets = new ESAssetConfigKeyTable<ESAssetReferTimelineAssetConfigData, UnityEngine.Object>(64, "Asset.TimelineAsset");
        internal readonly ESAssetConfigKeyTable<ESAssetReferVideoClipConfigData, UnityEngine.Video.VideoClip> VideoClips = new ESAssetConfigKeyTable<ESAssetReferVideoClipConfigData, UnityEngine.Video.VideoClip>(64, "Asset.VideoClip");
        internal readonly ESAssetConfigKeyTable<ESAssetReferTerrainDataConfigData, TerrainData> TerrainDatas = new ESAssetConfigKeyTable<ESAssetReferTerrainDataConfigData, TerrainData>(32, "Asset.TerrainData");
        internal readonly ESAssetConfigKeyTable<ESAssetReferRawConfigData, TextAsset> RawAssets = new ESAssetConfigKeyTable<ESAssetReferRawConfigData, TextAsset>(64, "Asset.Raw");

        internal ESAssetConfigTableGenerationState(
            long baseGeneration,
            Action<ESAssetConfigTableGenerationState, Exception> reclaimed)
        {
            BaseGeneration = baseGeneration;
            this.reclaimed = reclaimed;
        }

        internal long BaseGeneration { get; }
        internal long Generation { get; private set; }
        internal string CatalogFingerprint { get; private set; } = string.Empty;
        internal int ProviderGeneration { get; private set; }
        internal int ReaderCount { get { lock (lifetimeSync) return readerCount; } }
        internal bool IsRetired { get { lock (lifetimeSync) return retired; } }
        internal bool IsReclaimed { get { lock (lifetimeSync) return reclaimedCompleted; } }
        internal IReadOnlyList<ESAssetConfigGenerationRecord> Records => records;

        internal int RegisteredCount => Prefabs.Count + Sprites.Count + AudioClips.Count
            + AnimationClips.Count + AnimatorControllers.Count + Materials.Count + Meshes.Count
            + Scenes.Count + Textures.Count + Texture2Ds.Count + SpriteAtlases.Count + Avatars.Count
            + PlayableAssets.Count + ScriptableObjects.Count + TimelineAssets.Count + VideoClips.Count
            + TerrainDatas.Count + RawAssets.Count;

        internal int ConflictCount => Prefabs.ConflictCount + Sprites.ConflictCount + AudioClips.ConflictCount
            + AnimationClips.ConflictCount + AnimatorControllers.ConflictCount + Materials.ConflictCount
            + Meshes.ConflictCount + Scenes.ConflictCount + Textures.ConflictCount + Texture2Ds.ConflictCount
            + SpriteAtlases.ConflictCount + Avatars.ConflictCount + PlayableAssets.ConflictCount
            + ScriptableObjects.ConflictCount + TimelineAssets.ConflictCount + VideoClips.ConflictCount
            + TerrainDatas.ConflictCount + RawAssets.ConflictCount;

        internal bool HasPendingLoads => Prefabs.HasPendingLoads || Sprites.HasPendingLoads || AudioClips.HasPendingLoads
            || AnimationClips.HasPendingLoads || AnimatorControllers.HasPendingLoads || Materials.HasPendingLoads
            || Meshes.HasPendingLoads || Scenes.HasPendingLoads || Textures.HasPendingLoads
            || Texture2Ds.HasPendingLoads || SpriteAtlases.HasPendingLoads || Avatars.HasPendingLoads
            || PlayableAssets.HasPendingLoads || ScriptableObjects.HasPendingLoads || TimelineAssets.HasPendingLoads
            || VideoClips.HasPendingLoads || TerrainDatas.HasPendingLoads || RawAssets.HasPendingLoads;

        internal void BeginBuild()
        {
            Prefabs.BeginBuild(true);
            Sprites.BeginBuild(true);
            AudioClips.BeginBuild(true);
            AnimationClips.BeginBuild(true);
            AnimatorControllers.BeginBuild(true);
            Materials.BeginBuild(true);
            Meshes.BeginBuild(true);
            Scenes.BeginBuild(true);
            Textures.BeginBuild(true);
            Texture2Ds.BeginBuild(true);
            SpriteAtlases.BeginBuild(true);
            Avatars.BeginBuild(true);
            PlayableAssets.BeginBuild(true);
            ScriptableObjects.BeginBuild(true);
            TimelineAssets.BeginBuild(true);
            VideoClips.BeginBuild(true);
            TerrainDatas.BeginBuild(true);
            RawAssets.BeginBuild(true);
        }

        internal void CompleteBuild(List<ESAssetConfigGenerationRecord> sourceRecords)
        {
            if (buildCompleted)
                throw new InvalidOperationException("Asset ConfigTable 候选代已完成构建。");

            Prefabs.EndBuild();
            Sprites.EndBuild();
            AudioClips.EndBuild();
            AnimationClips.EndBuild();
            AnimatorControllers.EndBuild();
            Materials.EndBuild();
            Meshes.EndBuild();
            Scenes.EndBuild();
            Textures.EndBuild();
            Texture2Ds.EndBuild();
            SpriteAtlases.EndBuild();
            Avatars.EndBuild();
            PlayableAssets.EndBuild();
            ScriptableObjects.EndBuild();
            TimelineAssets.EndBuild();
            VideoClips.EndBuild();
            TerrainDatas.EndBuild();
            RawAssets.EndBuild();
            records = sourceRecords == null ? Array.Empty<ESAssetConfigGenerationRecord>() : sourceRecords.ToArray();
            buildCompleted = true;
        }

        internal void PrepareForCommit(long generation, string catalogFingerprint)
        {
            if (!buildCompleted || published || retired)
                throw new InvalidOperationException("Asset ConfigTable 候选代不处于可提交状态。");
            if (generation <= 0)
                throw new ArgumentOutOfRangeException(nameof(generation));

            Generation = generation;
            CatalogFingerprint = catalogFingerprint ?? string.Empty;
        }

        internal void BindProvider(IESAssetRuntimeProvider provider, int providerGeneration)
        {
            if (!buildCompleted || published || retired)
                throw new InvalidOperationException("只能在候选代发布前绑定 Provider。");
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            if (providerGeneration <= 0)
                throw new ArgumentOutOfRangeException(nameof(providerGeneration));

            Prefabs.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferPrefabConfigData, GameObject>(provider));
            Sprites.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferSpriteConfigData, Sprite>(provider));
            AudioClips.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferAudioClipConfigData, AudioClip>(provider));
            AnimationClips.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferAnimationClipConfigData, AnimationClip>(provider));
            AnimatorControllers.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferAnimatorControllerConfigData, RuntimeAnimatorController>(provider));
            Materials.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferMaterialConfigData, Material>(provider));
            Meshes.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferMeshConfigData, Mesh>(provider));
            Scenes.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferSceneConfigData, UnityEngine.Object>(provider));
            Textures.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferTextureConfigData, Texture>(provider));
            Texture2Ds.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferTexture2DConfigData, Texture2D>(provider));
            SpriteAtlases.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferSpriteAtlasConfigData, UnityEngine.U2D.SpriteAtlas>(provider));
            Avatars.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferAvatarConfigData, Avatar>(provider));
            PlayableAssets.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferPlayableAssetConfigData, UnityEngine.Playables.PlayableAsset>(provider));
            ScriptableObjects.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferScriptableObjectConfigData, ScriptableObject>(provider));
            TimelineAssets.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferTimelineAssetConfigData, UnityEngine.Object>(provider));
            VideoClips.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferVideoClipConfigData, UnityEngine.Video.VideoClip>(provider));
            TerrainDatas.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferTerrainDataConfigData, TerrainData>(provider));
            RawAssets.SetLoader(new ESRuntimeAssetTableLoader<ESAssetReferRawConfigData, TextAsset>(provider));
            ProviderGeneration = providerGeneration;
        }

        internal void Publish()
        {
            if (!buildCompleted || Generation <= 0 || retired)
                throw new InvalidOperationException("Asset ConfigTable 候选代尚未准备完成。");
            published = true;
        }

        internal bool TryAcquire(out ESAssetConfigTableGenerationLease lease)
        {
            lock (lifetimeSync)
            {
                if (!published || retired || reclaimStarted)
                {
                    lease = null;
                    return false;
                }
                if (readerCount == int.MaxValue)
                    throw new InvalidOperationException("Asset ConfigTable generation reader 计数已溢出。");

                readerCount++;
                lease = new ESAssetConfigTableGenerationLease(this);
                return true;
            }
        }

        internal void ReleaseReader()
        {
            bool reclaimNow = false;
            lock (lifetimeSync)
            {
                if (readerCount <= 0)
                    return;
                readerCount--;
                if (readerCount == 0 && retired && !reclaimStarted)
                {
                    reclaimStarted = true;
                    reclaimNow = true;
                }
            }
            if (reclaimNow)
                ReclaimLoaders();
        }

        internal void Retire()
        {
            bool reclaimNow = false;
            lock (lifetimeSync)
            {
                if (retired)
                    return;
                retired = true;
                published = false;
                if (readerCount == 0 && !reclaimStarted)
                {
                    reclaimStarted = true;
                    reclaimNow = true;
                }
            }
            if (reclaimNow)
                ReclaimLoaders();
        }

        internal string GetConflictReport()
        {
            var builder = new System.Text.StringBuilder(512);
            AppendConflict(builder, "Prefab", Prefabs.GetConflictReport());
            AppendConflict(builder, "Sprite", Sprites.GetConflictReport());
            AppendConflict(builder, "AudioClip", AudioClips.GetConflictReport());
            AppendConflict(builder, "AnimationClip", AnimationClips.GetConflictReport());
            AppendConflict(builder, "AnimatorController", AnimatorControllers.GetConflictReport());
            AppendConflict(builder, "Material", Materials.GetConflictReport());
            AppendConflict(builder, "Mesh", Meshes.GetConflictReport());
            AppendConflict(builder, "Scene", Scenes.GetConflictReport());
            AppendConflict(builder, "Texture", Textures.GetConflictReport());
            AppendConflict(builder, "Texture2D", Texture2Ds.GetConflictReport());
            AppendConflict(builder, "SpriteAtlas", SpriteAtlases.GetConflictReport());
            AppendConflict(builder, "Avatar", Avatars.GetConflictReport());
            AppendConflict(builder, "PlayableAsset", PlayableAssets.GetConflictReport());
            AppendConflict(builder, "ScriptableObject", ScriptableObjects.GetConflictReport());
            AppendConflict(builder, "TimelineAsset", TimelineAssets.GetConflictReport());
            AppendConflict(builder, "VideoClip", VideoClips.GetConflictReport());
            AppendConflict(builder, "TerrainData", TerrainDatas.GetConflictReport());
            AppendConflict(builder, "Raw", RawAssets.GetConflictReport());
            return builder.ToString();
        }

        private static void AppendConflict(System.Text.StringBuilder builder, string title, string report)
        {
            if (string.IsNullOrEmpty(report))
                return;
            builder.Append('[').Append(title).Append(']').AppendLine();
            builder.Append(report);
        }

        private void ReclaimLoaders()
        {
            List<Exception> failures = null;
            ResetLoader(Prefabs.ResetLoader, ref failures);
            ResetLoader(Sprites.ResetLoader, ref failures);
            ResetLoader(AudioClips.ResetLoader, ref failures);
            ResetLoader(AnimationClips.ResetLoader, ref failures);
            ResetLoader(AnimatorControllers.ResetLoader, ref failures);
            ResetLoader(Materials.ResetLoader, ref failures);
            ResetLoader(Meshes.ResetLoader, ref failures);
            ResetLoader(Scenes.ResetLoader, ref failures);
            ResetLoader(Textures.ResetLoader, ref failures);
            ResetLoader(Texture2Ds.ResetLoader, ref failures);
            ResetLoader(SpriteAtlases.ResetLoader, ref failures);
            ResetLoader(Avatars.ResetLoader, ref failures);
            ResetLoader(PlayableAssets.ResetLoader, ref failures);
            ResetLoader(ScriptableObjects.ResetLoader, ref failures);
            ResetLoader(TimelineAssets.ResetLoader, ref failures);
            ResetLoader(VideoClips.ResetLoader, ref failures);
            ResetLoader(TerrainDatas.ResetLoader, ref failures);
            ResetLoader(RawAssets.ResetLoader, ref failures);

            Exception failure = failures == null
                ? null
                : failures.Count == 1 ? failures[0] : new AggregateException(
                    "Asset ConfigTable 旧代 Loader 回收发生多个异常。",
                    failures);
            lock (lifetimeSync)
                reclaimedCompleted = true;
            reclaimed?.Invoke(this, failure);
        }

        private static void ResetLoader(Func<int> reset, ref List<Exception> failures)
        {
            try { reset(); }
            catch (Exception exception)
            {
                if (failures == null)
                    failures = new List<Exception>(2);
                failures.Add(exception);
            }
        }
    }

    internal sealed class ESAssetConfigTableGenerationLease : IDisposable
    {
        private ESAssetConfigTableGenerationState state;

        internal ESAssetConfigTableGenerationLease(ESAssetConfigTableGenerationState state)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        }

        internal ESAssetConfigTableGenerationState State => state
            ?? throw new ObjectDisposedException(nameof(ESAssetConfigTableGenerationLease));
        internal long Generation => state?.Generation ?? 0;

        public void Dispose()
        {
            Interlocked.Exchange(ref state, null)?.ReleaseReader();
        }
    }

    public static class ESRuntimeDataAsset
    {
        private readonly struct AssetBusinessRegistrationKey : IEquatable<AssetBusinessRegistrationKey>
        {
            private readonly ESAssetReferKind kind;
            private readonly int enumKey;
            private readonly string stringKey;

            internal AssetBusinessRegistrationKey(
                ESAssetReferKind kind,
                int enumKey,
                string stringKey)
            {
                this.kind = kind;
                this.enumKey = enumKey;
                this.stringKey = stringKey ?? string.Empty;
            }

            public bool Equals(AssetBusinessRegistrationKey other)
            {
                return kind == other.kind
                       && enumKey == other.enumKey
                       && string.Equals(stringKey, other.stringKey, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is AssetBusinessRegistrationKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)kind;
                    hash = (hash * 397) ^ enumKey;
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(stringKey ?? string.Empty);
                    return hash;
                }
            }
        }

        private static readonly object commitSync = new object();
        private static readonly List<ESAssetConfigTableGenerationState> retiredStates = new List<ESAssetConfigTableGenerationState>(4);
        private static long editorCatalogCommitGeneration;
        private static long authorityEpoch;
        private static bool assetConfigTablesAvailable;
        private static ESAssetConfigTableGenerationState currentState;
        private static ESAssetConfigTableGenerationState pendingProviderCandidate;
        private static string pendingProviderFingerprint = string.Empty;
        private static bool providerCandidateBuildActive;
        private static IESAssetRuntimeProvider activeProvider;
        private static int activeProviderGeneration;
        private static int reclamationFailureCount;
        private static string lastReclamationFailure = string.Empty;

        public static long EditorCatalogCommitGeneration => editorCatalogCommitGeneration;
        public static long AssetConfigTableGeneration => Volatile.Read(ref currentState)?.Generation ?? 0;
        public static int AssetConfigProviderGeneration => Volatile.Read(ref currentState)?.ProviderGeneration ?? 0;
        public static bool AssetConfigTablesAvailable => Volatile.Read(ref assetConfigTablesAvailable);
        public static bool AssetConfigProviderBindingCurrent
            => AssetConfigTablesAvailable
                && activeProvider != null
                && ESAssets.IsReady
                && AssetConfigProviderGeneration == activeProviderGeneration
                && AssetConfigProviderGeneration == ESAssets.RuntimeBackendGeneration;
        public static string EditorCatalogCommittedFingerprint
            => AssetConfigTablesAvailable ? Volatile.Read(ref currentState)?.CatalogFingerprint ?? string.Empty : string.Empty;
        public static long EditorCatalogCommittedConfigTableGeneration
            => string.IsNullOrWhiteSpace(EditorCatalogCommittedFingerprint) ? 0 : AssetConfigTableGeneration;
        public static int RetiredAssetConfigGenerationCount { get { lock (commitSync) return retiredStates.Count; } }
        public static int ActiveAssetConfigReaderCount
        {
            get
            {
                lock (commitSync)
                {
                    int count = currentState?.ReaderCount ?? 0;
                    for (int i = 0; i < retiredStates.Count; i++)
                        if (!ReferenceEquals(retiredStates[i], currentState))
                            count += retiredStates[i].ReaderCount;
                    return count;
                }
            }
        }
        public static int AssetConfigReclamationFailureCount => Volatile.Read(ref reclamationFailureCount);
        public static string LastAssetConfigReclamationFailure { get { lock (commitSync) return lastReclamationFailure; } }

        public static readonly ESAssetConfigTableReader<ESAssetReferPrefabConfigData, GameObject> Prefabs = new ESAssetConfigTableReader<ESAssetReferPrefabConfigData, GameObject>(state => state.Prefabs);
        public static readonly ESAssetConfigTableReader<ESAssetReferSpriteConfigData, Sprite> Sprites = new ESAssetConfigTableReader<ESAssetReferSpriteConfigData, Sprite>(state => state.Sprites);
        public static readonly ESAssetConfigTableReader<ESAssetReferAudioClipConfigData, AudioClip> AudioClips = new ESAssetConfigTableReader<ESAssetReferAudioClipConfigData, AudioClip>(state => state.AudioClips);
        public static readonly ESAssetConfigTableReader<ESAssetReferAnimationClipConfigData, AnimationClip> AnimationClips = new ESAssetConfigTableReader<ESAssetReferAnimationClipConfigData, AnimationClip>(state => state.AnimationClips);
        public static readonly ESAssetConfigTableReader<ESAssetReferAnimatorControllerConfigData, RuntimeAnimatorController> AnimatorControllers = new ESAssetConfigTableReader<ESAssetReferAnimatorControllerConfigData, RuntimeAnimatorController>(state => state.AnimatorControllers);
        public static readonly ESAssetConfigTableReader<ESAssetReferMaterialConfigData, Material> Materials = new ESAssetConfigTableReader<ESAssetReferMaterialConfigData, Material>(state => state.Materials);
        public static readonly ESAssetConfigTableReader<ESAssetReferMeshConfigData, Mesh> Meshes = new ESAssetConfigTableReader<ESAssetReferMeshConfigData, Mesh>(state => state.Meshes);
        public static readonly ESAssetConfigTableReader<ESAssetReferSceneConfigData, UnityEngine.Object> Scenes = new ESAssetConfigTableReader<ESAssetReferSceneConfigData, UnityEngine.Object>(state => state.Scenes);
        public static readonly ESAssetConfigTableReader<ESAssetReferTextureConfigData, Texture> Textures = new ESAssetConfigTableReader<ESAssetReferTextureConfigData, Texture>(state => state.Textures);
        public static readonly ESAssetConfigTableReader<ESAssetReferTexture2DConfigData, Texture2D> Texture2Ds = new ESAssetConfigTableReader<ESAssetReferTexture2DConfigData, Texture2D>(state => state.Texture2Ds);
        public static readonly ESAssetConfigTableReader<ESAssetReferSpriteAtlasConfigData, UnityEngine.U2D.SpriteAtlas> SpriteAtlases = new ESAssetConfigTableReader<ESAssetReferSpriteAtlasConfigData, UnityEngine.U2D.SpriteAtlas>(state => state.SpriteAtlases);
        public static readonly ESAssetConfigTableReader<ESAssetReferAvatarConfigData, Avatar> Avatars = new ESAssetConfigTableReader<ESAssetReferAvatarConfigData, Avatar>(state => state.Avatars);
        public static readonly ESAssetConfigTableReader<ESAssetReferPlayableAssetConfigData, UnityEngine.Playables.PlayableAsset> PlayableAssets = new ESAssetConfigTableReader<ESAssetReferPlayableAssetConfigData, UnityEngine.Playables.PlayableAsset>(state => state.PlayableAssets);
        public static readonly ESAssetConfigTableReader<ESAssetReferScriptableObjectConfigData, ScriptableObject> ScriptableObjects = new ESAssetConfigTableReader<ESAssetReferScriptableObjectConfigData, ScriptableObject>(state => state.ScriptableObjects);
        public static readonly ESAssetConfigTableReader<ESAssetReferTimelineAssetConfigData, UnityEngine.Object> TimelineAssets = new ESAssetConfigTableReader<ESAssetReferTimelineAssetConfigData, UnityEngine.Object>(state => state.TimelineAssets);
        public static readonly ESAssetConfigTableReader<ESAssetReferVideoClipConfigData, UnityEngine.Video.VideoClip> VideoClips = new ESAssetConfigTableReader<ESAssetReferVideoClipConfigData, UnityEngine.Video.VideoClip>(state => state.VideoClips);
        public static readonly ESAssetConfigTableReader<ESAssetReferTerrainDataConfigData, TerrainData> TerrainDatas = new ESAssetConfigTableReader<ESAssetReferTerrainDataConfigData, TerrainData>(state => state.TerrainDatas);
        public static readonly ESAssetConfigTableReader<ESAssetReferRawConfigData, TextAsset> RawAssets = new ESAssetConfigTableReader<ESAssetReferRawConfigData, TextAsset>(state => state.RawAssets);

        public static bool HasPendingAssetLoads
        {
            get
            {
                lock (commitSync)
                {
                    if (currentState?.HasPendingLoads == true || pendingProviderCandidate?.HasPendingLoads == true)
                        return true;
                    for (int i = 0; i < retiredStates.Count; i++)
                        if (retiredStates[i].HasPendingLoads)
                            return true;
                    return false;
                }
            }
        }

        internal static UniTask WaitForAssetConfigReadersAsync(CancellationToken cancellationToken)
            => UniTask.WaitUntil(() => ActiveAssetConfigReaderCount == 0, cancellationToken: cancellationToken);

        internal static bool TryAcquireAssetConfigGeneration(
            bool requireProvider,
            out ESAssetConfigTableGenerationLease lease)
        {
            lease = null;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                long epoch = Volatile.Read(ref authorityEpoch);
                if ((epoch & 1L) != 0 || !Volatile.Read(ref assetConfigTablesAvailable))
                    return false;

                ESAssetConfigTableGenerationState state = Volatile.Read(ref currentState);
                if (state == null || !state.TryAcquire(out ESAssetConfigTableGenerationLease acquired))
                    return false;

                bool providerCurrent = !requireProvider
                    || (activeProvider != null
                        && ESAssets.IsReady
                        && state.ProviderGeneration == activeProviderGeneration
                        && state.ProviderGeneration == ESAssets.RuntimeBackendGeneration);
                if (providerCurrent
                    && ReferenceEquals(state, Volatile.Read(ref currentState))
                    && epoch == Volatile.Read(ref authorityEpoch)
                    && Volatile.Read(ref assetConfigTablesAvailable))
                {
                    lease = acquired;
                    return true;
                }

                acquired.Dispose();
                if (!providerCurrent)
                    return false;
            }
            return false;
        }

        /// <summary>
        /// Provider/Catalog 切换开始时使当前权威代失效并进入退休流程。已有 Lease 可以
        /// 完成，但新调用不能再取得旧代；Loader 只在最后一个读者退出后回收。
        /// </summary>
        public static void InvalidateAssetConfigTableBinding()
        {
            lock (commitSync)
            {
                BeginAuthorityMutationLocked();
                RetireStateLocked(currentState);
                EndAuthorityMutationLocked(false);
            }
        }

        public static bool IsCurrentEditorCatalogCommit(
            string catalogSetFingerprint,
            long configTableGeneration)
        {
            ESAssetConfigTableGenerationState state = Volatile.Read(ref currentState);
            return AssetConfigTablesAvailable
                && state != null
                && configTableGeneration == state.Generation
                && activeProvider != null
                && ESAssets.IsReady
                && state.ProviderGeneration == activeProviderGeneration
                && state.ProviderGeneration == ESAssets.RuntimeBackendGeneration
                && !string.IsNullOrWhiteSpace(catalogSetFingerprint)
                && string.Equals(
                    catalogSetFingerprint,
                    state.CatalogFingerprint,
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static void BeginProviderCandidateBuild()
        {
            lock (commitSync)
            {
                if (providerCandidateBuildActive)
                    throw new InvalidOperationException("Provider Catalog 候选构建事务已经开始。");
                DisposePendingCandidateLocked();
                providerCandidateBuildActive = true;
            }
        }

        internal static void CancelProviderCandidateBuild()
        {
            lock (commitSync)
            {
                providerCandidateBuildActive = false;
                DisposePendingCandidateLocked();
            }
        }

        internal static void AttachRuntimeProvider(IESAssetRuntimeProvider provider, int providerGeneration)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            lock (commitSync)
            {
                activeProvider = provider;
                activeProviderGeneration = providerGeneration;
                providerCandidateBuildActive = false;
                if (pendingProviderCandidate != null)
                {
                    ESAssetConfigTableGenerationState candidate = pendingProviderCandidate;
                    string fingerprint = pendingProviderFingerprint;
                    pendingProviderCandidate = null;
                    pendingProviderFingerprint = string.Empty;
                    CommitCandidateLocked(candidate, fingerprint, provider, providerGeneration);
                }
            }
        }

        internal static void DetachRuntimeProvider(IESAssetRuntimeProvider provider)
        {
            lock (commitSync)
            {
                if (provider != null && !ReferenceEquals(activeProvider, provider))
                    return;
                activeProvider = null;
                activeProviderGeneration = 0;
                providerCandidateBuildActive = false;
                DisposePendingCandidateLocked();
                BeginAuthorityMutationLocked();
                RetireStateLocked(currentState);
                EndAuthorityMutationLocked(false);
            }
        }

        internal static void RotateGenerationAtSafePoint(
            IESAssetRuntimeProvider provider,
            int providerGeneration)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            ESAssetConfigGenerationRecord[] records;
            string fingerprint;
            long sourceGeneration;
            lock (commitSync)
            {
                if (!ReferenceEquals(activeProvider, provider) || activeProviderGeneration != providerGeneration)
                    throw new InvalidOperationException("安全点代际旋转的 Provider 已变化。");
                ESAssetConfigTableGenerationState state = currentState;
                if (!assetConfigTablesAvailable || state == null || state.IsRetired
                    || state.ProviderGeneration != providerGeneration)
                    return;
                records = state?.Records.ToArray() ?? Array.Empty<ESAssetConfigGenerationRecord>();
                fingerprint = state?.CatalogFingerprint ?? string.Empty;
                sourceGeneration = state?.Generation ?? 0;
            }

            ESAssetConfigTableGenerationState candidate = BuildCandidateFromGenerationRecords(
                records,
                sourceGeneration,
                out _);
            lock (commitSync)
                CommitCandidateLocked(candidate, fingerprint, provider, providerGeneration);
        }

        public static void ClearAssetConfigTables()
        {
            InvalidateAssetConfigTableBinding();
        }

        internal static void CommitOrStageCandidate(
            ESAssetConfigTableGenerationState candidate,
            string catalogFingerprint)
        {
            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));
            lock (commitSync)
            {
                if (providerCandidateBuildActive && activeProvider == null)
                {
                    DisposePendingCandidateLocked();
                    pendingProviderCandidate = candidate;
                    pendingProviderFingerprint = catalogFingerprint ?? string.Empty;
                    return;
                }
                CommitCandidateLocked(candidate, catalogFingerprint, activeProvider, activeProviderGeneration);
            }
        }

        public static void ResetAllAssetLoaders()
        {
            InvalidateAssetConfigTableBinding();
        }

        private static void CommitCandidateLocked(
            ESAssetConfigTableGenerationState candidate,
            string catalogFingerprint,
            IESAssetRuntimeProvider provider,
            int providerGeneration)
        {
            long currentGeneration = currentState?.Generation ?? 0;
            if (candidate.BaseGeneration != currentGeneration)
            {
                candidate.Retire();
                throw new InvalidOperationException(
                    "Asset ConfigTable 候选代已过期：构建基线 " + candidate.BaseGeneration
                    + "，当前权威代 " + currentGeneration + "。");
            }

            long nextGeneration = currentGeneration == long.MaxValue
                ? 1
                : currentGeneration + 1;
            try
            {
                candidate.PrepareForCommit(nextGeneration, catalogFingerprint);
                if (provider != null)
                    candidate.BindProvider(provider, providerGeneration);
                candidate.Publish();
            }
            catch
            {
                candidate.Retire();
                throw;
            }

            BeginAuthorityMutationLocked();
            ESAssetConfigTableGenerationState previous = null;
            try
            {
                previous = Interlocked.Exchange(ref currentState, candidate);
            }
            catch
            {
                EndAuthorityMutationLocked(true);
                candidate.Retire();
                throw;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(catalogFingerprint))
                    editorCatalogCommitGeneration = editorCatalogCommitGeneration == long.MaxValue
                        ? 1
                        : editorCatalogCommitGeneration + 1;
                RetireStateLocked(previous);
            }
            catch (Exception exception)
            {
                reclamationFailureCount++;
                lastReclamationFailure = "Asset ConfigTable 新代已提交，但旧代退休登记失败：" + exception;
                Debug.LogException(exception);
            }
            finally { EndAuthorityMutationLocked(true); }
        }

        private static void BeginAuthorityMutationLocked()
        {
            Volatile.Write(ref assetConfigTablesAvailable, false);
            Interlocked.Increment(ref authorityEpoch);
        }

        private static void EndAuthorityMutationLocked(bool available)
        {
            Interlocked.Increment(ref authorityEpoch);
            Volatile.Write(ref assetConfigTablesAvailable, available);
        }

        private static void RetireStateLocked(ESAssetConfigTableGenerationState state)
        {
            if (state == null || state.IsRetired)
                return;
            retiredStates.Add(state);
            state.Retire();
        }

        private static void DisposePendingCandidateLocked()
        {
            ESAssetConfigTableGenerationState pending = pendingProviderCandidate;
            pendingProviderCandidate = null;
            pendingProviderFingerprint = string.Empty;
            pending?.Retire();
        }

        private static void OnGenerationReclaimed(
            ESAssetConfigTableGenerationState state,
            Exception failure)
        {
            lock (commitSync)
            {
                if (failure == null)
                {
                    retiredStates.Remove(state);
                    return;
                }
                reclamationFailureCount++;
                lastReclamationFailure = failure.ToString();
                Debug.LogException(failure);
            }
        }

#if UNITY_EDITOR
        [MenuItem("【ES】/资源与发布/索引与注册/从 AssetLibrary 重建编辑器查询表")]
        public static void MenuRebuildEditorConfigQueryTableFromLibraries()
        {
            ESAssetAutoRegisterReport report = RebuildEditorConfigQueryTableFromLibraries(true, true);
            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString());
        }

        public static ESAssetAutoRegisterReport RebuildEditorConfigQueryTableFromLibraries(bool rebuildAssetConfigTables = true, bool clearBeforeBuild = true)
        {
            ESAssetAutoRegisterReport report = new ESAssetAutoRegisterReport();
            List<ESAssetLibrary> indexedLibraries = ESEditorSO.GetGroupOfType<ESAssetLibrary>() ?? new List<ESAssetLibrary>(0);
            List<ESAssetLibrary> libraries = new List<ESAssetLibrary>(indexedLibraries.Count);
            for (int i = 0; i < indexedLibraries.Count; i++)
            {
                ESAssetLibrary library = indexedLibraries[i];
                if (library == null)
                    continue;

                if (libraries.Contains(library))
                    continue;

                report.normalizedPageCount += library.NormalizePagesEditor();
                libraries.Add(library);
                EditorUtility.SetDirty(library);
            }

            report.libraryCount = libraries.Count;
            ESAssetRegistry.BuildFromAssetLibraries(libraries, clearBeforeBuild);
            report.registeredPageCount = ESAssetRegistry.EditorConfigQueryTable.Count;

            if (rebuildAssetConfigTables)
                RebuildAssetConfigTablesFromPages(ESAssetRegistry.Pages);

            report.conflictCount = GetAssetConflictCount();
            report.conflictReport = GetAssetConflictReport();
            return report;
        }

        public static int RebuildAssetConfigTablesFromPages(IReadOnlyList<ESAssetPage> pages)
        {
            var records = new List<ESAssetConfigGenerationRecord>(pages?.Count ?? 0);
            if (pages != null)
            {
                for (int i = 0; i < pages.Count; i++)
                {
                    ESAssetPage page = pages[i];
                    if (page == null)
                        continue;
                    if (page.Kind == ESAssetReferKind.ScriptableObject && page.OB is ScriptableObject scriptableObject
                        && ESScriptableObjectClassification.GetClass(scriptableObject) == ESScriptableObjectClass.GameCore)
                        continue;
                    if (!ESAssetReferConfigKeySwitch.IsSupportedKind(page.Kind))
                        throw new InvalidOperationException("AssetPage 包含不受支持的资源类型：" + page.Kind);

                    ESAssetConfigRecord record = CreateAssetConfigRecord(page);
                    records.Add(new ESAssetConfigGenerationRecord(page.Kind, in record));
                }
            }

            ESAssetConfigTableGenerationState candidate = BuildCandidateFromGenerationRecords(records, out ESAssetCatalogBuildValidation validation);
            if (validation.conflictCount != 0
                || validation.expectedBusinessEntries == 0
                || validation.candidateEntries != validation.expectedBusinessEntries)
            {
                candidate.Retire();
                throw new InvalidOperationException(
                    "AssetPage 候选表为空、不完整或存在冲突：期望 " + validation.expectedBusinessEntries
                    + " 项，候选 " + validation.candidateEntries + " 项，冲突 " + validation.conflictCount
                    + " 项。\n" + validation.conflictReport);
            }
            CommitOrStageCandidate(candidate, string.Empty);
            LogEquivalentDuplicateWarnings("AssetPage", validation);
            return validation.candidateEntries;
        }

        private static ESAssetConfigRecord CreateAssetConfigRecord(ESAssetPage page)
        {
            return new ESAssetConfigRecord(
                page.EnumKey,
                page.EffectiveStringKey,
                page.AssetGuid,
                page.LocalFileId,
                page.AssetTypeName,
                page.AssetPath,
                page.Name,
                page.SourceLibrary);
        }
#endif

        public static int RebuildAssetConfigTablesFromCatalogs(IReadOnlyList<ESRuntimeCatalog> catalogs)
        {
            List<ESAssetConfigGenerationRecord> records = CollectCatalogRecords(catalogs);
            ESAssetConfigTableGenerationState candidate = BuildCandidateFromGenerationRecords(records, out ESAssetCatalogBuildValidation validation);
            if (validation.conflictCount != 0
                || validation.expectedBusinessEntries == 0
                || validation.candidateEntries != validation.expectedBusinessEntries)
            {
                candidate.Retire();
                throw new InvalidOperationException(
                    "Catalog 候选表为空、不完整或存在冲突：期望 " + validation.expectedBusinessEntries
                    + " 项，候选 " + validation.candidateEntries + " 项，冲突 " + validation.conflictCount
                    + " 项。\n" + validation.conflictReport);
            }
            CommitOrStageCandidate(candidate, string.Empty);
            LogEquivalentDuplicateWarnings("Catalog", validation);
            return validation.candidateEntries;
        }

        public static bool TryValidateAssetConfigTablesFromCatalogs(
            IReadOnlyList<ESRuntimeCatalog> catalogs,
            out ESAssetCatalogBuildValidation validation,
            out string error)
        {
            validation = new ESAssetCatalogBuildValidation();
            error = string.Empty;
            try
            {
                List<ESAssetConfigGenerationRecord> records = CollectCatalogRecords(catalogs);
                ESAssetConfigTableGenerationState candidate = BuildCandidateFromGenerationRecords(records, out validation);
                candidate.Retire();
                if (validation.conflictCount > 0)
                    error = "ConfigKey/ConfigData 候选表存在冲突：" + validation.conflictCount + " 项。\n" + validation.conflictReport;
                else if (validation.expectedBusinessEntries == 0)
                    error = "Editor Catalog 不包含可注入的业务资源。";
                else if (validation.candidateEntries != validation.expectedBusinessEntries)
                    error = "ConfigKey/ConfigData 候选表不完整：期望 " + validation.expectedBusinessEntries
                        + " 项，候选 " + validation.candidateEntries + " 项。";

                return string.IsNullOrEmpty(error);
            }
            catch (Exception exception)
            {
                error = "ConfigKey/ConfigData 候选表预检失败：" + exception.Message;
                return false;
            }
        }

        public static bool CommitValidatedAssetConfigTablesFromCatalogs(
            IReadOnlyList<ESRuntimeCatalog> catalogs,
            ESAssetCatalogBuildValidation validation,
            string catalogSetFingerprint,
            out int injectedEntries,
            out string error)
        {
            injectedEntries = 0;
            error = string.Empty;
            if (!validation.IsValid)
            {
                error = "ConfigKey/ConfigData 候选表未通过预检，拒绝提交。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(catalogSetFingerprint))
            {
                error = "ConfigKey/ConfigData 候选表缺少 Catalog 集合指纹，拒绝提交。";
                return false;
            }

            try
            {
                List<ESAssetConfigGenerationRecord> records = CollectCatalogRecords(catalogs);
                ESAssetConfigTableGenerationState candidate = BuildCandidateFromGenerationRecords(
                    records,
                    out ESAssetCatalogBuildValidation commitValidation);
                injectedEntries = commitValidation.candidateEntries;
                if (!commitValidation.IsValid
                    || commitValidation.sourceBusinessEntries != validation.sourceBusinessEntries
                    || commitValidation.expectedBusinessEntries != validation.expectedBusinessEntries
                    || commitValidation.candidateEntries != validation.candidateEntries
                    || commitValidation.equivalentDuplicateCount != validation.equivalentDuplicateCount)
                {
                    candidate.Retire();
                    error = "ConfigKey/ConfigData 候选提交结果不完整：期望 " + validation.expectedBusinessEntries
                        + " 项，实际 " + injectedEntries + " 项，冲突 " + commitValidation.conflictCount + " 项。\n"
                        + commitValidation.conflictReport;
                    return false;
                }

                CommitOrStageCandidate(candidate, catalogSetFingerprint);
                LogEquivalentDuplicateWarnings("Catalog", commitValidation);
                return true;
            }
            catch (Exception exception)
            {
                error = "ConfigKey/ConfigData 候选提交失败：" + exception.Message;
                return false;
            }
        }

        private static List<ESAssetConfigGenerationRecord> CollectCatalogRecords(
            IReadOnlyList<ESRuntimeCatalog> catalogs)
        {
            var records = new List<ESAssetConfigGenerationRecord>();
            if (catalogs == null)
                return records;

            for (int catalogIndex = 0; catalogIndex < catalogs.Count; catalogIndex++)
            {
                ESRuntimeCatalog catalog = catalogs[catalogIndex];
                if (catalog?.assets == null)
                    continue;
                for (int assetIndex = 0; assetIndex < catalog.assets.Count; assetIndex++)
                {
                    ESRuntimeCatalogEntry entry = catalog.assets[assetIndex];
                    if (entry == null || !entry.isBusinessAsset || entry.identity == null || !entry.identity.IsValid)
                        continue;
                    if (!Enum.TryParse(entry.kind, out ESAssetReferKind kind)
                        || !ESAssetReferConfigKeySwitch.IsSupportedKind(kind))
                        throw new InvalidOperationException("Catalog 业务资产包含不受支持的资源类型：" + entry.kind);
                    ESAssetConfigRecord record = CreateAssetConfigRecord(entry);
                    records.Add(new ESAssetConfigGenerationRecord(kind, in record));
                }
            }
            return records;
        }

        internal static ESAssetConfigTableGenerationState BuildCandidateFromGenerationRecords(
            IReadOnlyList<ESAssetConfigGenerationRecord> source,
            out ESAssetCatalogBuildValidation validation)
            => BuildCandidateFromGenerationRecords(source, AssetConfigTableGeneration, out validation);

        private static ESAssetConfigTableGenerationState BuildCandidateFromGenerationRecords(
            IReadOnlyList<ESAssetConfigGenerationRecord> source,
            long baseGeneration,
            out ESAssetCatalogBuildValidation validation)
        {
            var candidate = new ESAssetConfigTableGenerationState(baseGeneration, OnGenerationReclaimed);
            int sourceCount = source?.Count ?? 0;
            var accepted = new List<ESAssetConfigGenerationRecord>(sourceCount);
            var canonical = new List<ESAssetConfigGenerationRecord>(sourceCount);
            var registrationOwners = new Dictionary<AssetBusinessRegistrationKey, ESAssetConfigGenerationRecord>(sourceCount);
            var equivalentDuplicates = new List<string>();
            try
            {
                candidate.BeginBuild();
                if (source != null)
                {
                    for (int i = 0; i < source.Count; i++)
                    {
                        ESAssetConfigGenerationRecord item = source[i];
                        if (TryConsumeEquivalentDuplicate(
                            registrationOwners,
                            in item,
                            equivalentDuplicates))
                        {
                            continue;
                        }

                        canonical.Add(item);
                        if (RegisterGenerationRecord(candidate, item.Kind, in item.Record))
                            accepted.Add(item);
                    }
                }
                candidate.CompleteBuild(accepted);
                validation = new ESAssetCatalogBuildValidation
                {
                    sourceBusinessEntries = sourceCount,
                    expectedBusinessEntries = canonical.Count,
                    candidateEntries = candidate.RegisteredCount,
                    equivalentDuplicateCount = equivalentDuplicates.Count,
                    equivalentDuplicateReport = string.Join("\n", equivalentDuplicates),
                    conflictCount = candidate.ConflictCount,
                    conflictReport = candidate.GetConflictReport()
                };
                return candidate;
            }
            catch
            {
                candidate.Retire();
                throw;
            }
        }

        private static bool TryConsumeEquivalentDuplicate(
            Dictionary<AssetBusinessRegistrationKey, ESAssetConfigGenerationRecord> owners,
            in ESAssetConfigGenerationRecord incoming,
            List<string> report)
        {
            ESAssetConfigRecord record = incoming.Record;
            if (!ESConfigKeyMatch.IsConfigured(record.enumKey, record.stringKey))
                return false;

            var key = new AssetBusinessRegistrationKey(incoming.Kind, record.enumKey, record.stringKey);
            if (!owners.TryGetValue(key, out ESAssetConfigGenerationRecord existing))
            {
                owners.Add(key, incoming);
                return false;
            }

            if (!AreEquivalentAssetRegistrations(in existing.Record, in record))
                return false;

            report.Add(
                "Kind=" + incoming.Kind
                + ", EnumKey=" + record.enumKey
                + ", StringKey=" + (record.stringKey ?? string.Empty)
                + ", Identity=" + record.assetGuid + ":" + record.assetLocalFileId
                + ", Sources=" + DescribeAssetRegistration(existing.Record)
                + " / " + DescribeAssetRegistration(record));
            return true;
        }

        private static bool AreEquivalentAssetRegistrations(
            in ESAssetConfigRecord left,
            in ESAssetConfigRecord right)
        {
            return !string.IsNullOrEmpty(left.assetGuid)
                   && !string.IsNullOrEmpty(right.assetGuid)
                   && left.assetLocalFileId >= 0
                   && right.assetLocalFileId >= 0
                   && string.Equals(left.assetGuid, right.assetGuid, StringComparison.Ordinal)
                   && left.assetLocalFileId == right.assetLocalFileId
                   && string.Equals(left.assetTypeName, right.assetTypeName, StringComparison.Ordinal);
        }

        private static string DescribeAssetRegistration(in ESAssetConfigRecord record)
        {
            return (record.sourceLibrary ?? string.Empty) + "/" + (record.displayName ?? string.Empty);
        }

        private static void LogEquivalentDuplicateWarnings(
            string sourceName,
            in ESAssetCatalogBuildValidation validation)
        {
            if (validation.equivalentDuplicateCount == 0)
                return;

            Debug.LogWarning(
                "[ESRes][" + sourceName + "] 合并 " + validation.equivalentDuplicateCount
                + " 条同键同身份的等价重复注册。源记录 " + validation.sourceBusinessEntries
                + " 条，唯一记录 " + validation.expectedBusinessEntries + " 条。\n"
                + validation.equivalentDuplicateReport);
        }

        private static bool RegisterGenerationRecord(
            ESAssetConfigTableGenerationState state,
            ESAssetReferKind kind,
            in ESAssetConfigRecord record)
        {
            switch (kind)
            {
                case ESAssetReferKind.Prefab: return RegisterRecord<ESAssetReferPrefabConfigData, ESAssetReferPrefabConfigKey, GameObject>(in record, state.Prefabs, (data, key) => data.runtimeKey = key);
                case ESAssetReferKind.Scene: return RegisterRecord<ESAssetReferSceneConfigData, ESAssetReferSceneConfigKey, UnityEngine.Object>(in record, state.Scenes, (data, key) => data.runtimeKey = key);
                case ESAssetReferKind.Sprite: return RegisterRecord<ESAssetReferSpriteConfigData, ESAssetReferSpriteConfigKey, Sprite>(in record, state.Sprites, (data, key) => data.runtimeKey = key);
                case ESAssetReferKind.Texture2D: return RegisterRecord<ESAssetReferTexture2DConfigData, ESAssetReferTexture2DConfigKey, Texture2D>(in record, state.Texture2Ds, (data, key) => data.runtimeKey = key);
                case ESAssetReferKind.Texture: return RegisterRecord<ESAssetReferTextureConfigData, ESAssetReferTextureConfigKey, Texture>(in record, state.Textures, (data, key) => data.runtimeKey = key);
                case ESAssetReferKind.SpriteAtlas: return RegisterRecord<ESAssetReferSpriteAtlasConfigData, ESAssetReferSpriteAtlasConfigKey, UnityEngine.U2D.SpriteAtlas>(in record, state.SpriteAtlases, (data, key) => data.runtimeKey = key);
                case ESAssetReferKind.Material: return RegisterRecord<ESAssetReferMaterialConfigData, ESAssetReferMaterialConfigKey, Material>(in record, state.Materials, (data, key) => data.runtimeKey = key);
                case ESAssetReferKind.Mesh: return RegisterRecord<ESAssetReferMeshConfigData, ESAssetReferMeshConfigKey, Mesh>(in record, state.Meshes, (data, key) => data.runtimeKey = key);
                case ESAssetReferKind.AnimationClip: return RegisterRecord<ESAssetReferAnimationClipConfigData, ESAssetReferAnimationClipConfigKey, AnimationClip>(in record, state.AnimationClips, (data, key) => data.runtimeKey = key);
                case ESAssetReferKind.AnimatorController: return RegisterRecord<ESAssetReferAnimatorControllerConfigData, ESAssetReferAnimatorControllerConfigKey, RuntimeAnimatorController>(in record, state.AnimatorControllers, (data, key) => data.runtimeKey = key);
                case ESAssetReferKind.Avatar: return RegisterRecord<ESAssetReferAvatarConfigData, ESAssetReferAvatarConfigKey, Avatar>(in record, state.Avatars, (data, key) => data.runtimeKey = key);
                case ESAssetReferKind.AudioClip: return RegisterRecord<ESAssetReferAudioClipConfigData, ESAssetReferAudioClipConfigKey, AudioClip>(in record, state.AudioClips, (data, key) => data.runtimeKey = key);
                case ESAssetReferKind.VideoClip: return RegisterRecord<ESAssetReferVideoClipConfigData, ESAssetReferVideoClipConfigKey, UnityEngine.Video.VideoClip>(in record, state.VideoClips, (data, key) => data.runtimeKey = key);
                case ESAssetReferKind.TimelineAsset: return RegisterRecord<ESAssetReferTimelineAssetConfigData, ESAssetReferTimelineAssetConfigKey, UnityEngine.Object>(in record, state.TimelineAssets, (data, key) => data.runtimeKey = key);
                case ESAssetReferKind.PlayableAsset: return RegisterRecord<ESAssetReferPlayableAssetConfigData, ESAssetReferPlayableAssetConfigKey, UnityEngine.Playables.PlayableAsset>(in record, state.PlayableAssets, (data, key) => data.runtimeKey = key);
                case ESAssetReferKind.ScriptableObject: return RegisterRecord<ESAssetReferScriptableObjectConfigData, ESAssetReferScriptableObjectConfigKey, ScriptableObject>(in record, state.ScriptableObjects, (data, key) => data.runtimeKey = key);
                case ESAssetReferKind.TerrainData: return RegisterRecord<ESAssetReferTerrainDataConfigData, ESAssetReferTerrainDataConfigKey, TerrainData>(in record, state.TerrainDatas, (data, key) => data.runtimeKey = key);
                case ESAssetReferKind.Raw: return RegisterRecord<ESAssetReferRawConfigData, ESAssetReferRawConfigKey, TextAsset>(in record, state.RawAssets, (data, key) => data.runtimeKey = key);
                default: throw new InvalidOperationException("不受支持的 Asset ConfigTable 类型：" + kind);
            }
        }

        private static bool RegisterRecord<TData, TKey, TAsset>(
            in ESAssetConfigRecord record,
            ESAssetConfigKeyTable<TData, TAsset> table,
            Action<TData, int> setRuntimeKey)
            where TData : ESAssetReferConfigDataBase<TAsset>, IESAssetConfigDataInitializer<TKey>, new()
            where TKey : class, IESAssetConfigKeyInitializer, new()
            where TAsset : UnityEngine.Object
        {
            if (!ESConfigKeyMatch.IsConfigured(record.enumKey, record.stringKey))
                throw new InvalidOperationException("Catalog 业务资产缺少 EnumKey/StringKey：" + record.assetGuid);

            TKey key = new TKey();
            key.InitializeRuntimeKey(
                record.enumKey,
                record.stringKey,
                record.assetGuid,
                record.assetLocalFileId,
                record.assetTypeName,
                record.assetPath);
            if (!table.TryAcquireBuildRecord(
                    key,
                    AssetConfigDataFactory<TData>.Create,
                    record.assetGuid + ":" + record.assetLocalFileId,
                    out TData data))
                return false;

            data.InitializeFromRecord(key, in record);
            int runtimeKey = table.RegisterPreparedBuildRecord(key, data, record.stringKey);
            if (runtimeKey == 0)
                return false;
            setRuntimeKey(data, runtimeKey);
            return true;
        }

        private static ESAssetConfigRecord CreateAssetConfigRecord(ESRuntimeCatalogEntry entry)
        {
            return new ESAssetConfigRecord(
                entry.enumKey,
                entry.stringKey,
                entry.identity.guid,
                entry.identity.localFileId,
                entry.assetTypeName,
                null,
                entry.pageName,
                entry.libraryFolder);
        }

        private static class AssetConfigDataFactory<TData> where TData : new()
        {
            public static readonly Func<TData> Create = CreateInstance;

            private static TData CreateInstance()
            {
                return new TData();
            }
        }

        public static int GetAssetConflictCount()
        {
            if (!TryAcquireAssetConfigGeneration(false, out ESAssetConfigTableGenerationLease lease))
                return 0;
            try { return lease.State.ConflictCount; }
            finally { lease.Dispose(); }
        }

        public static string GetAssetConflictReport()
        {
            if (!TryAcquireAssetConfigGeneration(false, out ESAssetConfigTableGenerationLease lease))
                return string.Empty;
            try { return lease.State.GetConflictReport(); }
            finally { lease.Dispose(); }
        }

    }

    public sealed class ESConsumerResidentAssetPreloadReport
    {
        public int requestedCount;
        public int loadedCount;
        public int skippedCount;
        public readonly List<string> errors = new List<string>();
    }

    [Serializable, TypeRegistryItem("RuntimeData/Table")]
    public sealed class ESRuntimeDataModule : ESSystemModule
    {
        public static readonly ESBuffConfigKeyTable BuffTable = ESRuntimeDataGameCore.Buffs;
        public static readonly ESShotConfigKeyTable ShotTable = ESRuntimeDataGameCore.Shots;
        public static readonly ESMonsterConfigKeyTable MonsterTable = ESRuntimeDataGameCore.Monsters;
        public static readonly ESNpcConfigKeyTable NpcTable = ESRuntimeDataGameCore.Npcs;
        public static readonly ESWeaponConfigKeyTable WeaponTable = ESRuntimeDataGameCore.Weapons;
        public static readonly ESSkillConfigKeyTable SkillTable = ESRuntimeDataGameCore.Skills;
        public static readonly ESAudioCueConfigKeyTable AudioCueTable = ESRuntimeDataGameCore.AudioCues;
        public static readonly ESVfxConfigKeyTable VfxTable = ESRuntimeDataGameCore.Vfx;
        public static readonly ESActionConfigKeyTable ActionTable = ESRuntimeDataGameCore.Actions;
        public static readonly ESSkillTrackConfigKeyTable SkillTrackTable = ESRuntimeDataGameCore.SkillTracks;
        public static readonly ESRuntimeInstanceIndex<ESActiveBuffRuntime> BuffInstanceIndex = new ESRuntimeInstanceIndex<ESActiveBuffRuntime>(128);
        public static readonly ESRuntimeInstanceIndex<Item> ShotInstanceIndex = new ESRuntimeInstanceIndex<Item>(128);

        [ShowInInspector, ReadOnly, LabelText("Buff Table")]
        public readonly ESBuffConfigKeyTable Buffs = BuffTable;
        [ShowInInspector, ReadOnly, LabelText("\u98de\u884c\u7269\u8868")]
        public readonly ESShotConfigKeyTable Shots = ShotTable;

        [ShowInInspector, ReadOnly, LabelText("Monster Table")]
        public readonly ESMonsterConfigKeyTable Monsters = MonsterTable;

        [ShowInInspector, ReadOnly, LabelText("NPC Table")]
        public readonly ESNpcConfigKeyTable Npcs = NpcTable;

        [ShowInInspector, ReadOnly, LabelText("Weapon Table")]
        public readonly ESWeaponConfigKeyTable Weapons = WeaponTable;
        [ShowInInspector, ReadOnly, LabelText("Audio Cue Table")]
        public readonly ESAudioCueConfigKeyTable AudioCues = AudioCueTable;
        [ShowInInspector, ReadOnly, LabelText("VFX Table")]
        public readonly ESVfxConfigKeyTable Vfx = VfxTable;
        [ShowInInspector, ReadOnly, LabelText("\u6280\u80fd\u8868")]
        public readonly ESSkillConfigKeyTable Skills = SkillTable;
        [ShowInInspector, ReadOnly, LabelText("Action Table")]
        public readonly ESActionConfigKeyTable Actions = ActionTable;
        [ShowInInspector, ReadOnly, LabelText("SkillTrack Table")]
        public readonly ESSkillTrackConfigKeyTable SkillTracks = SkillTrackTable;
        [ShowInInspector, ReadOnly, LabelText("Buff\u5b9e\u4f8b\u7d22\u5f15")]
        public readonly ESRuntimeInstanceIndex<ESActiveBuffRuntime> BuffInstances = BuffInstanceIndex;

        [ShowInInspector, ReadOnly, LabelText("Shot Instance Index")]
        public readonly ESRuntimeInstanceIndex<Item> ShotInstances = ShotInstanceIndex;

        [ShowInInspector, ReadOnly, LabelText("Building")]
        private static bool isBuilding;

        [NonSerialized]
        private ESRuntimeDataAssetLoadingService assetLoadingService;
        [NonSerialized]
        private ESAssetScope consumerResidentAssetScope;
        [NonSerialized]
        private ESAssetScope consumerGameCoreAssetScope;
        [NonSerialized]
        private ESGlobalResSetting activeReleaseSettings;
        [NonSerialized]
        private ESRuntimeReleaseDownloadResult activeReleaseResult;
        [NonSerialized]
        private readonly SemaphoreSlim releaseStateGate = new SemaphoreSlim(1, 1);
        [NonSerialized]
        private readonly HashSet<string> activeConsumerIds = new HashSet<string>(StringComparer.Ordinal);
        [NonSerialized]
        private readonly HashSet<string> activeLibraryKeys = new HashSet<string>(StringComparer.Ordinal);
        [NonSerialized]
        private long releaseGeneration;

        public ESConsumerResidentAssetPreloadReport LastResidentAssetPreloadReport { get; private set; }

        public bool IsBuilding => isBuilding;
        public static bool IsBuildingStatic => isBuilding;
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public ESRuntimeDataAssetLoadingService ExistingAssetLoadingService => assetLoadingService;
        public ESRuntimeDataAssetLoadingService AssetLoadingService => assetLoadingService ??= new ESRuntimeDataAssetLoadingService();

        public void InitializeAssetLoading(ESGlobalAssetRuntimeMap manifest, IESRuntimeAssetBundleProvider provider, ESRuntimeRetryPolicy retryPolicy)
        {
            DisposeConsumerStartupAssets();
            AssetLoadingService.Initialize(manifest, provider, retryPolicy);
        }

        public void InitializeAssetLoading(IESAssetRuntimeProvider provider)
        {
            DisposeConsumerStartupAssets();
            AssetLoadingService.Initialize(provider);
        }

        public void InitializeAssetLoadingForRunMode(ESGlobalAssetRuntimeMap manifest, ESGlobalResSetting settings, ESRuntimeRetryPolicy retryPolicy)
        {
            DisposeConsumerStartupAssets();
            AssetLoadingService.Initialize(ESAssetRuntimeProviderFactory.Create(manifest, settings, retryPolicy));
        }
        /// <summary>Explicit entry for the current release pipeline.</summary>
        public async UniTask<ESRuntimeReleaseDownloadResult> InitializeAssetLoadingFromReleaseAsync(ESGlobalResSetting settings, CancellationToken cancellationToken = default)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            ESAssetRunMode runMode = ESAssetRunModeSession.Lock(settings);
            if (runMode != ESAssetRunMode.LocalBuild && runMode != ESAssetRunMode.HotUpdate)
                throw new InvalidOperationException($"\u53d1\u5e03\u8d44\u6e90\u94fe\u53ea\u652f\u6301 LocalBuild \u6216 HotUpdate\uff0c\u5f53\u524d\u6a21\u5f0f\u4e3a {runMode}\u3002");

            var result = await ESRuntimeReleaseBootstrap.InitializeAsync(settings, cancellationToken);
            await InitializeAssetLoadingFromReleaseResultAsync(settings, result, cancellationToken);
            return result;
        }

        public async UniTask InitializeAssetLoadingFromReleaseResultAsync(ESGlobalResSetting settings, ESRuntimeReleaseDownloadResult result, CancellationToken cancellationToken = default)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (result == null || result.RuntimeMap == null) throw new ArgumentNullException(nameof(result));
            await releaseStateGate.WaitAsync(cancellationToken);
            try
            {
                // Acquire the single transition gate before invalidating the active release.
                // If cancellation happens while another activation owns the gate, the old
                // Provider and its matching active result remain a valid pair; invalidating
                // before the wait would leave ESAssets Ready with a cleared release state.
                long generation = InvalidateActiveReleaseState();
                EnsureReleaseGeneration(generation);
                await InitializeAssetLoadingFromReleaseResultCoreAsync(
                    settings,
                    result,
                    cancellationToken,
                    preserveOnDemandActivationState: false,
                    expectedReleaseGeneration: generation);
            }
            finally { releaseStateGate.Release(); }
        }

        private async UniTask InitializeAssetLoadingFromReleaseResultCoreAsync(
            ESGlobalResSetting settings,
            ESRuntimeReleaseDownloadResult result,
            CancellationToken cancellationToken,
            bool preserveOnDemandActivationState,
            long expectedReleaseGeneration)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (result == null || result.RuntimeMap == null) throw new ArgumentNullException(nameof(result));
            EnsureReleaseGeneration(expectedReleaseGeneration);

            try
            {
                // A complete Bootstrap creates a new Provider/RuntimeMap. Its result contains
                // boot-required content only, so every previous on-demand activation marker is
                // invalid even when the release version is unchanged. Only the incremental merge
                // path is allowed to preserve these markers.
                if (preserveOnDemandActivationState)
                {
                    // The current Provider is about to be replaced as well. Keep the activation
                    // markers for a successful merge, but make a failed transition fail closed.
                    activeReleaseSettings = null;
                    activeReleaseResult = null;
                }
                DisposeConsumerStartupAssets();
                IESAssetRuntimeProvider provider = ESAssetRuntimeProviderFactory.Create(result.RuntimeMap, settings, ESRuntimeRetryPolicy.Default);
                await AssetLoadingService.InitializeAsync(
                    provider,
                    () => ESRuntimeDataAsset.RebuildAssetConfigTablesFromCatalogs(result.Catalogs),
                    cancellationToken);
                await PreloadConsumerResidentAssetsAsync(result.ResidentAssets, cancellationToken);
                await PreloadGameCoreAssetsAsync(result.GameCoreAssets, cancellationToken);
                EnsureReleaseGeneration(expectedReleaseGeneration);

                // Only this point means the release is genuinely usable: its Provider is attached,
                // resident assets are retained, and GameCore tables have injected successfully.
                activeReleaseSettings = settings;
                activeReleaseResult = result;
                if (ESAssetRunModeSession.Lock(settings) == ESAssetRunMode.HotUpdate
                    && !ESRuntimeReleaseDownloader.TryCommitLastKnownGood(settings, result.ReleaseVersion, out string fallbackError))
                    Debug.LogWarning("[ESRes][Release] 无法提交离线回退版本：" + fallbackError);
            }
            catch
            {
                // Provider attachment happens before resident/GameCore warm-up because those
                // assets must load through the new provider. Any failure before the commit
                // point must therefore tear that provider back down as well; otherwise
                // ESAssets can remain Ready while activeReleaseResult is null or incomplete.
                activeReleaseSettings = null;
                activeReleaseResult = null;
                activeConsumerIds.Clear();
                activeLibraryKeys.Clear();
                try
                {
                    AssetLoadingService.Dispose();
                }
                catch (Exception cleanupException)
                {
                    Debug.LogException(cleanupException);
                }
                throw;
            }
        }

        /// <summary>
        /// Ensures one declared Consumer and all of its dependencies are downloaded and active.
        /// It is the business-facing on-demand package API; map merging, Provider replacement,
        /// code package initialization and Scope/Plan recovery remain framework responsibilities.
        /// Call this at a loading screen or another resource safe point.
        /// </summary>
        public async UniTask EnsureConsumerAvailableAsync(string consumerId, CancellationToken cancellationToken = default)
        {
            string key = NormalizeOnDemandId(consumerId, nameof(consumerId));
            await releaseStateGate.WaitAsync(cancellationToken);
            try
            {
                EnsureOnDemandReleaseReady();
                if (activeConsumerIds.Contains(key))
                    return;
                long generation = releaseGeneration;
                ESGlobalResSetting settings = activeReleaseSettings;
                ESRuntimeReleaseDownloadResult current = activeReleaseResult;
                var downloader = new ESRuntimeReleaseDownloader(settings, ESAssetRunModeSession.Lock(settings));
                ESRuntimeReleaseDownloadResult addition = await downloader.DownloadConsumerAsync(key, cancellationToken);
                await ActivateReleaseAdditionAsync(settings, current, generation, addition, cancellationToken);
                EnsureReleaseGeneration(generation);
                activeConsumerIds.Add(key);
            }
            finally { releaseStateGate.Release(); }
        }

        /// <summary>Ensures one Library declared by a Consumer is downloaded and active without
        /// replacing any already active Consumer/Library content.</summary>
        public async UniTask EnsureLibraryAvailableAsync(string consumerId, string libraryFolder, CancellationToken cancellationToken = default)
        {
            string consumerKey = NormalizeOnDemandId(consumerId, nameof(consumerId));
            string libraryKey = NormalizeOnDemandId(libraryFolder, nameof(libraryFolder));
            string activeKey = consumerKey + "/" + libraryKey;
            await releaseStateGate.WaitAsync(cancellationToken);
            try
            {
                EnsureOnDemandReleaseReady();
                if (activeLibraryKeys.Contains(activeKey))
                    return;
                long generation = releaseGeneration;
                ESGlobalResSetting settings = activeReleaseSettings;
                ESRuntimeReleaseDownloadResult current = activeReleaseResult;
                var downloader = new ESRuntimeReleaseDownloader(settings, ESAssetRunModeSession.Lock(settings));
                ESRuntimeReleaseDownloadResult addition = await downloader.DownloadLibraryAsync(consumerKey, libraryKey, cancellationToken);
                await ActivateReleaseAdditionAsync(settings, current, generation, addition, cancellationToken);
                EnsureReleaseGeneration(generation);
                activeLibraryKeys.Add(activeKey);
            }
            finally { releaseStateGate.Release(); }
        }

        /// <summary>Content Info entry point. The binding's Consumer SO is editor authority only;
        /// Player execution uses its baked ID and a caller-owned lifecycle Scope.</summary>
        public async UniTask EnterContentResourcesAsync(ESContentResourceBinding binding, ESAssetScope lifetimeScope, CancellationToken cancellationToken = default)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            if (lifetimeScope == null || lifetimeScope.IsDisposed) throw new ObjectDisposedException(nameof(lifetimeScope));
            if (binding.RequiresConsumer && !binding.HasBakedConsumerId)
                throw new InvalidOperationException("[ESRes][Content] Consumer 配置尚未烘焙，请重新执行资源烘焙。");
            if (binding.HasBakedConsumerId)
                await EnsureConsumerAvailableAsync(binding.BakedConsumerId, cancellationToken);
            if (binding.ActivePlan != null)
                await ESGameManager.ResourcePlans.ApplyAsync(binding.ActivePlan, lifetimeScope, cancellationToken);
        }

        /// <summary>Returns the active Plan retain associated with one content lifecycle Scope.
        /// Downloaded Consumer files are intentionally left in cache for later reuse.</summary>
        public async UniTask LeaveContentResourcesAsync(ESContentResourceBinding binding, ESAssetScope lifetimeScope, CancellationToken cancellationToken = default)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            if (lifetimeScope == null || lifetimeScope.IsDisposed) return;
            if (binding.ActivePlan != null)
                await ESGameManager.ResourcePlans.ReleaseAsync(binding.ActivePlan, lifetimeScope, cancellationToken);
        }

        /// <summary>Optional exit resources always use a separate transition Scope.</summary>
        public async UniTask EnterExitTransitionResourcesAsync(ESContentResourceBinding binding, ESAssetScope transitionScope, CancellationToken cancellationToken = default)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            if (transitionScope == null || transitionScope.IsDisposed) throw new ObjectDisposedException(nameof(transitionScope));
            if (binding.ExitTransitionPlan != null)
                await ESGameManager.ResourcePlans.ApplyAsync(binding.ExitTransitionPlan, transitionScope, cancellationToken);
        }

        private void EnsureOnDemandReleaseReady()
        {
            if (activeReleaseSettings == null || activeReleaseResult == null || activeReleaseResult.RuntimeMap == null)
                throw new InvalidOperationException("[ESRes][OnDemand] 当前尚未通过新版 Release Bootstrap 初始化，不能增量激活 Consumer/Library。");
        }

        private long InvalidateActiveReleaseState()
        {
            releaseGeneration = releaseGeneration == long.MaxValue ? 1 : releaseGeneration + 1;
            activeReleaseSettings = null;
            activeReleaseResult = null;
            activeConsumerIds.Clear();
            activeLibraryKeys.Clear();
            return releaseGeneration;
        }

        private void EnsureReleaseGeneration(long expectedReleaseGeneration)
        {
            if (releaseGeneration != expectedReleaseGeneration)
                throw new OperationCanceledException("[ESRes][Release] 资源发布代际已变化，丢弃迟到的旧初始化结果。");
        }

        private static string NormalizeOnDemandId(string value, string parameterName)
        {
            string normalized = value?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                throw new ArgumentException("资源标识不能为空。", parameterName);
            return normalized;
        }

        private async UniTask ActivateReleaseAdditionAsync(
            ESGlobalResSetting settings,
            ESRuntimeReleaseDownloadResult current,
            long expectedReleaseGeneration,
            ESRuntimeReleaseDownloadResult addition,
            CancellationToken cancellationToken)
        {
            EnsureReleaseGeneration(expectedReleaseGeneration);
            if (addition == null || addition.RuntimeMap == null)
                throw new InvalidOperationException("[ESRes][OnDemand] 下载结果缺少 RuntimeMap。");
            if (!string.Equals(current.ReleaseVersion, addition.ReleaseVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("[ESRes][OnDemand] 下载期间发布版本已变化；请在下一个安全点重新执行完整 Bootstrap。");

            // A changed code hash deliberately throws here: HybridCLR cannot replace an already
            // loaded assembly in-process, so the user gets the same explicit restart boundary as boot.
            await ESRuntimeReleaseBootstrap.InitializeAdditionalCodePackagesAsync(addition.DownloadedCodePackages, cancellationToken);
            EnsureReleaseGeneration(expectedReleaseGeneration);
            ESRuntimeReleaseDownloadResult merged = ESRuntimeReleaseDownloadResult.Merge(current, addition);
            await InitializeAssetLoadingFromReleaseResultCoreAsync(
                settings,
                merged,
                cancellationToken,
                preserveOnDemandActivationState: true,
                expectedReleaseGeneration: expectedReleaseGeneration);
        }

        /// <summary>Consumer 启动常驻资产在 GameCore 注入前加载；由模块持有到资源系统重置。</summary>
        public async UniTask<ESConsumerResidentAssetPreloadReport> PreloadConsumerResidentAssetsAsync(
            IEnumerable<ESRuntimeConsumerResidentAssetReference> assets, CancellationToken cancellationToken = default)
        {
            if (!AssetLoadingService.IsInitialized)
                throw new InvalidOperationException("[ESRes][Resident] 必须先初始化 Asset Provider。");

            consumerResidentAssetScope?.Dispose();
            consumerResidentAssetScope = ESAssets.CreateScope();
            var report = new ESConsumerResidentAssetPreloadReport();
            var identities = new HashSet<ESAssetIdentity>();
            try
            {
                foreach (ESRuntimeConsumerResidentAssetReference entry in assets ?? Array.Empty<ESRuntimeConsumerResidentAssetReference>())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (entry == null || !entry.IsValid || !identities.Add(new ESAssetIdentity(entry.guid, entry.localFileId)))
                    {
                        report.skippedCount++;
                        continue;
                    }

                    report.requestedCount++;
                    try
                    {
                        var refer = new ESAssetReferUnityObject();
                        refer.InitializeGeneratedReference(entry.guid, entry.localFileId, ESAssetReferKind.Other, 0, string.Empty);
                        await consumerResidentAssetScope.LoadAsync(refer, cancellationToken);
                        report.loadedCount++;
                    }
                    catch (Exception exception)
                    {
                        string message = "[ESRes][Resident] 启动常驻资产加载失败：GUID=" + entry.guid
                            + ", LocalFileId=" + entry.localFileId + ", Error=" + exception.Message;
                        report.errors.Add(message);
                        Debug.LogError(message);
                        throw;
                    }
                }
                LastResidentAssetPreloadReport = report;
                return report;
            }
            catch
            {
                DisposeConsumerResidentAssets();
                throw;
            }
        }

        private void DisposeConsumerResidentAssets()
        {
            consumerResidentAssetScope?.Dispose();
            consumerResidentAssetScope = null;
            LastResidentAssetPreloadReport = null;
        }

        private void DisposeConsumerStartupAssets()
        {
            DisposeConsumerGameCoreAssets();
            DisposeConsumerResidentAssets();
        }

        private void DisposeConsumerGameCoreAssets()
        {
            ESRuntimeDataGameCore.ResetForResourceTransition();
            consumerGameCoreAssetScope?.Dispose();
            consumerGameCoreAssetScope = null;
        }

        /// <summary>
        /// Preloads Consumer GameCore assets after the runtime provider is ready.
        /// Each IGameCoreSO injects its own target GameCore table.
        /// </summary>
        public async UniTask<ESGameCoreAssetPreloadReport> PreloadGameCoreAssetsAsync(IEnumerable<ESRuntimeConsumerGameCoreReference> assets, CancellationToken cancellationToken = default)
        {
            if (!AssetLoadingService.IsInitialized) throw new InvalidOperationException("\u5fc5\u987b\u5148\u521d\u59cb\u5316\u65b0\u7248 Asset Provider\uff0c\u624d\u80fd\u9884\u70ed GameCore \u8d44\u4ea7\u3002");
            // GameCore 归属于当前 Consumer，而非默认全局 residentScope。重新加载 Consumer
            // 时整体清表、释放旧持有后再注入，避免跨 Provider 保留旧 SO。
            DisposeConsumerGameCoreAssets();
            consumerGameCoreAssetScope = ESAssets.CreateScope();
            var report = new ESGameCoreAssetPreloadReport();
            var identities = new HashSet<ESAssetIdentity>();
            bool gameCoreBuildOpen = false;
            try
            {
                if (isBuilding)
                    throw new InvalidOperationException("Consumer GameCore 预热不能嵌套到其他 RuntimeData 表构建事务中。");

                // The Consumer preload is one GameCore transaction, even though every SO owns
                // its own InjectGameCoreTables implementation. In particular, audio emitters
                // must not see the first Cue as a ready catalog while later Cues are still being
                // asynchronously loaded and injected.
                BeginBuildStatic(true);
                gameCoreBuildOpen = true;
                foreach (ESRuntimeConsumerGameCoreReference entry in OrderGameCoreAssets(assets))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (entry == null || !entry.IsValid || !identities.Add(new ESAssetIdentity(entry.guid, entry.localFileId)))
                    {
                        report.skippedCount++;
                        continue;
                    }

                    report.requestedCount++;
                    try
                    {
                        var refer = new ESAssetReferScriptableObject();
                        refer.InitializeGeneratedReference(entry.guid, entry.localFileId, ESAssetReferKind.ScriptableObject, 0, string.Empty);
                        ScriptableObject asset = await consumerGameCoreAssetScope.LoadAsync(refer, cancellationToken);
                        if (!(asset is IGameCoreSO gameCore))
                            throw new InvalidOperationException("Consumer GameCore \u8d44\u4ea7\u672a\u5b9e\u73b0 IGameCoreSO\uff1a" + entry.guid);
                        gameCore.InjectGameCoreTables();
                        report.loadedCount++;
                    }
                    catch (Exception exception)
                    {
                        report.failedCount++;
                        report.errors.Add(entry.guid + ":" + entry.localFileId + " : " + exception.Message);
                        throw;
                    }
                }

                EndBuildStatic();
                gameCoreBuildOpen = false;

                // ResourcePlanInfo is an IGameCoreSO: all plans have now registered their
                // target index. On first boot this enters Global; after a Provider replacement
                // it restores exactly the targets that were active before the transition.
                await AssetLoadingService.RestoreResourcePlansAfterGameCoreAsync(cancellationToken);
                return report;
            }
            catch
            {
                if (gameCoreBuildOpen)
                    EndBuildStatic(false);

                // 包括循环开始前的取消、依赖排序失败与任一注入失败；不能留下半张表。
                DisposeConsumerGameCoreAssets();
                throw;
            }
        }

        private static IReadOnlyList<ESRuntimeConsumerGameCoreReference> OrderGameCoreAssets(IEnumerable<ESRuntimeConsumerGameCoreReference> source)
        {
            var byId = new Dictionary<ESAssetIdentity, ESRuntimeConsumerGameCoreReference>();
            foreach (ESRuntimeConsumerGameCoreReference entry in source ?? Array.Empty<ESRuntimeConsumerGameCoreReference>())
                if (entry != null && entry.IsValid)
                    byId[new ESAssetIdentity(entry.guid, entry.localFileId)] = entry;

            var ordered = new List<ESRuntimeConsumerGameCoreReference>(byId.Count);
            var visited = new HashSet<ESAssetIdentity>();
            var visiting = new HashSet<ESAssetIdentity>();
            void Visit(ESAssetIdentity id)
            {
                if (visited.Contains(id)) return;
                if (!byId.TryGetValue(id, out ESRuntimeConsumerGameCoreReference entry))
                    throw new InvalidOperationException("Consumer \u7f3a\u5c11 GameCore \u4f9d\u8d56\uff1a" + id);
                if (!visiting.Add(id))
                    throw new InvalidOperationException("Consumer GameCore \u5b58\u5728\u5faa\u73af\u4f9d\u8d56\uff1a" + id);
                foreach (ESRuntimeConsumerGameCoreDependencyReference dependency in entry.dependencies ?? new List<ESRuntimeConsumerGameCoreDependencyReference>())
                {
                    if (dependency == null || !dependency.IsValid)
                        throw new InvalidOperationException("GameCore \u5305\u542b\u65e0\u6548\u4f9d\u8d56\uff1a" + id);
                    Visit(new ESAssetIdentity(dependency.guid, dependency.localFileId));
                }
                visiting.Remove(id);
                visited.Add(id);
                ordered.Add(entry);
            }

            foreach (ESAssetIdentity id in byId.Keys.OrderBy(item => item.Guid, StringComparer.Ordinal).ThenBy(item => item.LocalFileId))
                Visit(id);
            return ordered;
        }
        public void DisposeAssetLoading()
        {
            DisposeConsumerStartupAssets();
            assetLoadingService?.Dispose();
            assetLoadingService = null;
        }

        public override void OnDestroy()
        {
            DisposeAssetLoading();
            ESStoryDefinitionCatalog.AbortBuild();
            ESStoryDefinitionCatalog.Clear();
            base.OnDestroy();
        }

        public void BeginBuild(bool clear = false)
        {
            BeginBuildStatic(clear);
        }

        public void EndBuild()
        {
            EndBuildStatic();
        }

        public static void BeginBuildStatic(bool clear = false)
        {
            if (isBuilding)
            {
                if (clear)
                    throw new InvalidOperationException("ESRuntimeDataModule is already building. Clear rebuild cannot be nested.");

                return;
            }

            isBuilding = true;
            try
            {
                ESStoryDefinitionCatalog.BeginBuild(clear);
                ESRuntimeDataGameCore.BeginBuild(clear);
            }
            catch
            {
                ESStoryDefinitionCatalog.AbortBuild();
                isBuilding = false;
                throw;
            }
        }

        public static void EndBuildStatic()
        {
            EndBuildStatic(true);
        }

        private static void EndBuildStatic(bool audioCueCatalogReady)
        {
            if (!isBuilding)
                return;

            try
            {
                // Asset ConfigTables have an independent immutable generation transaction.
                // GameCore injection must not open or mutate the current Asset generation.
                // The callback may immediately start a Cue load, so it must never observe the
                // enclosing RuntimeData transaction half-built.
                ESRuntimeDataGameCore.EndBuild(false);
                if (audioCueCatalogReady) ESStoryDefinitionCatalog.EndBuild();
                else ESStoryDefinitionCatalog.AbortBuild();
            }
            catch
            {
                ESStoryDefinitionCatalog.AbortBuild();
                throw;
            }
            finally
            {
                isBuilding = false;
            }

            if (audioCueCatalogReady)
                ESAudioGameCoreTable.NotifyCatalogBuildCompleted();
            else
                ESAudioGameCoreTable.NotifyCatalogUnavailable();
        }

        public bool TryGetBuff(int runtimeKey, out ESBuffRuntimeData data) => Buffs.TryGet(runtimeKey, out data);
        public bool TryGetShot(int runtimeKey, out ESShotRuntimeData data) => Shots.TryGet(runtimeKey, out data);
        public bool TryGetMonster(int runtimeKey, out ESMonsterRuntimeData data) => Monsters.TryGet(runtimeKey, out data);
        public bool TryGetNpc(int runtimeKey, out ESNpcRuntimeData data) => Npcs.TryGet(runtimeKey, out data);
        public bool TryGetWeapon(int runtimeKey, out ESWeaponRuntimeData data) => Weapons.TryGet(runtimeKey, out data);
        public bool TryGetSkill(int runtimeKey, out ESSkillRuntimeData data) => Skills.TryGet(runtimeKey, out data);
        public bool TryGetAction(int runtimeKey, out ESActionRuntimeData data) => Actions.TryGet(runtimeKey, out data);
        public bool TryGetSkillTrack(int runtimeKey, out ESSkillTrackRuntimeData data) => SkillTracks.TryGet(runtimeKey, out data);

        public bool TryGetBuff(ESBuffEnumKey enumKey, out ESBuffRuntimeData data) => Buffs.TryGet((int)enumKey, out data);
        public bool TryGetShot(ESShotEnumKey enumKey, out ESShotRuntimeData data) => Shots.TryGet((int)enumKey, out data);
        public bool TryGetMonster(ESMonsterEnumKey enumKey, out ESMonsterRuntimeData data) => Monsters.TryGet((int)enumKey, out data);
        public bool TryGetNpc(ESNpcEnumKey enumKey, out ESNpcRuntimeData data) => Npcs.TryGet((int)enumKey, out data);
        public bool TryGetWeapon(ESWeaponEnumKey enumKey, out ESWeaponRuntimeData data) => Weapons.TryGet((int)enumKey, out data);
        public bool TryGetSkill(ESSkillEnumKey enumKey, out ESSkillRuntimeData data) => Skills.TryGet((int)enumKey, out data);
        public bool TryGetAction(ESActionEnumKey enumKey, out ESActionRuntimeData data) => Actions.TryGet((int)enumKey, out data);
        public bool TryGetSkillTrack(ESSkillTrackEnumKey enumKey, out ESSkillTrackRuntimeData data) => SkillTracks.TryGet((int)enumKey, out data);

        public bool TryGetBuff(string stringKey, out ESBuffRuntimeData data) => TryGetByString(Buffs, stringKey, out data);
        public bool TryGetShot(string stringKey, out ESShotRuntimeData data) => TryGetByString(Shots, stringKey, out data);
        public bool TryGetMonster(string stringKey, out ESMonsterRuntimeData data) => TryGetByString(Monsters, stringKey, out data);
        public bool TryGetNpc(string stringKey, out ESNpcRuntimeData data) => TryGetByString(Npcs, stringKey, out data);
        public bool TryGetWeapon(string stringKey, out ESWeaponRuntimeData data) => TryGetByString(Weapons, stringKey, out data);
        public bool TryGetSkill(string stringKey, out ESSkillRuntimeData data) => TryGetByString(Skills, stringKey, out data);
        public bool TryGetAction(string stringKey, out ESActionRuntimeData data) => TryGetByString(Actions, stringKey, out data);
        public bool TryGetSkillTrack(string stringKey, out ESSkillTrackRuntimeData data) => TryGetByString(SkillTracks, stringKey, out data);

        public string GetConflictReport()
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder(512);
            AppendConflictReport(builder, "Buff", Buffs);
            AppendConflictReport(builder, "Shot", Shots);
            AppendConflictReport(builder, "Monster", Monsters);
            AppendConflictReport(builder, "Npc", Npcs);
            AppendConflictReport(builder, "Weapon", Weapons);
            AppendConflictReport(builder, "Skill", Skills);
            AppendConflictReport(builder, "Action", Actions);
            AppendConflictReport(builder, "SkillTrack", SkillTracks);
            return builder.ToString();
        }

        private static bool TryGetByString<TData>(ESConfigKeyTable<TData> table, string stringKey, out TData data)
            where TData : class
        {
            if (table != null && table.TryGetRuntimeKey(stringKey, out int runtimeKey))
                return table.TryGet(runtimeKey, out data);

            data = null;
            return false;
        }

        private static void AppendConflictReport<TData>(System.Text.StringBuilder builder, string tableName, ESConfigKeyTable<TData> table)
            where TData : class
        {
            if (table == null || table.ConflictCount == 0)
                return;

            builder.Append("[").Append(tableName).Append("]").AppendLine();
            builder.Append(table.GetConflictReport());
        }
    }
}
