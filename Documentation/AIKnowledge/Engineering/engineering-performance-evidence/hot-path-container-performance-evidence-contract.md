# 热路径容器预热、扩容、池化与性能证据合同

`KnowledgeId`: `es.engineering.hot-path-container-performance-evidence.v1`  
`Authority`: `Unity 2022.3 official documentation + Source + AIWarnings + AICommand + Skill contracts`  
`RouteKeys`: `performance`, `runtime-hot-container`, `container-warmup`, `steady-state-gc`, `capacity-growth`, `pool`, `prewarm`, `profiler`, `run-record`, `evidence`, `zero-gc`  
`RequiredReads`: 见“RequiredReads”  
`RelatedSkills`: `es-ai-knowledge-curation`, `es-performance-budgeting`, `es-observability-evidence`, `es-first-principles-analysis`, `es-adversarial-review`, `es-use-ai-command`  
`ContentHash`: `d5110365cc32a1dc4be2efd6055f363f6f4ed3fd51d3a9e80741dd5fde90d27f`
`ContentHashMethod`: 所有 SourceRef 的实际 SHA-256 按哈希字符串升序无分隔拼接，再计算 UTF-8 SHA-256  
`EvidenceLevel`: `S1`  
`RuntimeStatus`: `runtime-not-run`  
`UnityBaseline`: `2022.3.45f1 (a13dfa44d684)`  
`DiscoveryStatus`: `registered`，已登记到共享 `KnowledgeIndex.yaml` 与 `AIBRAIN_ENTRY.md`；登记只提供路由，不提升 Runtime 或性能证据等级  

## Summary

热路径性能不是由“使用了池”或“源码里没有 `new`”证明，而是由阶段、输入规模、容量、所有权和运行证据共同限定。预热负责把首次成本移出稳态窗口；扩容是必须单独预算和记录的容量突破；池化用保留内存换取创建/销毁成本，并不会自动消除调用方分配、回调分配或池 miss。没有目标平台上的 Profiler artifact，只能报告静态结构和待验证合同，不能声称实际低 GC、0 GC、商业级性能或发布就绪。

## Scope 与相邻知识边界

本条目是“通用热路径容器性能证据”的 canonical owner，负责容器/Workspace/池的预热、稳态容量、扩容、所有权、Profiler 采样和证据声明规则。它不负责具体 Shot 调度实现、Pool/Operation/Skill Track 的完整生命周期、场景发布验收流程或 Automation/AIBrain 的通用执行机制。

| 相邻条目 | 适用条件 | 本条目保留的差异 | 不应复制的内容 |
|---|---|---|---|
| `es.project.shot-performance-evidence.v1` (`entries/shot-performance-evidence.md`) | 问题明确涉及 Shot、Projectile、Weapon 或 Scheduler | 通用容器容量与 Profiler 证据规则 | Shot 专项调用链、调度与 fixture |
| `es.project.pool-operation-skill-lifecycle.v1` (`entries/pool-operation-skill-lifecycle.md`) | 问题涉及 spawn/despawn、Lease、Operation 或 Skill Track | pool miss、容量突破和稳态证据边界 | Pool/Operation/Skill Track 生命周期细节 |
| `es.project.scene-release-evidence.v1` (`entries/scene-release-evidence.md`) | 问题涉及场景、PlayMode、Player、IL2CPP 或发布验收 | Profiler 产物必须限定平台与窗口 | 场景构建、发布验收与证据升级流程 |
| `es.project.automation-aibrain-graph.v1` (`entries/automation-aibrain-graph.md`) | 问题涉及 TaskContract、Worker、PlanHash 或通用 RunRecord | 性能 sidecar 与原始产物的哈希绑定字段 | Automation/AIBrain/Graph 的通用执行状态机 |
| `es.function-area.release.v1` (`entries/function-area-routing.md`) | 用户意图是选择编译、测试、Profiler 或发布功能区 | 性能任务的具体证据决策 | 功能区总路由与工具选择 |

回退规则：先以用户的决策对象选 canonical 条目；若用户只说“性能/0 GC”而未给出系统、平台、阶段和输入规模，先加载本条目，再按对象最多补充两个相邻条目。不得为了建立上下文一次加载所有相邻条目。

## Trigger and routing

