# 基于Playable的多层级动画状态机系统

## 概述

这是一个史上最精巧的基于Unity Playable API开发的高级动画状态机系统,具有以下核心特性:

### 🎯 核心特性

1. **三条层级架构**
   - **基本线** (Basic Pipeline): 控制跑跳下蹲等基础动作的硬性过渡支撑动画
   - **主线** (Main Pipeline): 控制技能、表情、交互等互相排斥的动画和执行
   - **Buff线** (Buff Pipeline): 控制Buff效果,可能不输出动作只执行效果
   - 三条线稳定Mix输出到Playable Graph

2. **代价参数化系统**
   - 四肢、意愿等编成浮点数代价值 (0~1)
   - 动作进入时消耗代价,后摇阶段逐步释放
   - 退出时必须完全返还代价
   - 支持分批返还和临时代价

3. **智能打断测试**
   - 代价条件符合 → 直接进入并占据同线
   - 代价不符合 → 测试主线 → 测试基本线和Buff线 → 收集打断目标完成过渡
   - 否则直接忽略跳转并更新备忘状态

4. **同路状态与退化机制**
   - 静止→走路→奔跑是同路状态
   - 被弱打断时不会完全退出,而是往低级退化
   - 支持优雅的状态降级

5. **备忘状态系统**
   - 防止合并冲突测试每帧发生造成性能损耗
   - 只有状态退出、退化或后摇时才刷新备忘状态
   - 滞留"尝试进入→禁止"的列表,提升性能

6. **丰富的上下文参数**
   - Float, Int, Bool, Trigger (自动重置)
   - StateValue (状态枚举)
   - Entity (实体对象引用)
   - String (字符串标记)
   - TempCost (临时代价)
   - Curve (曲线参数,用于IK)

7. **独立Clip配置表**
   - Clip不内置在动画中,按键查找
   - 方便复用和替换
   - 支持运行时动态加载

8. **多态序列化组件系统**
   - DisplayComponent: 显示组件,控制Clip播放
   - TransitionComponent: 过渡组件,控制状态转换
   - ExecutionComponent: 执行组件,处理非动画逻辑
   - IKComponent: IK组件,自动处理IK混合
   - 支持自定义组件扩展

9. **ScriptableObject数据驱动**
   - 总状态机使用SO存储
   - 与Clip替换拆分开
   - 可作为默认"环境"提供Clip表和参数
   - 基本线直接包含,主线和Buff线支持动态装载

10. **高级显示器功能**
    - 不直接绑定单个Clip
    - 支持Clip截断 (clip start/end offset)
    - 支持多个Clip阶段并组合
    - 支持Clip混合和权重控制

---

## 📁 文件结构

```
Core/
├── CostManager.cs                    # 代价管理器
├── StateContext.cs                   # 上下文参数系统
├── StateCondition.cs                 # 条件评估系统
├── MemoizationSystem.cs              # 备忘状态系统
├── StateComponents.cs                # 多态组件系统
├── StateDefinition.cs                # 状态定义
├── StatePipeline.cs                  # 层级和状态实例
├── AnimationClipTable.cs             # Clip配置表
├── StateMachineData.cs               # 状态机ScriptableObject
└── PlayableStateMachineController.cs # 主控制器

ValyeTypeSupport/
├── 0EnumSupport/
│   ├── StatePipelineType.cs         # 层级类型枚举
│   └── StateChannelMask.cs          # 通道掩码枚举
└── 1NormalFeatureSupport/
    ├── StateAnimationClip.cs         # Clip配置基类
    └── StateCost.cs                  # 代价配置
```

---

## 🚀 快速开始

### 1. 创建Clip配置表

右键菜单: `Create > ES > Animation > Clip Table`

```csharp
// 添加Clip映射
clipEntries.Add(new ClipEntry
{
    key = "Idle",
    clip = idleAnimationClip,
    tags = new List<string> { "basic", "loop" }
});
```

### 2. 创建状态机数据

右键菜单: `Create > ES > Animation > State Machine Data`

