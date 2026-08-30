using UnityEngine;

namespace ES
{
    /// <summary>
    /// 全游戏唯一的相机基础策略。ViewDefinition 只描述镜头差异；输入、避障和安全边界
    /// 等跨镜头不变量由此集中管理。旧 Definition 字段仍可作为兼容回退，避免现有资产失效。
    /// </summary>
    [CreateAssetMenu(menuName = "【ES】/配置/相机/全局相机策略", fileName = "ESCameraGlobalPolicy")]
    public sealed class ESCameraGlobalPolicy : ScriptableObject
    {
        [Header("输入")]
        public Vector2 povLookSensitivity = new Vector2(220f, 90f);
        public Vector2 freeLookSensitivity = new Vector2(220f, 0.5f);
        [Min(0.0001f)] public float pointerLookScale = 0.001f;
        public Vector2 maxPovLookRate = new Vector2(720f, 180f);
        public Vector2 maxFreeLookRate = new Vector2(720f, 1f);
        public bool invertVerticalLook;

        [Header("避障")]
        public bool enableObstruction = true;
        public LayerMask obstructionMask = ESPhysicsLayers.CameraObstacleMask;
        [Min(0.01f)] public float obstructionCameraRadius = 0.2f;
        [Min(0.01f)] public float obstructionMinimumDistance = 0.25f;
        [Range(1, 8)] public int obstructionMaximumEffort = 4;
        [Min(0f)] public float obstructionDamping = 0.12f;
        [Min(0f)] public float obstructionDampingWhenOccluded = 0.05f;

        public bool IsValid
        {
            get { return TryValidate(out _); }
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (!IsFinite(povLookSensitivity) || !IsFinite(freeLookSensitivity)
                || !IsFinite(maxPovLookRate) || !IsFinite(maxFreeLookRate)
                || povLookSensitivity.y < 0f || freeLookSensitivity.y < 0f
                || !IsFinite(pointerLookScale) || pointerLookScale <= 0f
                || maxPovLookRate.x <= 0f || maxPovLookRate.y <= 0f
                || maxFreeLookRate.x <= 0f || maxFreeLookRate.y <= 0f)
            {
                error = "输入灵敏度、指针缩放或最大转速必须是有限且大于零的值。";
                return false;
            }

            if (enableObstruction && (obstructionMask.value == 0
                || !IsFinite(obstructionCameraRadius) || obstructionCameraRadius <= 0f
                || !IsFinite(obstructionMinimumDistance) || obstructionMinimumDistance <= 0f
                || obstructionMaximumEffort < 1
                || obstructionMaximumEffort > 8 || !IsFinite(obstructionDamping)
                || !IsFinite(obstructionDampingWhenOccluded) || obstructionDamping < 0f
                || obstructionDampingWhenOccluded < 0f))
            {
                error = "避障已启用时，遮挡层、探针半径、最小距离、查询预算和阻尼必须有效。";
                return false;
            }

            return true;
        }

#if UNITY_EDITOR
        public void ResetToCommercialDefaults()
        {
            povLookSensitivity = new Vector2(220f, 90f);
            freeLookSensitivity = new Vector2(220f, 0.5f);
            pointerLookScale = 0.001f;
            maxPovLookRate = new Vector2(720f, 180f);
            maxFreeLookRate = new Vector2(720f, 1f);
            invertVerticalLook = false;
            enableObstruction = true;
            obstructionMask = ESPhysicsLayers.CameraObstacleMask;
            obstructionCameraRadius = 0.2f;
            obstructionMinimumDistance = 0.25f;
            obstructionMaximumEffort = 4;
            obstructionDamping = 0.12f;
            obstructionDampingWhenOccluded = 0.05f;
        }
#endif

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
