# StateSharedData 使用指南

> **更新日期：** 2026年2月4日  
> **版本：** v2.0 - 技能系统支持版  
> **适用范围：** 状态配置、技能配置、热插拔状态

---

## 📋 目录

1. [核心概念](#1-核心概念)
2. [基础状态配置](#2-基础状态配置)
3. [动画配置](#3-动画配置)
4. [技能系统配置](#4-技能系统配置)
5. [热插拔配置](#5-热插拔配置)
6. [API使用示例](#6-api使用示例)
7. [最佳实践](#7-最佳实践)

---

## 1. 核心概念

### StateSharedData 是什么？

`StateSharedData` 是状态的**共享配置数据**，采用Flyweight模式设计：
- **SharedData（共享数据）**：多个状态实例共享的配置，如动画Clip、优先级、标签等
- **VariableData（运行数据）**：每个状态实例独有的运行时数据，如进入时间、播放进度等

### 设计原则

1. ✅ **清晰自然**：去除高级词汇，使用直白的命名
2. ✅ **精准描述**：每个字段都有明确的LabelText和Tooltip
3. ✅ **功能扩展**：支持技能系统、热插拔、自定义淡入淡出曲线
4. ✅ **高性能**：支持热更新、临时状态、状态覆盖

---

## 2. 基础状态配置

### 2.1 最小配置示例

```csharp
var idleStateData = new StateSharedData
{
    basicConfig = new StateBasicConfig
    {
        stateName = "Idle",
        intKey = 100,
        pipelineType = StatePipelineType.Basic,
        priority = 10
    },
    hasAnimation = false // 无动画的纯逻辑状态
};
```

### 2.2 完整配置示例

```csharp
var walkStateData = new StateSharedData
{
    // 核心配置
    basicConfig = new StateBasicConfig
    {
        stateName = "Walk",
        stringKey = "Walk",
        intKey = 101,
        pipelineType = StatePipelineType.Basic,
        priority = 20,
        canBeFeedback = true,
        fallbackChannelIndex = 0
    },
    
    // 标记信息
    tags = new List<string> { "Movement", "Locomotion" },
    group = "Movement",
    displayName = "行走",
    description = "角色行走状态，支持8方向移动",
    
    // 切换配置
    mergeData = new StateMergeData
    {
        occupyChannels = new List<int> { 0, 1 }
    },
    
    costData = new StateCostData
    {
        motionCost = 0.3f,
        flexibilityCost = 0.8f
    }
};
```

---

## 3. 动画配置

### 3.1 基础动画

```csharp
var runStateData = new StateSharedData
{
    basicConfig = { stateName = "Run", intKey = 102 },
    
    // 启用动画
    hasAnimation = true,
    animationConfig = new StateAnimationConfigData
    {
        clip = runAnimClip,
        playbackSpeed = 1.0f,
        wrapMode = WrapMode.Loop
    }
};
```

### 3.2 淡入淡出配置

```csharp
var attackStateData = new StateSharedData
{
    basicConfig = { stateName = "Attack", intKey = 200 },
    
    hasAnimation = true,
    animationConfig = { clip = attackClip },
    
    // 启用平滑过渡
    enableFadeInOut = true,
    fadeInDuration = 0.15f,   // 0.15秒淡入
    fadeOutDuration = 0.2f,   // 0.2秒淡出
    
    // 自定义淡入淡出曲线
    fadeInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1),  // 缓入缓出
    fadeOutCurve = AnimationCurve.Linear(0, 1, 1, 0)     // 线性淡出
};
```

### 3.3 高级曲线配置

```csharp
// 快速淡入，慢速淡出（适合攻击动作）
attackStateData.fadeInCurve = new AnimationCurve(
    new Keyframe(0, 0, 0, 3),      // 开始时斜率3（快速上升）
    new Keyframe(1, 1, 0, 0)       // 结束时斜率0（平滑停止）
);

attackStateData.fadeOutCurve = new AnimationCurve(
    new Keyframe(0, 1, 0, 0),      // 开始时斜率0（平滑开始）
    new Keyframe(1, 0, -0.5f, 0)   // 结束时斜率-0.5（缓慢下降）
);
```

---

## 4. 技能系统配置

### 4.1 主动技能

```csharp
var fireballSkillData = new StateSharedData
{
    basicConfig = new StateBasicConfig
    {
        stateName = "Fireball",
        intKey = 1001,
        pipelineType = StatePipelineType.Main,
        priority = 100
    },
    
    // 技能标记
    isSkill = true,
    skillType = SkillType.Active,
    
    // 冷却配置
    cooldown = 8f,                    // 8秒冷却
    cooldownGroup = "Fire",           // 火系技能共享冷却
    
    // 消耗配置
    hasCost = true,
    costType = "Mana",
    costValue = 50f,
    
    // 标签
    tags = new List<string> { "Skill", "Attack", "Fire", "Range" },
    
    // 动画
    hasAnimation = true,
    animationConfig = { clip = fireballCastClip },
    enableFadeInOut = true,
    fadeInDuration = 0.1f,
    fadeOutDuration = 0.2f
};
```

### 4.2 技能连击

```csharp
// 技能1：重击
var heavyStrikeData = new StateSharedData
{
    basicConfig = { stateName = "HeavyStrike", intKey = 1002 },
    
    isSkill = true,
    skillType = SkillType.Active,
    cooldown = 5f,
    
    // 支持连击
    supportCombo = true,
    comboNextSkills = new List<string> { "UpperSlash", "GroundSmash" },
    comboWindow = 0.8f,  // 0.8秒内可接续技能
    
    tags = new List<string> { "Skill", "Melee", "Combo" }
};

// 技能2：上挑斩（连击技能）
var upperSlashData = new StateSharedData
{
    basicConfig = { stateName = "UpperSlash", intKey = 1003 },
    
    isSkill = true,
    skillType = SkillType.Active,
    cooldown = 0f,  // 连击技能无独立冷却
    
    supportCombo = true,
    comboNextSkills = new List<string> { "AerialAssault" },
    comboWindow = 0.6f,
    
    tags = new List<string> { "Skill", "Melee", "Combo", "Launcher" }
};
```

### 4.3 被动技能

```csharp
var dodgeReflexData = new StateSharedData
{
    basicConfig = { stateName = "DodgeReflex", intKey = 2001 },
    
    isSkill = true,
    skillType = SkillType.Passive,  // 被动技能
    
    // 被动技能无冷却、无消耗
    cooldown = 0f,
    hasCost = false,
    
    tags = new List<string> { "Skill", "Passive", "Defense" },
    
    hasAnimation = false  // 被动技能通常无动画
};
```

### 4.4 持续施法技能

```csharp
var healingChannelData = new StateSharedData
{
    basicConfig = { stateName = "HealingChannel", intKey = 3001 },
    
    isSkill = true,
    skillType = SkillType.Channeled,  // 持续施法
    
    cooldown = 15f,
    hasCost = true,
    costType = "Mana",
    costValue = 10f,  // 每秒消耗
    
    tags = new List<string> { "Skill", "Support", "Channeled" },
    
    hasAnimation = true,
    animationConfig = { 
        clip = healingChannelClip,
        wrapMode = WrapMode.Loop  // 循环播放
    },
    
    // 持续施法需要平滑的淡入淡出
    enableFadeInOut = true,
    fadeInDuration = 0.3f,
    fadeOutDuration = 0.3f
};
```

---

## 5. 热插拔配置

### 5.1 临时状态（热插拔）

```csharp
var knockbackStateData = new StateSharedData
{
    basicConfig = new StateBasicConfig
    {
        stateName = "Knockback",
        intKey = -1,  // -1表示动态分配ID
        pipelineType = StatePipelineType.Main,
        priority = 999  // 超高优先级
    },
    
    // 热插拔配置
    canBeTemporary = true,              // 可作为临时状态
    autoRemoveWhenDone = true,          // 播放完自动移除
    
    hasAnimation = true,
    animationConfig = { 
        clip = knockbackClip,
        wrapMode = WrapMode.Once  // 播放一次
    },
    
    tags = new List<string> { "Temporary", "Hit", "Reaction" }
};

// 运行时添加临时状态
stateMachine.AddTemporaryAnimation(
    "Knockback",
    knockbackClip,
    StatePipelineType.Main,
    speed: 1.0f,
    loop: false
);
```

### 5.2 热更新状态

```csharp
var dashStateData = new StateSharedData
{
    basicConfig = { stateName = "Dash", intKey = 300 },
    
    // 热更新配置
    supportHotReload = true,            // 支持运行时热更新
    keepRuntimeDataOnReload = true,     // 更新时保留播放进度
    allowOverride = true,               // 允许覆盖注册
    notifyOnOverride = true,            // 覆盖时触发回调
    
    hasAnimation = true,
    animationConfig = { clip = dashClip },
    
    tags = new List<string> { "Movement", "HotReload" }
};

// 运行时更新状态
var newDashData = dashStateData.Clone();  // 克隆配置
newDashData.animationConfig.clip = newDashClip;  // 替换动画
stateMachine.RegisterStateFromInfo(newDashData, allowOverride: true);
```

### 5.3 状态覆盖示例

```csharp
// 初始注册
stateMachine.RegisterStateFromInfo(originalStateInfo);

// 后续热更新（覆盖）
var hotfixStateInfo = new StateAniDataInfo
{
    sharedData = new StateSharedData
    {
        basicConfig = { 
            stateName = "Attack",  // 同名状态
            intKey = 200
        },
        allowOverride = true,  // 允许覆盖
        notifyOnOverride = true,  // 触发回调
        
        // 修复后的动画
        hasAnimation = true,
        animationConfig = { clip = fixedAttackClip }
    }
};

bool overridden = stateMachine.RegisterStateFromInfo(hotfixStateInfo, allowOverride: true);
if (overridden)
{
    Debug.Log("状态已热更新！");
}
```

---

## 6. API使用示例

### 6.1 标签操作

```csharp
// 添加标签
stateData.AddTag("Elite");
stateData.AddTag("Boss");

// 检查标签
if (stateData.HasTag("Elite"))
{
    Debug.Log("精英技能");
}

// 移除标签
stateData.RemoveTag("Boss");

// 通过标签查询状态（需要StateMachine支持）
var attackStates = stateMachine.GetStatesByTag("Attack");
```

### 6.2 显示名称

```csharp
// 设置显示名称
stateData.displayName = "烈焰冲击";

// 获取显示名称（未设置则返回状态名）
string displayName = stateData.GetDisplayName(stateData.basicConfig.stateName);
// 输出：烈焰冲击

// UI显示
uiLabel.text = stateData.GetDisplayName("未知技能");
```

### 6.3 激活检查

```csharp
// 检查是否可以激活（技能消耗、冷却等）
if (stateData.CanActivate(out string reason))
{
    stateMachine.TryActivateState("Fireball");
}
else
{
    Debug.LogWarning($"无法激活: {reason}");
    ShowToast(reason);  // 提示玩家：法力不足 / 冷却中
}
```

### 6.4 状态克隆

```csharp
// 克隆配置（用于热更新）
var clonedData = originalData.Clone();

// 修改克隆数据
clonedData.animationConfig.playbackSpeed = 1.5f;
clonedData.basicConfig.priority = 150;

// 使用克隆数据创建新状态
var newStateInfo = new StateAniDataInfo { sharedData = clonedData };
stateMachine.RegisterStateFromInfo(newStateInfo);
```

---

## 7. 最佳实践

### 7.1 命名规范

```csharp
// ✅ 推荐：清晰的命名
stateName = "AttackLight"      // 轻攻击
stateName = "DodgeRoll"        // 闪避翻滚
stateName = "SkillFireball"    // 技能-火球术

// ❌ 避免：模糊的命名
stateName = "State1"
stateName = "Atk"
stateName = "Temp"
```

### 7.2 标签使用

```csharp
// ✅ 推荐：有意义的标签
tags = new List<string> { "Attack", "Melee", "Combo", "Heavy" }

// ❌ 避免：冗余或无意义的标签
tags = new List<string> { "Tag1", "Test", "TODO" }
```

### 7.3 优先级分配

```csharp
// 建议的优先级范围：
// 0-50:   基础移动（Idle, Walk, Run）
// 50-100: 战斗动作（Attack, Block, Dodge）
// 100-200: 技能（Skill, Special）
// 200-500: 受击反馈（Hit, Knockback, Stun）
// 500+:   强制状态（Death, Cutscene）

basicConfig.priority = 10;   // Idle
basicConfig.priority = 80;   // Attack
basicConfig.priority = 150;  // Skill
basicConfig.priority = 300;  // Knockback
basicConfig.priority = 999;  // Death
```

### 7.4 性能优化

```csharp
// 高频状态：启用常驻内存
var idleData = new StateSharedData
{
    keepInMemory = true,      // 常驻内存
    canBeTemporary = false    // 不作为临时状态
};

// 低频状态：按需加载
var rareSkillData = new StateSharedData
{
    keepInMemory = false,     // 不常驻
    canBeTemporary = true,    // 可临时加载
    autoRemoveWhenDone = true // 用完自动卸载
};
```

### 7.5 淡入淡出优化

```csharp
// 快速动作：短淡入淡出
var dodgeData = new StateSharedData
{
    enableFadeInOut = true,
    fadeInDuration = 0.05f,   // 极快淡入
    fadeOutDuration = 0.1f
};

// 慢动作：长淡入淡出
var meditateData = new StateSharedData
{
    enableFadeInOut = true,
    fadeInDuration = 0.5f,    // 慢慢淡入
    fadeOutDuration = 0.5f
};

// 受击反馈：瞬间切换
var hitData = new StateSharedData
{
    enableFadeInOut = false   // 禁用过渡，立即切换
};
```

---

## 8. 技能系统集成示例

### 8.1 完整技能配置

```csharp
public class SkillDatabase : ScriptableObject
{
    public List<StateAniDataInfo> skills;
}

// 配置技能数据库
var skillDB = ScriptableObject.CreateInstance<SkillDatabase>();

// 技能1：火球术
skillDB.skills.Add(new StateAniDataInfo
{
    sharedData = new StateSharedData
    {
        basicConfig = { stateName = "Fireball", intKey = 1001 },
        isSkill = true,
        skillType = SkillType.Active,
        cooldown = 5f,
        cooldownGroup = "Fire",
        hasCost = true,
        costType = "Mana",
        costValue = 30f,
        tags = new List<string> { "Skill", "Fire", "Range" },
        displayName = "火球术",
        description = "发射火球，造成范围伤害",
        icon = fireballIcon
    }
});

// 技能2：冰冻新星
skillDB.skills.Add(new StateAniDataInfo
{
    sharedData = new StateSharedData
    {
        basicConfig = { stateName = "FrostNova", intKey = 1002 },
        isSkill = true,
        skillType = SkillType.Active,
        cooldown = 8f,
        cooldownGroup = "Ice",
        hasCost = true,
        costType = "Mana",
        costValue = 50f,
        tags = new List<string> { "Skill", "Ice", "AOE", "Control" },
        displayName = "冰冻新星",
        description = "冰冻周围敌人",
        icon = frostNovaIcon
    }
});
```

### 8.2 运行时注册技能

```csharp
public class SkillSystemManager : MonoBehaviour
{
    public SkillDatabase skillDB;
    public StateMachine stateMachine;
    
    void Start()
    {
        // 批量注册技能
        foreach (var skillInfo in skillDB.skills)
        {
            if (skillInfo.sharedData.isSkill)
            {
                stateMachine.RegisterStateFromInfo(skillInfo);
                Debug.Log($"注册技能: {skillInfo.sharedData.GetDisplayName()}");
            }
        }
    }
    
    // 释放技能
    public bool CastSkill(string skillName)
    {
        var skillData = GetSkillData(skillName);
        if (skillData == null) return false;
        
        // 检查是否可以激活
        if (!skillData.CanActivate(out string reason))
        {
            ShowCastFailMessage(reason);
            return false;
        }
        
        // 激活技能状态
        return stateMachine.TryActivateState(skillName);
    }
    
    StateSharedData GetSkillData(string skillName)
    {
        var skillInfo = skillDB.skills.Find(s => s.sharedData.basicConfig.stateName == skillName);
        return skillInfo?.sharedData;
    }
}
```

---

## 9. 常见问题

### Q1: 如何让状态支持热更新？

```csharp
// 设置热更新标记
stateData.supportHotReload = true;
stateData.allowOverride = true;

// 注册时允许覆盖
stateMachine.RegisterStateFromInfo(stateInfo, allowOverride: true);
```

### Q2: 临时状态和普通状态的区别？

```csharp
// 临时状态：用于临时效果（受击、击飞等）
canBeTemporary = true;
autoRemoveWhenDone = true;

// 普通状态：持久存在于状态机中
canBeTemporary = false;
```

### Q3: 如何自定义淡入淡出效果？

```csharp
// 使用AnimationCurve自定义曲线
fadeInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);  // 缓入缓出
fadeOutCurve = new AnimationCurve(
    new Keyframe(0, 1),
    new Keyframe(0.5f, 0.5f),
    new Keyframe(1, 0)
);
```

### Q4: 技能连击如何配置？

```csharp
// 第一个技能
supportCombo = true;
comboNextSkills = new List<string> { "Skill2", "Skill3" };
comboWindow = 0.8f;  // 0.8秒窗口期

// 后续技能也需要配置
```

---

## 10. 总结

### 核心优势

1. ✅ **清晰易懂**：去除高级词汇，使用自然的命名
2. ✅ **功能完整**：支持技能、热插拔、自定义曲线
3. ✅ **高性能**：支持热更新、临时状态、状态克隆
4. ✅ **易扩展**：标签系统、分组系统、便捷API

### 快速上手

1. 创建StateSharedData配置基础信息
2. 根据需要启用动画、技能、热插拔
3. 使用便捷API操作标签、检查激活条件
4. 注册到StateMachine并使用

**祝开发顺利！** 🚀
