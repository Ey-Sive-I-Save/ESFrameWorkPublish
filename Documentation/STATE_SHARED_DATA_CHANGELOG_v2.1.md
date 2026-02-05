# StateSharedData 优化变更说明

> **日期：** 2026年2月4日  
> **版本：** v2.1 简化版

---

## ✅ 已完成的优化

### 1. **删除技能系统配置**
**原因：** 技能是基于状态的上层封装，不应放在StateSharedData中

**删除的内容：**
- ❌ `SkillType` 枚举（Active/Passive/Toggle/Channeled）
- ❌ `isSkill` 标记
- ❌ `skillType` 类型
- ❌ `cooldown` 冷却配置
- ❌ `cooldownGroup` 冷却组
- ❌ `hasCost` 消耗标记
- ❌ `costType` 消耗类型
- ❌ `costValue` 消耗数值
- ❌ `supportCombo` 连击支持
- ❌ `comboNextSkills` 连击技能列表
- ❌ `comboWindow` 连击窗口
- ❌ `CanActivate()` 方法（技能激活检查）

**技能系统正确实现方式：**
```csharp
// 技能应该是状态之上的另一层
public class Skill
{
    public StateBase state;        // 关联的状态
    public float cooldown;         // 冷却
    public ResourceCost cost;      // 消耗
    public List<Skill> comboChain; // 连击链
    
    public bool CanCast() { ... }  // 技能层的检查
}
```

---

### 2. **简化专业词汇**
**原因：** "热插拔"、"热更新"等词汇过于专业，不够直白

**词汇变更：**
| 旧词汇 | 新词汇 | 说明 |
|--------|--------|------|
| `热插拔` | `运行时替换` | 更直白 |
| `热更新` | `运行时替换` | 统一术语 |
| `supportHotReload` | `canReplaceAtRuntime` | 清晰易懂 |
| `keepRuntimeDataOnReload` | `keepDataOnReplace` | 简化命名 |
| `热更新支持` | `替换支持` | Tab标签名 |

**字段对比：**
```csharp
// ❌ 旧版本（专业词汇）
supportHotReload = true
keepRuntimeDataOnReload = true

// ✅ 新版本（简单直白）
canReplaceAtRuntime = true
keepDataOnReplace = true
```

---

### 3. **添加对象池支持**
**原因：** 使用ESSimplePool对大量StateBase对象进行池管理

**实现内容：**
```csharp
public class StateBase : IPoolableAuto
{
    // 对象池（容量500，预热10）
    public static readonly ESSimplePool<StateBase> Pool = new ESSimplePool<StateBase>(
        factoryMethod: () => new StateBase(),
        resetMethod: (obj) => obj.OnResetAsPoolable(),
        initCount: 10,
        maxCount: 500,
        poolDisplayName: "StateBase Pool"
    );

    // IPoolableAuto 接口实现
    public bool IsRecycled { get; set; }
    public void OnResetAsPoolable() { ... }
    public void TryAutoPushedToPool() { ... }
}
```

**使用方式：**
```csharp
// 从池中获取
var state = StateBase.Pool.GetInPool();

// 使用完毕后回收
state.TryAutoPushedToPool();
```

**性能优势：**
- ✅ 零GC分配（复用对象）
- ✅ 自动重置状态（OnResetAsPoolable）
- ✅ 线程安全（ESSimplePool保证）
- ✅ 支持大规模并发（500容量）

---

## 📊 精简对比

### 代码行数变化
| 类 | 旧版本 | 新版本 | 减少 |
|----|--------|--------|------|
| StateSharedData | 346行 | 268行 | -78行 (-22.5%) |
| StateBase | 457行 | 503行 | +46行 (对象池) |

### 字段数量变化
| 分类 | 旧版本 | 新版本 | 变化 |
|------|--------|--------|------|
| 核心字段 | 15个 | 15个 | 无变化 |
| 技能字段 | 11个 | 0个 | -11个 |
| 替换字段 | 6个 | 6个 | 重命名 |
| 总计 | 32个 | 21个 | -11个 (-34%) |

---

## 🎯 API变更清单

### 删除的API
```csharp
// ❌ 已删除
CanActivate(out string reason)  // 技能激活检查
```

### 新增的API
```csharp
// ✅ 对象池相关（StateBase）
StateBase.Pool.GetInPool()         // 从池获取
state.TryAutoPushedToPool()        // 回收到池
state.OnResetAsPoolable()          // 重置状态
```

### 保留的API
```csharp
// ✅ 继续保留
HasTag(string tag)                 // 标签检查
AddTag(string tag)                 // 添加标签
RemoveTag(string tag)              // 移除标签
GetDisplayName(string fallback)    // 获取显示名
Clone()                            // 克隆配置
```

---

## 🔄 迁移指南

### 如果你之前使用了技能配置

**旧代码：**
```csharp
var fireballData = new StateSharedData {
    isSkill = true,
    skillType = SkillType.Active,
    cooldown = 5f,
    hasCost = true,
    costType = "Mana",
    costValue = 30f
};
```

**新代码（推荐）：**
```csharp
// 1. 只配置状态本身
var fireballStateData = new StateSharedData {
    basicConfig = { stateName = "Fireball", intKey = 1001 },
    hasAnimation = true,
    tags = ["Attack", "Fire"]
};

// 2. 在技能层封装
public class FireballSkill : Skill
{
    public FireballSkill()
    {
        stateName = "Fireball";
        cooldown = 5f;
        cost = new ResourceCost { type = "Mana", value = 30f };
    }
    
    public override bool CanCast()
    {
        return CheckCooldown() && CheckCost();
    }
}
```

### 如果你使用了热更新配置

**旧代码：**
```csharp
stateData.supportHotReload = true;
stateData.keepRuntimeDataOnReload = true;
```

**新代码：**
```csharp
stateData.canReplaceAtRuntime = true;
stateData.keepDataOnReplace = true;
```

---

## 🎉 升级收益

### 1. **代码更清晰**
- ✅ 去除技能相关字段，职责单一
- ✅ 简化专业词汇，易于理解
- ✅ 减少34%字段数量

### 2. **性能提升**
- ✅ 对象池支持，零GC分配
- ✅ 自动回收机制，防止内存泄漏
- ✅ 支持500对象并发

### 3. **架构合理**
- ✅ 状态系统专注状态管理
- ✅ 技能系统独立封装
- ✅ 层次分明，易于扩展

---

## 📝 注意事项

1. **技能系统独立实现**  
   如需技能系统，请在状态系统之上单独实现Skill层

2. **对象池使用**  
   创建StateBase时优先使用`StateBase.Pool.GetInPool()`

3. **词汇统一**  
   后续文档和代码统一使用"运行时替换"而非"热更新"

4. **向后兼容**  
   旧代码中的技能配置字段需要手动迁移到Skill层

---

**变更生效时间：** 立即  
**影响范围：** StateSharedData、StateBase  
**建议操作：** 查看[STATE_SHARED_DATA_QUICK_REFERENCE.md](STATE_SHARED_DATA_QUICK_REFERENCE.md)快速上手
