# 玩家运动 / Item 飞行物 / Op 生命周期协作说明

Status: current
StableId: es.aiwarnings.runtime.player-motion.v1
Authority: ESFramework AIWarnings
RouteKeys: aiwarnings, runtime, entity-motion, player-motion, kcc, item, shot, vehicle, performance
Applicability: Entity、Item/Shot、VehicleController 运动与生命周期设计
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-player-motion-runtime-boundary.md`
StaleWhen: Entity KCC、Item/Shot、VehicleController、输入链或 StateSupportFlags 变化
Knowledge: `es.aiwarning.player-motion-runtime-boundary.v1`

## 长期约束

- 世界逻辑体只有 `Entity` 与 `Item` 两个大根；禁止恢复 `ESMotionBody`、`IESMotionDriver` 或并列运动根。`Item : Core`，运动/生命周期能力进入 Domain/Module。
- Item/Shot 层只负责飞、撞、到达、过期、停止和命中候选；不负责伤害、Buff、技能消费、VFX、音效、回收或全局调度。生命周期事件可由 Expression + Op + 自有 Support 编排，但高频运动不得进入 Op 链。
- Entity 运动必须沿 `Entity → Basic Domain Module → EntityKCCData → KinematicCharacterMotor`；StateSupportFlags 控制运动分支，普通 Update 不得直接写根 Transform/Motor，MatchTarget 只能经 KCC 边界应用。VehicleController 是独立载具例外，骑手输入经 EntityMountable 转交。
- 模块注册必须幂等；Disable 立即停止接管且不残留状态/输入；Destroy 注销并清理反向引用。禁止反射、中央 switch、扫描全部 Module 或每帧重建调度表。
- KCC/输入热路径禁止 LINQ、闭包、反射、字符串查找、每帧 GetComponent/new、动态扩容、装箱枚举和复杂 Expression/Op；初始化预热缓存/固定缓冲，目标平台 Profiler 才能确认 FixedTick `GC Alloc = 0 B`。
- 影响逻辑的随机必须由 seed、shotId、spawnTick 等可重放输入决定；网络复用同一 KCC 求解入口，不另写 Transform 分支。LayerMask 仅作物理粗过滤，阵营/身份/命中由上层裁决。
- `EntityKCCData`、作者 DataInfo 与 Attribute 覆盖链必须读取最终解析值；能力字段只有接入消费者才代表运行时行为。来源不一致标记 `Verifying`，不得写成 Stable。

## 证据边界

详细 Item 生命周期、Shot 模式/随机性、Support 切换、玩家手感优先级、模块生命周期、网络重放和验收矩阵见 Knowledge 条目；执行前仍须回读当前源码。普通移动、跳跃、飞行、游泳、攀爬、骑乘、MatchTarget、动态模块切换、目标平台 GC 与网络回滚尚未运行 Unity/PlayMode/Profiler/Player/IL2CPP。

原文 372 行、15,350 UTF-8 字节的详细事实已迁移至 `es.aiwarning.player-motion-runtime-boundary.v1`，可由 SourceRefs 回溯。
