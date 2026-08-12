using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
    /// <summary>
    /// Selects when a VFX-authored emitter asks <see cref="ESAudioModule"/> to start its configured
    /// sound. The component never calls AudioSource.Play directly.
    /// </summary>
    public enum ESVfxAudioStartMode : byte
    {
        [InspectorName("随对象启用")]
        OnEnable,

        [InspectorName("随 VFX 播放操作")]
        OnVfxPlay,

        [InspectorName("仅代码手动调用")]
        Manual
    }

    /// <summary>
    /// Adapter for one AudioSource authored inside a VFX or scene Prefab. It keeps the physical
    /// source on its owner GameObject while routing every real playback through ESAudioModule.
    /// Therefore normal budgets, fades, diagnostics, Handles and lifecycle cleanup still apply.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("【ES】/相机与表现/音频与特效/VFX 音频发射器（受管）")]
    public sealed class ESVfxAudioEmitter : MonoBehaviour
    {
        [Title("新手使用")]
        [InfoBox("1. 与 AudioSource 挂在同一对象；2. 选择 Audio Cue（推荐）或 Legacy Clip；3. 场景常驻音频选“随对象启用”，特效音频选“随 VFX 播放操作”。Cue 的随对象启用会等待音频模块、资源后端、AudioCue 目录及当前 ResourcePlan 的 Clip 预热全部就绪后只尝试一次。不要勾选 AudioSource 的 Play On Awake。")]
        [Title("托管 AudioSource")]
        [SerializeField, Required, LabelText("受管 AudioSource")]
        private AudioSource audioSource;

        // Editor-baked diagnostics metadata only. It is never used by playback, loading,
        // matching, pooling, save data, or any other runtime identity decision.
        [SerializeField, HideInInspector]
        private string diagnosticPrefabPath;

        [InfoBox("优先使用 Cue：它支持资源预热、随机变体、冷却、预算和诊断。Legacy Clip 仅用于现有 VFX/场景 AudioSource 迁移；两者都为空时，会尝试使用本组件 AudioSource 上已有的 Clip。")]
        [Title("播放内容")]
        [LabelText("音频 Cue（推荐）"), InlineProperty]
        public ESAudioCueKey cue = new ESAudioCueKey();

        [LabelText("Legacy Clip（可选）")]
        public AudioClip legacyClip;

        [LabelText("Legacy Clip 播放配置（可选）"), InlineProperty]
        public ESAudioClipPlayConfig legacyClipConfig = new ESAudioClipPlayConfig();

        [Title("触发")]
        [LabelText("开始时机")]
        public ESVfxAudioStartMode startMode = ESVfxAudioStartMode.OnVfxPlay;

        [LabelText("强制循环")]
        [InfoBox("开启后无论 Cue/Clip 原配置如何，都会通过 PlayLoopOnEmitter 播放。")]
        public bool forceLoop;

        [LabelText("播放请求")]
        [InfoBox("位置与生命周期 Owner 由本组件所在 Transform 固定管理；这里只配置音量、音高、优先级和淡入。")]
        public ESAudioPlayRequest playRequest;

        [ShowInInspector, ReadOnly, LabelText("当前 Voice")]
        private ESAudioVoiceHandle activeHandle;

        [NonSerialized] private bool hasStarted;
        [NonSerialized] private bool autoPlayArmed;
        [NonSerialized] private bool autoPlayQueued;
        [NonSerialized] private bool waitingForPlaybackReadiness;

        public AudioSource Source => audioSource;
        public ESAudioVoiceHandle ActiveHandle => activeHandle;

        private void Reset()
        {
            CacheSource();
#if UNITY_EDITOR
            RefreshDiagnosticPrefabPathFromEditor();
#endif
            CaptureLegacyClipSettingsFromSource();
            if (audioSource != null)
                audioSource.playOnAwake = false;
        }

        private void Awake()
        {
            CacheSource();
            if (Application.isPlaying && audioSource != null)
            {
                // The component owns playback policy. Prevent a Prefab's legacy play-on-awake
                // setting from starting a new raw playback before the Voice is admitted. An
                // already-playing source is intentionally left alone and rejected as busy by the
                // module rather than being stopped behind another owner's back.
                audioSource.playOnAwake = false;
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying || startMode != ESVfxAudioStartMode.OnEnable)
                return;

            autoPlayArmed = true;
            if (hasStarted)
                TryPlayArmedOnEnable();
        }

        private void Start()
        {
            hasStarted = true;
            TryPlayArmedOnEnable();
        }

        private void OnDisable()
        {
            UnsubscribeFromPlaybackReadiness();
            if (Application.isPlaying)
                StopForLifecycle(ESAudioVoiceEndReason.OwnerDisabled);
        }

        private void OnDestroy()
        {
            UnsubscribeFromPlaybackReadiness();
            if (Application.isPlaying)
                StopForLifecycle(ESAudioVoiceEndReason.OwnerDestroyed);
        }

        private void OnValidate()
        {
            CacheSource();
            if (legacyClipConfig == null)
                legacyClipConfig = new ESAudioClipPlayConfig();
#if UNITY_EDITOR
            RefreshDiagnosticPrefabPathFromEditor();
#endif
        }

        /// <summary>Explicit gameplay/VFX entry. Returns the existing active Handle when already playing.</summary>
        public ESAudioVoiceHandle PlayConfigured()
        {
            ESAudioModule audio = ESGameManager.Audio;
            if (audio == null || !isActiveAndEnabled)
                return default;

            if (activeHandle.IsValid && audio.TryGetVoiceStatus(activeHandle, out ESAudioVoiceStatus status)
                && status.State != ESAudioVoiceState.Ended)
                return activeHandle;

            activeHandle = default;
            if (cue != null && cue.IsConfigured)
            {
                activeHandle = forceLoop
                    ? audio.PlayLoopOnEmitter(cue, this, playRequest)
                    : audio.PlayOnEmitter(cue, this, playRequest);
                return activeHandle;
            }

            AudioClip clip = legacyClip != null ? legacyClip : audioSource != null ? audioSource.clip : null;
            if (clip == null)
                return default;

            activeHandle = forceLoop
                ? audio.PlayLoopOnEmitter(clip, this, legacyClipConfig, playRequest)
                : audio.PlayOnEmitter(clip, this, legacyClipConfig, playRequest);
            return activeHandle;
        }

        /// <summary>Stops this emitter's own Voice only. It never directly controls AudioSource.</summary>
        public bool StopConfigured(float fadeOutSeconds = 0f)
        {
            ESAudioModule audio = ESGameManager.Audio;
            if (audio == null)
                return false;

            if (activeHandle.IsValid && audio.Stop(activeHandle, fadeOutSeconds))
                return true;

            activeHandle = default;
            return audio.StopBoundEmitter(this, ESAudioVoiceEndReason.ExplicitStop);
        }

        /// <summary>
        /// Synchronizes common legacy AudioSource settings into the Direct Clip path. It is an
        /// editor/conversion helper; category routes remain controlled by ESAudioModule.
        /// </summary>
        [Button("同步当前 AudioSource 的 Clip 参数")]
        public void CaptureLegacyClipSettingsFromSource()
        {
            CacheSource();
            if (audioSource == null)
                return;

            legacyClipConfig ??= new ESAudioClipPlayConfig();
            legacyClipConfig.loop = audioSource.loop;
            legacyClipConfig.volume = Mathf.Clamp01(audioSource.volume);
            legacyClipConfig.pitch = Mathf.Clamp(audioSource.pitch, 0.1f, 3f);
            legacyClipConfig.minDistance = Mathf.Max(0f, audioSource.minDistance);
            legacyClipConfig.maxDistance = Mathf.Max(legacyClipConfig.minDistance, audioSource.maxDistance);

            // Do not infer 2D from Unity's default AudioSource. VFX starts as 3D by convention;
            // artists can explicitly opt into a 2D override in the exposed Clip configuration.
        }

        internal bool TryGetManagedSource(out AudioSource source)
        {
            source = audioSource;
            return source != null;
        }

        /// <summary>
        /// Slow diagnostics-only origin text for a batched OnEnable overflow report. Runtime
        /// playback never calls this on a successful path, and the module samples at most four
        /// distinct origins per reported batch.
        /// </summary>
        internal string DescribeAutoPlayOriginForDiagnostics()
        {
            string scenePath;
            if (gameObject.scene.IsValid() && !string.IsNullOrEmpty(gameObject.scene.path))
                scenePath = gameObject.scene.path;
            else
            {
                string sceneName = gameObject.scene.IsValid() && !string.IsNullOrEmpty(gameObject.scene.name)
                    ? gameObject.scene.name
                    : "<未命名>";
                scenePath = "<runtime scene:" + sceneName + ">";
            }

            string content;
            if (cue != null && cue.IsConfigured)
            {
                content = "Cue=" + ESConfigKeyMatch.Describe(cue.EnumKeyInt, cue.StringKey);
            }
            else
            {
                AudioClip clip = legacyClip != null ? legacyClip : audioSource != null ? audioSource.clip : null;
                content = "LegacyClip=" + (clip != null ? clip.name : "<未配置>");
            }

            string prefabPath = string.IsNullOrEmpty(diagnosticPrefabPath)
                ? "<场景对象或未烘焙 Prefab>"
                : diagnosticPrefabPath;
            string instanceDetail = string.Empty;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            instanceDetail = "，InstanceID=" + gameObject.GetInstanceID();
#endif
            return "ScenePath=" + scenePath + "，PrefabPath=" + prefabPath + "，对象=" + gameObject.name + "，" + content + instanceDetail;
        }

        internal void NotifyVoiceEnded(ESAudioVoiceHandle handle)
        {
            if (activeHandle.Equals(handle))
                activeHandle = default;
        }

        /// <summary>Pool edge only: arm now, then let OnEnable start after SetActive(true).</summary>
        internal void ArmForPoolSpawn()
        {
            if (startMode == ESVfxAudioStartMode.OnEnable)
                autoPlayArmed = true;
        }

        /// <summary>VFX operation edge. Only emitters explicitly configured for VFX playback respond.</summary>
        internal void PlayFromVfx()
        {
            if (startMode == ESVfxAudioStartMode.OnVfxPlay)
                PlayConfigured();
        }

        internal void StopForLifecycle(ESAudioVoiceEndReason reason)
        {
            autoPlayArmed = false;
            UnsubscribeFromPlaybackReadiness();
            ESAudioModule audio = ESGameManager.Audio;
            if (autoPlayQueued && audio != null)
                audio.CancelAutoPlay(this);
            autoPlayQueued = false;
            if (audio != null)
                audio.StopBoundEmitter(this, reason);
            activeHandle = default;
        }

        private void TryPlayArmedOnEnable()
        {
            if (!autoPlayArmed || !isActiveAndEnabled || autoPlayQueued)
                return;

            if (!IsConfiguredContentReadyForAutoPlay())
            {
                SubscribeToPlaybackReadiness();
                return;
            }

            ESAudioModule audio = ESGameManager.Audio;
            if (audio == null)
            {
                SubscribeToPlaybackReadiness();
                return;
            }

            if (!audio.TryEnqueueAutoPlay(this))
            {
                // The module owns a second FIFO waiting ring, so normal 513/1024-emitter
                // startup bursts do not reach here. A larger burst is authoring over-capacity,
                // not a reason to broadcast hundreds of listeners for one execution slot.
                autoPlayArmed = false;
                audio.ReportAutoPlayQueueOverflow(this);
                return;
            }

            autoPlayQueued = true;
            UnsubscribeFromPlaybackReadiness();
        }

        /// <summary>
        /// Cue playback requires more than the Audio module instance: the resource backend, the
        /// current GameCore AudioCue catalog, and the active ResourcePlan's actual Clip ownership
        /// must all be ready. Direct Clips only need the module because their owning Prefab/scene
        /// already owns the Unity object. This is an edge-driven wait; it never polls in Update.
        /// </summary>
        private bool IsConfiguredContentReadyForAutoPlay()
        {
            ESAudioModule audio = ESGameManager.Audio;
            if (audio == null || !audio.Signal_IsActiveAndEnable)
                return false;

            return !UsesCuePlayback() || audio.IsCuePreparedForPlayback(cue);
        }

        private bool UsesCuePlayback()
        {
            return cue != null && cue.IsConfigured;
        }

        private void SubscribeToPlaybackReadiness()
        {
            if (waitingForPlaybackReadiness)
                return;

            ESGameManager.AudioModuleAvailabilityChanged += HandleAudioModuleAvailabilityChanged;
            if (UsesCuePlayback())
            {
                ESAssets.RuntimeBackendRebuilt += HandleRuntimeBackendRebuilt;
                ESAudioGameCoreTable.CatalogAvailabilityChanged += HandleAudioCueCatalogAvailabilityChanged;
                ESResourcePlanRuntimeService.PlanAvailabilityChanged += HandleResourcePlanAvailabilityChanged;
            }

            waitingForPlaybackReadiness = true;
        }

        private void UnsubscribeFromPlaybackReadiness()
        {
            if (!waitingForPlaybackReadiness)
                return;

            ESGameManager.AudioModuleAvailabilityChanged -= HandleAudioModuleAvailabilityChanged;
            ESAssets.RuntimeBackendRebuilt -= HandleRuntimeBackendRebuilt;
            ESAudioGameCoreTable.CatalogAvailabilityChanged -= HandleAudioCueCatalogAvailabilityChanged;
            ESResourcePlanRuntimeService.PlanAvailabilityChanged -= HandleResourcePlanAvailabilityChanged;
            waitingForPlaybackReadiness = false;
        }

        /// <summary>
        /// Called only by <see cref="ESAudioModule"/> after this emitter has consumed one bounded
        /// start slot. The second readiness check covers a provider/catalog transition that occurs
        /// between queueing and this frame's execution.
        /// </summary>
        internal void ExecuteQueuedAutoPlay(ESAudioModule queuedByAudio)
        {
            autoPlayQueued = false;
            if (!autoPlayArmed || !isActiveAndEnabled)
                return;

            if (!ReferenceEquals(ESGameManager.Audio, queuedByAudio)
                || !IsConfiguredContentReadyForAutoPlay())
            {
                SubscribeToPlaybackReadiness();
                return;
            }

            autoPlayArmed = false;
            UnsubscribeFromPlaybackReadiness();
            PlayConfigured();
        }

        /// <summary>Called when the audio module is disabled before a queued start can execute.</summary>
        internal void NotifyAutoPlayQueueCleared()
        {
            autoPlayQueued = false;
            if (autoPlayArmed && isActiveAndEnabled)
                SubscribeToPlaybackReadiness();
        }

        private void HandleAudioModuleAvailabilityChanged(ESAudioModule audio)
        {
            if (!autoPlayArmed || !isActiveAndEnabled)
                return;

            TryPlayArmedOnEnable();
        }

        private void HandleRuntimeBackendRebuilt()
        {
            if (autoPlayArmed && isActiveAndEnabled)
                TryPlayArmedOnEnable();
        }

        private void HandleAudioCueCatalogAvailabilityChanged()
        {
            if (autoPlayArmed && isActiveAndEnabled)
                TryPlayArmedOnEnable();
        }

        private void HandleResourcePlanAvailabilityChanged()
        {
            if (autoPlayArmed && isActiveAndEnabled)
                TryPlayArmedOnEnable();
        }

        private void CacheSource()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
        }

