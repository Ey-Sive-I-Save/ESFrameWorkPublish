# 一流 Mod 方案与技能系统思路（结合当前 ES 项目）

## 1. Mod 总体目标

- 允许玩家或关卡设计者：
  - 定义新的角色（属性、外观、行为标签）；
  - 定义新的物品（属性、效果、稀有度、掉落规则）；
  - 定义新的任务（前置条件、目标、奖励、剧情脚本）。
- 要求：
  - **数据驱动**：尽量通过 ScriptableObject + Json 等形式承载配置；
  - **热插拔**：Mod 可独立打包/启用/禁用；
  - **安全隔离**：限制 Mod 只在数据和受控脚本接口层面扩展，而不是随意执行任意 C# 代码。

## 2. 结合现有框架的落地方向

### 2.1 基于 ResLibrary/ResBook/ResPage 的资源组织

- 使用 ResLibrary 表示一个“资源 Mod 包”，例如：
  - `CoreGame.reslib`：基础游戏资源；
  - `UserMod_Xxx.reslib`：某个玩家 Mod 的独立资源库。
- 每个 ResBook 可以代表一个大类，比如：
  - 角色资源（角色 Prefab、动画、图标）；
  - 物品资源（图标、Prefab、特效）；
  - 任务/剧情资源（对话文本、Timeline、音频）。
- ResPage 再细分到具体子范围，例如某个职业、某条任务线等。

> 在 AIPreview 中可以增加一个 `ResLibraryPreviewWindow`，支持按 Mod 维度浏览所有 Library/Book/Page。

### 2.2 Mod 定义结构（建议）

- `ModDefinition`（ScriptableObject）：
  - ModId / 名称 / 版本 / 作者 / 依赖的其他 ModId 列表；
  - 入口 ResLibrary 引用；
  - 声明“本 Mod 提供哪些系统级扩展”：角色 / 物品 / 任务 / UI / 音效等。
- `CharacterDefinition` / `ItemDefinition` / `QuestDefinition` 等：
  - 统一放在 Mod 的 ResLibrary 中，通过特定命名规则 + SoLibrary/Book/Page 分类；
  - 游戏启动时扫描所有启用的 ModDefinition，构造全局“角色表/物品表/任务表”。

### 2.3 加载与卸载流程

1. 启动时加载一个 `ModConfig`：
   - 记录当前启用的 ModId 列表及顺序（顺序可用于解决冲突时的优先级）。
2. 根据 `ModConfig` 依次加载各 ModDefinition：
   - 使用 ESResMaster/ESResLoader 按需拉起相关 AB；
   - 注册对应的定义资产到全局表中。
3. 当玩家在 Mod 菜单中启用/禁用某个 Mod：
   - 更新 `ModConfig`；
   - 对应地增量加载/卸载 ResLibrary；
   - 通过 Link 体系发出 "ModChanged" 消息，让其他系统（UI、任务管理、存档系统）感知。

## 3. 技能系统开发思路（与 Mod 协同）

### 3.1 核心抽象

- `SkillDefinition`（ScriptableObject）：
  - 基本信息：Id、名称、图标、描述、冷却、消耗、施法距离等；
  - 目标筛选：单体/范围/自己/队友/敌人等枚举 + 参数；
  - 效果列表：由若干 `SkillEffect` 子资产组成；
  - 播放配置：动画剪辑名、特效 Prefab、声音资源索引（可对接 ResLibrary）。
- `SkillEffect`（ScriptableObject）：
  - 类型：伤害/治疗/位移/状态/召唤/脚本事件等；
  - 参数：数值、持续时间、叠加规则等。

### 3.2 运行时执行层

- `SkillRunner`：纯 C# 运行时模块，用于：
  - 校验施法条件（冷却、资源、距离、角度等）；
  - 按 `SkillDefinition` 播放动画/特效；
  - 逐个应用 `SkillEffect` 到目标。
- 可以复用 Link 体系：
  - 广播 `OnSkillCastStart`、`OnSkillHit`、`OnSkillEnd` 等 Link，供 UI/音效/任务系统监听。

### 3.3 与 Mod 的结合

- 允许 Mod 提供新的 SkillDefinition 与 SkillEffect：
  - Mod 的 ResLibrary 中增加专门的 Book/Page 放技能；
  - 加载 Mod 时扫描所有 `SkillDefinition`，并注册到全局技能表；
  - 任务或角色配置中只引用 SkillId，而不关心其来源是“本体”还是 Mod。

### 3.4 安全性约束

- 尽量避免让 Mod 直接注入 C# 逻辑（如任意脚本继承某个接口）；
- 将可扩展点控制在：
  - 数据参数（数值、标志、特效资源名）；
  - 受控枚举（效果类型、目标规则）；
  - 由底层约定好的“脚本事件 Id”（例如 TriggerId），由主工程中的脚本统一解释。

## 4. 小结

- Mod 方案建议以 **ResLibrary + ScriptableObject 定义 + 全局注册表** 为基础：
  - 结构清晰、编辑器友好，天然适配你当前的 ESRes 体系；
  - 容易做可视化与导出工具。
- 技能系统则建议：
  - 抽象出独立的 SkillDefinition / SkillEffect / SkillRunner；
  - 用 Link 作为跨系统的消息桥梁；
  - 通过 ModDefinition + ResLibrary 将技能定义纳入 Mod 流程管理。
