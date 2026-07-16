using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [CreateTrackItem(TrackItemType.Skill, "操作轨道")]
    public class SkillTrackItem_Operation : SkillTrackItem<SkillTrackClip_Operation>, ISkillRuntimeTrackCompiler
    {
        public override Color ItemBGColor => new Color(0.72f, 0.18f, 0.48f, 0.42f);

        public SkillTrackItem_Operation()
        {
            displayName = "操作轨道";
        }

        [TitleGroup("轨道目标包", "运行时为本操作轨道准备 RuntimeTarget。")]
        [LabelText("轨道目标包来源")]
        [EnumToggleButtons]
        public TrackRuntimeTargetSourceMode trackTargetSourceMode = TrackRuntimeTargetSourceMode.CopySkill;

        [TitleGroup("轨道目标包")]
        [ShowIf(nameof(UsesRuntimeTargetExpression))]
        [LabelText("轨道主目标表达式")]
        [InfoBox("仅在目标包来源需要表达式覆盖主目标时使用。")]
        [SerializeReference]
        public ESGetGameObjectExpression trackRuntimeTargetExpression;

        [TitleGroup("轨道目标包")]
        [LabelText("表达式结果加入目标列表")]
        public bool addExpressionResultToTargets = true;

        public ISkillRuntimeTrackPlayer CreateRuntimeTrackPlayer(SkillRuntimeBuildContext context)
        {
            return new SkillOperationTrackRuntimePlayer(this, context.TrackIndex);
        }

        private bool UsesRuntimeTargetExpression()
        {
            return trackTargetSourceMode == TrackRuntimeTargetSourceMode.CopySkillAndOverrideMainTarget ||
                   trackTargetSourceMode == TrackRuntimeTargetSourceMode.NewAndSetMainTargetByExpression;
        }
    }

    [System.Serializable, ESCreatePath("技能轨道剪辑", "操作轨道剪辑")]
    public class SkillTrackClip_Operation : SkillTrackClip, ISkillRuntimeClipCompiler
    {
        public SkillTrackClip_Operation()
        {
            name = "操作片段";
        }

        [TitleGroup("操作内容", "片段命中时执行的具体 Operation。")]
        [LabelText("操作描述")]
        [TextArea(2, 4)]
        public string OperationDescription;

        [TitleGroup("操作内容")]
        [SerializeReference]
        [HideLabel]
        [BoxGroup("操作内容/操作实例")]
        public ESOutputOp op;

        [TitleGroup("片段目标包", "Clip 可以继承、拷贝或创建自己的 RuntimeTarget，并按需写回。")]
        [LabelText("片段目标包来源")]
        [EnumToggleButtons]
        public ClipRuntimeTargetSourceMode clipTargetSourceMode = ClipRuntimeTargetSourceMode.ReferenceTrack;

        [TitleGroup("片段目标包")]
        [ShowIf(nameof(UsesClipTargetExpression))]
        [LabelText("片段主目标表达式")]
        [InfoBox("仅在片段目标包来源需要表达式覆盖主目标时使用。")]
        [SerializeReference]
        public ESGetGameObjectExpression clipTargetExpression;

        [TitleGroup("片段目标包")]
        [LabelText("表达式结果加入目标列表")]
        public bool addExpressionResultToTargets = true;

        [TitleGroup("片段目标包")]
        [LabelText("写回目标")]
        public RuntimeTargetWriteBackTarget writeBackTarget = RuntimeTargetWriteBackTarget.None;

        [TitleGroup("片段目标包")]
        [ShowIf(nameof(UsesWriteBack))]
        [LabelText("写回时机")]
        [InfoBox("写回会把本片段运行中的 RuntimeTarget 内容同步回技能级或轨道级目标包。")]
        public RuntimeTargetWriteBackTiming writeBackTiming = RuntimeTargetWriteBackTiming.OnExit;

        [TitleGroup("执行参数", "高频运行字段优先使用固定值，只有需要动态计算时才切换表达式。")]
        [LabelText("启用条件")]
        public bool conditionValue = true;

        [TitleGroup("执行参数")]
        [LabelText("运行Float")]
        public FloatExpressionSource runtimeFloat = new FloatExpressionSource { directFloat = 1f };

        [HideInInspector]
        public bool useStaticFloat = true;

        [HideInInspector]
        public float staticFloat = 1f;

        [HideInInspector]
        [SerializeReference]
        public ESGetFloatExpression dynamicFloatExpression;

        public ISkillRuntimeClipPlayer CreateRuntimeClipPlayer(SkillRuntimeBuildContext context)
        {
            return new SkillOperationClipRuntimePlayer(this, context.TrackIndex);
        }

        private bool UsesClipTargetExpression()
        {
            return clipTargetSourceMode == ClipRuntimeTargetSourceMode.CopyTrackAndOverrideMainTarget ||
                   clipTargetSourceMode == ClipRuntimeTargetSourceMode.CopySkillAndOverrideMainTarget ||
                   clipTargetSourceMode == ClipRuntimeTargetSourceMode.NewAndSetMainTargetByExpression;
        }

        private bool UsesWriteBack()
        {
            return writeBackTarget != RuntimeTargetWriteBackTarget.None;
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
            return new SkillTrackOperationEditorSampler(this, editorTarget as ESRuntimeTargetPack);
        }
#endif
    }

