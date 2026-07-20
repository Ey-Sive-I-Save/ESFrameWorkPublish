# 通用架构理解：跨系统纠偏 AI 协作警告

职责：把 2026-07-18 多份 AIWarnings 的共同结论抽象成跨系统原则。后续 AI 在改输入、ValueChange、Buff、Entity、Item、StateMachine、GameManager、AITalk 前，都应该先读本文，避免用陈旧“大一统”思路误改工程。

最后核对时间：2026-07-18。

## 已参考的现有警告

- `InputRuntime/输入与交互入口_AI协作警告.md`
- `PlayerArchitecture/模型重构_今日修正_CoreDomain与AI域控制_AI协作警告.md`
- `玩家运动_PlayerMotion_AI协作说明.md`
- `AI协作职责_状态机与IK上层_Buff边界说明.md`
- `GameManager_SaveSystem/架构体系_ESGameManager_SaveSystem_AI协作警告.md`
- `CodexNotes/Codex_工具重写_商业级验证协作上下文.md`

## 今天的新的一般理解

ES 当前最重要的方向不是再造一个更大的“总系统”，而是让每个底层协议清楚、可释放、可缓存、可桥接。

更准确的理解是：

```text
底层协议管规则。
Domain 管域级协调。
Module 管具体能力。
Facade 管结构和入口，不当新大脑。
Binding 管跨层翻译。
Runtime State 管运行变量。
DataInfo 管配置声明。
Editor 管制作体验，不改变运行时语义。
```

如果一个方案看起来“什么都能管”，它通常已经开始危险。

## 必须纠正的陈旧思想

### 1. 不要用一个大外壳接管 Entity

`CharacterActorFacade / LifeActorFacade` 这类概念只能作为结构入口、引用聚合、旧系统桥接。它不能替代 `Core -> Domain -> Module`，也不能把输入、AI、剧情、网络、运动、状态机、Buff 都收进自己内部。

正确倾向：

```text
外壳：薄，管结构和引用。
AI域：管控制来源和控制请求。
Basic域：管身体能力和执行。
State域：管状态、动画、IK表现。
Buff域：未来管效果、限制、叠层、来源、驱散。
```

### 2. 不要把表现层当逻辑层

`StateLayerType.Buff` 是动画/表现层，不是完整 Buff 系统。

StateMachine 可以表达：

- 动画状态
- 过渡
- IK 姿态贡献
- 技能序列表现
- Buff 姿态表现

StateMachine 不应该承担：

- 属性数值合成
- Buff 叠层规则
- 驱散规则
- 权限仲裁
- 伤害结算
- 背包/装备规则

表现桥接可以有，但逻辑主权不要反向塞进表现系统。

### 3. 不要让 Op / Expression / Buff / ValueChange 混成一团

推荐边界：

```text
ValueChange：持续值变化、权限合成、缓存最终值。
Expression：计算动态值，刷新时 Evaluate 成 primitive。
Op：一次性动作或成对 Start/Stop 的行为。
Buff：生命周期、来源、持续时间、叠层、驱散、事件绑定。
```

常规 Buff 可以用 Pack / Group / Info 声明，但 DataInfo 只放配置，运行变量必须进 `BuffInstance` 或等价 runtime state。

不要让 Change 持有 Expression、Unity Object、字符串 key、目标 Entity 引用。热结构只放 primitive 和 token。

### 4. 不要把高频路径交给通用逻辑器

Item / Shot / 飞行物可以用 Op + Expression 编排生命周期事件，但高频 Tick 必须保持纯粹。

高频运动只做：

- 运动求解
- 命中候选
- 到达/过期/停止事件
- 固定缓冲 NonAlloc 检测

不要每帧执行：

- Op 链
- 复杂 Expression
- 反射
- LINQ
- 字符串查找
- 动态 GetComponent
- 动态模块查找
- 临时集合分配

事件发生时再进入 Op/Expression/Support 编排。

### 5. 不要为了统一而增加大量 Domain

Domain 是大边界，不是能力点。能力点优先放 Module。

例如 Item 当前更合理的是：

```text
Item : Core
  ItemBasicDomain
    ItemMotionModule
    ItemProjectileModule / ItemShotModule
    ItemLogicModule
```

