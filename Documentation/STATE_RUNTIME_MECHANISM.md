# 状态运行时机制设计文档

## 一、状态持续时间模式（StateDurationMode）

### 1. 无限持续（Infinite）
- **特性**：状态永久持续，不会自动退出
- **适用场景**：Idle、Walk、Run等基础移动状态
- **退出条件**：仅通过外部条件或转换规则退出

### 2. 按动画结束（UntilAnimationEnd）
- **特性**：跟随动画Clip的长度自动结束
- **适用场景**：攻击、技能释放等有明确动画周期的动作
- **退出条件**：动画播放完毕后自动进入释放阶段

### 3. 定时（Timed）
- **特性**：指定固定时长，不依赖动画长度
- **适用场景**：Buff状态、特殊效果状态
- **退出条件**：到达指定时长后退出

---

## 二、Loop动画与非Loop动画的运行机制

### Loop动画（loopClip = true）
```
┌─────────────────────────────────────────┐
│ 运行阶段 (Running Phase)                 │
│ ├─ 动画循环播放                          │
│ ├─ 占据代价通道                          │
│ └─ 持续运行直到被外部条件打断            │
└─────────────────────────────────────────┘
          ↓ (外部打断/转换)
┌─────────────────────────────────────────┐
│ 返还阶段 (Returning Phase)               │
│ ├─ 动画继续播放（平滑过渡）              │
│ ├─ 部分返还代价                          │
│ └─ 可以开始容纳其他动作                  │
└─────────────────────────────────────────┘
          ↓ (到达释放时间点)
┌─────────────────────────────────────────┐
│ 释放阶段 (Released Phase)                │
│ ├─ 动画可能未完成但不再占据代价          │
│ ├─ 完全释放所有通道                      │
│ └─ 状态标记为可清理                      │
└─────────────────────────────────────────┘
```

**关键点**：
- Loop动画在运行阶段会无限循环
- 进入返还阶段后动画继续播放，但代价开始返还
- 释放阶段动画可能还在播放，但不再影响其他状态

### 非Loop动画（loopClip = false）
```
┌─────────────────────────────────────────┐
│ 运行阶段 (Running Phase)                 │
│ ├─ 动画播放一次                          │
│ ├─ 占据代价通道                          │
│ └─ 播放到70%进入返还阶段（可配置）       │
└─────────────────────────────────────────┘
          ↓ (到达returnStartTime)
┌─────────────────────────────────────────┐
│ 返还阶段 (Returning Phase)               │
│ ├─ 动画继续播放后30%                     │
│ ├─ 部分返还代价（returnCostFraction）    │
│ └─ 可以被其他动作打断或叠加              │
└─────────────────────────────────────────┘
          ↓ (到达releaseStartTime)
┌─────────────────────────────────────────┐
│ 释放阶段 (Released Phase)                │
│ ├─ 动画播放完毕或接近完毕                │
│ ├─ 完全释放所有代价通道                  │
│ └─ 状态退出，可清理                      │
└─────────────────────────────────────────┘
          ↓
        [状态退出]
```

**关键点**：
- 非Loop动画只播放一次
- 自动根据归一化时间进入返还和释放阶段
- 适合Attack、Dodge等一次性动作

---

## 三、状态运行时阶段（StateRuntimePhase）

### 1. 运行阶段（Running）
- **时间范围**：0 → returnStartTime
- **代价状态**：完全占据配置的代价通道
- **行为特性**：
  - 动画正常播放
  - 不能被低优先级状态打断
  - 参数更新正常响应

### 2. 返还阶段（Returning）
- **时间范围**：returnStartTime → releaseStartTime
- **代价状态**：部分返还代价（按returnCostFraction比例）
- **行为特性**：
  - 动画继续播放（可能处于后摇动画）
  - 可以被同路状态弱打断
  - 可以开始容纳其他动作（如连招衔接）
  
**示例配置**：
```csharp
phaseConfig.returnStartTime = 0.7f;      // 70%时进入返还
phaseConfig.returnCostFraction = 0.5f;   // 返还50%代价
```

### 3. 释放阶段（Released）
- **时间范围**：releaseStartTime → 动画结束
- **代价状态**：完全释放所有代价通道
- **行为特性**：
  - 动画可能未播完但不再占据资源
  - 不再阻止其他状态进入
  - 状态标记为可清理

**示例配置**：
```csharp
phaseConfig.releaseStartTime = 0.9f;     // 90%时完全释放
```

---

## 四、阶段配置（StatePhaseConfig）

### 字段说明
```csharp
public class StatePhaseConfig
{
    // 返还阶段开始时间（归一化，0-1）
    public float returnStartTime = 0.7f;
    
    // 释放阶段开始时间（归一化，0-1）
    public float releaseStartTime = 0.9f;
    
    // 返还阶段返还多少比例的代价（0-1）
    public float returnCostFraction = 0.5f;
}
```

### 典型配置场景

#### 快速攻击（轻攻击）
```csharp
returnStartTime = 0.6f;       // 60%时可打断
releaseStartTime = 0.8f;      // 80%时完全释放
returnCostFraction = 0.7f;    // 返还70%代价，方便连招
```

#### 重型攻击（蓄力攻击）
```csharp
returnStartTime = 0.85f;      // 85%才可打断
releaseStartTime = 0.95f;     // 95%才完全释放
returnCostFraction = 0.3f;    // 只返还30%代价，限制连招
```

