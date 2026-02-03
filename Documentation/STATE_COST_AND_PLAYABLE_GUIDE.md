# 状态代价系统与Playable热插拔指南

## 一、代价权重评估结果

### ❌ 代价权重（Vector3 costWeights）**已移除**

**原因分析：**
1. **复杂度过高**：权重配置增加了使用难度，用户需要同时配置代价值和权重值
2. **不符合实际需求**：实际的代价系统需要的是"Channel重合度累加"，而不是"加权计算"
3. **性能无明显提升**：权重计算并不能带来实质性的性能优化

**新方案：简单直接的总代价**
```csharp
// ✅ 新设计：直接使用总代价
public float GetTotalCost() => motionCost + agilityCost + targetCost;

// 代价范围：0-300（三个维度各100）
// 使用场景：判断状态优先级、检查是否可以合并
```

---

## 二、基于Channel重合度的合并系统

### 核心思想
**当两个状态的Channel有重合时，累加它们的代价到总代价池中，如果总代价不超过1.0，则可以合并**

### 实现逻辑

```csharp
/// <summary>
/// 检查流水线中的状态是否可以与新状态合并
/// </summary>
private bool CanMergeByChannelOverlap(StatePipelineRuntime pipeline, StateBase incomingState)
{
    float totalOverlapCost = 0f;

    // 遍历流水线中的所有状态
    foreach (var existingState in pipeline.runningStates)
    {
        // 检查Channel是否有重合
        StateChannelMask overlap = 
            existingState.stateSharedData.mergeData.stateChannelMask & 
            incomingState.stateSharedData.mergeData.stateChannelMask;
        
        if (overlap != StateChannelMask.None)
        {
            // 有重合，累加代价
            totalOverlapCost += existingState.stateSharedData.costData.GetTotalCost();
        }
    }

    // 加上新状态的代价
    totalOverlapCost += incomingState.stateSharedData.costData.GetTotalCost();

    // 如果总代价不超过1，则可以合并
    return totalOverlapCost <= 1.0f;
}
```

### 使用示例

```csharp
// 示例1：可以合并的状态
var idleState = new IdleState();
idleState.stateSharedData = new StateSharedData
{
    costData = new StateCostData
    {
        motionCost = 0.2f,      // 低动向
        agilityCost = 0.1f,     // 低灵活
        targetCost = 0f,        // 无目标
        enableCostCalculation = true
    },
    mergeData = new StateMergeData
    {
        stateChannelMask = StateChannelMask.LowerBody  // 只占用下半身
    }
};

var upperAttackState = new AttackState();
upperAttackState.stateSharedData = new StateSharedData
{
    costData = new StateCostData
    {
        motionCost = 0.5f,      // 中等动向
        agilityCost = 0.2f,     // 低灵活
        targetCost = 0f,        // 无目标
        enableCostCalculation = true
    },
    mergeData = new StateMergeData
    {
        stateChannelMask = StateChannelMask.UpperBody  // 只占用上半身
    }
};

// ✅ 可以合并：没有Channel重合，总代价 = 0.3 + 0.7 = 1.0（临界）
// 或者有重合但总代价 < 1.0

// 示例2：不能合并的状态
var fullBodyAttack = new AttackState();
fullBodyAttack.stateSharedData = new StateSharedData
{
    costData = new StateCostData
    {
        motionCost = 0.8f,
        agilityCost = 0.3f,
        targetCost = 0f,
        enableCostCalculation = true
    },
    mergeData = new StateMergeData
    {
        stateChannelMask = StateChannelMask.FullBody  // 全身
    }
};

// ❌ 不能合并：与idleState的LowerBody有重合
// 总代价 = 0.3(idle) + 1.1(fullBodyAttack) = 1.4 > 1.0
```

### 代价配置建议

| 状态类型 | motionCost | agilityCost | targetCost | 说明 |
|---------|------------|-------------|------------|------|
| 待机 | 0.1-0.2 | 0.05-0.1 | 0 | 最低代价 |
| 行走 | 0.3-0.4 | 0.2-0.3 | 0-0.1 | 低代价 |
| 攻击（轻） | 0.5-0.6 | 0.2-0.3 | 0.1-0.2 | 中等代价 |
| 攻击（重） | 0.8-0.9 | 0.4-0.5 | 0.2-0.3 | 高代价 |
| 技能 | 0.7-0.9 | 0.3-0.5 | 0.3-0.5 | 高代价 |

