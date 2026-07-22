# 玩家运动 / Item 飞行物 / Op 生命周期协作说明

> 负责 AI：Codex 运动方向。职责是跑通玩家底层运动、场景交互闭环，以及 Item/飞行物的运动与生命周期架构。本文给后续 AI 快速建立正确上下文；实现前仍必须回读源码。

## 当前总判断

- 世界大型逻辑体只收敛为两类：`Entity` 与 `Item`。
- `Entity` 负责生命体：玩家、NPC、怪物，当前高频运动主线仍是 KCC。
- `Item` 负责非生命体世界逻辑体：飞行物、掉落物、机关、场景逻辑物、持续区域、召唤物表现体等。
- `Item` 不应该只是“运动物体”。更准确地说，`Item` 是一个可拥有完整生命周期的世界逻辑体。
- `Item` 的全生命周期可以融合 Expression + Op 系统，成为一个事件逻辑器；但高频运动 Tick 不能交给 Op。

## 必须纠正的陈旧思想

- 不要再引入 `ESMotionBody` 这类与 `Entity/Item` 并列的大根。
- 不要恢复 `Assets/Scripts/ESLogic/Runtime/Movement` 下的 `IESMotionDriver / ESMotion*` 旧方案。
- 不要把 `Item : Core, IESMotionDriver` 当正确方向。当前正确方向是 `Item : Core`，能力进入 Domain/Module。
- 不要让飞行物成为散落的独立 `MonoBehaviour` 闭环。
- 不要拆出一堆 `ItemMotionDomain / ItemCollisionDomain / ItemLifetimeDomain / ItemPresentationDomain`。Domain 是大边界，Module 才是能力点。
- 不要把飞行物模块写成“技能、伤害、Buff、VFX、音效、对象池、全局调度”全包模块。
- 不要把 OpSupport 当全局垃圾桶。谁拥有生命周期，谁持有并清理自己的 Support。

## 当前 Item 结构

```text
Item : Core
└── ItemBasicDomain
    ├── ItemMotionModule
    ├── ItemShotModule / ItemShotModule
    └── ItemLogicModule        // 规划方向：生命周期事件转 Op
```

当前源码位置：

- `Assets/Scripts/ESLogic/Runtime/Item/Item.cs`
- `Assets/Scripts/ESLogic/Runtime/Item/Domains/Basic/_ItemBasicDomain.cs`
- `Assets/Scripts/ESLogic/Runtime/Item/Domains/Basic/ItemBasicModules.cs`
- `Assets/Scripts/ESLogic/Runtime/Item/Domains/Basic/ShotMotionTypes.cs`
- `Assets/Scripts/ESLogic/Runtime/Item/Domains/Basic/ShotMotionSolver.cs`

## 飞行物职责边界

飞行物运动层只负责：

- 怎么飞
- 撞到谁或可能撞到谁
- 什么时候到达
- 什么时候过期
- 什么时候停止
- 输出运动事件和命中候选

飞行物运动层不负责：

- 伤害
- Buff
- 技能消费
- VFX
- 音效
- 对象池回收
- 全局调度策略

一句话：飞行物层负责“飞、撞、停”；战斗、表现、回收由外部消费事件处理。

## Shot 命名建议

后续如果继续扩展，建议逐步把飞行物业务命名简化为 `Shot`，比 `Shot` 更短、更通用：

```text
Shot       // 一次飞行物实例或运行态
ShotData   // 配置
ShotState  // 当前状态
ShotMove   // 运动配置
ShotHit    // 命中配置
ShotLife   // 生命周期配置
ShotEvent  // 输出事件
ShotSolver // 纯运动求解
```

`Shot` 可覆盖子弹、箭、法球、导弹、投掷物、激光段、技能飞行体、必中表现体。

## 必中不是特例

必须支持必中。必中不是碰撞系统的临时 hack，而是一种合法模式。

推荐语义：

```text
Free      // 自由飞行，靠空间检测命中
Target    // 锁定目标，朝目标飞
MustHit   // 战斗层已决定必中，飞行只是表现
Scan      // 瞬时扫描，如射线/激光
```

阻挡规则也要独立：

```text
None       // 不被阻挡
WorldOnly  // 只被地形/墙阻挡
AnyBlocker // 任意阻挡体可阻挡
```

示例：

- 治疗飞弹：`MustHit + None`
- 锁定火球：`MustHit + WorldOnly`
- 真实箭矢：`Free + AnyBlocker`
- FPS 子弹：`Scan + AnyBlocker`