- 自然语言触发：热路径、稳态 GC、0 GC、预热后仍分配、List/Dictionary 扩容、容量尖峰、对象池 miss、池允许扩容、Profiler 证据、性能 RunRecord、Workspace 重入。
- 精确 `routeKeys`：`performance`, `runtime-hot-container`, `container-warmup`, `steady-state-gc`, `capacity-growth`, `pool`, `prewarm`, `profiler`, `run-record`, `evidence`, `zero-gc`。
- 正常预期：本条目进入 Top 1。当前协调器只保留最大 routeKey 命中数的候选；Shot、Pool 生命周期、Scene/Release 或 Automation 成为独立决策对象时，调用方必须补充对应领域键并重新规划，而不能假设次级条目会自动加载。总数仍保持 1～3 个。
- 相邻误命中风险：`es.function-area.release.v1` 保留 `profiler` 作为运行证据入口，但不再包含容器专项性能键；仅凭 `profiler` 并列时，以“是否需要具体容器性能决策”为裁决，不把功能区路由当 canonical 性能合同。
- 零命中回退：回到 `AIBRAIN_ENTRY.md`、`KnowledgeIndex.yaml` 和 AIWarnings Start 链，报告 Knowledge 覆盖缺口；不得凭旧上下文猜测。

### 路由探针（2026-08-23 静态投影）

下表是基于当前 `KnowledgeIndex.yaml` routeKeys 的静态关键词投影，不是 AIBrain Runtime 路由回执。索引或条目路由变化后必须重跑，不能把本表当长期运行事实。

| # | 真实用户问题 | 预期 routeKeys | 预期 Knowledge（1～3 个） | 静态实际结果 | 诊断 |
|---:|---|---|---|---|---|
| 1 | 对象池预热完成后为什么仍然产生 GC？ | `pool`, `prewarm`, `steady-state-gc` | 本条目 | 仅本条目 | 正常；若转向 Pool 生命周期需补充领域键重规划 |
| 2 | 运行时 List 突然扩容造成帧尖峰，应该检查什么？ | `runtime-hot-container`, `capacity-growth`, `performance` | 本条目 | 本条目 Top 1 | 正常；Release 泛性能误命中已消除 |
| 3 | 用 Profiler 证明一条热路径 0 GC 需要记录哪些证据？ | `profiler`, `zero-gc`, `evidence` | 本条目 | 仅本条目 | 正常；需要发布裁决时补充 release/player 键重规划 |
| 4 | 对象池允许扩容时如何设置上限和溢出策略？ | `pool`, `capacity-growth`, `performance` | 本条目 | 仅本条目 | 正常；生命周期问题不自动加载 |
| 5 | 只有源码检查、没有 Profiler，能否声称 0 GC？ | `zero-gc`, `profiler`, `evidence` | 本条目 | 仅本条目 | 正常；不得升级证据 |
| 6 | Pool miss、repair 和 overflowDestroy 应怎样进入性能报告？ | `pool`, `performance`, `evidence`, `run-record` | 本条目 | 仅本条目 | 正常；通用 RunRecord 细节需补充 automation 键重规划 |
| 7 | Player IL2CPP 的 0 GC 结果能否沿用 Editor 数据？ | `zero-gc`, `profiler`, `evidence`, `player`, `il2cpp` | 本条目；Unity Compile/Player/IL2CPP | 本条目；Unity Compile/Player/IL2CPP | 正常；平台证据与热路径证据同时进入最小集 |
| 8 | ProfilerRecorder 找不到 counter 时应该写 0 吗？ | `profiler`, `evidence`, `zero-gc` | 本条目 | 仅本条目 | 正常；缺失值必须 Deferred |
| 9 | 复用全局静态 Workspace 支持重入和并发吗？ | `runtime-hot-container`, `performance` | 本条目 | 本条目主命中；无独立并发专项条目 | 覆盖可用，保留未来细化缺口 |
| 10 | Profiler capture 如何绑定到 RunRecord 和 PlanHash？ | `profiler`, `run-record`, `evidence`, `automation`, `task-contract`, `automation-run-record` | 本条目；Automation | 本条目；Automation | 正常；性能 sidecar 与通用 PlanHash 规则各归 canonical owner |

当前修复结果：本条目保持容器性能 canonical；共享索引已从 `es.function-area.release.v1` 移除 `performance`、`runtime-hot-container`、`container-warmup`、`steady-state-gc`、`capacity-growth` 和 `zero-gc`，并保留 `profiler` 作为发布证据入口。索引或选择算法变化后必须重放本组探针。

## AI 执行卡

