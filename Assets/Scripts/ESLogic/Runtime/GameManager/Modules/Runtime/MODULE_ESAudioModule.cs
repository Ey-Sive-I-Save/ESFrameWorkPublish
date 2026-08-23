using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;

namespace ES
{
    public enum ESAudioVoiceEndReason : byte
    {
        NaturalEnd = 0,
        ExplicitStop = 1,
        OwnerDestroyed = 2,
        Preempted = 3,
        ProviderTransition = 4,
        BackendFailure = 5,
        ModuleDisabled = 6,
        OwnerDisabled = 7,
        OwnerDespawned = 8,
        ResourceOwnerReleased = 9,
        None = 255
    }

    /// <summary>
    /// Publicly observable Voice states. Internal preparation stages intentionally stay private
    /// because they are synchronous and cannot be observed reliably by a polling caller.
    /// </summary>
    public enum ESAudioVoiceState : byte
    {
        PendingLoad,
        Playing,
        Stopping,
        Ended
    }

    /// <summary>Machine-readable cause for a rejected request or a playback failure.</summary>
    public enum ESAudioFailureCode : byte
    {
        None = 0,
        InvalidCueKey = 1,
        RuntimeAssetsNotReady = 2,
        CueNotRegistered = 3,
        RuntimeCueKeyUnavailable = 4,
        NoPlayableVariant = 5,
        CooldownActive = 6,
        MissingAudioClip = 7,
        InvalidDirectClipConfig = 8,
        MissingEmitterOwner = 9,
        EmitterUnavailable = 10,
        ClipConfigNotRegistered = 11,
        SourceConfigurationFailed = 12,
        VoiceAdmissionRejected = 13,
        MusicRestoreFailed = 14,
        VoicePreempted = 15,
        BackendFailure = 16,
        UnexpectedException = 17,
        SourceStartFailed = 18,
        BoundEmitterUnavailable = 19,
        BoundEmitterBusy = 20,
        AutoPlayQueueCapacityExceeded = 21,
        CueClipNotPrewarmed = 22
    }

    public readonly struct ESAudioVoiceHandle : IEquatable<ESAudioVoiceHandle>
    {
        internal readonly int id;
        internal readonly int generation;

        internal ESAudioVoiceHandle(int id, int generation)
        {
            this.id = id;
            this.generation = generation;
        }

        public bool IsValid => id != 0 && generation != 0;

        public bool Equals(ESAudioVoiceHandle other) => id == other.id && generation == other.generation;
        public override bool Equals(object obj) => obj is ESAudioVoiceHandle other && Equals(other);
        public override int GetHashCode() => (id * 397) ^ generation;
    }

    /// <summary>
    /// Snapshot returned for one accepted Voice request. Terminal results are retained for a
    /// bounded history window after the pooled Voice has been returned.
    /// </summary>
    public readonly struct ESAudioVoiceStatus
    {
        public readonly ESAudioVoiceHandle Handle;
        public readonly ESAudioVoiceState State;
        public readonly ESAudioVoiceEndReason EndReason;
        /// <summary>Machine-readable terminal failure detail; None for non-failure endings.</summary>
        public readonly ESAudioFailureCode FailureCode;
        public readonly float EndedAtUnscaledTime;
        public readonly bool IsPaused;

        internal ESAudioVoiceStatus(
            ESAudioVoiceHandle handle,
            ESAudioVoiceState state,
            ESAudioVoiceEndReason endReason,
            ESAudioFailureCode failureCode,
            float endedAtUnscaledTime,
            bool isPaused)
        {
            Handle = handle;
            State = state;
            EndReason = endReason;
            FailureCode = failureCode;
            EndedAtUnscaledTime = endedAtUnscaledTime;
            IsPaused = isPaused;
        }
    }

    /// <summary>
    /// Presentation-only Chinese labels. Runtime decisions must use enums rather than localized
    /// strings so game logic and automated tests remain language independent.
    /// </summary>
    public static class ESAudioDiagnosticText
    {
        public static string GetChineseState(ESAudioVoiceState state)
        {
            switch (state)
            {
                case ESAudioVoiceState.PendingLoad: return "等待资源加载";
                case ESAudioVoiceState.Playing: return "正在播放";
                case ESAudioVoiceState.Stopping: return "正在淡出停止";
                case ESAudioVoiceState.Ended: return "已结束";
                default: return "未知状态";
            }
        }

        public static string GetChineseEndReason(ESAudioVoiceEndReason reason)
        {
            switch (reason)
            {
                case ESAudioVoiceEndReason.None: return "未结束";
                case ESAudioVoiceEndReason.NaturalEnd: return "自然结束";
                case ESAudioVoiceEndReason.ExplicitStop: return "显式停止";
                case ESAudioVoiceEndReason.OwnerDestroyed: return "Owner 已销毁";
                case ESAudioVoiceEndReason.Preempted: return "被高优先级 Voice 抢占";
                case ESAudioVoiceEndReason.ProviderTransition: return "资源后端切换";
                case ESAudioVoiceEndReason.BackendFailure: return "播放后端失败";
                case ESAudioVoiceEndReason.ModuleDisabled: return "音频模块已停用";
                case ESAudioVoiceEndReason.OwnerDisabled: return "Owner 已禁用";
                case ESAudioVoiceEndReason.OwnerDespawned: return "Owner 已回收到对象池";
                case ESAudioVoiceEndReason.ResourceOwnerReleased: return "资源 Owner 已释放所借用的音频资源";
                default: return "未知结束原因";
            }
        }

        public static string GetChineseFailure(ESAudioFailureCode code)
        {
            switch (code)
            {
                case ESAudioFailureCode.InvalidCueKey: return "CueKey 未配置";
                case ESAudioFailureCode.RuntimeAssetsNotReady: return "运行时音频资源尚未就绪";
                case ESAudioFailureCode.CueNotRegistered: return "当前 GameCore 未注册该 Cue";
                case ESAudioFailureCode.RuntimeCueKeyUnavailable: return "无法解析 Cue 的运行时 Key";
                case ESAudioFailureCode.NoPlayableVariant: return "Cue 没有可播放的 Clip 变体";
                case ESAudioFailureCode.CooldownActive: return "Cue 仍在冷却中";
                case ESAudioFailureCode.MissingAudioClip: return "AudioClip 为空";
                case ESAudioFailureCode.InvalidDirectClipConfig: return "直接 Clip 播放配置无效";
                case ESAudioFailureCode.MissingEmitterOwner: return "附着播放缺少有效 Transform";
                case ESAudioFailureCode.EmitterUnavailable: return "音频 Emitter 池无法提供 AudioSource";
                case ESAudioFailureCode.ClipConfigNotRegistered: return "AudioClip ConfigKey 未注册到当前 AssetTable";
                case ESAudioFailureCode.SourceConfigurationFailed: return "AudioSource 配置失败";
                case ESAudioFailureCode.VoiceAdmissionRejected: return "Voice 准入被预算或播放条件拒绝";
                case ESAudioFailureCode.MusicRestoreFailed: return "资源后端切换后音乐恢复失败";
                case ESAudioFailureCode.VoicePreempted: return "Voice 被预算抢占";
                case ESAudioFailureCode.BackendFailure: return "音频后端失败";
                case ESAudioFailureCode.UnexpectedException: return "音频播放发生未预期异常";
                case ESAudioFailureCode.SourceStartFailed: return "AudioSource 未能进入播放状态";
                case ESAudioFailureCode.BoundEmitterUnavailable: return "托管 AudioSource 不可用";
                case ESAudioFailureCode.BoundEmitterBusy: return "托管 AudioSource 正被其他 Voice 使用";
                case ESAudioFailureCode.AutoPlayQueueCapacityExceeded: return "OnEnable 自动播放队列超出容量";
                case ESAudioFailureCode.CueClipNotPrewarmed: return "Cue 的 Clip 未由当前 ResourcePlan 预热并持有";
                default: return "未知音频失败";
            }
        }
    }

    [Serializable]
    public struct ESAudioPlayRequest
    {
        [LabelText("Owner")]
        public Transform owner;

        [LabelText("固定位置")]
        public Vector3 position;

        [LabelText("使用固定位置")]
        public bool hasPosition;

        [LabelText("跟随 Owner")]
        public bool followOwner;

        [LabelText("音量倍率（0 使用 Cue 默认值）")]
        public float volumeScale;

        [LabelText("音高倍率（0 使用 Cue 默认值）")]
        public float pitchScale;

        [LabelText("优先级修正")]
        public int priorityOffset;

        [LabelText("淡入（秒）"), MinValue(0f)]
        public float fadeInSeconds;

        // Internal resource-pipeline bridge only. Public gameplay APIs continue to use CueKey and
        // ResourcePlan; this is for a framework caller that already owns an explicit Scope and
        // must not create a second audio-local one.
        [NonSerialized]
        internal ESAssetScope resourceOwnerScope;
    }

    /// <summary>
    /// Playback-only settings for an already loaded AudioClip. Unlike an Audio Cue, this has no
    /// stable content identity, variants, cooldown, preload contract, or FMOD equivalence.
    /// </summary>
    [Serializable]
    public sealed class ESAudioClipPlayConfig
    {
        [LabelText("覆盖入口分类")]
        public bool overrideCategory;

        [ShowIf(nameof(overrideCategory)), LabelText("分类")]
        public ESAudioCategory category = ESAudioCategory.Sfx;

        [LabelText("覆盖入口 2D / 3D")]
        public bool overrideSpatialMode;

        [ShowIf(nameof(overrideSpatialMode)), LabelText("2D / 3D")]
        public ESAudioSpatialMode spatialMode = ESAudioSpatialMode.TwoD;

        [LabelText("默认循环")]
        public bool loop;

        [LabelText("优先级"), Range(0, 256)]
        public int priority = 128;

        [LabelText("预算策略")]
        public ESAudioCuePreemptionPolicy preemptionPolicy = ESAudioCuePreemptionPolicy.StopLowerPriority;

        [LabelText("基础音量"), Range(0f, 1f)]
        public float volume = 1f;

        [LabelText("音高"), Range(0.1f, 3f)]
        public float pitch = 1f;

        [LabelText("最小距离"), MinValue(0f)]
        public float minDistance = 1f;

        [LabelText("最大距离"), MinValue(0f)]
        public float maxDistance = 30f;

        [LabelText("可选 空间设置"), InlineProperty]
        [FormerlySerializedAs("spatialProfile")]
        public ESAudioSpatialSettings spatialSettings = new ESAudioSpatialSettings();

        public bool TryValidate(out string error)
        {
            if ((uint)category > (uint)ESAudioCategory.Cinematic
                || (uint)spatialMode > (uint)ESAudioSpatialMode.ThreeD
                || (uint)preemptionPolicy > (uint)ESAudioCuePreemptionPolicy.StopLowerPriority)
            {
                error = "AudioClip 播放配置包含无效的分类、空间模式或预算策略。";
                return false;
            }
            if (priority < 0 || priority > 256
                || !IsFinite(volume) || volume < 0f || volume > 1f
                || !IsFinite(pitch) || pitch < 0.1f || pitch > 3f
                || !IsFinite(minDistance) || !IsFinite(maxDistance)
                || minDistance < 0f || maxDistance < minDistance)
            {
                error = "AudioClip 播放配置包含无效的优先级、音量、音高或距离。";
                return false;
            }
            if (spatialSettings != null && !spatialSettings.TryValidate(out error))
                return false;

            error = null;
            return true;
        }

        public ESAudioCategory GetCategory(ESAudioCategory entryDefault)
            => overrideCategory ? category : entryDefault;

        public ESAudioSpatialMode GetSpatialMode(ESAudioSpatialMode entryDefault)
            => overrideSpatialMode ? spatialMode : entryDefault;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    public sealed class ESAudioCategoryVoiceBudget
    {
        public ESAudioCategory category;
        [MinValue(0)] public int maxVoices;
    }

    [Serializable]
    public sealed class ESAudioCategoryMixerRoute
    {
        public ESAudioCategory category;
        public AudioMixerGroup mixerGroup;
    }

    /// <summary>
    /// A serializable category preference used by the game's settings layer. It deliberately
    /// contains no mixer, clip or backend reference, so the same save data works with every
    /// audio backend.
    /// </summary>
    [Serializable]
    public sealed class ESAudioCategoryUserSetting
    {
        public ESAudioCategory category;
        [Range(-80f, 0f)] public float volumeDb;
        public bool muted;
    }

    /// <summary>
    /// A snapshot for game save/settings code. Gains are stored in dB and mute state is stored
    /// separately, so unmuting restores the prior gain. Apply it through ESAudioModule rather
    /// than letting callers manipulate Voice state directly.
    /// </summary>
    [Serializable]
    public sealed class ESAudioUserSettings
    {
        [Range(-80f, 0f)] public float masterVolumeDb;
        public bool masterMuted;
        public List<ESAudioCategoryUserSetting> categorySettings = new List<ESAudioCategoryUserSetting>(6);
    }

    public readonly struct ESAudioVoiceDiagnostic
    {
        public readonly ESAudioVoiceHandle Handle;
        public readonly string CueKey;
        public readonly string ClipKey;
        public readonly ESAudioCategory Category;
        public readonly bool IsLoading;
        public readonly bool IsLoop;
        public readonly int Priority;

        internal ESAudioVoiceDiagnostic(
            ESAudioVoiceHandle handle,
            string cueKey,
            string clipKey,
            ESAudioCategory category,
            bool isLoading,
            bool isLoop,
            int priority)
        {
            Handle = handle;
            CueKey = cueKey;
            ClipKey = clipKey;
            Category = category;
            IsLoading = isLoading;
            IsLoop = isLoop;
            Priority = priority;
        }
    }

    public readonly struct ESAudioFailureDiagnostic
    {
        public readonly string CueKey;
        /// <summary>None when a request was rejected before a Voice Handle existed.</summary>
        public readonly ESAudioVoiceEndReason Reason;
        public readonly ESAudioFailureCode Code;
        /// <summary>Optional non-localized detail for logs; UI text must come from Code.</summary>
        public readonly string TechnicalDetail;

        internal ESAudioFailureDiagnostic(
            string cueKey,
            ESAudioVoiceEndReason reason,
            ESAudioFailureCode code,
            string technicalDetail)
        {
            CueKey = cueKey;
            Reason = reason;
            Code = code;
            TechnicalDetail = technicalDetail;
        }
    }

