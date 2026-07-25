using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ES
{
    public enum ESResourcePlanState : byte
    {
        Idle,
        Loading,
        Prewarming,
        Ready,
        ReleasePending,
        Released,
        Failed,
        Canceled
    }

    public readonly struct ESResourcePlanError
    {
        public readonly ESAssetReferKind Kind;
        public readonly string Key;
        public readonly bool Required;
        public readonly string Message;

        public ESResourcePlanError(ESAssetReferKind kind, string key, bool required, string message)
        {
            Kind = kind;
            Key = key;
            Required = required;
            Message = message;
        }
    }

    public sealed class ESResourcePlanReport
    {
        public ESResourcePlanInfo Plan { get; internal set; }
        public ESResourcePlanState State { get; internal set; }
        public int TotalCount { get; internal set; }
        public int SuccessCount { get; internal set; }
        public int FailureCount { get; internal set; }
        public int RequiredFailureCount { get; internal set; }
        public IReadOnlyList<ESResourcePlanError> Errors => errors;

        internal readonly List<ESResourcePlanError> errors = new List<ESResourcePlanError>(4);
    }

    public sealed class ESResourcePlanRuntimeService : IDisposable
    {
        private sealed class PrewarmedPrefab
        {
            public GameObject prefab;
            public string poolKey;
            public int count;
        }

        private sealed class Context : IDisposable
        {
            public readonly ESAssetScope scope = ESAssets.CreateScope();
            public readonly CancellationTokenSource loadingCancellation = new CancellationTokenSource();
            public CancellationTokenSource releaseCancellation;
            public readonly List<PrewarmedPrefab> prefabs = new List<PrewarmedPrefab>(8);
            public readonly ESResourcePlanReport report = new ESResourcePlanReport();
            public readonly object poolSource = new object();
            public UniTaskCompletionSource<ESResourcePlanReport> completion = new UniTaskCompletionSource<ESResourcePlanReport>();

            public void Dispose()
            {
                loadingCancellation.Cancel();
                loadingCancellation.Dispose();
                releaseCancellation?.Cancel();
                releaseCancellation?.Dispose();
                scope.Dispose();
            }
        }

        private readonly Dictionary<ESResourcePlanInfo, Context> contexts = new Dictionary<ESResourcePlanInfo, Context>(16);

        public bool TryGetStatus(ESResourcePlanInfo plan, out ESResourcePlanReport report)
        {
            if (plan != null && contexts.TryGetValue(plan, out Context context))
            {
                report = context.report;
                return true;
            }
            report = null;
            return false;
        }

        public UniTask<ESResourcePlanReport> ApplyAsync(ESResourcePlanInfo plan, CancellationToken cancellationToken = default)
        {
            if (plan == null)
                return UniTask.FromException<ESResourcePlanReport>(new ArgumentNullException(nameof(plan)));

            if (contexts.TryGetValue(plan, out Context existing))
            {
                if (existing.report.State == ESResourcePlanState.ReleasePending)
                {
                    existing.releaseCancellation?.Cancel();
                    existing.report.State = ESResourcePlanState.Ready;
                }
                return existing.completion.Task;
            }

            var context = new Context();
            context.report.Plan = plan;
            context.report.State = ESResourcePlanState.Loading;
            contexts.Add(plan, context);
            ApplyCoreAsync(plan, context, cancellationToken).Forget();
            return context.completion.Task;
        }

        public UniTask<ESResourcePlanReport> ReleaseAsync(ESResourcePlanInfo plan, CancellationToken cancellationToken = default)
        {
            if (plan == null || !contexts.TryGetValue(plan, out Context context))
                return UniTask.FromResult(new ESResourcePlanReport { Plan = plan, State = ESResourcePlanState.Released });

            context.loadingCancellation.Cancel();
            context.releaseCancellation?.Cancel();
            context.releaseCancellation?.Dispose();
            context.releaseCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            return ReleaseCoreAsync(plan, context, context.releaseCancellation.Token);
        }

        private async UniTaskVoid ApplyCoreAsync(ESResourcePlanInfo plan, Context context, CancellationToken externalToken)
        {
            using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(externalToken, context.loadingCancellation.Token))
            {
                try
                {
                    var requiredTasks = new List<UniTask>(16);
                    var optionalTasks = new List<UniTask>(16);
                    ScheduleEntries(plan, context, requiredTasks, optionalTasks, linked.Token);
                    context.report.TotalCount = requiredTasks.Count + optionalTasks.Count;
                    UniTask[] required = requiredTasks.ToArray();
                    UniTask[] optional = optionalTasks.ToArray();
                    if (optional.Length > 0)
                        ObserveOptionalTasksAsync(optional, context, linked.Token).Forget();
                    await UniTask.WhenAll(required);

                    context.report.State = context.report.RequiredFailureCount > 0
                        ? ESResourcePlanState.Failed
                        : ESResourcePlanState.Ready;
                    context.completion.TrySetResult(context.report);
                }
                catch (OperationCanceledException)
                {
                    context.report.State = ESResourcePlanState.Canceled;
                    context.completion.TrySetResult(context.report);
                }
                catch (Exception exception)
                {
                    RecordFailure(context, ESAssetReferKind.None, plan.name, true, exception);
                    context.report.State = ESResourcePlanState.Failed;
                    context.completion.TrySetResult(context.report);
                }
            }
        }

        private async UniTaskVoid ObserveOptionalTasksAsync(UniTask[] tasks, Context context, CancellationToken token)
        {
            try { await UniTask.WhenAll(tasks); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception)
            {
                RecordFailure(context, ESAssetReferKind.None, "optional-background", false, exception);
            }
        }

        private void ScheduleEntries(ESResourcePlanInfo plan, Context context, List<UniTask> requiredTasks, List<UniTask> optionalTasks, CancellationToken token)
        {
            AddPrefabTasks(plan.prefabs, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanSpriteEntry, ESAssetReferSpriteConfigKey, ESAssetReferSprite, Sprite, ESAssetReferSpriteEnumKey>(plan.sprites, ESAssetReferKind.Sprite, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanAudioEntry, ESAssetReferAudioClipConfigKey, ESAssetReferAudioClip, AudioClip, ESAssetReferAudioClipEnumKey>(plan.audioClips, ESAssetReferKind.AudioClip, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanAnimationEntry, ESAssetReferAnimationClipConfigKey, ESAssetReferAnimationClip, AnimationClip, ESAssetReferAnimationClipEnumKey>(plan.animationClips, ESAssetReferKind.AnimationClip, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanAnimatorEntry, ESAssetReferAnimatorControllerConfigKey, ESAssetReferAnimatorController, RuntimeAnimatorController, ESAssetReferAnimatorControllerEnumKey>(plan.animatorControllers, ESAssetReferKind.AnimatorController, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanMaterialEntry, ESAssetReferMaterialConfigKey, ESAssetReferMaterial, Material, ESAssetReferMaterialEnumKey>(plan.materials, ESAssetReferKind.Material, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanMeshEntry, ESAssetReferMeshConfigKey, ESAssetReferMesh, Mesh, ESAssetReferMeshEnumKey>(plan.meshes, ESAssetReferKind.Mesh, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanTextureEntry, ESAssetReferTextureConfigKey, ESAssetReferTexture, Texture, ESAssetReferTextureEnumKey>(plan.textures, ESAssetReferKind.Texture, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanTexture2DEntry, ESAssetReferTexture2DConfigKey, ESAssetReferTexture2D, Texture2D, ESAssetReferTexture2DEnumKey>(plan.texture2Ds, ESAssetReferKind.Texture2D, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanSpriteAtlasEntry, ESAssetReferSpriteAtlasConfigKey, ESAssetReferSpriteAtlas, UnityEngine.U2D.SpriteAtlas, ESAssetReferSpriteAtlasEnumKey>(plan.spriteAtlases, ESAssetReferKind.SpriteAtlas, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanAvatarEntry, ESAssetReferAvatarConfigKey, ESAssetReferAvatar, Avatar, ESAssetReferAvatarEnumKey>(plan.avatars, ESAssetReferKind.Avatar, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanPlayableEntry, ESAssetReferPlayableAssetConfigKey, ESAssetReferPlayableAsset, UnityEngine.Playables.PlayableAsset, ESAssetReferPlayableAssetEnumKey>(plan.playableAssets, ESAssetReferKind.PlayableAsset, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanScriptableObjectEntry, ESAssetReferScriptableObjectConfigKey, ESAssetReferScriptableObject, ScriptableObject, ESAssetReferScriptableObjectEnumKey>(plan.scriptableObjects, ESAssetReferKind.ScriptableObject, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanTimelineEntry, ESAssetReferTimelineAssetConfigKey, ESAssetReferUnityObject, UnityEngine.Object, ESAssetReferTimelineAssetEnumKey>(plan.timelineAssets, ESAssetReferKind.TimelineAsset, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanVideoEntry, ESAssetReferVideoClipConfigKey, ESAssetReferVideoClip, UnityEngine.Video.VideoClip, ESAssetReferVideoClipEnumKey>(plan.videoClips, ESAssetReferKind.VideoClip, e => e.key, context, requiredTasks, optionalTasks, token);
            AddTasks<ESResourcePlanTerrainEntry, ESAssetReferTerrainDataConfigKey, ESAssetReferTerrainData, TerrainData, ESAssetReferTerrainDataEnumKey>(plan.terrainDatas, ESAssetReferKind.TerrainData, e => e.key, context, requiredTasks, optionalTasks, token);
        }

        private void AddPrefabTasks(List<ESResourcePlanPrefabEntry> entries, Context context, List<UniTask> requiredTasks, List<UniTask> optionalTasks, CancellationToken token)
        {
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null)
                    (entries[i].required ? requiredTasks : optionalTasks).Add(LoadPrefabAsync(entries[i], context, token));
        }

        private async UniTask LoadPrefabAsync(ESResourcePlanPrefabEntry entry, Context context, CancellationToken token)
        {
            try
            {
                GameObject prefab = await LoadAsync<ESAssetReferPrefab, GameObject, ESAssetReferPrefabEnumKey>(entry.key, ESAssetReferKind.Prefab, context.scope, token);
                if (entry.prewarmCount > 0)
                {
                    if (context.report.State == ESResourcePlanState.Loading)
                        context.report.State = ESResourcePlanState.Prewarming;
                    ESGameObjectPoolModule pool = ESGameManager.PoolModule;
                    if (pool == null) throw new InvalidOperationException("对象池模块尚未就绪。");
                    ESGameObjectPoolConfig config = entry.useCustomPoolConfig ? entry.poolConfig : pool.defaultConfig;
                    pool.PrewarmOwned(prefab, entry.prewarmCount, context.poolSource, entry.poolKey, config);
                    context.prefabs.Add(new PrewarmedPrefab { prefab = prefab, poolKey = entry.poolKey, count = entry.prewarmCount });
                }
                context.report.SuccessCount++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) { RecordFailure(context, ESAssetReferKind.Prefab, FormatKey(entry.key), entry.required, exception); }
        }

        private void AddTasks<TEntry, TKey, TRefer, TAsset, TEnum>(List<TEntry> entries, ESAssetReferKind kind, Func<TEntry, TKey> getKey, Context context, List<UniTask> requiredTasks, List<UniTask> optionalTasks, CancellationToken token)
            where TEntry : ESResourcePlanEntryBase
            where TKey : ESAssetConfigKey<TEnum>
            where TRefer : ESAssetRefer<TAsset>, new()
            where TAsset : UnityEngine.Object
            where TEnum : struct, Enum
        {
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null)
                    (entries[i].required ? requiredTasks : optionalTasks).Add(LoadEntryAsync<TEntry, TKey, TRefer, TAsset, TEnum>(entries[i], getKey(entries[i]), kind, context, token));
        }

        private async UniTask LoadEntryAsync<TEntry, TKey, TRefer, TAsset, TEnum>(TEntry entry, TKey key, ESAssetReferKind kind, Context context, CancellationToken token)
            where TEntry : ESResourcePlanEntryBase
            where TKey : ESAssetConfigKey<TEnum>
            where TRefer : ESAssetRefer<TAsset>, new()
            where TAsset : UnityEngine.Object
            where TEnum : struct, Enum
        {
            try
            {
                await LoadAsync<TRefer, TAsset, TEnum>(key, kind, context.scope, token);
                context.report.SuccessCount++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) { RecordFailure(context, kind, FormatKey(key), entry.required, exception); }
        }

        private static UniTask<TAsset> LoadAsync<TRefer, TAsset, TEnum>(ESAssetConfigKey<TEnum> key, ESAssetReferKind kind, ESAssetScope scope, CancellationToken token)
            where TRefer : ESAssetRefer<TAsset>, new()
            where TAsset : UnityEngine.Object
            where TEnum : struct, Enum
        {
            if (key == null || (key.EnumKeyInt == 0 && string.IsNullOrEmpty(key.StringKey)))
                return UniTask.FromException<TAsset>(new InvalidOperationException("ConfigKey 缺少 EnumKey/StringKey。"));
            if (!key.HasGuid)
                return UniTask.FromException<TAsset>(new InvalidOperationException("ConfigKey 尚未解析到 GUID，请先完成资源注册与构表。"));

            var refer = new TRefer();
            refer.InitializeGeneratedReference(key.guid, key.localFileId, kind, key.EnumKeyInt, key.StringKey);
            return scope.LoadAsync(refer, token);
        }

        private static string FormatKey<TEnum>(ESAssetConfigKey<TEnum> key) where TEnum : struct, Enum
            => key == null ? "<null>" : key.EnumKeyInt != 0 ? key.EnumKeyInt.ToString() : key.StringKey;

        private static void RecordFailure(Context context, ESAssetReferKind kind, string key, bool required, Exception exception)
        {
            context.report.FailureCount++;
            if (required) context.report.RequiredFailureCount++;
            string message = exception?.Message ?? "Unknown error";
            context.report.errors.Add(new ESResourcePlanError(kind, key, required, message));
            Debug.LogError($"[ESRes][Plan] 资源处理失败：Kind={kind}, Key={key}, Required={required}, Error={message}", context.report.Plan);
        }

        private async UniTask<ESResourcePlanReport> ReleaseCoreAsync(ESResourcePlanInfo plan, Context context, CancellationToken token)
        {
            try
            {
                await context.completion.Task;
                context.report.State = ESResourcePlanState.ReleasePending;
                float delay = plan.releaseOnExit ? Mathf.Max(0f, plan.releaseDelaySeconds) : 0f;
                if (delay > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), DelayType.Realtime, PlayerLoopTiming.Update, token);

                ESGameObjectPoolModule pool = ESGameManager.PoolModule;
                if (pool != null)
                    for (int i = 0; i < context.prefabs.Count; i++)
                    {
                        PrewarmedPrefab prefab = context.prefabs[i];
                        pool.ReleaseOwnedPrewarm(prefab.prefab, prefab.count, context.poolSource, prefab.poolKey);
                    }

                context.report.State = ESResourcePlanState.Released;
                contexts.Remove(plan);
                context.Dispose();
                return context.report;
            }
            catch (OperationCanceledException)
            {
                if (contexts.ContainsKey(plan)) context.report.State = ESResourcePlanState.Ready;
                return context.report;
            }
        }

        public void Dispose()
        {
            ESGameObjectPoolModule pool = ESGameManager.PoolModule;
            foreach (Context context in contexts.Values)
            {
                if (pool != null)
                    for (int i = 0; i < context.prefabs.Count; i++)
                    {
                        PrewarmedPrefab prefab = context.prefabs[i];
                        pool.ReleaseOwnedPrewarm(prefab.prefab, prefab.count, context.poolSource, prefab.poolKey);
                    }
                context.Dispose();
            }
            contexts.Clear();
        }
    }
}