| 环节 | 不可跳过的动作 |
|---|---|
| 触发条件 | 用户要求设计、诊断或证明热路径容器/池的预热、容量、稳态 GC、扩容尖峰或 Profiler 证据 |
| 开始前必读 | `AIBRAIN_ENTRY.md` → `KnowledgeIndex.yaml` 最小路由集 → 本条目 `RequiredReads` 中的 Start、CurrentStatus、RuleIndex、命中 P0、相关源码/AICommand |
| 允许继续 | SourceRef 哈希闭合；问题的阶段、平台、输入规模、容量和 owner 已明确；操作处于当前授权范围内 |
| 必须补读 | Shot、Pool 生命周期、Scene/Release 或 Automation 成为主要决策对象时，只补读对应 canonical 条目和它的 RequiredReads |
| 必须停止 | SourceRef/ContentHash 漂移导致事实不可靠、目标/owner 不明、请求扩展到用户未点名的 Unity/外部执行，或输入会覆盖他人工作；AICommand/TaskContract 缺失只阻断受管通道 |
| Deferred/Blocked | 缺 Profiler artifact、counter、平台身份或采样窗口时标记 `Deferred（缺少运行证据）`；权限或必需来源缺失时标记 `Blocked`，不得填 0 或猜测 |
| 允许动作 | 静态读取、容量/预算设计、证据清单、限定范围的文档更新，以及当前合同明确授权的验证 |
| 禁止动作 | 把测试源码存在当测试通过；把 Editor 结果推广到 Player/IL2CPP；无界扩容；共享可变 Workspace 冒充并发安全；无权限启动 Unity、改源码、Git、发布或审计状态 |
| 失败恢复 | 保留原始输入/产物；标记失败阶段；重新解析最新来源与授权；新建独立采样或 RunId；不覆盖旧 capture，不恢复身份已失效的旧运行 |
| 完成验证 | 重放 SourceRef 与 ContentHash；检查 route/index 绑定和 UTF-8；静态结论维持 S1；只有新鲜 Profiler/Player 产物才能提升对应 Runtime 声明 |

## Decision rules

1. 先把问题归入首次调用、预热、稳态、容量突破、重建或回收阶段；阶段未明确时不得汇总“平均性能”。
2. 只有容量在采样窗口内保持不变，才能把该窗口当稳态；发生 growth/miss/repair/overflow 时立即拆成独立 case。
3. 只有 SourceRef 能证明的结构写作 verified fact；测试文件只证明测试定义存在，不证明 Test Runner 已执行。
4. 只有绑定平台、后端、场景、输入、窗口、Marker/Counter 和原始产物哈希的 Profiler 结果，才能声明该限定范围内的实际 GC/CPU 表现。
5. 缺 counter 或 capture 时使用 `Deferred（缺少运行证据）`；SourceRef、索引或 artifact 哈希漂移时标记 stale 并重新发现。
6. 设计、文档和源码修改可在当前用户明确范围内继续；启动 Unity、Worker、Profiler 或 Player 必须由用户单独点名。只有通过受管通道执行时才要求匹配 AICommand 与 TaskContract。

## 已验证事实与非声明

已验证事实：

- 项目 Unity 版本为 `2022.3.45f1`。来源：`ProjectSettings/ProjectVersion.txt`。
- P0 将首次调用、预热后稳态、容量突破、重建和诊断分成不同分配边界；只有在声明范围内由 Profiler 证明稳定帧 `GC Alloc = 0 B`，才能报告该范围实际 0 GC。来源：P0 热路径容器预热与稳态 GC 警告。
- 当前 `ESGameObjectPoolModule` 暴露 `prewarmCount`、`maxInactiveCount`、`maxTotalCount`、`allowExpand` 和 `destroyOverflow`，并记录 `missCount`、`repairCount`、`overflowDestroyCount` 等统计。来源：`MODULE_ESGameObjectPoolModule.cs` 与两份 Pool AIWarnings。
- 当前 `ESAutomationRunRecord` 记录任务/Worker 身份、Git/Unity 版本、输入输出哈希、状态、时间、错误和可选 `ExecutionSnapshot`；性能原始产物可通过 `outputs` 与 `outputHashes` 绑定，而不应把瞬时 Console 文本升级为事实。来源：`ESAutomationCenter.cs` 与 AutomationCenter/受管 Worker AIWarning。
- Unity 2022.3 官方文档将对象池描述为减少频繁创建/销毁 CPU 负担的复用手段；`ObjectPool<T>` 不是线程安全容器。`ProfilerMarker` 用于标记代码范围，`ProfilerRecorder` 用于读取 Profiler 指标，但二者都必须绑定明确场景和采样窗口。来源：`ExternalEvidenceRefs` 列出的四个 Unity 2022.3 官方页面；外部来源按其读取时间和哈希限定新鲜度。

