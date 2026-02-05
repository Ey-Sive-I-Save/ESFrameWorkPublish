# 动画系统全面改进方案

> **日期：** 2026年2月4日  
> **目标：** 增强事件系统、添加实用计算器、改进临时状态、分析Animancer

---

## 📋 改进清单

### 1. ✅ AnimationClipConfig包装类
**文件：** AnimationClipConfig.cs  
**功能：**
- 包裹Clip及其扩展参数（speed、overrideKey、triggerEvents）
- 支持事件触发点配置（TriggerEventAt）
- 用于所有计算器（除OriginalSimple和多向混合）

### 2. ✅ 事件触发系统
**核心类：** TriggerEventAt  
**功能：**
- 归一化时间点触发事件（0-1）
- 支持事件名称和参数
- 支持仅触发一次或循环触发
- 自动重置机制

### 3. 🔧 临时状态增强
**改进点：**
- 支持播放一次自动退出（已实现）
- 支持循环播放（已实现）
- 添加播放完成回调

### 4. 🔧 严格运行时间更新
**StateBase改进：**
- 严格更新hasEnterTime
- 准确计算normalizedProgress
- 确保事件触发在正确时间点

### 5. 📊 新增实用计算器
即将添加：
- RandomClipCalculator：随机播放
- WeightedRandomCalculator：权重随机
- TimelineCalculator：时间线控制
- LayeredCalculator：分层混合
- AdditiveCalculator：叠加动画

---

## 🎯 实现细节

### AnimationClipConfig 使用示例

```csharp
// 创建配置
var config = new AnimationClipConfig
{
    clip = attackClip,
    speed = 1.2f,
    overrideKey = "attack_override",
    triggerEvents = new List<TriggerEventAt>
    {
        new TriggerEventAt
        {
            normalizedTime = 0.3f,
            eventName = "OnHitFrame",
            eventParam = "damage:50",
            triggerOnce = true
        },
        new TriggerEventAt
        {
            normalizedTime = 0.8f,
            eventName = "OnRecoveryStart",
            triggerOnce = true
        }
    }
};
```

### 事件触发检测

```csharp
// StateBase中添加
private float _lastNormalizedProgress = 0f;

private void CheckEventTriggers()
{
    if (clipConfig == null || clipConfig.triggerEvents.Count == 0)
        return;

    foreach (var evt in clipConfig.triggerEvents)
    {
        // 检测是否穿过触发点
        bool crossedTriggerPoint = false;
        
        if (_lastNormalizedProgress < evt.normalizedTime && 
            normalizedProgress >= evt.normalizedTime)
        {
            crossedTriggerPoint = true;
        }
        
        // 处理循环情况（从1回到0）
        if (_lastNormalizedProgress > normalizedProgress)
        {
            evt.ResetTrigger(); // 新循环，重置触发标记
            
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

// 事件回调
protected virtual void OnAnimationEvent(string eventName, string eventParam)
{
    StateMachineDebugSettings.Global.LogStateTransition(
        $"[AnimEvent] {eventName} | Param: {eventParam}");
    
    // 可以添加到StateMachine的事件系统
    host?.BroadcastAnimationEvent(this, eventName, eventParam);
}
```

---

## 🎮 新增计算器设计

### 1. RandomClipCalculator - 随机播放

```csharp
[Serializable, TypeRegistryItem("随机Clip播放器")]
public class StateAnimationMixCalculatorForRandomClip : StateAnimationMixCalculator
{
    [LabelText("Clip列表")]
    public List<AnimationClipConfig> clips = new List<AnimationClipConfig>();
    
    [LabelText("随机种子")]
    public int randomSeed = 0;
    
    [LabelText("避免重复")]
    [Tooltip("避免连续播放同一个Clip")]
    public bool avoidRepeat = true;
    
    // 运行时数据
    private int _lastClipIndex = -1;
    
    public override bool InitializeRuntime(AnimationCalculatorRuntime runtime, 
        PlayableGraph graph, ref Playable output)
    {
        if (clips.Count == 0)
            return false;
            
        // 随机选择一个Clip
        int randomIndex = GetRandomClipIndex();
        var selectedConfig = clips[randomIndex];
        
        runtime.singlePlayable = AnimationClipPlayable.Create(graph, selectedConfig.clip);
        runtime.singlePlayable.SetSpeed(selectedConfig.speed);
        
        output = runtime.singlePlayable;
        runtime.IsInitialized = true;
        return true;
    }
    
    private int GetRandomClipIndex()
    {
        var random = new System.Random(randomSeed != 0 ? randomSeed : (int)Time.time);
        int index;
        
        do
        {
            index = random.Next(0, clips.Count);
        } while (avoidRepeat && index == _lastClipIndex && clips.Count > 1);
        
        _lastClipIndex = index;
        return index;
    }
}
```