    /// <summary>
    /// The sole runtime audio authority. Gameplay supplies CueKeys, or an already loaded AudioClip
    /// for simple playback; resource leases, emitters, concurrency and backend transitions stay here.
    /// </summary>
    [Serializable, TypeRegistryItem("系统模块/音频")]
    public sealed class ESAudioModule : ESSystemModule
    {
        private const string EmitterPoolKey = "ES.AudioEmitter";
        private const int FailureCapacity = 64;
        private const int TerminalVoiceHistoryCapacity = 128;
        // Authored OnEnable emitters can all become ready when GameCore finishes injecting its
        // catalog. Keep that edge bounded: the queue is preallocated and only a small fixed
        // number of starts may reach Cue resolution/admission in one frame.
        private const int AutoPlayQueueCapacity = 512;
        // Overflow remains module-owned and FIFO. This avoids waking every blocked emitter to
        // compete for one newly freed execution slot while retaining 1024 waiting requests.
        private const int AutoPlayWaitingQueueCapacity = 1024;
        private const int AutoPlayStartsPerFrame = 16;
        private const int AutoPlayOverflowSourceSampleCapacity = 4;
        private const int FailureLogKeyCapacity = 64;
        private const int ProviderMusicRestoreMaxAttempts = 40;
        private const float ProviderMusicRestoreRetrySeconds = 0.25f;
        private const float MinimumVolumeDb = -80f;
        private const float MinimumLinearVolume = 0.0001f;
        private static readonly ESAudioCategory[] AllCategories =
        {
            ESAudioCategory.Music,
            ESAudioCategory.Ambient,
            ESAudioCategory.Sfx,
            ESAudioCategory.UI,
            ESAudioCategory.Voice,
            ESAudioCategory.Cinematic
        };

        [TitleGroup("Voice 预算")]
        [LabelText("最大 Voice 数"), MinValue(1)]
        public int maxVoices = 32;

        [TitleGroup("Voice 预算")]
        [LabelText("分类预算")]
        public List<ESAudioCategoryVoiceBudget> categoryBudgets = new List<ESAudioCategoryVoiceBudget>(6);

        [TitleGroup("Voice 预算")]
        [LabelText("Emitter 预热数"), MinValue(0)]
        public int initialEmitterCount = 8;

        [TitleGroup("混音路由")]
        [LabelText("分类 Mixer 路由")]
        public List<ESAudioCategoryMixerRoute> categoryMixerRoutes = new List<ESAudioCategoryMixerRoute>(6);

        [TitleGroup("全局设置")]
        [LabelText("Master 音量"), Range(0f, 1f)]
        public float masterVolume = 1f;

        [TitleGroup("全局设置")]
        [LabelText("失焦时暂停")]
        public bool pauseOnFocusLost = true;

        [TitleGroup("全局设置")]
        [LabelText("Provider 重建音乐淡入（秒）"), MinValue(0f)]
        public float providerMusicRestoreFadeInSeconds = 0.25f;

        [TitleGroup("诊断")]
        [LabelText("同类失败日志间隔（秒）"), MinValue(0f)]
        public float failureLogIntervalSeconds = 5f;

        private readonly List<Voice> voices = new List<Voice>(32);
        private readonly List<VoiceAdmissionTransaction> pendingAdmissions = new List<VoiceAdmissionTransaction>(8);
        private readonly ESSimplePool<Voice> voicePool = new ESSimplePool<Voice>(
            factoryMethod: () => new Voice(),
            initCount: 32,
            maxCount: 512,
            poolDisplayName: "Audio Voice Pool",
            groupName: "Audio");
        private readonly ESSimplePool<VoiceAdmissionTransaction> admissionPool = new ESSimplePool<VoiceAdmissionTransaction>(
            factoryMethod: () => new VoiceAdmissionTransaction(),
            initCount: 8,
            maxCount: 128,
            poolDisplayName: "Audio Admission Pool",
            groupName: "Audio");
        private readonly Dictionary<int, float> lastPlayTimeByCue = new Dictionary<int, float>();
        private readonly Dictionary<ESAudioCategory, float> categoryVolumes = new Dictionary<ESAudioCategory, float>();
        private readonly HashSet<ESAudioCategory> mutedCategories = new HashSet<ESAudioCategory>();
        private readonly Dictionary<string, float> lastFailureLogTimeByKey = new Dictionary<string, float>();
        private readonly ESRingBuffer<ESAudioFailureDiagnostic> recentFailures = new ESRingBuffer<ESAudioFailureDiagnostic>(FailureCapacity);
        private readonly TerminalVoiceRecord[] terminalVoiceHistory = new TerminalVoiceRecord[TerminalVoiceHistoryCapacity];
        private readonly ESVfxAudioEmitter[] autoPlayQueue = new ESVfxAudioEmitter[AutoPlayQueueCapacity];
        private readonly ESVfxAudioEmitter[] autoPlayWaitingQueue = new ESVfxAudioEmitter[AutoPlayWaitingQueueCapacity];
        private readonly string[] autoPlayOverflowSourceSamples = new string[AutoPlayOverflowSourceSampleCapacity];

        private Transform emitterRoot;
        private GameObject emitterTemplate;
        private int nextVoiceId = 1;
        private int nextVoiceGeneration = 1;
        private int nextTerminalVoiceHistoryIndex;
        private int autoPlayQueueHead;
        private int autoPlayQueueTail;
        private int autoPlayQueueCount;
        private int autoPlayWaitingQueueHead;
        private int autoPlayWaitingQueueTail;
        private int autoPlayWaitingQueueCount;
        private int autoPlayOverflowCount;
        private int autoPlayOverflowSourceSampleCount;
        private float nextAutoPlayOverflowLogAt;
        private int musicTransitionVersion;
        private ESAudioVoiceHandle currentMusicHandle;
        private ESAudioVoiceHandle pendingMusicHandle;
        private ESAudioVoiceHandle fadingOutMusicHandle;
        private ESAudioCueKey musicCueToRestore;
        private bool providerMusicRestorePending;
        private int providerMusicRestoreAttempts;
        private float providerMusicRestoreNextAttemptAt;
        private bool paused;
        private float pauseStartedAtUnscaledTime;
        private float accumulatedPausedTime;
        private bool muted;
        private bool subscribedToResourceTransitions;

        public int ActiveVoiceCount => voices.Count;
        public int LoadingVoiceCount => pendingAdmissions.Count;
        public int PendingAdmissionCount => pendingAdmissions.Count;
        /// <summary>Diagnostics only: authored OnEnable emitters waiting for their bounded start slot.</summary>
        public int PendingAutoPlayCount => autoPlayQueueCount + autoPlayWaitingQueueCount;
        /// <summary>Diagnostics only: queued OnEnable emitters waiting behind the execution ring.</summary>
        public int PendingAutoPlayWaitingCount => autoPlayWaitingQueueCount;
        public bool IsPaused => paused;
        public bool IsMuted => muted;

        protected override void OnEnable()
        {
            base.OnEnable();
            masterVolume = ClampUnit(masterVolume, 1f);
            SubscribeToResourceTransitions();
            EnsureEmitterPool();
        }

        protected override void OnDisable()
        {
            FlushAutoPlayQueueOverflowDiagnostics(force: true);
            ClearAutoPlayQueue();
            StopAll(ESAudioVoiceEndReason.ModuleDisabled);
            UnsubscribeFromResourceTransitions();
            base.OnDisable();
        }

        protected override void Update()
        {
            CancelDestroyedPendingAdmissions();

            if (!ESAssets.IsReady && HasCueVoices())
            {
                BeginProviderMusicRestore();
                StopCueVoices(ESAudioVoiceEndReason.ProviderTransition);
            }

            if (ESAssets.IsReady)
                TryRestoreMusicAfterProviderTransition();

            ProcessAutoPlayQueue();
            FlushAutoPlayQueueOverflowDiagnostics(force: false);

            float now = AudioClock;
            for (int i = voices.Count - 1; i >= 0; i--)
            {
                Voice voice = voices[i];
                if (!voice.active)
                    continue;

                if (IsLifecycleOwnerMissing(voice))
                {
                    EndVoice(voice, ESAudioVoiceEndReason.OwnerDestroyed, null);
                    continue;
                }
                else if (voice.followOwner)
                {
                    voice.position = voice.owner.position;
                    if (voice.source != null)
                        voice.source.transform.position = voice.position;
                }

                if (voice.fadeOutEndTime > 0f)
                {
                    float remaining = Mathf.Max(0f, voice.fadeOutEndTime - now);
                    float scale = voice.fadeOutDuration <= 0f ? 0f : remaining / voice.fadeOutDuration;
                    ApplyVoiceVolume(voice, scale);
                    if (remaining <= 0f)
                    {
                        EndVoice(voice, ESAudioVoiceEndReason.ExplicitStop, null);
                        continue;
                    }
                }
                else if (voice.fadeInEndTime > 0f)
                {
                    ApplyVoiceVolume(voice, GetFadeInScale(voice, now));
                    if (now >= voice.fadeInEndTime)
                        voice.fadeInEndTime = 0f;
                }

                if (!voice.loading && !paused && voice.source != null && voice.usesPlaybackWindow
                    && (voice.source.timeSamples >= voice.playbackEndSample || !voice.source.isPlaying))
                {
                    if (voice.loop)
                    {
                        voice.source.timeSamples = voice.playbackStartSample;
                        if (!voice.source.isPlaying)
                            voice.source.Play();
                    }
                    else
                    {
                        EndVoice(voice, ESAudioVoiceEndReason.NaturalEnd, null);
                        continue;
                    }
                }

                if (!voice.loading && !paused && voice.source != null && !voice.loop && !voice.source.isPlaying)
                    EndVoice(voice, ESAudioVoiceEndReason.NaturalEnd, null);
            }
        }

        /// <summary>
        /// Adds one authored <see cref="ESVfxAudioEmitter"/> to the bounded OnEnable start queue.
        /// The caller owns duplicate suppression; this method deliberately has no allocation or
        /// hierarchy lookup because it is reached by catalog/backend readiness edges.
        /// </summary>
        internal bool TryEnqueueAutoPlay(ESVfxAudioEmitter emitter)
        {
            if (emitter == null)
                return false;

            // Cancellation can leave a hole in the execution ring while older requests wait.
            // Promote before accepting new work so a later request can never overtake FIFO work.
            PromoteAutoPlayWaiters();
            if (autoPlayQueueCount < AutoPlayQueueCapacity)
                return TryAppendAutoPlay(autoPlayQueue, ref autoPlayQueueTail, ref autoPlayQueueCount, emitter);

            return TryAppendAutoPlay(
                autoPlayWaitingQueue,
                ref autoPlayWaitingQueueTail,
                ref autoPlayWaitingQueueCount,
                emitter);
        }

        /// <summary>
        /// Records an authored OnEnable burst that exceeds both fixed FIFO rings. This path is
        /// intentionally batched: it samples only a few distinct origins and emits one bounded
        /// diagnostic on the audio module's Update instead of letting every rejected Emitter log.
        /// </summary>
        internal void ReportAutoPlayQueueOverflow(ESVfxAudioEmitter emitter)
        {
            autoPlayOverflowCount++;
            if (autoPlayOverflowSourceSampleCount >= AutoPlayOverflowSourceSampleCapacity || emitter == null)
                return;

            string source = emitter.DescribeAutoPlayOriginForDiagnostics();
            for (int i = 0; i < autoPlayOverflowSourceSampleCount; i++)
                if (string.Equals(autoPlayOverflowSourceSamples[i], source, StringComparison.Ordinal))
                    return;

            autoPlayOverflowSourceSamples[autoPlayOverflowSourceSampleCount++] = source;
        }

        /// <summary>
        /// Removes a disabled/despawned authored emitter before its queued start. This is a rare
        /// lifecycle edge, so preserving FIFO order is preferable to leaving stale slots to hold
        /// queue capacity until a later frame.
        /// </summary>
        internal void CancelAutoPlay(ESVfxAudioEmitter emitter)
        {
            if (emitter == null)
                return;

            RemoveAutoPlayEmitter(
                autoPlayQueue,
                ref autoPlayQueueHead,
                ref autoPlayQueueTail,
                ref autoPlayQueueCount,
                emitter);
            RemoveAutoPlayEmitter(
                autoPlayWaitingQueue,
                ref autoPlayWaitingQueueHead,
                ref autoPlayWaitingQueueTail,
                ref autoPlayWaitingQueueCount,
                emitter);
        }

        private void ProcessAutoPlayQueue()
        {
            PromoteAutoPlayWaiters();
            if (autoPlayQueueCount == 0)
                return;

            int countToStart = Mathf.Min(autoPlayQueueCount, AutoPlayStartsPerFrame);
            for (int i = 0; i < countToStart; i++)
            {
                ESVfxAudioEmitter emitter = autoPlayQueue[autoPlayQueueHead];
                autoPlayQueue[autoPlayQueueHead] = null;
                autoPlayQueueHead = (autoPlayQueueHead + 1) % AutoPlayQueueCapacity;
                autoPlayQueueCount--;
                if (emitter != null)
                    emitter.ExecuteQueuedAutoPlay(this);
            }

            PromoteAutoPlayWaiters();
        }

        private void ClearAutoPlayQueue()
        {
            ClearAutoPlayQueue(autoPlayQueue, ref autoPlayQueueHead, ref autoPlayQueueTail, ref autoPlayQueueCount);
            ClearAutoPlayQueue(
                autoPlayWaitingQueue,
                ref autoPlayWaitingQueueHead,
                ref autoPlayWaitingQueueTail,
                ref autoPlayWaitingQueueCount);
        }

        private void PromoteAutoPlayWaiters()
        {
            while (autoPlayQueueCount < AutoPlayQueueCapacity && autoPlayWaitingQueueCount > 0)
            {
                ESVfxAudioEmitter emitter = autoPlayWaitingQueue[autoPlayWaitingQueueHead];
                autoPlayWaitingQueue[autoPlayWaitingQueueHead] = null;
                autoPlayWaitingQueueHead = (autoPlayWaitingQueueHead + 1) % AutoPlayWaitingQueueCapacity;
                autoPlayWaitingQueueCount--;
                if (emitter != null)
                    TryAppendAutoPlay(autoPlayQueue, ref autoPlayQueueTail, ref autoPlayQueueCount, emitter);
            }
        }

        private static bool TryAppendAutoPlay(
            ESVfxAudioEmitter[] queue,
            ref int tail,
            ref int count,
            ESVfxAudioEmitter emitter)
        {
            if (count >= queue.Length)
                return false;

            queue[tail] = emitter;
            tail = (tail + 1) % queue.Length;
            count++;
            return true;
        }

