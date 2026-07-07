using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [CreateTrackItem(TrackItemType.Skill, "Animation轨道")]
    public class SkillTrackItem_Animation : SkillTrackItem<SkillTrackClip_Animation>
    {
        public string AnimationIM = "这是一个动画轨道";
        public override Color ItemBGColor => Color.cyan._WithAlpha(0.35f);

#if UNITY_EDITOR
        public override List<IEditorTimeSampler> CreateEditorSamplers(ITrackSequence sequence, object editorTarget)
        {
            var runtimeTarget = editorTarget as ESRuntimeTarget;
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
                if (clip == null)
                    continue;

                AnimationClipEditorSampler clipSampler = clip.CreateAnimationClipEditorSampler(sequence, this, runtimeTarget);
                if (clipSampler == null)
                    continue;

                trackSampler.AddClipSampler(clipSampler);
                list.Add(new TrackClipEditorSampler(clip, clipSampler));
            }

            return list;
        }
#endif
    }

    [System.Serializable, ESCreatePath("技能轨道剪辑", "动画轨道剪辑")]
    public class SkillTrackClip_Animation : SkillTrackClip
    {
        [LabelText("目标表达式")]
        [SerializeReference]
        public ESGetGameObjectExpression targetExpression;

        [LabelText("动画剪辑")]
        public AnimationClip AnimationClipName;

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
            return CreateAnimationClipEditorSampler(sequence, track, editorTarget as ESRuntimeTarget);
        }

        public AnimationClipEditorSampler CreateAnimationClipEditorSampler(ITrackSequence sequence, ITrackItem track, ESRuntimeTarget runtimeTarget)
        {
            GameObject targetObject = targetExpression != null
                ? targetExpression.Evaluate(runtimeTarget, null)
                : ResolveDefaultAnimationTarget(runtimeTarget);

            return new AnimationClipEditorSampler(targetObject, AnimationClipName, startTime, durationTime);
        }

        private static GameObject ResolveDefaultAnimationTarget(ESRuntimeTarget runtimeTarget)
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