#if UNITY_EDITOR
    public class SkillTrackOperationEditorSampler : EditorTimeSamplerBase
    {
        private readonly SkillTrackClip_Operation clip;
        private readonly ESRuntimeTargetPack target;
        private bool isInside;

        public SkillTrackOperationEditorSampler(SkillTrackClip_Operation clip, ESRuntimeTargetPack target)
        {
            this.clip = clip;
            this.target = target;
        }

        public override void SampleTime(float time)
        {
            if (clip == null || clip.op == null || target == null || target.IsRecycled)
                return;

            bool inside = time >= clip.StartTime && time < clip.StartTime + clip.DurationTime;
            if (inside == isInside)
                return;

            isInside = inside;
            if (isInside)
                clip.op._TryStartOp(target, null);
            else
                clip.op._TryStopOp(target, null);
        }

        public override void OnEditorPreviewStop()
        {
            if (clip != null && clip.op != null && isInside)
                clip.op._TryStopOp(target, null);

            isInside = false;
        }
    }
#endif

    public sealed class SkillOperationTrackRuntimePlayer : ISkillRuntimeTrackPlayer
    {
        private readonly SkillTrackItem_Operation track;
        private readonly int trackIndex;

        public SkillOperationTrackRuntimePlayer(SkillTrackItem_Operation track, int trackIndex)
        {
            this.track = track;
            this.trackIndex = trackIndex;
        }

        public void OnSkillEnter(EntityState_Skill state, ref SkillRuntimeTrackState trackState)
        {
            ESRuntimeTargetPack target = SkillOperationRuntimeUtility.BuildTrackTarget(
                track != null ? track.trackTargetSourceMode : TrackRuntimeTargetSourceMode.CopySkill,
                state != null ? state.SkillRuntimeTarget : null,
                track != null ? track.trackRuntimeTargetExpression : null,
                state != null ? state.OpSupporter : null,
                track != null && track.addExpressionResultToTargets,
                out bool ownsTarget);

            SkillOperationTrackRuntimeState runtimeState = SkillOperationTrackRuntimeState.Pool.GetInPool();
            runtimeState.target = target;
            runtimeState.ownsTarget = ownsTarget;
            trackState.UserData = runtimeState;
            trackState.IsRunning = true;
            trackState.CurrentClipIndex = -1;

            if (state != null)
                state.SetTrackRuntimeTarget(trackIndex, target);
        }

        public void Tick(EntityState_Skill state, ref SkillRuntimeTrackState trackState, float time, float deltaTime)
        {
        }

        public void OnSkillExit(EntityState_Skill state, ref SkillRuntimeTrackState trackState)
        {
            if (trackState.UserData is SkillOperationTrackRuntimeState runtimeState)
            {
                if (runtimeState.ownsTarget && runtimeState.target != null && !runtimeState.target.IsRecycled)
                    runtimeState.target.ForcePushToPool();

                runtimeState.TryAutoPushedToPool();
            }
            else if (trackState.UserData is ESRuntimeTargetPack target && target != null && !target.IsRecycled)
            {
                target.ForcePushToPool();
            }

            trackState.Reset();

            if (state != null)
                state.SetTrackRuntimeTarget(trackIndex, null);
        }
    }

    public sealed class SkillOperationClipRuntimePlayer : ISkillRuntimeClipPlayer
    {
        private readonly SkillTrackClip_Operation clip;
        private readonly int trackIndex;

        public SkillOperationClipRuntimePlayer(SkillTrackClip_Operation clip, int trackIndex)
        {
            this.clip = clip;
            this.trackIndex = trackIndex;
        }

        public void OnClipEnter(EntityState_Skill state, ref SkillRuntimeClipState clipState)
        {
            if (clip == null || clip.op == null || !clip.conditionValue)
                return;

            ESRuntimeTargetPack target = SkillOperationRuntimeUtility.BuildClipTarget(
                clip.clipTargetSourceMode,
                state != null ? state.SkillRuntimeTarget : null,
                state != null ? state.GetTrackRuntimeTarget(trackIndex) : null,
                clip.clipTargetExpression,
                state != null ? state.OpSupporter : null,
                clip.addExpressionResultToTargets,
                out bool ownsTarget);

            SkillOperationClipRuntimeState runtimeState = SkillOperationClipRuntimeState.Pool.GetInPool();
            runtimeState.target = target;
            runtimeState.ownsTarget = ownsTarget;
            clipState.UserData = runtimeState;
            ApplyRuntimeValues(target, state);
            WriteBackIfNeeded(state, target, RuntimeTargetWriteBackTiming.OnEnter);
            clip.op._TryStartOp(target, state != null ? state.OpSupporter : null);
        }

        public void Tick(EntityState_Skill state, ref SkillRuntimeClipState clipState, float time, float deltaTime)
        {
        }

        public void OnClipExit(EntityState_Skill state, ref SkillRuntimeClipState clipState)
        {
            SkillOperationClipRuntimeState runtimeState = clipState.UserData as SkillOperationClipRuntimeState;
            ESRuntimeTargetPack target = runtimeState != null ? runtimeState.target : clipState.UserData as ESRuntimeTargetPack;
            if (clip != null && clip.op != null)
                clip.op._TryStopOp(target, state != null ? state.OpSupporter : null);

            WriteBackIfNeeded(state, target, RuntimeTargetWriteBackTiming.OnExit);

            if (runtimeState != null && runtimeState.ownsTarget && target != null && !target.IsRecycled)
                target.ForcePushToPool();

            runtimeState?.TryAutoPushedToPool();
            clipState.UserData = null;
        }

        private void WriteBackIfNeeded(EntityState_Skill state, ESRuntimeTargetPack source, RuntimeTargetWriteBackTiming timing)
        {
            if (clip == null || source == null || state == null)
                return;

            if (clip.writeBackTarget == RuntimeTargetWriteBackTarget.None)
                return;

            bool shouldWrite = clip.writeBackTiming == timing ||
                               clip.writeBackTiming == RuntimeTargetWriteBackTiming.OnEnterAndExit;
            if (!shouldWrite)
                return;

            if (clip.writeBackTarget == RuntimeTargetWriteBackTarget.Skill ||
                clip.writeBackTarget == RuntimeTargetWriteBackTarget.SkillAndTrack)
            {
                ESRuntimeTargetPack skillTarget = state.SkillRuntimeTarget;
                if (skillTarget != null && skillTarget != source)
                    source.CopyTo(skillTarget);
            }

            if (clip.writeBackTarget == RuntimeTargetWriteBackTarget.Track ||
                clip.writeBackTarget == RuntimeTargetWriteBackTarget.SkillAndTrack)
            {
                ESRuntimeTargetPack trackTarget = state.GetTrackRuntimeTarget(trackIndex);
                if (trackTarget != null && trackTarget != source)
                    source.CopyTo(trackTarget);
            }
        }

        private void ApplyRuntimeValues(ESRuntimeTargetPack target, EntityState_Skill state)
        {
            if (target == null || clip == null)
                return;

            target.runtimeBool = clip.conditionValue;
            target.runtimeFloat = clip.runtimeFloat != null
                ? clip.runtimeFloat.Evaluate(target, state != null ? state.OpSupporter : null)
                : EvaluateLegacyRuntimeFloat(target, state);
        }

        private float EvaluateLegacyRuntimeFloat(ESRuntimeTargetPack target, EntityState_Skill state)
        {
            return clip.useStaticFloat
                ? clip.staticFloat
                : (clip.dynamicFloatExpression != null
                    ? clip.dynamicFloatExpression.Evaluate(target, state != null ? state.OpSupporter : null)
                    : clip.staticFloat);
        }
    }

    internal sealed class SkillOperationTrackRuntimeState : IPoolableAuto
    {
        public static readonly ESSimplePool<SkillOperationTrackRuntimeState> Pool = new ESSimplePool<SkillOperationTrackRuntimeState>(
            factoryMethod: () => new SkillOperationTrackRuntimeState(),
            initCount: 8,
            maxCount: 256,
            poolDisplayName: "SkillOperationTrackRuntimeState Pool"
        );

        public bool IsRecycled { get; set; }
        public ESRuntimeTargetPack target;
        public bool ownsTarget;

        public void OnResetAsPoolable()
        {
            target = null;
            ownsTarget = false;
        }

        public void TryAutoPushedToPool()
        {
            if (!IsRecycled)
                Pool.PushToPool(this);
        }
    }

    internal sealed class SkillOperationClipRuntimeState : IPoolableAuto
    {
        public static readonly ESSimplePool<SkillOperationClipRuntimeState> Pool = new ESSimplePool<SkillOperationClipRuntimeState>(
            factoryMethod: () => new SkillOperationClipRuntimeState(),
            initCount: 16,
            maxCount: 512,
            poolDisplayName: "SkillOperationClipRuntimeState Pool"
        );

        public bool IsRecycled { get; set; }
        public ESRuntimeTargetPack target;
        public bool ownsTarget;

        public void OnResetAsPoolable()
        {
            target = null;
            ownsTarget = false;
        }

        public void TryAutoPushedToPool()
        {
            if (!IsRecycled)
                Pool.PushToPool(this);
        }
    }

    public static class SkillOperationRuntimeUtility
    {
        public static ESRuntimeTargetPack BuildTrackTarget(
            TrackRuntimeTargetSourceMode mode,
            ESRuntimeTargetPack skillTarget,
            ESGetGameObjectExpression expression,
            IOperationRuntimeServices support,
            bool addExpressionResultToTargets,
            out bool ownsTarget)
        {
            ownsTarget = false;

            switch (mode)
            {
                case TrackRuntimeTargetSourceMode.ReferenceSkill:
                    return skillTarget;

                case TrackRuntimeTargetSourceMode.NewEmpty:
                    ownsTarget = true;
                    return ESRuntimeTargetPack.Pool.GetInPool();

                case TrackRuntimeTargetSourceMode.CopySkillAndOverrideMainTarget:
                {
                    ESRuntimeTargetPack target = CopyFrom(skillTarget);
                    ownsTarget = true;
                    ApplyExpressionTarget(target, expression, support, addExpressionResultToTargets);
                    return target;
                }

                case TrackRuntimeTargetSourceMode.NewAndSetMainTargetByExpression:
                {
                    ESRuntimeTargetPack target = ESRuntimeTargetPack.Pool.GetInPool();
                    ownsTarget = true;
                    ApplyExpressionTarget(target, expression, support, addExpressionResultToTargets);
                    return target;
                }

                case TrackRuntimeTargetSourceMode.CopySkill:
                default:
                    ownsTarget = true;
                    return CopyFrom(skillTarget);
            }
        }

        public static ESRuntimeTargetPack BuildClipTarget(
            ClipRuntimeTargetSourceMode mode,
            ESRuntimeTargetPack skillTarget,
            ESRuntimeTargetPack trackTarget,
            ESGetGameObjectExpression expression,
            IOperationRuntimeServices support,
            bool addExpressionResultToTargets,
            out bool ownsTarget)
        {
            ownsTarget = false;

            switch (mode)
            {
                case ClipRuntimeTargetSourceMode.ReferenceSkill:
                    return skillTarget;

                case ClipRuntimeTargetSourceMode.ReferenceTrack:
                    return trackTarget != null ? trackTarget : skillTarget;

                case ClipRuntimeTargetSourceMode.CopySkill:
                    ownsTarget = true;
                    return CopyFrom(skillTarget);

                case ClipRuntimeTargetSourceMode.NewEmpty:
                    ownsTarget = true;
                    return ESRuntimeTargetPack.Pool.GetInPool();

                case ClipRuntimeTargetSourceMode.CopyTrackAndOverrideMainTarget:
                {
                    ESRuntimeTargetPack target = CopyFrom(trackTarget != null ? trackTarget : skillTarget);
                    ownsTarget = true;
                    ApplyExpressionTarget(target, expression, support, addExpressionResultToTargets);
                    return target;
                }

                case ClipRuntimeTargetSourceMode.CopySkillAndOverrideMainTarget:
                {
                    ESRuntimeTargetPack target = CopyFrom(skillTarget);
                    ownsTarget = true;
                    ApplyExpressionTarget(target, expression, support, addExpressionResultToTargets);
                    return target;
                }

                case ClipRuntimeTargetSourceMode.NewAndSetMainTargetByExpression:
                {
                    ESRuntimeTargetPack target = ESRuntimeTargetPack.Pool.GetInPool();
                    ownsTarget = true;
                    ApplyExpressionTarget(target, expression, support, addExpressionResultToTargets);
                    return target;
                }

                case ClipRuntimeTargetSourceMode.CopyTrack:
                default:
                    ownsTarget = true;
                    return CopyFrom(trackTarget != null ? trackTarget : skillTarget);
            }
        }

        private static ESRuntimeTargetPack CopyFrom(ESRuntimeTargetPack source)
        {
            ESRuntimeTargetPack target = ESRuntimeTargetPack.Pool.GetInPool();
            target.CopyFrom(source);
            return target;
        }

        private static void ApplyExpressionTarget(ESRuntimeTargetPack target, ESGetGameObjectExpression expression, IOperationRuntimeServices support, bool addToTargets)
        {
            if (target == null || expression == null)
                return;

            GameObject gameObject = expression.Evaluate(target, support);
            Entity entity = FindEntityInSelfOrParents(gameObject);
            target.SetEntityMainTarget(entity);

            if (addToTargets && entity != null)
                target.AddTarget(entity);
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
    }
}
