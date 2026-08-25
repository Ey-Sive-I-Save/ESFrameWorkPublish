# 托管分配静态识别与误报边界

`KnowledgeId`: `es.engineering.managed-allocation-static-audit.v1`  
`Authority`: `Microsoft C# official documentation + Unity 2022.3 official documentation + AIWarnings P0 + Skill contract`  
`RouteKeys`: `performance`, `managed-allocation`, `allocation-static-audit`, `boxing`, `closure`, `delegate`, `foreach`, `iterator`, `yield`, `async`, `linq`, `gc`, `false-positive`, `hot-path`  
`RequiredReads`: 见“RequiredReads”  
`RelatedSkills`: `es-performance-budgeting`, `es-ai-knowledge-curation`, `es-knowledge-validator`, `es-first-principles-analysis`, `es-adversarial-review`  
`ContentHash`: `d567b65cad6548801d9ab52572406b8f0b8e08ace5192e7f4411cc0b0efc9858`  
`ContentHashMethod`: 所有本地 SourceRef 的实际 SHA-256 按哈希字符串升序无分隔拼接，再计算 UTF-8 SHA-256  
`EvidenceLevel`: `S1`  
`RuntimeStatus`: `runtime-not-run`  
`UnityBaseline`: `2022.3.45f1 (a13dfa44d684)`  
`DiscoveryStatus`: `registered`，已通过当前用户明确人工批准登记到共享 `KnowledgeIndex.yaml` 与 `AIBRAIN_ENTRY.md`；本次未调用 AIBrain Runtime，不声称取得 PlanHash

## Summary

静态审查能识别“可能形成托管对象、数组、委托、闭包、迭代器或状态机”的源码形态，但不能仅凭关键字、API 名称或反编译印象断言实际分配字节。正确裁决必须同时检查编译期类型、泛型约束、捕获状态、调用频率、首次与稳态阶段、Unity/C# 编译器版本以及调用方包装。静态结论最多是“已识别明确分配”“存在条件性分配风险”或“未发现已知显式分配”；实际 `GC Alloc` 必须由对应平台和场景的 Profiler 证据签收。

## Scope 与责任边界

本条目负责 C# / Unity 托管分配的静态识别、误报收口和审查表达，不负责：

- 声明某条路径实际分配多少字节；
- 证明 Player、IL2CPP 或目标设备达到 `0 B`；
- 制定池容量、驻留内存或 Trim 预算；
- 取代热路径总合同中的阶段、容量和 Profiler 证据要求；
- 因追求零分配而删除必要的正确性、诊断、原子性或所有权边界。

相邻 canonical owner：

| 决策对象 | 应加载的条目 | 本条目保留的职责 |
|---|---|---|
| 预热、稳态、扩容、Profiler 证据 | `es.engineering.hot-path-container-performance-evidence.v1` | 判断源码形态是否可能分配 |
| 容量、高水位、池/缓存驻留成本 | `es.engineering.runtime-memory-capacity-budget.v1` | 只提供分配来源，不决定容量 |
| Pool Spawn/Despawn 生命周期 | `es.project.pool-operation-skill-lifecycle.v1` | 不重复生命周期协议 |
| Shot/Projectile 专项性能 | `es.project.shot-performance-evidence.v1` | 通用静态规则，不复制 Shot 调用链 |

## Trigger and routing

- 自然语言触发：这段 C# 会不会 GC、`foreach` 是否分配、闭包/委托是否分配、装箱、`yield`、`async`、LINQ、静态 0 GC 审查、GC 误报。
- 精确 routeKeys：`managed-allocation`, `allocation-static-audit`, `boxing`, `closure`, `delegate`, `foreach`, `iterator`, `yield`, `async`, `linq`, `false-positive`。
- 仅出现 `memory`、`pool size`、`resident` 或 `high-water` 时不应由本条目主导，应路由到容量与驻留内存条目。
- 没有具体源码、编译期类型或调用上下文时，只能给风险分类，不能给确定性分配结论。

