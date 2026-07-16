# Player Motion AI Brief

> 面向后续协作 AI：本文件记录“运动方向”协作上下文。我的职责是跑通玩家底层运动，并保证运动与场景交互能形成闭环。请优先用它建立索引，但不要把它当作最终事实；改动前仍需回读对应源码。

## Scope

- 项目路径：`F:\aaProject\ESFrameWorkPublish`
- 当前职责：玩家底层运动优先，包括 KCC 位移、跳跃、下蹲、飞行/游泳/攀爬/骑乘分支、运动参数回灌状态机，以及运动触发的场景交互。
- 交互只在“运动闭环”范围内处理：检测候选、靠近/面向限制、交互期间移动取消、MatchTarget、IK 写入、门/攀爬面这类场景对象协议。背包、任务、UI 流程等不属于本文件职责。
- 重点源码不在 `Assets/Plugins/ES`，而在 `Assets/Scripts/ESLogic/Runtime`。`Plugins/ES` 更多是框架、编辑器和历史资料。
- Unity 版本：`2022.3.57f1c1`。`Packages/manifest.json` 已包含 `com.unity.inputsystem`、Cinemachine、URP、Timeline、UniTask、MemoryPack。

## Confirmed Architecture

- 玩家实体入口：`Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs`
  - `Entity : Core, KinematicCharacterController.ICharacterController`
  - `Entity` 直接实现 KCC 回调，不是外部示例控制器。
  - `EntityKCCData` 内嵌在同文件中，是当前高频运动核心。

- 运动内核：`EntityKCCData`
  - 依赖 `KinematicCharacterMotor`。
  - 已实现地面移动、空中移动、跳跃缓冲、下蹲胶囊高度切换、RootMotion 速度叠加、速度倍率、速度上限。
  - `BeforeCharacterUpdate` 会清理未在本帧设置的 move/vertical 输入；输入必须逐帧写入。
  - `AfterCharacterUpdate` 更新 `EntityKCCMonitor`，并包含静止上漂修正逻辑。

- 支持状态：`Assets/Scripts/ESLogic/Runtime/State/BaseDefine/StateSupportFlags.cs`
  - 重要枚举：`Grounded`、`Crouched`、`Swimming`、`Flying`、`Mounted`、`Climbing`、`SpecialInteraction`。
  - KCC 分支通过 `stateDomain.stateMachine.currentSupportFlags` 判断飞行/游泳/攀爬/骑乘等运动模式。
  - 不要只移动 Transform；正确链路应同步状态机支持状态与 KCC 监控数据。

- 基础移动模块：`Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/EntityBasicModules.cs`
  - `EntityBasicMoveRotateModule` 处理跳跃/下蹲状态触发。
  - 它使用 `MyCore.kcc.monitor.velocity` 的实际速度，而不是输入意图，写回 `StateMachine.SetMotionSpeedXZ` 和 `SetAvgSpeedXZ`。
  - 动画状态默认名称含中文：`跳跃`、`下蹲`。状态缺失时跳跃/下蹲生命周期可能不完整。

- 扩展运动模块：
  - 攀爬：`Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/Modules/EntityBasicMotionModule_Climb.cs`
  - 可攀爬表面：`Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/ClimbableSurface.cs`
  - 飞行/游泳：`Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/Modules/EntityBasicMotionModules_FlySwim.cs`
  - 骑乘：`Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/Modules/EntityBasicMotionModule_Mount.cs`
  - 这些模块实现或配合 `IEntitySupportMotion`，由 `EntityKCCData` 在 KCC 回调中分发。

## Input Chain

- 输入采集和分发：`Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/AI/EntityAIModules.cs`
  - `EntityAIInputSystemModule` 采集 Unity Input System 输入并生成 `InputSnapshot`。
  - `EntityAIInputDispatchModule` 把输入分发到移动、跳跃、下蹲、飞行、骑乘、攀爬、交互、战斗、技能。
  - 默认快速绑定由 `EntityInputQuickInit` 生成：WASD、鼠标、Space、C、E、G、F、R 等已覆盖。

- 分发事实：
  - 移动：`MyCore.SetMoveInput(moveWorld)`，逐帧调用。
  - 朝向：`MyCore.SetLookInput(_lastLookWorld)`。
  - 跳跃：`moveModule.RequestJump()`，再由基础移动模块调用 `MyCore.RequestJump()`。
  - 下蹲：`moveModule.ToggleCrouch()`。
  - 交互：`interactionModule.RequestInteract()`。
  - 攀爬：`climbModule.ToggleClimb()`。

## Interaction Chain

