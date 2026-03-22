using System;
using UnityEngine;

// ============================================================================
// 文件：StateBase.IK.cs
// 作用：StateBase 的 IK 运行时接口与自动应用配置（状态侧只定义节点目标权重；lerpingRate 由 Driver 或外部系统负责）。
//
// Public（本文件定义的对外成员；按模块分组，“先功能、后成员”，便于扫读）：
//
// 【基础 IK 操作】
// - 设置四肢 IK 目标：public void SetIKGoal(...)
// - 设置 IK Hint（肘/膝）：public void SetIKHintPosition(...)
// - 设置注视目标（LookAt）：public void SetLookAtTarget(...)
//
// 【状态控制与查询】
// - 立即禁用 IK：public void DisableIK()
// - 是否激活：public bool IsIKActive { get; }
//
// Private/Internal：IK 激活状态与运行时写入辅助。
// ============================================================================

namespace ES
{
    public partial class StateBase
    {
        #region IK 配置与运行时

        /// <summary>
        /// IK是否已激活（避免重复设置）
        /// </summary>
        [NonSerialized]
        private bool _ikActive = false;

        /// <summary>
        /// 状态进入时同步 IK 开关缓存。
        /// 目标位姿必须由运行时代码提供，状态资产不再承担“配置目标”。
        /// </summary>
        private void ApplyIKConfigOnEnter(AnimationCalculatorRuntime runtime)
        {
            var proceduralDriveConfig = GetProceduralDriveConfigCachedOrSharedOrNull();
            if (proceduralDriveConfig == null) return;
            if (!proceduralDriveConfig.enableIK) return;

            if (runtime != null)
            {
                runtime.ik.enabled = runtime.ik.enabled || runtime.ik.HasAnyTargetWeight;
            }
        }

        /// <summary>
        /// 启用IK并设置目标（由外部系统调用）
        /// </summary>
        /// <param name="goal">IK目标（左/右手/脚）</param>
        /// <param name="position">目标位置</param>
        /// <param name="rotation">目标旋转</param>
        /// <param name="weight">目标权重 [0-1]</param>
        /// <param name="lerpingRate">lerping 速度倍率，1 为默认</param>
        public void SetIKGoal(IKGoal goal, Vector3 position, Quaternion rotation, float weight, float lerpingRate = 1f)
        {
            if (_animationRuntime == null) return;
            _ikActive = true;
            _animationRuntime.SetIKGoal(goal, position, rotation, weight, lerpingRate);
        }

        /// <summary>
        /// 设置 IK 目标（常用简化版：直接传目标 Transform）。
        /// </summary>
        public void SetIKGoal(IKGoal goal, Transform target, float weight, float lerpingRate = 1f, Transform hintTarget = null, bool useTargetRotation = true)
        {
            if (target == null) return;

            Quaternion rotation = useTargetRotation ? target.rotation : Quaternion.identity;
            SetIKGoal(goal, target.position, rotation, weight, lerpingRate);

            if (hintTarget != null)
                SetIKHintPosition(goal, hintTarget.position);
        }

        /// <summary>
        /// 设置IK提示位置（肘/膝方向引导）
        /// </summary>
        public void SetIKHintPosition(IKGoal goal, Vector3 position)
        {
            if (_animationRuntime == null) return;
            _ikActive = true;
            _animationRuntime.SetIKHintPosition(goal, position);
        }

        /// <summary>
        /// 设置注视目标（简化版，骨骼子权重保持当前值不变）
        /// </summary>
        public void SetLookAtTarget(Vector3 position, float weight, float lerpingRate = 1f)
        {
            if (_animationRuntime == null) return;
            _ikActive = true;
            _animationRuntime.SetLookAtTarget(position, weight, lerpingRate);
        }

        /// <summary>
        /// 设置注视目标（常用简化版：直接传目标 Transform）。
        /// </summary>
        public void SetLookAtTarget(Transform target, float weight, float lerpingRate = 1f)
        {
            if (target == null) return;
            SetLookAtTarget(target.position, weight, lerpingRate);
        }

        /// <summary>
        /// 设置注视目标（完整版，同时指定 Body/Head/Eyes/Clamp 四个骨骼分权重）。
        /// </summary>
        public void SetLookAtTarget(Vector3 position, float weight, float lerpingRate,
            float bodyWeight, float headWeight, float eyesWeight, float clampWeight)
        {
            if (_animationRuntime == null) return;
            _ikActive = true;
            _animationRuntime.SetLookAtTarget(position, weight, lerpingRate, bodyWeight, headWeight, eyesWeight, clampWeight);
        }

        /// <summary>
        /// 禁用IK
        /// </summary>
        public void DisableIK()
        {
            _ikActive = false;

            // 禁用时也回到配置合成值，避免外部写入残留影响后续再次启用。
            _resolvedRuntimeDirty = true;
            RefreshResolvedRuntimeConfig();

            if (_animationRuntime != null)
            {
                _animationRuntime.ResetIKData();
            }
        }

        /// <summary>
        /// IK是否处于活跃状态
        /// </summary>
        public bool IsIKActive => _ikActive && _animationRuntime != null && _animationRuntime.ik.enabled;

        #endregion
    }
}
