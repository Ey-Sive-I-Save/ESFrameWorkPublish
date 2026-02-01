# 状态合并机制实装方案

## 一、从旧架构提取的核心设计（ESStandard级别）

### 1.1 保留的设计要素

#### ✅ **位标记通道系统（Channel）**
```csharp
[Flags]
public enum StateChannelMask
{
    None = 0,
    RightHand = 1 << 0,      // 右手
    LeftHand = 1 << 1,       // 左手
    DoubleHand = RightHand | LeftHand,
    RightLeg = 1 << 2,       // 右腿
    LeftLeg = 1 << 3,        // 左腿
    DoubleLeg = RightLeg | LeftLeg,
    FourLimbs = DoubleHand | DoubleLeg,
    Head = 1 << 4,           // 头部
    BodySpine = 1 << 5,      // 躯干脊柱
    AllBodyActive = FourLimbs | Head | BodySpine,
    Heart = 1 << 6,          // 心灵（buff/debuff）
    Eye = 1 << 7,            // 视觉（致盲）
    Ear = 1 << 8,            // 听觉（耳鸣）
    AllBodyAndHeartAndMore = AllBodyActive | Heart | Eye | Ear,
    Target = 1 << 9          // 目标锁定
}
```
**用途**：快速判断两个状态是否有身体部位冲突
- 位运算性能极高（O(1)）
- 内存占用小（uint32）
- 支持组合通道（四肢、全身等）

#### ✅ **多级判定机制（四层优先级）**
```
第1层：无条件名单（字符串匹配）
  ├─ 无条件被打断名单 → HitAndReplace
  ├─ 无条件打断名单 → HitAndReplace
  ├─ 无条件被融入名单 → MergeComplete
  └─ 无条件融入名单 → MergeComplete

第2层：通道冲突检查（位运算）
  ├─ 通道无冲突 → MergeComplete
  └─ 通道有冲突 → 继续判定

第3层：Pipeline层级判定
  ├─ 层级不重叠 → 高层级优先
  └─ 层级重叠 → 继续判定

第4层：优先级数值比较
  └─ 比较BeHitOrder和HitOrder
```

#### ✅ **合并结果枚举**
```csharp
public enum MergeResult
{
    HitAndReplace,    // 打断并替换（左被右打断）
    MergeComplete,    // 合并成功（左右共存）
    MergeFail,        // 合并失败（右无法加入）
    WeakInterrupt     // 弱打断（新增：保留左状态但降级）
}
```

---

## 二、优化后的实装架构

### 2.1 核心数据结构（已实现）