不要为了“看起来完整”拆出一堆 `ItemMotionDomain / ItemCollisionDomain / ItemLifetimeDomain / ItemPresentationDomain`。

### 6. 不要替换可被业务缓存的服务对象

输入系统已经明确：业务可以缓存 `ESInputService`。运行时改键、切方案、重建配置时，应替换内部表、输入源和缓存，不应替换服务对象本身。

这个原则可以推广：

- 高频服务对象要稳定。
- 配置重建尽量换内部数据。
- 对外引用一旦允许缓存，就不能随便替换。
- 如果必须替换，要提供明确 Rebind 语义。

### 7. 不要把 Runtime State 放进可复用 Player / Calculator / Data

之前 RuntimePlayer 共享状态问题已经说明：缓存对象如果按 sequence 或配置复用，就不能存 `ownsTarget / activeTarget / originalActive` 这类实体运行变量。

通用规则：

```text
配置对象：可共享，只放配置。
Player/Calculator：可缓存，只放结构和配置引用。
State.UserData / Instance：放实体本次运行变量。
DataInfo：只声明。
Runtime Instance：存 token、计时、叠层、目标、临时状态。
```

任何“多实体同时播放同一份配置”的场景，都必须先检查运行变量是否串号。

### 8. Entity 只做入口，不要摊平 Domain

今天已经确认：`Entity` 的 Inspector 只负责自己的总览和入口排版，`Domain` 必须保留自己的完整绘制。

正确规则：

```text
Entity：
  只放自身总览、关系链、KCC 等自己的东西。
  四个 Domain 只提供清晰入口，不接管 Domain 内部排版。

Domain：
  自己负责完整 Inspector。
  自己管理自己的标题、分组、折叠、字段顺序。
  不要被 Entity 展平成普通字段。
```

不要再做这些事：

- 把 Domain 的内部字段塞进 Entity 统一排版。
- 用 `InlineProperty` 把 Domain 摊平成 Entity 的子字段。
- 为了“整齐”让 Entity 接管 Domain 的标题和折叠结构。

这条原则适用于 `EntityBasicDomain`、`EntityAIDomain`、`EntityBuffDomain`、`EntityStateDomain`。

## 对未来 Buff 系统的一般判断

可以开始设计 Buff，但不要绕过 ValueChange。

最低正确路线：

```text
ValueChange / Permit
  -> Stat 聚合
  -> BuffDefinition
  -> BuffInstance
  -> Expression Binding
  -> Op Trigger
  -> VisualBridge
  -> Editor / 诊断
```

Buff 系统要商业级，关键不是先堆 100 个 Buff 类型，而是先保证：

- token 可释放
- owner/source 可批量清理
- 叠层可解释
- 优先级可解释
- Expression 不进热路径
- Op 生命周期能 Stop / Dispose
- 表现层和数值层不互相污染

## 对 AITalk 的一般判断

AITalk 的价值不是“写几个协作文档”，而是给多个 AI 一个可持续轮询、可沉淀共识、可回报用户的纯文本协议。

通用原则：

- 必须给工程绝对路径和 Session 绝对路径。
- 必须读协议、会话说明、参与者、历史消息。
- 必须持续轮询，不要写一条就结束。
- 必须声明代码修改权限。
- 必须区分公开信息和私密信息。
- 必须让主持者拥有权威身份分配权。
- 最终结论必须主动回报用户。

PlayMode 不是狼人杀专用。狼人杀、MOBA、架构评审、方案辩论都只是规则模板。

## 跨系统自检清单

改任何运行时架构前，先问：

- 这个类是在管配置、运行实例、服务、桥接，还是表现？
- 是否把运行变量放进了可共享对象？
- 是否把表现层当成逻辑层？
- 是否为了统一新建了过多 Domain / 脚本？
- 高频路径是否有反射、LINQ、字符串、临时集合、动态查找？
- 是否有 token / owner / source / version，能否完整释放？
- 是否允许业务缓存引用？如果允许，重建时是否会替换对象？
- 是否把 Expression 放进了热路径？
- 是否修改了运行时语义来解决编辑器排版问题？
- 是否读了当前源码，而不是只按旧讨论继续写？

## 一句话原则

不要追求“一个系统管所有东西”。商业级架构更像一组边界清楚的协议：配置可复用，运行态可释放，热路径可缓存，表现可桥接，编辑器可诊断。
