# Vehicle、Mount 与单写入者运动协议

`KnowledgeId`: `es.project.vehicle-mount-motion.v1`  
`Authority`: `Source + AIWarnings`  
`RouteKeys`: `vehicle`, `mount`, `rider`, `driver`, `motion`, `rigidbody`, `kcc`, `scheduler`, `input`, `camera-request`  
`ContentHash`: `48ca02dde7f94833d5acebdc1c135d3f360aa42cf82775faaab26ae48a937bd5`

## 三方职责

`VehicleController` 是可移动载具的唯一运动权威；`EntityMountable` 只拥有座位、单一 rider、matchPoint、武器挂点与驾驶输入转交；Entity 的 Mount Module 管理骑乘状态、玩家输入采样和骑手 MatchTarget。座位、相机、武器、动画、AI、网络层都不能直接写载具 Transform/Rigidbody/KCC。

乘客座和静态骑乘点可以没有 Controller。只有允许驾驶输入的座位才引用并转交给父级 Controller。载具不是生命体时无需为了运动添加 Entity 或角色 Profile。

## 驾驶占用与输入新鲜度

Controller 以 `seat + driver` 成对取得驾驶权；`TrySetDriverInput` 只接受当前二元组，无来源 Set 不能覆盖已占用驾驶座。下车、状态中断、座位/Controller Disable、Destroy 或池化复用都必须清 rider、driver、输入和驾驶相机请求。

驾驶意图只允许跨过紧邻的一次 FixedUpdate；超过刷新窗口就清空，避免断路后持续使用最后一帧方向。输入路由、Tag 条件或控制权失效同样必须主动清理。

## 两种后端与一个提交者

Rigidbody 后端要求非 Kinematic Rigidbody，并只在 FixedUpdate 用 MoveRotation/velocity 提交。KCC 后端要求 KinematicCharacterMotor，不能同时启用非 Kinematic Rigidbody，并只在 ICharacterController 的 rotation/velocity 回调中写 ref 候选值。配置错误直接失败，不回退成 Transform 移动。

车辆能力实现独立的 Before/Rotation/Velocity/After 接口，注册到 Controller 自己的 `ESWorkScheduler`；不复用 Entity KCC 接口。阶段任务只能改候选值，最终提交仍只有 Controller。遍历用 generation-safe `TryGetAlive`，单任务异常记录并跳过，不能阻断本帧其他能力及最终提交。

外力通过运动影响接收器进入候选速度，不绕过后端。驾驶相机同样是可释放请求，Controller 取得/释放驾驶者时同步维护，但 Camera 模块仍拥有最终仲裁权。

## 验收边界

源码能证明单写入者、后端互斥和清理路径存在；仍需 PlayMode 分别验证 Rigidbody 碰撞/固定步、KCC 地面斜坡回调、上下车中断重进、移动载具上的 MatchTarget、输入断流和对象池复用。没有这些证据时保持 S1。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/Entity运动（EntityMotion）/载具运动与骑乘职责_VehicleMotion_AI协作警告.md` (`67f5565428ff61c53d99890e1330e8d33598919089d09c1811634d2c3c05308b`)
- `Assets/Scripts/ESLogic/Runtime/Vehicle/VehicleController.cs` (`edfb42e42ad5a662d5602e26f68e1ea9386fc0d6bc6cdf44e5f6edf8c0c4d3c6`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Mount/EntityMountable.cs` (`fa26c1d415ae228cb60a84c2ebef80155bed5ef480f1294ec4f50af683c6c42b`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/Modules/EntityBasicMotionModule_Mount.cs` (`4c74611ab992c959f4053b00058cf52f53c7061ab782acf58c7f35b87301b742`)

`EvidenceLevel`: `S1`; `StaleWhen`: 驾驶占用、输入新鲜度、Rigidbody/KCC 后端、车辆调度或 Mount 清理协议变化。
