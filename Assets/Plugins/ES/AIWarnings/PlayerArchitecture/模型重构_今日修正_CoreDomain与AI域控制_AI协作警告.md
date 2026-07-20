# 模型重构_今日修正_CoreDomain与AI域控制_AI协作警告

职责：服务于玩家/角色/所有生命体模型重构。本文专门纠正 2026-07-18 讨论中暴露的陈旧或不准确理解，避免后续 AI 把“外壳”“控制请求”“控制权”误做成一套脱离 ES 框架的大系统。

最后核对时间：2026-07-18。

## 已核对路径

- `Assets/Plugins/ES/1_Design/Core_Domain_Module/Core.cs`
- `Assets/Plugins/ES/1_Design/Core_Domain_Module/Domain.cs`
- `Assets/Plugins/ES/1_Design/Core_Domain_Module/Module.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/AI/_EntityAIDomain.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/AI/EntityAIModules.cs`
- `Assets/Plugins/ES/AITalk/Sessions/20260717_205728_生命体兼容模型重构/Consensus/最终结论_返回用户.md`

## 必须纠正的旧想法

### 1. 不要说 Core / Domain 没有逻辑能力

源码没有看到“Core 和 Domain 不具备逻辑能力”的强制规定。

实际情况：

```text
Core:
  Awake / Update / OnEnable / OnDisable / OnDestroy
  注册 Domain
  调度 Domain

Domain:
  Update / OnEnable / OnDisable / OnDestroy / FixedUpdateExpand
  调度 Module
  可以维护域级缓存、域级规则、域级协调逻辑

Module:
  Start / Update / OnEnable / OnDisable / OnDestroy / FixedUpdateExpand
  实现具体功能
```

所以后续 AI 不要机械地认为“所有逻辑必须在 Module，Domain 只能当容器”。更准确的说法是：

```text
Core 可以有总入口和少量总控。
Domain 可以有域级协调逻辑。
Module 放具体功能实现。
```

限制不是“Domain 不能写逻辑”，而是“Domain 不应膨胀成巨型具体实现类”。

### 2. “外壳”不是新控制系统

之前讨论中的 `CharacterActorFacade / LifeActorFacade` 容易被误解成：在 `Entity` 外面新建一套大控制系统，把玩家、AI、剧情、网络控制权全部上交给外壳。

这个理解需要纠正。

当前更合适的定位：

```text
角色外壳：
  规范整体角色结构
  管引用关系
  提供统一入口
  做旧 Entity 与新模板之间的桥
  不抢 Domain / Module 的具体职责
```

外壳可以存在，但不要让它变成“新大脑”。项目已有 `Core -> Domain -> Module`，控制来源更应该落在合适的 Domain/Module 中。

### 3. 控制来源优先考虑 AI 域，而不是外壳独立接管

如果本项目里的 `EntityAIDomain` 语义是“意识 / 输入 / 调度 / 控制来源”，而不是狭义怪物 AI，那么控制来源和控制请求整理放在 AI 域是合理的。

建议职责：

```text
EntityAIDomain:
  管意识、输入、AI、控制来源、控制请求整理

EntityBasicDomain:
  管身体能力、运动、KCC、跳跃、攀爬、游泳、骑乘、部分战斗执行

EntityStateDomain:
  管状态机、动画状态、状态与 IK 表现关系

EntityBuffDomain:
  未来管 Buff 逻辑、限制条件、叠层、来源、驱散等
```

推荐链路：

```text
玩家输入 / AI决策 / 剧情接管 / 网络同步 / 载具控制 / 状态限制
  -> EntityAIDomain 收集和仲裁控制来源
  -> 输出本帧控制请求
  -> BasicDomain / StateDomain / Skill 或战斗模块执行
```

不要把“控制权判断”默认做成外壳里的独立大系统。

### 4. 不需要一开始加很多脚本

“统一控制请求”和“判断谁能控制”不等于马上创建大量脚本。

第一阶段应尽量少：

```text
角色外壳：可选，很薄，只管结构和引用。
AI域控制来源模块：1 个小模块即可起步。
控制请求数据：1 个轻量 struct/class 即可起步。
```

不要一开始创建：

```text
玩家控制脚本
AI控制脚本
剧情控制脚本
网络控制脚本
眩晕控制脚本
载具控制脚本
技能控制脚本
```

这些可以未来按需求拆。现在先把入口统一起来。

## 当前推荐落点

优先方案：

```text
Entity
  EntityAIDomain
    EntityControlSourceModule      // 当前控制来源、优先级、允许能力
    EntityControlRequestModule     // 本帧整理后的移动/朝向/技能/交互请求
```

如果不想新增 Domain，这比新增外部大系统更贴近现有代码。

如果以后发现 AI 域名称不适合承载“玩家/网络/剧情控制来源”，再考虑拆出：

```text
EntityControlDomain
  EntityControlSourceModule
  EntityControlRequestModule
```

但不要在没有验证前直接大规模新增 Domain 和脚本。

## 控制请求的通俗定义

控制请求不是复杂新系统，只是把“想让角色做什么”整理成一份本帧结果。

例如：

```text
想移动到哪个方向
想看向哪里
是否跳跃
是否交互
是否释放技能
是否被剧情接管
是否因为眩晕禁止移动/技能
```

执行仍由现有系统负责：

```text
移动 -> Entity / KCC / BasicDomain
技能 -> 技能模块 / 状态机
动画表现 -> StateDomain / StateMachine / IK Driver
相机 -> 相机系统绑定角色目标点
```

## 对场景层级模板的修正理解

`【必须】玩家_大黑塔_工业级层级模板` 的设计思路可保留。它的价值是把：

```text
总根
运行时逻辑
运动碰撞
模型表现
IK/挂点
装备
特效音频
相机参考点
运行时生成区
```

分开。

但它不是已经跑通的工业级 prefab。它更像说明模板。未来要支撑 MMO、RPG、MOBA、FPS、剧情、Boss、载具、投射物，需要补：

```text
控制来源
控制请求
基础模板 + 变体
Runtime / Save / Network 快照边界
```

注意：这些补充不要求场景对象立刻增加大量节点或脚本。优先在 AI 域/模块和少量数据结构里验证链路。

## 高性能警告

- 不要每帧 `Find` 层级节点。
- 不要每帧反射。
- 不要每帧字符串查找。
- 不要每帧动态 `GetMoudle<T>()` 找热路径模块；初始化时缓存引用。
- 控制请求尽量用 struct 或复用对象，避免每帧 new 一堆 class/list。
- Domain 可以协调，但具体高频实现不要堆进 Domain 巨型方法。

## 以后 AI 不要做的事

- 不要把“所有生命体架构”理解成推翻 `Entity`。
- 不要把“外壳”理解成接管所有控制逻辑。
- 不要继续把所有新功能塞进 `EntityBasicModules.cs`。
- 不要说 Core / Domain 没有逻辑能力。
- 不要把 AI 域只理解成怪物 AI；当前已有输入采集、输入调度等控制来源职责痕迹。
- 不要为了统一而让投射物、道具、载具使用完整玩家/角色模板。

## 当前更正后的简版原则

```text
外壳管结构，不当新大脑。
AI域管控制来源和控制请求。
Basic域管身体执行。
State域管状态和表现。
Buff域未来管限制和效果。
Core/Domain/Module 都能有逻辑，但职责要分清。
先少量验证，不要铺很多新脚本。
```

