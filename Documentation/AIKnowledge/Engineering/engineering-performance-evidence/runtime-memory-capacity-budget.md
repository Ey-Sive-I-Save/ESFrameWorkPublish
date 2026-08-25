# 容量、驻留内存与池化成本预算

`KnowledgeId`: `es.engineering.runtime-memory-capacity-budget.v1`  
`Authority`: `Unity 2022.3 official documentation + current Pool source + AIWarnings + Skill contract`  
`RouteKeys`: `performance`, `memory-budget`, `resident-memory`, `capacity-budget`, `high-water-mark`, `pool-size`, `cache-size`, `prewarm`, `trim`, `retention`, `pool`, `memory-profiler`, `gc-tradeoff`  
`RequiredReads`: 见“RequiredReads”  
`RelatedSkills`: `es-performance-budgeting`, `es-observability-evidence`, `es-ai-knowledge-curation`, `es-knowledge-validator`, `es-first-principles-analysis`, `es-adversarial-review`  
`ContentHash`: `f50ce611088e4221ab280053b308f53d059a469b68a6cb04727d917aec88bc68`  
`ContentHashMethod`: 所有本地 SourceRef 的实际 SHA-256 按哈希字符串升序无分隔拼接，再计算 UTF-8 SHA-256  
`EvidenceLevel`: `S1`  
`RuntimeStatus`: `runtime-not-run`  
`UnityBaseline`: `2022.3.45f1 (a13dfa44d684)`  
`DiscoveryStatus`: `registered`，已通过当前用户明确人工批准登记到共享 `KnowledgeIndex.yaml` 与 `AIBRAIN_ENTRY.md`；本次未调用 AIBrain Runtime，不声称取得 PlanHash

## Summary

池化、缓存和预分配通过保留对象与容量换取较少的创建、销毁、扩容和 GC 压力，但保留内存、加载时间、重置成本与生命周期复杂度会同步上升。容量不能按“能放多少”单点决定，必须同时预算活跃量、空闲保留量、每项 retained size、峰值持续时间、命中/miss、扩容、溢出、Trim/卸载边界和恢复成本。`GC Alloc = 0 B/frame` 不等于驻留内存低；池更大也不等于性能更好。

## Scope 与责任边界

本条目是容量、高水位、驻留内存、池/缓存保留成本和 Trim 决策的 canonical owner。它不负责：

- Pool Spawn/Despawn、Lease 或 Generation 的完整生命周期协议；
- 静态判断具体 C# 语句是否分配；
- 代替 Profiler/Memory Profiler 给出实际字节；
- 仅凭平均并发量批准容量；
- 把 Editor 采样推广到 Player、IL2CPP 或目标设备。

相邻条目分工：

| 决策对象 | canonical owner | 本条目的连接点 |
|---|---|---|
| 预热、稳态 GC、扩容与性能证据 | `es.engineering.hot-path-container-performance-evidence.v1` | 为其补充驻留内存和容量预算 |
| C# 托管分配静态识别 | `es.engineering.managed-allocation-static-audit.v1` | 输入“可能分配”的来源，不替代测量 |
| Pool 生命周期 | `es.project.pool-operation-skill-lifecycle.v1` | 使用其 owner/归还/清理边界，不复制协议 |
| Resource Scope 与发布资源 | 资源/发布 canonical 条目 | 资源域卸载决定可回收时间点 |

## Trigger and routing

- 自然语言触发：池要开多大、缓存越大是否越好、预热数量、常驻内存、高水位、峰值后不降、Trim、对象池内存换 GC、Memory Profiler、容量上限、内存预算。
- 精确 routeKeys：`memory-budget`, `resident-memory`, `capacity-budget`, `high-water-mark`, `pool-size`, `cache-size`, `trim`, `retention`, `memory-profiler`, `gc-tradeoff`。
- 仅问 `foreach`、boxing、closure 或某行是否 GC 时，应由托管分配静态审查主导。
- 仅问 Spawn/Despawn 回调顺序、Lease 或归还所有权时，应由 Pool 生命周期条目主导。

## 第一原则

1. 容量是业务并发分布与恢复策略的结果，不是一个脱离场景的常量。
2. 任何池或缓存都必须同时写“收益预算”和“保留成本预算”。
3. 平均值不能代表峰值；单次极端峰值也不能无条件固化为永久容量。
4. 总容量、活跃容量与空闲保留容量必须分开。空闲对象仍可能保留 GameObject、组件、托管集合、Native/GPU 资源或间接引用。
5. Trim 只能在明确安全边界执行；它会降低驻留量，但可能把下一次增长成本重新带回运行路径。
6. 没有目标平台 Memory Profiler/Profiler artifact 时，只能给预算模型和待测字段，不能填造实际 MB。