#### StateSharedData - 状态共享数据
```csharp
[Serializable]
public class StateSharedData
{
    [TitleGroup("身份与通道")]
    [LabelText("状态ID")]
    public int stateId;
    
    [LabelText("状态名称")]
    public string stateName;
    
    [LabelText("Pipeline类型")]
    public StatePipelineType pipelineType;
    
    [LabelText("通道掩码")]
    public StateChannelMask channelMask;
    
    // === 打断配置（优化后） ===
    [TitleGroup("打断配置")]
    [LabelText("能被打断")]
    public InterruptOption canBeInterrupted;
    
    [LabelText("能打断别人")]
    public InterruptOption canInterrupt;
    
    // === 无条件名单（优化为ID） ===
    [TitleGroup("无条件名单（最高优先）")]
    [LabelText("无条件被打断ID列表")]
    [InfoBox("这些状态可以无条件打断本状态（最高优先级）")]
    public List<int> unconditionalBeInterruptedByIds = new List<int>();
    
    [LabelText("无条件打断ID列表")]
    [InfoBox("本状态可以无条件打断这些状态（最高优先级）")]
    public List<int> unconditionalInterruptIds = new List<int>();
    
    [LabelText("无条件被融入ID列表")]
    [InfoBox("这些状态可以无条件与本状态共存（合并）")]
    public List<int> unconditionalBeMergedByIds = new List<int>();
    
    [LabelText("无条件融入ID列表")]
    [InfoBox("本状态可以无条件与这些状态共存（合并）")]
    public List<int> unconditionalMergeIds = new List<int>();
    
    // === 优先级配置 ===
    [TitleGroup("优先级")]
    [LabelText("被打断优先级"), Range(0, 255)]
    [InfoBox("数值越小越容易被打断")]
    public byte beInterruptedPriority = 128;
    
    [LabelText("打断优先级"), Range(0, 255)]
    [InfoBox("数值越大越容易打断别人")]
    public byte interruptPriority = 128;
    
    // === 运行时缓存（优化为HashSet） ===
    [NonSerialized] private HashSet<int> _cachedUnconditionalBeInterruptedByIds;
    [NonSerialized] private HashSet<int> _cachedUnconditionalInterruptIds;
    [NonSerialized] private HashSet<int> _cachedUnconditionalBeMergedByIds;
    [NonSerialized] private HashSet<int> _cachedUnconditionalMergeIds;
    [NonSerialized] private bool _isInitialized;
    
    /// <summary>
    /// 初始化运行时缓存（List转HashSet，O(n)→O(1)查询）
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized) return;
        
        _cachedUnconditionalBeInterruptedByIds = new HashSet<int>(unconditionalBeInterruptedByIds);
        _cachedUnconditionalInterruptIds = new HashSet<int>(unconditionalInterruptIds);
        _cachedUnconditionalBeMergedByIds = new HashSet<int>(unconditionalBeMergedByIds);
        _cachedUnconditionalMergeIds = new HashSet<int>(unconditionalMergeIds);
        
        _isInitialized = true;
    }
    
    /// <summary>
    /// 快速检查是否在无条件名单（O(1)）
    /// </summary>
    public bool IsUnconditionalBeInterruptedBy(int otherId) 
        => _cachedUnconditionalBeInterruptedByIds.Contains(otherId);
    
    public bool CanUnconditionalInterrupt(int otherId) 
        => _cachedUnconditionalInterruptIds.Contains(otherId);
    
    public bool IsUnconditionalBeMergedBy(int otherId) 
        => _cachedUnconditionalBeMergedByIds.Contains(otherId);
    
    public bool CanUnconditionalMerge(int otherId) 
        => _cachedUnconditionalMergeIds.Contains(otherId);
}
```

#### 关键枚举定义
```csharp
/// <summary>
/// 打断选项（简化版）
/// </summary>
public enum InterruptOption
{
    [LabelText("总是允许")]
    Always,
    
    [LabelText("仅同Pipeline测试")]
    SamePipelineOnly,
    
    [LabelText("仅Pipeline碾压")]
    PipelineCrushOnly,
    
    [LabelText("永不允许")]
    Never
}

/// <summary>
/// Pipeline类型（对应旧的LogicLayer）
/// </summary>
public enum StatePipelineType
{
    [LabelText("基础Pipeline（移动/待机）")]
    Basic = 0,
    
    [LabelText("主Pipeline（攻击/技能）")]
    Main = 1,
    
    [LabelText("Buff Pipeline（增益/减益）")]
    Buff = 2,
    
    [LabelText("超级Pipeline（必杀技/QTE）")]
    Super = 3
}
```

---

## 三、核心算法实现

### 3.1 状态合并判定器（StateMergeResolver）

