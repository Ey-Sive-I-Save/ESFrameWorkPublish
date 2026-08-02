using UnityEngine;

namespace ES
{
    /// <summary>
    /// 一个可由业务引用的镜头内容定义。它不保存任何场景实例；场景 Binding 通过
    /// rigKey 在当前 Scene Rig Registry 中解析真正的 Cinemachine Rig。
    /// </summary>
    [CreateAssetMenu(menuName = "ES/Camera/Profile", fileName = "ESCameraProfile")]
    public sealed class ESCameraProfile : ScriptableObject
    {
        [Tooltip("稳定内容键。Skill、角色和载具只能引用此键，不能持有 VCam 引用。")]
        public string profileKey;

        [Tooltip("由 ESCameraRigCatalog 解析的稳定 Rig 键。")]
        public string rigKey;

        [Tooltip("普通 VCam + CinemachinePOV 使用的每秒轴速度（单位通常为角度）。")]
        public Vector2 povLookSensitivity = new Vector2(220f, 90f);

        [Tooltip("CinemachineFreeLook 使用的每秒轴速度。vertical 必须按 FreeLook 的 0-1 轴范围配置。")]
        public Vector2 freeLookSensitivity = new Vector2(220f, 0.5f);

        public bool invertVerticalLook;

        [Range(1f, 179f)]
        public float baseFieldOfView = 60f;

        [Min(0.01f)]
        public float baseDistanceScale = 1f;

        public Vector3 baseShoulderOffset = Vector3.zero;

        [Min(0f)]
        public float baseShakeAmplitude;

        [Header("Third Person Obstruction")]
        [Tooltip("仅当前获胜 Rig 的 CinemachineCollider 会启用。关闭后该 Profile 不执行镜头避障查询。")]
        public bool enableObstruction = true;

        [Tooltip("专用相机遮挡层；不要使用 Everything。")]
        public LayerMask obstructionMask = ESPhysicsLayers.CameraObstacleMask;

        [Range(0.01f, 1f)]
        public float obstructionCameraRadius = 0.2f;

        [Range(0.01f, 3f)]
        public float obstructionMinimumDistance = 0.25f;

        [Range(1, 8)]
        public int obstructionMaximumEffort = 4;

        [Min(0f)]
        public float obstructionDamping = 0.12f;

        [Min(0f)]
        public float obstructionDampingWhenOccluded = 0.05f;

        public bool IsValid => !string.IsNullOrWhiteSpace(profileKey) && !string.IsNullOrWhiteSpace(rigKey);
    }
}