```csharp
// 配置基本线状态
basicStates.Add(new StateDefinition
{
    stateId = 0,
    stateName = "Idle",
    pipelineType = StatePipelineType.Basic,
    priority = 10,
    duration = -1, // 无限循环
    
    // 配置代价
    cost = new StateCost
    {
        mainCostPart = new StateChannelCostPart
        {
            channelMask = StateChannelMask.AllBodyActive,
            EnterCostValue = 0.2f
        }
    },
    
    // 配置显示组件
    displayComponent = new DisplayComponent
    {
        mode = DisplayMode.SingleClip,
        singleClip = new ClipSegment
        {
            clipKey = "Idle",
            loop = true
        }
    }
});
```

### 3. 添加控制器到GameObject

```csharp
// 添加组件
var controller = gameObject.AddComponent<PlayableStateMachineController>();
controller.stateMachineData = yourStateMachineData;

// 初始化并启动
controller.Initialize();
controller.StartStateMachine();
```

### 4. 控制状态转换

```csharp
// 设置参数
controller.SetFloat("Speed", 5.0f);
controller.SetBool("IsGrounded", true);
controller.SetTrigger("Jump");

// 尝试进入状态
controller.TryEnterState(stateId: 10);
```

---

## 💡 核心概念详解

### 代价系统 (Cost System)

代价系统是本架构的核心创新之一,将人体的四肢和意愿抽象为浮点数代价值:

```csharp
// 定义通道掩码
StateChannelMask.RightHand  // 右手
StateChannelMask.LeftHand   // 左手
StateChannelMask.DoubleLeg  // 双腿
StateChannelMask.Heart      // 心灵/意愿
StateChannelMask.Eye        // 眼睛/注视

// 配置代价
var cost = new StateCost
{
    mainCostPart = new StateChannelCostPart
    {
        channelMask = StateChannelMask.DoubleHand | StateChannelMask.Heart,
        EnterCostValue = 0.8f,  // 需要80%的手部和意愿代价
        EnableReturnProgress = true,
        ReturnFraction = 1f      // 完全返还
    }
};
```

**代价流程:**
1. 进入状态时消耗代价
2. 到达后摇时间点 (recoveryStartTime) 开始返还
3. 在返还持续时间 (recoveryDuration) 内逐步释放
4. 退出时必须完全返还

### 层级混合

三条层级独立运行,最终混合输出:

```csharp
// 设置层级权重
stateMachineData.basicPipelineWeight = 1.0f;  // 基本线 100%
stateMachineData.mainPipelineWeight = 1.0f;   // 主线 100%
stateMachineData.buffPipelineWeight = 0.5f;   // Buff线 50%

// 运行时调整
controller.GetPipeline(StatePipelineType.Buff).SetWeight(0.8f);
```

### 备忘状态优化

```csharp
// 自动管理,无需手动调用
// 当状态退出、退化或进入后摇时:
_memoSystem.MarkDirty();  // 标记为脏,下帧刷新

// 拒绝记录会自动保存,避免重复测试:
if (_memoSystem.IsStateDenied(stateId, currentTime))
{
    return false; // 直接返回,不再测试条件
}
```

### 同路状态与退化

```csharp
// 定义同路状态
var idleState = new StateDefinition
{
    samePathType = SamePathType.Idle,
    allowWeakInterrupt = false  // 最低级,不能再退化
};

var walkState = new StateDefinition
{
    samePathType = SamePathType.Walk,
    allowWeakInterrupt = true,
    degradeTargetId = 0  // 退化到Idle
};

var runState = new StateDefinition
{
    samePathType = SamePathType.Run,
    allowWeakInterrupt = true,
    degradeTargetId = 1  // 退化到Walk
};
```

当 Run 状态被低优先级打断时,不会完全退出,而是退化到 Walk。

---

## 🎨 高级功能示例

### 1. Clip截断和组合

```csharp
var displayComponent = new DisplayComponent
{
    mode = DisplayMode.MultipleSegments,
    clipSegments = new List<ClipSegment>
    {
        // 第一段: 攻击前摇 (使用攻击动画的前30%)
        new ClipSegment
        {
            clipKey = "Attack",
            startTime = 0f,
            endTime = 0.3f,
            clipStartOffset = 0f,
            clipEndOffset = 0.3f
        },
        // 第二段: 保持姿势 (使用另一个Clip)
        new ClipSegment
        {
            clipKey = "AttackHold",
            startTime = 0.3f,
            endTime = 0.7f,
            loop = true
        },
        // 第三段: 攻击后摇
        new ClipSegment
        {
            clipKey = "Attack",
            startTime = 0.7f,
            endTime = 1f,
            clipStartOffset = 0.7f,
            clipEndOffset = 1f
        }
    }
};
```