## 必填预算合同

每个池、缓存、Registry 或复用 Workspace 至少填写：

| 字段 | 含义 |
|---|---|
| Owner | 谁创建、持有、清理，生命周期域是什么 |
| Platform/backend | 目标设备、Player/Editor、Mono/IL2CPP |
| Scenario | 场景、玩法阶段、资源域和内容版本 |
| Unit identity | pool key、prefab、cache key、buffer type |
| Steady active | 稳态活跃量的分位值或上限 |
| Burst high-water | 峰值活跃量、持续时间和同时发生的其他池 |
| Prewarm | 预热量及其依据 |
| Max total | 活跃加空闲的总上限 |
| Max inactive | 允许长期保留的空闲上限 |
| Per-unit retained | 每个空闲/活跃项 retained size 的测量值或 Deferred |
| Baseline resident | 场景稳定后的基线驻留量 |
| Peak resident | 压力窗口峰值与原始快照 |
| Post-burst resident | 峰值结束、GC、卸载/Trim 后的驻留量 |
| Miss/expand/overflow | 计数、发生频率和处理策略 |
| Reset cost | 归还/租出重设 CPU、回调和资源恢复成本 |
| Trim boundary | 何时可释放、释放多少、下一次如何恢复 |
| Threshold | 明确数值门槛；未知必须 Deferred |
| Artifact | Profiler/Memory Profiler capture、日志或 RunRecord 哈希 |
| Owner/staleWhen | 维护责任人与失效条件 |

预算总合同沿用 `performance-budget-contract.md`：metric、platform、scenario、input size、warmup、steady-state、threshold、artifact、baseline、owner 和 staleWhen 缺一不可。

## 容量模型

### 1. 数量模型

```text
totalCount = activeCount + inactiveCount
headroom = maxTotalCount - observedHighWaterActive
retainedIdleCount = min(inactiveCount, maxInactiveCount)
```

这些公式只描述数量，不代表内存字节。容量批准至少需要高水位分布、峰值持续时间和 miss 成本。对长尾尖峰，应比较：永久保留峰值容量、峰值时受控扩容、降级/拒绝、分批预热四种策略，而不是自动选择最大值。

### 2. 驻留成本模型

```text
estimatedPoolRetained = poolInfrastructure
                      + activeCount * measuredActiveRetainedPerUnit
                      + inactiveCount * measuredInactiveRetainedPerUnit
                      + retainedReferencedAssets
```

这是预算结构，不是精确 Unity 内存会计。共享资源、Native Object、纹理/网格、Managed Shell 与引擎侧内存可能重复引用或由别的 owner 持有，禁止用对象数乘 Inspector 大小冒充真实 retained size。必须用 Memory Profiler 的引用链和分类确认 owner。

### 3. 容器容量成本

`List<T>.Capacity`、Dictionary buckets/entries、Queue/HashSet 缓冲和数组容量即使逻辑 Count 下降也可能继续保留。清空内容通常不会自动缩容。是否 Trim 必须比较保留字节、Trim CPU/分配、下次扩容尖峰和调用安全点。

## 当前 ESGameObjectPool 事实

当前源码可静态确认：

- `ESGameObjectPoolConfig` 包含 `prewarmCount`、`maxInactiveCount = 64`、`maxTotalCount = 256`、`allowExpand = true`、`destroyOverflow = true`、自动修补相关字段。
- Pool 组维护 active、inactive、created、rent、return、miss、repair、overflowDestroy 和 prewarm source 计数。
- 释放预热源后，在无其他预热 source 时可清理独占组的空闲对象；存在活跃对象时不会无条件销毁它们。
- `ClearExclusiveGroup` 会销毁 inactive 队列中的 GameObject；组只有在没有预热 source、active 和 inactive 时才从字典移除。
- 这些事实说明系统具备容量和清理控制点，不证明默认 `64/256` 适合任何具体 Prefab，也不证明当前驻留内存、命中率或 Trim 成本达标。

## 决策规则

### Prewarm

- 预热量优先覆盖可重复出现的稳态需求和可接受的启动/加载预算。
- 不用历史最大值直接充当预热量；应记录 P50/P95/P99 或设计上限、峰值持续时间和冷启动 miss 成本。
- 多个池会同时预热时，必须计算总加载时间与总驻留量，不能逐池局部最优。

### Expand

- `allowExpand=true` 是行为策略，不是预算通过。
- 扩容必须有 `maxTotal`、告警阈值、发生阶段、miss 计数和恢复策略。
- 峰值扩容后若容量永久保留，必须进入 post-burst resident 采样；否则只是把一次峰值永久化。

### Inactive retention