        private static void RemoveAutoPlayEmitter(
            ESVfxAudioEmitter[] queue,
            ref int head,
            ref int tail,
            ref int count,
            ESVfxAudioEmitter emitter)
        {
            int originalCount = count;
            int keptCount = 0;
            for (int i = 0; i < originalCount; i++)
            {
                int sourceIndex = (head + i) % queue.Length;
                ESVfxAudioEmitter candidate = queue[sourceIndex];
                if (ReferenceEquals(candidate, emitter))
                    continue;

                int destinationIndex = (head + keptCount) % queue.Length;
                queue[destinationIndex] = candidate;
                keptCount++;
            }

            for (int i = keptCount; i < originalCount; i++)
                queue[(head + i) % queue.Length] = null;

            tail = (head + keptCount) % queue.Length;
            count = keptCount;
        }

        private static void ClearAutoPlayQueue(
            ESVfxAudioEmitter[] queue,
            ref int head,
            ref int tail,
            ref int count)
        {
            while (count > 0)
            {
                ESVfxAudioEmitter emitter = queue[head];
                queue[head] = null;
                head = (head + 1) % queue.Length;
                count--;
                emitter?.NotifyAutoPlayQueueCleared();
            }

            head = 0;
            tail = 0;
        }

        private void FlushAutoPlayQueueOverflowDiagnostics(bool force)
        {
            if (autoPlayOverflowCount <= 0)
                return;

            float now = Time.unscaledTime;
            if (!force && failureLogIntervalSeconds > 0f && now < nextAutoPlayOverflowLogAt)
                return;

            string sourceSummary = BuildAutoPlayOverflowSourceSummary();
            string technicalDetail = "本批拒绝=" + autoPlayOverflowCount
                                     + "，执行队列=" + autoPlayQueueCount
                                     + "，等待队列=" + autoPlayWaitingQueueCount
                                     + "，示例来源=" + sourceSummary;
            recentFailures.EnqueueOverwrite(new ESAudioFailureDiagnostic(
                "OnEnableAutoPlay",
                ESAudioVoiceEndReason.None,
                ESAudioFailureCode.AutoPlayQueueCapacityExceeded,
                technicalDetail), out _);

            Debug.LogError(
                "[ESAudio] OnEnable 自动播放队列超过 1536 条容量；"
                + ESAudioDiagnosticText.GetChineseFailure(ESAudioFailureCode.AutoPlayQueueCapacityExceeded)
                + "。" + technicalDetail);

            nextAutoPlayOverflowLogAt = now + Mathf.Max(0f, failureLogIntervalSeconds);
            autoPlayOverflowCount = 0;
            Array.Clear(autoPlayOverflowSourceSamples, 0, autoPlayOverflowSourceSamples.Length);
            autoPlayOverflowSourceSampleCount = 0;
        }

        private string BuildAutoPlayOverflowSourceSummary()
        {
            if (autoPlayOverflowSourceSampleCount == 0)
                return "<未能读取来源>";

            string summary = autoPlayOverflowSourceSamples[0];
            for (int i = 1; i < autoPlayOverflowSourceSampleCount; i++)
                summary += " | " + autoPlayOverflowSourceSamples[i];
            return summary;
        }

        public ESAudioVoiceHandle PlayOneShot(ESAudioCueKey cueKey, ESAudioPlayRequest request = default)
            => Play(cueKey, request, false);

        /// <summary>Plays an already loaded Clip as a 2D SFX Voice under the normal audio budgets.</summary>
        public ESAudioVoiceHandle PlayOneShot(AudioClip clip, ESAudioPlayRequest request = default)
            => PlayDirectClip(clip, null, request, false, ESAudioCategory.Sfx, ESAudioSpatialMode.TwoD);

        /// <summary>Plays an already loaded Clip with explicit playback-only settings.</summary>
        public ESAudioVoiceHandle PlayOneShot(
            AudioClip clip,
            ESAudioClipPlayConfig config,
            ESAudioPlayRequest request = default)
            => PlayDirectClip(clip, config, request, false, ESAudioCategory.Sfx, ESAudioSpatialMode.TwoD);

        /// <summary>Plays an already loaded Clip as a 2D UI Voice under the normal audio budgets.</summary>
        public ESAudioVoiceHandle PlayUI(AudioClip clip, ESAudioPlayRequest request = default)
            => PlayDirectClip(clip, null, request, false, ESAudioCategory.UI, ESAudioSpatialMode.TwoD);

        /// <summary>Plays an already loaded Clip as a UI Voice with explicit playback-only settings.</summary>
        public ESAudioVoiceHandle PlayUI(
            AudioClip clip,
            ESAudioClipPlayConfig config,
            ESAudioPlayRequest request = default)
            => PlayDirectClip(clip, config, request, false, ESAudioCategory.UI, ESAudioSpatialMode.TwoD);

        public ESAudioVoiceHandle PlayAttached(ESAudioCueKey cueKey, Transform emitter, ESAudioPlayRequest request = default)
        {
            if (emitter == null)
            {
                RecordFailure(DescribeCueKey(cueKey), ESAudioVoiceEndReason.None,
                    ESAudioFailureCode.MissingEmitterOwner, "PlayAttached requires a valid Transform.");
                return default;
            }

            request.owner = emitter;
            request.followOwner = true;
            request.position = emitter.position;
            request.hasPosition = true;
            return Play(cueKey, request, null);
        }

        public ESAudioVoiceHandle PlayAttached(AudioClip clip, Transform emitter, ESAudioPlayRequest request = default)
            => PlayAttached(clip, emitter, null, request);

        public ESAudioVoiceHandle PlayAttached(
            AudioClip clip,
            Transform emitter,
            ESAudioClipPlayConfig config,
            ESAudioPlayRequest request = default)
        {
            if (emitter == null)
            {
                RecordFailure(DescribeDirectClip(clip), ESAudioVoiceEndReason.None,
                    ESAudioFailureCode.MissingEmitterOwner, "PlayAttached requires a valid Transform.");
                return default;
            }

            request.owner = emitter;
            request.followOwner = true;
            request.position = emitter.position;
            request.hasPosition = true;
            return PlayDirectClip(clip, config, request, null, ESAudioCategory.Sfx, ESAudioSpatialMode.ThreeD);
        }

        public ESAudioVoiceHandle PlayLoop(ESAudioCueKey cueKey, Transform emitter, ESAudioPlayRequest request = default)
        {
            if (emitter == null)
            {
                RecordFailure(DescribeCueKey(cueKey), ESAudioVoiceEndReason.None,
                    ESAudioFailureCode.MissingEmitterOwner, "PlayLoop requires a valid Transform.");
                return default;
            }

            request.owner = emitter;
            request.followOwner = true;
            request.position = emitter.position;
            request.hasPosition = true;
            return Play(cueKey, request, true);
        }

        public ESAudioVoiceHandle PlayLoop(AudioClip clip, Transform emitter, ESAudioPlayRequest request = default)
            => PlayLoop(clip, emitter, null, request);

        public ESAudioVoiceHandle PlayLoop(
            AudioClip clip,
            Transform emitter,
            ESAudioClipPlayConfig config,
            ESAudioPlayRequest request = default)
        {
            if (emitter == null)
            {
                RecordFailure(DescribeDirectClip(clip), ESAudioVoiceEndReason.None,
                    ESAudioFailureCode.MissingEmitterOwner, "PlayLoop requires a valid Transform.");
                return default;
            }

            request.owner = emitter;
            request.followOwner = true;
            request.position = emitter.position;
            request.hasPosition = true;
            return PlayDirectClip(clip, config, request, true, ESAudioCategory.Sfx, ESAudioSpatialMode.ThreeD);
        }

        public ESAudioVoiceHandle PlayAtPosition(ESAudioCueKey cueKey, Vector3 position, ESAudioPlayRequest request = default)
        {
            request.position = position;
            request.hasPosition = true;
            return Play(cueKey, request, null);
        }

        public ESAudioVoiceHandle PlayAtPosition(AudioClip clip, Vector3 position, ESAudioPlayRequest request = default)
            => PlayAtPosition(clip, position, null, request);

        public ESAudioVoiceHandle PlayAtPosition(
            AudioClip clip,
            Vector3 position,
            ESAudioClipPlayConfig config,
            ESAudioPlayRequest request = default)
        {
            request.position = position;
            request.hasPosition = true;
            return PlayDirectClip(clip, config, request, null, ESAudioCategory.Sfx, ESAudioSpatialMode.ThreeD);
        }

        /// <summary>
        /// Plays a Cue through a scene or VFX authored AudioSource. The bound source remains owned
        /// by its GameObject; it never enters the shared emitter pool.
        /// </summary>
        public ESAudioVoiceHandle PlayOnEmitter(
            ESAudioCueKey cueKey,
            ESVfxAudioEmitter emitter,
            ESAudioPlayRequest request = default)
        {
            if (!TryPrepareBoundEmitterRequest(emitter, ref request))
                return default;

            return Play(cueKey, request, null, boundEmitter: emitter);
        }

        /// <summary>Legacy Clip variant of <see cref="PlayOnEmitter(ESAudioCueKey, ESVfxAudioEmitter, ESAudioPlayRequest)"/>.</summary>
        public ESAudioVoiceHandle PlayOnEmitter(
            AudioClip clip,
            ESVfxAudioEmitter emitter,
            ESAudioClipPlayConfig config = null,
            ESAudioPlayRequest request = default)
        {
            if (!TryPrepareBoundEmitterRequest(emitter, ref request))
                return default;

            return PlayDirectClip(
                clip,
                config,
                request,
                null,
                ESAudioCategory.Sfx,
                ESAudioSpatialMode.ThreeD,
                emitter);
        }

        /// <summary>Forces a Cue to loop through a scene or VFX authored AudioSource.</summary>
        public ESAudioVoiceHandle PlayLoopOnEmitter(
            ESAudioCueKey cueKey,
            ESVfxAudioEmitter emitter,
            ESAudioPlayRequest request = default)
        {
            if (!TryPrepareBoundEmitterRequest(emitter, ref request))
                return default;

            return Play(cueKey, request, true, boundEmitter: emitter);
        }

        /// <summary>Forces a legacy Clip to loop through a scene or VFX authored AudioSource.</summary>
        public ESAudioVoiceHandle PlayLoopOnEmitter(
            AudioClip clip,
            ESVfxAudioEmitter emitter,
            ESAudioClipPlayConfig config = null,
            ESAudioPlayRequest request = default)
        {
            if (!TryPrepareBoundEmitterRequest(emitter, ref request))
                return default;

            return PlayDirectClip(
                clip,
                config,
                request,
                true,
                ESAudioCategory.Sfx,
                ESAudioSpatialMode.ThreeD,
                emitter);
        }

        public ESAudioVoiceHandle PlayMusic(ESAudioCueKey cueKey, float fadeOutCurrentSeconds = 0.25f, float fadeInSeconds = 0.25f)
        {
            providerMusicRestorePending = false;
            int transitionVersion = NextMusicTransitionVersion();
            float fadeOut = ClampNonNegative(fadeOutCurrentSeconds);
            var request = new ESAudioPlayRequest { fadeInSeconds = ClampNonNegative(fadeInSeconds) };
            ESAudioCueKey requestedCue = CopyCueKey(cueKey);

            CancelPendingMusicTransition();
            Voice outgoingMusic = GetCurrentMusicVoice();
            StopSupersededMusicVoices(outgoingMusic);
            fadingOutMusicHandle = default;
            ESAudioVoiceHandle outgoingMusicHandle = CreateVoiceHandle(outgoingMusic);

            ESAudioVoiceHandle handle = Play(cueKey, request, true,
                // The outgoing track is explicitly excluded from this admission's effective
                // budget. Other lower-priority Voices remain eligible victims, so a first BGM
                // request is not rejected merely because ordinary SFX filled the global pool.
                allowPreemption: true,
                ignoredVoice: outgoingMusic);
            if (!handle.IsValid)
                return default;

            // Cue admission is synchronous once the ResourcePlan owns the selected Clip. Marking
            // this handle before the hand-off preserves the music state-machine contract that was
            // previously established by the async completion callback.
            pendingMusicHandle = handle;
            if (TryGetVoice(handle, out Voice voice))
                OnMusicVoicePlayable(voice, transitionVersion, fadeOut, requestedCue, outgoingMusicHandle);
            else
                pendingMusicHandle = default;
            return handle;
        }

        public bool Stop(ESAudioVoiceHandle handle, float fadeOutSeconds = 0f)
        {
            if (TryGetAdmission(handle, out VoiceAdmissionTransaction admission))
            {
                if (handle.Equals(currentMusicHandle) || handle.Equals(pendingMusicHandle))
                    ClearMusicIntent();
                CancelAdmission(admission, ESAudioVoiceEndReason.ExplicitStop, null, false);
                return true;
            }

            if (!TryGetVoice(handle, out Voice voice))
                return false;

            if (handle.Equals(currentMusicHandle))
                ClearMusicIntent();

            fadeOutSeconds = ClampNonNegative(fadeOutSeconds);
            if (fadeOutSeconds <= 0f || voice.loading || voice.source == null)
                EndVoice(voice, ESAudioVoiceEndReason.ExplicitStop, null);
            else
            {
                voice.fadeOutDuration = fadeOutSeconds;
                voice.fadeOutEndTime = AudioClock + fadeOutSeconds;
            }
            return true;
        }

        /// <summary>
        /// Lifecycle-only stop path for a dedicated VFX source. It intentionally stays internal so
        /// gameplay cannot use a Component as a second raw audio control surface.
        /// </summary>
        internal bool StopBoundEmitter(ESVfxAudioEmitter emitter, ESAudioVoiceEndReason reason)
        {
            if (emitter == null)
                return false;

            for (int i = pendingAdmissions.Count - 1; i >= 0; i--)
            {
                VoiceAdmissionTransaction admission = pendingAdmissions[i];
                if (ReferenceEquals(admission.voice?.boundEmitter, emitter))
                {
                    CancelAdmission(admission, reason, null, false);
                    return true;
                }
            }

            for (int i = voices.Count - 1; i >= 0; i--)
            {
                Voice voice = voices[i];
                if (ReferenceEquals(voice.boundEmitter, emitter))
                {
                    EndVoice(voice, reason, null);
                    return true;
                }
            }

            return false;
        }

