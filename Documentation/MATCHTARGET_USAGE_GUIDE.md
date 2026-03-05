# MatchTarget（目标对齐）使用指南（2026-03）

## 一句话规则
- MatchTarget（目标对齐）配置只负责“参数与偏移”，不负责“目标位姿（位置与旋转）”。
- 目标位置与目标旋转必须由运行时传入；阶段一（Phase1）与阶段二（Phase2）共用同一份目标位姿。
- 阶段二（Phase2）由 `StateBase` 内部时序推进，业务模块不要手动触发。

## 当前数据职责
### 1）`MatchTargetRequest`（配置层）
负责内容（参数与偏移）：
- `bodyPart`（身体部位）
- `timeRange`（时间窗）
- `positionApproachSpeed`（位置逼近速度）
- `rotationApproachSpeed`（旋转基础速度）
- `rotationWeight`（旋转权重）
- `positionOffset` 与 `enablePositionOffset`（位置偏移与开关）
- `rotationOffsetY` 与 `enableRotationOffset`（Y轴旋转偏移与开关）

不再负责内容：
- 目标 `Transform`（目标变换）
- 固定 `Position/Rotation`（固定位置/固定旋转）

### 2）运行时目标位姿（执行层）
由调用方提供：
- `targetPos`（目标位置）
- `targetRot`（目标旋转）

写入方式：
- 启动：`ApplyMatchTarget(request, targetPos, targetRot)` 或 `StartMatchTargetFromConfig(targetPos, targetRot)`
- 持续更新：`SetMatchTargetTargetWithConfigOffset(rawPos, rawRot)`

## 阶段机制（重点）
- `_configAutoPhaseIndex = -1/0/1` 表示阶段时序状态。
- `autoActivateMatchTarget = true`（自动激活）时：状态进入后按 `timeRange` 自动推进阶段。
- `autoActivateMatchTarget = false`（手动激活）时：业务代码通常只启动阶段一（Phase1），阶段二（Phase2）仍由 `StateBase` 自动接管。
- 硬约束：阶段二（Phase2）必须满足“阶段一结束后”才能启动。
  - 判定门槛等价于：`elapsed >= max(Phase2.timeRange.x, Phase1.timeRange.y)`。
  - 即使阶段二开始时间更小，也不能越过阶段一结束边界。
- 业务层不要把 `StartMatchTargetFromPhase2Config(...)` 作为常规路径。

## 业务模块标准写法
### 交互模块
- 启动时把交互物体的 `transform.position/rotation`（位置/旋转）作为目标传入。

### 骑乘与攀爬模块（Mount / Climb）
- 每帧只更新同一份 `raw`（原始）目标位姿，例如 `matchPoint`（匹配点）或墙面计算点。
- 通过 `SetMatchTargetTargetWithConfigOffset(...)` 让当前阶段 `offset`（偏移）自动叠加。
- 不维护本地阶段二标志，不做阶段二显式切换。

## 常见误区
- 误区：在配置里查找目标 `Transform`。
  - 现状：已移除，目标位姿由运行时提供。
- 误区：阶段二需要业务模块手动触发。
  - 现状：由 `StateBase` 自动推进。
- 误区：阶段一和阶段二使用不同目标位置。
  - 现状：两阶段共用同一份 `raw`（原始）目标位姿，差异仅在参数与偏移。

## 快速排查清单
- 看不到对齐：确认是否传入了运行时 `targetPos/targetRot`（目标位置/旋转）。
- 偏移不生效：确认是否使用 `SetMatchTargetTargetWithConfigOffset(...)`。
- 阶段二不触发：检查 `enableMatchTargetPhase2` 与 `matchTargetPresetPhase2.timeRange.x`，并确认阶段一结束时间是否过晚。
- 提前退出：检查 `timeRange.y`（结束时间）是否过小、逼近速度是否过低。