- `maxInactive` 应由下一个复用窗口、单项 retained size 和重建成本共同决定。
- 大型 Prefab、持有大数组/缓存的组件或间接资源引用不能与轻量 VFX 使用同一经验默认值。
- 空闲对象仍在场景层级、Managed Heap 或 Native 侧存在，不能因 inactive 就记作零成本。

### Trim / unload

- 推荐边界：场景/区域/ResourcePlan 离开、玩法阶段切换、长时间低负载、显式内存压力处理。
- 禁止在每帧或高频归还路径随意 Trim；这会造成销毁、下次重建和潜在 GC/CPU 抖动。
- 清理前必须确认没有活跃 owner、Lease、异步回调或共享预热 source；生命周期裁决交给对应 Pool/Resource canonical owner。

### Cache

- 缓存必须有 key 上限、淘汰/失效规则、负缓存期限、版本/Generation 和 owner。
- 无界 Dictionary、按内容版本不断增长的 key、静态跨场景缓存和未解绑的回调都可能把暂时峰值变成永久根引用。
- 降低重复计算不等于值得永久保留；要比较命中收益、查找/维护 CPU、保留字节与失效复杂度。

## 预算裁决矩阵

| 现象 | 不能直接得出的结论 | 正确下一步 |
|---|---|---|
| `GC Alloc = 0 B/frame` | 内存低、无泄漏、池大小合理 | 比较 baseline/peak/post-burst 快照与引用链 |
| Pool miss 为 0 | 预热合理 | 检查是否以过大驻留换来零 miss |
| miss 很少 | 应无限扩容 | 比较 miss 帧成本、降级策略和峰值保留成本 |
| inactive 很多 | 一定泄漏 | 检查配置上限、预热 source、复用窗口和 owner |
| GC 后 Managed Used 下降 | 总驻留已恢复 | 继续看 reserved、Native、GPU 和引用资源 |
| `Clear()` 后 Count 为 0 | 容量和内存已释放 | 检查 Capacity、引用清除、Native 对象和资源 owner |
| Editor Memory Profiler 正常 | 目标设备预算通过 | 在目标 Player/平台复验 |
| 峰值快照很高 | 一定要立刻 Trim | 区分合理活跃峰值、共享资源、泄漏与可恢复保留 |

## AI 常见失败模式

1. 只优化 GC：无限增加预热、池和缓存，却不计算 retained memory。
2. 只看平均：用平均 active 数设置上限，忽略瞬时并发和峰值持续时间。
3. 只看最大：把一次极端峰值永久固化，导致所有场景长期付费。
4. 数对象冒充量内存：忽略 Prefab、Native Object、纹理/网格和共享引用。
5. 把 `Clear()` 当释放：未检查 Capacity、静态根、事件和资源 owner。
6. 把 GC 降低写成内存降低：二者可能方向相反。
7. 无证据填阈值：没有目标设备和快照却写固定 MB 或默认容量为“商业级”。
8. 激进 Trim：在高频或不安全生命周期销毁，造成下一次尖峰或悬空回调。

## 测量与证据要求

最小实验应覆盖：

1. 冷启动前基线；
2. 预热完成后；
3. 稳态负载；
4. 设计峰值和长尾峰值；
5. 峰值结束后等待业务归还；
6. 显式 GC 前后（仅用于观察，不代表生产策略）；
7. 场景/资源域卸载或 Trim 后；
8. 再次进入负载时的恢复 CPU、分配和 miss。

每个时间点记录 active/inactive/total/high-water、miss/repair/overflow、Managed Used/Reserved、Native/Graphics（适用时）、总驻留、采样平台、输入规模与 artifact hash。Memory Profiler 快照要比较引用链和 owner；不能只抄 Summary 数字。

## AI 执行卡

| 环节 | 必须动作 |
|---|---|
| 开始 | 确定池/缓存 owner、平台、场景、业务规模与生命周期域 |
| 静态审查 | 找到容量字段、默认值、扩容、溢出、清理和统计实现 |
| 建模 | 填写数量、单项 retained、共享资源和 post-burst 模型 |
| 方案 | 同时比较预热、扩容、拒绝/降级、Trim 四类策略 |
| 运行验证 | 绑定 Profiler/Memory Profiler 原始 artifact 和哈希 |
| 结论 | 分开报告 GC、CPU、Managed、Native/GPU 和驻留恢复 |
| 缺失数据 | 标记 `Deferred`，不得写 0 或经验 MB |
| 变更权限 | 修改源码、配置、Unity 资产或运行 Unity 前另取匹配授权 |

## 验收清单