        internal int StopAttachedCue(ESAudioCueKey cueKey, Transform owner, float fadeOutSeconds = 0f)
        {
            if (cueKey == null || !cueKey.IsConfigured || owner == null
                || !ESRuntimeDataGameCore.AudioCues.TryGetRuntimeKey(cueKey, out int runtimeCueKey))
                return 0;

            int stoppedCount = 0;
            for (int i = voices.Count - 1; i >= 0; i--)
            {
                Voice voice = voices[i];
                if (voice.runtimeCueKey != runtimeCueKey || !ReferenceEquals(voice.owner, owner))
                    continue;

                Stop(new ESAudioVoiceHandle(voice.id, voice.generation), fadeOutSeconds);
                stoppedCount++;
            }

            for (int i = pendingAdmissions.Count - 1; i >= 0; i--)
            {
                VoiceAdmissionTransaction admission = pendingAdmissions[i];
                Voice voice = admission.voice;
                if (voice.runtimeCueKey != runtimeCueKey || !ReferenceEquals(voice.owner, owner))
                    continue;

                CancelAdmission(admission, ESAudioVoiceEndReason.ExplicitStop, null, false);
                stoppedCount++;
            }

            return stoppedCount;
        }

        public void StopAll(ESAudioVoiceEndReason reason = ESAudioVoiceEndReason.ExplicitStop)
        {
            if (reason != ESAudioVoiceEndReason.ProviderTransition)
                ClearMusicIntent();

            CancelAllAdmissions(reason, cueOnly: false);
            for (int i = voices.Count - 1; i >= 0; i--)
                EndVoice(voices[i], reason, null);
        }

        public int StopCategory(ESAudioCategory category, float fadeOutSeconds = 0f)
        {
            if (category == ESAudioCategory.Music)
                ClearMusicIntent();

            int stoppedCount = 0;
            for (int i = voices.Count - 1; i >= 0; i--)
            {
                Voice voice = voices[i];
                if (voice.category != category)
                    continue;

                Stop(new ESAudioVoiceHandle(voice.id, voice.generation), fadeOutSeconds);
                stoppedCount++;
            }

            for (int i = pendingAdmissions.Count - 1; i >= 0; i--)
            {
                VoiceAdmissionTransaction admission = pendingAdmissions[i];
                if (admission.voice.category != category)
                    continue;

                CancelAdmission(admission, ESAudioVoiceEndReason.ExplicitStop, null, false);
                stoppedCount++;
            }

            return stoppedCount;
        }

        public void PauseAll()
        {
            if (paused)
                return;

            paused = true;
            pauseStartedAtUnscaledTime = Time.unscaledTime;
            for (int i = 0; i < voices.Count; i++)
                if (!voices[i].loading && voices[i].source != null)
                    voices[i].source.Pause();
        }

        public void ResumeAll()
        {
            if (!paused)
                return;

            accumulatedPausedTime += Mathf.Max(0f, Time.unscaledTime - pauseStartedAtUnscaledTime);
            pauseStartedAtUnscaledTime = 0f;
            paused = false;
            for (int i = 0; i < voices.Count; i++)
                if (!voices[i].loading && voices[i].source != null)
                    voices[i].source.UnPause();
        }

        public void SetCategoryVolume(ESAudioCategory category, float volume)
        {
            categoryVolumes[category] = ClampUnit(volume, 1f);
            RefreshCategoryVoiceVolumes(category);
        }

        public float GetCategoryVolume(ESAudioCategory category)
            => categoryVolumes.TryGetValue(category, out float value) ? value : 1f;

        /// <summary>Sets a category gain in dB for settings UIs and persisted preferences.</summary>
        public void SetCategoryVolumeDb(ESAudioCategory category, float volumeDb)
        {
            SetCategoryVolume(category, DbToLinear(volumeDb));
        }

        /// <summary>Gets the category gain in dB for settings UIs and persisted preferences.</summary>
        public float GetCategoryVolumeDb(ESAudioCategory category)
        {
            return LinearToDb(GetCategoryVolume(category));
        }

        public void SetCategoryMuted(ESAudioCategory category, bool value)
        {
            bool changed = value ? mutedCategories.Add(category) : mutedCategories.Remove(category);
            if (!changed)
                return;

            RefreshCategoryVoiceVolumes(category);
        }

        public bool IsCategoryMuted(ESAudioCategory category) => mutedCategories.Contains(category);

        public void SetMasterVolume(float volume)
        {
            masterVolume = ClampUnit(volume, 1f);
            RefreshVoiceVolumes();
        }

        /// <summary>Sets the master gain in dB for settings UIs and persisted preferences.</summary>
        public void SetMasterVolumeDb(float volumeDb)
        {
            SetMasterVolume(DbToLinear(volumeDb));
        }

        /// <summary>Gets the master gain in dB for settings UIs and persisted preferences.</summary>
        public float GetMasterVolumeDb()
        {
            return LinearToDb(masterVolume);
        }

        public void SetMasterMuted(bool value)
        {
            if (muted == value)
                return;

            muted = value;
            RefreshVoiceVolumes();
        }

        /// <summary>Compatibility spelling for existing callers. New code should use SetMasterMuted.</summary>
        public void SetMuted(bool value)
        {
            SetMasterMuted(value);
        }

        /// <summary>
        /// Applies a settings/save snapshot atomically. Unknown or duplicate category entries are
        /// harmless; the final occurrence is authoritative.
        /// </summary>
        public void ApplyUserSettings(ESAudioUserSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            masterVolume = DbToLinear(settings.masterVolumeDb);
            muted = settings.masterMuted;
            categoryVolumes.Clear();
            mutedCategories.Clear();

            if (settings.categorySettings != null)
            {
                for (int i = 0; i < settings.categorySettings.Count; i++)
                {
                    ESAudioCategoryUserSetting entry = settings.categorySettings[i];
                    if (entry == null)
                        continue;

                    categoryVolumes[entry.category] = DbToLinear(entry.volumeDb);
                    if (entry.muted)
                        mutedCategories.Add(entry.category);
                }
            }

            RefreshVoiceVolumes();
        }

        /// <summary>
        /// Copies the current effective values into a caller-owned settings/save snapshot. Every
        /// known category is emitted so saved defaults remain explicit if categories expand later.
        /// </summary>
        public void CopyUserSettings(ESAudioUserSettings destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            destination.masterVolumeDb = LinearToDb(masterVolume);
            destination.masterMuted = muted;
            if (destination.categorySettings == null)
                destination.categorySettings = new List<ESAudioCategoryUserSetting>(AllCategories.Length);
            else
                destination.categorySettings.Clear();

            for (int i = 0; i < AllCategories.Length; i++)
            {
                ESAudioCategory category = AllCategories[i];
                destination.categorySettings.Add(new ESAudioCategoryUserSetting
                {
                    category = category,
                    volumeDb = LinearToDb(GetCategoryVolume(category)),
                    muted = IsCategoryMuted(category)
                });
            }
        }

        public void CopyVoiceDiagnostics(List<ESAudioVoiceDiagnostic> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            destination.Clear();
            for (int i = 0; i < voices.Count; i++)
            {
                Voice voice = voices[i];
                destination.Add(new ESAudioVoiceDiagnostic(
                    new ESAudioVoiceHandle(voice.id, voice.generation),
                    voice.cueName,
                    DescribeVoiceClip(voice),
                    voice.category,
                    voice.loading,
                    voice.loop,
                    voice.priority));
            }

            for (int i = 0; i < pendingAdmissions.Count; i++)
            {
                Voice voice = pendingAdmissions[i].voice;
                destination.Add(new ESAudioVoiceDiagnostic(
                    new ESAudioVoiceHandle(voice.id, voice.generation),
                    voice.cueName,
                    DescribeVoiceClip(voice),
                    voice.category,
                    true,
                    voice.loop,
                    voice.priority));
            }
        }

        /// <summary>
        /// Copies recent rejected-request and playback-failure summaries. Presentation should map
        /// Code through ESAudioDiagnosticText instead of displaying TechnicalDetail directly.
        /// </summary>
        public void CopyRecentFailures(List<ESAudioFailureDiagnostic> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            destination.Clear();
            if (destination.Capacity < recentFailures.Count)
                destination.Capacity = recentFailures.Count;
            for (int i = 0; i < recentFailures.Count; i++)
                destination.Add(recentFailures[i]);
        }

        /// <summary>
        /// Looks up an accepted Voice request without allocating. A false result means the Handle
        /// was invalid, never accepted, or its terminal record has aged out of the fixed history.
        /// </summary>
        public bool TryGetVoiceStatus(ESAudioVoiceHandle handle, out ESAudioVoiceStatus status)
        {
            if (!handle.IsValid)
            {
                status = default;
                return false;
            }

            if (TryGetAdmission(handle, out _))
            {
                status = new ESAudioVoiceStatus(
                    handle,
                    ESAudioVoiceState.PendingLoad,
                    ESAudioVoiceEndReason.None,
                    ESAudioFailureCode.None,
                    0f,
                    paused);
                return true;
            }

            if (TryGetVoice(handle, out Voice voice))
            {
                ESAudioVoiceState state = voice.fadeOutEndTime > 0f
                    ? ESAudioVoiceState.Stopping
                    : ESAudioVoiceState.Playing;
                status = new ESAudioVoiceStatus(
                    handle,
                    state,
                    ESAudioVoiceEndReason.None,
                    ESAudioFailureCode.None,
                    0f,
                    paused);
                return true;
            }

            return TryGetTerminalVoiceStatus(handle, out status);
        }

        internal void HandleApplicationFocus(bool hasFocus)
        {
            if (!pauseOnFocusLost)
                return;

            if (hasFocus)
                ResumeAll();
            else
                PauseAll();
        }

        private ESAudioVoiceHandle Play(
            ESAudioCueKey cueKey,
            ESAudioPlayRequest request,
            bool? forceLoop,
            bool allowPreemption = true,
            Voice ignoredVoice = null,
            ESVfxAudioEmitter boundEmitter = null)
        {
            if (cueKey == null || !cueKey.IsConfigured)
            {
                RecordFailure("<unconfigured>", ESAudioVoiceEndReason.None,
                    ESAudioFailureCode.InvalidCueKey, "Audio Cue Key is not configured.");
                return default;
            }
            if (!ESAssets.IsReady)
            {
                RecordFailure(DescribeCueKey(cueKey), ESAudioVoiceEndReason.None,
                    ESAudioFailureCode.RuntimeAssetsNotReady, "Audio runtime assets are not ready.");
                return default;
            }
            if (!ESRuntimeDataGameCore.AudioCues.TryGet(cueKey, out ESAudioCueRuntimeData cueData)
                || !cueData.Ready
                || cueData.source == null)
            {
                RecordFailure(DescribeCueKey(cueKey), ESAudioVoiceEndReason.None,
                    ESAudioFailureCode.CueNotRegistered, "Audio Cue is not registered in the current GameCore table.");
                return default;
            }
            if (!ESRuntimeDataGameCore.AudioCues.TryGetRuntimeKey(cueKey, out int runtimeCueKey))
            {
                RecordFailure(DescribeCueKey(cueKey), ESAudioVoiceEndReason.None,
                    ESAudioFailureCode.RuntimeCueKeyUnavailable, "Audio Cue RuntimeKey could not be resolved.");
                return default;
            }
            if (!cueData.source.TrySelectVariant(out ESAssetReferAudioClipConfigKey clipKey))
            {
                RecordFailure(cueData.keyName, ESAudioVoiceEndReason.None,
                    ESAudioFailureCode.NoPlayableVariant, "Audio Cue has no valid Clip variant.");
                return default;
            }

            ESRuntimeAssetCatalog catalog = ESRuntimeAssetCatalog.Current;
            if (catalog == null || !catalog.TryResolveAssetIdentity(
                    ESAssetReferKind.AudioClip,
                    clipKey.EnumKeyInt,
                    clipKey.StringKey,
                    out ESAssetIdentity clipIdentity))
            {
                RecordFailure(cueData.keyName, ESAudioVoiceEndReason.None,
                    ESAudioFailureCode.ClipConfigNotRegistered,
                    "AudioClip ConfigKey is absent from the current AssetTable.");
                return default;
            }

            // A Cue does not create an audio-local Scope. It borrows only a Clip already owned by
            // the active ResourcePlan or an explicitly supplied upper Owner Scope, so Voice
            // lifetime cannot silently extend the resource owner's declared lifetime.
            ESAssetScope borrowedOwnerScope = null;
            if (!ESAssets.TryGetActivePlanAsset(clipIdentity, out AudioClip clip))
            {
                borrowedOwnerScope = request.resourceOwnerScope;
                if (borrowedOwnerScope == null || !borrowedOwnerScope.TryGetResolved(clipIdentity, out clip))
                {
                    RecordFailure(cueData.keyName, ESAudioVoiceEndReason.None,
                        ESAudioFailureCode.CueClipNotPrewarmed,
                        "Cue Clip is not owned by an active ResourcePlan or the explicit Owner Scope. Add this Cue to the plan that owns this gameplay lifetime.");
                    return default;
                }
            }

            float now = Time.unscaledTime;
            if (cueData.source.cooldownSeconds > 0f
                && lastPlayTimeByCue.TryGetValue(runtimeCueKey, out float lastPlay)
                && now - lastPlay < cueData.source.cooldownSeconds)
            {
                RecordFailure(cueData.keyName, ESAudioVoiceEndReason.None,
                    ESAudioFailureCode.CooldownActive, "Audio Cue is cooling down.");
                return default;
            }

            long requestedPriority = (long)cueData.source.priority + request.priorityOffset;
            int priority = requestedPriority < 0L ? 0 : requestedPriority > 256L ? 256 : (int)requestedPriority;

            Voice voice = voicePool.GetInPool();
            voice.id = NextVoiceId();
            voice.generation = NextVoiceGeneration();
            voice.runtimeCueKey = runtimeCueKey;
            voice.cueName = cueData.keyName;
            voice.category = cueData.source.category;
            voice.priority = priority;
            voice.preemptionPolicy = cueData.source.preemptionPolicy;
            voice.loop = forceLoop ?? cueData.source.loop;
            voice.owner = request.owner;
            voice.hasLifecycleOwner = request.owner != null;
            voice.followOwner = request.followOwner;
            voice.position = request.hasPosition ? request.position : request.owner != null ? request.owner.position : Vector3.zero;
            voice.clipKey = clipKey;
            voice.clipIdentity = clipIdentity;
            voice.clipOwnerScope = borrowedOwnerScope;
            voice.sourceConfig = cueData.source;
            voice.spatialMode = cueData.source.spatialMode;
            voice.minDistance = cueData.source.minDistance;
            voice.maxDistance = cueData.source.maxDistance;
            voice.spatialSettings = cueData.source.spatialSettings;
            voice.baseVolume = ResolveBaseVolume(cueData.source, request);
            voice.pitch = ResolvePitch(cueData.source, request);
            voice.createdAt = now;
            voice.fadeInDuration = ClampNonNegative(request.fadeInSeconds);
            voice.fadeInEndTime = 0f;
            voice.loading = false;
            voice.active = false;

            if (!TryAssignBoundEmitter(voice, boundEmitter, out ESAudioFailureCode emitterFailure, out string emitterError))
            {
                RecordFailure(cueData.keyName, ESAudioVoiceEndReason.None, emitterFailure, emitterError);
                voicePool.PushToPool(voice);
                return default;
            }

            VoiceAdmissionTransaction admission = RentAdmission(
                voice,
                isCue: true,
                allowPreemption: allowPreemption,
                ignoredVoice: ignoredVoice);

            AudioSource source = voice.source != null ? voice.source : RentEmitter(voice.position);
            if (source == null)
            {
                FailAdmission(admission, ESAudioVoiceEndReason.BackendFailure, ESAudioFailureCode.EmitterUnavailable,
                    "Audio emitter pool could not provide an AudioSource.");
                return default;
            }

            voice.source = source;
            bool sourceConfigured;
            string configureError;
            try
            {
                sourceConfigured = TryConfigureSource(voice, clip, out configureError);
            }
            catch (Exception exception)
            {
                sourceConfigured = false;
                configureError = exception.Message;
            }
            if (!sourceConfigured)
            {
                FailAdmission(admission, ESAudioVoiceEndReason.BackendFailure,
                    ESAudioFailureCode.SourceConfigurationFailed, configureError);
                return default;
            }

            if (!TryAdmitPreparedVoice(admission, out string admissionError))
            {
                FailAdmission(admission, GetEndReasonForAdmissionFailure(admission.failureCode),
                    admission.failureCode, admissionError);
                return default;
            }

            if (paused)
                source.Pause();
            CompleteAdmission(admission);
            lastPlayTimeByCue[runtimeCueKey] = now;
            return new ESAudioVoiceHandle(voice.id, voice.generation);
        }

