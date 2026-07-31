# 载具运行时与骑乘契约

状态：现行代码契约；Unity 全项目编译待既有 `CoreBreakout` 错误修复后复验。
最后验证：2026-08-01。
适用源码入口：`Assets/Scripts/ESLogic/Runtime/Vehicle/VehicleController.cs`、`Assets/Scripts/ESLogic/Runtime/Entity/Entity/Mount/EntityMountable.cs`、`Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/Modules/EntityBasicMotionModule_Mount.cs`。

## 运行时边界

载具是独立的世界物体，不因可骑乘就成为 `Entity`。`VehicleController` 是载具的唯一位姿和物理写入者；它在 Rigidbody 或 KinematicCharacterMotor 两种后端间二选一。

骑手仍然是 `Entity`：角色的 KCC、骑乘 State 和 MatchTarget 只负责骑手进入座位、稳定跟随和退出，不能写载具的 Transform、Rigidbody 或 KCC。

```text
已仲裁的驾驶输入
  -> EntityMountable.SubmitDriverInput
  -> VehicleController.SetDriverInput
  -> 车辆 ESWorkScheduler 阶段
  -> Rigidbody FixedUpdate / KCC ICharacterController 回调
```

## 组件职责

| 组件 | 职责 | 明确不负责 |
| --- | --- | --- |
| `VehicleController` | 后端验证、输入快照、车辆能力调度、最终旋转/速度写入 | 座位、骑手动画、角色状态和阵营逻辑 |
| `EntityMountable` | 单一骑手、`matchPoint`、武器挂点、输入转交、离座清理 | 车辆速度、转向、重力和 Transform 写入 |
| `EntityBasicMountModule` | 骑乘状态、MatchTarget、骑手 KCC 接管、采样玩家输入 | 载具物理与车辆能力调度 |
| 车辆能力 | 实现车辆专用运动阶段接口，修改候选旋转或速度 | 绕开 Controller 直接写物理后端 |

## 运动调度

`VehicleController` 在自己的 `ESWorkScheduler` 上暴露四个阶段：

1. `IVehicleBeforeMotion`：状态、座位或能力前置计算。
2. `IVehicleRotationMotion`：修改候选旋转。
3. `IVehicleVelocityMotion`：修改候选速度。
4. `IVehicleAfterMotion`：表现和状态同步，不驱动物理。

车辆能力不得复用 `IEntityKCCBeforeMotion`、`IEntityKCCRotationMotion`、`IEntityKCCVelocityMotion`。这些接口包含 Entity、角色状态旗标和角色工作额度语义；它们只适用于生命体自身运动。

一个物理步只能送入一个已仲裁的驾驶来源。Controller 不定义多驾驶者、AI 接管或网络命令的优先级；控制权系统在调用 `SetDriverInput` 前完成裁决。

## 后端规则

- `Rigidbody`：用于受力、碰撞和关节驱动的车辆。Controller 仅在 `FixedUpdate` 调用 `MoveRotation` 和写入 `velocity`；Rigidbody 必须为非 Kinematic。
- `KinematicCharacterMotor`：用于胶囊/平台式碰撞和 KCC 接触语义。Controller 仅在 `ICharacterController` 回调中修改 `ref currentRotation`、`ref currentVelocity`；不得同时启用非 Kinematic Rigidbody。
- 后端错误必须在 `ValidateBackend` / `Initialize` 报错，不允许降级到直接 Transform 移动。

## 验收

- 上车、输入接管、下车、状态打断和对象池复用后，`rider` 与输入快照没有残留。
- Rigidbody 车辆在碰撞、斜坡和固定步频下验证稳定性。
- KCC 车辆在地面探测、斜坡、碰撞回调与重力下验证稳定性。
- 移动载具上的 MatchTarget 对齐、稳定跟随和中断重进必须进行 PlayMode 验收。
- Unity 当前全项目编译被 `CoreBreakout` 中缺失类型/枚举项阻断；修复后必须确认本契约三个入口在 Unity 编译中通过，再执行上述 PlayMode 验收。