本条目不声明：

- 当前任何 ES 热路径已经达到 0 GC、CPU 预算或内存预算。
- Editor 采样可以替代 Player、IL2CPP 或目标设备结果。
- 预热完成后不会扩容、miss、自动修补或溢出销毁。
- 池化总是优于创建/销毁，或池越大性能越好。
- 本次已运行 Unity、PlayMode、Profiler、Player 或发布验收。

## 阶段模型

| 阶段 | 允许的典型行为 | 必须记录 | 不得混入的结论 |
|---|---|---|---|
| 配置/加载 | 解析 Key、建索引、创建容器、载入 Prefab | 配置版本、输入规模、初始容量、资源来源 | 不代表正式路径已预热 |
| 首次调用 | JIT/后端初始化、首次组件查询、首次 Marker/Recorder 建立 | 首次 CPU、首次分配、触发调用链 | 不得计入稳态均值后隐藏 |
| 预热 | `EnsureCapacity`、预创建、执行正式路径的命中/未命中样本 | 预热数量、耗时、分配、完成条件、取消结果 | 不证明容量永不突破 |
| 稳态 | 复用结果与 Workspace，执行常规命中、未命中、空状态和写入 | 调用次数、实例量、帧数、GC/CPU、容量不变量 | 只有该固定范围可讨论 0 GC |
| 容量突破 | 扩容、池 miss、Instantiate、rehash、缓冲替换 | 旧/新容量、触发输入、分配、尖峰、回退动作 | 不得被稳态样本静默排除 |
| 重建/切换 | 场景/Space/Profile 切换、索引重建、批量提交 | Generation/Version、旧状态释放、恢复时间 | 不得伪装为普通稳态帧 |
| 回收/卸载 | 归还、清空、Trim、销毁空闲对象、释放证据采集器 | 活跃/空闲/总量、遗留 Lease、产物落盘结果 | 不得只凭 `SetActive(false)` 声称清理完成 |

## 结果、工作区与并发所有权

每个热路径合同必须先回答：

1. 输入和公共结果由谁拥有，是否允许原地修改。
2. 排序、筛选、去重、候选副本和快照使用的 Workspace 由谁持有。
3. Workspace 在何时清空、保留容量、Trim 或归还池。
4. 是否允许重入、并发、Job、跨线程或跨异步边界。
5. 容量突破时是扩容、拒绝、降级、排队还是丢弃。

单宿主且明确不可并发/不可重入时，可以复用实例字段；存在并发或重入时，应使用每次调用、任务私有、分区池或显式租借的 Workspace。无隔离的全局可变静态缓存不能作为 0 GC 方案。池的复用对象也必须保留 Version/Generation 或等价身份，避免旧归还、旧回调或旧 Lease 污染已重新借出的实例。

## 容量、扩容与池化合同

任何进入热路径的容器或池至少声明以下字段：

| 字段 | 合同问题 |
|---|---|
| `initialCapacity` / `prewarmCount` | 由什么负载上限或基线推出，何时完成 |
| `steadyStateCapacity` | 哪个采样窗口内必须保持不变 |
| `growthPolicy` | 增长因子、固定步长、下一档容量或禁止扩容 |
| `hardLimit` | 达到上限后的拒绝、排队、降级或销毁策略 |
| `shrinkPolicy` | 仅在卸载/Trim 边界缩容，还是长期保留峰值容量 |
| `overflowPolicy` | 池 miss、临时创建、溢出销毁及业务可见后果 |
| `ownership` | 容器、池组、租借对象、Workspace 与证据采集器的 owner |
| `reentrancy` | 归还回调、生命周期事件或递归调用如何隔离 |

池化的收益必须与代价一起记录：创建/销毁 CPU 与分配下降，换来保留内存、预热时间、重置成本、生命周期复杂度和容量误配风险。`allowExpand=true` 只是容量策略，不是性能证明；无界扩容会把峰值负载永久转化为保留内存。过小的池会产生 miss/Instantiate 尖峰，过大的池会增加加载时间和驻留内存。

## 性能预算矩阵

实际任务必须先填阈值，不能在采样后按结果反推“合格线”。除 P0 的稳态 `GC Alloc = 0 B` 目标外，本条目不替具体平台发明数值。

