using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace ES
{

    [Serializable, TypeRegistryItem("状态基类")]
    public class StateBase
    {
        #region  基础属性

        [NonSerialized]
        public StateMachine host;

        // 建议：添加初始化方法
        public virtual void Initialize(StateMachine machine)
        {
            host = machine;
        }

        /// <summary>
        /// 状态激活时间（用于追踪持续时长）
        /// </summary>
        [NonSerialized]
        public float activationTime = 0f;

        [LabelText("共享数据", SdfIconType.Calendar2Date), FoldoutGroup("基础属性"), NonSerialized/*不让自动序列化*/] public StateSharedData stateSharedData = null;
        [LabelText("自变化数据", SdfIconType.Calendar3Range), FoldoutGroup("基础属性")] public StateVariableData stateVariableData;

        /// <summary>
        /// 运行时动画数据 - 由Calculator创建并管理，零GC
        /// </summary>
        [NonSerialized]
        private AnimationCalculatorRuntime _animationRuntime;

        #endregion

        #region 键


        public string strKey;
        public int intKey;
        #endregion

        #region 状态生命周期
        public StateBaseStatus baseStatus = StateBaseStatus.Never;
        public StateRuntimePhase stateRuntimePhase = StateRuntimePhase.Running;
        public void OnStateEnter()
        {
            if (baseStatus == StateBaseStatus.Running) return;
            baseStatus = StateBaseStatus.Running;
            stateRuntimePhase = StateRuntimePhase.Running;
            activationTime = Time.time; // 记录激活时间
            OnStateEnterLogic();
        }

        public void OnStateUpdate()
        {
            OnStateUpdateLogic();
        }

        public void OnStateExit()
        {
            if (baseStatus != StateBaseStatus.Running) return;
            baseStatus = StateBaseStatus.Never;
            //这里需要编写释放逻辑
            OnStateExitLogic();
        }
        #endregion

        #region 应用层用户自己重写逻辑
        protected virtual void OnStateEnterLogic()
        {
            //默认的进入执行逻辑
        }
        protected virtual void OnStateUpdateLogic()
        {
            //默认的更新执行逻辑
        }
        protected virtual void OnStateExitLogic()
        {
            //默认的退出执行逻辑
        }


        #endregion

        #region Playable创建与管理

        /// <summary>
        /// 创建状态的Playable节点 - 零GC高性能实现
        /// 使用StateSharedData中的混合计算器生成Playable
        /// </summary>
        /// <param name="graph">PlayableGraph引用</param>
        /// <param name="output">输出Playable引用</param>
        /// <returns>是否创建成功</returns>
        public virtual bool CreatePlayable(PlayableGraph graph, out Playable output)
        {
            output = Playable.Null;

            // 验证数据有效性
            if (stateSharedData?.animationConfig?.calculator == null)
            {
                return false;
            }

            // 创建运行时数据（仅创建一次）
            if (_animationRuntime == null)
            {
                _animationRuntime = stateSharedData.animationConfig.calculator.CreateRuntimeData();
            }

            // 委托给Calculator初始化Playable
            Playable tempOutput = Playable.Null;
            bool success = stateSharedData.animationConfig.calculator.InitializeRuntime(
                _animationRuntime,
                graph,
                ref tempOutput
            );

            if (success && tempOutput.IsValid())
            {
                output = tempOutput;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 更新动画权重 - 每帧调用（可选）
        /// </summary>
        /// <param name="context">状态上下文</param>
        /// <param name="deltaTime">帧时间</param>
        public virtual void UpdateAnimationWeights(StateMachineContext context, float deltaTime)
        {
            if (_animationRuntime?.IsInitialized == true && stateSharedData?.animationConfig?.calculator != null)
            {
                stateSharedData.animationConfig.calculator.UpdateWeights(_animationRuntime, context, deltaTime);
            }
        }

        /// <summary>
        /// 销毁Playable - 状态退出时调用（零GC）
        /// </summary>
        public virtual void DestroyPlayable()
        {
            if (_animationRuntime != null)
            {
                // 使用Runtime的Cleanup方法统一清理所有Playable资源
                _animationRuntime.Cleanup();
                _animationRuntime = null;
            }
        }

        /// <summary>
        /// 获取当前主动画Clip（调试用）
        /// </summary>
        public virtual AnimationClip GetCurrentClip()
        {
            if (_animationRuntime?.IsInitialized == true && stateSharedData?.animationConfig?.calculator != null)
            {
                return stateSharedData.animationConfig.calculator.GetCurrentClip(_animationRuntime);
            }
            return null;
        }

        /// <summary>
        /// 检查状态是否应该自动退出（根据持续时间模式）
        /// </summary>
        /// <param name="currentTime">当前时间</param>
        /// <returns>是否应该退出</returns>
        public virtual bool ShouldAutoExit(float currentTime)
        {
            if (stateSharedData?.basicConfig == null) return false;

            var config = stateSharedData.basicConfig;
            float elapsedTime = currentTime - activationTime;

            switch (config.durationMode)
            {
                case StateDurationMode.Infinite:
                    return false; // 无限持续，不自动退出

                case StateDurationMode.Timed:
                    return elapsedTime >= config.timedDuration;

                case StateDurationMode.UntilAnimationEnd:
                    // 检查动画是否播放完毕
                    if (_animationRuntime?.IsInitialized == true && stateSharedData?.animationConfig?.calculator != null)
                    {
                        var clip = stateSharedData.animationConfig.calculator.GetCurrentClip(_animationRuntime);
                        if (clip != null)
                        {
                            // 获取动画速度
                            float speed = 1.0f;
                            if (stateSharedData.animationConfig.calculator is StateAnimationMixCalculatorForSimpleClip simpleCalc)
                            {
                                speed = simpleCalc.speed;
                            }
                            
                            float animDuration = clip.length / Mathf.Max(0.01f, speed);
                            return elapsedTime >= animDuration;
                        }
                    }
                    return false;

                default:
                    return false;
            }
        }

        #endregion

    }
}





