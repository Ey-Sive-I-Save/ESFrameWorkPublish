# 玩家运动、Item/Shot 与 KCC 运行时边界

`KnowledgeId`: `es.aiwarning.player-motion-runtime-boundary.v1`  
`Authority`: `AIWarnings + current Entity/Item/Vehicle source`  
`RouteKeys`: `aiwarnings`, `runtime`, `entity-motion`, `player-motion`, `kcc`, `item`, `shot`, `vehicle`, `performance`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `14dddeb115bd75f719364ee4bcea7542a991d4b44002ac41440e0cd8aaafafb1`  
`SourceSetHash`: `14dddeb115bd75f719364ee4bcea7542a991d4b44002ac41440e0cd8aaafafb1`  
`EntryBodyHash`: `12936f7b67b8bab2f25043cb4b9a7b7581fa79517797f6fa944abc1d0d81e0c1`  
`StaleWhen`: `Entity KCC、Item/Shot、VehicleController、输入链、StateSupportFlags 或原 Warning SourceRef 变化。`

## 迁移说明

原 Warning 372 行、15,350 UTF-8 字节；现 Warning 仅保留长期约束、权限/证据边界和本条目导航。详细的 Item 生命周期、Shot 求解、Entity KCC 调度、Support 切换、随机性、网络重放、玩家手感覆盖优先级和验收清单迁移至本条目。原文路径及当前源码均保留为可回溯 SourceRefs，不以本条目替代源码或用户授权。

## 不变量

- 世界逻辑体只有 `Entity` 与 `Item` 两个大根；不得恢复 `ESMotionBody`、`IESMotionDriver` 或与 Entity/Item 并列的运动根。`Item : Core`，运动和生命周期能力进入 Basic Domain/Module。
- Item/Shot 运动层只负责飞、撞、到达、过期、停止和命中候选事件；不负责伤害、Buff、技能消费、VFX、音效、回收或全局调度。生命周期事件可由 Expression + Op + 自有 Support 编排，但高频运动不得进入 Op 链。
- Entity 生命体运动必须沿 `Entity → Basic Domain Module → EntityKCCData → KinematicCharacterMotor`；StateSupportFlags 控制飞行、游泳、攀爬、骑乘等分支，普通 Update 不得直接写根 Transform/Motor，MatchTarget 只能通过 `QueueMatchTargetPose` 在 KCC 边界应用。VehicleController 是独立载具例外，骑手输入经 EntityMountable 转交。
- 模块 Start 注册必须幂等，Disable 立即停止接管且不残留状态/输入，Destroy 注销并清理反向引用；禁止反射、中央 switch、扫描全部 Module 或每帧重建调度表。
- KCC/输入热路径禁止 LINQ、闭包、反射、字符串查找、每帧 GetComponent/new、动态扩容、装箱枚举和复杂 Expression/Op；初始化预热缓存/固定缓冲，目标平台 Profiler 才能确认 FixedTick `GC Alloc = 0 B`。
- 影响逻辑的随机必须由 seed、shotId、spawnTick 等可重放输入决定；网络复用同一 KCC 求解入口，不另写 Transform 分支。LayerMask 仅做物理粗过滤，阵营/身份/命中裁决由上层系统完成。
- `EntityKCCData`/作者 DataInfo/Preset/Attribute 覆盖链必须读取最终解析值；能力字段只有接入实际消费者才代表运行时行为。任何来源不一致标记 `Verifying`，不能写成 Stable。

## 必须回读的验收边界

普通移动、跳跃、飞行、游泳、攀爬、骑乘及 MatchTarget 的进入/持续/退出/中断；模块动态禁用、重新启用、移除和重复注销；目标平台 KCC FixedTick GC；网络预测/回滚与逻辑随机重放。当前均未运行 Unity/PlayMode/Profiler/Player/IL2CPP。

## EvidenceRefs

- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/EntityBasicModules.cs`
- `Assets/Scripts/ESLogic/Runtime/Vehicle/VehicleController.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/Entity运动（EntityMotion）/玩家运动_PlayerMotion_AI协作说明.md` (`723540e47e96cd52678ed7949887e79adae5820fcf328d9f1e83215cb8f903c4`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs` (`5d1f0225e27a8b04d219917fc15da5026675bc8d7b10024a17e91c8682c9751e`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/EntityBasicModules.cs` (`397d0c465f6b59069e388445d6d5724d190a0d08ec3d1719bfdfc9c6a1418c46`)
- `Assets/Scripts/ESLogic/Runtime/Vehicle/VehicleController.cs` (`edfb42e42ad5a662d5602e26f68e1ea9386fc0d6bc6cdf44e5f6edf8c0c4d3c6`)
