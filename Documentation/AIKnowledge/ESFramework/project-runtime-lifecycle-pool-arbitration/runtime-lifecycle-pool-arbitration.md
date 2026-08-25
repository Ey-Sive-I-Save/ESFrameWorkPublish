# ESGenericLife、Pool、Operation、Lease 与请求仲裁边界

`KnowledgeId`: `es.project.runtime-lifecycle-pool-arbitration.v1`
`Authority`: `Current source + AIWarnings P0 + Unity 2022.3 official documentation`
`RouteKeys`: `runtime`, `lifecycle`, `generic-life`, `pool`, `operation`, `lease`, `request`, `arbitration`, `commit`, `executor`, `ownership`
`ContentHash`: `a220e2ad2e9ecbe0f3d57c6e2cd978497778b8aa0cc79e85348a1b059f918150`

## AI 必读执行协议

阅读本条目后，AI 必须按以下顺序工作，不能只摘取某一段直接设计或改代码：

1. **先判新鲜度**：重新计算全部 `SourceRefs` 的 SHA-256。任一路径缺失、哈希不符或当前源码与正文冲突时，将本条目标记为 stale，回读当前源码；禁止继续引用旧结论。
2. **再判问题类型**：先在下方决策表中选且只选主要机制；确有跨域调用时再组合，不能因为都出现 `Owner`、`Version`、`Lease` 或 `Stop` 就抽取公共管理器。
3. **再写所有权句子**：在实现或建议前明确写出“谁创建、谁持有、谁可更新、谁释放、什么代际失效、失败时谁补偿”。任一项未知时停止实现并回读来源。
4. **最后判证据等级**：源码和文档只能支持 S1 静态事实。没有 Unity/PlayMode/Profiler/Player 证据时，必须保留 `runtime-not-run`，不得使用“已验证可用”“0 GC”“已完成”或“可发布”。

### 冲突裁决

当本条目与 `es.project.pool-operation-skill-lifecycle.v1` 同时被读取时，旧条目中的下列两句不得作为当前事实：

- “Version 用于拒绝旧归还请求影响重新借出的实例。”
- “重复归还旧 Version：应拒绝。”

原因不是 SourceRef 哈希漂移，而是当前 `PushToPool(GameObject)` API 没有携带或校验租用时 Version。当前源码优先于两个 AIKnowledge 摘要；除上述纠偏外，旧条目的其他结论仍需逐项按其 SourceRefs 判断，不能整篇废弃。

### 机制选择决策表

| 用户真实问题 | 选择的主要机制 | 必须保存的运行身份 | 唯一结束/提交点 | 禁止替代 |
|---|---|---|---|---|
| GameObject 被借出、归还、重置或预热 | Pool + `ESGenericLife` Pool 分部 | 当前只有对象、Pool owner、账本状态和变化中的 Version；不是完整 Lease | `PushToPool` 的归还收口 | `OnEnable/OnDisable`、Operation Stop、全子树 Reset 广播 |
| 一次执行持续持有 Audio/VFX/Tag/Permit/ValueChange 等效果 | Operation runtime state + `ESOpSupport` | 本次执行取得的 Handle/Lease | Clip/Skill/Owner 退出清理；必要时 `StopOperation` | 共享 Operation 配置字段、全局扫描、Pool Despawn 代替所有 Stop |
| `ESRuntimeTargetPack` 的创建与回收 | 创建者所有权 + rented Version | `target + rentedVersion` | `TryReturnOwned` | 裸引用、Reference 路径认领所有权 |
| 多来源争用一个 Camera/UI Focus/控制权/Voice 出口 | 活跃请求仲裁协议 | Host/View + Slot/Token + Generation 的 Lease | 领域唯一 Commit 驱动 Executor | OwnerId、裸对象、恢复栈、普通请求者直接写后端 |
| Tag 计数、Stat 合成、Resource 引用持有 | 各自领域协议 | 各领域自己的 Token/Lease/Handle | 各领域自己的清理或合成点 | 强行套用 Winner 仲裁或通用 RequestManager |

### 实现前硬停止条件

出现任一情况，AI 必须停止写实现，先向当前源码或用户补齐答案：

