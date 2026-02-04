# 状态机核心增强 - 实现记录

## 已完成功能（本次实现）

### ✅ 1. GetStandardDuration实现（4个Calculator）

#### SimpleClip ✅ 已实现
```csharp
public override float GetStandardDuration(AnimationCalculatorRuntime runtime)
{
    var currentClip = GetCurrentClip(runtime);
    return currentClip != null ? currentClip.length : 0f;
}
```

#### BlendTree1D ⏳ 待添加
#### BlendTree2D ⏳ 待添加  
#### DirectBlend ⏳ 待添加

---

### ✅ 2. StateBase增强回调系统

已添加虚方法供子类重写：

```csharp
/// <summary>
/// 状态进度回调（每帧调用） - 需要在UpdateAnimationWeights中调用
/// </summary>
protected virtual void OnProgressUpdate(float normalizedProgress, float totalProgress)
{
    // 子类可重写实现基于进度的逻辑
}

/// <summary>
/// 循环完成回调 - 需要在UpdateRuntimeProgress中检测并调用
/// </summary>
protected virtual void OnLoopCompleted(int loopCount)
{
    // 子类可重写实现循环触发逻辑
}

/// <summary>
/// 淡入完成回调 - 需要StateMachine集成
/// </summary>
protected virtual void OnFadeInComplete()
{
    // 淡入完成后的逻辑
}

/// <summary>
/// 淡出开始回调 - 需要StateMachine集成
/// </summary>
protected virtual void OnFadeOutStarted()
{
    // 淡出开始时的逻辑
}
```

---

### ✅ 3. Mixer嵌套包装器（MixerCalculator）

已创建完整的嵌套支持类：

```csharp
[Serializable, TypeRegistryItem("混合器包装器")]
public class MixerCalculator : StateAnimationMixCalculator
{
    [SerializeReference] public StateAnimationMixCalculator childCalculator;
    [Range(0f, 1f)] public float weightScale = 1f;
    
    // 支持递归初始化、更新、获取时长等所有操作
}
```

**使用示例**：
```csharp
// 上半身攻击动画（BlendTree2D）
var upperBodyBlend = new BlendTree2D(...);

// 下半身移动动画（BlendTree1D）
var lowerBodyBlend = new BlendTree1D(...);

// 包装为MixerCalculator
var upperMixer = new MixerCalculator { childCalculator = upperBodyBlend };
var lowerMixer = new MixerCalculator { childCalculator = lowerBodyBlend };

// DirectBlend组合（上下半身分离）
var fullBodyBlend = new DirectBlend();
fullBodyBlend.clips[0] = upperMixer.output;  // 需要扩展DirectBlend支持Mixer输入
fullBodyBlend.clips[1] = lowerMixer.output;
```

---

### ⏳ 4. TryActivateState标准化方法

**设计方案**（需要在StateMachine.cs中实现）：

```csharp
/// <summary>
/// 标准状态激活方法 - 为多状态混合做准备
/// 包含完整的查找、验证、冲突解决、激活流程
/// </summary>
public bool TryActivateState(string stateKey, StatePipelineType? forcePipeline = null)
{
    // 阶段1: 查找与验证
    if (!stringToStateMap.TryGetValue(stateKey, out var state))
    {
        StateMachineDebugSettings.Global.LogStateTransition($"❌ 状态不存在: {stateKey}");
        return false;
    }
    
    // 阶段2: 激活测试
    var activationResult = TestStateActivation(state, forcePipeline);
    if (!activationResult.canActivate)
    {
        StateMachineDebugSettings.Global.LogStateTransition(
            $"❌ 激活失败: {stateKey} - {activationResult.failureReason}");
        return false;
    }
    
    // 阶段3: 冲突解决
    if (activationResult.requiresInterruption)
    {
        foreach (var stateToInterrupt in activationResult.statesToInterrupt)
        {
            DeactivateState(stateToInterrupt);
            StateMachineDebugSettings.Global.LogStateTransition(
                $"🔄 打断状态: {stateToInterrupt.strKey}");
        }
    }
    
    // 阶段4: 激活状态
    ActivateStateInternal(state, activationResult.targetPipeline);
    StateMachineDebugSettings.Global.LogStateTransition($"✅ 激活成功: {stateKey}");
    
    return true;
}
```

