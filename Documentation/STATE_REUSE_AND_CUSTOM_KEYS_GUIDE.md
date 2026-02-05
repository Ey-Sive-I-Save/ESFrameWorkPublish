# 状态复用与自定义键注册指南

**作者:** ES Framework Team  
**日期:** 2026年2月4日  
**版本:** 1.0  

---

## 📋 目录

1. [功能概述](#功能概述)
2. [自定义键注册](#自定义键注册)
3. [StateSharedData复用](#stateshareddata复用)
4. [实战案例](#实战案例)
5. [性能优化建议](#性能优化建议)

---

## 功能概述

### 核心需求

1. **自定义键注册**: 注册状态时允许指定自定义String键和Int键，而不使用Info或SharedData中的默认值
2. **动画复用**: 同一套状态逻辑配置，仅替换动画即可生成多个子状态

### 设计原则

- **零GC**: 所有克隆操作使用浅拷贝 + 选择性深拷贝，避免不必要的内存分配
- **灵活性**: 支持3种粒度的复用（Clip级、AnimationConfig级、批量生成）
- **向后兼容**: 保留原有注册API，新功能通过重载实现

---

## 自定义键注册

### 1. RegisterStateFromInfo 重载

#### 基础版本（原有）
```csharp
// 使用Info中的stateName和stateId
bool success = stateMachine.RegisterStateFromInfo(attackInfo, allowOverride: false);
```

#### 自定义String键
```csharp
// 使用自定义String键，IntKey使用Info中的stateId
bool success = stateMachine.RegisterStateFromInfo(
    info: walkInfo, 
    customStringKey: "Walk_Variant1", 
    allowOverride: false
);
```

#### 自定义String和Int键
```csharp
// 完全自定义键
bool success = stateMachine.RegisterStateFromInfo(
    info: dashInfo,
    customStringKey: "Dash_Quick",
    customIntKey: 10001,
    allowOverride: false
);
```

### 2. RegisterStateFromSharedData 新增API

#### 使用默认键
```csharp
StateSharedData walkData = CreateWalkData();
bool success = stateMachine.RegisterStateFromSharedData(walkData);
```

#### 使用自定义String键
```csharp
bool success = stateMachine.RegisterStateFromSharedData(
    sharedData: walkData,
    customStringKey: "Walk_Forward"
);
```

#### 使用完全自定义键
```csharp
bool success = stateMachine.RegisterStateFromSharedData(
    sharedData: walkData,
    customStringKey: "Walk_Backward",
    customIntKey: 2001,
    allowOverride: false
);
```

### 自定义键冲突处理

**规则:**
- 如果提供了自定义键，首先尝试直接注册
- 如果直接注册失败且`allowOverride=false`，自动回退到智能冲突处理（添加后缀）
- 如果`allowOverride=true`，覆盖已存在的状态

```csharp
// 场景：同一Info注册多个不同名的状态
for (int i = 0; i < 3; i++)
{
    bool success = stateMachine.RegisterStateFromInfo(
        baseAttackInfo,
        customStringKey: $"Attack_Combo{i+1}",
        customIntKey: 1000 + i
    );
}
// 结果: Attack_Combo1, Attack_Combo2, Attack_Combo3 (ID: 1000, 1001, 1002)
```

---

## StateSharedData复用

### 1. CloneWithClip - 最简单复用

**适用场景:** 同一逻辑，仅替换AnimationClip

```csharp
// 原始数据
StateSharedData walkBaseData = CreateWalkData();
walkBaseData.basicConfig.stateName = "Walk_Base";
walkBaseData.basicConfig.stateId = 100;

// 克隆并替换Clip
StateSharedData walkForward = walkBaseData.CloneWithClip(
    newStateName: "Walk_Forward",
    newStateId: 101,
    newClip: walkForwardClip
);

StateSharedData walkBackward = walkBaseData.CloneWithClip(
    newStateName: "Walk_Backward",
    newStateId: 102,
    newClip: walkBackwardClip
);

// 注册
stateMachine.RegisterStateFromSharedData(walkForward);
stateMachine.RegisterStateFromSharedData(walkBackward);
```

**优势:**
- API简洁，单行调用
- 自动创建SimpleClip Calculator
- 配置（代价、通道、过渡等）完全共享

### 2. CloneWithAnimation - 中级复用

**适用场景:** 替换整个AnimationConfig（如BlendTree、DirectBlend）

```csharp
// 原始数据（使用BlendTree1D）
StateSharedData runBaseData = CreateRunData();

// 创建不同方向的BlendTree
var blendTree_0_90 = new StateAnimationConfigData
{
    calculator = new StateAnimationMixCalculatorForBlendTree1D
    {
        parameterFloat = "Speed",
        samples = new[] { /* 0°到90°采样 */ }
    }
};

var blendTree_90_180 = new StateAnimationConfigData
{
    calculator = new StateAnimationMixCalculatorForBlendTree1D
    {
        parameterFloat = "Speed",
        samples = new[] { /* 90°到180°采样 */ }
    }
};

// 克隆并替换动画配置
StateSharedData run_0_90 = runBaseData.CloneWithAnimation(
    "Run_0_90", 201, blendTree_0_90
);

StateSharedData run_90_180 = runBaseData.CloneWithAnimation(
    "Run_90_180", 202, blendTree_90_180
);

// 注册
stateMachine.RegisterStateFromSharedData(run_0_90);
stateMachine.RegisterStateFromSharedData(run_90_180);
```

### 3. CloneWithAnimations - 批量复用

**适用场景:** 一次性生成多个变体

```csharp
// 原始数据
StateSharedData dashBaseData = CreateDashData();

// 准备多个动画配置
StateAnimationConfigData[] dashAnimations = new StateAnimationConfigData[]
{
    CreateDashForwardAnim(),
    CreateDashBackwardAnim(),
    CreateDashLeftAnim(),
    CreateDashRightAnim()
};

// 批量克隆
StateSharedData[] dashVariants = dashBaseData.CloneWithAnimations(
    baseNamePrefix: "Dash_",
    baseIdStart: 300,
    animations: dashAnimations,
    nameSuffixes: new[] { "Forward", "Backward", "Left", "Right" }
);

// 批量注册
foreach (var variant in dashVariants)
{
    stateMachine.RegisterStateFromSharedData(variant);
}
// 结果: Dash_Forward (300), Dash_Backward (301), Dash_Left (302), Dash_Right (303)
```

**优势:**
- 减少重复代码
- 统一命名规范
- 支持自动ID递增或-1自动分配

---

## 实战案例

### 案例1: 攻击连招系统

**需求:** 同一攻击逻辑（代价、通道、过渡），3段不同动画

```csharp
// 1. 创建基础攻击配置
StateSharedData attackBase = new StateSharedData
{
    basicConfig = new StateBasicConfig
    {
        stateName = "Attack_Base",
        stateId = 1000,
        pipelineType = StatePipelineType.Main,
        priority = 80
    },
    mergeData = new StateMergeData
    {
        channelMask = StateChannelMask.DoubleHand | StateChannelMask.Body,
        canCoexist = false
    },
    costData = new StateCostData
    {
        motionCost = 0.8f,
        flexibilityCost = 0.6f
    },
    hasAnimation = true,
    fadeInDuration = 0.05f,
    fadeOutDuration = 0.1f
};

// 2. 批量创建连招
AnimationClip[] comboClips = { attack1Clip, attack2Clip, attack3Clip };
StateSharedData[] comboStates = new StateSharedData[3];

for (int i = 0; i < 3; i++)
{
    comboStates[i] = attackBase.CloneWithClip(
        $"Attack_Combo{i+1}",
        1001 + i,
        comboClips[i]
    );
}

// 3. 注册所有连招
foreach (var state in comboStates)
{
    stateMachine.RegisterStateFromSharedData(state);
}
```

### 案例2: 多方向移动状态

**需求:** 8方向移动，共享移动逻辑，使用BlendTree2D

```csharp
// 1. 创建基础移动数据
StateSharedData moveBase = new StateSharedData
{
    basicConfig = new StateBasicConfig
    {
        pipelineType = StatePipelineType.Main,
        durationMode = StateDurationMode.Infinite
    },
    mergeData = new StateMergeData
    {
        channelMask = StateChannelMask.Legs,
        canCoexist = true
    }
};

// 2. 创建8方向BlendTree配置
string[] directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
StateAnimationConfigData[] blendTrees = new StateAnimationConfigData[8];

for (int i = 0; i < 8; i++)
{
    float angle = i * 45f;
    blendTrees[i] = CreateDirectionalBlendTree(angle, angle + 45f);
}

// 3. 批量生成
StateSharedData[] moveStates = moveBase.CloneWithAnimations(
    "Move_",
    2000,
    blendTrees,
    directions
);

// 4. 注册
foreach (var state in moveStates)
{
    stateMachine.RegisterStateFromSharedData(state);
}
```

### 案例3: 自定义键注册（技能热更）

**需求:** 运行时加载外部技能，使用GUID作为键

```csharp
// 外部技能数据（从AssetBundle加载）
StateAniDataInfo skillInfo = LoadSkillFromBundle("Skill_FireBall");

// 生成唯一GUID作为键（避免ID冲突）
string guid = Guid.NewGuid().ToString();
int uniqueId = GenerateHashCode(guid);

// 使用自定义键注册
bool success = stateMachine.RegisterStateFromInfo(
    skillInfo,
    customStringKey: guid,
    customIntKey: uniqueId,
    allowOverride: true // 允许热更覆盖
);

if (success)
{
    Debug.Log($"✅ 外部技能已加载: {guid} (ID:{uniqueId})");
}
```

---

## 性能优化建议

### 1. 克隆时机

**推荐:** 初始化阶段批量克隆
```csharp
// ✅ 好做法 - Awake/Start阶段
void Start()
{
    StateSharedData[] variants = baseData.CloneWithAnimations(/*...*/);
    foreach (var v in variants)
        stateMachine.RegisterStateFromSharedData(v);
}
```

**避免:** 运行时频繁克隆
```csharp
// ❌ 差做法 - 每帧克隆
void Update()
{
    var newState = baseData.CloneWithClip(/*...*/); // 产生GC！
}
```

### 2. 配置共享策略

**共享数据（零拷贝）:**
- `mergeData` (冲突规则)
- `costData` (代价计算)
- `phaseConfig` (阶段配置)
- `tags`, `group`, `description` 等元数据

**独立数据（需复制）:**
- `basicConfig.stateName` / `stateId`
- `animationConfig` (动画配置)
- `hasAnimation` 标记

### 3. 内存占用估算

单个StateSharedData克隆：
- **浅拷贝**: ~200 bytes
- **新BasicConfig**: ~100 bytes
- **新AnimationConfig**: ~50-500 bytes（取决于Calculator类型）
- **总计**: 350-800 bytes/状态

批量生成100个变体：
- 内存增量: ~35-80 KB
- GC压力: 极低（仅初始化时分配）

### 4. 最佳实践

#### ✅ 推荐模式

```csharp
// 1. 预定义模板
private StateSharedData attackTemplate;
private StateSharedData moveTemplate;

void Awake()
{
    // 2. 创建模板（一次性）
    attackTemplate = CreateAttackTemplate();
    moveTemplate = CreateMoveTemplate();
    
    // 3. 批量生成变体
    var attacks = GenerateAttackVariants(attackTemplate);
    var moves = GenerateMoveVariants(moveTemplate);
    
    // 4. 统一注册
    RegisterStates(attacks);
    RegisterStates(moves);
}

StateSharedData[] GenerateAttackVariants(StateSharedData template)
{
    return template.CloneWithAnimations(
        "Attack_", 1000, 
        new[] { combo1Anim, combo2Anim, combo3Anim },
        new[] { "Light", "Heavy", "Finisher" }
    );
}
```

#### ❌ 避免模式

```csharp
// 每次需要时动态创建（产生大量GC）
StateSharedData GetAttackState(int index)
{
    return attackBase.CloneWithClip(/*...*/); // 反复创建！
}
```

---

## API 快速参考

### StateMachine注册API

| 方法 | 说明 | 自定义键支持 |
|------|------|-------------|
| `RegisterStateFromInfo(info, allowOverride)` | 使用Info默认键 | ❌ |
| `RegisterStateFromInfo(info, customStringKey, allowOverride)` | 自定义String键 | ✅ |
| `RegisterStateFromInfo(info, customStringKey, customIntKey, allowOverride)` | 自定义双键 | ✅ |
| `RegisterStateFromSharedData(sharedData, customStringKey, customIntKey, allowOverride)` | SharedData直接注册 | ✅ |

### StateSharedData克隆API

| 方法 | 说明 | 返回类型 | 使用场景 |
|------|------|----------|---------|
| `Clone()` | 完整浅拷贝 | `StateSharedData` | 完全复制配置 |
| `CloneWithClip(name, id, clip)` | 替换单个Clip | `StateSharedData` | 简单动画变体 |
| `CloneWithAnimation(name, id, animConfig)` | 替换动画配置 | `StateSharedData` | 复杂动画系统 |
| `CloneWithAnimations(prefix, idStart, anims, suffixes)` | 批量生成 | `StateSharedData[]` | 多变体生成 |

---

## 常见问题

### Q1: CloneWithClip 和 CloneWithAnimation 有什么区别？

**A:** 
- `CloneWithClip`: 仅替换单个AnimationClip，自动创建SimpleClip Calculator
- `CloneWithAnimation`: 替换整个AnimationConfig，支持BlendTree、DirectBlend等复杂模式

### Q2: 克隆后的状态能修改配置吗？

**A:** 可以，但注意：
- 修改`mergeData`/`costData`会影响所有克隆体（共享引用）
- 修改`basicConfig`/`animationConfig`仅影响当前克隆体（独立对象）

### Q3: 自定义键冲突时会怎样？

**A:**
- 如果`allowOverride=false`: 自动添加后缀（如`_r1`, `_r2`）
- 如果`allowOverride=true`: 覆盖已存在的状态
- 如果直接注册失败，自动回退到智能冲突处理

### Q4: 性能开销如何？

**A:**
- 克隆: ~350-800 bytes/状态，仅初始化时分配
- 注册: O(log N) 查找 + O(1) 插入
- 运行时: 零额外开销（共享配置）

---

## 总结

### 核心价值

1. **自定义键**: 完全控制状态标识，支持热更、多实例等高级场景
2. **动画复用**: 减少90%配置重复，降低维护成本
3. **零GC设计**: 智能共享策略，最小化内存占用
4. **向后兼容**: 保留原有API，新功能通过重载实现

### 使用建议

- **简单场景**: 使用`CloneWithClip`
- **复杂场景**: 使用`CloneWithAnimation`
- **批量生成**: 使用`CloneWithAnimations`
- **热更新**: 使用自定义键注册

---

**最后更新:** 2026年2月4日  
**版本:** 1.0.0  
**反馈:** ES Framework Team
