using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    public enum ESAudioCategory : byte
    {
        Music,
        Ambient,
        Sfx,
        UI,
        Voice,
        Cinematic
    }

    public enum ESAudioSpatialMode : byte
    {
        TwoD,
        ThreeD
    }

    public enum ESAudioCuePreemptionPolicy : byte
    {
        RejectNew,
        StopOldest,
        StopLowerPriority
    }

    /// <summary>
    /// Optional Unity-native spatial controls. All features default to disabled, so ordinary
    /// Cues retain the lightweight 2D/3D distance path and perform no extra per-frame work.
    /// </summary>
    [Serializable]
    public sealed class ESAudioSpatialProfile
    {
        [LabelText("自定义衰减曲线")]
        public bool useCustomRolloff;

        [ShowIf(nameof(useCustomRolloff))]
        [LabelText("衰减曲线")]
        public AnimationCurve customRolloffCurve;

        [LabelText("启用 Doppler")]
        public bool enableDoppler;

        [ShowIf(nameof(enableDoppler)), LabelText("Doppler 强度"), Range(0f, 5f)]
        public float dopplerLevel = 1f;

        [LabelText("启用 Spread")]
        public bool enableSpread;

        [ShowIf(nameof(enableSpread)), LabelText("Spread 角度"), Range(0f, 360f)]
        public float spread;

        [LabelText("启用 Reverb Zone")]
        public bool enableReverbZoneMix;

        [ShowIf(nameof(enableReverbZoneMix)), LabelText("Reverb Zone Mix"), Range(0f, 1.1f)]
        public float reverbZoneMix = 1f;

        [LabelText("启用 Spatializer")]
        public bool enableSpatializer;

        [ShowIf(nameof(enableSpatializer)), LabelText("Spatializer 后处理")]
        public bool spatializePostEffects;

        public bool TryValidate(out string error)
        {
            if (useCustomRolloff)
            {
                if (customRolloffCurve == null || customRolloffCurve.length < 2)
                {
                    error = "自定义衰减曲线至少需要两个有效关键帧。";
                    return false;
                }

                Keyframe[] keys = customRolloffCurve.keys;
                for (int i = 0; i < keys.Length; i++)
                {
                    Keyframe key = keys[i];
                    if (!IsFinite(key.time) || !IsFinite(key.value)
                        || !IsFinite(key.inTangent) || !IsFinite(key.outTangent))
                    {
                        error = "自定义衰减曲线不能包含 NaN 或 Infinity。";
                        return false;
                    }
                }
            }

            if (enableDoppler && (!IsFinite(dopplerLevel) || dopplerLevel < 0f || dopplerLevel > 5f))
            {
                error = "Doppler 强度必须是 0 到 5 之间的有限数值。";
                return false;
            }

            if (enableSpread && (!IsFinite(spread) || spread < 0f || spread > 360f))
            {
                error = "Spread 角度必须是 0 到 360 之间的有限数值。";
                return false;
            }

            if (enableReverbZoneMix && (!IsFinite(reverbZoneMix) || reverbZoneMix < 0f || reverbZoneMix > 1.1f))
            {
                error = "Reverb Zone Mix 必须是 0 到 1.1 之间的有限数值。";
                return false;
            }

            error = null;
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    public sealed class ESAudioCueVariant
    {
        [LabelText("音频剪辑"), InlineProperty]
        public ESAssetReferAudioClipConfigKey clipKey = new ESAssetReferAudioClipConfigKey();

        [LabelText("权重"), MinValue(0.01f)]
        public float weight = 1f;
    }

    [ESCreatePath("数据信息/GameCore", "音频 Cue")]
    public sealed class ESAudioCueInfo : SoDataInfo, IGameCoreSO
    {
        [TitleGroup("基础")]
        [LabelText("Cue Key"), InlineProperty]
        public ESAudioCueKey key = new ESAudioCueKey();

        [TitleGroup("基础")]
        [LabelText("分类")]
        public ESAudioCategory category = ESAudioCategory.Sfx;

        [TitleGroup("基础")]
        [LabelText("2D / 3D")]
        public ESAudioSpatialMode spatialMode = ESAudioSpatialMode.ThreeD;

        [TitleGroup("播放规则")]
        [LabelText("默认循环")]
        public bool loop;

        [TitleGroup("播放规则")]
        [LabelText("优先级"), Range(0, 256)]
        public int priority = 128;

        [TitleGroup("播放规则")]
        [LabelText("最大并发"), MinValue(0)]
        [InfoBox("0 表示只受分类和全局 Voice 预算限制。")]
        public int maxConcurrent;

        [TitleGroup("播放规则")]
        [LabelText("并发策略")]
        public ESAudioCuePreemptionPolicy preemptionPolicy = ESAudioCuePreemptionPolicy.RejectNew;

        [TitleGroup("播放规则")]
        [LabelText("冷却（秒）"), MinValue(0f)]
        public float cooldownSeconds;

        [TitleGroup("播放规则")]
        [LabelText("基础音量"), Range(0f, 1f)]
        public float volume = 1f;

        [TitleGroup("播放规则")]
        [LabelText("随机音量"), MinMaxSlider(0f, 1f, true)]
        public Vector2 randomVolume = Vector2.one;

        [TitleGroup("播放规则")]
        [LabelText("随机音高"), MinMaxSlider(0.1f, 3f, true)]
        public Vector2 randomPitch = Vector2.one;

        [TitleGroup("三维")]
        [ShowIf(nameof(IsThreeDimensional))]
        [LabelText("最小距离"), MinValue(0f)]
        public float minDistance = 1f;

        [TitleGroup("三维")]
        [ShowIf(nameof(IsThreeDimensional))]
        [LabelText("最大距离"), MinValue(0f)]
        public float maxDistance = 30f;

        [TitleGroup("三维/高级")]
        [ShowIf(nameof(IsThreeDimensional))]
        [LabelText("可选 Spatial Profile"), InlineProperty]
        public ESAudioSpatialProfile spatialProfile = new ESAudioSpatialProfile();

        [TitleGroup("播放窗口")]
        [LabelText("启用播放窗口")]
        public bool usePlaybackWindow = false;

        [TitleGroup("播放窗口")]
        [ShowIf(nameof(usePlaybackWindow))]
        [LabelText("开始时间（秒）"), MinValue(0f)]
        public float playbackStartSeconds;

        [TitleGroup("播放窗口")]
        [ShowIf(nameof(usePlaybackWindow))]
        [LabelText("结束时间（秒，0 表示 Clip 末尾）"), MinValue(0f)]
        public float playbackEndSeconds;

        [TitleGroup("Unity 变体")]
        [ListDrawerSettings(ShowFoldout = true)]
        public List<ESAudioCueVariant> variants = new List<ESAudioCueVariant>(1);

        private bool IsThreeDimensional => spatialMode == ESAudioSpatialMode.ThreeD;

        public void InjectGameCoreTables()
        {
            ESAudioGameCoreTable.Inject(this);
        }

        public bool TrySelectVariant(out ESAssetReferAudioClipConfigKey selected)
        {
            selected = null;
            if (variants == null)
                return false;

            float totalWeight = 0f;
            for (int i = 0; i < variants.Count; i++)
            {
                ESAudioCueVariant variant = variants[i];
                if (!IsSelectableVariant(variant))
                    continue;

                totalWeight += variant.weight;
                if (!IsFinite(totalWeight))
                    return false;
            }

            if (totalWeight <= 0f)
                return false;

            float cursor = UnityEngine.Random.value * totalWeight;
            ESAssetReferAudioClipConfigKey lastValid = null;
            for (int i = 0; i < variants.Count; i++)
            {
                ESAudioCueVariant variant = variants[i];
                if (!IsSelectableVariant(variant))
                    continue;

                lastValid = variant.clipKey;
                cursor -= variant.weight;
                if (cursor <= 0f)
                {
                    selected = variant.clipKey;
                    return true;
                }
            }

            selected = lastValid;
            return selected != null;
        }

        public bool HasValidVariant()
        {
            if (variants == null)
                return false;

            for (int i = 0; i < variants.Count; i++)
            {
                ESAudioCueVariant variant = variants[i];
                if (IsSelectableVariant(variant))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves this Cue's authored playback window against a concrete Clip. Runtime and
        /// Editor preview share this conversion so both hear the same sample interval.
        /// </summary>
        public bool TryResolvePlaybackSampleRange(
            AudioClip clip,
            out int startSample,
            out int endSample,
            out string error)
        {
            startSample = 0;
            endSample = 0;
            if (clip == null || clip.samples <= 0 || clip.frequency <= 0)
            {
                error = "Audio Clip 没有可播放的 Sample 数据。";
                return false;
            }
            if (!IsFinite(playbackStartSeconds) || !IsFinite(playbackEndSeconds))
            {
                error = "Audio Cue 的播放窗口秒数必须是有限数值。";
                return false;
            }
            if (!usePlaybackWindow)
            {
                startSample = 0;
                endSample = clip.samples;
                error = null;
                return true;
            }
            if (playbackStartSeconds < 0f || playbackEndSeconds < 0f
                || (playbackEndSeconds > 0f && playbackEndSeconds <= playbackStartSeconds))
            {
                error = "Audio Cue 的播放窗口配置无效。";
                return false;
            }

            int clipSamples = clip.samples;
            startSample = SecondsToSample(playbackStartSeconds, clip.frequency, clipSamples);
            endSample = playbackEndSeconds > 0f
                ? SecondsToSample(playbackEndSeconds, clip.frequency, clipSamples)
                : clipSamples;
            if (startSample >= clipSamples || endSample <= startSample)
            {
                error = "Audio Cue 的播放窗口不包含有效的 Clip Sample。";
                return false;
            }

            error = null;
            return true;
        }

        public bool TryValidate(out string error)
        {
            if (key == null || !key.IsConfigured)
            {
                error = "Audio Cue 必须配置 EnumKey 或 StringKey。";
                return false;
            }

            if (!HasValidVariant())
            {
                error = "Audio Cue 至少需要一个有效的 AudioClip ConfigKey 变体。";
                return false;
            }

            if ((uint)category > (uint)ESAudioCategory.Cinematic
                || (uint)spatialMode > (uint)ESAudioSpatialMode.ThreeD
                || (uint)preemptionPolicy > (uint)ESAudioCuePreemptionPolicy.StopLowerPriority)
            {
                error = "Audio Cue 包含无效的分类、空间模式或并发策略。";
                return false;
            }

            if (priority < 0 || priority > 256 || maxConcurrent < 0)
            {
                error = "Audio Cue 的优先级必须在 0 到 256 之间，最大并发不能为负数。";
                return false;
            }

            if (!IsFinite(cooldownSeconds) || cooldownSeconds < 0f
                || !IsFinite(volume) || volume < 0f || volume > 1f)
            {
                error = "Audio Cue 的冷却和基础音量必须是有限的合法数值。";
                return false;
            }

            if (!IsFinite(playbackStartSeconds) || !IsFinite(playbackEndSeconds)
                || (usePlaybackWindow
                    && (playbackStartSeconds < 0f || playbackEndSeconds < 0f
                        || (playbackEndSeconds > 0f && playbackEndSeconds <= playbackStartSeconds))))
            {
                error = "Audio Cue 的播放窗口必须是有限非负区间，结束时间为 0 或大于开始时间。";
                return false;
            }

            if (!IsInRange(randomVolume.x, 0f, 1f) || !IsInRange(randomVolume.y, 0f, 1f)
                || randomVolume.x > randomVolume.y)
            {
                error = "Audio Cue 的随机音量必须是 0 到 1 之间的有限递增区间。";
                return false;
            }

            if (!IsInRange(randomPitch.x, 0.1f, 3f) || !IsInRange(randomPitch.y, 0.1f, 3f)
                || randomPitch.x > randomPitch.y)
            {
                error = "Audio Cue 的随机音高必须是 0.1 到 3 之间的有限递增区间。";
                return false;
            }

            if (!IsFinite(minDistance) || !IsFinite(maxDistance)
                || minDistance < 0f || maxDistance < minDistance)
            {
                error = "Audio Cue 的距离必须是有限的非负数，且最大距离不能小于最小距离。";
                return false;
            }

            if (spatialMode == ESAudioSpatialMode.ThreeD
                && spatialProfile != null
                && !spatialProfile.TryValidate(out error))
                return false;

            for (int i = 0; i < variants.Count; i++)
            {
                ESAudioCueVariant variant = variants[i];
                if (variant == null || variant.clipKey == null || !variant.clipKey.IsConfigured)
                    continue;

                if (!IsFinite(variant.weight) || variant.weight < 0.01f)
                {
                    error = "Audio Cue 变体权重必须是大于等于 0.01 的有限数值。";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool IsSelectableVariant(ESAudioCueVariant variant)
        {
            return variant != null
                && variant.clipKey != null
                && variant.clipKey.IsConfigured
                && IsFinite(variant.weight)
                && variant.weight >= 0.01f;
        }

        private static bool IsInRange(float value, float min, float max)
            => IsFinite(value) && value >= min && value <= max;

        private static int SecondsToSample(float seconds, int frequency, int maximumSample)
        {
            double sample = seconds * frequency;
            if (sample <= 0d)
                return 0;
            if (sample >= maximumSample)
                return maximumSample;
            return (int)Math.Floor(sample);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Audio Cue 的唯一 GameCore 注入入口。</summary>
    public static class ESAudioGameCoreTable
    {
        public static ESAudioCueConfigKeyTable Table => ESRuntimeDataGameCore.AudioCues;
        /// <summary>
        /// True only after the current AudioCue table build has completed. This says the catalog
        /// phase is complete, not that every arbitrary CueKey is valid; callers still resolve
        /// their own CueKey normally and receive a non-transient failure for an unknown key.
        /// </summary>
        public static bool IsCatalogReady { get; private set; }

        /// <summary>
        /// Framework lifecycle edge for authored OnEnable playback. It is deliberately not a
        /// gameplay event and must not be used for polling or general business flow.
        /// </summary>
        public static event Action CatalogAvailabilityChanged;

        public static void Inject(ESAudioCueInfo info)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));
            if (!info.TryValidate(out string validationError))
                throw new InvalidOperationException("Audio Cue 配置无效：" + info.name + "，" + validationError);

            bool ownsBuild = !Table.IsBuilding;
            if (ownsBuild)
            {
                NotifyCatalogBuildStarted();
                Table.BeginBuild();
            }

            try
            {
                if (Table.TryGet(info.key, out ESAudioCueRuntimeData existing))
                {
                    if (ReferenceEquals(existing.source, info))
                        return;
                    throw new InvalidOperationException("Audio Cue GameCore Key 重复：" + info.name);
                }

                ESAudioCueRuntimeData data = Table.AcquireRetained(info.key);
                try
                {
                    data.keyName = ESConfigKeyMatch.Describe(info.key.EnumKeyInt, info.key.StringKey);
                    data.displayName = info.name;
                    data.source = info;
                    int runtimeKey = Table.CommitRetained(info.key, data, info.name);
                    if (runtimeKey == 0)
                        throw new InvalidOperationException("Audio Cue GameCore 注入失败：" + info.name);
                }
                catch
                {
                    Table.AbandonRetained(data);
                    throw;
                }
            }
            finally
            {
                if (ownsBuild)
                {
                    Table.EndBuild();
                    NotifyCatalogBuildCompleted();
                }
            }
        }

        internal static void NotifyCatalogBuildStarted()
        {
            if (!IsCatalogReady)
                return;

            IsCatalogReady = false;
            NotifyCatalogAvailabilityChanged();
        }

        internal static void NotifyCatalogBuildCompleted()
        {
            IsCatalogReady = true;
            NotifyCatalogAvailabilityChanged();
        }

        internal static void NotifyCatalogUnavailable()
        {
            if (!IsCatalogReady)
                return;

            IsCatalogReady = false;
            NotifyCatalogAvailabilityChanged();
        }

        private static void NotifyCatalogAvailabilityChanged()
        {
            try
            {
                CatalogAvailabilityChanged?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