- 说不清该运行状态的创建者和唯一释放者。
- 需要防旧持有者操作，却只有裸 GameObject、OwnerId 或没有租用时 Generation 的引用。
- 新仲裁器说不清 Active Set owner、唯一 Commit、Executor、稳定决胜键或 Owner 回池后的清理入口。
- Operation 计划把本次 Handle、`running` 或 Lease 写入共享配置对象。
- Pool 扩展计划使用 `GetComponentsInChildren`、每次借还扫描层级，或把 `OnEnable/OnDisable` 当 Pool 回调。
- 计划声明 0 GC、线程安全、重入安全或运行验收，但没有对应 Profiler、并发模型或 Runtime 证据。

## 范围与结论

本条目只整理当前源码中的五类边界：Unity GameObject 激活生命周期、`ESGenericLife` 的 Pool 分部、GameObject Pool 账本、Operation/Skill 运行所有权，以及 `Request -> Lease -> Active Set -> Arbitration -> Commit -> Executor` 协议。

这些机制共享“明确所有者、显式结束、拒绝过期操作”的安全目标，但不是同一个抽象：

- `ESGenericLife` 组织一个根 GameObject 的框架生命周期能力；当前只落地 Pool 分部，不拥有业务状态。
- Pool 管实例创建、借出、归还、重置和容量；它不是仲裁器，也不是 Operation 所有权树。
- Operation 定义一次作用；只有产生持续状态或外部资源的执行才需要 Stop，运行凭证属于本次 Skill/Clip/Support。
- Lease 是特定宿主和代际内的写入/释放资格；仅有 `Version` 字段不等于已经实现 generation-safe Lease。
- 活跃请求仲裁用于多来源争用单一出口；Push/Update/Release 只改变 Active Set，唯一 Commit 才驱动 Executor。

## 已验证源码事实

### Unity 激活边界不是 Pool 边界

项目版本为 Unity `2022.3.45f1`。Unity 2022.3 官方文档说明：

- `Object.Instantiate` 克隆激活层级中的对象时，会按 Unity 生命周期调用其 `Awake`/`OnEnable`。
- `GameObject.SetActive(false/true)` 会停用/启用组件，并在层级激活状态变化时触发 `OnDisable`/`OnEnable`。
- `Awake` 的跨 GameObject 调用顺序不确定，不应依赖另一个对象先完成 `Awake`。

因此 Pool 不能推迟 `Awake`，也不能把 `OnEnable/OnDisable` 当作借出/归还协议。当前创建顺序是先 `Instantiate`，若 Prefab 处于激活层级，`Awake/OnEnable` 可能已经在池绑定前执行；随后才 `SetActive(false)`、显式绑定 `ESPooledGameObject/ESGenericLife`，再执行一次 `OnPoolDespawned` 建立 inactive 基线。依赖具体借出租期的初始化只能放在 `OnPoolSpawned`。

### ESGenericLife 的 Pool 分部

`ESGenericLife` 当前缓存一个同根的 `IESGameObjectPoolLifecycle` 主接收者和按具体类型唯一的扩展接收者：

```text
Spawn   : Root -> Extensions（注册顺序）
Despawn : Extensions（逆注册顺序） -> Root
```

注册、注销和换 Root 只允许在 inactive 且未派发时发生。冷路径校验只扫描根 GameObject；借还热路径不扫描子树。生命周期回调异常会被隔离，派发状态由 `finally` 收口。

### Pool 账本与回收序列

当前 `ESGameObjectPoolModule` 的主要顺序是：

```text
Instantiate
  -> SetActive(false)
  -> 绑定 ESPooledGameObject / ESGenericLife
  -> OnPoolDespawned（建立首次 inactive 基线）
  -> inactive 队列

GetInPool
  -> 先登记 active 账本
  -> MarkGetInPool
  -> OnPoolSpawned（对象仍 inactive）
  -> 处理 Spawn 内延迟归还/终止
  -> SetActive(true)
  -> 交给调用者

PushToPool
  -> 从 active 账本移除
  -> OnPoolDespawned
  -> 清 Rigidbody / Particle / Trail / Parent
  -> MarkPushToPool
  -> SetActive(false)
  -> inactive 队列或明确销毁
```