**注意：总代价应该根据Channel占用情况设计，占用越多代价越高**

---

## 三、hasAnimation标记

### 为什么需要hasAnimation？

**问题：** 不是所有状态都有动画，纯逻辑状态（如Buff、标记状态）不需要加入Playable图

**解决方案：** 在SharedData中添加`hasAnimation`标记

### 配置方式

```csharp
// 有动画的状态
state.stateSharedData = new StateSharedData
{
    hasAnimation = true,  // ✅ 会被加入Playable图
    animationConfig = new StateAnimationConfigData
    {
        // 动画配置...
    }
};

// 纯逻辑状态
buffState.stateSharedData = new StateSharedData
{
    hasAnimation = false,  // ❌ 不会加入Playable图
    // 不需要animationConfig
};
```

### 性能优化

```csharp
// 在ExecuteStateActivation中自动判断
if (targetState.stateSharedData?.hasAnimation == true)
{
    HotPlugStateToPlayable(targetState, pipeline);  // 只插入有动画的状态
}
```

---

## 四、Playable热插拔实现详解

### 架构概览

```
StateMachine (PlayableGraph)
    └─ RootMixer (流水线总混合器)
        ├─ BasicPipeline.mixer (基础流水线混合器)
        │   ├─ State1.playable (动态插入)
        │   ├─ State2.playable (动态插入)
        │   └─ ...
        ├─ MainPipeline.mixer (主流水线混合器)
        │   ├─ State3.playable (动态插入)
        │   └─ ...
        └─ BuffPipeline.mixer (Buff流水线混合器)
            └─ State4.playable (动态插入)
```

### 热插拔核心方法

#### 1. 插入Playable（HotPlugStateToPlayable）

```csharp
private void HotPlugStateToPlayable(StateBase state, StatePipelineRuntime pipeline)
{
    // 1. 检查是否有动画
    if (state.stateSharedData?.hasAnimation != true)
    {
        return;  // 没有动画，跳过
    }

    // 2. 创建状态的Playable节点
    var statePlayable = CreateStatePlayable(state, state.stateSharedData.animationConfig);
    if (!statePlayable.IsValid())
    {
        return;
    }

    // 3. 连接到流水线Mixer
    int inputIndex = pipeline.mixer.GetInputCount();
    pipeline.mixer.SetInputCount(inputIndex + 1);  // 动态扩展输入
    playableGraph.Connect(statePlayable, 0, pipeline.mixer, inputIndex);
    pipeline.mixer.SetInputWeight(inputIndex, 1.0f);

    // 4. 记录索引（用于后续卸载）
    // state.playableInputIndex = inputIndex;
}
```

#### 2. 卸载Playable（HotUnplugStateFromPlayable）

```csharp
private void HotUnplugStateFromPlayable(StateBase state, StatePipelineRuntime pipeline)
{
    // 1. 只卸载有动画的状态
    if (state.stateSharedData?.hasAnimation != true)
    {
        return;
    }

    // 2. 断开连接
    if (pipeline.mixer.IsValid())
    {
        // playableGraph.Disconnect(pipeline.mixer, state.playableInputIndex);
        // 注：需要在StateBase中维护playableInputIndex字段
    }
}
```

### 调用时机

```csharp
// 状态激活时 - ExecuteStateActivation()
targetState.OnStateEnter();
runningStates.Add(targetState);
pipeline.runningStates.Add(targetState);
HotPlugStateToPlayable(targetState, pipeline);  // ✅ 插入

// 状态停用时 - DeactivateState()
HotUnplugStateFromPlayable(state, pipelineData);  // ✅ 卸载
state.OnStateExit();
runningStates.Remove(state);
```

### 扩展点：CreateStatePlayable

**允许子类重写以支持不同类型的动画：**

