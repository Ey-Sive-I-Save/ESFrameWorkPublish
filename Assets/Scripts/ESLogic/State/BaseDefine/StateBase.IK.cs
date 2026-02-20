using System;
using UnityEngine;

// ============================================================================
// 文件：StateBase.IK.cs
// 作用：StateBase 的 IK 运行时接口与自动应用配置（外部可写入 ResolvedConfig.ikTargetWeight 与内部逻辑共用）。
//
// Public（本文件定义的对外成员；按模块分组，“先功能、后成员”，便于扫读）：
//
// 【基础 IK 操作】
// - 设置四肢 IK 目标：public void SetIKGoal(...)
// - 设置 IK Hint（肘/膝）：public void SetIKHintPosition(...)
// - 设置注视目标（LookAt）：public void SetLookAtTarget(...)
//
// 【总权重控制】
// - 设置 IK 总目标权重：public void SetIKTargetWeight(float weight)
// - 外部写入 IK 总目标权重（共用 ResolvedConfig）：public void SetIKExternalTargetWeight(float weight)
// - 取消外部写入（回到配置合成结果）：public void ClearIKExternalTargetWeight()
//
// 【状态控制与查询】
// - 立即禁用 IK：public void DisableIK()
// - 是否激活：public bool IsIKActive { get; }
//
// Private/Internal：进入时应用配置、默认权重缓存、以及 ResolvedConfig 的 IK 权重合成结果。
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

        private float _ikDefaultTargetWeight = 1f;

        /// <summary>
        /// 状态进入时自动从 StateAnimationConfigData 应用IK配置到Runtime
        /// 仅在 enableIK=true 且 ikSourceMode != CodeOnly 时生效
        /// </summary>
        private void ApplyIKConfigOnEnter(AnimationCalculatorRuntime runtime)
        {
    #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (runtime == null) throw new InvalidOperationException("ApplyIKConfigOnEnter: runtime 不能为空（调用方应在边界处做一次性判空）");
    #endif

            var animConfig = GetAnimConfigCachedOrSharedOrNull();
            if (animConfig == null) return;
            if (!animConfig.enableIK) return;
            if (animConfig.ikSourceMode == IKSourceMode.CodeOnly) return;

            // 从Inspector配置数据写入Runtime
            Transform rootTransform = host?.BoundAnimator?.transform;
            animConfig.ApplyIKConfigToRuntime(runtime, rootTransform);
            _ikActive = true;
        }

        /// <summary>
        /// 启用IK并设置目标（由外部系统调用）
        /// </summary>
        /// <param name="goal">IK目标（左/右手/脚）</param>
        /// <param name="position">目标位置</param>
        /// <param name="rotation">目标旋转</param>
        /// <param name="weight">权重 [0-1]</param>
        public void SetIKGoal(IKGoal goal, Vector3 position, Quaternion rotation, float weight)
        {
            if (_animationRuntime == null) return;
            _ikActive = true;
            _animationRuntime.SetIKGoal(goal, position, rotation, weight);
        }

        /// <summary>
        /// 设置IK提示位置（肘/膝方向引导）
        /// </summary>
        public void SetIKHintPosition(IKGoal goal, Vector3 position)
        {
            if (_animationRuntime == null) return;
            _animationRuntime.SetIKHintPosition(goal, position);
        }

        /// <summary>
        /// 设置注视目标
        /// </summary>
        public void SetLookAtTarget(Vector3 position, float weight)
        {
            if (_animationRuntime == null) return;
            _ikActive = true;
            _animationRuntime.SetLookAtTarget(position, weight);
        }

        /// <summary>
        /// 设置IK总目标权重（平滑过渡）
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void SetIKTargetWeight(float weight)
        {
            if (_animationRuntime == null) return;
            float clamped = Mathf.Clamp01(weight);
            // 避免每帧写相同值（减少无意义的 native bridge 写入）
            if (Mathf.Abs(_animationRuntime.ik.targetWeight - clamped) <= 0.0001f) return;
            _animationRuntime.ik.targetWeight = clamped;
        }

        /// <summary>
        /// 外部写入 IK 的“总目标权重”（与内部逻辑共用同一个数据源：ResolvedConfig.ikTargetWeight）。
        /// 典型用途：交互/抓取时根据进度或距离动态调节 IK 总权重。
        ///
        /// 说明：
        /// - 本方法不再维护“独立覆盖开关/覆盖值”。
        /// - UpdateAnimationRuntime 读取 ResolvedConfig.ikTargetWeight；外部写入后即可自然参与曲线乘子与阶段覆盖逻辑。
        /// </summary>
        public void SetIKExternalTargetWeight(float weight)
        {
            _ikActive = true;
            float clamped = Mathf.Clamp01(weight);

            // 确保 ResolvedConfig 是最新的合成结果；随后外部写入直接修改该结果，实现“共用”。
            EnsureResolvedRuntimeConfig();
            var resolved = ResolvedConfig;
            resolved.ikOverrideEnabled = true;
            resolved.ikTargetWeight = clamped;

            if (_animationRuntime != null)
            {
                _animationRuntime.ik.enabled = true;
                // 立即把目标权重写到 runtime，避免等到下一帧 UpdateAnimationRuntime 才生效。
                SetIKTargetWeight(clamped);
            }
        }

        /// <summary>
        /// 取消外部写入，恢复由配置合成（RefreshResolvedRuntimeConfig）驱动。
        /// </summary>
        public void ClearIKExternalTargetWeight()
        {
            _resolvedRuntimeDirty = true;
            RefreshResolvedRuntimeConfig();
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
                _animationRuntime.ik.targetWeight = 0f;
                // IK权重会在UpdateAnimationWeights中平滑过渡到0
            }
        }

        /// <summary>
        /// IK是否处于活跃状态
        /// </summary>
        public bool IsIKActive => _ikActive && _animationRuntime != null && _animationRuntime.ik.enabled;

        #endregion
    }
}