#### 移动状态（Loop）
```csharp
returnStartTime = 0.0f;       // 立即可打断
releaseStartTime = 0.1f;      // 快速释放
returnCostFraction = 1.0f;    // 完全返还
```

---

## 五、运行时状态更新流程

### 每帧更新逻辑
```csharp
void UpdateState(float deltaTime)
{
    // 1. 更新状态时间
    stateTime += deltaTime;
    normalizedTime = stateTime / animationDuration;
    
    // 2. 检查阶段转换
    if (currentPhase == StateRuntimePhase.Running)
    {
        if (normalizedTime >= phaseConfig.returnStartTime)
        {
            EnterReturningPhase();
        }
    }
    else if (currentPhase == StateRuntimePhase.Returning)
    {
        if (normalizedTime >= phaseConfig.releaseStartTime)
        {
            EnterReleasedPhase();
        }
    }
    
    // 3. 处理动画播放
    if (loopClip)
    {
        // Loop动画：循环播放
        UpdateLoopAnimation();
    }
    else
    {
        // 非Loop动画：播放一次
        if (normalizedTime >= 1.0f)
        {
            OnAnimationComplete();
        }
    }
    
    // 4. 检查退出条件
    CheckExitConditions();
}
```

### 阶段转换处理
```csharp
void EnterReturningPhase()
{
    currentPhase = StateRuntimePhase.Returning;
    
    // 返还部分代价
    float returnAmount = costData.CalculateReturnAmount(phaseConfig.returnCostFraction);
    stateMachine.ReturnCost(returnAmount);
    
    Debug.Log($"状态[{stateName}]进入返还阶段，返还{returnAmount}代价");
}

void EnterReleasedPhase()
{
    currentPhase = StateRuntimePhase.Released;
    
    // 完全释放代价
    stateMachine.ReleaseCost(costData);
    
    // 标记可清理
    canBeCleanedUp = true;
    
    Debug.Log($"状态[{stateName}]进入释放阶段，完全释放代价");
}
```

---

## 六、冲突合并机制（准备实现）

### 同通道状态冲突处理
```
状态A (占据: RightHand + BodySpine)
   vs
状态B (占据: RightHand + LeftHand)

冲突通道: RightHand
```

**解决策略**：
1. **优先级判断**：比较状态优先级
2. **代价检查**：检查是否有足够可用代价
3. **阶段判断**：
   - 状态A在运行阶段 → 不可打断
   - 状态A在返还阶段 → 可能弱打断
   - 状态A在释放阶段 → 可完全替换

### 弱打断（WeakInterrupt）
```csharp
// 状态A在返还阶段，状态B尝试进入
if (stateA.currentPhase == StateRuntimePhase.Returning &&
    stateA.allowWeakInterrupt &&
    IsSamePath(stateA, stateB))
{
    // 弱打断：状态A退化而不是完全退出
    stateA.DegradeToTarget(stateA.degradeTargetId);
    stateB.Enter();
}
```

---

## 七、享元数据初始化

所有配置模块支持享元初始化：
```csharp
// StateAnimationConfig
animationConfig.InitializeAsShared();

// StateParameterConfig
parameterConfig.InitializeAsShared();

// StateTransitionConfig
transitionConfig.InitializeAsShared();

// StateConditionConfig
conditionConfig.InitializeAsShared();

// StateAdvancedConfig
advancedConfig.InitializeAsShared();
```

**优势**：
- 减少重复代码
- 统一初始化逻辑
- 方便批量创建状态
- 支持配置模板复用

---

## 八、商业级最佳实践

### 1. 状态设计原则
- Loop动画用于持续性状态（Idle、Run）
- 非Loop动画用于一次性动作（Attack、Jump）
- 合理设置returnStartTime平衡手感与流畅度
- 重型动作设置较晚的releaseStartTime避免过早打断

### 2. 阶段配置建议
| 动作类型 | returnStartTime | releaseStartTime | returnCostFraction |
|---------|-----------------|------------------|--------------------|
| 快速轻攻击 | 0.5-0.6 | 0.8-0.9 | 0.7-0.8 |
| 重型攻击 | 0.8-0.9 | 0.95-1.0 | 0.3-0.5 |
| 闪避 | 0.4-0.5 | 0.7-0.8 | 0.8-1.0 |
| 技能释放 | 0.7-0.8 | 0.9-0.95 | 0.5-0.6 |
| 移动状态 | 0.0-0.1 | 0.1-0.2 | 1.0 |

### 3. 性能优化
- 启用备忘系统（enableMemoization）缓存条件检查
- 使用脏标记（dirtyThreshold）减少不必要的更新
- Loop动画控制循环次数避免无限占用

---

## 九、调试与可视化

### 编辑器工具
- StateAniDataInfo提供数据验证按钮
- StateCostData提供配置合理性检查
- 支持自定义调试日志颜色

### 运行时监控
```csharp
if (advancedConfig.enableDebugLog)
{
    Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(advancedConfig.debugLogColor)}>" +
              $"[{stateName}] Phase: {currentPhase}, Time: {normalizedTime:F2}" +
              $"</color>");
}
```

---

**文档版本**：1.0  
**最后更新**：2026年2月1日  
**维护者**：ES Framework Team
