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

Unity `LayerMask` 只负责物理粗过滤；它不是阵营、归属或玩法身份系统。不要据此再创建一个名为 `Game Layer` 的平行运行时服务。

```text
Unity Layer：物理粗过滤；必须复用 ESPhysicsLayers 的 Ground、Wall、WorldDynamic、EntityHurtbox、ItemBody、Shot 等语义 Mask
FactionId + Relation：友军、敌军、中立、自伤与友伤关系
Actor / Spawn Archetype：玩家、NPC、怪物、召唤物等身份
GameTag + HitResolver：可命中、无敌、部位倍率、技能或任务条件
```

飞行物可以读取轻量目标接口，例如：

```text
Id
OwnerId
Side
Kind
```

但不要让飞行物直接理解完整阵营/仇恨/战斗系统。它只输出候选，上层系统最终裁决。不得把本节旧称的“Game Layer”误读成应新增第二套 Layer、枚举或全局管理器。

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

## 玩家手感参数权威与写入边界

玩家手感参数必须先确认“作者值”和“最终运行值”是否为同一条链，不能只看某个
Prefab、DataInfo 或 Attribute 表中的数字。

当前运动执行链为：

```text
EntityKCCData
  -> EntityKCCData.UpdateRotation / UpdateVelocity
  -> Entity.GetCharacterFloatStatValue
  -> KinematicCharacterMotor
```

当前规则：

- `EntityKCCData` 的 `stableMovementSharpness`、`orientationSharpness` 是当前 Prefab
  运行基线；原 `EntityBasicDomain.groundedDefaults` 转发层已移除，不得恢复第二套地面
  调参入口。
- `ESCharacterAttributeCatalog` 的 ValueChange/Permit 是运行时覆盖层；一旦存在有效
  覆盖，KCC 序列化值不是最终值。验收必须读取最终解析值，而不是只检查 Inspector。
- `ActorDataInfo.motionShared` 现在由 `Entity.BindDefinition(...)` 写入 KCC 的作者默认
  值，包含地面/空中速度、地面响应、朝向响应和跳跃参数；`ClearDefinition`/回池会恢复
  Prefab 基线。`speedMultiplier` 缺省零值按旧序列化兼容规则回退为 `1`，禁止因数据缺字段
  让玩家角色静默失去移动。它仍不是最高优先级：有效 Character Attribute/ValueChange
  覆盖会取代该作者默认值，验收必须读取最终解析值。
- `motionShared` 中的能力开关（例如 `enableClimb`、`enableMount`）以及
  `motionVariable` 中的 `allowMoveInput`、`allowLookInput`、`allowJump`、
  `gravityMultiplier` 等字段目前不是 `Entity.BindDefinition(...)` 的 KCC 直接写入项；
  它们只有在对应 Basic Module/State/Permit 消费者明确接线后才代表运行时行为。当前不得
  把 DataInfo 中“有字段”表述成“已经启用或已经限制”。
- 同一角色的 GroundSharpness、OrientationSharpness、最大速度、空中响应和跳跃参数，
  必须在生成器、正式 Prefab、作者 DataInfo 和运行时诊断中形成可追踪的一致记录；任一
  来源不一致都应标记为 `Verifying`，不得写成 Stable。

大黑塔当前作者基线为：

```text
GroundMovementSharpness = 20
OrientationSharpness    = 18
```

这组数字已同步生成器、正式 Prefab KCC 与 ActorData；仍需 Unity PlayMode 实测确认
最终解析值及起步 T90、松手停止距离、180°反向完成时间。

## P0 冻结：Entity 新运动能力扩展规范

本节为最高优先级约束。后续 AI 或开发者新增飞行、滑翔、墙跑、绳索、骑乘等**生命体自身**运动能力时，必须沿用现有 `Entity → BasicDomain Module → EntityKCCData → KinematicCharacterMotor` 链路，不得再造运动大根或中央类型分派。

`VehicleController` 是唯一例外，但它不是 Entity 的新运动模块：它代表一个独立的载具体，拥有自己的 Rigidbody 或 KCC 物理后端。骑手仍按本节通过 Entity KCC 进入、退出和跟随座位；骑手输入只能经 `EntityMountable` 转交给载具，不能直接改载具 Transform 或 Motor。

标准扩展流程：

```text
1. 在既有 Basic Domain 的合适文件中新增/扩展一个 EntityBasicXXXModule
2. 按实际需要实现：
   IEntityKCCBeforeMotion
   IEntityKCCRotationMotion
   IEntityKCCVelocityMotion
3. Start 中以幂等方式注册：
   if (!_motionRegistration.IsValid)
       _motionRegistration = MyCore.kcc.RegisterMotionFeature(this, order);
4. 每个 KCC 回调首段检查 Signal_IsActiveAndEnable、模块开关和 StateSupportFlags
5. OnDisable 只结束本模块的运行语义、状态和持续输入；保留注册句柄供重新启用复用
6. OnDestroy 调用 UnregisterMotionFeature(ref _motionRegistration)，并清理模块反向引用
```

