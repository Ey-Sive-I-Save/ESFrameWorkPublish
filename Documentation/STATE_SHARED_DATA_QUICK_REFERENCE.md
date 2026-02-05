# StateSharedData 快速参考卡片

> **一页纸速查手册** - 打印或保存到桌面快速查阅  
> **更新：** 技能系统已移除（技能是状态之上的另一层），简化专业词汇

---

## 🎯 核心字段速查

| 字段名 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `basicConfig` | StateBasicConfig | 必填 | 状态名称、ID、流水线、优先级 |
| `hasAnimation` | bool | false | 是否启用动画 |
| `enableFadeInOut` | bool | true | 是否启用平滑过渡 |
| `fadeInDuration` | float | 0.2f | 淡入时长（秒） |
| `fadeOutDuration` | float | 0.15f | 淡出时长（秒） |
| `fadeInCurve` | AnimationCurve | Linear | 淡入曲线 |
| `fadeOutCurve` | AnimationCurve | Linear | 淡出曲线 |

---

## 🔄 运行时替换速查

| 字段名 | 类型 | 说明 |
|--------|------|------|
| `canReplaceAtRuntime` | bool | 允许在游戏运行时替换配置 |
| `keepDataOnReplace` | bool | 替换时保留运行数据 |
| `canBeTemporary` | bool | 可作为临时状态 |
| `autoRemoveWhenDone` | bool | 播放完自动移除 |
| `allowOverride` | bool | 允许覆盖注册 |
| `notifyOnOverride` | bool | 覆盖时触发通知 |

---

## 📝 便捷API速查

```csharp
// 标签操作
HasTag(string tag)              // 检查是否有标签
AddTag(string tag)              // 添加标签
RemoveTag(string tag)           // 移除标签

// 显示名称
GetDisplayName(string fallback) // 获取显示名称

// 克隆
Clone()                         // 克隆配置（用于运行时替换）
```

---

## 🏊 对象池支持

```csharp
// StateBase 实现了 IPoolableAuto 接口
var state = StateBase.Pool.GetInPool();  // 从池中获取
state.TryAutoPushedToPool();             // 回收到池中

// 对象池配置
容量: 500个对象
预热: 10个初始对象
自动重置: 自动清理运行时数据
```

---

## ⚡ 常用配置模板

### 基础移动状态
```csharp
basicConfig = { stateName = "Walk", intKey = 101 }
hasAnimation = true
enableFadeInOut = true
fadeInDuration = 0.2f
tags = ["Movement", "Locomotion"]
```

### 攻击动作
```csharp
basicConfig = { stateName = "Attack", intKey = 200, priority = 80 }
hasAnimation = true
enableFadeInOut = true
fadeInDuration = 0.1f
fadeOutDuration = 0.15f
tags = ["Attack", "Combat"]
```

### 临时状态（受击）
```csharp
basicConfig = { stateName = "Knockback", intKey = -1, priority = 999 }
canBeTemporary = true
autoRemoveWhenDone = true
hasAnimation = true
enableFadeInOut = false  // 瞬间切换
tags = ["Temporary", "Hit"]
```

### 运行时替换状态
```csharp
canReplaceAtRuntime = true
keepDataOnReplace = true
allowOverride = true
notifyOnOverride = true
```

---

## 🎨 淡入淡出曲线预设

```csharp
// 线性（默认）
AnimationCurve.Linear(0, 0, 1, 1)

// 缓入缓出（平滑）
AnimationCurve.EaseInOut(0, 0, 1, 1)

// 快速淡入
new AnimationCurve(
    new Keyframe(0, 0, 0, 3),
    new Keyframe(1, 1, 0, 0)
)

// 慢速淡出
new AnimationCurve(
    new Keyframe(0, 1, 0, 0),
    new Keyframe(1, 0, -0.5f, 0)
)
```

---

## ⚙️ 优先级参考

| 范围 | 用途 |
|------|------|
| 0-50 | 基础移动（Idle, Walk, Run） |
| 50-100 | 战斗动作（Attack, Block, Dodge） |
| 100-200 | 技能（Skill, Special） |
| 200-500 | 受击反馈（Hit, Knockback, Stun） |
| 500+ | 强制状态（Death, Cutscene） |

---

## 🏷️ 推荐标签

```csharp
// 通用类别
"Movement", "Combat", "Buff", "Debuff"

// 攻击类型
"Melee", "Range", "AOE", "Single"

// 特殊标记
"Temporary", "Elite", "Boss"
```

---

## ⚠️ 常见错误

| 错误 | 原因 | 解决方案 |
|------|------|----------|
| 状态不切换 | 优先级过低 | 提高priority值 |
| 动画不播放 | hasAnimation=false | 设为true |
| 过渡太突兀 | enableFadeInOut=false | 设为true并调整时长 |
| 运行时替换无效 | allowOverride=false | 设为true |
| 内存泄漏 | 忘记回收对象 | 使用TryAutoPushedToPool() |

---

## 📞 快速支持

- 📄 完整文档：[STATE_SHARED_DATA_USAGE_GUIDE.md](STATE_SHARED_DATA_USAGE_GUIDE.md)
- 📊 系统分析：[ES_STATE_SYSTEM_ANALYSIS.md](ES_STATE_SYSTEM_ANALYSIS.md)
- 🔧 API文档：见代码注释

---

**版本：** v2.1 - 简化版  
**更新：** 2026年2月4日  
**变更：** 移除技能系统（独立层），简化专业词汇，添加对象池支持  
**适用：** ES Framework State System
