using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        public int OptionalPendingCount { get; internal set; }
        public int RetainCount { get; internal set; }
        public bool IsBackgroundComplete => OptionalPendingCount == 0;
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
            public readonly List<PrewarmedPrefab> prefabs = new List<PrewarmedPrefab>(8);
            public readonly ESResourcePlanReport report = new ESResourcePlanReport();
            public readonly object poolSource = new object();
            public readonly UniTaskCompletionSource<ESResourcePlanReport> completion = new UniTaskCompletionSource<ESResourcePlanReport>();
            public readonly UniTaskCompletionSource allLoadsCompletion = new UniTaskCompletionSource();
            public readonly UniTaskCompletionSource<ESResourcePlanReport> releaseCompletion = new UniTaskCompletionSource<ESResourcePlanReport>();
            public int retainCount = 1;
            public bool releaseRequested;
            private bool disposed;

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                loadingCancellation.Cancel();
                loadingCancellation.Dispose();
                scope.Dispose();
            }
        }

        private readonly Dictionary<ESResourcePlanInfo, Context> contexts = new Dictionary<ESResourcePlanInfo, Context>(16);
        private readonly Dictionary<ESResourcePlanInfo, Context> latestReleasingContexts = new Dictionary<ESResourcePlanInfo, Context>(4);
        private readonly HashSet<Context> releasingContexts = new HashSet<Context>();

        public bool TryGetStatus(ESResourcePlanInfo plan, out ESResourcePlanReport report)
        {
            if (plan != null && contexts.TryGetValue(plan, out Context context))
            {
                report = context.report;
                return true;
            }
            if (plan != null && latestReleasingContexts.TryGetValue(plan, out context))
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
                existing.retainCount++;
                existing.report.RetainCount = existing.retainCount;
                return AwaitApplyAsync(existing.completion.Task, cancellationToken);
            }

            var context = new Context();
            context.report.Plan = plan;
            context.report.State = ESResourcePlanState.Loading;
            context.report.RetainCount = 1;
            contexts.Add(plan, context);
            ApplyCoreAsync(plan, context).Forget();
            return AwaitApplyAsync(context.completion.Task, cancellationToken);
        }

        private static async UniTask<ESResourcePlanReport> AwaitApplyAsync(UniTask<ESResourcePlanReport> applyTask, CancellationToken cancellationToken)
        {
            return await applyTask.AttachExternalCancellation(cancellationToken);
        }

        public UniTask<ESResourcePlanReport> ReleaseAsync(ESResourcePlanInfo plan, CancellationToken cancellationToken = default)
        {
            if (plan == null)
                return UniTask.FromResult(new ESResourcePlanReport { Plan = plan, State = ESResourcePlanState.Released });

            if (!contexts.TryGetValue(plan, out Context context))
            {
                if (latestReleasingContexts.TryGetValue(plan, out context))
                    return AwaitReleaseAsync(context.releaseCompletion.Task, cancellationToken);
                return UniTask.FromResult(new ESResourcePlanReport { Plan = plan, State = ESResourcePlanState.Released });
            }

            context.retainCount--;
            context.report.RetainCount = context.retainCount;
            if (context.retainCount > 0)
                return UniTask.FromResult(context.report);

            // Detach first: a subsequent Apply receives a fresh scope and cancellation lifetime.
            contexts.Remove(plan);
            latestReleasingContexts[plan] = context;
            releasingContexts.Add(context);
            context.releaseRequested = true;
            context.report.State = ESResourcePlanState.ReleasePending;
            context.loadingCancellation.Cancel();
            ReleaseCoreAsync(plan, context).Forget();
            return AwaitReleaseAsync(context.releaseCompletion.Task, cancellationToken);
        }

        private static async UniTask<ESResourcePlanReport> AwaitReleaseAsync(UniTask<ESResourcePlanReport> releaseTask, CancellationToken cancellationToken)
        {
            return await releaseTask.AttachExternalCancellation(cancellationToken);
        }

        private async UniTaskVoid ApplyCoreAsync(ESResourcePlanInfo plan, Context context)
        {
            CancellationToken loadingToken = context.loadingCancellation.Token;
            try
            {
                UniTask optionalWork = UniTask.CompletedTask;
                try
                {
                    var requiredTasks = new List<UniTask>(16);
                    var optionalTasks = new List<UniTask>(16);
                    ScheduleEntries(plan, context, requiredTasks, optionalTasks, loadingToken);
                    context.report.TotalCount = requiredTasks.Count + optionalTasks.Count;
                    UniTask[] required = requiredTasks.ToArray();
                    UniTask[] optional = optionalTasks.ToArray();
                    context.report.OptionalPendingCount = optional.Length;
                    if (optional.Length > 0)
                        optionalWork = ObserveOptionalTasksAsync(optional, context, loadingToken);
                    await UniTask.WhenAll(required);

                    if (!context.releaseRequested)
                        context.report.State = context.report.RequiredFailureCount > 0
                            ? ESResourcePlanState.Failed
                            : ESResourcePlanState.Ready;
                    context.completion.TrySetResult(context.report);
                }
                catch (OperationCanceledException)
                {
                    if (!context.releaseRequested)
                        context.report.State = ESResourcePlanState.Canceled;
                    context.completion.TrySetResult(context.report);
                }
                catch (Exception exception)
                {
                    RecordFailure(context, ESAssetReferKind.None, plan.name, true, exception);
                    if (!context.releaseRequested)
                        context.report.State = ESResourcePlanState.Failed;
                    context.completion.TrySetResult(context.report);
                }

                try
                {
                    await optionalWork;
                }
                finally
                {
                    context.report.OptionalPendingCount = 0;
                    context.allLoadsCompletion.TrySetResult();
                }
            }
            catch (Exception exception)
            {
                RecordFailure(context, ESAssetReferKind.None, plan.name, true, exception);
                if (!context.releaseRequested)
                    context.report.State = ESResourcePlanState.Failed;
                context.completion.TrySetResult(context.report);
                context.allLoadsCompletion.TrySetResult();
            }
        }

        private async UniTask ObserveOptionalTasksAsync(UniTask[] tasks, Context context, CancellationToken token)
        {
            var observers = new UniTask[tasks.Length];
            for (int i = 0; i < tasks.Length; i++)
                observers[i] = ObserveOptionalTaskAsync(tasks[i], context, token);
            await UniTask.WhenAll(observers);
        }

        private async UniTask ObserveOptionalTaskAsync(UniTask task, Context context, CancellationToken token)
        {
            try { await task; }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception)
            {
                RecordFailure(context, ESAssetReferKind.None, "optional-background", false, exception);
            }
            finally
            {
                context.report.OptionalPendingCount--;
            }
        }

        private void ScheduleEntries(ESResourcePlanInfo plan, Context context, List<UniTask> requiredTasks, List<UniTask> optionalTasks, CancellationToken token)
        {
            AddPrefabTasks(plan.prefabs, context, requiredTasks, optionalTasks, token);
            AddPrefabPrewarmTasks(plan.prefabPrewarms, context, requiredTasks, optionalTasks, token);
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
                await LoadAsync<ESAssetReferPrefab, GameObject, ESAssetReferPrefabEnumKey>(entry.key, ESAssetReferKind.Prefab, context.scope, token);
                context.report.SuccessCount++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) { RecordFailure(context, ESAssetReferKind.Prefab, FormatKey(entry.key), entry.required, exception); }
        }

        private void AddPrefabPrewarmTasks(List<ESResourcePlanPrefabPrewarmEntry> entries, Context context, List<UniTask> requiredTasks, List<UniTask> optionalTasks, CancellationToken token)
        {
            if (entries == null) return;
            var scheduled = new HashSet<PrefabPrewarmDataInfo>();
            for (int i = 0; i < entries.Count; i++)
            {
                ESResourcePlanPrefabPrewarmEntry entry = entries[i];
                if (entry == null) continue;
                if (entry.data != null && !scheduled.Add(entry.data))
                {
                    Debug.LogWarning($"[ESRes][Plan] 已忽略重复的 Prefab 预热配置：Plan={context.report.Plan.name}, Prewarm={entry.data.name}", context.report.Plan);
                    continue;
                }
                (entry.required ? requiredTasks : optionalTasks).Add(LoadPrefabPrewarmAsync(entry, context, token));
            }
        }

        private async UniTask LoadPrefabPrewarmAsync(ESResourcePlanPrefabPrewarmEntry entry, Context context, CancellationToken token)
        {
            try
            {
                PrefabPrewarmDataInfo data = entry.data;
                if (data == null)
                    throw new InvalidOperationException("PrefabPrewarmDataInfo 未配置。");

                ESGameObjectPoolModule pool = ESGameManager.PoolModule;
                if (pool == null)
                    throw new InvalidOperationException("对象池模块尚未就绪。");

                string sceneName = SceneManager.GetActiveScene().name;
                if (!data.Supports(sceneName, pool.currentSpaceName))
                {
                    context.report.SuccessCount++;
                    return;
                }

                if (data.entries != null)
                    for (int i = 0; i < data.entries.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        PrefabPrewarmEntry prefabEntry = data.entries[i];
                        if (prefabEntry == null || !prefabEntry.enabled || prefabEntry.prewarmCount <= 0)
                            continue;

                        GameObject prefab = await LoadAsync<ESAssetReferPrefab, GameObject, ESAssetReferPrefabEnumKey>(
                            prefabEntry.prefabKey, ESAssetReferKind.Prefab, context.scope, token);
                        ESGameObjectPoolConfig config = prefabEntry.useCustomConfig ? prefabEntry.config : pool.defaultConfig;
                        pool.PrewarmOwned(prefab, prefabEntry.prewarmCount, context.poolSource, prefabEntry.key, config);
                        context.prefabs.Add(new PrewarmedPrefab
                        {
                            prefab = prefab,
                            poolKey = prefabEntry.key,
                            count = prefabEntry.prewarmCount
                        });
                    }

                context.report.SuccessCount++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                RecordFailure(context, ESAssetReferKind.Prefab, entry.data != null ? entry.data.name : "<null-prewarm>", entry.required, exception);
            }
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

        private async UniTaskVoid ReleaseCoreAsync(ESResourcePlanInfo plan, Context context)
        {
            try
            {
                await context.allLoadsCompletion.Task;

                ESGameObjectPoolModule pool = ESGameManager.PoolModule;
                if (pool != null)
                    for (int i = 0; i < context.prefabs.Count; i++)
                    {
                        PrewarmedPrefab prefab = context.prefabs[i];
                        pool.ReleaseOwnedPrewarm(prefab.prefab, prefab.count, context.poolSource, prefab.poolKey);
                    }

                float delay = plan.releaseOnExit ? Mathf.Max(0f, plan.releaseDelaySeconds) : 0f;
                if (delay > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), DelayType.Realtime, PlayerLoopTiming.Update);

                context.report.State = ESResourcePlanState.Released;
                RemoveReleasingContext(plan, context);
                context.Dispose();
                context.releaseCompletion.TrySetResult(context.report);
            }
            catch (Exception exception)
            {
                RecordFailure(context, ESAssetReferKind.None, plan.name, false, exception);
                context.report.State = ESResourcePlanState.Failed;
                RemoveReleasingContext(plan, context);
                context.Dispose();
                context.releaseCompletion.TrySetResult(context.report);
            }
        }

        private void RemoveReleasingContext(ESResourcePlanInfo plan, Context context)
        {
            releasingContexts.Remove(context);
            if (latestReleasingContexts.TryGetValue(plan, out Context latest) && ReferenceEquals(latest, context))
                latestReleasingContexts.Remove(plan);
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

            foreach (Context context in releasingContexts)
                context.Dispose();
            releasingContexts.Clear();
            latestReleasingContexts.Clear();
        }
    }

    /// <summary>资源计划的业务层入口。普通代码无需接触 Service、Scope 或对象池。</summary>
    public static class ESResourcePlanExtensions
    {
        public static UniTask<ESResourcePlanReport> ApplyAsync(this ESResourcePlanInfo plan, CancellationToken cancellationToken = default)
        {
            if (plan == null)
                return UniTask.FromException<ESResourcePlanReport>(new ArgumentNullException(nameof(plan)));

            ESResourcePlanRuntimeService service = ESGameManager.ResourcePlans;
            if (service == null)
                return UniTask.FromException<ESResourcePlanReport>(new InvalidOperationException("[ESRes][Plan] 资源系统尚未就绪。"));
            return service.ApplyAsync(plan, cancellationToken);
        }

        public static UniTask<ESResourcePlanReport> ReleaseAsync(this ESResourcePlanInfo plan, CancellationToken cancellationToken = default)
        {
            if (plan == null)
                return UniTask.FromResult(new ESResourcePlanReport { State = ESResourcePlanState.Released });

            ESResourcePlanRuntimeService service = ESGameManager.ResourcePlans;
            if (service == null)
                return UniTask.FromResult(new ESResourcePlanReport { Plan = plan, State = ESResourcePlanState.Released });
            return service.ReleaseAsync(plan, cancellationToken);
        }

        public static bool TryGetStatus(this ESResourcePlanInfo plan, out ESResourcePlanReport report)
        {
            ESResourcePlanRuntimeService service = ESGameManager.ResourcePlans;
            if (plan != null && service != null)
                return service.TryGetStatus(plan, out report);
            report = null;
            return false;
        }
    }

    /// <summary>将资源计划绑定到 GameObject 生命周期，无需编写加载和释放代码。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("ES/Resource/Resource Plan Binder")]
    public sealed class ESResourcePlanBinder : MonoBehaviour
    {
        [SerializeField, Tooltip("进入该对象生命周期时需要准备的资源计划。")]
        private ESResourcePlanInfo plan;

        [SerializeField, Tooltip("启用对象时自动准备资源。")]
        private bool applyOnEnable = true;

        private int activationVersion;
        private CancellationTokenSource activationCancellation;
        private bool ownsPlanRetain;

        public ESResourcePlanInfo Plan => plan;
        public ESResourcePlanReport LastReport { get; private set; }
        public bool IsReady => LastReport != null && LastReport.State == ESResourcePlanState.Ready;

        private void OnEnable()
        {
            if (!applyOnEnable || plan == null)
                return;

            int version = ++activationVersion;
            activationCancellation?.Dispose();
            activationCancellation = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            ApplyWhenReadyAsync(version, activationCancellation.Token).Forget();
        }

        private void OnDisable()
        {
            int version = ++activationVersion;
            activationCancellation?.Cancel();
            activationCancellation?.Dispose();
            activationCancellation = null;

            if (ownsPlanRetain && plan != null && plan.releaseOnExit)
            {
                ownsPlanRetain = false;
                ReleaseSafelyAsync(version).Forget();
            }
        }

        public UniTask<ESResourcePlanReport> ApplyAsync(CancellationToken cancellationToken = default)
        {
            if (plan == null)
                return UniTask.FromException<ESResourcePlanReport>(new InvalidOperationException("[ESRes][Plan] 接入器尚未指定资源计划。"));
            return ApplyAndRememberAsync(cancellationToken);
        }

        public UniTask<ESResourcePlanReport> ReleaseAsync(CancellationToken cancellationToken = default)
        {
            return plan == null
                ? UniTask.FromResult(new ESResourcePlanReport { State = ESResourcePlanState.Released })
                : plan.ReleaseAsync(cancellationToken);
        }

        private async UniTaskVoid ApplyWhenReadyAsync(int version, CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.WaitUntil(() => ESGameManager.ResourcePlans != null, cancellationToken: cancellationToken);
                ownsPlanRetain = true;
                ESResourcePlanReport report = await plan.ApplyAsync(cancellationToken);
                if (version != activationVersion)
                    return;
                LastReport = report;
                ReportRequiredFailures(report);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                Debug.LogError("[ESRes][Plan] 自动准备资源失败：" + exception.Message, this);
            }
        }

        private async UniTask<ESResourcePlanReport> ApplyAndRememberAsync(CancellationToken cancellationToken)
        {
            LastReport = await plan.ApplyAsync(cancellationToken);
            ReportRequiredFailures(LastReport);
            return LastReport;
        }

        private void ReportRequiredFailures(ESResourcePlanReport report)
        {
            if (report != null && report.RequiredFailureCount > 0)
                Debug.LogError($"[ESRes][Plan] 必需资源准备失败：Plan={plan.name}, Count={report.RequiredFailureCount}", this);
        }

        private async UniTaskVoid ReleaseSafelyAsync(int version)
        {
            try
            {
                ESResourcePlanReport report = await plan.ReleaseAsync();
                if (version == activationVersion)
                    LastReport = report;
            }
            catch (Exception exception)
            {
                Debug.LogError("[ESRes][Plan] 自动释放资源失败：" + exception.Message, this);
            }
        }
    }
}
