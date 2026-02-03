using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ES
{
    /// <summary>
    /// 多态组件基类 - 所有状态组件的基类
    /// </summary>
    [Serializable]
    public abstract class StateComponent
    {
        public bool enabled = true;
        
        public virtual void OnStateEnter(StateRuntime runtime) { }
        public virtual void OnStateUpdate(StateRuntime runtime, float deltaTime) { }
        public virtual void OnStateExit(StateRuntime runtime) { }
    }

    /// <summary>
    /// 显示组件 - 控制动画Clip的播放
    /// 支持Clip截断、组合、混合等高级功能
    /// </summary>
    [Serializable]
    public class DisplayComponent : StateComponent
    {
        [Tooltip("显示模式")]
        public DisplayMode mode = DisplayMode.SingleClip;
        
        [Tooltip("单个Clip配置")]
        public ClipSegment singleClip;
        
        [Tooltip("多个Clip段配置")]
        public List<ClipSegment> clipSegments;
        
        [Tooltip("混合权重曲线")]
        public AnimationCurve blendCurve = AnimationCurve.Linear(0, 0, 1, 1);

        public override void OnStateEnter(StateRuntime runtime)
        {
            base.OnStateEnter(runtime);
            
            switch (mode)
            {
                case DisplayMode.SingleClip:
                    if (singleClip != null)
                        singleClip.Setup(runtime);
                    break;
                    
                case DisplayMode.MultipleSegments:
                    if (clipSegments != null)
                    {
                        foreach (var segment in clipSegments)
                            segment.Setup(runtime);
                    }
                    break;
            }
        }

        public override void OnStateUpdate(StateRuntime runtime, float deltaTime)
        {
            base.OnStateUpdate(runtime, deltaTime);
            
            float normalizedTime = runtime.NormalizedTime;
            
            switch (mode)
            {
                case DisplayMode.SingleClip:
                    if (singleClip != null)
                        singleClip.Update(runtime, normalizedTime);
                    break;
                    
                case DisplayMode.MultipleSegments:
                    UpdateMultipleSegments(runtime, normalizedTime);
                    break;
            }
        }

        private void UpdateMultipleSegments(StateRuntime runtime, float normalizedTime)
        {
            if (clipSegments == null || clipSegments.Count == 0)
                return;
            
            // 根据归一化时间确定当前应该播放哪个段
            foreach (var segment in clipSegments)
            {
                if (normalizedTime >= segment.startTime && normalizedTime <= segment.endTime)
                {
                    float segmentTime = Mathf.InverseLerp(segment.startTime, segment.endTime, normalizedTime);
                    segment.Update(runtime, segmentTime);
                }
            }
        }

        public enum DisplayMode
        {
            SingleClip,         // 单个完整Clip
            MultipleSegments,   // 多个Clip段组合
            ClipBlending        // Clip混合
        }
    }

    /// <summary>
    /// Clip片段 - 可以截断或组合Clip
    /// </summary>
    [Serializable]
    public class ClipSegment
    {
        [Tooltip("Clip配置键")]
        public string clipKey;
        
        [Tooltip("Clip在段中的起始归一化时间")]
        public float startTime = 0f;
        
        [Tooltip("Clip在段中的结束归一化时间")]
        public float endTime = 1f;
        
        [Tooltip("Clip本身的截断起点(归一化)")]
        public float clipStartOffset = 0f;
        
        [Tooltip("Clip本身的截断终点(归一化)")]
        public float clipEndOffset = 1f;
        
        [Tooltip("播放速度倍率")]
        public float speedMultiplier = 1f;
        
        [Tooltip("是否循环")]
        public bool loop = false;

        [NonSerialized]
        private AnimationClipPlayable _playable;

        public void Setup(StateRuntime runtime)
        {
            // 从Clip表中获取Clip
            var clip = runtime.GetClip(clipKey);
            if (clip == null)
            {
                Debug.LogWarning($"Clip not found: {clipKey}");
                return;
            }
            
            // 创建Playable
            _playable = AnimationClipPlayable.Create(runtime.Graph, clip);
            _playable.SetSpeed(speedMultiplier);
            _playable.SetDuration(clip.length);
            
            // 连接到混合器
            runtime.ConnectToMixer(_playable, 0f);
        }

        public void Update(StateRuntime runtime, float segmentNormalizedTime)
        {
            if (!_playable.IsValid())
                return;
            
            // 计算Clip内的实际时间
            float clipTime = Mathf.Lerp(clipStartOffset, clipEndOffset, segmentNormalizedTime);
            
            var clip = _playable.GetAnimationClip();
            if (clip != null)
            {
                float actualTime = clipTime * clip.length;
                
                if (loop)
                {
                    actualTime = actualTime % clip.length;
                }
                
                _playable.SetTime(actualTime);
            }
            
            // 更新权重
            runtime.UpdatePlayableWeight(_playable, 1f);
        }

        public void Cleanup()
        {
            if (_playable.IsValid())
            {
                _playable.Destroy();
            }
        }
    }

    /// <summary>
    /// 过渡组件 - 控制状态间的过渡
    /// </summary>
    [Serializable]
    public class TransitionComponent : StateComponent
    {
        [Tooltip("过渡持续时间(秒)")]
        public float duration = 0.3f;
        
        [Tooltip("过渡曲线")]
        public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Tooltip("过渡模式")]
        public TransitionMode mode = TransitionMode.Blend;

        public override void OnStateEnter(StateRuntime runtime)
        {
            base.OnStateEnter(runtime);
            runtime.SetTransitionDuration(duration);
        }

        public enum TransitionMode
        {
            Blend,      // 平滑混合
            Cut,        // 硬切
            CrossFade   // 交叉淡化
        }
    }

    /// <summary>
    /// 执行组件 - 执行自定义逻辑(非动画)
    /// 例如:特效、音效、游戏逻辑等
    /// </summary>
    [Serializable]
    public class ExecutionComponent : StateComponent
    {
        [Tooltip("执行时机")]
        public ExecutionTiming timing = ExecutionTiming.OnEnter;
        
        [Tooltip("延迟执行时间(秒)")]
        public float delay = 0f;
        
        [Tooltip("执行动作列表")]
        public List<StateAction> actions = new List<StateAction>();

        private float _executionTime;

        public override void OnStateEnter(StateRuntime runtime)
        {
            base.OnStateEnter(runtime);
            
            if (timing == ExecutionTiming.OnEnter && delay <= 0f)
            {
                ExecuteActions(runtime);
            }
            else
            {
                _executionTime = runtime.CurrentTime + delay;
            }
        }

        public override void OnStateUpdate(StateRuntime runtime, float deltaTime)
        {
            base.OnStateUpdate(runtime, deltaTime);
            
            if (timing == ExecutionTiming.Delayed && runtime.CurrentTime >= _executionTime)
            {
                ExecuteActions(runtime);
                timing = ExecutionTiming.None; // 防止重复执行
            }
            else if (timing == ExecutionTiming.EveryFrame)
            {
                ExecuteActions(runtime);
            }
        }

        public override void OnStateExit(StateRuntime runtime)
        {
            base.OnStateExit(runtime);
            
            if (timing == ExecutionTiming.OnExit)
            {
                ExecuteActions(runtime);
            }
        }

        private void ExecuteActions(StateRuntime runtime)
        {
            if (actions == null)
                return;
            
            foreach (var action in actions)
            {
                if (action != null && action.enabled)
                {
                    action.Execute(runtime);
                }
            }
        }

        public enum ExecutionTiming
        {
            None,
            OnEnter,
            Delayed,
            EveryFrame,
            OnExit
        }
    }

    /// <summary>
    /// 状态动作 - 可执行的具体行为
    /// </summary>
    [Serializable]
    public abstract class StateAction
    {
        public bool enabled = true;
        public abstract void Execute(StateRuntime runtime);
    }

    /// <summary>
    /// 设置参数动作
    /// </summary>
    [Serializable]
    public class SetParameterAction : StateAction
    {
        public string parameterName;
        public ContextParameterType parameterType;
        public float floatValue;
        public int intValue;
        public bool boolValue;

        public override void Execute(StateRuntime runtime)
        {
            var context = runtime.Context;
            switch (parameterType)
            {
                case ContextParameterType.Float:
                    context.SetFloat(parameterName, floatValue);
                    break;
                case ContextParameterType.Int:
                    context.SetInt(parameterName, intValue);
                    break;
                case ContextParameterType.Bool:
                    context.SetBool(parameterName, boolValue);
                    break;
            }
        }
    }

    /// <summary>
    /// IK组件 - 控制IK混合
    /// </summary>
    [Serializable]
    public class IKComponent : StateComponent
    {
        [Tooltip("IK曲线绑定")]
        public List<IKCurveBinding> curveBindings = new List<IKCurveBinding>();

        public override void OnStateUpdate(StateRuntime runtime, float deltaTime)
        {
            base.OnStateUpdate(runtime, deltaTime);
            
            float normalizedTime = runtime.NormalizedTime;
            
            foreach (var binding in curveBindings)
            {
                if (binding.enabled)
                {
                    float value = runtime.Context.EvaluateCurve(binding.curveName, normalizedTime);
                    runtime.SetIKWeight(binding.ikTarget, value);
                }
            }
        }
    }

    /// <summary>
    /// IK曲线绑定
    /// </summary>
    [Serializable]
    public class IKCurveBinding
    {
        public bool enabled = true;
        public string curveName;
        public string ikTarget;  // IK目标名称
    }

    /// <summary>
    /// 状态运行时 - 提供给组件访问运行时信息的接口
    /// </summary>
    public class StateRuntime
    {
        public PlayableGraph Graph { get; set; }
        public StateMachineContext Context { get; set; }
        public float CurrentTime { get; set; }
        public float StateTime { get; set; }
        public float NormalizedTime { get; set; }
        
        private AnimationMixerPlayable _mixer;
        private Dictionary<string, AnimationClip> _clipTable;
        private Dictionary<string, float> _ikWeights;

        public StateRuntime(PlayableGraph graph, StateMachineContext context)
        {
            Graph = graph;
            Context = context;
            _clipTable = new Dictionary<string, AnimationClip>();
            _ikWeights = new Dictionary<string, float>();
        }

        public void SetMixer(AnimationMixerPlayable mixer)
        {
            _mixer = mixer;
        }

        public AnimationClip GetClip(string key)
        {
            return _clipTable.TryGetValue(key, out var clip) ? clip : null;
        }

        public void RegisterClip(string key, AnimationClip clip)
        {
            _clipTable[key] = clip;
        }

        public void ConnectToMixer(Playable playable, float weight)
        {
            if (_mixer.IsValid())
            {
                int inputCount = _mixer.GetInputCount();
                _mixer.SetInputCount(inputCount + 1);
                Graph.Connect(playable, 0, _mixer, inputCount);
                _mixer.SetInputWeight(inputCount, weight);
            }
        }

        public void UpdatePlayableWeight(Playable playable, float weight)
        {
            // 实现权重更新逻辑
        }

        public void SetTransitionDuration(float duration)
        {
            // 实现过渡时长设置
        }

        public void SetIKWeight(string target, float weight)
        {
            _ikWeights[target] = weight;
        }

        public float GetIKWeight(string target)
        {
            return _ikWeights.TryGetValue(target, out float weight) ? weight : 0f;
        }
    }
}