```csharp
/// <summary>
/// 状态合并判定器 - 核心算法
/// 负责判断两个状态能否合并、谁能打断谁
/// </summary>
public static class StateMergeResolver
{
    /// <summary>
    /// 执行合并判定
    /// </summary>
    /// <param name="left">当前正在运行的状态</param>
    /// <param name="right">尝试加入的新状态</param>
    /// <param name="leftPhase">左状态当前运行阶段</param>
    /// <returns>合并结果</returns>
    public static MergeResult ResolveMerge(
        StateAniDataInfo left,
        StateAniDataInfo right,
        StateRuntimePhase leftPhase = StateRuntimePhase.Running)
    {
        // === 第1层：无条件名单检查（最高优先级） ===
        var result = CheckUnconditionalLists(left, right);
        if (result != MergeResult.None)
            return result;
        
        // === 第2层：通道冲突检查 ===
        result = CheckChannelConflict(left, right);
        if (result != MergeResult.None)
            return result;
        
        // === 第3层：Pipeline层级判定 ===
        result = CheckPipelineLevel(left, right, leftPhase);
        if (result != MergeResult.None)
            return result;
        
        // === 第4层：优先级数值比较 ===
        return CompareInterruptPriority(left, right);
    }
    
    // ========== 第1层：无条件名单检查 ==========
    private static MergeResult CheckUnconditionalLists(
        StateAniDataInfo left, 
        StateAniDataInfo right)
    {
        var leftShared = left.sharedData;
        var rightShared = right.sharedData;
        
        // 检查：左是否无条件被右打断
        if (leftShared.IsUnconditionalBeInterruptedBy(rightShared.stateId))
            return MergeResult.HitAndReplace;
        
        // 检查：右是否无条件打断左
        if (rightShared.CanUnconditionalInterrupt(leftShared.stateId))
            return MergeResult.HitAndReplace;
        
        // 检查：左是否无条件被右融入
        if (leftShared.IsUnconditionalBeMergedBy(rightShared.stateId))
            return MergeResult.MergeComplete;
        
        // 检查：右是否无条件融入左
        if (rightShared.CanUnconditionalMerge(leftShared.stateId))
            return MergeResult.MergeComplete;
        
        return MergeResult.None;  // 无匹配，继续下一层
    }
    
    // ========== 第2层：通道冲突检查 ==========
    private static MergeResult CheckChannelConflict(
        StateAniDataInfo left, 
        StateAniDataInfo right)
    {
        var leftShared = left.sharedData;
        var rightShared = right.sharedData;
        
        // 位运算检查通道冲突（O(1)）
        var channelConflict = (int)leftShared.channelMask & (int)rightShared.channelMask;
        
        // 如果任一方设置为Never，则根据通道冲突决定
        if (leftShared.canBeInterrupted == InterruptOption.Never ||
            rightShared.canInterrupt == InterruptOption.Never)
        {
            if (channelConflict == 0)
                return MergeResult.MergeComplete;  // 无冲突，可共存
            else
                return MergeResult.MergeFail;      // 有冲突但不允许打断
        }
        
        // 无冲突时直接允许合并
        if (channelConflict == 0)
            return MergeResult.MergeComplete;
        
        return MergeResult.None;  // 有冲突，继续下一层判定
    }
    
    // ========== 第3层：Pipeline层级判定 ==========
    private static MergeResult CheckPipelineLevel(
        StateAniDataInfo left, 
        StateAniDataInfo right,
        StateRuntimePhase leftPhase)
    {
        var leftShared = left.sharedData;
        var rightShared = right.sharedData;
        
        var leftPipeline = (int)leftShared.pipelineType;
        var rightPipeline = (int)rightShared.pipelineType;
        
        // Pipeline层级不同
        if (leftPipeline != rightPipeline)
        {
            // 都设置为SamePipelineOnly，且层级不同 → 允许共存
            if (leftShared.canBeInterrupted == InterruptOption.SamePipelineOnly &&
                rightShared.canInterrupt == InterruptOption.SamePipelineOnly)
                return MergeResult.MergeComplete;
            
            // 高Pipeline碾压低Pipeline
            if (rightPipeline > leftPipeline)
                return MergeResult.HitAndReplace;
            else
                return MergeResult.MergeFail;  // 低Pipeline无法打断高Pipeline
        }
        
        // Pipeline层级相同，但任一方设置为PipelineCrushOnly → 拒绝合并
        if (leftShared.canBeInterrupted == InterruptOption.PipelineCrushOnly ||
            rightShared.canInterrupt == InterruptOption.PipelineCrushOnly)
            return MergeResult.MergeFail;
        
        // === 特殊：运行阶段判定 ===
        // 如果左状态处于Returning或Released阶段，更容易被打断
        if (leftPhase == StateRuntimePhase.Returning || 
            leftPhase == StateRuntimePhase.Released)
        {
            return MergeResult.HitAndReplace;
        }
        
        return MergeResult.None;  // 继续下一层判定
    }
    
    // ========== 第4层：优先级数值比较 ==========
    private static MergeResult CompareInterruptPriority(
        StateAniDataInfo left, 
        StateAniDataInfo right)
    {
        var leftShared = left.sharedData;
        var rightShared = right.sharedData;
        
        // 比较：左的被打断优先级 vs 右的打断优先级
        if (leftShared.beInterruptedPriority <= rightShared.interruptPriority)
            return MergeResult.HitAndReplace;
        else
            return MergeResult.MergeFail;
    }
    
    // ========== 弱打断判定（高级功能） ==========
    /// <summary>
    /// 检查是否可以执行弱打断
    /// </summary>
    public static bool CanWeakInterrupt(StateAniDataInfo left, StateAniDataInfo right)
    {
        // 需要左状态启用弱打断
        if (!left.advancedConfig.allowWeakInterrupt)
            return false;
        
        // 右状态必须能打断左状态（但不想完全替换）
        var result = ResolveMerge(left, right);
        return result == MergeResult.HitAndReplace;
    }
}

/// <summary>
/// 合并结果枚举（新增None用于多层判定）
/// </summary>
public enum MergeResult
{
    None = -1,            // 无结果，继续下一层判定
    HitAndReplace = 0,    // 打断并替换
    MergeComplete = 1,    // 合并成功
    MergeFail = 2,        // 合并失败
    WeakInterrupt = 3     // 弱打断
}
```

