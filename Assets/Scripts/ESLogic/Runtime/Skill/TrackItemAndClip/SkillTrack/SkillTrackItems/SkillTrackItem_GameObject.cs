using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [CreateTrackItem(TrackItemType.Skill, "对象轨道")]
    public class SkillTrackItem_GameObject : SkillTrackItem<SkillTrackClip_GameObject>
    {
        [System.NonSerialized]
        private GameObjectTrackEditorSampler lastCreatedEditorSampler;

        public override Color ItemBGColor => new Color(0.22f, 0.58f, 0.34f, 0.42f);

        public SkillTrackItem_GameObject()
        {
            displayName = "对象轨道";
        }

#if UNITY_EDITOR
        public override System.Collections.Generic.List<IEditorTimeSampler> CreateEditorSamplers(ITrackSequence sequence, object editorTarget)
        {
            var runtimeTarget = editorTarget as ESRuntimeTargetPack;
            ESRuntimeTargetPack target = runtimeTarget;
            bool ownsEditorTarget = false;

            if (overrideTrackPreviewTarget)
            {
                target = ESRuntimeTargetPack.Pool.GetInPool();
                target.SetEntity(runtimeTarget != null ? runtimeTarget.userEntity : null);
                target.SetUser(runtimeTarget != null ? runtimeTarget.userEntity : null);

                GameObject targetObject = trackTargetExpression != null
                    ? trackTargetExpression.Evaluate(runtimeTarget, null)
                    : null;

                if (targetObject != null)
                    target.SetEntityMainTarget(FindEntityInSelfOrParents(targetObject));

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
                if (clip == null || !clip.Enabled)
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
    public class SkillTrackClip_GameObject : SkillTrackClip, ISkillRuntimeClipCompiler
    {
        public SkillTrackClip_GameObject()
        {
            name = "对象片段";
        }

        [TitleGroup("对象片段", "在片段时间内控制目标 GameObject 的激活状态。")]
        [LabelText("目标来源")]
        [EnumToggleButtons]
        public TrackClipEditorTargetMode targetMode = TrackClipEditorTargetMode.InheritTrackTarget;

        [TitleGroup("对象片段")]
        [LabelText("片段覆盖目标")]
        [ShowIf(nameof(UseOverrideClipTarget))]
        [InfoBox("只有目标来源为 OverrideClipTarget 时生效。", InfoMessageType.None)]
        [SerializeReference]
        public ESGetGameObjectExpression targetExpression;

        [TitleGroup("对象片段")]
        [LabelText("激活状态")]
        public bool Activate = true;

        private bool UseOverrideClipTarget => targetMode == TrackClipEditorTargetMode.OverrideClipTarget;

        public ISkillRuntimeClipPlayer CreateRuntimeClipPlayer(SkillRuntimeBuildContext context)
        {
            return new GameObjectClipRuntimePlayer(this);
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
            var runtimeTarget = editorTarget as ESRuntimeTargetPack;
            GameObjectTrackEditorSampler trackSampler = track is SkillTrackItem_GameObject gameObjectTrack
                ? gameObjectTrack.GetLastCreatedEditorSampler()
                : null;
            GameObject targetObject = ResolveEditorTarget(runtimeTarget, trackSampler, out string targetSource);
            TrackSequenceEditorSettings settings = TrackSequenceEditorSettings.Instance;
            bool debugSampler = settings != null && settings.gameObjectDebug;
            string debugName = $"{track?.DisplayName ?? "<Track>"}/{DisplayName}";
            return new GameObjectEditorSampler(trackSampler, targetObject, Activate, startTime, durationTime, debugName, targetSource, debugSampler);
        }

        public GameObject ResolveEditorTarget(ESRuntimeTargetPack runtimeTarget, GameObjectTrackEditorSampler trackSampler = null)
        {
            return ResolveEditorTarget(runtimeTarget, trackSampler, out _);
        }

        private GameObject ResolveEditorTarget(ESRuntimeTargetPack runtimeTarget, GameObjectTrackEditorSampler trackSampler, out string targetSource)
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

    public sealed class GameObjectClipRuntimePlayer : ISkillRuntimeClipPlayer
    {
        private readonly SkillTrackClip_GameObject clip;

        public GameObjectClipRuntimePlayer(SkillTrackClip_GameObject clip)
        {
            this.clip = clip;
        }

        public void OnClipEnter(EntityState_Skill state, ref SkillRuntimeClipState clipState)
        {
            GameObject target = ResolveTarget(state);
            if (target == null)
                return;

            GameObjectClipRuntimeState runtimeState = GameObjectClipRuntimeState.Pool.GetInPool();
            runtimeState.activeTarget = target;
            runtimeState.originalActive = target.activeSelf;
            runtimeState.hasOriginalActive = true;

            target.SetActive(clip == null || clip.Activate);
            clipState.UserData = runtimeState;
        }

        public void Tick(EntityState_Skill state, ref SkillRuntimeClipState clipState, float time, float deltaTime)
        {
        }

        public void OnClipExit(EntityState_Skill state, ref SkillRuntimeClipState clipState)
        {
            if (clipState.UserData is GameObjectClipRuntimeState runtimeState)
            {
                if (runtimeState.activeTarget != null && runtimeState.hasOriginalActive)
                    runtimeState.activeTarget.SetActive(runtimeState.originalActive);

                runtimeState.TryAutoPushedToPool();
            }

            clipState.UserData = null;
        }

        private GameObject ResolveTarget(EntityState_Skill state)
        {
            if (clip == null || state == null)
                return null;

            if (clip.targetMode == TrackClipEditorTargetMode.OverrideClipTarget && clip.targetExpression != null)
                return clip.targetExpression.Evaluate(state.SkillRuntimeTarget, state.OpSupporter);

            return state.SkillRuntimeTarget != null ? state.SkillRuntimeTarget.GetGameObject() : null;
        }
    }

    internal sealed class GameObjectClipRuntimeState : IPoolableAuto
    {
        public static readonly ESSimplePool<GameObjectClipRuntimeState> Pool = new ESSimplePool<GameObjectClipRuntimeState>(
            factoryMethod: () => new GameObjectClipRuntimeState(),
            initCount: 16,
            maxCount: 512,
            poolDisplayName: "GameObjectClipRuntimeState Pool"
        );

        public bool IsRecycled { get; set; }
        public GameObject activeTarget;
        public bool originalActive;
        public bool hasOriginalActive;

        public void OnResetAsPoolable()
        {
            activeTarget = null;
            originalActive = false;
            hasOriginalActive = false;
        }

        public void TryAutoPushedToPool()
        {
            if (!IsRecycled)
                Pool.PushToPool(this);
        }
    }
}
