using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ES
{
    /// <summary>
    /// 流水线 - 管理一条动画流水线上的状态
    /// 每条流水线独立运行,最终混合输出
    /// </summary>
    public class StatePipeline
    {
        public StatePipelineType Type { get; private set; }
        
        // Fallback状态ID（管线空转时激活）
        public int? FallbackStateId { get; set; }
        
        // 当前激活的状态
        private StateInstance _currentState;
        
        // 上一个状态(用于过渡)
        private StateInstance _previousState;
        
        // 流水线的Playable混合器
        private AnimationMixerPlayable _mixer;
        
        // 过渡进度 (0~1)
        private float _transitionProgress;
        
        // 过渡持续时间
        private float _transitionDuration;
        
        // 是否正在过渡
        private bool _isTransitioning;
        
        // 流水线权重 (用于与其他流水线混合)
        private float _weight = 1f;

        public StatePipeline(StatePipelineType type, PlayableGraph graph)
        {
            Type = type;
            _mixer = AnimationMixerPlayable.Create(graph, 2);
        }

        public AnimationMixerPlayable GetMixer() => _mixer;

        /// <summary>
        /// 进入新状态
        /// </summary>
        public void EnterState(StateDefinition stateDef, StateMachineContext context, PlayableGraph graph, float currentTime)
        {
            // 保存上一个状态用于过渡
            if (_currentState != null)
            {
                _previousState = _currentState;
            }

            // 创建新状态实例
            _currentState = new StateInstance(stateDef, context, graph, currentTime);
            _currentState.SetMixer(_mixer);
            _currentState.Enter();

            // 开始过渡
            if (stateDef.transitionComponent != null && _previousState != null)
            {
                _transitionDuration = stateDef.transitionComponent.duration;
                _transitionProgress = 0f;
                _isTransitioning = true;
            }
            else
            {
                // 无过渡,直接切换
                _isTransitioning = false;
                _transitionProgress = 1f;
                
                if (_previousState != null)
                {
                    _previousState.Exit();
                    _previousState = null;
                }
            }
        }

        /// <summary>
        /// 更新流水线
        /// </summary>
        /// <returns>返回是否需要激活Fallback状态</returns>
        public bool Update(float deltaTime, float currentTime)
        {
            bool needsFallback = false;
            
            // 检测空转：当前无状态且有Fallback配置
            if (_currentState == null && FallbackStateId.HasValue)
            {
                needsFallback = true;
            }
            
            // 更新过渡
            if (_isTransitioning && _previousState != null)
            {
                _transitionProgress += deltaTime / _transitionDuration;
                
                if (_transitionProgress >= 1f)
                {
                    _transitionProgress = 1f;
                    _isTransitioning = false;
                    
                    // 完成过渡,清理上一个状态
                    _previousState.Exit();
                    _previousState = null;
                }

                // 更新过渡权重
                UpdateTransitionWeights();
            }

            // 更新当前状态
            if (_currentState != null)
            {
                _currentState.Update(deltaTime, currentTime);
            }

            // 更新上一个状态(如果在过渡中)
            if (_previousState != null && _isTransitioning)
            {
                _previousState.Update(deltaTime, currentTime);
            }
            
            return needsFallback;
        }

        private void UpdateTransitionWeights()
        {
            if (_currentState == null || _previousState == null)
                return;

            // 使用过渡曲线计算权重
            float curveValue = _transitionProgress;
            if (_currentState.Definition.transitionComponent != null)
            {
                curveValue = _currentState.Definition.transitionComponent.transitionCurve.Evaluate(_transitionProgress);
            }

            // 设置Mixer的输入权重
            _mixer.SetInputWeight(0, 1f - curveValue); // 上一个状态
            _mixer.SetInputWeight(1, curveValue);      // 当前状态
        }

        /// <summary>
        /// 强制退出当前状态
        /// </summary>
        public void ExitCurrentState()
        {
            if (_currentState != null)
            {
                _currentState.Exit();
                _currentState = null;
            }

            if (_previousState != null)
            {
                _previousState.Exit();
                _previousState = null;
            }

            _isTransitioning = false;
        }

        /// <summary>
        /// 检查是否可以进入新状态
        /// </summary>
        public bool CanEnterState(StateDefinition stateDef)
        {
            // 如果当前无状态,可以直接进入
            if (_currentState == null)
                return true;

            // 检查优先级
            if (stateDef.priority <= _currentState.Definition.priority)
                return false;

            return true;
        }

        /// <summary>
        /// 获取当前状态
        /// </summary>
        public StateInstance GetCurrentState() => _currentState;

        /// <summary>
        /// 是否有激活状态
        /// </summary>
        public bool HasActiveState() => _currentState != null;

        /// <summary>
        /// 设置流水线权重
        /// </summary>
        public void SetWeight(float weight)
        {
            _weight = Mathf.Clamp01(weight);
        }

        public float GetWeight() => _weight;

        /// <summary>
        /// 清理流水线
        /// </summary>
        public void Cleanup()
        {
            if (_currentState != null)
            {
                _currentState.Exit();
                _currentState = null;
            }

            if (_previousState != null)
            {
                _previousState.Exit();
                _previousState = null;
            }

            if (_mixer.IsValid())
            {
                _mixer.Destroy();
            }
        }
    }

    /// <summary>
    /// 状态实例 - 运行时的状态实例
    /// </summary>
    public class StateInstance
    {
        public StateDefinition Definition { get; private set; }
        public StateRuntime Runtime { get; private set; }
        
        private float _enterTime;
        private float _stateTime;

        public StateInstance(StateDefinition definition, StateMachineContext context, PlayableGraph graph, float currentTime)
        {
            Definition = definition;
            Runtime = new StateRuntime(graph, context);
            _enterTime = currentTime;
            _stateTime = 0f;
        }

        public void SetMixer(AnimationMixerPlayable mixer)
        {
            Runtime.SetMixer(mixer);
        }

        /// <summary>
        /// 进入状态
        /// </summary>
        public void Enter()
        {
            // 调用所有组件的OnEnter
            foreach (var component in Definition.GetAllComponents())
            {
                if (component != null && component.enabled)
                {
                    component.OnStateEnter(Runtime);
                }
            }
        }

        /// <summary>
        /// 更新状态
        /// </summary>
        public void Update(float deltaTime, float currentTime)
        {
            _stateTime += deltaTime;
            
            // 更新Runtime时间信息
            Runtime.CurrentTime = currentTime;
            Runtime.StateTime = _stateTime;
            
            // 计算归一化时间
            if (Definition.duration > 0f)
            {
                Runtime.NormalizedTime = Mathf.Clamp01(_stateTime / Definition.duration);
            }
            else
            {
                Runtime.NormalizedTime = 0f; // 无限循环状态
            }

            // 调用所有组件的OnUpdate
            foreach (var component in Definition.GetAllComponents())
            {
                if (component != null && component.enabled)
                {
                    component.OnStateUpdate(Runtime, deltaTime);
                }
            }

            // 检查保持条件
            if (!Definition.CheckKeepConditions(Runtime.Context))
            {
                // 不满足保持条件,应该退出
                // 这里需要通知状态机
            }

            // 检查退出条件
            if (Definition.CheckExitConditions(Runtime.Context))
            {
                // 满足退出条件
                // 这里需要通知状态机
            }

            // 检查自动转换
            CheckAutoTransitions();
        }

        private void CheckAutoTransitions()
        {
            if (Definition.transitions == null || Definition.transitions.Count == 0)
                return;

            foreach (var transition in Definition.transitions)
            {
                if (transition.CheckConditions(Runtime.Context, Runtime.NormalizedTime))
                {
                    // 满足转换条件
                    // 这里需要通知状态机进行转换
                }
            }
        }

        /// <summary>
        /// 退出状态
        /// </summary>
        public void Exit()
        {
            // 调用所有组件的OnExit
            foreach (var component in Definition.GetAllComponents())
            {
                if (component != null && component.enabled)
                {
                    component.OnStateExit(Runtime);
                }
            }
        }

        public float GetNormalizedTime() => Runtime.NormalizedTime;
        public float GetStateTime() => _stateTime;
        public bool IsInRecovery() => false;
    }
}