        private ESAudioVoiceHandle PlayDirectClip(
            AudioClip clip,
            ESAudioClipPlayConfig config,
            ESAudioPlayRequest request,
            bool? forceLoop,
            ESAudioCategory defaultCategory,
            ESAudioSpatialMode defaultSpatialMode,
            ESVfxAudioEmitter boundEmitter = null)
        {
            if (clip == null)
            {
                RecordFailure("<null AudioClip>", ESAudioVoiceEndReason.None,
                    ESAudioFailureCode.MissingAudioClip, "AudioClip playback requires a Clip.");
                return default;
            }
            if (config != null && !config.TryValidate(out string configError))
            {
                RecordFailure(DescribeDirectClip(clip), ESAudioVoiceEndReason.None,
                    ESAudioFailureCode.InvalidDirectClipConfig, configError);
                return default;
            }

            ESAudioCategory category = config != null ? config.GetCategory(defaultCategory) : defaultCategory;
            ESAudioSpatialMode spatialMode = config != null ? config.GetSpatialMode(defaultSpatialMode) : defaultSpatialMode;
            ESAudioCuePreemptionPolicy preemptionPolicy = config != null
                ? config.preemptionPolicy
                : ESAudioCuePreemptionPolicy.StopLowerPriority;
            float volume = config != null ? config.volume : 1f;
            float pitch = config != null ? config.pitch : 1f;
            float minDistance = config != null ? config.minDistance : 1f;
            float maxDistance = config != null ? config.maxDistance : 30f;
            ESAudioSpatialSettings spatialSettings = config != null ? config.spatialSettings : null;
            bool loop = forceLoop ?? (config != null && config.loop);
            long requestedPriority = (long)(config != null ? config.priority : 128) + request.priorityOffset;
            int priority = requestedPriority < 0L ? 0 : requestedPriority > 256L ? 256 : (int)requestedPriority;

            float now = Time.unscaledTime;
            Voice voice = voicePool.GetInPool();
            voice.id = NextVoiceId();
            voice.generation = NextVoiceGeneration();
            voice.runtimeCueKey = 0;
            voice.cueName = null;
            voice.category = category;
            voice.priority = priority;
            voice.preemptionPolicy = preemptionPolicy;
            voice.loop = loop;
            voice.owner = request.owner;
            voice.hasLifecycleOwner = request.owner != null;
            voice.followOwner = request.followOwner;
            voice.position = request.hasPosition ? request.position : request.owner != null ? request.owner.position : Vector3.zero;
            voice.directClip = clip;
            voice.spatialMode = spatialMode;
            voice.minDistance = minDistance;
            voice.maxDistance = maxDistance;
            voice.spatialSettings = spatialSettings;
            voice.baseVolume = ResolveBaseVolume(volume, request);
            voice.pitch = ResolvePitch(pitch, request);
            voice.createdAt = now;
            voice.fadeInDuration = ClampNonNegative(request.fadeInSeconds);
            voice.fadeInEndTime = 0f;
            voice.loading = false;
            voice.active = false;

            if (!TryAssignBoundEmitter(voice, boundEmitter, out ESAudioFailureCode emitterFailure, out string emitterError))
            {
                RecordFailure(DescribeDirectClip(clip), ESAudioVoiceEndReason.None, emitterFailure, emitterError);
                voicePool.PushToPool(voice);
                return default;
            }

            VoiceAdmissionTransaction admission = RentAdmission(
                voice,
                isCue: false,
                allowPreemption: true,
                ignoredVoice: null);

            AudioSource source = voice.source != null ? voice.source : RentEmitter(voice.position);
            if (source == null)
            {
                FailAdmission(admission, ESAudioVoiceEndReason.BackendFailure, ESAudioFailureCode.EmitterUnavailable,
                    "Audio emitter pool could not provide an AudioSource.");
                return default;
            }

            voice.source = source;
            bool sourceConfigured;
            string configureError;
            try
            {
                sourceConfigured = TryConfigureSource(voice, clip, out configureError);
            }
            catch (Exception exception)
            {
                sourceConfigured = false;
                configureError = exception.Message;
            }
            if (!sourceConfigured)
            {
                FailAdmission(admission, ESAudioVoiceEndReason.BackendFailure,
                    ESAudioFailureCode.SourceConfigurationFailed, configureError);
                return default;
            }

            if (!TryAdmitPreparedVoice(admission, out string admissionError))
            {
                FailAdmission(admission, GetEndReasonForAdmissionFailure(admission.failureCode),
                    admission.failureCode, admissionError);
                return default;
            }

            if (paused)
                source.Pause();
            CompleteAdmission(admission);
            return new ESAudioVoiceHandle(voice.id, voice.generation);
        }

        private bool TryConfigureSource(Voice voice, AudioClip clip, out string error)
        {
            AudioSource source = voice.source;
            if (source == null || !source.isActiveAndEnabled)
            {
                error = "AudioSource is missing, disabled, or inactive before configuration.";
                return false;
            }

            int playbackStartSample;
            int playbackEndSample;
            if (voice.sourceConfig != null)
            {
                if (!voice.sourceConfig.TryResolvePlaybackSampleRange(
                        clip,
                        out playbackStartSample,
                        out playbackEndSample,
                        out error))
                    return false;
            }
            else if (clip == null || clip.samples <= 0 || clip.frequency <= 0)
            {
                error = "AudioClip 没有可播放的 Sample 数据。";
                return false;
            }
            else
            {
                playbackStartSample = 0;
                playbackEndSample = clip.samples;
                error = null;
            }

            voice.playbackStartSample = playbackStartSample;
            voice.playbackEndSample = playbackEndSample;
            voice.usesPlaybackWindow = playbackStartSample > 0 || playbackEndSample < clip.samples;
            source.Stop();
            source.clip = clip;
            source.loop = voice.loop && !voice.usesPlaybackWindow;
            source.pitch = voice.pitch;
            source.playOnAwake = false;
            source.spatialBlend = voice.spatialMode == ESAudioSpatialMode.ThreeD ? 1f : 0f;
            source.minDistance = Mathf.Max(0f, voice.minDistance);
            source.maxDistance = Mathf.Max(source.minDistance, voice.maxDistance);
            ResetOptionalSpatialSettings(source);
            if (voice.spatialMode == ESAudioSpatialMode.ThreeD)
                ApplySpatialSettings(source, voice.spatialSettings);
            source.outputAudioMixerGroup = ResolveCategoryMixerGroup(voice.category);
            if (!voice.usesBoundEmitter)
                source.transform.position = voice.position;
            source.timeSamples = voice.playbackStartSample;
            return true;
        }

        private bool TryStartConfiguredSource(Voice voice, out string error)
        {
            AudioSource source = voice.source;
            if (source == null)
            {
                error = "AudioSource is unavailable before playback starts.";
                return false;
            }

            try
            {
                voice.fadeInEndTime = voice.fadeInDuration > 0f ? AudioClock + voice.fadeInDuration : 0f;
                ApplyVoiceVolume(voice, GetFadeInScale(voice, AudioClock));
                source.Play();
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }

            if (!source.isPlaying)
            {
                error = "AudioSource.Play did not enter the playing state.";
                return false;
            }

            error = null;
            return true;
        }

        private void OnMusicVoicePlayable(
            Voice voice,
            int transitionVersion,
            float fadeOutCurrentSeconds,
            ESAudioCueKey requestedCue,
            ESAudioVoiceHandle outgoingMusicHandle)
        {
            if (!voice.active)
                return;

            // A later PlayMusic request supersedes an older loading request without touching the
            // current track. Only the latest successfully playable Voice may start a crossfade.
            if (transitionVersion != musicTransitionVersion)
            {
                EndVoice(voice, ESAudioVoiceEndReason.ExplicitStop, null);
                return;
            }

            pendingMusicHandle = default;
            currentMusicHandle = new ESAudioVoiceHandle(voice.id, voice.generation);
            musicCueToRestore = requestedCue;
            providerMusicRestorePending = false;
            providerMusicRestoreAttempts = 0;

            for (int i = voices.Count - 1; i >= 0; i--)
            {
                Voice other = voices[i];
                if (ReferenceEquals(other, voice) || other.category != ESAudioCategory.Music)
                    continue;

                ESAudioVoiceHandle otherHandle = CreateVoiceHandle(other);
                if (otherHandle.Equals(outgoingMusicHandle))
                {
                    // Keep both sides of an active crossfade out of normal preemption until the
                    // previous music Voice has actually ended.
                    fadingOutMusicHandle = otherHandle;
                    Stop(otherHandle, fadeOutCurrentSeconds);
                }
                else
                {
                    EndVoice(other, ESAudioVoiceEndReason.ExplicitStop, null);
                }
            }
        }

        private void CancelPendingMusicTransition()
        {
            if (!pendingMusicHandle.IsValid || pendingMusicHandle.Equals(currentMusicHandle))
            {
                pendingMusicHandle = default;
                return;
            }

            if (TryGetAdmission(pendingMusicHandle, out VoiceAdmissionTransaction pendingAdmission))
                CancelAdmission(pendingAdmission, ESAudioVoiceEndReason.ExplicitStop, null, false);
            else if (TryGetVoice(pendingMusicHandle, out Voice pendingVoice))
                EndVoice(pendingVoice, ESAudioVoiceEndReason.ExplicitStop, null);
            pendingMusicHandle = default;
        }

        private Voice GetCurrentMusicVoice()
        {
            if (TryGetVoice(currentMusicHandle, out Voice currentVoice)
                && currentVoice.category == ESAudioCategory.Music)
                return currentVoice;

            Voice fallback = null;
            for (int i = 0; i < voices.Count; i++)
            {
                Voice candidate = voices[i];
                if (candidate.category != ESAudioCategory.Music || candidate.loading)
                    continue;
                if (fallback == null || candidate.createdAt > fallback.createdAt)
                    fallback = candidate;
            }

            currentMusicHandle = fallback != null
                ? new ESAudioVoiceHandle(fallback.id, fallback.generation)
                : default;
            return fallback;
        }

        private void StopSupersededMusicVoices(Voice currentVoice)
        {
            for (int i = voices.Count - 1; i >= 0; i--)
            {
                Voice voice = voices[i];
                if (voice.category == ESAudioCategory.Music && !ReferenceEquals(voice, currentVoice))
                    EndVoice(voice, ESAudioVoiceEndReason.ExplicitStop, null);
            }
        }

        private void ClearMusicIntent()
        {
            currentMusicHandle = default;
            pendingMusicHandle = default;
            fadingOutMusicHandle = default;
            musicCueToRestore = null;
            providerMusicRestorePending = false;
            providerMusicRestoreAttempts = 0;
            providerMusicRestoreNextAttemptAt = 0f;
        }

        private void TryRestoreMusicAfterProviderTransition()
        {
            if (!providerMusicRestorePending)
                return;

            if (musicCueToRestore == null || !musicCueToRestore.IsConfigured)
            {
                providerMusicRestorePending = false;
                return;
            }

            if (TryGetVoice(currentMusicHandle, out _))
            {
                providerMusicRestorePending = false;
                return;
            }

            if (Time.unscaledTime < providerMusicRestoreNextAttemptAt)
                return;

            if (providerMusicRestoreAttempts >= ProviderMusicRestoreMaxAttempts)
            {
                providerMusicRestorePending = false;
                RecordFailure(DescribeCueKey(musicCueToRestore), ESAudioVoiceEndReason.None,
                    ESAudioFailureCode.MusicRestoreFailed,
                    "Music Cue could not be restored after the runtime asset provider transition.");
                return;
            }

            providerMusicRestoreAttempts++;
            providerMusicRestoreNextAttemptAt = Time.unscaledTime + ProviderMusicRestoreRetrySeconds;
            ESAudioVoiceHandle handle = PlayMusic(musicCueToRestore, 0f, providerMusicRestoreFadeInSeconds);
            if (handle.IsValid)
            {
                providerMusicRestorePending = false;
                providerMusicRestoreAttempts = 0;
            }
            else
            {
                providerMusicRestorePending = true;
            }
        }

