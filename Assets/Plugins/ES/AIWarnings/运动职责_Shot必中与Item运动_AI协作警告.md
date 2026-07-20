# 运动职责：Shot 必中与 Item 运动协作警告

> 负责 AI：Codex 运动方向。职责是跑通玩家底层运动、场景交互闭环，以及 Entity/Item/Shot 的运动基础。后续 AI 改动前请先读源码，不要只按旧文档猜。

## 当前结论

- `Entity` 管生命体：玩家、NPC、怪物等，当前高频运动主线仍以 KCC 为核心。
- `Item` 管世界逻辑体：飞行物、门、机关、塔、陷阱、拾取物、武器、移动平台、区域等。
- `Shot` 是 Item 的飞行物能力，不是技能、伤害、特效、对象池的总入口。
- 运动层只负责：飞、追、撞、到达、过期、停止，并输出事件/候选结果。
- 伤害、Buff、VFX、音效、Pool、复杂剧情逻辑都由外层消费事件处理。

## 已落地的 Shot 基础能力

源码位置：

- `Assets/Scripts/ESLogic/Runtime/Item/Domains/Basic/ProjectileMotionTypes.cs`
- `Assets/Scripts/ESLogic/Runtime/Item/Domains/Basic/ProjectileMotionSolver.cs`
- `Assets/Scripts/ESLogic/Runtime/Item/Domains/Basic/ItemBasicModules.cs`
- `Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ItemDataInfo.cs`

已经支持：

- 发射延迟：`launchDelay`，延迟期间不移动、不查碰撞。
- 预热时间：`warmupTime`，用于刚生成但还不进入正式飞行的阶段。
- 加速与限速：`acceleration`、`maxSpeed`。
- 锁头时间窗：`trackingStartTime`、`trackingDuration`。
- 转向速度：`turnSpeed`，避免目标追踪瞬间硬拐。
- Transform 目标：`LaunchTo(Transform target)` / `LaunchTo(Transform target, bool mustHit)`。
- 必中模式：`ShotAimMode.MustHit` 到达目标点时生成命中候选，即使没有真实物理碰撞。
- 内部命中查询预留：`IItemProjectileHitSolver`，默认使用 `Physics.SphereCastNonAlloc`。
- 内部 Tick 预留：`IItemProjectileTickScheduler`，后续可替换为空间哈希、分组 Tick、距离预算等。

## 必中不是碰撞特例

必中语义是：战斗/技能层已经决定“这次应该命中”，Shot 只负责把飞行表现跑完，并在到达时交出命中候选。

推荐理解：

```text
Free      自由飞行，靠空间检测命中
Target    追踪目标，是否命中仍看碰撞/规则
MustHit   必中表现，到达目标时产生命中候选
Scan      扫描/射线类，后续扩展
```

阻挡规则单独处理：

```text
None       不被阻挡，适合治疗弹、必中表现弹
WorldOnly  只被场景阻挡，适合锁定火球
AnyBlocker 任意阻挡体可阻挡，适合箭矢/实体弹
```

注意：当前 `WorldOnly` 还只是数据语义，真正区分“世界层/角色层/机关层”的 Game Layer 过滤还需要后续补齐。不要把阵营、归属、伤害规则塞进 Shot 运动模块。

## 简单能力内置，复杂组合交给 Op

Shot 内置的应该是高频、通用、可预测的运动参数：

- 延迟
- 预热
- 加速
- 限速
- 锁头开始
- 锁头持续
- 转向速度
- 寿命
- 命中半径

交给 ESOutputOp / Expression / Support 的是低频事件逻辑：

- 命中后分裂
- 中途按 Buff 换目标
- 根据环境触发二段飞行
- 到达后生成区域
- 过期后爆炸
- 命中后连锁
- 击中目标后切换到目标 EntitySupport 执行逻辑

高频 Tick 禁止跑 Op 链、反射、LINQ、字符串查找、每帧 new 数组、每帧 GetComponent。

## 防止系统上限锁死

当前设计已经预留两处可替换点：

- `IItemProjectileHitSolver`：默认物理 SphereCastNonAlloc，后续可替换为空间哈希、圆/球简化碰撞、Job 批处理。
- `IItemProjectileTickScheduler`：默认每帧 Tick，后续可替换为分组 Tick、预算 Tick、远距离降频、重要性排序。

不要提前把所有飞行物锁死在 Unity Physics 单发查询上，也不要把所有飞行物锁死为每帧全量 Tick。第一版可以用默认实现跑通，但接口必须保留。

## 中文配置口径

`ItemDataInfo` 是总入口，不要拆出 `DoorDataInfo`、`TowerDataInfo`、`ProjectileDataInfo`。

常见配置块：

```text
baseConfig      基础：类型、预制体、显示名、图标、标签
interactConfig  交互：能否交互、提示、距离、条件
logicConfig     逻辑：生成、使用、命中、过期、销毁前 Op
moveConfig      移动：门/机关/平台/掉落/旋转/跟随等
shotConfig      飞行物：Shot 专属
weaponConfig    武器：引用 Shot Item，不内嵌飞行逻辑
```

门、塔、机关不需要一上来各自独立 Config。多数门只需要 `interactConfig + logicConfig`，会移动再加 `moveConfig`。

## 近期不要做的事

- 不要恢复旧的 `Runtime/Movement` 方案。
- 不要重新引入 `ESMotionBody` 这种和 Entity/Item 并列的大根。
- 不要把 Shot 改成独立散落的 MonoBehaviour 闭环。
- 不要把伤害、Buff、VFX、音效、对象池、全局调度塞进 `ItemProjectileModule`。
- 不要创建一堆 `ItemMotionDomain / ItemCollisionDomain / ItemLifetimeDomain`。Domain 是大边界，能力点进 Module。
- 不要把 `MustHit` 当作跳过架构的 hack，它是合法瞄准模式。

## 当前验证

已执行：

```text
dotnet build ES_Logic.csproj --no-restore -v:minimal
```

结果：编译成功。当前有 2 个既有示例字段未使用 warning，和本次 Item/Shot 改动无关。