## 层级管理

不要只依赖 Unity `LayerMask`。推荐分两层：

```text
Unity Layer：物理粗过滤
Game Layer：阵营、归属、目标类型、可命中规则
```

飞行物可以读取轻量目标接口，例如：

```text
Id
OwnerId
Side
Kind
```

但不要让飞行物直接理解完整阵营/仇恨/战斗系统。它只输出候选，上层系统最终裁决。

## 随机性与网络

影响逻辑的随机必须可重放，不允许直接用 `UnityEngine.Random`。

每个 Shot 至少应能关联：

```text
shotId
seed
spawnTick
ownerId
targetId
dataId
```

随机分两类：

- `LogicRandom`：影响命中、散射、轨迹、反弹，必须由 seed 决定。
- `ViewRandom`：只影响特效、音效、抖动，可不参与网络校验。

目标是：同一个 seed + 同一组发射参数 + 同一个 tick，应得到同一个逻辑结果。

## Expression + Op + Support 的结论

可以把 `Item` 全生命周期当成一个 ESLogicer 风格的事件逻辑体。

推荐关系：

```text
ItemShotModule：飞、撞、停，产生事件
ItemLogicModule：消费事件，执行 Op
Expression：发射时或事件时计算参数/条件
OpSupport：跟随 Item 生命周期保存上下文
```

标准 Op 执行三件套：

```text
ESOutputOp
ESRuntimeTargetPack
ESOpSupport
```

标准入口：

```text
op._TryStartOp(targetPack, scopeSupport, hostSupport)
op._TryStopOp(targetPack, scopeSupport, hostSupport)
```

一次性事件只 Start；持续型事件必须 Start/Stop 成对。

## Support 生命周期原则

`Entity`、`EntitySkill`、`Item`、`Buff` 都可以符合 OpSupport 使用场景，但身份语义必须分清：

```text
EntitySupport：角色长期逻辑
SkillSupport：一次技能释放周期
ItemSupport：Item 全生命周期、飞行物事件、持续区域
BuffSupport：Buff 生命周期、周期触发、结束清理
```

硬规则：

- 谁拥有生命周期，谁持有 Support。
- 谁触发 Op，谁组装 `ESRuntimeTargetPack`。
- 谁结束生命周期，谁 Stop 并清理 Op。
- Support 可以切换，但必须显式切换。
- 切换 Support 时，TargetPack 应复制或新建，不要原地污染旧上下文。

典型链路：

```text
SkillSupport
  -> 生成飞行物 Item
  -> 切到 ItemSupport
  -> Item OnHit
  -> 可再切到 Target EntitySupport
```

这能形成跨生命周期逻辑流，但不能让高频运动进入 Op 链。

## 性能警告

高频 Tick 禁止：

- LINQ
- 反射
- 字符串查找
- 每帧 `GetComponent`
- 每帧 new 数组
- 每帧动态扩容 List
- 每帧跑复杂 Expression
- 每帧执行 Op 链

推荐：

- 发射时计算 Expression 并缓存结果。
- Tick 时只跑纯 Solver 和 NonAlloc 命中检测。
- 事件发生时才执行 Op。
- 命中使用固定缓冲。
- 位姿只由 `ItemMotionModule` 写回。

## Entity 运动提醒

- 玩家/生命体入口：`Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs`
- `Entity : Core, KinematicCharacterController.ICharacterController`
- `EntityKCCData` 是当前高频运动核心。
- 不要绕过 `StateSupportFlags`。飞行、游泳、攀爬、骑乘依赖它切换 KCC 分支。
- Item/Shot 体系不要替换 Entity KCC 热路径。

## 当前实现状态

已落地：

- `Item : Core`
- `ItemBasicDomain`
- `ItemMotionModule`
- `ItemShotModule`
- `ShotMotionSolver`
- `ShotMotionTypes`

已验证：

```text
dotnet build ES_Logic.csproj --no-restore -v:minimal
0 warning, 0 error
```

尚未落地但方向明确：

- `ItemLogicModule`
- Item 生命周期事件表
- Shot 命名收敛
- MustHit/Scan 等更完整命中模式
- LogicRandom/ViewRandom 分离
- Support 显式切换 Op

## 给后续 AI 的一句话

不要把 Item 飞行物做成“会动的技能特效”。正确方向是：`Item` 是世界逻辑体，`Shot` 是它的一类飞行能力，运动层只负责飞撞停，生命周期事件交给 Op/Expression/Support 编排。