| Metric | Scope | First-run | Steady-state | Peak/overflow | Measurement artifact |
|---|---|---|---|---|---|
| `GC Alloc B/frame` | 固定业务链与实例量 | 单列 | 目标阈值；声称 0 GC 时必须为 `0 B` | 单列容量突破帧 | Profiler capture + counter export |
| `GC Alloc B/call` | Marker 内固定调用次数 | 单列 | 阈值与统计窗口 | miss/异常路径单列 | Marker/Recorder 结果 |
| CPU 时间 | Marker、主线程/Worker 线程 | 首次耗时 | p50/p95/p99 或明确聚合 | max 与尖峰归因 | Profiler capture |
| 容量 | 容器/Workspace/池组 | 初始值 | min/max 不变量 | 旧值、新值、增长次数 | 结构化 counters |
| Pool miss/repair/overflow | poolKey/prefab 与负载 | 预热结果 | 每窗口次数或比率 | 首次 miss 与溢出后果 | Pool stats snapshot |
| 驻留与峰值内存 | 场景/Space/Profile | 加载后 | 稳态驻留 | 峰值与卸载后残留 | Memory/Profiler artifact |
| 吞吐/延迟 | 固定批次、实体量、并发度 | 首批 | calls/frame、items/s 或 latency | 饱和与拒绝点 | 场景报告 + capture |

预算记录必须包含 `metric`、`platform`、`scenario`、`inputSize`、`warmup`、`steadyStateWindow`、`threshold`、`measurementArtifact`、`baseline`、`owner` 和 `staleWhen`。

## Profiler 采样协议

1. 固定身份：记录 branch、HEAD、工作树相关路径、Unity 版本、平台、CPU/GPU、Scripting Backend、Build 类型和场景/fixture 版本。
2. 固定输入：记录实体数、容器初始容量、池预热量、调用频率、并发度、随机种子和容量上限。
3. 分离阶段：先采首次调用与预热，再开始稳态窗口；容量突破和错误注入使用独立 case。
4. 放置 Marker：用稳定名称围住被测业务范围，避免把加载、日志和无关系统混入；调用方包装、事件和结果复制应能分别归因。
5. 记录 counters：仅使用当前 Unity/平台可用的 Profiler counter，并记录 counter 名称、单位、可用性和采样频率；缺失 counter 时结果为 `Deferred（缺少运行证据）`，不能填 0。
6. 重复采样：至少保留重复次数、每次窗口帧数和聚合方式；平均值不能隐藏单帧 GC 或容量增长。
7. 保存原始产物：Profiler capture、结构化 counters、场景输入 manifest 和摘要必须各自哈希；聊天文本和瞬时 Console 不是证据。
8. 分层结论：Editor 只支持 Editor 范围预检查；Player、IL2CPP、设备和 Release 各自需要新鲜产物，不能继承升级。

## RunRecord 与证据绑定

当前 `ESAutomationRunRecord` 可直接承载：

- `runId`, `taskId`, `taskVersion`, `operatorId`；
- `gitCommit`, `unityVersion`, Worker 类型/版本/入口哈希；
- `inputManifestHash`, `invocationHash`, 风险接受字段；
- `status`, `exitCode`, `retryCount`, 开始/结束/更新时间；
- `outputs`, `outputHashes`, `findings`, `errors`；
- 可选 `idempotencyKey`, `executionSnapshot`, `completionDecision`, `traceReconciliation`。

性能任务应把下列内容写入哈希绑定的输出 sidecar，而不是擅自扩写 RunRecord 核心类型：

```text
scenarioId
platform / scriptingBackend / buildType
inputSize / concurrency / randomSeed
warmupIterations / warmupFrames
sampleFrames / sampleIterations
markerNames / counterNames / units
initialCapacity / finalCapacity / growthEvents
poolMisses / repairs / overflowDestroys
gcAllocBytesPerFrame / gcAllocBytesPerCall
cpuAggregation / memoryPeak
profilerArtifactPath / profilerArtifactHash
verdict / claimsNotProven / staleWhen
```

若走受管 Automation，`ExecutionSnapshot` 还应绑定 `inputManifestHash`、`sourceHash`、`taskContractHash`、`commandHash` 和 AIBrain `brainPlanHash`。本次知识整理没有取得 AIBrain Runtime PlanHash，也没有启动 Worker；这不影响 S1 静态知识条目，但禁止把本次输出描述为 Runtime receipt。

## Evidence boundary

Static S1 只能证明当前来源、合同、路由和哈希闭合，以及设计中是否出现特定结构；它不能证明 Unity、PlayMode、Profiler、Player、IL2CPP、设备或发布行为。Runtime 结论必须绑定对应执行身份和原始产物，且不能从一个平台、阶段或输入规模外推到另一个范围。本条目当前保持 `runtime-not-run`。

