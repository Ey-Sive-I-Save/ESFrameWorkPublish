# 载具运动与骑乘职责：AI 协作警告

状态：现行实现约束。最后核对：2026-08-01。

## 结论

`VehicleController` 是可移动载具根的唯一运动权威；`EntityMountable` 只是座位、骑手对齐、武器挂点和驾驶输入转交器。乘客座与静态骑乘点不强制挂 Controller，只有驾驶座按需引用父级 Controller。两者不合并，也不向角色 Prefab 固定组件表扩散。

```text
玩家/AI/网络控制权
  -> Entity 输入与骑乘状态
  -> EntityMountable.SubmitDriverInput
  -> VehicleController.SubmitDriverInput(seat, driver, ...)
  -> 载具运动调度
  -> Rigidbody FixedUpdate 或 KCC 回调

Entity KCC
  -> MatchTarget / 座位跟随
```

## 组件职责

- `VehicleController`：选择并验证 Rigidbody/KCC 后端，保存已仲裁的驾驶意图，调度车辆能力，提交最终旋转和速度。
- `EntityMountable`：维护单一 rider、座位 `matchPoint`、武器挂点与骑手同步；仅 `allowInput` 的驾驶座转交输入，乘客座可没有 Controller；不得计算或写入载具物理。它必须位于可命中 Collider 的同节点或祖先节点；原型的根节点持有该组件，`DriverSeat` 子节点只是匹配点。
- `EntityBasicMountModule`：仅管理骑乘状态、MatchTarget 和玩家输入采样；其 KCC 回调只接管骑手，绝不接管载具。
- 车辆专用能力：按需实现 `IVehicleBeforeMotion`、`IVehicleRotationMotion`、`IVehicleVelocityMotion`、`IVehicleAfterMotion`，注册到载具自身的 `ESWorkScheduler`。

## 调度与写入规则

- 不复用 `IEntityKCCBeforeMotion`、`IEntityKCCRotationMotion` 或 `IEntityKCCVelocityMotion`；它们带有 Entity、状态旗标和角色额度语义。
- 车辆调度接口只传 `VehicleController` 和候选值。能力可修改候选旋转/速度，但只有 Controller 提交最终结果。
- Rigidbody 载具在 `FixedUpdate` 写 `Rigidbody.MoveRotation` 与 `Rigidbody.velocity`；KCC 载具只在 `ICharacterController` 回调内写 `ref currentRotation` / `ref currentVelocity`。
- 禁止座位、镜头、武器、动画事件、AI 或网络补丁直接写载具 `Transform`、`Rigidbody`、`KinematicCharacterMotor`。
- 每个物理步只能有一个已仲裁的输入来源。座位输入只能经 `SubmitDriverInput(seat, driver, ...)` 进入 Controller；无来源 `SetDriverInput` 不能覆盖当前驾驶座。
- 输入路由被禁用、Tag 条件失效或快照超过一帧未刷新时必须清空驾驶意图，禁止继续消费最后一帧方向。
- KCC 后端在 Controller 禁用时解绑，在启用时重绑；四个 KCC 运动回调和 Rigidbody 固定步均以 `IsReady` 为前提。
- 调度器遍历必须使用 `TryGetAlive`；单个车辆能力异常只能记录并跳过，不能阻断后续能力和本帧物理提交。

## 后端选择

- 使用 `Rigidbody`：汽车、船、会受力/碰撞/关节影响的载具。必须使用非 Kinematic Rigidbody。
- 使用 `KinematicCharacterMotor`：稳定胶囊碰撞、平台式移动、需要 KCC 接触语义的载具。不得同时启用非 Kinematic Rigidbody。
- 不是所有载具都必须由 Entity 表示；载具不是生命体时，不应为了运动而添加 `Entity`、角色 Domain 或角色 Profile。

## 验收

- Controller 后端引用和配置有效，错误配置必须报错，不允许回退为 Transform 移动。
- 骑手上车、输入接管、下车、骑乘状态被打断与对象池复用后，载具输入和 rider 引用均被清理。
- Rigidbody 载具验证碰撞和固定步稳定性；KCC 载具验证地面、斜坡和碰撞回调。
- 骑手在移动载具上的 MatchTarget 对齐、稳定跟随和中断重进必须在 PlayMode 验证。
