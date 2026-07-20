using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [CreateTrackItem(TrackItemType.Skill, "动画轨道")]
    public class SkillTrackItem_Animation : SkillTrackItem<SkillTrackClip_Animation>
    {
        [TitleGroup("动画轨道", "按时间采样 AnimationClip，编辑器预览可使用高级过渡混合。")]
        [HideInInspector]
        public string AnimationIM = "这是一个动画轨道";
        public override Color ItemBGColor => new Color(0.25f, 0.45f, 0.78f, 0.42f);

        public SkillTrackItem_Animation()
        {
            displayName = "动画轨道";
        }

        [TitleGroup("编辑器预览", "只影响轨道窗口预览表现，不改变运行时数据。")]
        [LabelText("使用高级过渡预览")]
        public bool useAdvancedPreviewTransition = true;

        [TitleGroup("编辑器预览")]
        [ShowIf(nameof(useAdvancedPreviewTransition))]
        [LabelText("过渡时长")]
        [MinValue(0f)]
        [SuffixLabel("秒", true)]
        public float previewTransitionDuration = 0.15f;

#if UNITY_EDITOR
        public override List<IEditorTimeSampler> CreateEditorSamplers(ITrackSequence sequence, object editorTarget)
        {
            var runtimeTarget = editorTarget as ESRuntimeTargetPack;
            if (useAdvancedPreviewTransition)
                return CreateAdvancedEditorSamplers(sequence, runtimeTarget);

            var trackSampler = new AnimationTrackEditorSampler(this, DisplayName);
            var list = new List<IEditorTimeSampler>
            {
                trackSampler
            };

            if (clips == null)
                return list;

            for (int i = 0; i < clips.Count; i++)
            {
                SkillTrackClip_Animation clip = clips[i];
                if (clip == null || !clip.Enabled)
                    continue;

                AnimationClipEditorSampler clipSampler = clip.CreateAnimationClipEditorSampler(sequence, this, runtimeTarget);
                if (clipSampler == null)
                    continue;

                trackSampler.AddClipSampler(clipSampler);
                list.Add(new TrackClipEditorSampler(clip, clipSampler));
            }

            return list;
        }

        private List<IEditorTimeSampler> CreateAdvancedEditorSamplers(ITrackSequence sequence, ESRuntimeTargetPack runtimeTarget)
        {
            bool allowIdleOnlyFallback = ShouldCreateIdleOnlyFallback(sequence, runtimeTarget);
            var trackSampler = new AdvancedAnimationTrackEditorSampler(this, DisplayName, previewTransitionDuration, ResolveDefaultPreviewTarget(runtimeTarget), allowIdleOnlyFallback);
            var list = new List<IEditorTimeSampler>
            {
                trackSampler
            };

            if (clips == null)
                return list;

            for (int i = 0; i < clips.Count; i++)
            {
                SkillTrackClip_Animation clip = clips[i];
                if (clip == null || !clip.Enabled)
                    continue;

                AdvancedAnimationClipEditorSampler clipSampler = clip.CreateAdvancedAnimationClipEditorSampler(sequence, this, runtimeTarget);
                if (clipSampler == null)
                    continue;

                trackSampler.AddClipSampler(clipSampler);
                list.Add(new TrackClipEditorSampler(clip, clipSampler));
            }

            return list;
        }

        private bool ShouldCreateIdleOnlyFallback(ITrackSequence sequence, ESRuntimeTargetPack runtimeTarget)
        {
            if (!ReferenceEquals(this, FindFirstEnabledAnimationTrack(sequence)))
                return false;

            return !SequenceHasAnyValidAnimationClip(sequence, runtimeTarget);
        }

        private static SkillTrackItem_Animation FindFirstEnabledAnimationTrack(ITrackSequence sequence)
        {
            if (sequence == null || sequence.Tracks == null)
                return null;

            foreach (var track in sequence.Tracks)
            {
                if (track is SkillTrackItem_Animation animationTrack && animationTrack.Enabled)
                    return animationTrack;
            }

            return null;
        }

        private static bool SequenceHasAnyValidAnimationClip(ITrackSequence sequence, ESRuntimeTargetPack runtimeTarget)
        {
            if (sequence == null || sequence.Tracks == null)
                return false;

            foreach (var track in sequence.Tracks)
            {
                if (track is not SkillTrackItem_Animation animationTrack || !animationTrack.Enabled || animationTrack.clips == null)
                    continue;

                for (int i = 0; i < animationTrack.clips.Count; i++)
                {
                    SkillTrackClip_Animation clip = animationTrack.clips[i];
                    if (clip != null && clip.Enabled && clip.CanCreateAdvancedEditorAnimationSampler(runtimeTarget))
                        return true;
                }
            }

            return false;
        }

        private static GameObject ResolveDefaultPreviewTarget(ESRuntimeTargetPack runtimeTarget)
        {
            var animator = runtimeTarget != null && runtimeTarget.userEntity != null
                ? runtimeTarget.userEntity.animator
                : null;

            if (animator != null)
                return animator.gameObject;

            return runtimeTarget != null ? runtimeTarget.GetGameObject() : null;
        }
#endif
    }

    [System.Serializable, ESCreatePath("技能轨道剪辑", "动画轨道剪辑")]
    public class SkillTrackClip_Animation : SkillTrackClip
    {
        public SkillTrackClip_Animation()
        {
            name = "动画片段";
        }

        [TitleGroup("动画片段", "未指定目标表达式时，会尝试使用预览目标 Entity 上的 Animator。")]
        [LabelText("目标表达式")]
        [InfoBox("可选。指定后会覆盖默认动画采样目标。", InfoMessageType.None)]
        [SerializeReference]
        public ESGetGameObjectExpression targetExpression;

        [TitleGroup("动画片段")]
        [LabelText("动画剪辑")]
        [Required("动画片段需要指定 AnimationClip，否则不会进入采样器。")]
        [OnValueChanged(nameof(OnAnimationClipChanged))]
        public AnimationClip AnimationClipName;

        [TitleGroup("动画片段")]
        [LabelText("动画标记")]
        [Tooltip("用于运行时按标记替换动画。留空时使用动画资源名；资源名也为空时使用 Clip0/Clip1。")]
        public string clipMarker;

        [TitleGroup("动画采样", "控制该片段在动画资源内部从哪里开始采样，以及片段时长内如何推进。")]
        [HorizontalGroup("动画采样/采样参数", Width = 0.34f)]
        [LabelText("裁剪起点")]
        [MinValue(0f)]
        [SuffixLabel("秒", true)]
        public float clipStartOffset;

        [HorizontalGroup("动画采样/采样参数", Width = 0.33f)]
        [LabelText("播放速度")]
        [MinValue(0.01f)]
        public float playbackSpeed = 1f;

        [HorizontalGroup("动画采样/采样参数", Width = 0.33f)]
        [LabelText("循环采样")]
        public bool loopClip;

        [TitleGroup("动画采样")]
        [ShowInInspector]
        [ReadOnly]
        [LabelText("可用长度")]
        [SuffixLabel("秒", true)]
        private float AvailableClipLengthPreview => GetAvailableClipLength();

        [TitleGroup("动画采样")]
        [Button("同步持续时间到动画长度", ButtonSizes.Small)]
        [GUIColor(0.48f, 0.82f, 1f)]
        public void SyncDurationToAnimationLength()
        {
            float available = GetAvailableClipLength();
            durationTime = Mathf.Max(0.0001f, available / Mathf.Max(0.01f, playbackSpeed));
        }

        public float ResolveAnimationLocalTime(float sequenceTime)
        {
            if (AnimationClipName == null)
                return 0f;

            float local = clipStartOffset + Mathf.Max(0f, sequenceTime - startTime) * Mathf.Max(0.01f, playbackSpeed);
            float clipLength = Mathf.Max(0f, AnimationClipName.length);
            if (clipLength <= 0.0001f)
                return 0f;

            return loopClip ? Mathf.Repeat(local, clipLength) : Mathf.Clamp(local, 0f, clipLength);
        }

        private float GetAvailableClipLength()
        {
            if (AnimationClipName == null)
                return 0f;

            return Mathf.Max(0f, AnimationClipName.length - Mathf.Max(0f, clipStartOffset));
        }

        private void OnAnimationClipChanged()
        {
            if (string.IsNullOrWhiteSpace(name) || name == "动画片段")
                name = AnimationClipName != null ? AnimationClipName.name : "动画片段";

            if (AnimationClipName != null && durationTime <= 0.0001f)
                SyncDurationToAnimationLength();
        }

        public override IEditorTimeSampler CreateSampler(ITrackSequence sequence, ITrackItem track)
        {
#if UNITY_EDITOR
            return CreateEditorSampler(sequence, track, null);
#else
            return base.CreateSampler(sequence, track);
#endif
        }

#if UNITY_EDITOR
        public override IEditorTimeSampler CreateEditorSampler(ITrackSequence sequence, ITrackItem track, object editorTarget)
        {
            return CreateAnimationClipEditorSampler(sequence, track, editorTarget as ESRuntimeTargetPack);
        }

        public AnimationClipEditorSampler CreateAnimationClipEditorSampler(ITrackSequence sequence, ITrackItem track, ESRuntimeTargetPack runtimeTarget)
        {
            if (!CanCreateEditorAnimationSampler())
                return null;

            GameObject targetObject = targetExpression != null
                ? targetExpression.Evaluate(runtimeTarget, null)
                : ResolveDefaultAnimationTarget(runtimeTarget);

            if (targetObject == null)
                return null;

            return new AnimationClipEditorSampler(targetObject, AnimationClipName, startTime, durationTime, clipStartOffset, playbackSpeed, loopClip);
        }

        public AdvancedAnimationClipEditorSampler CreateAdvancedAnimationClipEditorSampler(ITrackSequence sequence, ITrackItem track, ESRuntimeTargetPack runtimeTarget)
        {
            if (!CanCreateEditorAnimationSampler())
                return null;

            GameObject targetObject = targetExpression != null
                ? targetExpression.Evaluate(runtimeTarget, null)
                : ResolveDefaultAnimationTarget(runtimeTarget);

            if (targetObject == null)
                return null;

            return new AdvancedAnimationClipEditorSampler(targetObject, AnimationClipName, startTime, durationTime, clipStartOffset, playbackSpeed, loopClip);
        }

        private bool CanCreateEditorAnimationSampler()
        {
            return AnimationClipName != null && durationTime > 0.0001f && AnimationClipName.length > 0.0001f;
        }

        public bool CanCreateAdvancedEditorAnimationSampler(ESRuntimeTargetPack runtimeTarget)
        {
            if (!CanCreateEditorAnimationSampler())
                return false;

            GameObject targetObject = targetExpression != null
                ? targetExpression.Evaluate(runtimeTarget, null)
                : ResolveDefaultAnimationTarget(runtimeTarget);

            return targetObject != null;
        }

        private static GameObject ResolveDefaultAnimationTarget(ESRuntimeTargetPack runtimeTarget)
        {
            var animator = runtimeTarget != null && runtimeTarget.userEntity != null
                ? runtimeTarget.userEntity.animator
                : null;

            if (animator != null)
                return animator.gameObject;

            return runtimeTarget != null ? runtimeTarget.GetGameObject() : null;
        }
#endif
    }
}