### “0 GC”声明决策表

| 可用证据 | 允许声明 | 禁止声明 |
|---|---|---|
| 仅源码/编译/静态测试 | “未识别到某类显式分配”“设计目标为稳态 0 B” | “实际 0 GC”“性能已通过” |
| Editor Profiler 固定窗口为 0 B | “该 Editor、场景、规模、预热状态和窗口内为 0 B” | Player/IL2CPP/目标设备 0 GC |
| Player/目标后端固定窗口为 0 B | “该平台、构建、输入和窗口内为 0 B” | 未测规模、容量突破、异常路径或其他平台 |
| 稳态窗口任一目标帧有分配 | 报告字节数、频率、调用栈和归因 | 通过平均后继续称 0 GC |
| 没有 Profiler artifact 或 artifact/hash 漂移 | `runtime-not-run` / `Deferred（缺少运行证据）` / stale | 用截图、聊天摘要或旧结论代替 |

0 GC 声明必须同时限定：平台、Unity 版本、构建/后端、场景、输入规模、预热完成条件、容量未突破、采样窗口、调用次数、Marker/Counter、原始产物哈希和捕获时间。任何一个范围变化都需要重新验证。

## Common AI failure modes

### 1. 把“用了对象池”直接写成“0 GC”

- 错误行为：从池类型、`prewarmCount` 或源码缺少显式 `new` 推导实际 0 GC。
- 典型症状：结论没有平台、输入规模、稳态窗口、GC counter 或 capture 哈希。
- 根因：混淆设计手段、静态结构和运行测量。
- 预防检查：逐项检查阶段、容量不变量、调用方/回调分配、pool miss 和 Profiler artifact。
- 正确替代动作：写成“设计目标为稳态 0 B”或“静态未识别某类显式分配”，并列出测量计划。
- 恢复动作：撤回夸大声明，把结果降为 `runtime-not-run`/`Deferred（缺少运行证据）`，按采样协议重新运行独立 case。
- 缺失证据：目标平台 Profiler capture、counter 导出、输入 manifest 与产物哈希。

### 2. 把预热、首次调用或扩容帧混入稳态平均值

- 错误行为：用一个长窗口的均值隐藏首次分配或单帧容量尖峰。
- 典型症状：平均 GC/CPU 合格，但 max、growth event 或单帧调用栈缺失。
- 根因：没有先建立阶段模型和容量不变量。
- 预防检查：在采样开始/结束记录容量、miss/repair/overflow 计数，并检查窗口内是否变化。
- 正确替代动作：首次调用、预热、稳态和容量突破分别采样、分别裁决。
- 恢复动作：使混合窗口结论失效，从原始 capture 切分阶段；无法切分时重新采样。
- 缺失证据：阶段边界 Marker、逐帧 counter、旧/新容量与增长事件。

### 3. 把测试源码存在或静态 validator 通过当成 Runtime 通过

- 错误行为：看到测试文件、断言或静态 `passed` 就报告 Unity/Test Runner/Profiler 已通过。
- 典型症状：没有 Unity 日志、测试结果 XML、RunId 或 capture，却出现“测试通过”“0 GC 已验收”。
- 根因：混淆测试定义、静态合同闭合和真实执行证据。
- 预防检查：要求执行时间、环境身份、命令/Worker、退出码和原始产物。
- 正确替代动作：只报告“存在测试入口/断言”或“静态合同通过”，Runtime 保持 `runtime-not-run`。
- 恢复动作：更正结论与证据等级；取得授权后以新运行身份执行，不复用旧聊天摘要。
- 缺失证据：Unity/Test Runner/Profiler 的机器可读产物和哈希绑定。

### 4. 用无界扩容或全局 Workspace 规避当次分配

- 错误行为：启用 `allowExpand` 后不设 hardLimit，或用无隔离全局静态容器支持多宿主/重入。
- 典型症状：峰值后驻留内存持续上升、跨请求数据污染、旧回调归还到新租借对象。
- 根因：只优化单次分配，忽略 owner、并发、Generation 和生命周期成本。
- 预防检查：核对 hardLimit、overflow/shrink policy、宿主数量、重入模型和租借身份。
- 正确替代动作：采用任务私有/分区 Workspace 或显式 Lease，并为增长、拒绝、降级和卸载设合同。
- 恢复动作：停止共享写入，隔离受污染实例，按 Generation/Version 清理并重建容量基线。
- 缺失证据：并发/重入 fixture、峰值驻留内存、Lease 泄漏与恢复采样。