## 静态裁决等级

| 等级 | 可写结论 | 必要条件 | 禁止升级 |
|---|---|---|---|
| A：明确语义 | `已识别托管分配` | 语言或 API 语义要求创建托管对象，且该表达式确在目标路径执行 | 不写具体字节数 |
| B：条件性风险 | `可能分配，需解析编译期形态/生成代码` | 分配取决于捕获、装箱、枚举器形态、同步完成、缓存或编译器实现 | 不直接判违规 |
| C：静态未发现 | `未发现已知显式托管分配` | 已检查目标方法及调用方包装，未命中已知来源 | 不等同 `GC Alloc = 0 B` |
| D：运行签收 | `限定场景实际 GC Alloc ...` | 有平台、后端、输入、预热、窗口和原始 Profiler artifact | 不跨平台/阶段外推 |

## 核心决策流程

1. 先确认该代码是否处于每帧、每实体、批量循环等热路径；冷路径也可优化，但不能套用相同门槛。
2. 区分构造/首次调用、预热后稳态、容量突破、异常与诊断路径。
3. 以表达式的编译期类型和实际重载为准，不以变量运行时类型或方法名猜测。
4. 识别显式对象/数组创建，再检查隐式装箱、捕获闭包、委托构造、迭代器、异步状态机和调用方结果包装。
5. 对条件性形态检查编译器生成代码或 IL；生成代码仍只证明结构，不证明目标平台实际字节。
6. 最后用 Profiler 在真实调用链中归因；静态结论与运行结果冲突时，以可复现运行证据为性能事实，同时回查采样噪声和遗漏调用方。

## 分配形态与误报矩阵

| 形态 | 静态默认 | 需要检查 | 常见误报或漏报 |
|---|---|---|---|
| `new` 引用类型、数组 | A | 是否实际执行、是否在稳态路径 | 把预热期创建算进稳态；漏掉数组语法 |
| 值类型转 `object` 或接口 | A/B | 泛型约束、constrained call、实际重载 | 看到接口就断言装箱；忽略非泛型 API 的真实装箱 |
| 捕获局部变量的 lambda/匿名函数 | A/B | 闭包生命周期、委托是否缓存、创建位置 | 只看 lambda 关键字；忽略捕获发生在循环内 |
| 不捕获 lambda、静态 lambda、方法组 | B | 编译器版本与缓存行为、委托逃逸 | 一律判每次分配，或一律判零分配 |
| `foreach` | B | 编译期集合类型、`GetEnumerator` 返回类型、是否转接口 | 看到 `foreach` 就报错；忽略接口枚举导致装箱/对象枚举器 |
| `yield return` / `yield break` | A/B | 迭代器对象创建位置、枚举次数、逃逸 | 只看循环体，漏掉调用迭代器方法时创建的状态对象 |
| `async` / `await` | B | 同步完成、是否挂起、返回 `Task`/`ValueTask`、builder/continuation | 一律认定分配，或因状态机是 struct 就认定零分配 |
| LINQ | A/B | 具体操作符、迭代器、委托、捕获与最终物化 | 只查 `ToList`，漏掉惰性查询对象；把所有扩展方法都当 LINQ |
| `params` | A/B | 调用点是否创建数组、是否已有数组重载 | 只审方法体，不审调用点 |
| 字符串拼接/格式化/插值 | A/B | 常量折叠、结果是否为新 string、handler 重载 | 把编译期常量也报成运行分配；漏掉日志参数构造 |
| `List<T>`/`Dictionary<TKey,TValue>` 查询 | C/B | 容量变化、键构造、比较器、接口枚举 | 因集合类型名直接判分配；忽略扩容和临时 key |
| struct 返回、`out` 参数、Span 风格 View | C/B | 是否被装箱、是否逃逸到接口/对象 | 因值拷贝误报堆分配 |
| 异常、日志、堆栈、完整快照 | A/B | 是否属于正常业务分支、参数是否预先构造 | 只看异常是否真的抛出；忽略日志调用前已完成的字符串构造 |