### 2. WeightedRandomCalculator - 权重随机

```csharp
[Serializable, TypeRegistryItem("权重随机播放器")]
public class StateAnimationMixCalculatorForWeightedRandom : StateAnimationMixCalculator
{
    [Serializable]
    public struct WeightedClip
    {
        public AnimationClipConfig config;
        [Range(0f, 10f)]
        public float weight;
    }
    
    [LabelText("Clip列表")]
    public List<WeightedClip> clips = new List<WeightedClip>();
    
    // 根据权重随机选择
    private int GetWeightedRandomIndex()
    {
        float totalWeight = 0f;
        foreach (var clip in clips)
            totalWeight += clip.weight;
        
        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        float累积Weight = 0f;
        
        for (int i = 0; i < clips.Count; i++)
        {
            累积Weight += clips[i].weight;
            if (randomValue <= 累积Weight)
                return i;
        }
        
        return clips.Count - 1;
    }
}
```

### 3. TimelineCalculator - 时间线控制

```csharp
[Serializable, TypeRegistryItem("时间线播放器")]
public class StateAnimationMixCalculatorForTimeline : StateAnimationMixCalculator
{
    [Serializable]
    public struct TimelineClip
    {
        public AnimationClipConfig config;
        [LabelText("开始时间")]
        public float startTime;
        [LabelText("结束时间")]
        public float endTime;
        [LabelText("混合方式")]
        public BlendMode blendMode;
    }
    
    public enum BlendMode
    {
        Override,   // 覆盖
        Additive,   // 叠加
        Layered     // 分层
    }
    
    [LabelText("时间线Clips")]
    public List<TimelineClip> timelineClips = new List<TimelineClip>();
    
    [LabelText("总时长")]
    public float totalDuration = 10f;
    
    public override void UpdateWeights(AnimationCalculatorRuntime runtime, 
        in StateMachineContext context, float deltaTime)
    {
        float currentTime = context.GetFloat("TimelineTime", 0f);
        
        // 计算每个Clip的权重
        for (int i = 0; i < timelineClips.Count; i++)
        {
            var timelineClip = timelineClips[i];
            float weight = 0f;
            
            if (currentTime >= timelineClip.startTime && 
                currentTime <= timelineClip.endTime)
            {
                // 计算淡入淡出
                float fadeInDuration = 0.2f;
                float fadeOutDuration = 0.2f;
                
                if (currentTime < timelineClip.startTime + fadeInDuration)
                {
                    weight = (currentTime - timelineClip.startTime) / fadeInDuration;
                }
                else if (currentTime > timelineClip.endTime - fadeOutDuration)
                {
                    weight = (timelineClip.endTime - currentTime) / fadeOutDuration;
                }
                else
                {
                    weight = 1f;
                }
            }
            
            runtime.mixer.SetInputWeight(i, weight);
        }
    }
}
```

### 4. LayeredCalculator - 分层混合

```csharp
[Serializable, TypeRegistryItem("分层混合播放器")]
public class StateAnimationMixCalculatorForLayered : StateAnimationMixCalculator
{
    [Serializable]
    public struct LayerClip
    {
        public AnimationClipConfig config;
        [LabelText("层权重参数")]
        public StateParameter weightParameter;
        [LabelText("Avatar遮罩")]
        public AvatarMask avatarMask;
    }
    
    [LabelText("层列表")]
    public List<LayerClip> layers = new List<LayerClip>();
    
    public override void UpdateWeights(AnimationCalculatorRuntime runtime, 
        in StateMachineContext context, float deltaTime)
    {
        // 每层独立权重控制
        for (int i = 0; i < layers.Count; i++)
        {
            float weight = context.GetFloat(layers[i].weightParameter, 0f);
            runtime.mixer.SetInputWeight(i, weight);
        }
    }
}
```

### 5. AdditiveCalculator - 叠加动画