Spawn 回调中的归还请求会延迟到派发结束，避免 Spawn/Despawn 重入。失败路径通过 `try/finally` 保证实例回到可追踪的 inactive 状态或被明确销毁。预热、Scene/Space 加载和自动修补属于管理路径；`GetInPool/PushToPool` 不应遍历预热配置或动态拼接业务 key。

### Pool Version 当前不是可校验 Lease

`ESPooledGameObject.Version` 在 `MarkGetInPool` 和 `MarkPushToPool` 时递增，但当前 `PushToPool(GameObject)`、`RequestPushToPool()` 和模块内部归还路径没有接收租用时版本，也没有比较版本。

所以当前静态事实只能表述为：Version 是实例租期变化标记；它尚不能独立拒绝“旧持有者拿同一 GameObject 引用归还已再次借出的当前实例”。不要把它描述成与 `ESCameraLease` 或 `ESRuntimeTargetPack.TryReturnOwned(pack, rentedVersion)` 等价的代际安全 Lease。修复旧摘要或新增旧持有者防护时，需要单独设计返回凭证/API 与兼容边界，本条目不授权修改源码。

### Operation、Stop 与运行所有权

`ESOutputOp.NeedsStop` 默认是 `false`。一次性写入、事件或 OneShot 行为不制造虚假 Stop；只有 Start 后持续持有资源或状态的 Operation 才声明 `NeedsStop=true` 并释放本次执行取得的凭证。

`SkillOperationClipRuntimePlayer` 在构建时缓存 `NeedsStop`；实际 `_TryStopOp` 还受 `Enabled || MustTriggerStop` 二级门禁约束，但 Clip Exit 始终负责写回、TargetPack 归还和运行状态清理。共享 Operation 配置不得保存一次执行的 Handle；`ESOpSupport` 和本次 Skill/Track/Clip runtime state 才是 Audio、VFX、Tag、Permit、ValueChange、TargetPack 等运行凭证的所有权位置。

TargetPack 的创建者保存租用时 `Version`，通过 `ESRuntimeTargetPack.TryReturnOwned(target, rentedVersion)` 归还；Reference 路径只借用，不获得回收权。这是实际版本门禁，与当前 GameObject Pool 的裸对象归还 API 不同。

### 请求仲裁的六阶段协议

```text
Request -> Lease -> Active Set -> Arbitration -> Commit -> Executor
```

- Request 只是意图，不是生效结果。
- Lease 绑定 Host/View、Slot/Token 和 Generation；旧 Lease 的 Update/Release 必须安全失败。
- Push/Update/Release 只修改 Active Set 并标脏。
- Arbitration 使用确定规则选择 Winner 或合成 Modifier；同优先级必须有稳定决胜键。
- Commit 是领域唯一写入点；Executor 只执行已决策结果，不反向发明优先级。

当前 Camera 是该协议的首个源码投影：`ESCameraLease` 包含 `ViewId + SceneEpoch + Slot + Generation`；`ESCameraDirector` 的 Push/Update/Release 标脏，`LateTick` 清理失效请求、重算 Base/Shot Winner、合成 Modifier，并向 Adapter 提交一次结果。Camera 代码存在不等于 Unity/PlayMode/Profiler 已验收。

## 不能合并的边界

| 表面相似点 | 当前正确边界 | 错误合并后果 |
|---|---|---|
| `OnEnable/OnDisable` 与 Pool Spawn/Despawn | 前者由 Unity 激活层级触发；后者由 Pool 账本显式派发 | 非 Pool 激活也会误触发借还逻辑 |
| Pool `Version` 与 Lease `Generation` | 前者当前未进入归还校验；后者是更新/释放门禁的一部分 | 对旧归还能力作出不存在的保证 |
| Operation Stop 与 Pool Despawn | Stop 释放一次执行持有的持续资源；Despawn 重置一个 GameObject 租期 | 重复清理、跨 Owner 释放或遗漏 Clip Exit 收口 |
| Tag/Stat/Resource Lease 与 Winner 仲裁 | 它们可复用租期纪律，但分别是计数、合成或资源持有语义 | 形成万能管理器并丢失领域规则 |
| Owner 与 Lease | Owner 可持有多个独立请求；Lease 才标识一次可独立结束的租期 | 按 Owner 粗暴清除其他有效请求 |