### 2. IK曲线绑定

```csharp
var ikComponent = new IKComponent
{
    curveBindings = new List<IKCurveBinding>
    {
        new IKCurveBinding
        {
            curveName = "RightHandIK",
            ikTarget = "RightHandTarget"
        }
    }
};

// 在状态运行时,曲线会自动驱动IK权重
context.SetCurve("RightHandIK", AnimationCurve.Linear(0, 0, 1, 1));
```

### 3. 执行组件

```csharp
var executionComponent = new ExecutionComponent
{
    timing = ExecutionTiming.OnEnter,
    actions = new List<StateAction>
    {
        new SetParameterAction
        {
            parameterName = "ComboCount",
            parameterType = ContextParameterType.Int,
            intValue = 1
        }
    }
};
```

### 4. 条件系统

```csharp
// 组合条件
var enterCondition = new CompositeCondition
{
    mode = LogicMode.And,
    conditions = new List<StateCondition>
    {
        new FloatCondition
        {
            parameterName = "Speed",
            mode = CompareMode.Greater,
            threshold = 5f
        },
        new BoolCondition
        {
            parameterName = "IsGrounded",
            expectedValue = true
        }
    }
};

stateDef.enterConditions.Add(enterCondition);
```

---

## 🔧 运行时API

### 参数操作

```csharp
// Float
controller.SetFloat("Speed", 5.0f);
float speed = controller.GetFloat("Speed");

// Int
controller.SetInt("ComboCount", 2);
int combo = controller.GetInt("ComboCount");

// Bool
controller.SetBool("IsAttacking", true);
bool isAttacking = controller.GetBool("IsAttacking");

// Trigger (自动在下一帧重置)
controller.SetTrigger("Jump");
```

### 状态控制

```csharp
// 尝试进入状态
bool success = controller.TryEnterState(stateId: 10);

// 强制进入状态 (忽略条件和代价)
controller.TryEnterState(stateId: 10, forceEnter: true);

// 指定层级
controller.TryEnterState(stateId: 10, StatePipelineType.Main);

// 获取当前状态
StateInstance currentState = controller.GetCurrentState(StatePipelineType.Basic);
```

### 事件监听

```csharp
controller.OnStateEntered += (stateId, pipeline) =>
{
    Debug.Log($"进入状态: {stateId} on {pipeline}");
};

controller.OnStateTransitioned += (from, to, pipeline) =>
{
    Debug.Log($"状态转换: {from} -> {to} on {pipeline}");
};
```

---

## 🎯 设计优势

1. **性能优化**
   - 备忘状态系统避免重复测试
   - 按需刷新,减少CPU开销
   - 层级独立更新,支持多线程扩展

2. **高度复用**
   - Clip表独立管理,可跨项目复用
   - 组件化设计,状态配置灵活
   - 参数驱动,减少硬编码

3. **强大扩展性**
   - 支持自定义StateComponent
   - 支持自定义StateCondition
   - 支持自定义StateAction
   - 完全数据驱动

4. **直观可视化**
   - 使用Odin Inspector增强编辑体验
   - 运行时调试信息一目了然
   - 支持Clip表验证和状态机验证

5. **精巧架构**
   - 代价系统创新性地解决动画冲突
   - 同路退化机制优雅处理状态降级
   - 多层级混合满足复杂动画需求
   - 组件化设计达到极致解耦

---

## 📝 注意事项

1. 确保GameObject上有Animator组件
2. StateMachineData必须配置defaultClipTable
3. 状态ID必须唯一
4. 代价返还时间不能超过状态持续时间
5. 同路状态的degradeTargetId必须指向有效状态

---

## 🎓 学习路径

1. 从简单的单状态开始 (Idle)
2. 添加基本线的跑跳状态
3. 配置主线的技能状态
4. 尝试代价系统和打断测试
5. 实现同路退化
6. 使用高级功能 (Clip截断、IK混合等)

---

## 📚 相关文档

- Unity Playable API: https://docs.unity3d.com/Manual/Playables.html
- Odin Inspector: https://odininspector.com/

---

**这是一个真正精巧、强大、可扩展的动画状态机系统! 🎉**
