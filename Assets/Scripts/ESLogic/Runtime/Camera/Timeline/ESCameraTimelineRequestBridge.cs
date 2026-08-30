using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace ES
{
    /// <summary>
    /// Timeline 相机镜头的纯数据描述。Timeline 集成层只能持有稳定 Definition 引用与目标 Transform，
    /// 不能取得 VCam、CinemachineBrain 或 Priority。它最终仍是 Director 活跃集合中的 Shot，
    /// 不存在绕过仲裁的“Timeline 特权通道”。
    /// </summary>
    [Serializable]
    public struct ESCameraTimelineShot
    {
        public ESCameraViewId viewId;
        [SerializeField, HideInInspector, FormerlySerializedAs("profileKey"), FormerlySerializedAs("definitionKey")]
        private string legacyDefinitionKey;
        public ESCameraDefinitionReference definition;
        public int priority;
        public UnityEngine.Object owner;
        public Transform follow;
        public Transform lookAt;

        public bool IsValid => definition.IsConfigured
                               && owner != null
                               && follow != null
                               && viewId.IsValid;

        public ESCameraRequest ToRequest()
        {
            return ESCameraRequest.CreateShot(viewId, definition, priority, owner, follow, lookAt);
        }

        public static ESCameraTimelineShot Create(
            ESCameraViewId viewId,
            ESCameraDefinitionReference definition,
            int priority,
            UnityEngine.Object owner,
            Transform follow,
            Transform lookAt = null)
        {
            return new ESCameraTimelineShot
            {
                viewId = viewId,
                definition = definition,
                priority = priority,
                owner = owner,
                follow = follow,
                lookAt = lookAt,
            };
        }
    }

    /// <summary>
    /// Timeline 的唯一正式相机控制路径。未来 PlayableBehaviour 的 OnBehaviourPlay/
    /// ProcessFrame/OnBehaviourPause 只允许调用 Push/Update/Release；严禁使用 Cinemachine
    /// Timeline Track 或直接改写 Virtual Camera Priority 与 Blend。
    /// </summary>
    public static class ESCameraTimelineRequestBridge
    {
        /// <summary>
        /// 受信 Timeline 执行器取得短生命周期 Scope 的入口。普通数据对象不能自行构造
        /// Timeline 权限；模块会校验当前本地 Owner 与 Cutscene RuntimeMode。
        /// </summary>
        internal static bool TryOpenScope(
            Entity owner,
            out ESCameraControlScope scope,
            out ESCameraFailure failure)
        {
            scope = null;
            failure = ESCameraFailure.None;
            ESCameraModule camera = ESGameManager.Camera;
            return camera != null
                ? camera.TryOpenTimelineScope(owner, out scope, out failure)
                : SetFailure(out failure, ESCameraFailureReason.ModuleUnavailable);
        }

        /// <summary>Playable 的 OnPlay/PrepareFrame 可重复调用；Scope 统一幂等清理。</summary>
        internal static bool TryPush(
            ESCameraControlScope scope,
            in ESCameraTimelineShot shot,
            out ESCameraLease lease,
            out ESCameraFailure failure)
        {
            lease = ESCameraLease.Invalid;
            failure = ESCameraFailure.None;
            if (scope == null || !scope.IsValid)
                return SetFailure(out failure, ESCameraFailureReason.ScopeInvalid);
            if (!shot.IsValid || shot.viewId != scope.ViewId || !ReferenceEquals(shot.owner, scope.RequestOwner))
                return SetFailure(out failure, ESCameraFailureReason.RequestRejected);

            return scope.TryPlayShot(
                shot.definition,
                shot.follow,
                shot.lookAt,
                shot.priority - 320,
                out lease,
                out failure);
        }

        public static ESCameraLease Push(in ESCameraTimelineShot shot)
        {
            if (!shot.IsValid)
                return ESCameraLease.Invalid;

            ESCameraModule camera = ESGameManager.Camera;
            return camera != null ? camera.Push(shot.ToRequest()) : ESCameraLease.Invalid;
        }

        public static bool Update(ESCameraLease lease, in ESCameraTimelineShot shot)
        {
            if (!lease.IsValid || !shot.IsValid)
                return false;

            ESCameraModule camera = ESGameManager.Camera;
            return camera != null && camera.Update(lease, shot.ToRequest());
        }

        public static bool Release(ESCameraLease lease)
        {
            if (!lease.IsValid)
                return false;

            ESCameraModule camera = ESGameManager.Camera;
            return camera != null && camera.Release(lease);
        }

        private static bool SetFailure(out ESCameraFailure failure, ESCameraFailureReason reason)
        {
            failure = new ESCameraFailure(reason);
            return false;
        }
    }
}
