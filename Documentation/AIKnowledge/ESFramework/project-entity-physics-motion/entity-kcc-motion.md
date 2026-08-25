# Entity KCC 运动所有权与执行边界

`KnowledgeId`: `es.project.entity-kcc-motion.v1`

`Authority`: `Current project source + AIWarnings + bundled KCC source + Unity 2022.3 official documentation + project settings`

`EvidenceLevel`: `S1`

`RouteKeys`: `entity`, `motion`, `kcc`, `fixed-step`, `grounding`, `moving-platform`, `teleport`, `motion-influence`, `velocity`, `character-controller`

`ContentHash`: `4b7b52537433924fba27e888cc34bdeec1247ddcaee349cb1054099339d315ce`

`StaleWhen`: Unity 版本或官方来源响应、Time/Physics 设置、Entity/EntityKCCData、KCC Motor/System/PhysicsMover 接口或实现、运动影响测试定义、AIWarnings 运动规则或任一 SourceRef 哈希变化。

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `ProjectSettings/TimeManager.asset` (`1a83e54adbbda7c9f4851103a0c6ab7f6448a3343d4ea5b7620452fa08416ecd`)
- `ProjectSettings/DynamicsManager.asset` (`d2335e2eda7611069b27c87e7956b4f1846523ec6ef7dfbff1346375435201e8`)
- `Documentation/AIKnowledge/Unity/unity-physics-motion-authority/official-source-lock.md` (`e6136f94be9db78fcc841e406996c7e391275a26d0242ac1ffd4c36b89710d06`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/Entity运动（EntityMotion）/玩家运动_PlayerMotion_AI协作说明.md` (`5657d82f084de45029e861ae669eeea0c45c54f11a65c8c9cc00fb99f56c6b0d`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs` (`5d1f0225e27a8b04d219917fc15da5026675bc8d7b10024a17e91c8682c9751e`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/IEntitySupportMotion.cs` (`05dd352e38553e6358a737e46faca31ae40badb7f8c38a59ca0ef37204e0de1f`)
- `Assets/KinematicCharacterController/Core/ICharacterController.cs` (`c648ef29feff7168f0bc71bf91c1ca268990709194ba05331a8a4047f9cb3205`)
- `Assets/KinematicCharacterController/Core/KinematicCharacterMotor.cs` (`c68ca173eee6a183dd82339f08a7c0b538f4983ef39d4a231b1035c85d65749c`)
- `Assets/KinematicCharacterController/Core/KinematicCharacterSystem.cs` (`574051603725a8d8870ba59570722901cbe4e9ca45ecf2e9aa0973368a735384`)
- `Assets/KinematicCharacterController/Core/PhysicsMover.cs` (`e3daa660ac67a43105c400f91911f3e778ef8610e98486c7a9cf20504dd08b91`)
- `Assets/KinematicCharacterController/Core/KCCSettings.cs` (`2e3cc1a1f28228c275fb5a12e615b346c52d91f55f6333a26ca8361ab89d2aff`)
- `Assets/KinematicCharacterController/Core/IMoverController.cs` (`71c8a30f5fc4ca7f143df38e26d57e1e8fd5497300361c4669e5e1b24f33bf6b`)
- `Assets/Scripts/ESLogic/Editor/Generation/Tests/ESMotionInfluenceTests.cs` (`13741989e93d984a57c850e855eac97315178205166f22b1a9e34456818abdc8`)
- `Assets/Plugins/ES/1_Design/Tests/EntityKCCSafetyTests.cs` (`871b46b663a50fbbe4898128a8895ace6e2b8471d6ab9123850b60381e7b5458`)

## EvidenceRefs

- `Assets/Scripts/ESLogic/Editor/Generation/Tests/ESMotionInfluenceTests.cs` 只证明测试定义存在；本条目没有 Unity Test Runner 执行回执。

## 1. Scope

本条目负责生命体 Entity 的 KCC 根运动：`Entity -> EntityKCCData -> KinematicCharacterMotor` 单写入链、KCC 固定步阶段、运动能力注册、MatchTarget 位姿提交、外力/击退提交、KCC `PhysicsMover` 移动平台交互、禁用/销毁/回池清理，以及对应证据边界。

本条目不负责：

- 载具根 Rigidbody/KCC 后端、驾驶占用与骑手转交；使用 `es.project.vehicle-mount-motion.v1`。
- 输入来源、Permit 和控制所有权；使用 `es.project.entity-control-ownership.v1`。
- ESCommand Player/Runner 生命周期；使用 `es.project.command-runner-lifecycle.v1`。
- 通用 Unity Rigidbody、Collider、Trigger、Physics Query API 语义；没有匹配的独立权威条目时回读当前 Unity 版本官方资料或已安装包源码，禁止由本条目外推。

## 2. Trigger and routing

自然语言触发包括：玩家/角色移动、KCC、CharacterController、FixedUpdate、接地/斜坡、跳跃、KCC PhysicsMover、移动平台抖动、平台 Rigidbody 插值、平台 MovePosition、平台传送、击退/冲量/外力、速度接管、飞行/游泳/攀爬运动能力、MatchTarget 根运动。

精确路由为：`entity, motion, kcc, fixed-step, grounding, moving-platform, teleport, motion-influence, velocity, character-controller`。预期只加载本条目，并按任务补充最多两个相邻条目：载具任务补 `es.project.vehicle-mount-motion.v1`；输入/Permit 任务补 `es.project.entity-control-ownership.v1`。

混合路由裁决：纯 Entity/KCC 根运动优先只命中本条目；同时出现 KCC/PhysicsMover 与 Rigidbody 插值、MovePosition、SyncTransforms 或 Physics Query 时，预期先命中本条目，再补 `es.unity.physics-motion-authority.v1`。如果这类混合任务只命中通用物理条目，必须按误路由停止，不能直接套用普通 Rigidbody 方案。

可能误命中：`rigidbody` 容易命中载具，`lifecycle` 容易命中 Pool，`input` 容易命中控制链。回退时先按运动对象判定 canonical owner：Entity 根运动留在本条目；Vehicle 根运动切到载具条目；输入意图和 Permit 切到控制所有权条目。无法从自然语言推导 routeKeys 时停止自动选择，回读 AIBrain 入口、AIWarnings Start 链与当前源码，并记录路由缺口，不得用相似条目代替。

## 3. Decision rules

### 可以继续

- 目标明确是 Entity 身体能力，且根位置/旋转最终只提交给 `EntityKCCData`/Motor。
- 移动平台已明确由 KCC `PhysicsMover`/`IMoverController` 或另一种唯一后端负责，Entity Motor 与平台各自只有一个最终位姿写入者。
- 所有 SourceRef 存在、哈希与 ContentHash 一致，KnowledgeIndex 唯一绑定且 routeKeys 有交集。
- 新能力只实现实际需要的 `IEntityKCCBeforeMotion`、`IEntityKCCRotationMotion`、`IEntityKCCVelocityMotion`，并持有一个可注销注册句柄。

### 必须先读额外来源

- 涉及载具、骑乘或驾驶输入：读取载具条目和对应 Vehicle/Mount 源码。
- 涉及输入许可、AI/LocalControl 切换或控制仲裁：读取控制所有权条目、Character Attribute/Permit 源码。
- 涉及 Unity Rigidbody/Collider/Physics Query 的通用行为：读取 `es.unity.physics-motion-authority.v1`、当前 Unity `2022.3.45f1` 官方资料或本机包源码；通用 Unity 建议不能覆盖 KCC 的项目内 owner 和模拟顺序。
- 涉及移动平台：读取 `PhysicsMover`、`KinematicCharacterSystem`、Mover Controller、平台 Prefab 和查询调用方；只看到 `Rigidbody` 或官方 `MovePosition` 示例不足以决定项目实现。
- 涉及 0 GC、手感、碰撞结果或发布可用性：请求目标平台 Profiler、PlayMode/Test Runner、Player 或发布回执。

### 必须停止或降级

- 任一 SourceRef 缺失、哈希漂移、索引绑定冲突：标记 `stale`，丢弃旧计划并重新读取。
- 根运动出现第二写入者、普通 `Update` 直接写 Entity 根 Transform/Motor、反射/中央 switch/每帧扫描模块：`Blocked`。
- KCC 移动平台同时被 `PhysicsMover`、自定义 `FixedUpdate`、动画、网络或表现代码写 Transform/Rigidbody，或有人试图额外启用 Rigidbody 内建插值：`Blocked`，先收敛平台 owner 和插值 owner。
- 查询要求观察“本次平台移动后的最终物理姿态”，但调用方位于未确认的 Update/KCC 模拟阶段：`Blocked`；先确定查询相对 KCC `Simulate` 的阶段，不能用 `Physics.SyncTransforms` 掩盖第二写入者或阶段错误。
- 只有字段、按钮、Prefab、测试源码或静态检查，没有真实执行回执：保持 `S1` 与 `runtime-not-run`。
- 写入源码、索引、Unity、外部状态或发布需要的 AICommand/TaskContract 不可用：记录 `PlanTaskUnavailable`，不得改写成 `NoMatchingCommand`。

### 高风险门禁：KCC 移动平台

以下是本条目必须阻止的高风险通用化错误：

1. Unity 官方 `Rigidbody.MovePosition + Interpolate + FixedUpdate` 是普通 Kinematic Rigidbody 的通用条件规则，不是 KCC `PhysicsMover` 的直接处方。
2. 当前 KCC 在 `PhysicsMover` 校验和注册时把 `Rigidbody.interpolation` 设为 `None`，并在 KCC 系统启用插值时使用自己的 transient pose 插值。不得再开启内建插值，也不得新增平行 `FixedUpdate` 写入来“修抖动”。
3. 平台连续运动应由 `IMoverController.UpdateMovement` 向 `PhysicsMover` 提交目标；显式平台传送应通过 `PhysicsMover.SetPosition`、`SetRotation` 或 `SetPositionAndRotation` 保持 Transform、Rigidbody、initial/transient state 一致。直接写 Transform 后补 `Physics.SyncTransforms` 不是等价替代。
4. KCC 自动模拟顺序是：更新 PhysicsMover 速度 -> Motor phase 1 -> 应用 PhysicsMover transient pose -> Motor phase 2/最终移动。依赖平台最终姿态的 Query 必须绑定到明确阶段；未知阶段时停止实施并请求调用链证据。
5. 插值只能改变显示姿态，不能证明接地、碰撞、查询或平台速度正确。任何“抖动已修复”结论都必须有多 FixedTick/零 FixedTick、平台反向/传送和角色接地的 PlayMode 证据。

## 4. Verified facts

- `[S1][Project settings]` 当前项目声明 Unity `2022.3.45f1`，固定步为 `0.02` 秒，Physics gravity 为 `(0, -9.81, 0)`；这些是配置值，不是运行测量。
- `[S1][Current source]` `Entity` 要求 `KinematicCharacterMotor`，实现 `ICharacterController` 和 `IESMotionInfluenceReceiver`，并持有 `EntityKCCData`。
- `[S1][Current source]` `EntityKCCData.Initialize` 在缺少 Motor 时会尝试补齐组件并仅记录开发期警告；缺少 StateMachine 则断言后返回。这是源码的降级路径，不等于 Prefab 已满足 P0 的固定配置要求；未有实例/运行回执时必须按配置缺失风险处理。
- `[S1][Current source]` `EntityKCCData` 在 KCC 的 Before/Rotation/Velocity 阶段调度已注册能力；异常时恢复该任务修改前的旋转或速度，不把异常等同成功。
- `[S1][Current source]` MatchTarget 通过 `QueueMatchTargetPose` 排队，在 `BeforeCharacterUpdate` 使用 Motor 应用；普通业务 Update 不应争写根位姿。
- `[S1][Current source]` `TryAddVelocity` 拒绝非有限值和不允许的锁定状态；向上速度超过阈值时请求 `ForceUnground`。持续场通过 Lease 提交，生命周期结束由 `ResetMotionInfluences` 清理。
- `[S1][Current source]` `RegisterMotionFeature` 只注册对象实际实现的 KCC 阶段；`UnregisterMotionFeature(ref registration)` 检查句柄后注销并清空，源码声明重复调用安全。
- `[S1][Unity official/API]` 普通 Kinematic Rigidbody 的 `MovePosition` 在 `FixedUpdate` 中提交并服从 Rigidbody 插值；固定步与渲染帧不是一一对应，插值只管理视觉抖动，直接 Transform 修改需要单独评估物理同步。这些通用语义不覆盖 KCC 包的专用 owner。
- `[S1][Bundled KCC source]` `PhysicsMover` 用于让运动学刚体与角色正确交互；它强制 `isKinematic=true`、`RigidbodyInterpolation.None`，由 `IMoverController.UpdateMovement` 产生下一 transient pose，并在启用/禁用时向 KCC 系统注册/注销。
- `[S1][Bundled KCC source]` KCC 自动模拟由 `KinematicCharacterSystem.FixedUpdate` 驱动：先更新 PhysicsMover 速度，再执行 Motor phase 1，随后应用 PhysicsMover 位姿，最后执行 Motor phase 2 并写回角色 Transform；KCC 可在自己的 LateUpdate 路径插值 Motor 与 PhysicsMover。这不证明当前场景实例配置、Settings 或实际帧序。
- `[S1][Bundled KCC source]` `PhysicsMover.SetPosition/SetRotation/SetPositionAndRotation` 同步其 Transform、Rigidbody 和 initial/transient state；当 `MoveWithPhysics` 为真时，KCC 插值提交路径内部使用 Rigidbody `MovePosition/MoveRotation`。
- `[S1][Bundled KCC source]` `KCCSettings` 与 `IMoverController` 定义自动模拟/插值默认值和平台目标提交接口；但 `KinematicCharacterSystem` 的默认创建路径是运行时 `CreateInstance<KCCSettings>()`，静态发现的 Settings 资产、场景 Prefab 或 `IMoverController` 绑定不能据此视为已注入。`PhysicsMover.VelocityUpdate` 对缺失 `MoverController` 没有项目级安全降级回执，实例验证必须先确认绑定，否则保持 blocked/待验证。
- `[S1][Test definition]` `ESMotionInfluenceTests` 定义了速度增量一次消费、锁定切换、场叠加/限幅、陈旧 Lease、容量和非法值等用例；测试源码存在不证明已经执行或通过。
- `[P0][AIWarnings]` Entity 新身体运动能力必须沿用 Basic Domain Module 到 EntityKCCData/Motor 的链路；载具是独立责任边界；发布冻结前仍要求 PlayMode 和目标平台 Profiler。

## 5. Common AI failure modes

| 错误行为 | 典型症状与根因 | 预防检查 | 正确动作与恢复 | 尚缺证据 |
|---|---|---|---|---|
| 给 KCC PhysicsMover 开 Rigidbody 插值或另加 MovePosition | 平台和角色抖动、速度重复、接地漂移；把普通 Rigidbody 示例套到已有 KCC owner | 检查 PhysicsMover、KCC Settings、所有 Transform/Rigidbody 写入点 | 保留 PhysicsMover/IMoverController 单 writer；撤销外部插值和并行写入，恢复后复核 transient/initial pose | 平台多帧率 PlayMode |
| 平台移动后立即 Query 并用 SyncTransforms 强行修复 | 查询偶发旧/新姿态不一致；调用方阶段不明或存在直接 Transform 写入 | 标注 Query 相对 KCC Simulate、Mover phase 和 Physics Simulation 的顺序 | 先移除第二 writer，再把 Query 放到有明确姿态语义的阶段；确有合法直接 Transform 变更时才评估一次受控同步 | 同 tick 查询与成本实跑 |
| 平台传送只写 Transform/Rigidbody | 视觉位置、PhysicsMover transient state 和平台速度分裂 | 检查传送是否走 PhysicsMover 自带 Set API | 使用 PhysicsMover Set API 由同一入口对齐内部状态；失败后停止平台并恢复到已证明的一致状态，不继续累计位移 | 传送、反向、禁用恢复 PlayMode |
| 在 `Update` 直接写 Entity 根 Transform | 抖动、穿透、FixedTick 后被覆盖；根因是第二写入者 | 搜索根 Transform/Motor 写入并标注 owner/phase | 改为 KCC 回调、`QueueMatchTargetPose` 或速度提交；清除旧写入和残留状态 | PlayMode 多 FixedTick/多 Update |
| 新能力重复注册 | 回调次数翻倍、重启后任务数增长 | Start 前检查 registration.IsValid，记录三类注册计数 | 只注册一次；销毁时幂等注销；重复启停后核对计数恢复 | Unity 生命周期实跑 |
| 禁用后仍接管运动 | 飞行/攀爬/骑乘状态残留 | 每个回调首段检查 active、开关和 SupportFlags | 禁用时结束状态/输入；销毁时注销并清反向引用 | 中断与恢复 PlayMode |
| 用 Rigidbody 替代 Entity KCC | 碰撞、接地和控制链分叉 | 检查目标是 Entity 还是 Vehicle | Entity 回到 KCC；Vehicle 转载具 canonical 条目 | Prefab/场景实例检查 |
| 把外力直接加到 Transform/速度字段 | 锁定状态失效、冲量重复或丢失 | 检查 `TryAddVelocity` 返回值和 Lease 所有者 | 走运动影响 API；失败时按 `Locked/InvalidValue/NotReady` 恢复或取消 | Test Runner 与实际碰撞 |
| 把序列化值当最终手感值 | Inspector 正确但运行值被 Attribute/Permit 覆盖 | 读取最终解析值与 Permit | 诊断 runtime resolved value；证据不足标记 Verifying | 手感指标与 Profiler |
| 把静态或测试源码当已验证 | 报告“测试通过/0 GC/可发布”但无回执 | 要求证据类型、平台、入口和时间 | 降级为 S1、`runtime-not-run`，请求真实回执 | Unity/Test Runner/Profiler/Player |

## 6. Execution checklist

### 开始前

- 读取 `AGENTS.md -> AIBRAIN_ENTRY -> KnowledgeIndex`，只加载本条目及最多两个必要邻居。
- 读取 AIWarnings README、CurrentStatus、RuleIndex、Entity 运动专项和当前源码。
- 重算全部 SourceRef 与 ContentHash；检查唯一 KnowledgeId、唯一索引绑定和工作树重叠。
- 明确对象、根运动 owner、更新阶段、生命周期 owner、失败/取消/重复执行和证据目标。
- 移动平台额外列出 PhysicsMover、IMoverController、MoveWithPhysics、KCC AutoSimulation/Interpolate、平台传送入口和全部 Transform/Rigidbody 写入点。

### 实施中

- Entity 根位置和旋转保持单写入者；能力只提交候选位姿、旋转或速度。
- KCC 平台只由 PhysicsMover/IMoverController 提交；不得额外开启 Rigidbody 插值、添加平行 MovePosition 或用表现 Transform 覆盖 transient pose。
- 依赖平台最终姿态的 Physics Query 必须记录执行阶段；LayerMask、Trigger、缓冲和溢出继续遵循通用物理 canonical 条目，不在本条目复制一份规则。
- 注册必须幂等；回调先检查 active/enable/SupportFlags；异常不得留下半写入值。
- 外力检查提交结果，持续场保存并释放 Lease；回池/销毁清空运动影响。
- 热路径禁止 LINQ、闭包、反射、字符串查找、每帧 `GetComponent`、每帧分配或可增长容器。

### 完成后

- 重复启用/禁用/销毁/注销并核对注册计数、状态、输入、Lease 和反向引用恢复。
- 覆盖失败、取消、中断、重复执行、回池复用和旧 Lease 失效。
- 移动平台覆盖 25/100 FPS 等零次/多次 FixedUpdate 比例、反向、旋转、传送、禁用/重启、角色离地/落地、查询前后阶段和第二 writer 负例。
- 运行严格 UTF-8、SourceRef、ContentHash、Entry/Index 和相关 Skill 静态验证。
- Runtime 声明必须另附 Unity/Test Runner/PlayMode/Profiler/Player 对应回执。

### 禁止事项

- 禁止普通 Update、表现、镜头、挂座或网络分支直接写 Entity 根 Transform/Motor。
- 禁止用普通 Rigidbody 插值、额外 MovePosition 或无条件 SyncTransforms 修补 KCC PhysicsMover 抖动。
- 禁止创建第二套 Entity Rigidbody 运动后端或中央运动类型 switch。
- 禁止忽略提交返回值、泄漏 Lease、重复注册、静默吞掉 SourceRef 漂移。
- 禁止把文件/Prefab/按钮/测试源码存在写成运行成功或发布通过。

## 7. Evidence boundary

Static S1 可以证明当前文本、配置、官方 API 条件、KCC PhysicsMover/Motor/System 源码控制流、测试定义、SourceRef 和 ContentHash 闭包。它不能证明场景中实际 PhysicsMover/IMoverController/Settings 配置、Unity 导入/编译、Domain Reload、FixedTick 时序、接地/碰撞/Query 结果、移动平台平滑度、手感、GC Alloc、Test Runner、PlayMode、Profiler、Player、IL2CPP 或发布状态。

当前结论：`runtime-not-run`。缺少相应真实回执时，必须使用“源码支持/测试已定义/待运行验证”，不得使用“已验证、稳定、0 GC、可发布”。