```csharp
// 在StateMachine子类中重写
protected override Playable CreateStatePlayable(StateBase state, StateAnimationConfigData animConfig)
{
    if (animConfig.animationClip != null)
    {
        // 单个Clip
        return AnimationClipPlayable.Create(playableGraph, animConfig.animationClip);
    }
    else if (animConfig.blendTree != null)
    {
        // 混合树
        var mixer = AnimationMixerPlayable.Create(playableGraph, animConfig.blendTree.clips.Length);
        for (int i = 0; i < animConfig.blendTree.clips.Length; i++)
        {
            var clipPlayable = AnimationClipPlayable.Create(playableGraph, animConfig.blendTree.clips[i]);
            playableGraph.Connect(clipPlayable, 0, mixer, i);
            mixer.SetInputWeight(i, animConfig.blendTree.weights[i]);
        }
        return mixer;
    }
    
    return Playable.Null;
}
```

---

## 五、完整使用示例

```csharp
// 1. 创建状态机
var stateMachine = new StateMachine();
stateMachine.Initialize(entity, animator);

// 2. 创建状态 - 有动画的攻击状态
var attackState = new AttackState();
attackState.stateSharedData = new StateSharedData
{
    hasAnimation = true,
    animationConfig = new StateAnimationConfigData
    {
        animationClip = attackClip
    },
    costData = new StateCostData
    {
        motionCost = 0.8f,
        agilityCost = 0.4f,
        targetCost = 0.2f,
        enableCostCalculation = true
    },
    mergeData = new StateMergeData
    {
        stateChannelMask = StateChannelMask.UpperBody | StateChannelMask.Target
    }
};

// 3. 创建状态 - 无动画的Buff状态
var buffState = new BuffState();
buffState.stateSharedData = new StateSharedData
{
    hasAnimation = false,  // ✅ 纯逻辑状态，不加入Playable
    costData = new StateCostData
    {
        motionCost = 0f,
        agilityCost = 0f,
        targetCost = 0f,
        enableCostCalculation = false  // 不参与代价计算
    }
};

// 4. 注册状态
stateMachine.RegisterState("attack", attackState, StatePipelineType.Main);
stateMachine.RegisterState("buff", buffState, StatePipelineType.Buff);

// 5. 运行时激活 - 自动热插拔
stateMachine.TryActivateState("attack", StatePipelineType.Main);
// ✅ attackState会被自动插入MainPipeline.mixer

stateMachine.TryActivateState("buff", StatePipelineType.Buff);
// ✅ buffState不会插入Playable图（hasAnimation=false）

// 6. 运行时停用 - 自动卸载
stateMachine.TryDeactivateState("attack");
// ✅ 自动从MainPipeline.mixer卸载
```

---

## 六、性能对比

| 特性 | 优化前 | 优化后 |
|------|--------|--------|
| 代价计算 | 3次乘法 + 2次加法 | 2次加法 |
| 代价配置 | 6个参数（3代价+3权重） | 3个参数（3代价） |
| Playable插入 | 无选择性（全插入） | 按需插入（hasAnimation） |
| Channel重合判断 | 无 | ✅ 位运算判断 |

---

## 七、最佳实践

### ✅ DO
1. **合理设置hasAnimation**：纯逻辑状态设为false
2. **代价值与Channel对应**：占用Channel越多，代价应越高
3. **使用Channel重合判断**：避免冲突状态同时运行
4. **合理配置总代价**：确保可合并的状态总代价 ≤ 1.0

### ❌ DON'T
1. **不要为纯逻辑状态创建动画配置**
2. **不要将所有状态的代价设为相同值**
3. **不要忽略Channel配置**
4. **不要让重要状态的代价过低**

---

## 八、FAQ

**Q: 为什么总代价要限制在1.0以下？**
A: 1.0是一个归一化的阈值，表示"系统容量"。超过1.0意味着系统过载，需要打断某些状态。

**Q: hasAnimation和animationConfig的关系？**
A: hasAnimation是快速判断标记，animationConfig是详细配置。只有hasAnimation=true时才会读取animationConfig。

**Q: 如何实现动画平滑过渡？**
A: 在CreateStatePlayable中配置AnimationClipPlayable的淡入淡出时间，或使用AnimationMixerPlayable的权重插值。

**Q: Playable热插拔的性能开销如何？**
A: 非常低。只在状态激活/停用时执行一次，不影响运行时性能。相比预创建所有Playable，内存占用大幅降低。
