# Entity 控制所有权与生命周期边界

`KnowledgeId`: `es.project.entity-control-ownership.v1`  
`Authority`: `Current source + AIWarnings P0`  
`RouteKeys`: `entity`, `prefab`, `domain`, `control`, `writer`, `permit`, `lifecycle`, `pool`  
`ContentHash`: `9b83fd9a47ea74d4449aeec6af2097183d62a1af209701e6305e72f948ffad9f`

## 已验证事实

### Entity 根与定义入口

`Entity` 直接继承 `Core`，并在根上持有 Basic、AI、Buff、Equipment、State 五个 Domain；KCC 仍是 Entity 的高频核心，不经过普通 Module。`OnBeforeAwakeRegister` 的当前顺序是：修复结构、捕获作者运动基线、准备 OpSupport/Tag/TransformMapping、应用同根 `EntityCharacterIdentity` 的定义、刷新默认相机请求、初始化 KCC。

Prefab 定义只有三种角色语义：

- `BuildInput`：主动清空定义，只是制作输入。
- `RuntimePoolTemplate`：身份组件不覆盖定义，由租出方调用 `Entity.BindDefinition(...)`。
- `CharacterVariant`：从同根身份组件绑定且只绑定一个 Actor、Monster 或 NPC DataInfo。

`Entity` 在回池和再次租出时都会推进 `LifecycleGeneration`。回池阶段还清理相机请求、控制 Permit、Buff、定义、ValueChange 与 Tag；旧 Token/Lease 不得作用于下一轮租户。

### 控制链是四层，不是一个 bool

```text
ESLocalControlService
  -> 选择唯一的本地受控 Entity
EntityPlayerInputWriteModule
  -> 从全局 ESInputModule 写入 EntityInputState
EntityAIDomain.inputState
  -> 保存本帧已解析意图
EntityAIDomain controlPermit + dispatchTagCondition
  -> 决定意图能否提交到 Basic/State/Skill/Interaction 等执行者
```

`ESLocalControlService.TryClaim` 只在无现有 Owner 或同一 Entity 重入时成功；显式换角必须调用 `SetControlledEntity`。`claimLocalControl` 是模块级声明：当前 `EntityPlayerInputWriteModule` 会在更新路径中反复尝试 `TryClaim`，并不等于 Prefab 初始化时已经取得控制权，也不等于取得全局输入读取资格。

`EntityAIDomain.AcquireControlBlock` 拒绝 `None` source 和零 owner，并返回代际安全的 `ESValueChangeToken`。该 Permit 不选择 Player/AI/Network writer，但当前实现会同时在 `CanPlayerWriteInput` 和最终 `UpdateInputDispatch` 阶段阻断：既清除未写入/旧意图，也重置运动、载具输入和战斗 latch。

### 帧内顺序与单点执行

Domain 更新先调用 `base.Update()`，让 writer Module 更新 `inputState`，再由 `EntityAIDomain.UpdateInputDispatch()` 单点分发。挂载状态是显式路由边界：骑乘时转交 Driver 输入并停止角色动作继续下发。相机 Look 只有本地受控 Entity 可以提交，角色 Aim/IK 意图仍留在角色域。

## 派生结论

- “谁写意图”和“意图能否执行”是不同所有权问题；当前 Permit 在写入和执行两处防守，但不能替代 LocalControl 或未来的多来源 writer 仲裁器。
- `inputState` 是帧态投影，不是持久化控制身份，也不应承载跨生命周期 Lease。
- Pool、Disable、Destroy 都是控制租期终点；新增 writer 或 Permit 时必须复用这些清理边界。

当前实现的一个未闭合边界是 `ESLocalControl` 外部持有权：Entity 的 Domain/Permit/输入清理会在 Disable、Destroy 和回池路径执行，但 Entity 生命周期本身不会自动通知 `ESLocalControl` 释放 `ControlledEntity`。由 `claimLocalControl=true` 的输入写入模块取得的控制权可在模块 Disable 时释放；若控制权由外部 `SetControlledEntity` 取得，或模块未声明自动 claim，则不能据静态源码断言回池、Disable、Destroy 后全局 Owner 已清空。此路径必须显式释放或增加生命周期回调，并在修复与测试前按“控制权可能悬挂”处理，不能把 Permit 清零等同于 LocalControl 已释放。

## 非声明

- 未验证玩家、AI、网络或载具多来源同时争用时的运行结果。
- 未运行 Prefab、PlayMode、KCC、相机、池复用或 Profiler 验收。
- 未验证外部 `SetControlledEntity` 持有者在 Entity Disable、Destroy、回池后的自动释放；当前源码静态显示该通知链缺失。
- 不声明当前控制链已满足商业级可玩闭环。

## EvidenceRefs

- `Git`: `main@a31d58c740210f79eb346415168d7ba425037564`
- `StaticReview`: 当前源码与命中 AIWarnings 已逐项读取。
- `Runtime`: `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_ES活跃请求仲裁协议_跨领域安全标准_AI协作警告.md` (`064642f794962c253c2504ae6516586d3232ce0002cdebf849433e6d0ba354ef`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Entity与世界（EntityWorld）/角色Prefab职责与DataInfo入口_AI协作警告.md` (`3ffbcc8b1030f7c47e82eff496f0a22cc892d65d04e02e97ff1116a6aba31d83`)
- `Assets/Plugins/ES/1_Design/Core_Domain_Module/Domain.cs` (`4adb66b6792a6198b6d002f93ed91556d471884c150574a7287aecfd8626ab77`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs` (`5d1f0225e27a8b04d219917fc15da5026675bc8d7b10024a17e91c8682c9751e`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Utilities/EntityCharacterIdentity.cs` (`11c0b7b888ca34faa87cee7afc2dc87db5452781ca5222f6111f9e0822b03304`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/AI/_EntityAIDomain.cs` (`28578ef54995dbcc085e7856e237bffb0292914d7b3bcae34b8152b470a99b05`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/AI/EntityAIModules.cs` (`1d2a4bd6f45cfc7841b6a0c226798370d85684fd92fc1303df70334b409a76f1`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/-GameManager_Core/ESGameManager.StaticCache.cs` (`71e4abde91fd76c253f732f966bb04fbef373a87fc0288eebb396eb87e5acf78`)

`EvidenceLevel`: `S1`（源码与规则静态核对；runtime-not-run）  
`StaleWhen`: Entity 生命周期或 Domain 顺序、Prefab Role/DataInfo 入口、LocalControl 所有权、Entity writer、controlPermit、挂载路由或任一 SourceRef 哈希变化。