## 关键语言事实与限定

### Boxing

Microsoft 官方文档明确：把值类型转换为 `object` 或其实现的接口会发生 boxing，并在托管堆上创建对象。静态审查必须检查重载解析和泛型约束；受约束泛型调用、值类型自身实现方法或完全不经过 `object`/接口的路径不能仅凭“用了接口概念”判定装箱。

### Lambda、闭包与委托

Lambda 可以转换为委托或表达式树。捕获外部局部变量会形成捕获状态，其生命周期可能超过原作用域。静态审查应区分捕获和不捕获、创建一次和循环内创建、委托缓存和每次转换。缓存是具体编译器与代码生成事实，不得仅凭源码语法跨 Unity/C# 版本承诺。

### foreach 与枚举器

`foreach` 使用集合的枚举模式。是否产生托管分配取决于编译期集合类型、`GetEnumerator` 结果及是否经接口调用；数组或具有 struct enumerator 的具体泛型集合不能因为出现 `foreach` 就自动判定分配。反之，把值类型枚举器转成非泛型或接口枚举器可能引入装箱，必须检查实际类型流。

### yield

包含 `yield` 的方法是迭代器；编译器生成保持枚举状态的实现。它适合表达惰性序列，但在热路径中必须把迭代器对象的创建位置和枚举次数纳入审查，不能只检查 `MoveNext` 循环体。

### async/await

异步方法由编译器生成状态机。是否产生托管分配与返回类型、同步完成、实际挂起、continuation 和 builder 行为有关。`async` 关键字本身不是足够证据；“状态机是 struct”也不是零分配证明。必须用生成代码/IL缩小风险，再以目标运行证据签收。

## Unity 热路径静态规则

- P0 已禁止热路径中的 LINQ、反射、捕获委托、装箱接口枚举、迭代器状态机、临时数组/集合及字符串构造；这是项目设计约束，不等于任意出现这些形态都已实测分配。
- Unity 官方 GC 最佳实践建议降低临时分配、复用集合和对象池，并指出闭包和装箱等来源；这些是风险识别依据，不是 ES 当前代码的运行证据。
- 调用方包装与被调 API 必须一起审查。无分配容器查询外层若新建 key、闭包、日志字符串或结果集合，整条业务链仍可能分配。
- 热路径缺失核心依赖应在初始化期暴露，不能用每帧查找、日志或异常兜底制造额外成本。

## AI 常见失败模式

1. 关键字审查：看到 `foreach`、lambda、`async` 或泛型就直接判定分配。
2. 局部审查：只看目标方法体，漏掉调用点的 `params` 数组、key 构造、日志格式化和结果物化。
3. 编译器神话：把某个 .NET 编译器版本的缓存优化当成 Unity 2022.3 所有后端的稳定合同。
4. IL 越权：看到生成类或 `newobj` 就写具体帧分配字节，忽略路径是否执行及运行时优化。
5. 运行越权：没有 Profiler artifact 却写“0 GC”或“每帧 X B”。
6. 为零分配牺牲正确性：使用共享可变静态缓存，破坏并发、重入或所有权隔离。

## 审查输出模板

每个发现至少记录：

| 字段 | 内容 |
|---|---|
| Location | 文件、成员、调用点 |
| Path phase | 首次/预热/稳态/突破/异常 |
| Frequency and scale | 每帧、每实体、每批次及规模 |
| Allocation mechanism | 对象、数组、boxing、closure、delegate、iterator、async、字符串、扩容等 |
| Confidence | A/B/C/D |
| Conditions | 编译期类型、捕获、重载、容量、同步完成等 |
| Evidence | 源码、生成代码/IL、Profiler artifact |
| Remediation | 移出热路径、缓存、复用缓冲、改 API 或保留并预算 |
| Residual risk | 调用方、平台、后端或未覆盖分支 |

## 验收清单