1. 是否同时写明活跃、空闲和总容量？
2. 是否有高水位分布、峰值持续时间和多池同时峰值？
3. 是否测量而非猜测单项 retained size？
4. 是否区分 Managed、Native、GPU、共享资源和总驻留？
5. 是否记录预热时间、reset cost、miss、repair、overflow？
6. 是否观察 peak 后、GC 后、卸载/Trim 后和再次进入负载？
7. 是否有明确 maxTotal、maxInactive、Trim 边界和恢复策略？
8. 是否避免把零 GC、零 miss 或 Count=0 冒充低内存？
9. 是否绑定目标平台、后端、输入规模、artifact、owner 和 staleWhen？
10. 是否保留 Pool/Resource 生命周期 canonical owner 的裁决权？

## RequiredReads

- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_P0_热路径容器预热与稳态GC边界_AI协作警告.md`
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/对象池（Pool）/GameObject对象池_GameManager模块_AI协作警告.md`
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/对象池（Pool）/对象池预热_Space与0GC_AI协作警告.md`
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESGameObjectPoolModule.cs`
- `.agents/skills/es-performance-budgeting/references/performance-budget-contract.md`

## SourceRefs

本地来源：

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_P0_热路径容器预热与稳态GC边界_AI协作警告.md` (`2f5cbca2bf00645da654a88262a228e60999e0a7af44cc35d7a8a7b8267f7665`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/对象池（Pool）/GameObject对象池_GameManager模块_AI协作警告.md` (`6beb3f9d18ebf505170695a06e52c0065a49c0fd7628a800853bc529f355a633`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/对象池（Pool）/对象池预热_Space与0GC_AI协作警告.md` (`f88f17a86b2703c968ba19aefafacfc36b79c26c0b20d567dd0e69d10b7c25a3`)
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESGameObjectPoolModule.cs` (`e5904b9119fed0902e25bb048a0c24682b4e372c0873e2637785a4355a53fe27`)
- `.agents/skills/es-performance-budgeting/references/performance-budget-contract.md` (`d285bec3cfd0d86000bb828353c70b5b8bd26e498437f77dba9dd2568618ed6e`)

## ExternalEvidenceRefs

Unity 官方页面于 2026-08-23 读取，HTTP 200。响应 SHA-256 不参与项目相对 SourceRef 的 `ContentHash`，页面变化后必须重新读取：

- Memory overview: `https://docs.unity3d.com/2022.3/Documentation/Manual/performance-memory-overview.html`; SHA-256 `53df746ed027aa8cd6a0777991688ba1670680fbfd627c44894534162a30c807`
- Managed memory: `https://docs.unity3d.com/2022.3/Documentation/Manual/performance-managed-memory.html`; SHA-256 `bb7a223c6e9747c8d6cfcdfbfeafc375167cda3266edd9a930328126231e6dd5`
- Garbage collection best practices: `https://docs.unity3d.com/2022.3/Documentation/Manual/performance-garbage-collection-best-practices.html`; SHA-256 `152d29e7dfe38044f672b6cb34167dc96dc36fb3c64590cd721d446f7af3d655`
- `ObjectPool<T>`: `https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Pool.ObjectPool_1.html`; SHA-256 `1e56a3a1498c69ffbad7aaf0c8a3d3f3245bb3610d56984170e63cd14e31d0a8`
- Memory Profiler module: `https://docs.unity3d.com/2022.3/Documentation/Manual/ProfilerMemory.html`; SHA-256 `b802224eb3d894f6ad143037ae4baf438cb99e08e2379a5af457cf659a850e06`
- Memory Profiler package 1.1: `https://docs.unity3d.com/Packages/com.unity.memoryprofiler@1.1/manual/index.html`; SHA-256 `418dfe54d7a11b43f204b7852dbcbf6df7d136d7d6bb4defc379d4ff8cc5efd2`

## Evidence Boundary

- 当前仅完成规则、源码与官方文档静态审查，`runtime-not-run`。
- 未运行 Unity、Profiler、Memory Profiler、Player、IL2CPP 或压力 fixture。
- 所有实际 MB、分位值、阈值和恢复时间均为 `Deferred`，必须由目标项目场景采证。
- 共享索引和 AIBRAIN 功能区已登记；该登记只提供静态发现，不授予执行权限，也不提升 Runtime 证据等级。

## StaleWhen

以下任一变化使本条目 stale：Unity 版本或六个官方页面响应哈希变化；Pool P0/专项规则、性能预算合同或 Pool 源码变化；Pool 容量字段、统计、清理、预热 source 或生命周期改变；目标平台、资源内容、Prefab 组成、业务并发分布或内存阈值变化；共享索引中的 KnowledgeId、RouteKeys、RequiredReads 或 ContentHash 与正文不一致；新增运行证据改变现有预算结论。
