# 走路/跑步运动系统设计文档

## 🎯 设计目标

实现清晰的走路(Walk)和跑步(Run)状态区分，支持：
- 基于速度阈值的自动状态切换
- 平滑的过渡动画
- 支持Idle → Walk → Run → Sprint多级运动
- 支持手动控制（如按住Shift强制跑步）

---

## 🏗️ 架构设计

### 1. 状态层级结构

```
LocomotionState (Root)
├── IdleState          # 静止状态 (Speed < 0.1)
├── WalkState          # 走路状态 (0.1 <= Speed < WalkThreshold)
├── RunState           # 跑步状态 (WalkThreshold <= Speed < RunThreshold)
└── SprintState        # 冲刺状态 (Speed >= RunThreshold)
```

### 2. 参数定义

#### 新增StateDefaultFloatParameter枚举

```csharp
public enum StateDefaultFloatParameter
{
    // ... 现有参数 ...
    
    // 运动阈值 (新增 8-15)
    WalkSpeedThreshold = 8,      // 走路速度阈值 (默认 2.0)
    RunSpeedThreshold = 9,       // 跑步速度阈值 (默认 5.0)
    SprintSpeedThreshold = 10,   // 冲刺速度阈值 (默认 8.0)
    
    // 运动状态 (新增 16-20)
    IsWalking = 11,              // 是否在走路 (0/1)
    IsRunning = 12,              // 是否在跑步 (0/1)
    IsSprinting = 13,            // 是否在冲刺 (0/1)
}
```

#### Context参数使用

- **Speed** - 当前移动速度（由BasicMoveRotateModule自动更新）
- **SpeedX** / **SpeedZ** - 方向速度分量
- **WalkSpeedThreshold** - 走路阈值（可配置）
- **RunSpeedThreshold** - 跑步阈值（可配置）
- **IsWalking** / **IsRunning** / **IsSprinting** - 状态标记

---

## 🔧 实现方案

### 方案1：使用BlendTree1D（推荐）

**优点**：
- 简单高效，利用现有计算器
- 自动平滑过渡
- 支持多个动画Clip (Idle/Walk/Run/Sprint)

**实现**：

```csharp
// 配置BlendTree1D
var locomotionCalculator = new StateAnimationMixCalculatorForBlendTree1D
{
    parameterFloat = StateDefaultFloatParameter.Speed,
    smoothTime = 0.15f,  // 平滑时间
    samples = new[]
    {
        new ClipSampleForBlend1D { clip = idleClip, threshold = 0f },
        new ClipSampleForBlend1D { clip = walkClip, threshold = 2f },  // 走路阈值
        new ClipSampleForBlend1D { clip = runClip, threshold = 5f },   // 跑步阈值
        new ClipSampleForBlend1D { clip = sprintClip, threshold = 8f } // 冲刺阈值
    }
};
```

### 方案2：使用Condition + 独立状态

**优点**：
- 明确的状态边界
- 可添加进入/退出逻辑
- 支持不同的代价和组件

**实现**：

#### Walk State
```csharp
var walkState = new StateDefinition
{
    stateName = "Walk",
    enterConditions = new List<StateCondition>
    {
        new StateCondition
        {
            parameterName = StateDefaultFloatParameter.Speed,
            conditionType = ConditionType.GreaterOrEqual,
            floatValue = 0.1f
        },
        new StateCondition
        {
            parameterName = StateDefaultFloatParameter.Speed,
            conditionType = ConditionType.Less,
            floatValue = 5.0f  // WalkSpeedThreshold
        }
    },
    displayComponent = new DisplayComponent
    {
        animationCalculator = new StateAnimationMixCalculatorForSimpleClip
        {
            clip = walkClip,
            speed = 1f
        }
    }
};
```

#### Run State
```csharp
var runState = new StateDefinition
{
    stateName = "Run",
    enterConditions = new List<StateCondition>
    {
        new StateCondition
        {
            parameterName = StateDefaultFloatParameter.Speed,
            conditionType = ConditionType.GreaterOrEqual,
            floatValue = 5.0f  // RunSpeedThreshold
        }
    },
    displayComponent = new DisplayComponent
    {
        animationCalculator = new StateAnimationMixCalculatorForSimpleClip
        {
            clip = runClip,
            speed = 1f
        }
    }
};
```

### 方案3：混合方案（最灵活）

使用BlendTree1D处理Idle/Walk过渡，使用独立状态处理Run/Sprint。

---