## 失败与恢复检查

- Pool Spawn 回调异常：不得让实例脱离 active/inactive 两套账本；失败后 Despawn 补偿或销毁。
- Spawn 内归还：延迟到完整 Spawn 派发结束，禁止回调重入。
- Operation Enter 失败或强制退出：持续资源按本次 runtime state 清理；TargetPack 只由创建者按版本归还。
- Lease 复制、重复释放或跨代释放：只有具备 Host/Slot/Generation 校验的领域才可声明安全拒绝。
- 同优先级仲裁：结果必须可重现；不得依赖 Dictionary 枚举、对象 Hash 或未定义回调顺序。
- 容量与 GC：预热和集合复用只能形成静态结构证据；没有目标场景 Profiler 不能宣称稳态 `0 B`。

## AI 反例验收

AI 在输出实现建议前，应能对下面用例给出唯一、稳定的答案：

1. **普通 Set/Log Operation**：没有持续资源，`NeedsStop=false`；Clip Exit 仍清 TargetPack 与 runtime state。不得为了 API 对称添加空 Stop。
2. **循环 VFX Operation**：Start 获得本次实例/Handle，存入本次 runtime Support；`NeedsStop=true`，退出只释放本次凭证。不得写入共享 Operation 定义。
3. **非 Pool 的 `SetActive(true)`**：Unity 可能触发 `OnEnable`，但不得触发 `OnPoolSpawned` 语义；只有 Pool 账本可以派发 Pool 生命周期。
4. **旧调用方仍保存同一 GameObject 引用**：实例已归还并再次借出后，当前裸 `PushToPool(GameObject)` 没有租用时 Version 参数；不得声称旧调用必然被拒绝。
5. **两个 Camera Request 同优先级**：必须使用领域定义的稳定决胜规则，并在唯一 Commit 写 Adapter；请求者不能各自直接改 Cinemachine 后端。

若 AI 对任一用例给出相反答案，应视为未正确理解本条目，重新读取对应 SourceRef，而不是继续实现。

## AI 输出最小模板

涉及本领域的设计、修改或审查结论至少包含：

```text
问题类型：Pool / Operation / TargetPack / Arbitration / 其他领域 Lease
权威所有者：...
运行身份：...
创建点：...
更新门禁：...
释放或 Commit 点：...
代际失效方式：...
异常/取消/回池补偿：...
静态证据：SourceRef + SHA-256
Runtime 证据：runtime-not-run 或精确回执
禁止声明：...
```

缺字段时不得用“框架会处理”“统一生命周期”“有 Version 所以安全”等模糊句子补位。

## ExternalRefs

- Unity 2022.3 `MonoBehaviour.Awake`: `https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.Awake.html`（读取于 2026-08-23）
- Unity 2022.3 `GameObject.SetActive`: `https://docs.unity3d.com/2022.3/Documentation/ScriptReference/GameObject.SetActive.html`（读取于 2026-08-23）
- Unity 2022.3 `Object.Instantiate`: `https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Object.Instantiate.html`（读取于 2026-08-23）

外部链接用于解释 Unity 生命周期，不参与本条目的本地 `ContentHash`；项目 Unity 版本由 `ProjectSettings/ProjectVersion.txt` 绑定。

