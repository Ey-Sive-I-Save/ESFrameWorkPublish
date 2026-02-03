# 状态过渡系统指南

## 概述

状态过渡系统提供了状态间平滑切换的功能，支持自动转换、条件判断和自定义过渡曲线。

## 核心组件

### 1. StateTransition（状态转换规则）

定义从当前状态到目标状态的转换规则。

```csharp
public class StateTransition
{
    public int targetStateId;                    // 目标状态ID
    public List<StateCondition> conditions;      // 转换条件列表
    public float transitionTime = -1f;           // 转换时间（归一化，-1表示任意时间）
    public bool forceTransition = false;         // 是否强制转换（忽略代价限制）
}
```

**使用示例：**
```csharp
// 创建一个从Idle到Walk的转换
var transition = new StateTransition
{
    targetStateId = walkStateId,
    transitionTime = 0.8f,  // 在动画播放到80%时允许转换
    forceTransition = false
};

// 添加条件：速度大于0.1
transition.conditions.Add(new FloatCondition
{
    parameterName = "Speed",
    compareMode = CompareMode.Greater,
    threshold = 0.1f
});
```

### 2. StateTransitionConfig（过渡配置）

配置过渡动画的时长、曲线和自动转换规则。

```csharp
public class StateTransitionConfig
{
    public float transitionDuration = 0.3f;           // 过渡时长（秒）
    public TransitionMode transitionMode;             // 过渡模式（Blend/Crossfade）
    public AnimationCurve transitionCurve;            // 过渡曲线（预采样优化）
    public List<StateTransition> autoTransitions;     // 自动转换规则列表
}
```

**配置示例：**
```csharp
var transitionConfig = new StateTransitionConfig
{
    transitionDuration = 0.25f,  // 250ms过渡
    transitionMode = TransitionMode.Blend,
    transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1)
};

// 初始化（预采样曲线以提升性能）
transitionConfig.Initialize();
```

### 3. StateCondition（转换条件）

定义状态转换的判断条件。

**内置条件类型：**
- **FloatCondition**: 浮点数比较（大于、小于、等于）
- **BoolCondition**: 布尔值判断
- **IntCondition**: 整数比较
- **TimeCondition**: 时间条件（动画归一化时间）

**自定义条件示例：**
```csharp
public class CustomCondition : StateCondition
{
    public override bool Evaluate(StateMachineContext context)
    {
        // 自定义逻辑
        return context.IsGrounded && context.Speed > 0.5f;
    }
}
```

## 系统架构

### 过渡流程

```
┌──────────────┐
│ 当前状态     │
│ (Idle)       │
└──────┬───────┘
       │
       │ 1. 检查autoTransitions
       │ 2. 评估conditions
       │ 3. 检查transitionTime
       │
       ▼
┌──────────────┐
│ 满足条件？   │
└──────┬───────┘
       │ Yes
       ▼
┌──────────────┐
│ 开始过渡     │
│ - 权重插值   │
│ - 曲线采样   │
└──────┬───────┘
       │
       │ transitionDuration秒后
       │
       ▼
┌──────────────┐
│ 目标状态     │
│ (Walk)       │
└──────────────┘
```

### 性能优化

1. **曲线预采样**：
   - 初始化时采样32个点，避免每帧Evaluate
   - 运行时使用Lerp在采样点间插值

2. **条件缓存**：
   - 条件评估结果可缓存1帧
   - 避免重复计算相同条件

3. **零GC设计**：
   - 使用对象池管理过渡实例
   - 避免每次转换创建新对象

## 使用示例

### 示例1：简单Idle→Walk转换

```csharp
// 在StateSharedData中配置
state.stateSharedData.transitionConfig = new StateTransitionConfig
{
    transitionDuration = 0.2f,
    transitionMode = TransitionMode.Blend
};

// 添加自动转换规则
var idleToWalk = new StateTransition
{
    targetStateId = walkStateId,
    transitionTime = -1f  // 任意时间可转换
};
idleToWalk.conditions.Add(new FloatCondition
{
    parameterName = "Speed",
    compareMode = CompareMode.Greater,
    threshold = 0.1f
});

state.stateSharedData.transitionConfig.autoTransitions.Add(idleToWalk);
```