```csharp
[Serializable, TypeRegistryItem("叠加动画播放器")]
public class StateAnimationMixCalculatorForAdditive : StateAnimationMixCalculator
{
    [LabelText("基础动画")]
    public AnimationClipConfig baseClip;
    
    [LabelText("叠加动画列表")]
    public List<AnimationClipConfig> additiveClips = new List<AnimationClipConfig>();
    
    [LabelText("叠加强度参数")]
    public List<StateParameter> additiveWeightParameters = new List<StateParameter>();
    
    public override bool InitializeRuntime(AnimationCalculatorRuntime runtime, 
        PlayableGraph graph, ref Playable output)
    {
        // 创建LayerMixer
        runtime.mixer = AnimationLayerMixerPlayable.Create(graph, additiveClips.Count + 1);
        
        // 基础层（权重1）
        var basePlayable = AnimationClipPlayable.Create(graph, baseClip.clip);
        graph.Connect(basePlayable, 0, runtime.mixer, 0);
        runtime.mixer.SetInputWeight(0, 1f);
        runtime.mixer.SetLayerAdditive(0, false);
        
        // 叠加层
        for (int i = 0; i < additiveClips.Count; i++)
        {
            var additivePlayable = AnimationClipPlayable.Create(graph, additiveClips[i].clip);
            graph.Connect(additivePlayable, 0, runtime.mixer, i + 1);
            runtime.mixer.SetInputWeight(i + 1, 0f);
            runtime.mixer.SetLayerAdditive(i + 1, true); // 设置为叠加模式
        }
        
        output = runtime.mixer;
        return true;
    }
    
    public override void UpdateWeights(AnimationCalculatorRuntime runtime, 
        in StateMachineContext context, float deltaTime)
    {
        // 基础层始终为1
        runtime.mixer.SetInputWeight(0, 1f);
        
        // 更新叠加层权重
        for (int i = 0; i < additiveWeightParameters.Count && i < additiveClips.Count; i++)
        {
            float weight = context.GetFloat(additiveWeightParameters[i], 0f);
            runtime.mixer.SetInputWeight(i + 1, weight);
        }
    }
}
```

---

## 🔍 Animancer 深度分析

### 核心功能拆解

#### 1. **状态管理系统**
```csharp
// Animancer.AnimancerState
public class AnimancerState
{
    public float Time { get; set; }          // 当前时间
    public float Speed { get; set; }         // 播放速度
    public float Weight { get; set; }        // 混合权重
    public AnimationClip Clip { get; }       // 动画Clip
    
    // 关键：平滑权重过渡
    public void FadeTo(float targetWeight, float fadeDuration)
    {
        // 使用协程或Update循环平滑过渡
    }
}
```

**ES实现对比：**
```csharp
// ES.StateBase
public class StateBase
{
    public float hasEnterTime;               // ≈ Time
    public StateSharedData sharedData;       // 包含Clip
    // ES通过权重缓存实现平滑过渡
    runtime.weightCache[i]                   // ≈ Weight
}
```

#### 2. **转换系统（Transitions）**
```csharp
// Animancer核心：声明式转换
state.AddTransition(targetState, condition);

// 示例
idleState.AddTransition(walkState, () => input.magnitude > 0.1f);
```

**为何泛用性强：**
- ✅ **声明式**：配置转换规则，不写if-else
- ✅ **可视化**：Inspector中直接配置
- ✅ **可复用**：转换规则独立于状态

**ES可改进方向：**
```csharp
// 建议添加
public class StateTransition
{
    public StateBase fromState;
    public StateBase toState;
    public Func<bool> condition;
    public float transitionDuration;
}
```

#### 3. **混合树系统（Mixers）**
```csharp
// Animancer.LinearMixerState
public class LinearMixerState : AnimancerState
{
    public float Parameter { get; set; }
    
    // 自动计算权重
    public void UpdateWeights()
    {
        // 根据Parameter自动插值
        for (int i = 0; i < clips.Length; i++)
        {
            float weight = CalculateWeight(Parameter, thresholds[i]);
            SetChildWeight(i, weight);
        }
    }
}
```

**ES已实现：**
```csharp
// ES.StateAnimationMixCalculatorForBlendTree1D
public class StateAnimationMixCalculatorForBlendTree1D
{
    public StateParameter parameterFloat;
    public ClipSampleForBlend1D[] samples;
    
    // 类似实现
    public override void UpdateWeights(...)
    {
        float input = context.GetFloat(parameterFloat);
        // 计算权重...
    }
}
```

#### 4. **事件系统（Events）**
```csharp
// Animancer.AnimancerEvent
state.Events.Add(0.5f, () => PlayFootstepSound());
state.Events.OnEnd = () => ReturnToIdle();
```

**为何好用：**
- ✅ 归一化时间（0-1）
- ✅ Lambda表达式
- ✅ 自动触发

**ES需要添加：**
```csharp
// 建议实现
public class StateAnimationEvents
{
    public List<AnimationEvent> events;
    
    public void AddEvent(float normalizedTime, Action callback)
    {
        events.Add(new AnimationEvent { time = normalizedTime, callback = callback });
    }
}
```

