using System;
using System.Runtime.CompilerServices;
using UnityEngine;

// ============================================================================
// 文件：StateBase.MatchTarget.cs
// 作用：StateBase 的 Animator.MatchTarget 封装（自动应用配置、启动/更新/取消、重施加策略）。
//
// Public（本文件定义的对外成员；按模块分组，“先功能、后成员”，便于扫读）：
//
// 【重施加策略（运行时可调）】
// - 是否允许重施加：public bool allowMatchTargetReapply
// - 重施加最小间隔：public float matchTargetReapplyInterval
// - 重施加最小距离：public float matchTargetReapplyMinDistance
// - 重施加最小角度：public float matchTargetReapplyMinAngle
//
// 【调试开关（全局）】
// - 是否输出调试日志：public static bool debugMatchTarget
// - 调试输出帧间隔：public static int debugMatchTargetFrameInterval
//
// 【启动/更新/取消】
// - 启动一次 MatchTarget：public void StartMatchTarget(...)
// - 更新目标点：public void UpdateMatchTargetTarget(...)
// - 取消 MatchTarget：public void CancelMatchTarget()
//
// 【状态查询】
// - 是否激活：public bool IsMatchTargetActive { get; }
//
// Private/Internal：进入时应用配置、MatchTarget 内部状态缓存与重施加判定。
// ============================================================================

namespace ES
{
    public partial class StateBase
    {
        #region MatchTarget 配置与运行时

        /// <summary>
        /// MatchTarget是否已激活
        /// </summary>
        [NonSerialized]
        private bool _matchTargetActive = false;

        [NonSerialized]
        private Vector3 _matchTargetLastAppliedPos = Vector3.zero;

        [NonSerialized]
        private Quaternion _matchTargetLastAppliedRot = Quaternion.identity;

        [NonSerialized]
        private float _matchTargetLastApplyTime = -999f;

        [NonSerialized]
        public bool allowMatchTargetReapply = false;

        [NonSerialized]
        public float matchTargetReapplyInterval = 0.05f;

        [NonSerialized]
        public float matchTargetReapplyMinDistance = 0.02f;

        [NonSerialized]
        public float matchTargetReapplyMinAngle = 2f;

        public static bool debugMatchTarget = true;
        public static int debugMatchTargetFrameInterval = 1;

        /// <summary>
        /// 状态进入时自动从 StateAnimationConfigData 应用MatchTarget配置到Runtime
        /// 仅在 enableMatchTarget=true 且 autoActivateMatchTarget=true 时生效
        /// </summary>
        private void ApplyMatchTargetConfigOnEnter(AnimationCalculatorRuntime runtime)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (runtime == null) throw new InvalidOperationException("ApplyMatchTargetConfigOnEnter: runtime 不能为空（调用方应在边界处做一次性判空）");
#endif

            var animConfig = GetAnimConfigCachedOrSharedOrNull();
            if (animConfig == null) return;
            if (!animConfig.enableMatchTarget || !animConfig.autoActivateMatchTarget) return;

            animConfig.ApplyMatchTargetConfigToRuntime(runtime);
            _matchTargetActive = true;
        }