        private VoiceAdmissionTransaction RentAdmission(
            Voice voice,
            bool isCue,
            bool allowPreemption,
            Voice ignoredVoice)
        {
            VoiceAdmissionTransaction admission = admissionPool.GetInPool();
            admission.Initialize(voice, isCue, allowPreemption, ignoredVoice);
            return admission;
        }

        private bool IsCurrentAdmission(VoiceAdmissionTransaction admission, int id, int generation)
        {
            return admission != null
                && admission.active
                && admission.id == id
                && admission.generation == generation;
        }

        private bool TryGetAdmission(ESAudioVoiceHandle handle, out VoiceAdmissionTransaction result)
        {
            for (int i = 0; i < pendingAdmissions.Count; i++)
            {
                VoiceAdmissionTransaction admission = pendingAdmissions[i];
                if (admission.id == handle.id && admission.generation == handle.generation && admission.active)
                {
                    result = admission;
                    return true;
                }
            }

            result = null;
            return false;
        }

        private void CompleteAdmission(VoiceAdmissionTransaction admission)
        {
            if (admission == null || !admission.active)
                return;

            if (admission.isCue)
                pendingAdmissions.Remove(admission);
            admission.active = false;
            admissionPool.PushToPool(admission);
        }

        private void FailAdmission(
            VoiceAdmissionTransaction admission,
            ESAudioVoiceEndReason reason,
            string error)
        {
            FailAdmission(admission, reason, GetFailureCodeForEndReason(reason), error);
        }

        private void FailAdmission(
            VoiceAdmissionTransaction admission,
            ESAudioVoiceEndReason reason,
            ESAudioFailureCode code,
            string error)
        {
            CancelAdmission(admission, reason, error, reportFailure: true, code: code);
        }

        private void CancelAdmission(
            VoiceAdmissionTransaction admission,
            ESAudioVoiceEndReason reason,
            string error,
            bool reportFailure)
        {
            CancelAdmission(admission, reason, error, reportFailure, GetFailureCodeForEndReason(reason));
        }

        private void CancelAdmission(
            VoiceAdmissionTransaction admission,
            ESAudioVoiceEndReason reason,
            string error,
            bool reportFailure,
            ESAudioFailureCode code)
        {
            if (admission == null || !admission.active)
                return;

            Voice voice = admission.voice;
            ESAudioVoiceHandle handle = CreateVoiceHandle(voice);
            if (admission.isCue)
                pendingAdmissions.Remove(admission);
            admission.active = false;
            if (handle.Equals(pendingMusicHandle))
                pendingMusicHandle = default;

            RecordTerminalVoiceStatus(handle, reason, code);
            if (reportFailure)
                RecordFailure(DescribeVoiceForFailure(voice), reason, code, error);
            DiscardUnstartedVoice(voice);
            admissionPool.PushToPool(admission);
        }

        private void CancelAllAdmissions(ESAudioVoiceEndReason reason, bool cueOnly)
        {
            for (int i = pendingAdmissions.Count - 1; i >= 0; i--)
            {
                VoiceAdmissionTransaction admission = pendingAdmissions[i];
                if (cueOnly && !admission.isCue)
                    continue;

                CancelAdmission(admission, reason, null, false);
            }
        }

        private void CancelDestroyedPendingAdmissions()
        {
            for (int i = pendingAdmissions.Count - 1; i >= 0; i--)
            {
                VoiceAdmissionTransaction admission = pendingAdmissions[i];
                if (IsLifecycleOwnerMissing(admission.voice))
                    CancelAdmission(admission, ESAudioVoiceEndReason.OwnerDestroyed, null, false);
            }
        }

        private bool TryAdmitPreparedVoice(VoiceAdmissionTransaction admission, out string error)
        {
            Voice voice = admission.voice;
            if (!TryPlanAdmission(admission, out error))
            {
                admission.failureCode = ESAudioFailureCode.VoiceAdmissionRejected;
                return false;
            }

            voice.loading = false;
            voice.active = true;
            voices.Add(voice);
            if (!TryStartConfiguredSource(voice, out error))
            {
                voices.Remove(voice);
                voice.active = false;
                admission.failureCode = ESAudioFailureCode.SourceStartFailed;
                return false;
            }

            admission.failureCode = ESAudioFailureCode.None;
            CommitAdmission(admission);
            return true;
        }