### 5. Counter 不可用时填 0，或沿用旧平台结果

- 错误行为：把 Recorder 缺失值当 `0 B`，或把 Editor 结果推广到 Player、IL2CPP、设备与新输入规模。
- 典型症状：报告缺 counter 名称/单位/可用性，或 artifact 的平台身份与声明不一致。
- 根因：把“没有观察到数据”误当“观察值为零”，忽略证据作用域。
- 预防检查：对齐 Unity、平台、后端、Build、场景、输入、counter、窗口和捕获时间。
- 正确替代动作：记录 counter unavailable 并标记 `Deferred（缺少运行证据）`；每个目标平台独立验证。
- 恢复动作：撤销跨平台结论，保留旧结果仅作对应范围历史基线，创建新采样。
- 缺失证据：目标环境可用 counter、对应 capture 和新鲜 artifact hash。

### 6. 路由过宽或来源漂移后仍继续使用摘要

- 错误行为：仅因 `performance`/`evidence` 命中就加载 Release 总路由，或 SourceRef/ContentHash 漂移后沿用本条目。
- 典型症状：一次加载多个重复条目、决策对象不明、引用哈希与当前文件不一致。
- 根因：没有执行 AIKnowledge 发现门禁和 canonical owner 裁决。
- 预防检查：先读 AIBRAIN 入口和索引；限制 1～3 个条目；重放 SourceRef 与 ContentHash。
- 正确替代动作：按容器性能、专项生命周期、发布证据或 Automation 对象选择 canonical 条目，只补充交叉引用。
- 恢复动作：丢弃旧摘要和计划，回读权威来源，报告 stale/路由覆盖缺口后重新规划。
- 缺失证据：新鲜索引绑定、SourceRef 哈希闭合和真实 AIBrain Runtime 路由回执。

## Execution checklist

- 开始前：确认用户意图、当前授权、branch/HEAD/相关工作树状态；读取 AIBRAIN 入口、最小 route-pack、Start/CurrentStatus/RuleIndex 和命中 P0。
- 实施中：固定 owner、阶段、输入、容量、hardLimit、overflow/shrink policy、平台和证据输出；任何 growth/miss/repair/overflow 立即拆分 case。
- 完成后：验证 SourceRef、ContentHash、index binding、UTF-8 和目标 diff；逐条列出 verified facts、non-claims 与 staleWhen。
- Runtime 后置验证：若任务获准运行，保留首次/预热/稳态/突破的独立 capture、counter、manifest、RunRecord/sidecar 与哈希；否则明确 `runtime-not-run`。
- 禁止：AI 在当前用户范围外扩展到 Unity/源码/Git/发布；用文件存在、按钮存在、日志片段或聊天摘要代替执行回执；覆盖另一轮原始产物。无匹配 AICommand/TaskContract 只阻断受管通道。

## 失败、取消与恢复

- 预热取消：记录已创建数量、已注册容量、已释放对象和再次执行的幂等边界。
- 容量突破：单独终结为 overflow/growth case；不得继续归入稳态 0 B case。
- Recorder/Marker 不可用：记录缺失名称和平台，不将缺失值写为 0。
- Profiler capture 未落盘或哈希失败：结果为 `Deferred（缺少运行证据）` 或 failed，摘要不得 Accepted。
- Domain Reload/进程退出：RunRecord 进入允许的终态，保留可重读错误与已完成产物哈希；禁止猜测恢复一个身份无效的旧 RunId。
- 输入、源码、命令、PlanHash 或 artifact 漂移：旧结果 stale，重新规划和采样。
- 重复 invocation：使用稳定输入哈希/幂等键识别；不得覆盖另一轮原始 capture。

## RequiredReads

- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_P0_热路径容器预热与稳态GC边界_AI协作警告.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_核心热路径缺失依赖不判空_AI协作警告.md`
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/对象池（Pool）/对象池预热_Space与0GC_AI协作警告.md`
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/对象池（Pool）/GameObject对象池_GameManager模块_AI协作警告.md`
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAutomationCenter与受管Worker治理_AI协作警告.md`
- `.agents/skills/es-performance-budgeting/SKILL.md`
- `.agents/skills/es-observability-evidence/SKILL.md`
- `.agents/skills/es-ai-knowledge-curation/SKILL.md`
- `Assets/Plugins/ES/AICommands/方案_性能预算与0GC_AI命令.md`