权威边界：

- AI Domain 只采样玩家、AI、剧情、网络等控制来源，并形成运动请求。
- Basic Domain Module 只实现身体能力，不直接成为最终 Transform 权威。
- StateMachine 负责运动状态、动画时序、SupportFlags 与 MatchTarget 语义。
- `EntityKCCData` 负责调度各运动能力；`KinematicCharacterMotor` 是根位置和根旋转的最终执行权威。
- MatchTarget 只能由 State 计算并通过 `QueueMatchTargetPose` 提交，在 KCC `BeforeCharacterUpdate` 边界应用；普通 `Update` 禁止直接写玩家根 Transform 或 Motor。
- 载具根由 `VehicleController` 负责调度和写入：Rigidbody 后端只在 `FixedUpdate` 写 Rigidbody，KCC 后端只在 KCC 回调写候选旋转/速度；挂座、武器、镜头和表现模块不得绕过它写载具 Transform、Rigidbody 或 KCC。

生命周期硬规则：

- `Start` 注册必须幂等，禁止重复注册。
- 动态禁用后，KCC 回调必须立即失效，且不得残留飞行、攀爬、游泳、骑乘状态或持续输入。
- 重新启用不得产生第二份调度任务。
- `OnDestroy` 注销必须可重复调用且安全；模块销毁后调度器中不得残留任务。
- 不允许通过反射、中央 `switch`、扫描全部 Module 或每帧重建调度表完成扩展。

热路径硬规则：

- `BeforeCharacterUpdate`、`UpdateRotation`、`UpdateVelocity` 和输入分发高频路径不得使用 LINQ、闭包、反射、字符串查找、每帧 `GetComponent`、每帧 `new`、可增长容器或装箱接口枚举。
- 调度器、缓存、固定缓冲在初始化阶段预热；Profiler 必须在目标发布平台确认每个 FixedTick 的 `GC Alloc = 0 B`。
- 调试日志与 Gizmos 必须受 Editor、Development Build 或显式调试开关保护。

网络扩展原则：

- 网络层传输输入命令、状态快照、Tick、序号和必要的权威纠正，不同步各模块内部对象引用。
- 客户端预测与回滚必须复用同一套 KCC 运动求解入口；禁止额外写 Transform 的“网络运动分支”。
- 影响逻辑的运动参数、随机种子和 MatchTarget 目标采样必须可按 Tick 重放。

发布冻结前必须完成以下 PlayMode/Profiler 验收，不得仅凭静态代码判断完成：

- 普通移动、跳跃、飞行、游泳、攀爬、攀上、翻越、攀爬跳跃、骑乘逐项验证进入、持续、退出和被打断。
- MatchTarget 验证一帧多个 Update、一次渲染帧前多个 FixedTick、最终帧、状态提前退出、动态目标移动、骑乘中断与重新进入。
- 动态切换模块 `_enableSelf`，验证禁用立即停止接管、重新启用无重复任务、移除后调度器数量恢复、重复注销安全。
- 在目标发布平台使用 Unity Profiler 验证 KCC 每个 FixedTick 的 `GC Alloc = 0 B`；Editor 结果只能作为预检查。
- 原型 Entity 必须通过 Basic Domain 的“检查完整运动原型”和 AI Domain 的“检查玩家输入链路”按钮。

## 当前实现状态

已落地：

- `Item : Core`
- `ItemBasicDomain`
- `ItemMotionModule`
- `ItemShotModule`
- `ShotMotionSolver`
- `ShotMotionTypes`

历史静态证据（已过期，不可替代当前验收）：

```text
dotnet build ES_Logic.csproj --no-restore -v:minimal
0 warning, 0 error
```

上述记录对应本文件早期阶段。当前生成的 `.csproj` 可能因 Unity 尚未刷新而包含陈旧路径或漏收录；是否已通过，必须以当次命令输出、Unity Editor 域重载、Unity Test Runner、PlayMode 和目标平台 Profiler 分层判断。不得引用这一历史 `0/0` 结论来签收当前 Character、Item/Shot、Vehicle 或 Camera 改动。

尚未落地但方向明确：

- `ItemLogicModule`
- Item 生命周期事件表
- Shot 命名收敛
- MustHit/Scan 等更完整命中模式
- LogicRandom/ViewRandom 分离
- Support 显式切换 Op

## 给后续 AI 的一句话

不要把 Item 飞行物做成“会动的技能特效”。正确方向是：`Item` 是世界逻辑体，`Shot` 是它的一类飞行能力，运动层只负责飞撞停，生命周期事件交给 Op/Expression/Support 编排。
