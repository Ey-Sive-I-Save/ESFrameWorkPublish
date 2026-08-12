using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace ES
{
    /// <summary>
    /// 技能相机轨道只提交 CameraRequest。它不持有 VCam、Rig 或 Cinemachine 类型，
    /// 片段中断、异常补偿和状态回收都通过 CameraClipRuntimeState 释放 Lease。
    /// </summary>
    [CreateTrackItem(TrackItemType.Skill, "相机轨道")]
    public sealed class SkillTrackItem_Camera : SkillTrackItem<SkillTrackClip_Camera>
    {
        public override Color ItemBGColor => new Color(0.18f, 0.62f, 0.72f, 0.42f);

        public SkillTrackItem_Camera()
        {
            displayName = "相机轨道";
        }

#if UNITY_EDITOR
        /// <summary>
        /// Runtime 轨道只向纯契约提交预览描述。真正的 Preview Session、Sampler、
        /// Camera/Brain 和 UnityEditor 生命周期全部位于 Editor/Camera；没有 Factory
        /// 时退回普通轨道预览，不在 Runtime 侧偷偷创建任何相机对象。
        /// </summary>
        public override List<IEditorTimeSampler> CreateEditorSamplers(ITrackSequence sequence, object editorTarget)
        {
            ICameraTrackPreviewFactory factory = ESCameraTrackPreviewFactoryRegistry.Factory;
            if (factory == null)
                return base.CreateEditorSamplers(sequence, editorTarget);

            ESRuntimeTargetPack resolvedTarget = editorTarget as ESRuntimeTargetPack;
            bool ownsResolvedTarget = false;
            if (overrideTrackPreviewTarget)
            {
                resolvedTarget = ESRuntimeTargetPack.Pool.GetInPool();
                ownsResolvedTarget = true;

                ESRuntimeTargetPack inheritedTarget = editorTarget as ESRuntimeTargetPack;
                resolvedTarget.SetEntity(inheritedTarget != null ? inheritedTarget.userEntity : null);
                resolvedTarget.SetUser(inheritedTarget != null ? inheritedTarget.userEntity : null);

                GameObject targetObject = trackTargetExpression != null
                    ? trackTargetExpression.Evaluate(inheritedTarget, null)
                    : null;
                if (targetObject != null)
                    resolvedTarget.SetEntityMainTarget(targetObject.GetComponentInParent<Entity>());
            }

            var previewClips = new List<ESCameraTrackPreviewClip>(clips != null ? clips.Count : 0);
            if (clips != null)
            {
                for (int i = 0; i < clips.Count; i++)
                {
                    SkillTrackClip_Camera clip = clips[i];
                    if (clip == null || !clip.Enabled)
                        continue;

                    previewClips.Add(new ESCameraTrackPreviewClip(
                        clip,
                        clip.definition,
                        clip.viewKey,
                        clip.priority,
                        clip.targetSource == SkillCameraTargetSource.MainTarget
                            ? ESCameraTrackPreviewTargetSource.MainTarget
                            : ESCameraTrackPreviewTargetSource.SkillUser));
                }
            }

            try
            {
                List<IEditorTimeSampler> samplers = factory.CreateSamplers(new ESCameraTrackPreviewRequest(
                    sequence,
                    this,
                    resolvedTarget,
                    ownsResolvedTarget,
                    previewClips));
                if (samplers != null)
                    return samplers;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            if (ownsResolvedTarget && resolvedTarget != null && !resolvedTarget.IsRecycled)
                resolvedTarget.ForcePushToPool();

            return base.CreateEditorSamplers(sequence, editorTarget);
        }
#endif
    }

    [System.Serializable, ESCreatePath("技能轨道剪辑", "相机轨道剪辑")]
    public sealed class SkillTrackClip_Camera : SkillTrackClip, ISkillRuntimeClipCompiler
    {
        [PropertyOrder(ESTrackInspectorFieldStandard.ContentOrder)]
        [TitleGroup(ESTrackInspectorFieldStandard.Content, "进入片段申请相机 Shot；离开、打断或补偿清理时释放对应 Lease。")]
        [LabelText("相机定义")]
        [Tooltip("稳定相机内容引用；不得填入 VCam、Rig Prefab 或场景对象。")]
        public ESCameraDefinitionReference definition;

        [SerializeField, HideInInspector, FormerlySerializedAs("profileKey"), FormerlySerializedAs("definitionKey")]
        private string legacyDefinitionKey;

        [PropertyOrder(ESTrackInspectorFieldStandard.ContentOrder + 1)]
        [TitleGroup(ESTrackInspectorFieldStandard.Content)]
        [LabelText("视图键")]
        [Tooltip("相机模块中的逻辑视图名称，不是场景对象名称。")]
        public string viewKey = "MainView";

        [PropertyOrder(ESTrackInspectorFieldStandard.BehaviorOrder)]
        [TitleGroup(ESTrackInspectorFieldStandard.Behavior, "优先级用于同一视图内的请求仲裁。")]
        [LabelText("优先级")]
        public int priority = 100;

        [PropertyOrder(ESTrackInspectorFieldStandard.TargetOrder)]
        [TitleGroup(ESTrackInspectorFieldStandard.Target, "选择相机请求跟随技能使用者还是当前主目标。")]
        [LabelText("目标来源")]
        public SkillCameraTargetSource targetSource = SkillCameraTargetSource.SkillUser;

        public SkillTrackClip_Camera()
        {
            name = "相机片段";
        }

        public ISkillRuntimeClipPlayer CreateRuntimeClipPlayer(SkillRuntimeBuildContext context)
        {
            return new SkillCameraClipRuntimePlayer(this);
        }
    }

    public enum SkillCameraTargetSource
    {
        [InspectorName("技能使用者")]
        SkillUser = 0,

        [InspectorName("技能主目标")]
        MainTarget = 1,
    }

    public sealed class SkillCameraClipRuntimePlayer : ISkillRuntimeClipPlayer
    {
        private readonly SkillTrackClip_Camera clip;

        public SkillCameraClipRuntimePlayer(SkillTrackClip_Camera clip)
        {
            this.clip = clip;
        }

        public void OnClipEnter(EntityState_Skill state, ref SkillRuntimeClipState clipState)
        {
            if (clip == null || !clip.definition.IsConfigured)
                return;

            ESCameraModule camera = ESGameManager.Camera;
            Entity target = ResolveTarget(state);
            Entity cameraOwner = state != null && state.SkillRuntimeTarget != null
                ? state.SkillRuntimeTarget.GetUserEntity()
                : null;
            if (camera == null || target == null || cameraOwner == null)
                return;

            EntityTransformMapping mapping = target.TransformMapping;
            Transform follow = mapping != null ? mapping.Resolve("CameraTarget") : null;
            if (follow == null && mapping != null)
                follow = mapping.Resolve(DefaultTransformKey.Camera);
            if (follow == null)
                return;

            CameraClipRuntimeState runtimeState = CameraClipRuntimeState.Pool.GetInPool();
            try
            {
                ESCameraRequest request = ESCameraRequest.CreateShot(
                    new ESCameraViewId(clip.viewKey),
                    clip.definition,
                    clip.priority,
                    cameraOwner,
                    follow,
                    mapping != null ? mapping.Resolve("CameraAimTarget") : null);

                runtimeState.lease = camera.Push(request);
                if (!runtimeState.lease.IsValid)
                {
                    runtimeState.TryAutoPushedToPool();
                    return;
                }

                clipState.UserData = runtimeState;
            }
            catch
            {
                runtimeState.TryAutoPushedToPool();
                throw;
            }
        }

        public void Tick(EntityState_Skill state, ref SkillRuntimeClipState clipState, float time, float deltaTime)
        {
        }

        public void OnClipExit(EntityState_Skill state, ref SkillRuntimeClipState clipState)
        {
            if (clipState.UserData is CameraClipRuntimeState runtimeState)
            {
                clipState.UserData = null;
                runtimeState.TryAutoPushedToPool();
                return;
            }

            clipState.UserData = null;
        }

        private Entity ResolveTarget(EntityState_Skill state)
        {
            ESRuntimeTargetPack target = state != null ? state.SkillRuntimeTarget : null;
            if (target == null)
                return null;

            return clip.targetSource == SkillCameraTargetSource.MainTarget
                ? target.GetEntityMainTarget()
                : target.GetUserEntity();
        }
    }

    /// <summary>
    /// 放入 UserData 的始终是这个池化引用对象，而不是装箱的 CameraLease 结构体。
    /// OnResetAsPoolable 负责兜底释放，因此 Clip Enter 失败、Exit 异常、技能打断和
    /// 状态机的统一补偿都无法遗漏 Lease。
    /// </summary>
    internal sealed class CameraClipRuntimeState : ISkillRuntimeOwnedUserData
    {
        public static readonly ESSimplePool<CameraClipRuntimeState> Pool = new ESSimplePool<CameraClipRuntimeState>(
            factoryMethod: () => new CameraClipRuntimeState(),
            initCount: 16,
            maxCount: 512,
            poolDisplayName: "CameraClipRuntimeState Pool");

        public bool IsRecycled { get; set; }
        public ESCameraLease lease;

        public void OnResetAsPoolable()
        {
            if (lease.IsValid)
                ESGameManager.Camera?.Release(lease);

            lease = ESCameraLease.Invalid;
        }

        public void TryAutoPushedToPool()
        {
            if (!IsRecycled)
                Pool.PushToPool(this);
        }
    }
}
