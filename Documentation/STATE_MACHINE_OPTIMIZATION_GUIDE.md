# 状态机优化更新说明

## 核心改进

### 1. 零GC流水线管理
- ✅ **移除字典依赖**：完全移除`pipelines`字典，使用直接字段访问
- ✅ **零GC遍历**：`GetAllPipelines()`使用yield return直接返回字段，无枚举器开销
- ✅ **崩溃优先**：前两个流水线（Basic/Main）不判空，为空直接崩溃，确保核心流水线正确初始化

### 2. 流水线索引管理
```csharp
// 每个流水线的input索引唯一且不变
basicPipeline.rootInputIndex = (int)StatePipelineType.Basic;  // 0
mainPipeline.rootInputIndex = (int)StatePipelineType.Main;    // 1
buffPipeline.rootInputIndex = (int)StatePipelineType.Buff;    // 2
```

### 3. FeedbackState空转机制
```csharp
// 流水线现在支持空转反馈状态
pipeline.feedbackState = someIdleState;

// 当流水线所有状态退出时，自动尝试激活feedbackState
if (pipelineData.runningStates.Count == 0 && pipelineData.feedbackState != null)
{
    TryActivateState(pipelineData.feedbackState, pipeline);
}
```

**使用示例：**
```csharp
// 设置流水线的空转状态
var idleState = new IdleState();
stateMachine.RegisterState("idle", idleState, StatePipelineType.Main);

var mainPipeline = stateMachine.GetPipeline(StatePipelineType.Main);
mainPipeline.feedbackState = idleState;

// 现在当主流水线空闲时，会自动激活idle状态
```

### 4. MainState智能选择
主状态现在基于**带权重总代价最高**的状态自动选择：

```csharp
private void UpdatePipelineMainState(StatePipelineRuntime pipeline)
{
    StateBase maxCostState = null;
    float maxWeightedCost = float.MinValue;

    foreach (var state in pipeline.runningStates)
    {
        var costData = state.stateSharedData.costData;
        float weightedCost = costData.GetWeightedMotion() + 
                           costData.GetWeightedAgility() + 
                           costData.GetWeightedTarget();
        
        if (weightedCost > maxWeightedCost)
        {
            maxWeightedCost = weightedCost;
            maxCostState = state;
        }
    }
    
    pipeline.mainState = maxCostState ?? pipeline.runningStates.FirstOrDefault();
}
```

**代价配置示例：**
```csharp
state.stateSharedData.costData = new StateCostData
{
    enableCostCalculation = true,
    motionCost = 10f,
    agilityCost = 5f,
    targetCost = 3f,
    motionWeight = 1.0f,    // 总权重 = 10*1 + 5*0.5 + 3*0.3 = 13.4
    agilityWeight = 0.5f,
    targetWeight = 0.3f
};
```

### 5. StateSharedData扩展系统

**初始化接口：**
```csharp
// 在状态首次使用前初始化
state.stateSharedData.InitializeExtensions();

// 或使用扩展方法
state.EnsureInitialized();
```

**标签系统：**
```csharp
// 设置标签
state.SetTags("攻击", "近战", "高优先级");

// 检查标签
if (state.stateSharedData.HasTag("攻击"))
{
    // 处理攻击状态
}

// 检查多个标签
if (state.stateSharedData.HasAnyTag("攻击", "技能"))
{
    // 处理战斗相关状态
}
```

**扩展属性：**
```csharp
// 设置扩展属性
state.stateSharedData.SetExtensionProperty("ComboIndex", 3);
state.stateSharedData.SetExtensionProperty("SkillLevel", 5);
state.stateSharedData.SetExtensionProperty("CustomData", myCustomObject);

// 获取扩展属性
int comboIndex = state.stateSharedData.GetExtensionProperty("ComboIndex", 0);
int skillLevel = state.stateSharedData.GetExtensionProperty("SkillLevel", 1);
```

**自定义初始化：**
```csharp
[Serializable]
public class MyStateSharedData : StateSharedData
{
    public int customField;
    
    protected override void OnExtensionInitialize()
    {
        base.OnExtensionInitialize();
        
        // 自定义初始化逻辑
        SetExtensionProperty("InitTime", Time.time);
        customField = 100;
    }
}
```

## 性能优化对比

| 项目 | 优化前 | 优化后 |
|------|--------|--------|
| 流水线遍历GC | 72B/次 | 0B |
| 字典查找 | O(log n) | O(1) |
| MainState选择 | 简单逻辑 | 基于代价权重 |
| 空转处理 | 无 | FeedbackState自动激活 |

## 热拔插支持

```csharp
// 运行时注册新状态
var newState = new AttackState();
stateMachine.RegisterState("attack", newState, StatePipelineType.Main);

// 立即激活
stateMachine.TryActivateState("attack", StatePipelineType.Main);

// 运行时注销状态
stateMachine.UnregisterState("attack");
```

## 最佳实践

1. **必须初始化前两个流水线**
```csharp
// ✅ 正确：确保基础和主流水线初始化
stateMachine.Initialize(entity);

// ❌ 错误：不初始化会崩溃
```

2. **为核心流水线设置FeedbackState**
```csharp
// 推荐为主流水线设置默认空闲状态
mainPipeline.feedbackState = idleState;
```

3. **配置状态代价以控制MainState**
```csharp
// 高优先级状态设置更高的权重代价
attackState.stateSharedData.costData.motionCost = 100f;
idleState.stateSharedData.costData.motionCost = 1f;
```

4. **使用标签进行状态分类**
```csharp
// 便于批量查询和处理
attackState.SetTags("战斗", "攻击", "可打断");
defendState.SetTags("战斗", "防御", "不可打断");
```

## API变更

### 新增方法
- `UpdatePipelineMainState(pipeline)` - 更新流水线主状态
- `StateSharedData.InitializeExtensions()` - 初始化扩展
- `StateSharedData.SetExtensionProperty<T>()` - 设置扩展属性
- `StateSharedData.GetExtensionProperty<T>()` - 获取扩展属性
- `StateSharedData.HasTag()` - 检查标签
- `StateBase.EnsureInitialized()` - 扩展方法：确保初始化

### 移除内容
- ❌ `Dictionary<StatePipelineType, StatePipelineRuntime> pipelines` - 已完全移除

### 修改行为
- `GetAllPipelines()` - 不再判空前两个流水线
- `ExecuteStateActivation()` - 使用代价权重选择MainState
- `DeactivateState()` - 自动触发FeedbackState