#if UNITY_EDITOR
        private void RefreshDiagnosticPrefabPathFromEditor()
        {
            if (Application.isPlaying)
                return;

            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
            if (string.Equals(diagnosticPrefabPath, prefabPath, StringComparison.Ordinal))
                return;

            diagnosticPrefabPath = prefabPath;
            EditorUtility.SetDirty(this);
        }
#endif
    }

    /// <summary>
    /// Explicit root-level collection for VFX emitters. Multiple same-type Emitters must not each
    /// register themselves with ESGenericLife, because one life supports at most one extension of
    /// one concrete type. This Set owns the one pool extension and never scans child hierarchies.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("【ES】/相机与表现/音频与特效/VFX 音频发射器集合")]
    public sealed class ESVfxAudioEmitterSet : MonoBehaviour, IESGameObjectPoolLifecycle
    {
        [Title("使用说明")]
        [InfoBox("单个 Emitter 直接挂在 VFX 根节点即可响应“播放粒子”操作。多个 Emitter、子节点 Emitter 或需要对象池回收前精确停止时，在 VFX 根节点添加本组件，并点击下方按钮收集。一个 Emitter 永远归离它最近的 Set 所有。")]
        [SerializeField, ReadOnly, LabelText("受管 Emitter（由收集按钮维护）")]
        [InfoBox("收集会在遇到子 ESVfxAudioEmitterSet 时停止：子 VFX 必须由自己的 Set 管理，不能被父 VFX 重复播放或停止。对象池回收会先结束这些 Voice，再禁用对象。")]
        private List<ESVfxAudioEmitter> emitters = new List<ESVfxAudioEmitter>(2);

        [NonSerialized] private ESGenericLife genericLife;
        [NonSerialized] private bool registeredAsPoolExtension;

        public IReadOnlyList<ESVfxAudioEmitter> Emitters => emitters;

#if UNITY_EDITOR
        [Button("配置此 VFX 的对象池生命周期（仅编辑器）")]
        [InfoBox("Entity 根：会要求 Entity 为唯一 Pool Root，Set 在运行时作为 Extension 注册。独立 VFX：Set 自身为唯一 Pool Root。此按钮只写入明确配置，不修改运行时自动发现规则。")]
        private void ConfigurePoolLifecycleInEditor()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[ESAudio] 对象池生命周期只能在编辑状态配置。", this);
                return;
            }

            ESGenericLife life = GetComponent<ESGenericLife>();
            if (life == null)
                life = Undo.AddComponent<ESGenericLife>(gameObject);

            Entity entity = GetComponent<Entity>();
            MonoBehaviour desiredRoot = entity != null ? entity : this;
            if (life.PoolRootLifecycleComponent != null
                && !ReferenceEquals(life.PoolRootLifecycleComponent, desiredRoot))
            {
                Debug.LogError(
                    "[ESAudio] 当前 ESGenericLife 已绑定其他 Pool Root，不能自动覆盖。请先处理现有生命周期接收者，再重新配置 VFX 音频。",
                    life);
                return;
            }

            if (ReferenceEquals(life.PoolRootLifecycleComponent, desiredRoot))
            {
                Debug.Log("[ESAudio] 对象池生命周期已符合当前 VFX 音频配置。", life);
                return;
            }

            Undo.RecordObject(life, "配置 VFX 音频对象池生命周期");
            bool temporarilyRegisteredExtension = entity != null && life.RegisterPoolExtension(this);
            bool bound = life.BindPoolRoot((IESGameObjectPoolLifecycle)desiredRoot);
            if (temporarilyRegisteredExtension)
                life.UnregisterPoolExtension(this);

            if (!bound)
            {
                Debug.LogError(
                    "[ESAudio] 无法配置对象池生命周期。请保证 Entity 根只有 Entity 作为 Root，Set 只作为 Extension；"
                    + "独立 VFX 则让 Set 成为唯一 Root。",
                    this);
                return;
            }

            EditorUtility.SetDirty(life);
            EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// Authoring-only convenience. Runtime deliberately reads only the serialized list and
        /// never performs hierarchy discovery during VFX playback or pool lifecycle edges.
        /// </summary>
        [Button("收集子节点 Emitter（仅编辑器）")]
        private void CollectEmittersInEditor()
        {
            Undo.RecordObject(this, "收集 VFX 音频发射器");
            emitters ??= new List<ESVfxAudioEmitter>(2);
            emitters.Clear();
            CollectOwnedEmittersInEditor(transform);
            EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// Child Sets are ownership boundaries. A parent collector must never traverse through one,
        /// otherwise one emitter could be played/stopped by two VFX lifecycles.
        /// </summary>
        private void CollectOwnedEmittersInEditor(Transform current)
        {
            if (current != transform && current.GetComponent<ESVfxAudioEmitterSet>() != null)
                return;

            ESVfxAudioEmitter emitter = current.GetComponent<ESVfxAudioEmitter>();
            if (emitter != null)
                emitters.Add(emitter);

            for (int i = 0; i < current.childCount; i++)
                CollectOwnedEmittersInEditor(current.GetChild(i));
        }
#endif

        private void Awake()
        {
            TryRegisterPoolExtension();
        }

        private void OnDestroy()
        {
            if (registeredAsPoolExtension && genericLife != null)
                genericLife.UnregisterPoolExtension(this);
            registeredAsPoolExtension = false;
            genericLife = null;
        }

        /// <summary>Called before pooled root activation. This method deliberately never calls Play.</summary>
        public void OnPoolSpawned()
        {
            if (emitters == null)
                return;

            for (int i = 0; i < emitters.Count; i++)
                emitters[i]?.ArmForPoolSpawn();
        }

        /// <summary>Called before pooled root deactivation, while bound Sources are still valid.</summary>
        public void OnPoolDespawned()
        {
            StopConfiguredEmitters(ESAudioVoiceEndReason.OwnerDespawned);
        }

        /// <summary>VFX operation entry; only OnVfxPlay emitters in this explicit set are started.</summary>
        public void PlayConfiguredEmitters()
        {
            if (emitters == null)
                return;

            for (int i = 0; i < emitters.Count; i++)
                emitters[i]?.PlayFromVfx();
        }

        /// <summary>Stops all Voices owned by this VFX set through the central audio module.</summary>
        public void StopConfiguredEmitters()
        {
            StopConfiguredEmitters(ESAudioVoiceEndReason.ExplicitStop);
        }

        private void StopConfiguredEmitters(ESAudioVoiceEndReason reason)
        {
            if (emitters == null)
                return;

            for (int i = emitters.Count - 1; i >= 0; i--)
                emitters[i]?.StopForLifecycle(reason);
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            SanitizeEmitterOwnership();
        }

        /// <summary>
        /// Editor-only safety net for old Prefabs or manually edited YAML. Player builds only read
        /// the serialized list and never perform hierarchy discovery during VFX or pool lifecycles.
        /// </summary>
        private void SanitizeEmitterOwnership()
        {
            if (emitters == null)
                return;

            for (int i = emitters.Count - 1; i >= 0; i--)
            {
                ESVfxAudioEmitter candidate = emitters[i];
                if (candidate == null || !IsNearestOwnerSet(candidate) || ContainsEarlierEmitter(candidate, i))
                    emitters.RemoveAt(i);
            }
        }

        private bool IsNearestOwnerSet(ESVfxAudioEmitter emitter)
        {
            Transform current = emitter.transform;
            while (current != null)
            {
                ESVfxAudioEmitterSet owner = current.GetComponent<ESVfxAudioEmitterSet>();
                if (owner != null)
                    return ReferenceEquals(owner, this);

                current = current.parent;
            }

            return false;
        }

        private bool ContainsEarlierEmitter(ESVfxAudioEmitter candidate, int endExclusive)
        {
            for (int i = 0; i < endExclusive; i++)
                if (ReferenceEquals(emitters[i], candidate))
                    return true;
            return false;
        }
        #endif

        private void TryRegisterPoolExtension()
        {
            if (!Application.isPlaying || registeredAsPoolExtension)
                return;

            genericLife = GetComponent<ESGenericLife>();
            if (genericLife == null || genericLife.PoolRootLifecycleComponent == null || genericLife.IsPoolSpawned)
                return;

            registeredAsPoolExtension = genericLife.RegisterPoolExtension(this);
        }
    }
}
