# AIKnowledge 路由与质量门禁

状态：已注册的现行 Knowledge 质量治理 canonical 条目。

`KnowledgeId`: `es.knowledge.routing-quality.v1`
`Authority`: `Derived from current project contracts and routing source`
`EvidenceLevel`: `S1`
`RouteKeys`: `knowledge`, `knowledge-quality`, `knowledge-output`, `source-ref`, `content-hash`, `stale`, `canonical-entry`, `dedup`, `route-probe`, `misroute`, `bounded-output`, `evidence-boundary`, `permission-boundary`
`ContentHash`: `f0f605a86d0bbf37374dad191b2ce3669d0988aae3f5f34e319461d1692a878a`
`StaleWhen`: 任一 SourceRef 哈希、Knowledge 条目合同、AIBrain 选择算法、用户直接授权策略、AICommand 受管协议或验证器判定规则变化。

`RequiredReads`:

- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`
- `Documentation/AIKnowledge/KnowledgeIndex.yaml`
- `Documentation/AIKnowledge/entries/knowledge-routing-quality.md`
- `Documentation/AIKnowledge/RouteProbeRegistry.json`
- `ES/Automation/Contracts/es-knowledge-route-probe-registry.schema.json`
- `Documentation/AIKnowledge/tools/Test-ESKnowledgeRouteProbeRegistry.ps1`
- 命中任务对应的 AIWarnings、AICommand、Skill、源码、测试和真实证据

## SourceRefs

- `AGENTS.md` (`10de16335dc5eacbc13e943bd61b2c5cde770a1358cfc07612d697fe77f09ced`)
- `.agents/skills/es-knowledge-creator/SKILL.md` (`bb2d2869573f9468db36afa74b8d86ee928987ae0e297dc46b858f71f8876ad7`)
- `.agents/skills/es-ai-knowledge-curation/SKILL.md` (`dd6ddf596cd040345312ed4843a6d8403642f1712e874d12eef5ce39ae2510a1`)
- `.agents/skills/es-knowledge-validator/SKILL.md` (`6183ac59608a55c03a46bd0a3575e699116fb6e7910ac4f1ad23431da5f6a61e`)
- `.agents/skills/es-aibrain-route-authoring/SKILL.md` (`00688ba48c5485db39a5103d2b813af7c6409eca1f7a85c726aa37ba0252c637`)
- `.agents/skills/es-skill-governance/references/user-directed-low-risk-policy.json` (`bb242508ba5c0b08c046e1460d4e53ef2a216c428b146f954e1a5150ba8ba2b3`)
- `Assets/Plugins/ES/AICommands/受管AIKnowledge更新_AI命令.md` (`9abb93f4bedd67ea1d2560655efddc4a2eb16ad6110398b24d98fe008320e1d7`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs` (`edc33e609c4fefd2dbaf832043dade36e7ca07beedab1db09703947fa7cb9a19`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationCenter.cs` (`a636a42521eb8f13462455b726c7e06fe3211cd733e5c280092af0a45673e485`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs` (`20b63b3db889b705ae740d366fa234b8ae49b50a60bf72056cd2a96b86db9b57`)
- `Assets/Plugins/ES/1_Design/Tests/ESAIBrainKnowledgeRoutingTests.cs` (`a3440dc4f6c042cf0d54c1934a9119be7a6dfe90e72f376a73f8bccafe62f3af`)
- `Documentation/AIKnowledge/RouteProbeRegistry.json` (`d5e828c736e236ca12552fefb750509b3db4de48a4a6c1a9835b461ee24d017d`)
- `ES/Automation/Contracts/es-knowledge-route-probe-registry.schema.json` (`bd73b87a43e5b9a01d35e04cffebd8225bb1146aaf39553f670c6876a1c0d7ff`)
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md` (`72425a0e2703081f46d7f15c963f79ae24ebf2152ba1e3b61d2dbe3fb96fc6b4`)
- `Documentation/AIKnowledge/tools/Test-ESKnowledgeRouteProbeRegistry.ps1` (`b7fa8402056d122bfa60ea9002dd90ba356d2a26e8f9f7e9d6afe6f2cace4a0b`)
- `.agents/skills/es-knowledge-validator/scripts/Export-ESKnowledgeRefreshPlan.ps1` (`691150a3b51bf2f500ab99fa058ea70bb6a643ce5feca606acc625a2a80ec98c`)
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeStableRefresh.ps1` (`6ad678e350af07aa2e0d50cad6ae9a6f532b3f6ab2e71a7943f1bd84e00f29c5`)
- `.agents/tests/Test-ESKnowledgeStableRefresh.ps1` (`cffffd5c763d5860e2ac707e4b450ee455c05976821774985f3128973075488e`)

## Scope

本条目负责 AIKnowledge 的发现质量、最小路由、SourceRef/ContentHash 新鲜度、canonical 所有权、重复消除、路由探针、受限输出和证据边界。它不拥有项目领域事实，也不授予 KnowledgeIndex、AIBRAIN_ENTRY、Assets、源码、Unity、Git、发布、删除或外部操作权限。

当前路由实现固定使用 `per-route-best-top3-v1`：每个 routeKey 先保留最高交集候选，再按交集数量、命中比例和 KnowledgeId 稳定排序，单个计划最多注入 3 条 Knowledge。`Documentation/AIKnowledge/RouteProbeRegistry.json` 是路由探针唯一数据集；CLI 验证器和 Unity 测试消费者共同覆盖正向、禁止命中、混合意图、显式 routeKey、零命中和重复确定性。AI Bridge 暴露只读 `runKnowledgeRouteProbes`，生产力面也报告 route-probe 的 `static-routing-only` 证据边界。

当前 AIBrain/Bridge 会对 Skill Resource Index、Skill Catalog、Discovery Policy、KnowledgeIndex、AIBRAIN_ENTRY 和 AICommand Catalog 计算能力元数据指纹，并在 `queue-update`、`session-resume` 或轮询发现变化时发出有界 CapabilityDriftSignal。该信号只提示能力刷新，不自动加载全库、授予权限或替代重新 `planTask`；来源或索引漂移时相关 Knowledge 与依赖计划必须标为 stale。失败遥测以容量 256、最近 32 条和 detail SHA-256 的脱敏快照保留 `WrongKnowledgeRoute`、`RequiredReadOverflow`、`ClaimDowngraded` 等分类。

职责边界：

- `es.aibrain.orchestration.v1` 拥有 AIBrain 的计划、授权和执行控制流。
- `es.function-area-routing.v1` 是共享功能区投影，只负责把领域 routeKeys 导向 canonical 条目。
- `es.skill.resource-index.v1` 拥有 Skill 资源组合、Catalog、治理元数据和证据资源导航。
- 本条目拥有 Knowledge 质量判断、误路由恢复、canonical 去重、stale 和 bounded-output 决策。

## Trigger And Routing

自然语言触发包括：“建立/更新知识库”“校验 SourceRef 或 ContentHash”“知识过期或 stale”“canonical 去重”“路由探针”“误命中/零命中”“限制 Knowledge 输出”“静态证据被夸大”。

预期行为：

1. 任务同时命中 `knowledge` 与任一质量专用 routeKey 时，本条目应进入前 1～3 个结果。
2. 纯领域任务不应只因含有通用词 `knowledge`、`evidence` 或 `validation` 而选择本条目。
3. 相邻路由最多补充一个领域条目或一个基础设施条目；不得用 Scene Builder、Unity 生命周期、IL2CPP 或性能条目替代本条目。
4. 零命中时回读 AIWarnings Start 链与当前权威来源，报告覆盖缺口；不得选择“最像”的条目继续。
5. 过宽或错误命中时停止使用当前 route-pack，缩小 routeKeys、修复索引投影并重新规划。

## Decision Rules

### 可以继续

- 命中 1～3 个条目，且本任务的 canonical 条目明确。
- requiredReads、条目正文和 SourceRefs 均可读取。
- 所有 SourceRef SHA-256 与声明一致，ContentHash 可按排序后的来源哈希重算。
- Authority/EvidenceLevel 足以支持准备输出的事实，且权限与真实证据没有被 Knowledge 扩大。

### 必须先补读

- 涉及项目事实：补读当前源码、配置、测试或真实验证回执。
- 涉及禁止事项或风险：补读命中的 AIWarnings P0/领域规则。
- 涉及写入：确认当前用户请求与目标范围；涉及外部副作用时确认用户已单独点名。只有选用受管通道时才补读唯一匹配 AICommand 和 TaskContract。
- 涉及 Unity、PlayMode、Profiler、Player、IL2CPP 或发布：补读对应运行证据；只有静态来源时保留 `runtime-not-run`。

### 必须停止

- SourceRef 缺失、越界、哈希漂移或 ContentHash 不一致。
- KnowledgeIndex 绑定缺失、重复、路径不一致或 routeKeys 与正文不相交。
- 结果为零命中、明显误命中，或为了“建立上下文”需要递归加载全部条目。
- 输出将把摘要、文件存在、测试源码存在或静态检查描述成真实运行成功。
- 当前写入路径或操作不在用户明确目标及其严格必要范围内，或动作存在实质歧义。

### 状态裁决

- `stale`：来源、索引、路由算法或绑定哈希变化；丢弃旧计划并重新读取。
- `Deferred`：设计方向明确但缺少实现或运行证据；保留缺口，不升级事实。
- `Blocked`：路径、权限、结构、哈希、路由或证据边界不闭合；禁止继续写入或宣传完成。
- `runtime-not-run`：静态闭包不等于运行失败，但不能支持任何运行或发布结论。

`PlanTaskUnavailable` 是 AIBrain 受管通道能力缺口，不等于 `NoMatchingCommand`，也不撤销 current-user-direct 授权；不得借此扩大用户范围。

## Verified Facts

- 项目根规则要求项目任务先经过 AIBRAIN_ENTRY 和 KnowledgeIndex 最小发现链；这是当前项目指令事实。[SourceRef: `AGENTS.md`]
- AIBrain 当前先保留每个 routeKey 的最高交集候选，再按交集数、命中比例和 KnowledgeId 顺序选择最多 3 条；行内与多行 requiredReads 都进入计划绑定。[SourceRef: `ESAIBrainCoordinator.cs`]
- 机器可读探针注册表固定自然语言目标、显式恢复键、预期 routeKeys、精确 Top-3、禁止命中和逐条 requiredReads；NUnit 对每个探针重复规划并拒绝未知 rankingVersion。[SourceRef: `RouteProbeRegistry.json`, `ESAIBrainKnowledgeRoutingTests.cs`]
- AI Bridge 注册 `runKnowledgeRouteProbes` 与 `getFailureTelemetry` 两个只读动作；前者用当前 AIBrain planner 重放注册表，后者返回容量 256、最近 32 条且只保留 detail SHA-256 的内存遥测。[SourceRef: `ESAIBrainCoordinator.cs`, `ESAutomationAiBridge.cs`]
- 计划失败分类区分 `PlanTaskUnavailable`、`NoMatchingCommand`、`NoKnowledgeRoute` 与 `SourceHashDrift`；已声明 accepted 但被证据或运行边界降级时记录 `ClaimDowngraded`。[SourceRef: `ESAIBrainCoordinator.cs`, `ESAutomationCenter.cs`]
- Stable SourceRef refresh 会验证 planHash 与 apply-time 来源哈希，先准备完整批次，再提交条目和 KnowledgeIndex；提交异常回滚已落盘文件，来源漂移时整批拒绝。[SourceRef: `Export-ESKnowledgeRefreshPlan.ps1`, `Invoke-ESKnowledgeStableRefresh.ps1`, `Test-ESKnowledgeStableRefresh.ps1`]
- 详细条目必须绑定真实 SourceRefs、可重算 ContentHash、Authority、EvidenceLevel 和 StaleWhen；Knowledge 不能覆盖当前源码或真实证据。[SourceRef: `es-knowledge-creator`]
- 全量/单条验证器只证明文本、路径、哈希、索引与路由静态闭包，不证明 Unity 或发布行为。[SourceRef: `es-knowledge-validator`]
- 受管 Knowledge 更新命令限制正式条目和索引写入范围，禁止修改 Assets 来适配摘要或伪造证据。[SourceRef: `受管AIKnowledge更新_AI命令.md`]

以上均为源码或合同静态事实；没有声明 Unity、外部进程或发布已经验收。

## Common AI Failure Modes

| 错误行为 | 典型症状与根因 | 预防检查 | 正确动作与恢复 | 缺失证据 |
|---|---|---|---|---|
| 跳过发现链 | 直接搜索源码或凭记忆回答 | 确认已读 AIBRAIN_ENTRY、索引和 1～3 个 requiredReads | 回到入口重新路由 | 当前来源哈希 |
| 通用键导致误路由 | Knowledge 任务命中 Scene Builder/Unity 条目 | 探针必须包含质量专用 routeKeys，并检查前三名 | 停止旧 route-pack，修复专用绑定后重放 | 修复后的探针结果 |
| 一次加载过多条目 | 上下文被通用摘要占满 | 结果数必须为 1～3，且每条都有明确作用 | 只保留 canonical、一个领域补充和必要基础设施 | 无 |
| 把摘要当事实 | 使用 Knowledge 替代源码/P0 | 为每条输出标注事实来源与 Authority | 回读高权威来源，冲突时标记 stale | 当前源码/证据 |
| 忽略哈希漂移 | 旧计划在 SourceRef 变化后继续 | 重算每个 SHA-256 和 ContentHash | 丢弃旧计划，重新读取和规划 | 新哈希快照 |
| 静态冒充运行 | “测试文件存在”被写成 PlayMode 已通过 | 分离 staticStatus 与 runtimeStatus | 改为 `runtime-not-run`，列出所需真实运行证据 | Unity/Player 回执 |
| 重复 canonical 事实 | 多条复制同一规则并分别漂移 | 建立一事实一 owner 表 | canonical 保留规则，其他条目只保留适用差异和链接 | 去重后的索引绑定 |
| 权限扩张 | AI 将用户目标外的索引/Assets 加入计划 | 比较声明用户范围与实际目标；受管通道再检查 AICommand/TaskContract | 移除推断扩张或向用户澄清；不要索取重复批准 | 用户范围记录；受管时再附 PlanHash/回执 |
| 忘记失败恢复 | 只写 happy path | 检查非法输入、拒绝扩权、重复执行和中断恢复 | 补齐恢复动作后再接受条目 | 回放结果 |
| 临时结果长期化 | 缓存、扫描或旧快照写成长期事实 | 检查 Authority、时间和 StaleWhen | 改为 Deferred/临时证据并绑定失效条件 | 新鲜权威来源 |

## Canonical Ownership And Deduplication

| canonicalEntry | 保留内容 | 其他条目只保留 | 不可合并理由 |
|---|---|---|---|
| `es.knowledge.routing-quality.v1` | Knowledge 质量、探针、去重、stale、bounded-output、证据与权限防错 | 适用条件和交叉路由 | 这是 Knowledge 产品质量，不是 AIBrain 执行控制流 |
| `es.aibrain.orchestration.v1` | planTask、PlanHash、TaskContract、Facade 和运行编排 | 指向本条目的质量门禁链接 | 编排权限属于 AIBrain，不属于 Knowledge 摘要 |
| `es.function-area-routing.v1` | 共享功能区 routeKey 投影 | 不复制领域决策规则 | 投影可多绑定，事实 owner 必须唯一 |
| `es.skill.resource-index.v1` | Skill 资源、Catalog、治理与证据组合 | 不复制 Knowledge 质量矩阵 | Skill 资源生命周期与 Knowledge 内容质量是不同对象 |

## Route Probe Acceptance

`Documentation/AIKnowledge/RouteProbeRegistry.json` 是探针事实的唯一数据集，本条目是其 canonical owner；其他条目不再复制探针表。注册表以 `operational-static` 注册 CLI 与 Unity 两个消费者，覆盖正向领域路由、Graph/TaskContract 碰撞、禁止宽泛命中、混合意图、零命中、显式恢复与确定性重复，Schema 固定 `per-route-best-top3-v1` 排名版本。

接受条件：CLI 返回 `static-passed`；Schema 与注册表结构有效；consumer 路径可读；生产力面注册 `diagnostic.knowledge-route-probes`；AI Bridge 可调用 `runKnowledgeRouteProbes`；`probeId` 唯一；每个非零探针精确匹配 routeKeys、Top-3 顺序和逐条 requiredReads；禁止项不命中；零命中保持阻断；重复规划顺序不漂移。任何 rankingVersion 变化必须先升级注册表与消费者，不能静默沿用旧预期。Unity 消费者未运行时必须保留 `objectiveInferenceStatus=registered-unity-consumer-not-run` 和 `runtime-not-run`。

## Execution Checklist

开始前：

- 读取项目入口、索引和最小 route-pack。
- 声明本批功能域、目标文件、最多 1～3 个条目、停止条件和回滚方式。
- 记录当前用户目标、计划路径和单独点名的副作用动作。
- 连续采样所有 SourceRefs，确认没有并发漂移。

实施中：

- 每条事实绑定权威来源，不复制大段源码、AIWarnings 或其他 Knowledge。
- 保持 canonical owner 唯一；共享投影只保存路由和差异。
- 更新来源后立即重算 ContentHash，并使旧计划 stale。
- 发现路径、权限、冲突或哈希异常时停止，不自动扩大范围。

完成后：

- 运行单条和全量 Knowledge 验证。
- 运行 `Documentation/AIKnowledge/tools/Test-ESKnowledgeRouteProbeRegistry.ps1` 并要求零 finding。
- 运行 `.agents/tests/Test-ESKnowledgeStableRefresh.ps1`，确认整批提交、漂移拒绝、planHash 拒绝与幂等恢复。
- 重放 10 个路由探针和至少一个无匹配/拒绝扩权用例。
- 检查严格 UTF-8、差异完整性、重复 KnowledgeId、requiredReads 和 relatedSkills。
- 分开报告 staticStatus、runtimeStatus、未证明项和剩余 stale 来源。

禁止事项：递归加载全部条目；修改源码来适配 Knowledge；删除冲突条目；伪造哈希或运行证据；把 `PlanTaskUnavailable` 当成用户未授权；超出当前用户目标修改索引/Assets，或从普通写入推导 Git、审计、发布及外部动作。

## Evidence Boundary

本条目及其验证器可以证明：文本是严格 UTF-8；SourceRef 存在且哈希一致；ContentHash 可重算；索引、路径、routeKeys、requiredReads 和 relatedSkills 形成静态闭包；路由探针按当前静态算法得到确定结果。

本条目不能证明：Unity 编译或 Domain Reload、EditMode/PlayMode、Profiler 分配、Player/IL2CPP、视觉结果、外部服务、生产发布或商业验收。没有对应真实回执时，以上状态统一为 `runtime-not-run`。
