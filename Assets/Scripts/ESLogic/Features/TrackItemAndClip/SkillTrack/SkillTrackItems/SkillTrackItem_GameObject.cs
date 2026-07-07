using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [CreateTrackItem(TrackItemType.Skill, "GameObject轨道")]
    public class SkillTrackItem_GameObject : SkillTrackItem<SkillTrackClip_GameObject>
    {
        [System.NonSerialized]
        private GameObjectTrackEditorSampler lastCreatedEditorSampler;

        public override Color ItemBGColor => Color.green._WithAlpha(0.35f);

#if UNITY_EDITOR
        public override System.Collections.Generic.List<IEditorTimeSampler> CreateEditorSamplers(ITrackSequence sequence, object editorTarget)
        {
            var runtimeTarget = editorTarget as ESRuntimeTarget;
            ESRuntimeTarget target = runtimeTarget;
            bool ownsEditorTarget = false;

            if (overrideTrackPreviewTarget)
            {
                target = ESRuntimeTarget.Pool.GetInPool();
                target.SetEntity(runtimeTarget != null ? runtimeTarget.userEntity : null);

                GameObject targetObject = trackTargetExpression != null
                    ? trackTargetExpression.Evaluate(runtimeTarget, null)
                    : null;

                if (targetObject != null)
                    target.SetEntity(FindEntityInSelfOrParents(targetObject));

                ownsEditorTarget = true;
            }

            TrackSequenceEditorSettings settings = TrackSequenceEditorSettings.Instance;
            bool debugSampler = settings != null && settings.gameObjectDebug;
            var trackSampler = new GameObjectTrackEditorSampler(this, target, ownsEditorTarget, DisplayName, debugSampler);
            lastCreatedEditorSampler = trackSampler;

            var list = new System.Collections.Generic.List<IEditorTimeSampler>
            {
                trackSampler
            };

            if (clips == null)
                return list;

            for (int i = 0; i < clips.Count; i++)
            {
                SkillTrackClip_GameObject clip = clips[i];
                if (clip == null)
                    continue;

                var clipSampler = clip.CreateEditorSampler(sequence, this, target);
                if (clipSampler != null)
                    list.Add(new TrackClipEditorSampler(clip, clipSampler));
            }

            return list;
        }

        public GameObjectTrackEditorSampler GetLastCreatedEditorSampler()
        {
            return lastCreatedEditorSampler;
        }

        private static Entity FindEntityInSelfOrParents(GameObject gameObject)
        {
            Transform current = gameObject != null ? gameObject.transform : null;
            while (current != null)
            {
                Entity entity = current.GetComponent<Entity>();
                if (entity != null)
                    return entity;

                current = current.parent;
            }

            return null;
        }
#endif
    }

    [System.Obsolete("Use TrackClipEditorTargetMode instead.")]
    public enum GameObjectTrackClipTargetMode
    {
        InheritTrackTarget,
        OverrideClipTarget
    }

    [System.Serializable, ESCreatePath("技能轨道剪辑", "游戏对象轨道剪辑")]
    public class SkillTrackClip_GameObject : SkillTrackClip
    {
        [LabelText("目标来源")]
        public TrackClipEditorTargetMode targetMode = TrackClipEditorTargetMode.InheritTrackTarget;

        [LabelText("片段覆盖目标")]
        [ShowIf(nameof(UseOverrideClipTarget))]
        [SerializeReference]
        public ESGetGameObjectExpression targetExpression;

        [LabelText("激活状态")]
        public bool Activate = true;

        private bool UseOverrideClipTarget => targetMode == TrackClipEditorTargetMode.OverrideClipTarget;

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
            var runtimeTarget = editorTarget as ESRuntimeTarget;
            GameObjectTrackEditorSampler trackSampler = track is SkillTrackItem_GameObject gameObjectTrack
                ? gameObjectTrack.GetLastCreatedEditorSampler()
                : null;
            GameObject targetObject = ResolveEditorTarget(runtimeTarget, trackSampler, out string targetSource);
            TrackSequenceEditorSettings settings = TrackSequenceEditorSettings.Instance;
            bool debugSampler = settings != null && settings.gameObjectDebug;
            string debugName = $"{track?.DisplayName ?? "<Track>"}/{DisplayName}";
            return new GameObjectEditorSampler(trackSampler, targetObject, Activate, startTime, durationTime, debugName, targetSource, debugSampler);
        }

        public GameObject ResolveEditorTarget(ESRuntimeTarget runtimeTarget, GameObjectTrackEditorSampler trackSampler = null)
        {
            return ResolveEditorTarget(runtimeTarget, trackSampler, out _);
        }

        private GameObject ResolveEditorTarget(ESRuntimeTarget runtimeTarget, GameObjectTrackEditorSampler trackSampler, out string targetSource)
        {
            if (targetMode == TrackClipEditorTargetMode.OverrideClipTarget)
            {
                if (targetExpression == null)
                {
                    targetSource = "OverrideClipTarget targetExpression=null";
                    return null;
                }

                GameObject targetObject = targetExpression.Evaluate(runtimeTarget, null);
                targetSource = targetObject != null
                    ? $"OverrideClipTarget expression={targetExpression.GetType().Name}"
                    : $"OverrideClipTarget expression={targetExpression.GetType().Name} result=null";
                return targetObject;
            }

            if (trackSampler != null)
            {
                GameObject targetObject = trackSampler.GetInheritedTarget();
                targetSource = targetObject != null
                    ? "InheritTrackTarget from TrackEditorSampler"
                    : trackSampler.GetInheritedTargetDebugInfo();
                return targetObject;
            }

            GameObject fallback = runtimeTarget != null ? runtimeTarget.GetGameObject() : null;
            targetSource = fallback != null
                ? "InheritTrackTarget fallback runtimeTarget"
                : "InheritTrackTarget fallback runtimeTarget=null";
            return fallback;
        }
#endif
    }
}