- 交互模块：`Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/Modules/EntityBasicInteractionModule.cs`
  - 通过 `Physics.OverlapSphereNonAlloc` 半径检测 `ESInteractable`。
  - 可配置检测半径、最大数量、LayerMask、面向角限制、是否要求 grounded。
  - `RequestInteract` 若正在交互则取消，否则选择候选并开始交互。
  - `BeginInteraction` 会解析或注入交互状态、可选覆盖 `StateSupportFlags.SpecialInteraction`、可选 `MatchTarget`、调用 `target.OnInteractStarted`。
  - `UpdateInteraction` 支持移动输入取消、持续时间完成、超时失败、逐帧 IK 写入和目标对象更新。

- 交互物基类：`Assets/Scripts/ESLogic/Runtime/Entity/Interaction/ESInteractable.cs`
  - 协议包括：`CanInteract`、`OnInteractStarted`、`OnInteractUpdate`、`OnInteractCompleted`。
  - 支持可选状态注入、`stateKeyOverride`、IK 目标、IK hint、IK 权重、IK lerping、目标旋转、MatchTarget。

- 门示例：`Assets/Scripts/ESLogic/Runtime/Entity/Interaction/ESInteractableDoor.cs`
  - 交互成功时切换 `isOpen`。
  - `Update` 中用 `Quaternion.RotateTowards` 朝开/关局部旋转移动。

## State Machine Relation

- 状态机在 `EntityStateDomain` 中持有：`stateDomain.stateMachine`。
- 初始化路径：`Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/State/EntityStateDomain.StateRegistration.cs`
  - `InitializeStateMachine()` 调用 `stateMachine.Initialize(MyCore, _cachedAnimator)`。
  - `StartStateMachineAfterDataLoaded()` 调用 `stateMachine.StartStateMachine()`。
- `StateMachine.Utils.cs` 暴露运动参数快路径：
  - `SetMotionSpeedXZ`
  - `SetAvgSpeedXZ`
  - `SetClimbInput`
  - `SetFloat/SetInt/SetBool`

## Common AI Pitfalls

- 不要从 `Assets/KinematicCharacterController/ExampleCharacter` 或 `Walkthrough` 复制示例控制器作为正式实现；项目已有 `Entity` 版 KCC 控制器。
- 不要把 `OpMovement_Translate` 当作玩家底层运动主入口。`OpMovementPhysics.cs` 只是操作系统里的轻量 Transform/Rigidbody 操作，不是 KCC 主链路。
- 不要只检查 `Assets/Plugins/ES`。玩家业务层主要在 `Assets/Scripts/ESLogic/Runtime/Entity` 与 `Assets/Scripts/ESLogic/Runtime/State`。
- 不要绕过 `StateSupportFlags`。飞行/游泳/攀爬/骑乘依赖它切换 KCC 速度分支。
- 不要假设输入会保持。`EntityKCCData.BeforeCharacterUpdate` 会在本帧未写入时把 `moveInput` 和 `verticalInput` 清零。
- 不要忽视状态名。跳跃、下蹲、攀爬、交互若依赖中文状态名但状态机未注册，对应生命周期会失败或退化。
- 不要在交互中直接操纵 IK Driver。当前交互模块通过 `StateBase.SetIKGoal` 写入，让状态机/FinalIK Driver 汇总。

## Prefab And Scene Verification Points

后续要真正跑通时，优先验证场景或玩家 prefab 是否同时满足：

- `Entity` 组件存在。
- 同对象或可解析处存在 `KinematicCharacterMotor`，并在 `Entity.InitializeKCC()` 后绑定 `motor.CharacterController = owner`。
- `basicDomain` 中有 `EntityBasicMoveRotateModule`；需要交互则有 `EntityBasicInteractionModule`；需要攀爬/飞行/骑乘则有对应模块。
- `aiDomain` 中有 `EntityAIInputSystemModule` 和 `EntityAIInputDispatchModule`，输入 action 已启用或已使用 `InitBuiltin`/预设。
- `stateDomain` 有可运行的 `StateMachine`、Animator、默认状态、跳跃/下蹲/交互/攀爬相关状态。
- 场景交互物有 Collider，挂 `ESInteractable` 或派生类，并处于 `EntityBasicInteractionModule.interactableLayers` 可检测层。
- 攀爬物有 Collider，挂 `ClimbableSurface`，且层级在攀爬模块 `climbableLayerMask` 内。

## Current Assessment

- 代码层已经具备玩家底层运动和场景交互的完整主干。
- 更可能的风险点在装配层：Prefab/场景是否挂全模块、状态机是否注册所需状态、InputAction 是否启用、LayerMask 是否匹配。
- 下一阶段应以最小验证场景为目标：地面移动、跳跃、下蹲、一个 `ESInteractableDoor`、一个 `ClimbableSurface`，逐项确认输入到 KCC、状态机、交互对象的闭环。