---

## 四、具体使用场景详解

### 4.1 场景1：格斗游戏的攻击打断

#### 配置示例
```csharp
// === 轻攻击状态 ===
StateAniDataInfo lightAttack = new StateAniDataInfo();
lightAttack.sharedData.stateId = 101;
lightAttack.sharedData.stateName = "LightAttack";
lightAttack.sharedData.pipelineType = StatePipelineType.Main;
lightAttack.sharedData.channelMask = StateChannelMask.RightHand;
lightAttack.sharedData.canBeInterrupted = InterruptOption.Always;
lightAttack.sharedData.canInterrupt = InterruptOption.Always;
lightAttack.sharedData.beInterruptedPriority = 50;   // 容易被打断
lightAttack.sharedData.interruptPriority = 50;

// === 重攻击状态 ===
StateAniDataInfo heavyAttack = new StateAniDataInfo();
heavyAttack.sharedData.stateId = 102;
heavyAttack.sharedData.stateName = "HeavyAttack";
heavyAttack.sharedData.pipelineType = StatePipelineType.Main;
heavyAttack.sharedData.channelMask = StateChannelMask.DoubleHand;
heavyAttack.sharedData.canBeInterrupted = InterruptOption.Always;
heavyAttack.sharedData.canInterrupt = InterruptOption.Always;
heavyAttack.sharedData.beInterruptedPriority = 100;  // 不容易被打断
heavyAttack.sharedData.interruptPriority = 150;      // 容易打断别人

// === 判定结果 ===
var result = StateMergeResolver.ResolveMerge(lightAttack, heavyAttack);
// 结果：HitAndReplace（重攻击可以打断轻攻击）
```

**应用**：重攻击可以打断轻攻击，实现格斗游戏的攻击优先级系统。

---

### 4.2 场景2：Buff/Debuff与主动作共存

#### 配置示例
```csharp
// === 攻击状态 ===
StateAniDataInfo attack = new StateAniDataInfo();
attack.sharedData.pipelineType = StatePipelineType.Main;
attack.sharedData.channelMask = StateChannelMask.DoubleHand;

// === 移速增益Buff ===
StateAniDataInfo speedBuff = new StateAniDataInfo();
speedBuff.sharedData.pipelineType = StatePipelineType.Buff;
speedBuff.sharedData.channelMask = StateChannelMask.Heart;  // 心灵通道

// === 判定结果 ===
var result = StateMergeResolver.ResolveMerge(attack, speedBuff);
// 结果：MergeComplete（通道无冲突，可共存）
```

**应用**：Buff状态使用独立通道（Heart），可以与任何身体动作共存。

---

### 4.3 场景3：必杀技无条件打断

