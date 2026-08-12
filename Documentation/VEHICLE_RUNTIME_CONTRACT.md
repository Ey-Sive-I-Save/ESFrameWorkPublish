# 载具运行时与骑乘契约

状态：现行代码契约；Unity 全项目编译待既有 `CoreBreakout` 错误修复后复验。
最后验证：2026-08-02（程序集静态编译；Unity PlayMode 待验收）。
适用源码入口：`Assets/Scripts/ESLogic/Runtime/Vehicle/VehicleController.cs`、`Assets/Scripts/ESLogic/Runtime/Entity/Entity/Mount/EntityMountable.cs`、`Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/Modules/EntityBasicMotionModule_Mount.cs`、`Assets/Scripts/ESLogic/Runtime/Camera`。

## 运行时边界

载具是独立的世界物体，不因可骑乘就成为 `Entity`。`VehicleController` 是载具的唯一位姿和物理写入者；它在 Rigidbody 或 KinematicCharacterMotor 两种后端间二选一。

骑手仍然是 `Entity`：角色的 KCC、骑乘 State 和 MatchTarget 只负责骑手进入座位、稳定跟随和退出，不能写载具的 Transform、Rigidbody 或 KCC。

`EntityMountable` 不强制依赖 Controller：乘客座和静态骑乘点只需座位挂点；只有 `allowInput = true` 的驾驶座需要引用已就绪的 `VehicleController`。可骑乘组件必须位于可命中 Collider 的同节点或祖先节点；当前三个原型将其放在载具根，`DriverSeat` 子节点仅作为 `matchPoint`。

```text
已仲裁的驾驶输入
  -> EntityMountable.TrySetDriverInput
  -> VehicleController.TrySetDriverInput(seat, driver, ...)
  -> 车辆 ESWorkScheduler 阶段
  -> Rigidbody FixedUpdate / KCC ICharacterController 回调
```

## 组件职责

| 组件 | 职责 | 明确不负责 |
| --- | --- | --- |
| `VehicleController` | 后端验证、输入快照、车辆能力调度、最终旋转/速度写入 | 座位、骑手动画、角色状态和阵营逻辑 |
| `EntityMountable` | 单一骑手、`matchPoint`、武器挂点、离座清理；驾驶座按需转交输入 | 车辆速度、转向、重力和 Transform 写入；乘客座不要求 Controller |
| `EntityBasicMountModule` | 骑乘状态、MatchTarget、骑手 KCC 接管、采样玩家输入 | 载具物理与车辆能力调度 |
| 车辆能力 | 实现车辆专用运动阶段接口，修改候选旋转或速度 | 绕开 Controller 直接写物理后端 |

## 驾驶镜头

`VehicleController` 已有的组件承担驾驶镜头意图，不增加角色、座位或车辆能力组件。

```text
驾驶权授予 (seat, driver)
  -> VehicleController Push(vehicle.chase Shot，Owner=driver)
  -> ESCameraDirector 活跃集合仲裁
  -> driver 的既有 TrySetCameraLook 输入继续驱动获胜镜头
  -> 驾驶权释放 / Controller 禁用 / 销毁：Release Lease
```

- `driverCameraDefinitionKey` 为空时，载具不申请镜头；它是对静态骑乘点、AI 车辆和无镜头载具的正常配置，不需要空组件。
- 请求 Owner 必须是当前 `driver`，而不是 Controller。这样不增加输入转发 if，现有 `Entity.TrySetCameraLook` 仅在该请求获胜时自然被 Director 接受。
- 驾驶镜头的 `Follow` 默认是载具根；正式载具可显式配置专用相机锚点。它只保存 Transform 与 DefinitionKey，不保存 Brain、VCam 或 Rig。
- 默认内容工具生成 `vehicle.chase` Profile/Rig。方块汽车、自行车与直升机在“升级方块载具骑乘探针”时会显式写入该 Profile；项目内容可以替换为专属稳定 Key。

## 运动调度

`VehicleController` 在自己的 `ESWorkScheduler` 上暴露四个阶段：

1. `IVehicleBeforeMotion`：状态、座位或能力前置计算。
2. `IVehicleRotationMotion`：修改候选旋转。
3. `IVehicleVelocityMotion`：修改候选速度。
4. `IVehicleAfterMotion`：表现和状态同步，不驱动物理。

车辆能力不得复用 `IEntityKCCBeforeMotion`、`IEntityKCCRotationMotion`、`IEntityKCCVelocityMotion`。这些接口包含 Entity、角色状态旗标和角色工作额度语义；它们只适用于生命体自身运动。

一个物理步只能送入一个已仲裁的驾驶来源。座位输入必须经 `TrySetDriverInput(seat, driver, ...)`，Controller 只接受当前驾驶权对应的 `(seat, driver)`；无来源 `SetDriverInput` 不能覆盖已占用座位。输入路由失效会立即清空座位输入，Controller 也会清除超过一帧未刷新的快照。

禁用 Controller 会撤销当前驾驶座占用、解绑 KCC Controller，并通过座位事件使骑手同步退出 Mounted；重新启用 KCC 后端时重新绑定。车辆能力允许在调度中动态注销，单个能力异常仅记录，不会截断后续能力和本帧物理提交。

## 后端规则

- `Rigidbody`：用于受力、碰撞和关节驱动的车辆。Controller 仅在 `FixedUpdate` 调用 `MoveRotation` 和写入 `velocity`；Rigidbody 必须为非 Kinematic。
- `KinematicCharacterMotor`：用于胶囊/平台式碰撞和 KCC 接触语义。Controller 仅在 `ICharacterController` 回调中修改 `ref currentRotation`、`ref currentVelocity`；不得同时启用非 Kinematic Rigidbody。
- 后端错误必须在 `ValidateBackend` / `Initialize` 报错，不允许降级到直接 Transform 移动。

## 原型资产

`Assets/ESNormalAssets/VehiclePrototypes` 提供 `BlockCar`、`BlockBicycle` 和 `BlockHelicopter`。三者均为非 Entity 的方块原型：前两者使用有重力的 Rigidbody 速度型移动，直升机使用 `VehicleArcadeFlightModule` 读取升降轴。它们用于验证座位、后端和 `vehicle.chase` 驾驶镜头链路，不代表真实轮胎、悬挂或飞行力学实现。

## 验收

- 上车、输入接管、下车、状态打断和对象池复用后，`rider` 与输入快照没有残留。
- Rigidbody 车辆在碰撞、斜坡和固定步频下验证稳定性。
- KCC 车辆在地面探测、斜坡、碰撞回调与重力下验证稳定性。
- 移动载具上的 MatchTarget 对齐、稳定跟随和中断重进必须进行 PlayMode 验收。
- 三个原型均需验证：上车后 `vehicle.chase` 赢得 MainView、Look 继续可用；离座或禁用 Controller 后恢复玩家 Base，旧 Lease 不得残留。
- Unity 当前全项目编译被 `CoreBreakout` 中缺失类型/枚举项阻断；修复后必须确认本契约三个入口在 Unity 编译中通过，再执行上述 PlayMode 验收。
