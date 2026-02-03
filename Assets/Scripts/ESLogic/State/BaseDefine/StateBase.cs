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

        /// <summary>
        /// 动画控制的自动退出标志（AnimationClip反向控制）
        /// </summary>
        [NonSerialized]
        private bool _shouldAutoExitFromAnimation = false;

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
            _shouldAutoExitFromAnimation = false; // 重置动画完毕标志
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
                
                // ★ AnimationClip反向控制：检测动画是否播放完毕（仅针对UntilAnimationEnd模式）
                if (stateSharedData.basicConfig.durationMode == StateDurationMode.UntilAnimationEnd)
                {
                    CheckAnimationCompletion();
                }
            }
        }

        /// <summary>
        /// 检测动画播放完毕状态（AnimationClip反向控制）
        /// 当动画播放进度>=1.0且为非循环模式时，标记应该退出
        /// </summary>
        private void CheckAnimationCompletion()
        {
            if (_animationRuntime == null || !_animationRuntime.IsInitialized)
                return;

            // 检查单个Playable（SimpleClip）
            if (_animationRuntime.singlePlayable.IsValid())
            {
                Playable playable = _animationRuntime.singlePlayable;
                if (playable.GetPlayableType() == typeof(AnimationClipPlayable))
                {
                    var clipPlayable = (AnimationClipPlayable)playable;
                    var animationClip = GetAnimationClipFromPlayable(clipPlayable);
                    
                    if (animationClip != null && !animationClip.isLooping)
                    {
                        double currentTime = playable.GetTime();
                        double duration = playable.GetDuration();
                        
                        // 播放进度>=1.0，标记为应该退出
                        if (duration > 0.001 && currentTime >= duration - 0.016) // 0.016 = 1帧容错（60fps）
                        {
                            _shouldAutoExitFromAnimation = true;
                        }
                    }
                }
            }
            // 检查Mixer中的Playables（BlendTree/DirectBlend）
            else if (_animationRuntime.playables != null)
            {
                // 多动画混合的情况：只有当所有非循环动画都播放完毕才退出
                bool hasNonLoopingClip = false;
                bool allNonLoopingCompleted = true;

                for (int i = 0; i < _animationRuntime.playables.Length; i++)
                {
                    Playable playable = _animationRuntime.playables[i];
                    if (!playable.IsValid()) continue;

                    if (playable.GetPlayableType() == typeof(AnimationClipPlayable))
                    {
                        var clipPlayable = (AnimationClipPlayable)playable;
                        var animationClip = GetAnimationClipFromPlayable(clipPlayable);
                        
                        if (animationClip != null && !animationClip.isLooping)
                        {
                            hasNonLoopingClip = true;
                            
                            double currentTime = playable.GetTime();
                            double duration = playable.GetDuration();
                            
                            if (duration > 0.001 && currentTime < duration - 0.016)
                            {
                                allNonLoopingCompleted = false;
                                break;
                            }
                        }
                    }
                }

                if (hasNonLoopingClip && allNonLoopingCompleted)
                {
                    _shouldAutoExitFromAnimation = true;
                }
            }
        }

        /// <summary>
        /// 从AnimationClipPlayable获取AnimationClip
        /// TODO: 当前使用简单实现，后续可优化为缓存或更高效的方式
        /// </summary>
        private AnimationClip GetAnimationClipFromPlayable(AnimationClipPlayable clipPlayable)
        {
            // 当前简单实现：直接调用GetAnimationClip()
            return clipPlayable.GetAnimationClip();
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
                    // 检查动画是否播放完毕（优先使用AnimationClip反向控制的标志）
                    if (_shouldAutoExitFromAnimation)
                        return true;
                    
                    // 备用逻辑：通过Clip长度计算
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





