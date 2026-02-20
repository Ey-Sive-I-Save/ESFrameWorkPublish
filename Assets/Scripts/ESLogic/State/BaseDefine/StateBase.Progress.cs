using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// 文件：StateBase.Progress.cs
// 作用：StateBase 的进度/阶段/循环统计与事件触发相关逻辑。
//
// Public（本文件定义的对外成员；按模块分组，“先功能、后成员”，便于扫读）：
//
// 【运行时进度数据】
// - 激活时间戳：public float activationTime
// - 进入后累计时长：public float hasEnterTime
// - 归一化进度（0-1）：public float normalizedProgress
// - 总进度（可 > 1）：public float totalProgress
// - 循环次数计数：public int loopCount
//
// 【淡入/淡出回调（状态机驱动）】
// - （已移除：当前工程无使用点，且会引入额外回调/虚派发入口）
//
// 【进度换算】
// - 标准动画时长：public float GetStandardAnimationDuration()
//
// Protected（子类扩展点）：基于动画事件触发的回调（如 OnAnimationEvent）。
// Private/Internal：进度计算、阶段推进、动画事件触发的内部缓存与检测逻辑。
// ============================================================================

namespace ES
{
    public partial class StateBase
    {
        #region 运行时进度数据

        /// <summary>
        /// 状态激活时间（用于追踪持续时长）
        /// </summary>
        [NonSerialized]
        public float activationTime = 0f;

        /// <summary>
        /// 已经进入时间（从进入状态过去的时间）
        /// </summary>
        [NonSerialized]
        public float hasEnterTime = 0f;

        /// <summary>
        /// 归一化进度（适用于循环动画）[0-1]
        /// </summary>
        [NonSerialized]
        public float normalizedProgress = 0f;

        /// <summary>
        /// 总体进度（比如5.5代表已经循环了5次半）
        /// </summary>
        [NonSerialized]
        public float totalProgress = 0f;

        /// <summary>
        /// 循环次数（完成的循环次数）
        /// </summary>
        [NonSerialized]
        public int loopCount = 0;

        /// <summary>
        /// 上一帧的归一化进度（用于事件触发检测）
        /// </summary>
        [NonSerialized]
        private float _lastNormalizedProgress = 0f;

        #endregion

        #region 回调（扩展点）

        /// <summary>
        /// 动画事件回调
        /// 当动画播放到指定时间点时触发
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="eventParam">事件参数</param>
        protected virtual void OnAnimationEvent(string eventName, string eventParam)
        {
#if STATEMACHINEDEBUG
#if UNITY_EDITOR
            var shared = stateSharedData;
                var stateName = GetStateNameSafe();
            StateMachineDebugSettings.Instance.LogStateTransition(
                $"[AnimEvent] State:{stateName} | Event:{eventName} | Param:{eventParam}");
#endif
#endif

            // 可以通知StateMachine广播事件
            host?.BroadcastAnimationEvent(this, eventName, eventParam);
        }

        #endregion

        #region 进度与事件触发

        /// <summary>
        /// 更新运行时进度数据
        /// </summary>
        private void UpdateRuntimeProgress(float deltaTime)
        {
            // 获取标准动画时长（不经历外部缩放速度）
            float standardDuration = GetStandardAnimationDuration();

            if (standardDuration > 0.001f)
            {
                // 计算总体进度
                totalProgress = hasEnterTime / standardDuration;

                // 归一化进度与循环次数：合并一次 Floor 计算，避免 % 与重复 Floor
                float loopsF = Mathf.Floor(totalProgress);
                loopCount = (int)loopsF;
                normalizedProgress = totalProgress - loopsF;
            }
            else
            {
                // 无法获取时长，使用简单递增
                normalizedProgress = Mathf.Repeat(hasEnterTime, 1.0f);
                totalProgress = hasEnterTime;
            }

            // 自动阶段评估（无手动锁定时）
            // 性能关键：不启用阶段自动评估的状态，这里必须是零成本跳过。
            if (_autoPhaseByTimeCached)
            {
                UpdateAutoRuntimePhase();
            }

            // 检测动画事件触发
            CheckAnimationEventTriggers();
        }

        private void UpdateAutoRuntimePhase()
        {
            if (_runtimePhaseManual)
                return;

            // 调用点已用 _autoPhaseByTimeCached 做过 gating，这里只保留必要字段访问。
            var phaseConfig = _phaseConfigCached;
            if (phaseConfig == null) return;

            var nextPhase = phaseConfig.EvaluatePhase(normalizedProgress);
            if (nextPhase != _runtimePhase)
            {
                SwitchRuntimePhase(nextPhase, false);
            }
        }

        /// <summary>
        /// 检测并触发动画事件
        /// </summary>
        private void CheckAnimationEventTriggers()
        {
            // 获取动画配置中的事件列表
            var triggerEvents = GetAnimationTriggerEvents();
            if (triggerEvents == null || triggerEvents.Count == 0)
                return;

            foreach (var evt in triggerEvents)
            {
                // 检测是否穿过触发点
                bool crossedTriggerPoint = false;

                // 情况1：正常前进，穿过触发点
                if (_lastNormalizedProgress < evt.normalizedTime &&
                    normalizedProgress >= evt.normalizedTime)
                {
                    crossedTriggerPoint = true;
                }

                // 情况2：循环回绕（从1回到0）
                if (_lastNormalizedProgress > normalizedProgress)
                {
                    // 新循环开始，重置触发标记
                    evt.ResetTrigger();

                    // 检查是否在新循环中穿过触发点
                    if (evt.normalizedTime < normalizedProgress)
                    {
                        crossedTriggerPoint = true;
                    }
                }

                // 触发事件
                if (crossedTriggerPoint)
                {
                    if (!evt.triggerOnce || !evt.hasTriggered)
                    {
                        OnAnimationEvent(evt.eventName, evt.eventParam);
                        evt.hasTriggered = true;
                    }
                }
            }

            _lastNormalizedProgress = normalizedProgress;
        }

        /// <summary>
        /// 获取动画触发事件列表
        /// </summary>
        private List<TriggerEventAt> GetAnimationTriggerEvents()
        {
            // 暂时返回null，后续集成AnimationClipConfig时实现
            // TODO: 从animationConfig.calculator中获取triggerEvents
            return null;
        }

        /// <summary>
        /// 重置动画事件触发标记
        /// </summary>
        private void ResetAnimationEventTriggers()
        {
            var triggerEvents = GetAnimationTriggerEvents();
            if (triggerEvents != null)
            {
                foreach (var evt in triggerEvents)
                {
                    evt.ResetTrigger();
                }
            }
            _lastNormalizedProgress = 0f;
        }

        /// <summary>
        /// 获取标准动画时长（不经历外部缩放速度）
        /// </summary>
        public float GetStandardAnimationDuration()
        {
            var runtime = _animationRuntime;
            if (runtime == null || !runtime.IsInitialized) return 0f;

            var calculator = _calculatorCached;
            if (calculator == null) return 0f;

            return calculator.GetStandardDuration(runtime);
        }

        #endregion
    }
}