        private bool TryPlanAdmission(VoiceAdmissionTransaction admission, out string error)
        {
            Voice voice = admission.voice;
            admission.ClearReservation();
            int maxConcurrent = voice.sourceConfig != null ? voice.sourceConfig.maxConcurrent : 0;
            if (maxConcurrent > 0 && CountVoicesForCue(voice.runtimeCueKey, admission) >= maxConcurrent)
            {
                if (!admission.allowPreemption)
                {
                    error = "Voice budget rejected the Music transition before it could preempt an existing Voice.";
                    return false;
                }

                Voice victim = SelectCueVictim(voice.runtimeCueKey, voice.priority, voice.preemptionPolicy, admission);
                if (victim == null || !admission.TryReserveVictim(victim))
                {
                    error = "Cue concurrency budget rejected the new Voice.";
                    return false;
                }
                if (CountVoicesForCue(voice.runtimeCueKey, admission) >= maxConcurrent)
                {
                    error = "Cue concurrency remains over budget; the new Voice was not admitted.";
                    return false;
                }
            }

            int categoryBudget = GetCategoryBudget(voice.category);
            if (categoryBudget > 0 && CountVoicesForCategory(voice.category, admission) >= categoryBudget)
            {
                if (!admission.allowPreemption)
                {
                    error = "Category Voice budget rejected the Music transition before it could preempt an existing Voice.";
                    return false;
                }

                Voice victim = SelectLowestPriorityVictim(voice.category, voice.priority, admission);
                if (victim == null || !admission.TryReserveVictim(victim))
                {
                    error = "Category Voice budget rejected the new Voice.";
                    return false;
                }
                if (CountVoicesForCategory(voice.category, admission) >= categoryBudget)
                {
                    error = "Category Voice budget remains over limit; the new Voice was not admitted.";
                    return false;
                }
            }

            if (CountVoices(admission) >= Mathf.Max(1, maxVoices))
            {
                if (!admission.allowPreemption)
                {
                    error = "Global Voice budget rejected the Music transition before it could preempt an existing Voice.";
                    return false;
                }

                Voice victim = SelectLowestPriorityVictim(null, voice.priority, admission);
                if (victim == null || !admission.TryReserveVictim(victim))
                {
                    error = "Global Voice budget rejected the new Voice.";
                    return false;
                }
                if (CountVoices(admission) >= Mathf.Max(1, maxVoices))
                {
                    error = "Global Voice budget remains over limit; the new Voice was not admitted.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private void CommitAdmission(VoiceAdmissionTransaction admission)
        {
            for (int i = 0; i < admission.ReservedVictimCount; i++)
            {
                Voice victim = admission.GetReservedVictim(i);
                if (victim != null)
                    EndVoice(victim, ESAudioVoiceEndReason.Preempted, "Voice budget preempted this Voice.");
            }
        }

        private Voice SelectCueVictim(
            int runtimeCueKey,
            int incomingPriority,
            ESAudioCuePreemptionPolicy policy,
            VoiceAdmissionTransaction admission)
        {
            if (policy == ESAudioCuePreemptionPolicy.RejectNew)
                return null;

            Voice candidate = null;
            for (int i = 0; i < voices.Count; i++)
            {
                Voice voice = voices[i];
                if (voice.runtimeCueKey != runtimeCueKey
                    || IsExcludedFromAdmission(voice, admission)
                    || IsTransitionReservedMusicVoice(voice)
                    || (policy == ESAudioCuePreemptionPolicy.StopLowerPriority && voice.priority >= incomingPriority))
                    continue;
                if (candidate == null || voice.createdAt < candidate.createdAt)
                    candidate = voice;
            }
            return candidate;
        }

        private Voice SelectLowestPriorityVictim(
            ESAudioCategory? requiredCategory,
            int incomingPriority,
            VoiceAdmissionTransaction admission)
        {
            Voice candidate = null;
            for (int i = 0; i < voices.Count; i++)
            {
                Voice voice = voices[i];
                if ((requiredCategory.HasValue && voice.category != requiredCategory.Value)
                    || IsExcludedFromAdmission(voice, admission)
                    || IsTransitionReservedMusicVoice(voice)
                    || voice.priority > incomingPriority)
                    continue;
                if (candidate == null
                    || voice.priority < candidate.priority
                    || (voice.priority == candidate.priority && voice.createdAt < candidate.createdAt))
                {
                    candidate = voice;
                }
            }
            return candidate;
        }

        private static bool IsExcludedFromAdmission(Voice voice, VoiceAdmissionTransaction admission)
            => ReferenceEquals(voice, admission.ignoredVoice) || admission.ContainsReservedVictim(voice);

        private int CountVoicesForCue(int runtimeCueKey, VoiceAdmissionTransaction admission)
        {
            int count = 0;
            for (int i = 0; i < voices.Count; i++)
            {
                Voice voice = voices[i];
                if (voice.runtimeCueKey == runtimeCueKey && !IsExcludedFromAdmission(voice, admission))
                    count++;
            }
            return count;
        }

        private int CountVoicesForCategory(ESAudioCategory category, VoiceAdmissionTransaction admission)
        {
            int count = 0;
            for (int i = 0; i < voices.Count; i++)
            {
                Voice voice = voices[i];
                if (voice.category == category && !IsExcludedFromAdmission(voice, admission))
                    count++;
            }
            return count;
        }

        private int CountVoices(VoiceAdmissionTransaction admission)
        {
            int count = voices.Count - admission.ReservedVictimCount;
            if (admission.ignoredVoice != null
                && voices.Contains(admission.ignoredVoice)
                && !admission.ContainsReservedVictim(admission.ignoredVoice))
                count--;
            return count;
        }

        private bool IsTransitionReservedMusicVoice(Voice voice)
        {
            if (voice == null)
                return false;

            if (pendingMusicHandle.IsValid
                && TryGetAdmission(pendingMusicHandle, out _))
            {
                return MatchesHandle(voice, currentMusicHandle);
            }

            if (fadingOutMusicHandle.IsValid && TryGetVoice(fadingOutMusicHandle, out _))
                return MatchesHandle(voice, fadingOutMusicHandle) || MatchesHandle(voice, currentMusicHandle);

            return false;
        }

        private int GetCategoryBudget(ESAudioCategory category)
        {
            if (categoryBudgets == null)
                return 0;

            for (int i = 0; i < categoryBudgets.Count; i++)
            {
                ESAudioCategoryVoiceBudget budget = categoryBudgets[i];
                if (budget != null && budget.category == category)
                    return Mathf.Max(0, budget.maxVoices);
            }
            return 0;
        }

        private bool TryPrepareBoundEmitterRequest(
            ESVfxAudioEmitter emitter,
            ref ESAudioPlayRequest request)
        {
            if (emitter == null)
            {
                RecordFailure("<bound emitter>", ESAudioVoiceEndReason.None,
                    ESAudioFailureCode.BoundEmitterUnavailable,
                    "PlayOnEmitter requires a valid ESVfxAudioEmitter.");
                return false;
            }

            Transform anchor = emitter.transform;
            request.owner = anchor;
            request.followOwner = false;
            request.position = anchor.position;
            request.hasPosition = true;
            return true;
        }

        private bool TryAssignBoundEmitter(
            Voice voice,
            ESVfxAudioEmitter emitter,
            out ESAudioFailureCode failureCode,
            out string error)
        {
            failureCode = ESAudioFailureCode.None;
            error = null;
            if (emitter == null)
                return true;

            if (!emitter.TryGetManagedSource(out AudioSource source)
                || source == null
                || !source.isActiveAndEnabled)
            {
                failureCode = ESAudioFailureCode.BoundEmitterUnavailable;
                error = "The bound AudioSource is missing, disabled, or inactive.";
                return false;
            }

            if (source.isPlaying || IsBoundEmitterInUse(source))
            {
                failureCode = ESAudioFailureCode.BoundEmitterBusy;
                error = "The bound AudioSource already belongs to an active or loading Voice.";
                return false;
            }

            voice.source = source;
            voice.usesBoundEmitter = true;
            voice.boundEmitter = emitter;
            voice.boundEmitterSnapshot = CaptureAudioSourceSnapshot(source);
            return true;
        }

        private bool IsBoundEmitterInUse(AudioSource source)
        {
            for (int i = 0; i < voices.Count; i++)
            {
                Voice voice = voices[i];
                if (voice.usesBoundEmitter && voice.source == source)
                    return true;
            }

            for (int i = 0; i < pendingAdmissions.Count; i++)
            {
                Voice voice = pendingAdmissions[i].voice;
                if (voice != null && voice.usesBoundEmitter && voice.source == source)
                    return true;
            }

            return false;
        }

        private AudioSource RentEmitter(Vector3 position)
        {
            EnsureEmitterPool();
            ESGameObjectPoolModule pool = ESGameManager.PoolModule;
            if (pool == null || emitterTemplate == null)
                return null;

            GameObject emitter = pool.GetInPool(emitterTemplate, position, Quaternion.identity, null, false, 0f);
            if (emitter == null)
                return null;

            AudioSource source = emitter.GetComponent<AudioSource>();
            if (source != null)
                return source;

            if (!pool.PushToPool(emitter))
                UnityEngine.Object.Destroy(emitter);
            return null;
        }

        private void ReturnEmitter(AudioSource source)
        {
            if (source == null)
                return;

            source.Stop();
            source.clip = null;
            source.loop = false;
            source.outputAudioMixerGroup = null;
            source.spatialBlend = 0f;
            source.minDistance = 1f;
            source.maxDistance = 500f;
            ResetOptionalSpatialSettings(source);
            source.transform.SetParent(null, false);

            ESGameObjectPoolModule pool = ESGameManager.PoolModule;
            if (pool == null || !pool.PushToPool(source.gameObject))
                UnityEngine.Object.Destroy(source.gameObject);
        }

        private static AudioSourceSnapshot CaptureAudioSourceSnapshot(AudioSource source)
        {
            AnimationCurve customRolloffCurve = source.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
            return new AudioSourceSnapshot
            {
                clip = source.clip,
                loop = source.loop,
                playOnAwake = source.playOnAwake,
                volume = source.volume,
                pitch = source.pitch,
                spatialBlend = source.spatialBlend,
                minDistance = source.minDistance,
                maxDistance = source.maxDistance,
                rolloffMode = source.rolloffMode,
                hasCustomRolloffCurve = customRolloffCurve != null,
                customRolloffCurve = customRolloffCurve,
                dopplerLevel = source.dopplerLevel,
                spread = source.spread,
                reverbZoneMix = source.reverbZoneMix,
                spatialize = source.spatialize,
                spatializePostEffects = source.spatializePostEffects,
                outputAudioMixerGroup = source.outputAudioMixerGroup
            };
        }

        private static void RestoreBoundEmitter(AudioSource source, AudioSourceSnapshot snapshot)
        {
            if (source == null)
                return;

            source.Stop();
            source.clip = snapshot.clip;
            source.loop = snapshot.loop;
            source.playOnAwake = snapshot.playOnAwake;
            source.volume = snapshot.volume;
            source.pitch = snapshot.pitch;
            source.spatialBlend = snapshot.spatialBlend;
            source.minDistance = snapshot.minDistance;
            source.maxDistance = snapshot.maxDistance;
            source.rolloffMode = snapshot.rolloffMode;
            // A bound Source belongs to content authors. Restore both states explicitly: leaving
            // the previous Voice's curve installed when the authored Source had none corrupts
            // later VFX/scene playback just as much as restoring the wrong curve would.
            source.SetCustomCurve(
                AudioSourceCurveType.CustomRolloff,
                snapshot.hasCustomRolloffCurve ? snapshot.customRolloffCurve : null);
            source.dopplerLevel = snapshot.dopplerLevel;
            source.spread = snapshot.spread;
            source.reverbZoneMix = snapshot.reverbZoneMix;
            source.spatialize = snapshot.spatialize;
            source.spatializePostEffects = snapshot.spatializePostEffects;
            source.outputAudioMixerGroup = snapshot.outputAudioMixerGroup;
        }

        private void ReleaseVoiceEmitter(Voice voice, ESAudioVoiceHandle handle)
        {
            if (voice == null)
                return;

            AudioSource source = voice.source;
            if (voice.usesBoundEmitter)
            {
                RestoreBoundEmitter(source, voice.boundEmitterSnapshot);
                voice.boundEmitter?.NotifyVoiceEnded(handle);
            }
            else
            {
                ReturnEmitter(source);
            }

            voice.source = null;
            voice.usesBoundEmitter = false;
            voice.boundEmitter = null;
            voice.boundEmitterSnapshot = default;
        }

        private void EnsureEmitterPool()
        {
            if (emitterTemplate != null)
                return;

            ESGameObjectPoolModule pool = ESGameManager.PoolModule;
            if (pool == null)
                return;

            var root = new GameObject("ESAudioEmitterRoot") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(root);
            emitterRoot = root.transform;

            emitterTemplate = new GameObject("ESAudioEmitterTemplate") { hideFlags = HideFlags.HideAndDontSave };
            emitterTemplate.transform.SetParent(emitterRoot, false);
            AudioSource source = emitterTemplate.AddComponent<AudioSource>();
            source.playOnAwake = false;
            emitterTemplate.SetActive(false);

            pool.Register(emitterTemplate, EmitterPoolKey);
            if (initialEmitterCount > 0)
                pool.Prewarm(emitterTemplate, initialEmitterCount, EmitterPoolKey);
        }

        private AudioMixerGroup ResolveCategoryMixerGroup(ESAudioCategory category)
        {
            if (categoryMixerRoutes == null)
                return null;

            // Later entries intentionally override earlier entries, matching user-setting snapshots.
            for (int i = categoryMixerRoutes.Count - 1; i >= 0; i--)
            {
                ESAudioCategoryMixerRoute route = categoryMixerRoutes[i];
                if (route != null && route.category == category)
                    return route.mixerGroup;
            }

            return null;
        }

        private static void ResetOptionalSpatialSettings(AudioSource source)
        {
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.dopplerLevel = 0f;
            source.spread = 0f;
            source.reverbZoneMix = 0f;
            source.spatialize = false;
            source.spatializePostEffects = false;
        }

        private static void ApplySpatialSettings(AudioSource source, ESAudioSpatialSettings settings)
        {
            if (settings == null)
                return;

            if (settings.useCustomRolloff)
            {
                source.rolloffMode = AudioRolloffMode.Custom;
                source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, settings.customRolloffCurve);
            }
            if (settings.enableDoppler)
                source.dopplerLevel = settings.dopplerLevel;
            if (settings.enableSpread)
                source.spread = settings.spread;
            if (settings.enableReverbZoneMix)
                source.reverbZoneMix = settings.reverbZoneMix;
            if (settings.enableSpatializer)
            {
                source.spatialize = true;
                source.spatializePostEffects = settings.spatializePostEffects;
            }
        }

        private void EndVoice(Voice voice, ESAudioVoiceEndReason reason, string error)
        {
            if (voice == null || !voice.active)
                return;

            var handle = new ESAudioVoiceHandle(voice.id, voice.generation);
            bool endingCurrentMusic = handle.Equals(currentMusicHandle);
            if (endingCurrentMusic)
                currentMusicHandle = default;
            if (handle.Equals(pendingMusicHandle))
                pendingMusicHandle = default;
            if (handle.Equals(fadingOutMusicHandle))
                fadingOutMusicHandle = default;
            if (endingCurrentMusic && reason == ESAudioVoiceEndReason.ResourceOwnerReleased)
                ClearMusicIntent();

            RecordTerminalVoiceStatus(handle, reason, GetFailureCodeForEndReason(reason));
            voice.active = false;
            voices.Remove(voice);
            ReleaseVoiceEmitter(voice, handle);
            if (reason == ESAudioVoiceEndReason.BackendFailure || reason == ESAudioVoiceEndReason.Preempted)
                RecordFailure(DescribeVoiceForFailure(voice), reason, GetFailureCodeForEndReason(reason), error);
            voicePool.PushToPool(voice);
        }

        private void DiscardUnstartedVoice(Voice voice)
        {
            if (voice == null)
                return;

            ReleaseVoiceEmitter(voice, CreateVoiceHandle(voice));
            voicePool.PushToPool(voice);
        }

        private void RecordTerminalVoiceStatus(
            ESAudioVoiceHandle handle,
            ESAudioVoiceEndReason reason,
            ESAudioFailureCode failureCode)
        {
            if (!handle.IsValid)
                return;

            terminalVoiceHistory[nextTerminalVoiceHistoryIndex] = new TerminalVoiceRecord
            {
                id = handle.id,
                generation = handle.generation,
                endReason = reason,
                failureCode = failureCode,
                endedAtUnscaledTime = Time.unscaledTime
            };
            nextTerminalVoiceHistoryIndex++;
            if (nextTerminalVoiceHistoryIndex >= TerminalVoiceHistoryCapacity)
                nextTerminalVoiceHistoryIndex = 0;
        }

        private bool TryGetTerminalVoiceStatus(ESAudioVoiceHandle handle, out ESAudioVoiceStatus status)
        {
            for (int offset = 1; offset <= TerminalVoiceHistoryCapacity; offset++)
            {
                int index = nextTerminalVoiceHistoryIndex - offset;
                if (index < 0)
                    index += TerminalVoiceHistoryCapacity;

                TerminalVoiceRecord record = terminalVoiceHistory[index];
                if (record.id != handle.id || record.generation != handle.generation)
                    continue;

                status = new ESAudioVoiceStatus(
                    handle,
                    ESAudioVoiceState.Ended,
                    record.endReason,
                    record.failureCode,
                    record.endedAtUnscaledTime,
                    false);
                return true;
            }

            status = default;
            return false;
        }

        private static bool IsCurrentVoice(Voice voice, int voiceId, int voiceGeneration)
            => voice != null && voice.active && voice.id == voiceId && voice.generation == voiceGeneration;

        private static bool IsLifecycleOwnerMissing(Voice voice)
            => voice != null
               && voice.owner == null
               && (voice.hasLifecycleOwner || voice.followOwner);

        private bool TryGetVoice(ESAudioVoiceHandle handle, out Voice result)
        {
            for (int i = 0; i < voices.Count; i++)
            {
                Voice voice = voices[i];
                if (voice.id == handle.id && voice.generation == handle.generation && voice.active)
                {
                    result = voice;
                    return true;
                }
            }

            result = null;
            return false;
        }

        private void ApplyVoiceVolume(Voice voice, float fadeScale)
        {
            if (voice.source == null)
                return;

            float muteScale = muted || IsCategoryMuted(voice.category) ? 0f : 1f;
            voice.source.volume = Mathf.Clamp01(voice.baseVolume * masterVolume * GetCategoryVolume(voice.category) * fadeScale * muteScale);
        }

        private void RefreshCategoryVoiceVolumes(ESAudioCategory category)
        {
            float now = AudioClock;
            for (int i = 0; i < voices.Count; i++)
            {
                Voice voice = voices[i];
                if (voice.category != category)
                    continue;

                float fadeScale = voice.fadeOutEndTime > 0f
                    ? Mathf.Max(0f, voice.fadeOutEndTime - now) / Mathf.Max(0.0001f, voice.fadeOutDuration)
                    : GetFadeInScale(voice, now);
                ApplyVoiceVolume(voice, fadeScale);
            }
        }

        private void RefreshVoiceVolumes()
        {
            float now = AudioClock;
            for (int i = 0; i < voices.Count; i++)
            {
                Voice voice = voices[i];
                float fadeScale = voice.fadeOutEndTime > 0f
                    ? Mathf.Max(0f, voice.fadeOutEndTime - now) / Mathf.Max(0.0001f, voice.fadeOutDuration)
                    : GetFadeInScale(voice, now);
                ApplyVoiceVolume(voice, fadeScale);
            }
        }

        private static float GetFadeInScale(Voice voice, float now)
        {
            if (voice.fadeInEndTime <= 0f || voice.fadeInDuration <= 0f)
                return 1f;

            return Mathf.Clamp01(1f - (voice.fadeInEndTime - now) / voice.fadeInDuration);
        }

        private void SubscribeToResourceTransitions()
        {
            if (subscribedToResourceTransitions)
                return;

            ESAssets.RuntimeBackendTransitionStarting += OnRuntimeBackendTransitionStarting;
            ESAssets.ActivePlanAssetOwnershipEnding += OnActivePlanAssetOwnershipEnding;
            ESAssets.ScopeOwnershipEnding += OnScopeOwnershipEnding;
            subscribedToResourceTransitions = true;
        }

        private void UnsubscribeFromResourceTransitions()
        {
            if (!subscribedToResourceTransitions)
                return;

            ESAssets.RuntimeBackendTransitionStarting -= OnRuntimeBackendTransitionStarting;
            ESAssets.ActivePlanAssetOwnershipEnding -= OnActivePlanAssetOwnershipEnding;
            ESAssets.ScopeOwnershipEnding -= OnScopeOwnershipEnding;
            subscribedToResourceTransitions = false;
        }

        private void OnRuntimeBackendTransitionStarting()
        {
            BeginProviderMusicRestore();
            StopCueVoices(ESAudioVoiceEndReason.ProviderTransition);
        }

        /// <summary>
        /// The final ResourcePlan owner is about to unpublish this asset and dispose its Scope.
        /// Cue Voices borrow this ownership rather than creating a playback-local Scope, so they
        /// must stop synchronously before the borrowed Clip can become invalid.
        /// </summary>
        private void OnActivePlanAssetOwnershipEnding(ESAssetIdentity identity)
        {
            if (!identity.IsValid)
                return;

            for (int i = pendingAdmissions.Count - 1; i >= 0; i--)
            {
                VoiceAdmissionTransaction admission = pendingAdmissions[i];
                Voice pendingVoice = admission?.voice;
                if (admission != null && admission.isCue && pendingVoice != null
                    && pendingVoice.clipIdentity.Equals(identity))
                {
                    CancelAdmission(admission, ESAudioVoiceEndReason.ResourceOwnerReleased, null, false);
                }
            }

            for (int i = voices.Count - 1; i >= 0; i--)
            {
                Voice voice = voices[i];
                if (voice.sourceConfig != null && voice.clipIdentity.Equals(identity))
                    EndVoice(voice, ESAudioVoiceEndReason.ResourceOwnerReleased, null);
            }
        }

        private void OnScopeOwnershipEnding(ESAssetScope scope)
        {
            if (scope == null)
                return;

            for (int i = pendingAdmissions.Count - 1; i >= 0; i--)
            {
                VoiceAdmissionTransaction admission = pendingAdmissions[i];
                if (admission != null && admission.isCue && ReferenceEquals(admission.voice?.clipOwnerScope, scope))
                    CancelAdmission(admission, ESAudioVoiceEndReason.ResourceOwnerReleased, null, false);
            }

            for (int i = voices.Count - 1; i >= 0; i--)
            {
                Voice voice = voices[i];
                if (voice.sourceConfig != null && ReferenceEquals(voice.clipOwnerScope, scope))
                    EndVoice(voice, ESAudioVoiceEndReason.ResourceOwnerReleased, null);
            }
        }

        private bool HasCueVoices()
        {
            for (int i = 0; i < voices.Count; i++)
                if (voices[i].sourceConfig != null)
                    return true;

            for (int i = 0; i < pendingAdmissions.Count; i++)
                if (pendingAdmissions[i].isCue)
                    return true;

            return false;
        }

        private void StopCueVoices(ESAudioVoiceEndReason reason)
        {
            CancelAllAdmissions(reason, cueOnly: true);
            for (int i = voices.Count - 1; i >= 0; i--)
                if (voices[i].sourceConfig != null)
                    EndVoice(voices[i], reason, null);
        }

        /// <summary>
        /// Readiness predicate for authored OnEnable emitters. It performs no load and changes no
        /// ownership: every selectable Cue variant must already be published by an active
        /// ResourcePlan, otherwise random selection could still choose an unprepared Clip.
        /// </summary>
        internal bool IsCuePreparedForPlayback(ESAudioCueKey cueKey)
        {
            if (cueKey == null || !cueKey.IsConfigured || !ESAssets.IsReady || !ESAudioGameCoreTable.IsCatalogReady)
                return false;
            if (!ESRuntimeDataGameCore.AudioCues.TryGet(cueKey, out ESAudioCueRuntimeData cueData)
                || !cueData.Ready || cueData.source == null)
                return false;

            ESRuntimeAssetCatalog catalog = ESRuntimeAssetCatalog.Current;
            List<ESAudioCueVariant> variants = cueData.source.variants;
            if (catalog == null || variants == null)
                return false;

            bool foundSelectableVariant = false;
            for (int i = 0; i < variants.Count; i++)
            {
                ESAudioCueVariant variant = variants[i];
                if (variant == null || variant.clipKey == null || !variant.clipKey.IsConfigured
                    || !IsFinite(variant.weight) || variant.weight < 0.01f)
                    continue;

                foundSelectableVariant = true;
                if (!catalog.TryResolveAssetIdentity(
                        ESAssetReferKind.AudioClip,
                        variant.clipKey.EnumKeyInt,
                        variant.clipKey.StringKey,
                        out ESAssetIdentity identity)
                    || !ESAssets.TryGetActivePlanAsset(identity, out AudioClip clip)
                    || clip == null)
                {
                    return false;
                }
            }

            return foundSelectableVariant;
        }

        private void BeginProviderMusicRestore()
        {
            providerMusicRestorePending = musicCueToRestore != null && musicCueToRestore.IsConfigured;
            providerMusicRestoreAttempts = 0;
            providerMusicRestoreNextAttemptAt = 0f;
        }

        private void RecordFailure(string cueKey, ESAudioVoiceEndReason reason, string message)
        {
            RecordFailure(cueKey, reason, GetFailureCodeForEndReason(reason), message);
        }

        private void RecordFailure(
            string cueKey,
            ESAudioVoiceEndReason reason,
            ESAudioFailureCode code,
            string message)
        {
            recentFailures.EnqueueOverwrite(new ESAudioFailureDiagnostic(cueKey, reason, code, message), out _);

            string logKey = cueKey + "|" + reason + "|" + code + "|" + message;
            if (!lastFailureLogTimeByKey.ContainsKey(logKey) && lastFailureLogTimeByKey.Count >= FailureLogKeyCapacity)
                lastFailureLogTimeByKey.Clear();
            float now = Time.unscaledTime;
            if (failureLogIntervalSeconds > 0f
                && lastFailureLogTimeByKey.TryGetValue(logKey, out float lastLogTime)
                && now - lastLogTime < failureLogIntervalSeconds)
                return;

            lastFailureLogTimeByKey[logKey] = now;
            Debug.LogWarning(
                "[ESAudio] Cue=" + cueKey
                + "，原因码=" + code
                + "，原因=" + ESAudioDiagnosticText.GetChineseFailure(code)
                + "，结束语义=" + ESAudioDiagnosticText.GetChineseEndReason(reason)
                + (string.IsNullOrEmpty(message) ? string.Empty : "，技术详情=" + message));
        }

        private static ESAudioFailureCode GetFailureCodeForEndReason(ESAudioVoiceEndReason reason)
        {
            switch (reason)
            {
                case ESAudioVoiceEndReason.Preempted: return ESAudioFailureCode.VoicePreempted;
                case ESAudioVoiceEndReason.BackendFailure: return ESAudioFailureCode.BackendFailure;
                default: return ESAudioFailureCode.None;
            }
        }

        private static ESAudioVoiceEndReason GetEndReasonForAdmissionFailure(ESAudioFailureCode code)
        {
            return code == ESAudioFailureCode.VoiceAdmissionRejected
                ? ESAudioVoiceEndReason.Preempted
                : ESAudioVoiceEndReason.BackendFailure;
        }

        private static string DescribeCueKey(ESAudioCueKey key)
            => key == null ? "<null>" : ESConfigKeyMatch.Describe(key.EnumKeyInt, key.StringKey);

        private static ESAudioCueKey CopyCueKey(ESAudioCueKey source)
        {
            if (source == null || !source.IsConfigured)
                return null;

            return new ESAudioCueKey
            {
                enumKey = (ESAudioCueEnumKey)source.EnumKeyInt,
                stringKey = source.StringKey
            };
        }

        private static string DescribeClipKey(ESAssetReferAudioClipConfigKey key)
            => key == null ? "<null>" : ESConfigKeyMatch.Describe(key.EnumKeyInt, key.StringKey);

        private static string DescribeDirectClip(AudioClip clip)
            => clip == null ? "<null AudioClip>" : "AudioClip:" + (string.IsNullOrEmpty(clip.name) ? "<unnamed>" : clip.name);

        private static string DescribeVoiceClip(Voice voice)
            => voice != null && voice.directClip != null ? DescribeDirectClip(voice.directClip) : DescribeClipKey(voice?.clipKey);

        private static string DescribeVoiceForFailure(Voice voice)
        {
            if (voice != null && voice.directClip != null)
                return DescribeDirectClip(voice.directClip);

            return !string.IsNullOrEmpty(voice?.cueName)
                ? voice.cueName
                : DescribeClipKey(voice?.clipKey);
        }

        private static float ResolveBaseVolume(ESAudioCueInfo cue, ESAudioPlayRequest request)
        {
            float min = Mathf.Clamp01(Mathf.Min(cue.randomVolume.x, cue.randomVolume.y));
            float max = Mathf.Clamp01(Mathf.Max(cue.randomVolume.x, cue.randomVolume.y));
            float requestScale = IsFinite(request.volumeScale) && request.volumeScale > 0f ? request.volumeScale : 1f;
            return Mathf.Clamp01(cue.volume * UnityEngine.Random.Range(min, max) * requestScale);
        }

        private static float ResolveBaseVolume(float volume, ESAudioPlayRequest request)
        {
            float requestScale = IsFinite(request.volumeScale) && request.volumeScale > 0f ? request.volumeScale : 1f;
            return Mathf.Clamp01(volume * requestScale);
        }

        private static float ResolvePitch(ESAudioCueInfo cue, ESAudioPlayRequest request)
        {
            float min = Mathf.Clamp(Mathf.Min(cue.randomPitch.x, cue.randomPitch.y), 0.1f, 3f);
            float max = Mathf.Clamp(Mathf.Max(cue.randomPitch.x, cue.randomPitch.y), min, 3f);
            float requestScale = IsFinite(request.pitchScale) && request.pitchScale > 0f ? request.pitchScale : 1f;
            return Mathf.Clamp(UnityEngine.Random.Range(min, max) * requestScale, 0.1f, 3f);
        }

        private static float ResolvePitch(float pitch, ESAudioPlayRequest request)
        {
            float requestScale = IsFinite(request.pitchScale) && request.pitchScale > 0f ? request.pitchScale : 1f;
            return Mathf.Clamp(pitch * requestScale, 0.1f, 3f);
        }

        private static float ClampNonNegative(float value)
            => IsFinite(value) && value > 0f ? value : 0f;

        private static float ClampUnit(float value, float fallback)
            => IsFinite(value) ? Mathf.Clamp01(value) : fallback;

        /// <summary>Converts a persisted dB gain to the linear gain used by AudioSource.</summary>
        public static float DbToLinear(float volumeDb)
        {
            float clampedDb = ClampVolumeDb(volumeDb, 0f);
            return clampedDb <= MinimumVolumeDb ? 0f : Mathf.Pow(10f, clampedDb / 20f);
        }

        /// <summary>Converts a runtime linear gain to the dB value persisted in user settings.</summary>
        public static float LinearToDb(float linearVolume)
        {
            float clampedLinear = ClampUnit(linearVolume, 1f);
            return clampedLinear <= MinimumLinearVolume
                ? MinimumVolumeDb
                : Mathf.Clamp(20f * Mathf.Log10(clampedLinear), MinimumVolumeDb, 0f);
        }

        private static float ClampVolumeDb(float value, float fallback)
            => IsFinite(value) ? Mathf.Clamp(value, MinimumVolumeDb, 0f) : fallback;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static ESAudioVoiceHandle CreateVoiceHandle(Voice voice)
            => voice != null ? new ESAudioVoiceHandle(voice.id, voice.generation) : default;

        private static bool MatchesHandle(Voice voice, ESAudioVoiceHandle handle)
            => handle.IsValid && voice.id == handle.id && voice.generation == handle.generation;

        private float AudioClock => (paused ? pauseStartedAtUnscaledTime : Time.unscaledTime) - accumulatedPausedTime;

        private int NextVoiceId()
        {
            int id = nextVoiceId;
            nextVoiceId = nextVoiceId == int.MaxValue ? 1 : nextVoiceId + 1;
            return id;
        }

        private int NextVoiceGeneration()
        {
            int generation = nextVoiceGeneration;
            nextVoiceGeneration = nextVoiceGeneration == int.MaxValue ? 1 : nextVoiceGeneration + 1;
            return generation;
        }

        private int NextMusicTransitionVersion()
        {
            musicTransitionVersion = musicTransitionVersion == int.MaxValue ? 1 : musicTransitionVersion + 1;
            return musicTransitionVersion;
        }

        public override void OnDestroy()
        {
            FlushAutoPlayQueueOverflowDiagnostics(force: true);
            ClearAutoPlayQueue();
            StopAll(ESAudioVoiceEndReason.ModuleDisabled);
            UnsubscribeFromResourceTransitions();
            ESGameObjectPoolModule pool = ESGameManager.PoolModule;
            if (pool != null && emitterTemplate != null)
                pool.Clear(emitterTemplate);
            if (emitterRoot != null)
                UnityEngine.Object.Destroy(emitterRoot.gameObject);
            emitterRoot = null;
            emitterTemplate = null;
            base.OnDestroy();
        }

        private struct TerminalVoiceRecord
        {
            public int id;
            public int generation;
            public ESAudioVoiceEndReason endReason;
            public ESAudioFailureCode failureCode;
            public float endedAtUnscaledTime;
        }

        private struct AudioSourceSnapshot
        {
            public AudioClip clip;
            public bool loop;
            public bool playOnAwake;
            public float volume;
            public float pitch;
            public float spatialBlend;
            public float minDistance;
            public float maxDistance;
            public AudioRolloffMode rolloffMode;
            public bool hasCustomRolloffCurve;
            public AnimationCurve customRolloffCurve;
            public float dopplerLevel;
            public float spread;
            public float reverbZoneMix;
            public bool spatialize;
            public bool spatializePostEffects;
            public AudioMixerGroup outputAudioMixerGroup;
        }

        private sealed class VoiceAdmissionTransaction : IPoolable
        {
            public bool IsRecycled { get; set; }
            public bool active;
            public int id;
            public int generation;
            public Voice voice;
            public bool isCue;
            public bool allowPreemption;
            public Voice ignoredVoice;
            public ESAudioFailureCode failureCode;

            private Voice cueVictim;
            private Voice categoryVictim;
            private Voice globalVictim;
            private int reservedVictimCount;

            public int ReservedVictimCount => reservedVictimCount;

            public void Initialize(Voice source, bool isCueAdmission, bool canPreempt, Voice ignored)
            {
                voice = source;
                id = source.id;
                generation = source.generation;
                isCue = isCueAdmission;
                allowPreemption = canPreempt;
                ignoredVoice = ignored;
                failureCode = ESAudioFailureCode.None;
                active = true;
                ClearReservation();
            }

            public void ClearReservation()
            {
                cueVictim = null;
                categoryVictim = null;
                globalVictim = null;
                reservedVictimCount = 0;
            }

            public bool ContainsReservedVictim(Voice candidate)
            {
                return ReferenceEquals(candidate, cueVictim)
                    || ReferenceEquals(candidate, categoryVictim)
                    || ReferenceEquals(candidate, globalVictim);
            }

            public bool TryReserveVictim(Voice candidate)
            {
                if (candidate == null || ContainsReservedVictim(candidate))
                    return candidate != null;

                switch (reservedVictimCount)
                {
                    case 0:
                        cueVictim = candidate;
                        break;
                    case 1:
                        categoryVictim = candidate;
                        break;
                    case 2:
                        globalVictim = candidate;
                        break;
                    default:
                        return false;
                }

                reservedVictimCount++;
                return true;
            }

            public Voice GetReservedVictim(int index)
            {
                switch (index)
                {
                    case 0:
                        return cueVictim;
                    case 1:
                        return categoryVictim;
                    case 2:
                        return globalVictim;
                    default:
                        return null;
                }
            }

            public void OnResetAsPoolable()
            {
                active = false;
                id = 0;
                generation = 0;
                voice = null;
                isCue = false;
                allowPreemption = false;
                ignoredVoice = null;
                failureCode = ESAudioFailureCode.None;
                ClearReservation();
            }
        }

        private sealed class Voice : IPoolable
        {
            public bool IsRecycled { get; set; }
            public int id;
            public int generation;
            public int runtimeCueKey;
            public string cueName;
            public ESAudioCategory category;
            public int priority;
            public ESAudioCuePreemptionPolicy preemptionPolicy;
            public bool loop;
            public Transform owner;
            public bool hasLifecycleOwner;
            public bool followOwner;
            public Vector3 position;
            public ESAssetReferAudioClipConfigKey clipKey;
            public ESAssetIdentity clipIdentity;
            public ESAssetScope clipOwnerScope;
            public AudioClip directClip;
            public ESAudioCueInfo sourceConfig;
            public ESAudioSpatialMode spatialMode;
            public float minDistance;
            public float maxDistance;
            public ESAudioSpatialSettings spatialSettings;
            public float baseVolume;
            public float pitch;
            public float createdAt;
            public float fadeInDuration;
            public float fadeInEndTime;
            public float fadeOutDuration;
            public float fadeOutEndTime;
            public int playbackStartSample;
            public int playbackEndSample;
            public bool usesPlaybackWindow;
            public bool loading;
            public bool active;
            public AudioSource source;
            public bool usesBoundEmitter;
            public ESVfxAudioEmitter boundEmitter;
            public AudioSourceSnapshot boundEmitterSnapshot;

            public void OnResetAsPoolable()
            {
                id = 0;
                generation = 0;
                runtimeCueKey = 0;
                cueName = null;
                category = default;
                priority = 0;
                preemptionPolicy = default;
                loop = false;
                owner = null;
                hasLifecycleOwner = false;
                followOwner = false;
                position = default;
                clipKey = null;
                clipIdentity = default;
                clipOwnerScope = null;
                directClip = null;
                sourceConfig = null;
                spatialMode = default;
                minDistance = 0f;
                maxDistance = 0f;
                spatialSettings = null;
                baseVolume = 0f;
                pitch = 0f;
                createdAt = 0f;
                fadeInDuration = 0f;
                fadeInEndTime = 0f;
                fadeOutDuration = 0f;
                fadeOutEndTime = 0f;
                playbackStartSample = 0;
                playbackEndSample = 0;
                usesPlaybackWindow = false;
                loading = false;
                active = false;
                source = null;
                usesBoundEmitter = false;
                boundEmitter = null;
                boundEmitterSnapshot = default;
            }
        }
    }
}