### 示例2：Walk→Run转换（带时间限制）

```csharp
var walkToRun = new StateTransition
{
    targetStateId = runStateId,
    transitionTime = 0.5f,  // 走路动画播放到50%后才允许跑步
    forceTransition = false
};

// 条件：按住冲刺键且速度足够
walkToRun.conditions.Add(new FloatCondition
{
    parameterName = "IsSprintKeyPressed",
    compareMode = CompareMode.Equal,
    threshold = 1.0f
});
walkToRun.conditions.Add(new FloatCondition
{
    parameterName = "Speed",
    compareMode = CompareMode.Greater,
    threshold = 0.8f
});

walkState.stateSharedData.transitionConfig.autoTransitions.Add(walkToRun);
```

### 示例3：动画播放完毕自动退出

```csharp
// 配置状态为"按动画结束"模式
state.stateSharedData.basicConfig.durationMode = StateDurationMode.UntilAnimationEnd;

// 或使用定时模式
state.stateSharedData.basicConfig.durationMode = StateDurationMode.Timed;
state.stateSharedData.basicConfig.timedDuration = 2.0f;  // 2秒后自动退出
```

### 示例4：表情动画（直接混合，无淡入淡出）

```csharp
// 启用直接混合模式
emotionState.stateSharedData.basicConfig.useDirectBlend = true;

// 这样表情动画会立即切换，适合UI反馈、表情等即时响应场景
```

## API参考

### StateMachine相关方法

```csharp
// 手动触发状态转换
public bool TryTransitionTo(int targetStateId, StatePipelineType pipeline);

// 强制转换（忽略条件检查）
public bool ForceTransitionTo(int targetStateId, StatePipelineType pipeline);

// 检查是否可以转换到目标状态
public bool CanTransitionTo(int targetStateId);
```

### StateBase相关方法

```csharp
// 检查当前状态是否应该自动退出
public virtual bool ShouldAutoExit(float currentTime);

// 获取当前动画归一化时间（用于transitionTime判断）
public virtual float GetNormalizedTime();
```

## 最佳实践

### 1. 过渡时长设置

- **短距离移动**（Idle↔Walk）: 0.1-0.2秒
- **速度变化**（Walk↔Run）: 0.2-0.3秒
- **姿态切换**（站立↔蹲伏）: 0.3-0.5秒
- **战斗动作**（攻击、翻滚）: 0.1-0.15秒（要求快速响应）

### 2. 条件设计原则

- **互斥性**：确保不同转换的条件互斥，避免冲突
- **优先级**：重要转换（如受击）使用`forceTransition`
- **阈值调优**：速度阈值不要设置过低（建议>0.1），避免抖动

### 3. 性能考虑

- **限制转换数量**：每个状态的autoTransitions不超过5个
- **简化条件**：避免复杂条件计算，优先使用简单比较
- **预初始化**：在游戏开始时调用`transitionConfig.Initialize()`

### 4. 调试技巧

```csharp
// 在StateMachine中启用转换日志
stateMachine.logStateTransitions = true;

// 输出格式：
// [Transition] Idle -> Walk (条件满足: Speed=0.5 > 0.1)
```

## 待实现功能

当前已完成基础架构，以下功能可按需实现：

- [ ] **自动转换检测**：在UpdateStateMachine中检查autoTransitions
- [ ] **权重插值系统**：平滑权重过渡（当前是瞬时切换）
- [ ] **转换事件回调**：OnTransitionStart/OnTransitionEnd
- [ ] **转换打断机制**：允许高优先级转换打断低优先级过渡
- [ ] **条件组合器**：支持AND/OR逻辑组合多个条件

## 相关文档

- [StateBasicConfig配置指南](./ANIMATION_FLYWEIGHT_DATA_INVENTORY.md#statebasicconfig)
- [动画混合系统](./2D_BLEND_TREE_USAGE_GUIDE.md)
- [状态机架构](./PLAYABLE_STATE_MACHINE_GUIDE.md)