1. 是否给出了具体调用点，而不是只列危险 API？
2. 是否区分首次与稳态、热路径与冷路径？
3. 是否检查编译期类型、实际重载和泛型约束？
4. 是否区分捕获 lambda、不捕获 lambda、方法组和缓存委托？
5. 是否检查枚举器形态、接口转换和 boxing？
6. 是否检查 `yield` 调用点、`async` 挂起条件与结果类型？
7. 是否覆盖调用方字符串、日志、`params`、结果集合和扩容？
8. 是否把静态结论限制在 A/B/C，未冒充 D 级运行证据？
9. 修复是否保持正确性、可重入、并发和所有权边界？
10. 结论是否绑定 Unity/C# 版本与 stale 条件？

## RequiredReads

- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_P0_热路径容器预热与稳态GC边界_AI协作警告.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_核心热路径缺失依赖不判空_AI协作警告.md`
- `.agents/skills/es-performance-budgeting/references/performance-budget-contract.md`

## SourceRefs

本地来源：

- `ProjectSettings/ProjectVersion.txt` (`a1141b79efc22ad583133c02da76d77a863533680c77dcd2178d1b8413645a08`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_P0_热路径容器预热与稳态GC边界_AI协作警告.md` (`2f5cbca2bf00645da654a88262a228e60999e0a7af44cc35d7a8a7b8267f7665`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_核心热路径缺失依赖不判空_AI协作警告.md` (`02ba0a6d00e9a15ac0d6d4aec2c4689d5652ad284f169742b8c904e1c552440b`)
- `.agents/skills/es-performance-budgeting/references/performance-budget-contract.md` (`d285bec3cfd0d86000bb828353c70b5b8bd26e498437f77dba9dd2568618ed6e`)

## ExternalEvidenceRefs

官方页面于 2026-08-23 读取，HTTP 200。响应 SHA-256 不参与项目相对 SourceRef 的 `ContentHash`，页面变化后必须重新读取：

- Microsoft Boxing and Unboxing: `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/types/boxing-and-unboxing`; SHA-256 `fccbd59fb4637ff3d021ebd557f2341d086a244f7e209775e4134a318b21e2dd`
- Microsoft Lambda expressions: `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/lambda-expressions`; SHA-256 `fd5625154115b1c5072f7bc99b7e6b12a2d80f954cd05c53881c34f7960a3211`
- Microsoft Iteration statements: `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/iteration-statements`; SHA-256 `1c52f03f43f06a23ac45017282f83f5dac17ea0a505f88c572fa467863e9b7ef`
- Microsoft `yield`: `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/yield`; SHA-256 `adaa2cbaa4909b8540ca25ddcc257d488829cbc30e6b7cac6c529501891aa380`
- Microsoft async scenarios: `https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-scenarios`; SHA-256 `6e9c0ef79e3c6f76a3eb6cf99f3ccb84ab5734aae6bfcf309c47905cd8ae6a96`
- Unity 2022.3 Garbage collection best practices: `https://docs.unity3d.com/2022.3/Documentation/Manual/performance-garbage-collection-best-practices.html`; SHA-256 `152d29e7dfe38044f672b6cb34167dc96dc36fb3c64590cd721d446f7af3d655`

## Evidence Boundary

- 当前证据是源码/规则/官方文档静态审查，`runtime-not-run`。
- 未运行 Unity、Profiler、Player、IL2CPP、测试或反编译器。
- 外部页面哈希证明本次取得的响应，不证明页面永远不变，也不代表 ES 当前代码已满足建议。
- 共享索引和 AIBRAIN 功能区已登记；该登记只提供静态发现，不授予执行权限，也不提升 Runtime 证据等级。

## StaleWhen

以下任一变化使本条目 stale：Unity 或 C# 编译器版本变化；Microsoft/Unity 官方页面响应哈希变化；两份 P0 或性能预算合同变化；项目引入新的编译/代码生成后端；静态分析器规则变化；共享索引中的 KnowledgeId、RouteKeys、RequiredReads 或 ContentHash 与正文不一致；出现新的目标平台 Profiler 证据改变现有边界。