#### 5. **分层系统（Layers）**
```csharp
// Animancer支持多层
var upperBodyLayer = animancer.Layers[1];
var lowerBodyLayer = animancer.Layers[0];

upperBodyLayer.SetMask(upperBodyMask);
upperBodyLayer.Play(reloadClip);
lowerBodyLayer.Play(walkClip);
```

**泛用性强的原因：**
- ✅ 每层独立状态机
- ✅ 每层独立遮罩
- ✅ 每层独立权重

**ES对应：**
```csharp
// ES的流水线系统
StatePipelineType.Basic   // ≈ Layer 0
StatePipelineType.Main     // ≈ Layer 1
StatePipelineType.Buff     // ≈ Layer 2
```

---

### Animancer泛用性的核心原理

#### 1. **零配置开箱即用**
```csharp
// Animancer：1行代码播放动画
animancer.Play(clip);

// Unity Animator：需要创建Controller、State、Transition...
```

#### 2. **运行时完全控制**
```csharp
// 所有参数运行时可改
state.Speed = 2f;
state.Time = 0.5f;
state.Weight = 0.8f;

// Animator：很多参数烘焙在Controller中
```

#### 3. **类型安全**
```csharp
// Animancer：强类型
AnimancerState walkState = animancer.Play(walkClip);

// Animator：字符串参数
animator.SetBool("IsWalking", true); // 容易拼写错误
```

#### 4. **性能优化**
```csharp
// Animancer：零GC
private AnimancerState _cachedWalkState;

void Start()
{
    _cachedWalkState = animancer.Play(walkClip);
}

void Update()
{
    _cachedWalkState.Weight = input.magnitude; // 零GC
}
```

#### 5. **可扩展架构**
```csharp
// 自定义State
public class MyCustomState : AnimancerState
{
    // 添加自定义逻辑
}

// 自定义Mixer
public class MyCustomMixer : MixerState
{
    // 自定义混合逻辑
}
```

---

### ES vs Animancer 对比

| 特性 | Animancer | ES State System | 优劣 |
|------|-----------|-----------------|------|
| **状态管理** | AnimancerState | StateBase | 相似，ES更复杂 |
| **混合树** | LinearMixer, 2DMixer | BlendTree1D, BlendTree2D | ES更详细 |
| **转换系统** | ✅ 声明式转换 | ❌ 缺少 | Animancer胜 |
| **事件系统** | ✅ AnimancerEvent | ⚠️ 部分实现 | Animancer胜 |
| **分层系统** | ✅ Layers | ✅ Pipelines | 功能相似 |
| **零GC** | ✅ 完全零GC | ✅ 大部分零GC | 相当 |
| **运行时控制** | ✅ 完全控制 | ✅ 完全控制 | 相当 |
| **学习曲线** | 低 | 中等 | Animancer胜 |

---

### 建议ES改进方向

#### 1. **添加声明式转换**
```csharp
stateA.AddTransition(stateB)
    .When(() => input.magnitude > 0.1f)
    .WithDuration(0.2f);
```

#### 2. **完善事件系统**
```csharp
state.Events.Add(0.3f, OnHitFrame);
state.Events.OnEnd(OnAnimationEnd);
```

#### 3. **简化API**
```csharp
// 当前ES
stateMachine.TryActivateState("Walk");

// 可简化为
stateMachine.Play("Walk");
```

#### 4. **增强调试**
```csharp
// 运行时Inspector显示
- 当前播放状态
- 权重实时数值
- 转换状态
- 事件触发点
```

---

## 📊 总结

### Animancer强大的核心
1. ✅ **简单API** - 一行代码播放动画
2. ✅ **运行时控制** - 所有参数可动态修改
3. ✅ **零GC设计** - 高性能无垃圾
4. ✅ **声明式转换** - 配置而非代码
5. ✅ **完善事件** - 归一化时间事件
6. ✅ **可扩展** - 自定义State和Mixer

### ES的优势
1. ✅ **多流水线** - 3条独立流水线并行
2. ✅ **Fallback机制** - 5通道Fallback
3. ✅ **详细配置** - 更细粒度的控制
4. ✅ **状态合并** - 通道占用和冲突处理
5. ✅ **对象池** - 内存管理更优

### 建议整合
- 引入Animancer的简单API和声明式转换
- 保留ES的多流水线和Fallback机制
- 完善事件系统，参考Animancer设计
- 简化使用方式，降低学习曲线
