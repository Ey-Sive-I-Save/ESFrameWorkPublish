using UnityEngine;

namespace ES
{
    /// <summary>
    /// 一个可由业务引用的镜头内容定义。它不保存任何场景实例；场景 Binding 通过
    /// rigKey 在当前 Scene Rig Registry 中解析真正的 Cinemachine Rig。
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "ES", sourceAssembly: "ES_Logic", sourceClassName: "ESCameraProfile")]
    [CreateAssetMenu(menuName = "【ES】/配置/相机/相机视图定义", fileName = "ESCameraViewDefinition")]
    public sealed class ESCameraViewDefinition : ScriptableObject
    {
        [Tooltip("稳定内容身份。Skill、角色和载具只能引用此身份，不能持有 VCam 引用。")]
        public ESCameraDefinitionReference definition;

        // 仅保存待用户触发迁移的旧序列化值；运行时绝不以它作为 fallback。
        [SerializeField, HideInInspector]
        [UnityEngine.Serialization.FormerlySerializedAs("profileKey")]
        [UnityEngine.Serialization.FormerlySerializedAs("definitionKey")]
        private string legacyDefinitionKey;

        [Tooltip("由 ESCameraRigCatalog 解析的稳定 Rig 键。")]
        public string rigKey;

        [Tooltip("普通 VCam + CinemachinePOV 使用的每秒轴速度（单位通常为角度）。")]
        [HideInInspector] public Vector2 povLookSensitivity = new Vector2(220f, 90f);

        [Tooltip("CinemachineFreeLook 使用的每秒轴速度。vertical 必须按 FreeLook 的 0-1 轴范围配置。")]
        [HideInInspector] public Vector2 freeLookSensitivity = new Vector2(220f, 0.5f);

        [Tooltip("鼠标/触摸位移转换为单帧镜头增量时使用的缩放。手柄仍按每秒灵敏度和 deltaTime 计算。")]
        [Range(0.0001f, 0.1f)]
        [HideInInspector] public float pointerLookScale = 0.001f;

        [Tooltip("普通 POV 镜头每秒最大轴速（角度）。")]
        [HideInInspector] public Vector2 maxPovLookRate = new Vector2(720f, 180f);

        [Tooltip("FreeLook 镜头每秒最大轴速。Y 使用 FreeLook 的 0-1 轴范围。")]
        [HideInInspector] public Vector2 maxFreeLookRate = new Vector2(720f, 1f);

        [HideInInspector] public bool invertVerticalLook;

        [Range(1f, 179f)] public float baseFieldOfView = 60f;

        [Min(0.01f)] public float baseDistanceScale = 1f;

        public Vector3 baseShoulderOffset = Vector3.zero;

        [Min(0f)] public float baseShakeAmplitude;

        [Header("Third Person Obstruction")]
        [Tooltip("仅当前获胜 Rig 的 CinemachineCollider 会启用。关闭后该定义不执行镜头避障查询。")]
        [HideInInspector] public bool enableObstruction = true;

        [Tooltip("专用相机遮挡层；不要使用 Everything。")]
        [HideInInspector] public LayerMask obstructionMask = ESPhysicsLayers.CameraObstacleMask;

        [Range(0.01f, 1f)]
        [HideInInspector] public float obstructionCameraRadius = 0.2f;

        [Range(0.01f, 3f)]
        [HideInInspector] public float obstructionMinimumDistance = 0.25f;

        [Range(1, 8)]
        [HideInInspector] public int obstructionMaximumEffort = 4;

        [Min(0f)]
        [HideInInspector] public float obstructionDamping = 0.12f;

        [Min(0f)]
        [HideInInspector] public float obstructionDampingWhenOccluded = 0.05f;

        public ESCameraDefinitionReference Definition => definition;
        public string LegacyDefinitionKey => legacyDefinitionKey;
        public bool IsValid => definition.IsConfigured
                               && !string.IsNullOrWhiteSpace(rigKey)
                               && IsContentValid;

        /// <summary>
        /// 制作期内容门禁。Inspector 的 Range/Min 属性只约束 UI 输入，运行时或批处理仍可能
        /// 写入 NaN、Infinity 或非法距离；Catalog 必须在建立索引前拒绝这些值。
        /// </summary>
        public bool IsContentValid
        {
            get
            {
                if (float.IsNaN(baseFieldOfView) || float.IsInfinity(baseFieldOfView)
                    || baseFieldOfView < 1f || baseFieldOfView > 179f)
                    return false;
                if (float.IsNaN(baseDistanceScale) || float.IsInfinity(baseDistanceScale) || baseDistanceScale <= 0f)
                    return false;
                if (float.IsNaN(pointerLookScale) || float.IsInfinity(pointerLookScale) || pointerLookScale <= 0f)
                    return false;
                if (!IsFinite(povLookSensitivity) || !IsFinite(freeLookSensitivity)
                    || freeLookSensitivity.y < 0f || povLookSensitivity.y < 0f)
                    return false;
                if (!IsFinite(maxPovLookRate) || maxPovLookRate.x <= 0f || maxPovLookRate.y <= 0f
                    || !IsFinite(maxFreeLookRate) || maxFreeLookRate.x <= 0f || maxFreeLookRate.y <= 0f)
                    return false;
                if (float.IsNaN(baseShakeAmplitude) || float.IsInfinity(baseShakeAmplitude) || baseShakeAmplitude < 0f)
                    return false;
                if (!enableObstruction)
                    return true;
                return obstructionCameraRadius > 0f
                       && obstructionMinimumDistance > 0f
                       && obstructionMaximumEffort >= 1
                       && obstructionMaximumEffort <= 8
                       && obstructionMask.value != 0
                       && IsFinite(obstructionDamping)
                       && IsFinite(obstructionDampingWhenOccluded)
                       && obstructionDamping >= 0f
                       && obstructionDampingWhenOccluded >= 0f;
            }
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                   && !float.IsNaN(value.y) && !float.IsInfinity(value.y);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

#if UNITY_EDITOR
        public void SetDefinitionForAuthoring(ESCameraDefinitionReference value)
        {
            definition = value;
            legacyDefinitionKey = null;
        }

        internal bool TryMigrateLegacyDefinition(ESCameraDefinitionReference value)
        {
            if (definition.IsConfigured || string.IsNullOrWhiteSpace(legacyDefinitionKey))
                return false;

            if (!string.Equals(legacyDefinitionKey, value.stringKey, System.StringComparison.Ordinal))
                return false;

            definition = value;
            legacyDefinitionKey = null;
            return true;
        }
#endif
    }
}