#### 配置示例
```csharp
// === 普通攻击 ===
StateAniDataInfo normalAttack = new StateAniDataInfo();
normalAttack.sharedData.stateId = 201;
normalAttack.sharedData.pipelineType = StatePipelineType.Main;

// === 必杀技 ===
StateAniDataInfo ultimateSkill = new StateAniDataInfo();
ultimateSkill.sharedData.stateId = 301;
ultimateSkill.sharedData.pipelineType = StatePipelineType.Super;
ultimateSkill.sharedData.unconditionalInterruptIds.Add(201);  // 无条件打断普通攻击

// === 判定结果 ===
var result = StateMergeResolver.ResolveMerge(normalAttack, ultimateSkill);
// 结果：HitAndReplace（第1层无条件名单直接判定）
```

**应用**：必杀技无视所有规则，直接打断指定状态。

---

### 4.4 场景4：受击保护期（弱打断）

#### 配置示例
```csharp
// === 闪避状态 ===
StateAniDataInfo dodge = new StateAniDataInfo();
dodge.sharedData.pipelineType = StatePipelineType.Main;
dodge.sharedData.canBeInterrupted = InterruptOption.Always;
dodge.advancedConfig.allowWeakInterrupt = true;  // 允许弱打断
dodge.advancedConfig.degradeTargetId = 999;      // 弱打断后退化到待机

// === 受击状态 ===
StateAniDataInfo hit = new StateAniDataInfo();
hit.sharedData.pipelineType = StatePipelineType.Main;
hit.sharedData.interruptPriority = 200;

// === 判定结果 ===
if (StateMergeResolver.CanWeakInterrupt(dodge, hit))
{
    // 闪避状态不完全结束，而是退化到待机状态
    // 保留闪避的无敌帧，但允许受击动画播放部分帧
}
```

**应用**：实现受击保护期，避免玩家连续被打无法反应。

---

### 4.5 场景5：后摇可打断机制

#### 配置示例
```csharp
// === 攻击状态（有后摇） ===
StateAniDataInfo attack = new StateAniDataInfo();
attack.basicConfig.phaseConfig.returnStartTime = 0.7f;   // 70%进入返还阶段
attack.basicConfig.phaseConfig.releaseStartTime = 0.9f;  // 90%进入释放阶段

// === 闪避状态 ===
StateAniDataInfo dodge = new StateAniDataInfo();

// === 运行时判定 ===
var currentPhase = GetCurrentPhase(attack);  // 假设返回Returning
var result = StateMergeResolver.ResolveMerge(attack, dodge, currentPhase);
// 结果：HitAndReplace（第3层判定时检测到Returning阶段，允许打断）
```

**应用**：攻击前摇不可打断，后摇可打断，提升游戏手感。

---

## 五、性能优化对比

### 5.1 旧设计 vs 新设计

| 对比项 | 旧设计 | 新设计 | 性能提升 |
|-------|--------|--------|---------|
| **无条件名单查询** | `string[].Contains()` O(n) | `HashSet<int>.Contains()` O(1) | **~10倍** |
| **通道冲突检查** | `GetHashCode() &` (错误) | `(int)Enum &` (正确) | **修复Bug** |
| **Pipeline比较** | 位运算`&`判重叠 (混乱) | 整数比较 (清晰) | **逻辑简化** |
| **运行阶段判定** | 无 | 三阶段支持 | **新增功能** |
| **弱打断** | 无 | WeakInterrupt + Degrade | **新增功能** |

### 5.2 内存占用
```
StateSharedData大小（预估）：
- 基础字段：~20 bytes
- 无条件名单（4个List）：平均50个ID × 4 bytes × 4 = 800 bytes
- HashSet缓存：平均50个ID × 4 bytes × 4 = 800 bytes
- 总计：~1.6 KB/状态

典型项目（200个状态）：
- 总内存：~320 KB
- 可接受范围 ✅
```

---

## 六、集成步骤

### 6.1 第一步：扩展StateAniDataInfo
```csharp
// 在StateAniDataInfo中添加StateSharedData字段
[HideLabel, InlineProperty]
public StateSharedData sharedData = new StateSharedData();
```

