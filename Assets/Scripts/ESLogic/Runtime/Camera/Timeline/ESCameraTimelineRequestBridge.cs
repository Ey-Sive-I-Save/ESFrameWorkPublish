using System;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Timeline 相机镜头的纯数据描述。Timeline 集成层只能持有 ProfileKey 与目标 Transform，
    /// 不能取得 VCam、CinemachineBrain 或 Priority。它最终仍是 Director 活跃集合中的 Shot，
    /// 不存在绕过仲裁的“Timeline 特权通道”。
    /// </summary>
    [Serializable]
    public struct ESCameraTimelineShot
    {
        public ESCameraViewId viewId;
        public string profileKey;
        public int priority;
        public UnityEngine.Object owner;
        public Transform follow;
        public Transform lookAt;

        public bool IsValid => !string.IsNullOrWhiteSpace(profileKey)
                               && owner != null
                               && follow != null
                               && viewId.IsValid;

        public ESCameraRequest ToRequest()
        {
            return ESCameraRequest.CreateShot(viewId, profileKey, priority, owner, follow, lookAt);
        }

        public static ESCameraTimelineShot Create(
            ESCameraViewId viewId,
            string profileKey,
            int priority,
            UnityEngine.Object owner,
            Transform follow,
            Transform lookAt = null)
        {
            return new ESCameraTimelineShot
            {
                viewId = viewId,
                profileKey = profileKey,
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
    }
}