## 📊 参数映射表

| 速度范围 | 状态 | IsWalking | IsRunning | IsSprinting | 动画 |
|---------|------|-----------|-----------|-------------|------|
| 0 - 0.1 | Idle | 0 | 0 | 0 | Idle |
| 0.1 - 2.0 | Walk | 1 | 0 | 0 | Walk |
| 2.0 - 5.0 | Run | 0 | 1 | 0 | Run |
| 5.0+ | Sprint | 0 | 0 | 1 | Sprint |

---

## 🎮 使用示例

### 示例1：基础配置（BlendTree1D）

```csharp
public class LocomotionStateSetup : MonoBehaviour
{
    public AnimationClip idleClip;
    public AnimationClip walkClip;
    public AnimationClip runClip;
    public AnimationClip sprintClip;
    
    void SetupLocomotion()
    {
        // 1. 创建Locomotion状态
        var locomotionState = new StateDefinition
        {
            stateName = "Locomotion",
            displayComponent = new DisplayComponent
            {
                animationCalculator = new StateAnimationMixCalculatorForBlendTree1D
                {
                    parameterFloat = StateDefaultFloatParameter.Speed,
                    smoothTime = 0.15f,
                    samples = new[]
                    {
                        new ClipSampleForBlend1D { clip = idleClip, threshold = 0f },
                        new ClipSampleForBlend1D { clip = walkClip, threshold = 2f },
                        new ClipSampleForBlend1D { clip = runClip, threshold = 5f },
                        new ClipSampleForBlend1D { clip = sprintClip, threshold = 8f }
                    }
                }
            }
        };
        
        // 2. 设置阈值参数（可选，使用默认值）
        context.SetFloat(StateDefaultFloatParameter.WalkSpeedThreshold, 2f);
        context.SetFloat(StateDefaultFloatParameter.RunSpeedThreshold, 5f);
        context.SetFloat(StateDefaultFloatParameter.SprintSpeedThreshold, 8f);
        
        // 3. BasicMoveRotateModule会自动更新Speed参数
        // Speed参数变化 → BlendTree1D自动混合动画
    }
}
```

### 示例2：条件切换（独立状态）

```csharp
public class LocomotionWithConditions : MonoBehaviour
{
    void SetupLocomotionStates()
    {
        // Idle → Walk 转换条件
        var idleToWalk = new StateTransition
        {
            targetStateName = "Walk",
            conditions = new List<StateCondition>
            {
                new StateCondition
                {
                    parameterName = StateDefaultFloatParameter.Speed,
                    conditionType = ConditionType.Greater,
                    floatValue = 0.1f
                }
            }
        };
        
        // Walk → Run 转换条件
        var walkToRun = new StateTransition
        {
            targetStateName = "Run",
            conditions = new List<StateCondition>
            {
                new StateCondition
                {
                    parameterName = StateDefaultFloatParameter.Speed,
                    conditionType = ConditionType.GreaterOrEqual,
                    floatValue = 5.0f  // RunSpeedThreshold
                }
            }
        };
        
        // Run → Walk 转换条件
        var runToWalk = new StateTransition
        {
            targetStateName = "Walk",
            conditions = new List<StateCondition>
            {
                new StateCondition
                {
                    parameterName = StateDefaultFloatParameter.Speed,
                    conditionType = ConditionType.Less,
                    floatValue = 5.0f
                }
            }
        };
    }
}
```

### 示例3：手动控制（按键强制跑步）

```csharp
public class ManualLocomotionControl : MonoBehaviour
{
    private StateMachineContext _context;
    private bool _forceRun = false;
    
    void Update()
    {
        // 按住Shift强制跑步
        _forceRun = Input.GetKey(KeyCode.LeftShift);
        
        // 获取当前速度
        float speed = _context.Speed;
        
        // 调整阈值（强制跑步时降低阈值）
        if (_forceRun && speed > 0.5f)
        {
            _context.SetFloat(StateDefaultFloatParameter.IsRunning, 1f);
            _context.SetFloat(StateDefaultFloatParameter.IsWalking, 0f);
        }
        else
        {
            // 自动判断
            if (speed < 2f)
            {
                _context.SetFloat(StateDefaultFloatParameter.IsWalking, speed > 0.1f ? 1f : 0f);
                _context.SetFloat(StateDefaultFloatParameter.IsRunning, 0f);
            }
            else
            {
                _context.SetFloat(StateDefaultFloatParameter.IsWalking, 0f);
                _context.SetFloat(StateDefaultFloatParameter.IsRunning, 1f);
            }
        }
    }
}
```