## SourceRefs

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_ES活跃请求仲裁协议_跨领域安全标准_AI协作警告.md` (`064642f794962c253c2504ae6516586d3232ce0002cdebf849433e6d0ba354ef`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_P0_热路径容器预热与稳态GC边界_AI协作警告.md` (`2f5cbca2bf00645da654a88262a228e60999e0a7af44cc35d7a8a7b8267f7665`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/对象池（Pool）/GameObject对象池_GameManager模块_AI协作警告.md` (`6beb3f9d18ebf505170695a06e52c0065a49c0fd7628a800853bc529f355a633`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/对象池（Pool）/对象池预热_Space与0GC_AI协作警告.md` (`f88f17a86b2703c968ba19aefafacfc36b79c26c0b20d567dd0e69d10b7c25a3`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/技能与Operation（SkillOperation）/Operation默认无Stop_AI协作警告.md` (`77b7554f3ca549c265f0e8fdd86be2ef6315b4d53fe4d9469bd6355f144f8704`)
- `Documentation/ES_GENERIC_LIFE.md` (`5e9fafa5add7eabdc0790266f3ae4e5b6dbeb8ab0dfe5b05ff45e9dda13f9098`)
- `Documentation/SKILL_OPERATION_LIFECYCLE.md` (`9579666ecd3aa2a2185a835e38e8577ca57214f63f398c26beb15797374d80cc`)
- `Assets/Scripts/ESLogic/Runtime/Life/ESGenericLife.cs` (`519aad2dfef5778a906962d6ebce516ecfad983a2b5c526d76b162eb0c599425`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESGameObjectPoolModule.cs` (`e5904b9119fed0902e25bb048a0c24682b4e372c0873e2637785a4355a53fe27`)
- `Assets/Scripts/ESLogic/Runtime/Operation/Operations/ESOutputOp.cs` (`654889fb787f216dc88819bd88197aa97b871251c6737bfd46ee3be02062e87d`)
- `Assets/Scripts/ESLogic/Runtime/Operation/RuntimeServices/ESOpSupport.cs` (`4184f6c4264acbe5551af15140b7adaa1232400e1160182cc818e2d1936edc02`)
- `Assets/Scripts/ESLogic/Runtime/Skill/SkillSequence/Tracks/SkillTrackItem_Operation.cs` (`cf1f75ce54902dd936dab6c74118e1c4a1fa92c5b329119042c4998cb749f899`)
- `Assets/Scripts/ESLogic/Runtime/Camera/Core/ESCameraContracts.cs` (`84a29a3c96fd6d3b76175e1e30758d7d794a6b8b2a8de842a3b17fca8e83d703`)
- `Assets/Scripts/ESLogic/Runtime/Camera/Core/ESCameraDirector.cs` (`a0b7dffee4d1518c2a1c89e50f9f68dcf2f136f02a63c2642e9039199fd441c9`)
- `Assets/Plugins/ES/1_Design/Tests/ESGenericLifePoolTests.cs` (`57f1260c75da436d7f8e9c9cc0befc3332c8c4107f52e7ef60cbc4d8878c47cc`)
- `Assets/Plugins/ES/1_Design/Tests/SkillTrackLifecycleIsolationTests.cs` (`fda8013177ed153d1b275735f9dbb210b436f7b3f0302909b05cfad658c691d2`)
- `Assets/Plugins/ES/1_Design/Tests/ESCameraDirectorTests.cs` (`aac27dba287e21607b21fcde544e0b4f4ea44a6b49856412181091117298cb40`)

`EvidenceLevel`: `S1`
`RuntimeEvidence`: `runtime-not-run`
`StaleWhen`: 任一 SourceRef 哈希、Unity 版本或官方生命周期合同变化；GameObject Pool 新增/移除租期版本校验；`ESGenericLife` 分部或派发顺序变化；Operation `NeedsStop`、TargetPack 所有权或 `ESOpSupport` 清理边界变化；请求仲裁的 Lease、Active Set、Commit 或 Executor 合同变化。

## 静态测试绑定

- `ESGenericLifePoolTests`：池化借出/归还、回调异常后的账本收口和重入边界；
- `SkillTrackLifecycleIsolationTests`：`NeedsStop`、TargetPack 版本归还和旧租用者不得回收新租期；
- `ESCameraDirectorTests`：Active Set、稳定 Winner、过期 Lease 和唯一提交路径。

以上仅绑定当前测试源码，未执行 Unity Test Runner；不能升级为 Runtime/Profiler/0 GC 证据。

## 非声明

- 未运行 Unity Editor 编译、Test Runner、PlayMode、Profiler、Player 或 IL2CPP。
- 未证明 Pool、Operation 或 Camera 在目标业务场景达到 0 GC、性能预算或发布验收。
- 未把 Camera 首切片推广成跨领域通用实现，也未授权修改 Pool 归还 API。
- 已登记到共享 `KnowledgeIndex.yaml` 与 `AIBRAIN_ENTRY.md`；登记只提供路由，不提升 Runtime 或发布证据等级。