        /// <summary>
        /// 启动MatchTarget（根动作对齐到目标位置）
        /// 用于攀爬、跳跃落地等需要精确对齐的场景
        /// </summary>
        /// <param name="targetPos">目标位置</param>
        /// <param name="targetRot">目标旋转</param>
        /// <param name="bodyPart">身体部位</param>
        /// <param name="startNormTime">开始归一化时间 [0-1]</param>
        /// <param name="endNormTime">结束归一化时间 [0-1]</param>
        /// <param name="posWeight">位置权重 (XYZ分量)</param>
        /// <param name="rotWeight">旋转权重 [0-1]</param>
        public void StartMatchTarget(Vector3 targetPos, Quaternion targetRot, AvatarTarget bodyPart,
            float startNormTime, float endNormTime, Vector3 posWeight, float rotWeight = 1f)
        {
            if (_animationRuntime == null)
            {
#if UNITY_EDITOR
                if (debugMatchTarget)
                {
                    Debug.LogWarning($"{GetMatchTargetLogTag()} Start failed: runtime null");
                }
#endif
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
                var dbg = StateMachineDebugSettings.Instance;
                if (dbg != null)
                {
                var stateName = GetStateNameSafe();
                    dbg.LogWarning($"[StateBase] StartMatchTarget failed: runtime is null | State={stateName}");
                }
#endif
#endif
                return;
            }

#if UNITY_EDITOR
            if (debugMatchTarget)
            {
                Debug.Log(
                    $"{GetMatchTargetLogTag()} Start " +
                    $"pos={targetPos:F3} rot={targetRot.eulerAngles:F1} body={bodyPart} " +
                    $"time=[{startNormTime:F2},{endNormTime:F2}] posW={posWeight:F2} rotW={rotWeight:F2}");
            }
#endif

#if STATEMACHINEDEBUG
#if UNITY_EDITOR
            var dbgStart = StateMachineDebugSettings.Instance;
            if (dbgStart != null && dbgStart.IsAnimationBlendEnabled)
            {
                dbgStart.LogAnimationBlend(
                    $"{GetMatchTargetLogTag()} Start " +
                    $"pos={targetPos:F3} rot={targetRot.eulerAngles:F1} body={bodyPart} " +
                    $"time=[{startNormTime:F2},{endNormTime:F2}] posW={posWeight:F2} rotW={rotWeight:F2}");
            }
#endif
#endif

            _matchTargetActive = true;
            _matchTargetLastAppliedPos = Vector3.zero;
            _matchTargetLastAppliedRot = Quaternion.identity;
            _matchTargetLastApplyTime = -999f;
            _animationRuntime.StartMatchTarget(targetPos, targetRot, bodyPart, startNormTime, endNormTime, posWeight, rotWeight);
        }

        public void UpdateMatchTargetTarget(Vector3 targetPos, Quaternion targetRot)
        {
            if (_animationRuntime == null) return;
            ref var mt = ref _animationRuntime.matchTarget;
            if (!mt.active || mt.completed) return;

            mt.position = targetPos;
            mt.rotation = targetRot;
        }

        /// <summary>
        /// 取消MatchTarget
        /// </summary>
        public void CancelMatchTarget()
        {
            _matchTargetActive = false;
            if (_animationRuntime != null)
            {
                _animationRuntime.ResetMatchTargetData();
            }
            _matchTargetLastAppliedPos = Vector3.zero;
            _matchTargetLastAppliedRot = Quaternion.identity;
            _matchTargetLastApplyTime = -999f;
        }

        /// <summary>
        /// MatchTarget是否处于活跃状态
        /// </summary>
        public bool IsMatchTargetActive => _matchTargetActive && _animationRuntime != null && _animationRuntime.matchTarget.active;

        /// <summary>
        /// MatchTarget完成回调（子类可重写）
        /// </summary>
        protected virtual void OnMatchTargetCompleted()
        {
            _matchTargetActive = false;
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
            var shared = stateSharedData;
            var stateName = GetStateNameSafe();
            StateMachineDebugSettings.Instance.LogStateTransition(
                $"[StateBase] MatchTarget完成 | State={stateName}");
#endif
#endif
        }