---

## ⚙️ 配置建议

### 推荐阈值设置

| 类型 | Idle | Walk | Run | Sprint |
|------|------|------|-----|--------|
| **速度阈值** | 0 | 0.1 | 2.0 | 5.0 |
| **BlendTree位置** | 0.0 | 2.0 | 5.0 | 8.0 |
| **适用场景** | 静止 | 慢速移动 | 正常移动 | 快速移动 |

### 不同角色类型

#### 人形角色
- Walk: 0.5 - 2.5 m/s
- Run: 2.5 - 6.0 m/s
- Sprint: 6.0 - 10.0 m/s

#### 生物（四足动物）
- Walk: 1.0 - 3.0 m/s
- Trot: 3.0 - 7.0 m/s
- Gallop: 7.0 - 15.0 m/s

#### 机器人
- Slow: 0.5 - 2.0 m/s
- Normal: 2.0 - 5.0 m/s
- Boost: 5.0 - 12.0 m/s

---

## 🔄 过渡优化

### 平滑时间配置

```csharp
// BlendTree1D的smoothTime参数控制过渡
smoothTime = 0.15f;  // 推荐值

// 不同场景推荐值：
// - 快速响应（战斗）: 0.05f - 0.1f
// - 正常移动: 0.1f - 0.2f
// - 缓慢过渡（探索）: 0.2f - 0.4f
```

### 权重平滑

所有计算器已支持自动权重平滑（任务1完成）：
- 使用`weightCache`避免每帧重复计算
- 使用`SmoothDamp`实现平滑过渡
- 避免僵硬的动画跳变

---

## 📝 最佳实践

### ✅ 推荐做法

1. **使用BlendTree1D处理连续运动** - Idle/Walk/Run/Sprint
2. **合理设置阈值间隔** - 避免频繁切换
3. **启用权重平滑** - `smoothTime > 0.1f`
4. **使用Speed参数** - 由BasicMoveRotateModule自动更新
5. **配置状态标记** - IsWalking/IsRunning便于其他系统查询

### ❌ 避免的做法

1. **不要过度细分状态** - 避免Walk1/Walk2/Walk3等冗余状态
2. **不要使用硬切换** - 设置`smoothTime = 0`会导致僵硬
3. **不要频繁修改阈值** - 阈值应该是配置数据，不是运行时变量
4. **不要忽略边界条件** - 0速度和极低速度需要特殊处理

---

## 🐛 常见问题

### Q1: 为什么走路和跑步切换很僵硬？

**A**: 检查以下配置：
1. `smoothTime` 是否 > 0.1f
2. 权重平滑是否启用（`runtime.useSmoothing = true`）
3. 阈值间隔是否合理（建议 >= 2.0）

### Q2: 如何实现"按住Shift跑步"？

**A**: 两种方案：
1. **修改阈值** - 按下Shift时临时降低RunSpeedThreshold
2. **添加条件** - 在Run状态的enterConditions中添加Shift判断

### Q3: 如何支持倒退走路？

**A**: 使用2D BlendTree：
- X轴：SpeedX（左右）
- Y轴：SpeedZ（前后，负数表示倒退）

### Q4: 性能如何优化？

**A**: 
- BlendTree1D已经是O(log n)复杂度
- 权重缓存避免每帧重复计算
- 使用享元模式共享Calculator配置

---

## 🔗 相关文档

- [STATE_PARAMETER_OPTIMIZATION.md](./STATE_PARAMETER_OPTIMIZATION.md) - 参数系统
- [ANIMATION_FLYWEIGHT_DATA_INVENTORY.md](./ANIMATION_FLYWEIGHT_DATA_INVENTORY.md) - 计算器设计
- [BlendTree2D_QuickTest.cs](../Assets/Scripts/ESLogic/Entity/TestSystems/BlendTree2D_QuickTest.cs) - 2D移动示例

---

## 📊 性能指标

| 指标 | BlendTree1D | 独立状态 |
|------|-------------|---------|
| **内存占用** | ~200B | ~500B/状态 |
| **CPU开销** | O(log n) | O(1) |
| **GC分配** | 0 | 0 |
| **平滑度** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ |
| **灵活性** | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ |

**推荐**: 优先使用BlendTree1D，需要特殊逻辑时使用独立状态。

---

*最后更新: 2026-02-04*
*作者: ES Framework Team*
