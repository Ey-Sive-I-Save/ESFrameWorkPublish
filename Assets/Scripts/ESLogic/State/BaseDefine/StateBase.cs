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
    public class StateBase : IPoolableAuto
    {
        #region 对象池支持

        /// <summary>
        /// StateBase 对象池
        /// 容量: 500个对象
        /// 预热: 10个初始对象
        /// </summary>
        public static readonly ESSimplePool<StateBase> Pool = new ESSimplePool<StateBase>(
            factoryMethod: () => new StateBase(),
            resetMethod: (obj) => obj.OnResetAsPoolable(),
            initCount: 10,
            maxCount: -1,
            poolDisplayName: "StateBase Pool"
        );

        /// <summary>
        /// 对象回收标记
        /// </summary>
        public bool IsRecycled { get; set; }

        /// <summary>
        /// 重置对象状态
        /// </summary>
        public void OnResetAsPoolable()
        {
            host = null;
            activationTime = 0f;
            hasEnterTime = 0f;
            normalizedProgress = 0f;
            totalProgress = 0f;
            loopCount = 0;
            _lastNormalizedProgress = 0f;
            baseStatus = StateBaseStatus.Never;
            stateRuntimePhase = StateRuntimePhase.Running;
            strKey = null;
            intKey = -1;
            stateSharedData = null;
            stateVariableData = null;
            _shouldAutoExitFromAnimation = false;
            DestroyPlayable();
            // 其他字段在此重置
        }

        /// <summary>
        /// 尝试回收到对象池
        /// </summary>
        public void TryAutoPushedToPool()
        {
            if (!IsRecycled)
            {
                IsRecycled = true;
                Pool.PushToPool(this);
            }
        }

        #endregion

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

        #region 运行时数据（保证可用）
        
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
        /// 总体进度（比如05.5代表已经循环了5次）
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

        #region 增强回调系统

        /// <summary>
        /// 状态进度回调（每帧调用）
        /// 子类可重写实现基于进度的逻辑
        /// </summary>
        protected virtual void OnProgressUpdate(float normalizedProgress, float totalProgress)
        {
            // 默认不实现，子类重写
        }

        /// <summary>
        /// 循环完成回调
        /// 子类可重写实现循环触发逻辑
        /// </summary>
        protected virtual void OnLoopCompleted(int loopCount)
        {
            // 默认不实现，子类重写
        }

        /// <summary>
        /// 动画事件回调
        /// 当动画播放到指定时间点时触发
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="eventParam">事件参数</param>
        protected virtual void OnAnimationEvent(string eventName, string eventParam)
        {
            StateMachineDebugSettings.Instance.LogStateTransition(
                $"[AnimEvent] State:{stateSharedData?.basicConfig.stateName} | Event:{eventName} | Param:{eventParam}");
            
            // 可以通知StateMachine广播事件
            host?.BroadcastAnimationEvent(this, eventName, eventParam);
        }

        /// <summary>
        /// 淡入完成回调
        /// 由StateMachine调用
        /// </summary>
        public virtual void OnFadeInComplete()
        {
            // 默认不实现，子类重写
        }

        /// <summary>
        /// 淡出开始回调
        /// 由StateMachine调用
        /// </summary>
        public virtual void OnFadeOutStarted()
        {
            // 默认不实现，子类重写
        }

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
            
            // 重置运行时数据
            hasEnterTime = 0f;
            normalizedProgress = 0f;
            totalProgress = 0f;
            loopCount = 0;
            _lastNormalizedProgress = 0f; // 重置事件检测
            _shouldAutoExitFromAnimation = false; // 重置动画完毕标志
            
            // 重置动画事件触发标记
            ResetAnimationEventTriggers();
            
            OnStateEnterLogic();
        }

        private void MarkAutoExitFromAnimation(string reason)
        {
            if (_shouldAutoExitFromAnimation) return;
            _shouldAutoExitFromAnimation = true;
            Debug.Log($"[StateBase] AutoExitFlag=TRUE | State={stateSharedData?.basicConfig?.stateName} | Reason={reason}\n{Environment.StackTrace}");
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
        /// 当前动画计算器类型（用于扩展能力判断）
        /// </summary>
        public StateAnimationMixerKind CurrentCalculatorKind
            => stateSharedData?.animationConfig?.calculator?.CalculatorKind ?? StateAnimationMixerKind.Unknown;

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
            // 更新运行时数据
            hasEnterTime += deltaTime;
            UpdateRuntimeProgress(deltaTime);
            
            if (_animationRuntime?.IsInitialized == true && stateSharedData?.animationConfig?.calculator != null)
            {
                Debug.Log($"[StateBase] 更新动画权重: State={stateSharedData.basicConfig.stateName}");
                stateSharedData.animationConfig.calculator.UpdateWeights(_animationRuntime, context, deltaTime);
                
                // ★ AnimationClip反向控制：检测动画是否播放完毕（仅针对UntilAnimationEnd模式）
                if (stateSharedData.basicConfig.durationMode == StateDurationMode.UntilAnimationEnd)
                {
                    CheckAnimationCompletion();
                }
            }
        }

        /// <summary>
        /// 更新运行时进度数据
        /// </summary>
        private void UpdateRuntimeProgress(float deltaTime)
        {
            // 获取标准动画时长（不经历外部缩放速度）
            float standardDuration = GetStandardAnimationDuration();
            
            int previousLoopCount = loopCount;  // 记录上一次的循环次数
            
            if (standardDuration > 0.001f)
            {
                // 计算总体进度
                totalProgress = hasEnterTime / standardDuration;
                
                // 计算归一化进度 [0-1]
                normalizedProgress = totalProgress % 1.0f;
                
                // 计算循环次数
                loopCount = Mathf.FloorToInt(totalProgress);
            }
            else
            {
                // 无法获取时长，使用简单递增
                normalizedProgress = Mathf.Repeat(hasEnterTime, 1.0f);
                totalProgress = hasEnterTime;
            }
            
            // 调用进度回调
            OnProgressUpdate(normalizedProgress, totalProgress);
            
            // 检测循环完成
            if (loopCount > previousLoopCount)
            {
                OnLoopCompleted(loopCount);
            }
            
            // 检测动画事件触发
            CheckAnimationEventTriggers();
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
            if (_animationRuntime?.IsInitialized == true && stateSharedData?.animationConfig?.calculator != null)
            {
                return stateSharedData.animationConfig.calculator.GetStandardDuration(_animationRuntime);
            }
            return 0f;
        }

        /// <summary>
        /// 检测动画播放完毕状态（AnimationClip反向控制）
        /// 当动画播放进度>=1.0且为非循环模式时，标记应该退出
        /// </summary>
        private void CheckAnimationCompletion()
        {
            if (stateSharedData?.basicConfig == null)
            {
                Debug.LogWarning("[StateBase] 检测动画完毕失败：stateSharedData或basicConfig为空");
                return;
            }

            Debug.Log($"[StateBase] 检测动画完毕: State={stateSharedData.basicConfig.stateName} | RuntimeInit={_animationRuntime != null && _animationRuntime.IsInitialized}");
            if (_animationRuntime == null || !_animationRuntime.IsInitialized)
                return;

            // 检查单个Playable（SimpleClip）
            if (_animationRuntime.singlePlayable.IsValid())
            {
                Playable playable = _animationRuntime.singlePlayable;
                Debug.Log($"[StateBase] 单Playable检测: Type={playable.GetPlayableType().Name} | Time={playable.GetTime():F3} | Duration={playable.GetDuration():F3}");
                if (playable.GetPlayableType() == typeof(AnimationClipPlayable))
                {
                    var clipPlayable = (AnimationClipPlayable)playable;
                    var animationClip = GetAnimationClipFromPlayable(clipPlayable);
                    
                    if (animationClip != null && !animationClip.isLooping)
                    {
                        Debug.Log($"[StateBase] 单Clip: {animationClip.name} | Loop={animationClip.isLooping}");
                        double currentTime = playable.GetTime();
                        double duration = playable.GetDuration();
                        if (double.IsInfinity(duration) || duration <= 0.001)
                        {
                            Debug.LogWarning($"[StateBase] 单Clip时长异常: {duration}");
                            return;
                        }
                        
                        // 播放进度>=1.0，标记为应该退出
                        if (duration > 0.001 && currentTime >= duration - 0.016) // 0.016 = 1帧容错（60fps）
                        {
                            MarkAutoExitFromAnimation("SingleClipComplete");
                            Debug.Log($"[StateBase] 单Clip播放完毕 -> 标记退出");
                        }
                    }
                    else
                    {
                        Debug.Log($"[StateBase] 单Clip为空或循环播放，跳过完成检测");
                    }
                }
                else
                {
                    Debug.Log($"[StateBase] 单Playable非AnimationClipPlayable，跳过完成检测");
                }
            }
            // 检查Mixer中的Playables（BlendTree/DirectBlend）
            else if (_animationRuntime.playables != null)
            {
                Debug.Log($"[StateBase] 多Playable检测: Count={_animationRuntime.playables.Length}");
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
                            Debug.Log($"[StateBase] 子Clip[{i}]: {animationClip.name} | Time={currentTime:F3} | Duration={duration:F3} | Loop={animationClip.isLooping}");
                            if (double.IsInfinity(duration) || duration <= 0.001)
                            {
                                Debug.LogWarning($"[StateBase] 子Clip时长异常: {duration}");
                                continue;
                            }
                            
                            if (duration > 0.001 && currentTime < duration - 0.016)
                            {
                                allNonLoopingCompleted = false;
                                break;
                            }
                        }
                        else
                        {
                            Debug.Log($"[StateBase] 子Clip[{i}]为空或循环播放，跳过完成检测");
                        }
                    }
                    else
                    {
                        Debug.Log($"[StateBase] 子Playable[{i}]非AnimationClipPlayable: {playable.GetPlayableType().Name}");
                    }
                }

                if (hasNonLoopingClip && allNonLoopingCompleted)
                {
                    MarkAutoExitFromAnimation("AllNonLoopingClipsComplete");
                    Debug.Log($"[StateBase] 多Clip全部播放完毕 -> 标记退出");
                }
            }
            else
            {
                Debug.LogWarning("[StateBase] 无可检测Playable（singlePlayable无效且playables为空）");
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
                    if (elapsedTime >= config.timedDuration)
                    {
                        Debug.Log($"[StateBase] ShouldAutoExit=TRUE (Timed) | State={stateSharedData.basicConfig.stateName} | Elapsed={elapsedTime:F3} | Duration={config.timedDuration:F3}");
                        return true;
                    }
                    return false;

                case StateDurationMode.UntilAnimationEnd:
                    // 检查动画是否播放完毕（优先使用AnimationClip反向控制的标志）
                    if (_shouldAutoExitFromAnimation)
                    {
                        Debug.Log($"[StateBase] ShouldAutoExit=TRUE (AnimFlag) | State={stateSharedData.basicConfig.stateName}");
                        return true;
                    }
                    
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
                            if (elapsedTime >= animDuration)
                            {
                                Debug.Log($"[StateBase] ShouldAutoExit=TRUE (ClipLength) | State={stateSharedData.basicConfig.stateName} | Clip={clip.name} | Elapsed={elapsedTime:F3} | AnimDuration={animDuration:F3} | Speed={speed:F3}");
                                return true;
                            }
                            return false;
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