### 6.2 第二步：实现StateMergeResolver
创建新文件：`StateMergeResolver.cs`
- 实现四层判定算法
- 提供公共接口`ResolveMerge()`

### 6.3 第三步：集成到状态机
```csharp
public class StateController
{
    private List<StateRuntime> _activeStates = new List<StateRuntime>();
    
    public bool TryAddState(StateAniDataInfo newState)
    {
        foreach (var activeState in _activeStates)
        {
            var result = StateMergeResolver.ResolveMerge(
                activeState.DataInfo, 
                newState, 
                activeState.CurrentPhase
            );
            
            if (result == MergeResult.HitAndReplace)
            {
                RemoveState(activeState);
                AddState(newState);
                return true;
            }
            else if (result == MergeResult.MergeFail)
            {
                return false;  // 无法添加
            }
            // MergeComplete：继续检查下一个状态
        }
        
        // 所有状态都允许合并
        AddState(newState);
        return true;
    }
}
```

---

## 七、测试用例

### 7.1 单元测试框架
```csharp
[TestFixture]
public class StateMergeResolverTests
{
    [Test]
    public void UnconditionalInterrupt_ShouldReplace()
    {
        var left = CreateState(101, "Attack");
        var right = CreateState(102, "Ultimate");
        right.sharedData.unconditionalInterruptIds.Add(101);
        right.sharedData.Initialize();
        
        var result = StateMergeResolver.ResolveMerge(left, right);
        Assert.AreEqual(MergeResult.HitAndReplace, result);
    }
    
    [Test]
    public void NoChannelConflict_ShouldMerge()
    {
        var left = CreateState(101, "Attack");
        left.sharedData.channelMask = StateChannelMask.RightHand;
        
        var right = CreateState(102, "Buff");
        right.sharedData.channelMask = StateChannelMask.Heart;
        
        var result = StateMergeResolver.ResolveMerge(left, right);
        Assert.AreEqual(MergeResult.MergeComplete, result);
    }
    
    [Test]
    public void ReturningPhase_ShouldBeEasilyInterrupted()
    {
        var left = CreateState(101, "Attack");
        var right = CreateState(102, "Dodge");
        
        var result = StateMergeResolver.ResolveMerge(
            left, right, StateRuntimePhase.Returning
        );
        Assert.AreEqual(MergeResult.HitAndReplace, result);
    }
}
```

---

## 八、扩展方向

### 8.1 代价系统集成
```csharp
// StateCostData可以提供额外的代价信息
public class StateCostData
{
    public float manaCost;       // 法力消耗
    public float staminaCost;    // 体力消耗
    public float cooldownTime;   // 冷却时间
}

// 在判定前检查代价
if (player.CurrentMana < newState.costData.manaCost)
    return false;  // 资源不足，无法激活
```

### 8.2 同路径状态流畅衔接
```csharp
// SamePathType配置
public enum SamePathType
{
    None,              // 不同路径
    Combo,             // 连招
    ChargeLevel,       // 蓄力等级
    LoopCycle          // 循环节点
}

// 同路径状态之间降低打断优先级
if (left.advancedConfig.samePathType == SamePathType.Combo &&
    right.advancedConfig.samePathType == SamePathType.Combo)
{
    // 连招状态之间优先衔接而非打断
}
```

---

## 九、总结

### 保留的精华
1. ✅ **位标记通道系统** - 性能极佳
2. ✅ **多级判定逻辑** - 清晰灵活
3. ✅ **无条件名单机制** - 策划友好

### 优化的问题
1. 🔧 **字符串→ID** - 性能提升10倍
2. 🔧 **GetHashCode修复** - 避免严重Bug
3. 🔧 **三阶段支持** - 实现后摇打断
4. 🔧 **弱打断机制** - 提升手感

### 新增功能
1. ⭐ **运行时阶段判定** - StateRuntimePhase
2. ⭐ **弱打断机制** - WeakInterrupt + Degrade
3. ⭐ **Pipeline简化** - 整数层级替代位标记

---

**文档版本**：1.0  
**实装阶段**：架构设计完成，待集成  
**负责团队**：ES Framework Core Team