## SourceRefs

本地来源：

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_P0_热路径容器预热与稳态GC边界_AI协作警告.md` (`2f5cbca2bf00645da654a88262a228e60999e0a7af44cc35d7a8a7b8267f7665`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_核心热路径缺失依赖不判空_AI协作警告.md` (`02ba0a6d00e9a15ac0d6d4aec2c4689d5652ad284f169742b8c904e1c552440b`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/对象池（Pool）/对象池预热_Space与0GC_AI协作警告.md` (`f88f17a86b2703c968ba19aefafacfc36b79c26c0b20d567dd0e69d10b7c25a3`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/对象池（Pool）/GameObject对象池_GameManager模块_AI协作警告.md` (`6beb3f9d18ebf505170695a06e52c0065a49c0fd7628a800853bc529f355a633`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAutomationCenter与受管Worker治理_AI协作警告.md` (`a33c17c739c6394096b8892bd3eb2497ff4f02b2ecd17fd86e14b4d7ce8c3306`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationCenter.cs` (`a636a42521eb8f13462455b726c7e06fe3211cd733e5c280092af0a45673e485`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESGameObjectPoolModule.cs` (`e5904b9119fed0902e25bb048a0c24682b4e372c0873e2637785a4355a53fe27`)
- `.agents/skills/es-performance-budgeting/references/performance-budget-contract.md` (`d285bec3cfd0d86000bb828353c70b5b8bd26e498437f77dba9dd2568618ed6e`)
- `.agents/skills/es-observability-evidence/references/evidence-receipt-contract.md` (`bc4aa4619224223ad566d13473a28ce2a3073aad7f5262c7890bc37b260a5c7f`)
- `.agents/skills/es-ai-knowledge-curation/references/knowledge-entry-contract.md` (`9b13200cd6380a87c0f64aa5cc5cc4503628e34093d6a9a3d319b11b8cd5e20e`)
- `Assets/Plugins/ES/AICommands/方案_性能预算与0GC_AI命令.md` (`63daa1b9d857252c93f2f84193fd05c29ae3cdd5d09a21f46020d1541b346705`)

## ExternalEvidenceRefs

Unity 2022.3 官方文档（2026-08-23 读取，HTTP 200）。这些外部响应哈希不属于项目相对 `SourceRefs`，不参与 `ContentHash`；使用时必须按任务的新鲜度要求重新获取：

- URL: `https://docs.unity3d.com/2022.3/Documentation/Manual/performance-garbage-collection-best-practices.html`; retrieved SHA-256: `152d29e7dfe38044f672b6cb34167dc96dc36fb3c64590cd721d446f7af3d655`
- URL: `https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Pool.ObjectPool_1.html`; retrieved SHA-256: `1e56a3a1498c69ffbad7aaf0c8a3d3f3245bb3610d56984170e63cd14e31d0a8`
- URL: `https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Unity.Profiling.ProfilerMarker.html`; retrieved SHA-256: `fe4a70232585e937693f1aa69a3b725ebede33c989c2cae69aa58835fb1823d4`
- URL: `https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorder.html`; retrieved SHA-256: `77f2294a4712d4c866f64c7bec7c3f217068ccb771115b69152d51fe1facab2d`

## EvidenceRefs

- `Documentation/AIKnowledge/Engineering/engineering-performance-evidence/static-validation-receipt.json`
- Static source/configuration review only; `runtime-not-run`.
- AICommand discovery degraded: shared catalog contains unsupported `external-write` metadata for unrelated `feishu.task.mutate`; the selected `performance.zero-gc.design` record and contract were locally verified by exact path and SHA-256.
- Formal AIBrain Runtime PlanHash is unavailable and no Runtime TaskContract/Worker was invoked.

## StaleWhen

以下任一条件使本条目 stale：

- Unity 版本或四个官方文档页面内容哈希变化；
- 热路径容器 P0、Pool 规则、性能预算合同或证据回执合同变化；
- `ESGameObjectPoolModule` 的容量、扩容、统计或生命周期实现变化；
- `ESAutomationRunRecord`、`ExecutionSnapshot`、`CompletionDecision` 或 evidence binding 合同变化；
- AICommand `performance.zero-gc.design` 内容或权限元数据变化；
- 目标平台、Scripting Backend、场景、输入规模、预热条件、容量策略或 Profiler artifact 变化；
- 共享 `KnowledgeIndex.yaml` 记录与本条目的 KnowledgeId、RouteKeys、RequiredReads 或 ContentHash 不一致。
