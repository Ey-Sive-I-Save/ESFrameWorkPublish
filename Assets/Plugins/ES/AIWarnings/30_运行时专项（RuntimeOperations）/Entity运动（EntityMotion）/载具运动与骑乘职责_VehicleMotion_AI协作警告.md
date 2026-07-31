# 载具运动与骑乘职责：AI 协作警告

状态：现行实现约束。最后核对：2026-08-01。

## 结论

`VehicleController` 是载具根的唯一运动权威；`EntityMountable` 只是座位、骑手对齐、武器挂点和驾驶输入转交器。两者不合并，也不向角色 Prefab 固定组件表扩散。

```text
玩家/AI/网络控制权
  -> Entity 输入与骑乘状态
  -> EntityMountable.SubmitDriverInput
  -> VehicleController.SetDriverInput
  -> 载具运动调度
  -> Rigidbody FixedUpdate 或 KCC 回调

Entity KCC
  -> MatchTarget / 座位跟随
```

## 组件职责

- `VehicleController`：选择并验证 Rigidbody/KCC 后端，保存已仲裁的驾驶意图，调度车辆能力，提交最终旋转和速度。
- `EntityMountable`：维护单一 rider、座位 `matchPoint`、武器挂点、骑手同步和输入转交；不得计算或写入载具物理。
- `EntityBasicMountModule`：仅管理骑乘状态、MatchTarget 和玩家输入采样；其 KCC 回调只接管骑手，绝不接管载具。
- 车辆专用能力：按需实现 `IVehicleBeforeMotion`、`IVehicleRotationMotion`、`IVehicleVelocityMotion`、`IVehicleAfterMotion`，注册到载具自身的 `ESWorkScheduler`。

## 调度与写入规则

- 不复用 `IEntityKCCBeforeMotion`、`IEntityKCCRotationMotion` 或 `IEntityKCCVelocityMotion`；它们带有 Entity、状态旗标和角色额度语义。
- 车辆调度接口只传 `VehicleController` 和候选值。能力可修改候选旋转/速度，但只有 Controller 提交最终结果。
- Rigidbody 载具在 `FixedUpdate` 写 `Rigidbody.MoveRotation` 与 `Rigidbody.velocity`；KCC 载具只在 `ICharacterController` 回调内写 `ref currentRotation` / `ref currentVelocity`。
- 禁止座位、镜头、武器、动画事件、AI 或网络补丁直接写载具 `Transform`、`Rigidbody`、`KinematicCharacterMotor`。
- 每个物理步只能有一个已仲裁的输入来源。`SetDriverInput` 是最终输入入口，不负责裁决多个驾驶者、AI 或网络命令的优先级。

## 后端选择

- 使用 `Rigidbody`：汽车、船、会受力/碰撞/关节影响的载具。必须使用非 Kinematic Rigidbody。
- 使用 `KinematicCharacterMotor`：稳定胶囊碰撞、平台式移动、需要 KCC 接触语义的载具。不得同时启用非 Kinematic Rigidbody。
- 不是所有载具都必须由 Entity 表示；载具不是生命体时，不应为了运动而添加 `Entity`、角色 Domain 或角色 Profile。

## 验收

- Controller 后端引用和配置有效，错误配置必须报错，不允许回退为 Transform 移动。
- 骑手上车、输入接管、下车、骑乘状态被打断与对象池复用后，载具输入和 rider 引用均被清理。
- Rigidbody 载具验证碰撞和固定步稳定性；KCC 载具验证地面、斜坡和碰撞回调。
- 骑手在移动载具上的 MatchTarget 对齐、稳定跟随和中断重进必须在 PlayMode 验证。