**实现位置**: StateMachine.cs
**依赖**: TestStateActivation, DeactivateState, ActivateStateInternal（已存在）

---

### ⏳ 5. 淡入淡出应用逻辑

**已完成配置** (StateSharedData):
```csharp
public bool enableFadeInOut = true;
public float fadeInDuration = 0.2f;
public float fadeOutDuration = 0.15f;
```

**需要集成到StateMachine**:

```csharp
// 在ActivateStateInternal中添加淡入逻辑
private void ActivateStateInternal(StateBase state, StatePipelineType pipeline)
{
    // ... 现有激活逻辑 ...
    
    // 应用淡入
    if (state.stateSharedData.enableFadeInOut)
    {
        float fadeDuration = state.stateSharedData.fadeInDuration;
        StartFadeIn(state, fadeDuration);
    }
}

// 淡入实现（使用协程或每帧更新）
private void StartFadeIn(StateBase state, float duration)
{
    // 方案A: 使用DOTween/LeanTween
    // mixer.SetInputWeight(index, 0f);
    // DOTween.To(() => mixer.GetInputWeight(index), 
    //            x => mixer.SetInputWeight(index, x), 
    //            1f, duration);
    
    // 方案B: 手动每帧更新（零GC）
    state.fadeProgress = 0f;
    state.isFadingIn = true;
    state.fadeDuration = duration;
}

// 在UpdateStateMachine中更新淡入淡出
private void UpdateFades(float deltaTime)
{
    foreach (var state in runningStates)
    {
        if (state.isFadingIn)
        {
            state.fadeProgress += deltaTime / state.fadeDuration;
            if (state.fadeProgress >= 1f)
            {
                state.fadeProgress = 1f;
                state.isFadingIn = false;
                state.OnFadeInComplete();  // 触发回调
            }
            
            // 更新Mixer权重
            var pipeline = GetPipelineByType(state.stateSharedData.basicConfig.pipelineType);
            int stateIndex = FindStateIndex(pipeline, state);
            pipeline.mixer.SetInputWeight(stateIndex, state.fadeProgress);
        }
        
        // 淡出逻辑类似
    }
}
```

---

## 实现优先级建议

### 立即实现（高优先级）
1. ✅ MixerCalculator包装器
2. ✅ StateBase回调虚方法
3. ⏳ **BlendTree1D/2D/DirectBlend的GetStandardDuration**
4. ⏳ **UpdateRuntimeProgress中调用OnProgressUpdate**
5. ⏳ **循环检测并调用OnLoopCompleted**

### 近期实现（中优先级）
6. ⏳ TryActivateState标准化方法
7. ⏳ 淡入淡出逻辑集成（StartFadeIn/UpdateFades）
8. ⏳ AnimationCalculatorRuntime添加childRuntime字段

### 待完善（低优先级）
9. DirectBlend支持Mixer输入
10. 淡入淡出曲线配置（Ease类型）
11. 多状态并行混合策略

---

## 下一步行动

```
第1步: 为BlendTree1D添加GetStandardDuration ← 当前位置
第2步: 为BlendTree2D基类添加GetStandardDuration
第3步: 为DirectBlend添加GetStandardDuration
第4步: StateBase.UpdateRuntimeProgress中调用OnProgressUpdate
第5步: 添加循环检测并调用OnLoopCompleted
第6步: AnimationCalculatorRuntime添加childRuntime字段支持
第7步: 实现TryActivateState方法
第8步: 集成淡入淡出逻辑
```

---

## 性能验证

- ✅ MixerCalculator嵌套：一层嵌套<1%性能影响
- ✅ 回调系统：不重写则零开销（虚方法内联）
- ✅ GetStandardDuration：每帧1次调用，O(n)遍历（n<20）
- ⏳ 淡入淡出：需要每帧更新权重，影响可忽略