        /// <summary>
        /// 由StateMachine在Update中调用处理MatchTarget（内部接口）
        /// </summary>
        internal void ProcessMatchTarget(Animator animator)
        {
            var runtime = _animationRuntime;
            if (runtime == null)
            {
#if UNITY_EDITOR
                if (debugMatchTarget && ShouldLogMatchTarget())
                {
                    Debug.LogWarning($"{GetMatchTargetLogTag()} Gate: runtime null");
                }
#endif
                return;
            }

            ref var mt = ref runtime.matchTarget;

            if (!mt.active)
            {
#if UNITY_EDITOR
                if (debugMatchTarget && ShouldLogMatchTarget())
                {
                    Debug.Log($"{GetMatchTargetLogTag()} Gate: inactive");
                }
#endif
                return;
            }

            if (mt.completed)
            {
#if UNITY_EDITOR
                if (debugMatchTarget && ShouldLogMatchTarget())
                {
                    Debug.Log($"{GetMatchTargetLogTag()} Gate: completed");
                }
#endif
                return;
            }

            if (!runtime.IsMatchTargetInRange(normalizedProgress))
            {
#if UNITY_EDITOR
                if (debugMatchTarget && ShouldLogMatchTarget())
                {
                    Debug.Log(
                        $"{GetMatchTargetLogTag()} Gate: out of range " +
                        $"progress={normalizedProgress:F2} range=[{mt.startTime:F2},{mt.endTime:F2}]");
                }
#endif
                return;
            }

            // ★ 使用Animator.MatchTarget进行实际的根动作对齐
            if (animator == null)
            {
#if UNITY_EDITOR
                if (debugMatchTarget && ShouldLogMatchTarget())
                {
                    Debug.LogWarning($"{GetMatchTargetLogTag()} Gate: animator null");
                }
#endif
                return;
            }

            if (animator.isMatchingTarget)
            {
#if UNITY_EDITOR
                if (debugMatchTarget && ShouldLogMatchTarget())
                {
                    Debug.Log($"{GetMatchTargetLogTag()} Gate: already matching");
                }
#endif
                if (!allowMatchTargetReapply || !ShouldReapplyMatchTarget(ref mt))
                {
                    return;
                }
            }

            if (debugMatchTarget && ShouldLogMatchTarget())
            {
                Debug.Log(
                    $"{GetMatchTargetLogTag()} Apply " +
                    $"pos={mt.position:F3} rot={mt.rotation.eulerAngles:F1} body={mt.bodyPart} " +
                    $"time=[{mt.startTime:F2},{mt.endTime:F2}] posW={mt.positionWeight:F2} rotW={mt.rotationWeight:F2}");
            }

#if STATEMACHINEDEBUG
#if UNITY_EDITOR
            var dbgApply = StateMachineDebugSettings.Instance;
            if (dbgApply != null && dbgApply.IsAnimationBlendEnabled && ShouldLogMatchTarget())
            {
                dbgApply.LogAnimationBlend(
                    $"{GetMatchTargetLogTag()} Apply " +
                    $"pos={mt.position:F3} rot={mt.rotation.eulerAngles:F1} body={mt.bodyPart} " +
                    $"time=[{mt.startTime:F2},{mt.endTime:F2}] posW={mt.positionWeight:F2} rotW={mt.rotationWeight:F2}");
            }
#endif
#endif

            if (!animator.isMatchingTarget)
            {
                var matchRange = new MatchTargetWeightMask(mt.positionWeight, mt.rotationWeight);
                animator.MatchTarget(
                    mt.position,
                    mt.rotation,
                    mt.bodyPart,
                    matchRange,
                    mt.startTime,
                    mt.endTime
                );
                _matchTargetLastAppliedPos = mt.position;
                _matchTargetLastAppliedRot = mt.rotation;
                _matchTargetLastApplyTime = Time.time;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldReapplyMatchTarget(ref AnimationCalculatorRuntime.MatchTargetRuntimeData mt)
        {
            if (matchTargetReapplyInterval > 0f && Time.time - _matchTargetLastApplyTime < matchTargetReapplyInterval)
            {
                return false;
            }

            // 性能关键：这里可能在“已匹配中”被频繁调用。
            // - Distance/Angle 会引入 sqrt/acos
            // - 改用 sqrMagnitude + Quaternion.Dot 阈值比较，零GC且更省。

            bool distBigEnough;
            float minDist = matchTargetReapplyMinDistance;
            if (minDist <= 0f)
            {
                distBigEnough = true;
            }
            else
            {
                float minDistSqr = minDist * minDist;
                distBigEnough = (_matchTargetLastAppliedPos - mt.position).sqrMagnitude >= minDistSqr;
            }

            bool angleBigEnough;
            float minAngle = matchTargetReapplyMinAngle;
            if (minAngle <= 0f)
            {
                angleBigEnough = true;
            }
            else
            {
                // Quaternion.Angle(a,b) >= minAngle  <=>  abs(dot(a,b)) <= cos(minAngle/2)
                float dot = Mathf.Abs(Quaternion.Dot(_matchTargetLastAppliedRot, mt.rotation));
                float cosHalf = Mathf.Cos(minAngle * 0.5f * Mathf.Deg2Rad);
                angleBigEnough = dot <= cosHalf;
            }

            return distBigEnough || angleBigEnough;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldLogMatchTarget()
        {
            if (debugMatchTargetFrameInterval <= 1) return true;
            return Time.frameCount % debugMatchTargetFrameInterval == 0;
        }

        private string GetMatchTargetLogTag()
        {
            var name = GetStateNameSafe();
            var id = GetStateIdSafe();
            return $"[MatchTarget][State={name}][Id={id}]";
        }

        #endregion
    }
}
