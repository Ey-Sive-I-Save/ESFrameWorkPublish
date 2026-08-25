# AIBrain 统一发现入口

> 所有 AI 组件的统一导航页。源码入口：`Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs`；对外能力入口：`ESAutomationAiBridge` 的 `listCapabilities`、`planTask`、`runTask`。

## AI 最小启动协议

凡任务涉及 ESFramework 项目事实，AI 必须执行以下发现链：

```text
项目根 AGENTS.md
  -> 本文件
  -> AIBrain 从目标和显式 routeKeys 推导最小路由集合
  -> KnowledgeIndex.yaml 按 routeKeys 选择 1～3 个最相关条目
  -> 条目的 requiredReads 与正文
  -> 命中的 AIWarnings / AICommand / Skill
  -> 当前源码、配置、工作树和所需验证证据
```

执行规则：

1. 先用任务中的对象、动作和风险匹配 `KnowledgeIndex.yaml` 的 `routeKeys`，不要按文件名猜测；调用方可以提供 routeKeys，但不要求知道内部 Skill 名称。
2. 同时命中多个功能区时，先读共同上游，再按实际修改范围补充条目；禁止默认加载全部 `entries/`。
3. `Authority` 和 `EvidenceLevel` 决定条目能支持什么结论；S1 源码阅读不能冒充 Unity、PlayMode、Profiler、Player 或发布验收。
4. `SourceRefs` 是回到事实源的指针。来源缺失、SHA-256 漂移或相互矛盾时，旧条目和旧 AIBrain 计划立即 stale。
5. 对多文件一致性、重复读取、快照、源文件哈希、Parser/Projection、二进制解析或缓存漂移等任务，AIBrain 自动追加 `consistency` 基础路由；单文件一次性读取不自动附加该能力。
6. 没有匹配条目时，读取 AIWarnings Start 链、RuleIndex 与当前源码，并把“缺少 Knowledge 路由”作为待补知识，而不是自行发明项目约定。
7. 只读过本页不等于读过知识库；只有完成路由选择和 `requiredReads` 后，才能声称已加载本任务的项目知识。

## Two-phase execution and capability drift

`planTask` returns an immutable `planHash`. External `runTask` must submit it as `approvedPlanHash`; the coordinator compares the current bindings and rejects stale plans, `NeedsReview` Skills, or Skills without `authorized-only` runtime eligibility before issuing one-time authorization.

The ES host emits bounded capability-drift signals for queue updates and session resume, and polls Catalog, governance, Knowledge-route, and command metadata hashes. A signal contains only a trigger, generation, and metadata fingerprint. It never loads the full Skill portfolio, grants permission, or substitutes for route-scoped comparison and re-planning.

## AIKnowledge 权威质量门禁

本文件是 AIKnowledge 的**发现与使用权威**，不是源码、AIWarnings、AICommand、Skill 或 Unity 运行证据的替代品。任何把 Knowledge 用于项目事实、设计或实现的任务，都必须先通过以下一次性门禁；门禁未通过时只能报告缺口，不能继续基于猜测输出。

### 1. 来源和权威裁决

按以下顺序回读事实，较低层只能导航或补充，不能覆盖较高层：

```text
当前源码、配置、测试与真实验证回执
  > Unity 官方文档 / UnityCsReference / 已安装包源码（必须注明当前版本）
  > AIWarnings P0 与领域规则
  > AICommand、TaskContract 与 Skill 合同
  > AIBrain 路由记录
  > AIKnowledge 条目与索引
  > 缓存、搜索摘要或模型记忆
```

Unity、运行时、Profiler、Player、IL2CPP、视觉、性能或发布结论，必须绑定对应的真实证据；静态条目只能说明“源码/文档如此”，不能把 `S1`/`S2` 推断成运行通过。

### 2. 路由、读取和输出门禁

1. 用任务的对象、动作、风险和版本匹配 `KnowledgeIndex.yaml`，最多选择 1～3 个最小条目；禁止为建立上下文递归读取全部 `entries/`。
2. 先读取命中条目的 `requiredReads`、正文和 `SourceRefs`，再读取对应 AIWarnings、AICommand、Skill、源码和测试。路径越界、缺失或无法读取即 `blocked`。
3. 每条输出必须能转化为 AI 可执行检查：触发条件、前置读取、允许动作、禁止动作、失败处理/恢复、完成验证和明确 `non-claims`；只有背景摘要而没有决策或检查规则的内容不得作为正式 Knowledge。
4. 发现多个条目描述同一事实时，只保留一个 canonical 条目；其他条目只能通过共享路由投影引用它，不得复制另一份会漂移的摘要。新增条目前先做 route 探针和重复事实检查。
5. 没有匹配路由时，回到 AIWarnings Start、CurrentStatus、RuleIndex 与当前权威来源，记录 Knowledge 覆盖缺口；不得用相似条目或缓存替代。

### 3. 新鲜度和证据门禁

正式条目必须同时具备 `KnowledgeId`、`Authority`、`RouteKeys`、`RequiredReads`、`SourceRefs`、`ContentHash`、`EvidenceLevel` 和 `StaleWhen`；有验证或发布事实时还必须具备 `EvidenceRefs`。任一 SourceRef 缺失、SHA-256 漂移、索引绑定不一致、`StaleWhen` 触发或验证器返回 `blocked`，条目及依赖它的旧计划立即标记 `stale`，先回读并重新计算哈希。

静态验证的通过范围只覆盖文本、路径、路由、哈希和合同闭包；`runtime-not-run` 是未取得运行证据的明确状态，不能写成“已验证”“可发布”或“性能达标”。

### 4. 一次性接受回执

使用 Knowledge 前应保留本次门禁的最小回执：`selectedKnowledgeIds`、实际 `requiredReads`、来源哈希快照、`authorityDecision`、`evidenceLevel`、`staleCheck`、`nonClaims` 和验证器结果。回执只证明本次读取和静态判断，不授予源码、Unity、Git、发布或删除权限；来源变化后必须重新接受。

当用户说“你的理解已经过时”“刷新一下技能理解”“重新理解当前项目提供的 Skill”，或表达等价含义时，AIBrain 必须将其识别为 `understanding-drift` / `skill-understanding-refresh` 意图，自动路由到增量刷新能力；用户不需要知道或点名具体 Skill。该意图只触发哈希比较和当前任务路由筛选，不授权全量读取 Skill 正文。

## 组件如何找到 AIBrain

| 组件 | 固定指针 |
|---|---|
| AIWarnings | `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` |
| AICommands | `Assets/Plugins/ES/AICommands/README.md` |
| Project Skills | `.agents/README.md` 与各 Skill 的 `agents/openai.yaml` |
| AIKnowledge | 本文件与 `KnowledgeIndex.yaml` |
| Automation / MCP | `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs` |

## AIBrain 如何找到功能区

调用 `listCapabilities` 发现完整生产力面；调用 `planTask` 时 `routeKeys` 可选：AIBrain 优先从 objective 的对象、动作和风险推导基础路由，显式 `routeKeys` 只用于补充或锁定业务领域。

| 功能区 | RouteKeys | 首选 Skills |
|---|---|---|
| 治理、规划与 Skill 执行成本 | `aibrain, governance, planning, authority, skill-performance, execution-cost, fast-path, deep-path, cache` | `es-skill-governance`, `es-use-ai-command` |
| 分析、审查、迁移与变更风险 | `analysis, design, root-cause, review, risk, change-budget, rollback, migration, compatibility` | `es-first-principles-analysis`, `es-adversarial-review`, `es-change-risk-register`, `es-migration-planning` |
| AIBrain 编排与 Automation 任务路由 | `orchestration, task-routing, automation, task-contract, worker, automation-run-record, agent-execution-graph, aicommand, mcp` | `es-aibrain-route-authoring`, `es-use-ai-command`, `es-automation-worker-authoring`, `es-aicommand-contract-authoring` |
| Skill 验证与质量门禁 | `skill, validation, security, catalog, evidence, evidence-pending, portfolio, static-replay, deep-replay, deterministic, static-boundary, external-side-effect, blocking-layer` | `es-skill-validator`, `es-skill-creator`, `es-static-deep-replay` |
| 商业一致性与交付证据 | `commercial-coherence, delivery-tracking, evidence-receipt, report-hash, source-freshness, plan-hash, static-review, runtime-not-run` | `es-skill-governance`, `es-knowledge-validator`, `es-release-acceptance` |
| 工作树与编码 | `worktree, utf8, validation` | `es-worktree-audit`, `es-utf8-guard` |
| Unity 编译、MonoBehaviour 生命周期与验收 | `unity, compile, monobehaviour, lifecycle, static-state, domain-reload, scene-reload, enter-play-mode, script-execution-order, execute-always, player, il2cpp, aot, test, release, evidence` | `es-unity-compile`, `es-release-acceptance`, `es-editor-availability-validator` |
| 游戏 UI 自动化装配 | `ui-automation, screen-spec-v3, ui-prefab, ui-fixture-scene, ui-layout, responsive, visual-qa, asset-fallback` | `es-ui-prefab-authoring`, `es-unity-compile` |
| 测试场景构建与备份权威 | `scene-builder, prefab-override, scene-fixture, scene-layout, scene-backup, backup-manifest` | `es-test-fixture-authoring`, `es-editor-tooling` |
| 测试场景验收与发布证据 | `scene-validation, scene-guide, acceptance, release, evidence, receipt, profiler, unity` | `es-release-acceptance`, `es-observability-evidence`, `es-worktree-audit` |
| GameCore 与稳定身份 | `gamecore, config, identity, config-key, runtime-key, catalog, root-so, runtime-data, content-registration` | `es-gamecore-integration`, `es-gamecore-config-authoring`, `es-tag-config` |
| 资源与发布链 | `resource, asset, manifest, provider, resource-plan, owner-scope, temporary-scope, lease, provider-transition` | `es-resource-pipeline`, `es-resource-publish-audit` |
| Entity、输入与命令 | `entity, input, input-action, runtime-mode, control, command, runner, runner-tick, lifecycle` | `es-entity-authoring`, `es-entity-prefab-validation`, `es-input-action`, `es-command-authoring` |
| 运行时生命周期、Pool 与仲裁 | `runtime, lifecycle, generic-life, pool, operation, lease, request, arbitration, commit, executor` | `es-entity-authoring`, `es-performance-budgeting`, `es-test-fixture-authoring` |
| 热路径容器与性能证据 | `performance, runtime-hot-container, container-warmup, steady-state-gc, capacity-growth, pool, prewarm, profiler, run-record, zero-gc` | `es-performance-budgeting`, `es-observability-evidence`, `es-ai-knowledge-curation` |
| 编辑器、Graph 与 Agent 产物 | `editor, graph, agent-authoring, editor-window, editor-extension, inspector, drawer, dialog, popup, workbench, layout, responsive, high-dpi, single-axis-scroll, owner-lifecycle, reload-domain, undo-dirty, preview-lifecycle, editor-performance, window-production-standard` | `es-editor-tooling`, `es-editor-availability-validator`, `es-generate-agent-artifacts` |
| Stable Graph V2 作者与烘焙边界 | `graph, stable-graph-v2, graph-identity, graph-undo, graph-migration, edge-order, graph-snapshot, graph-bake, legacy-graph` | `es-stable-graph-authoring`, `es-editor-tooling` |
| 编辑器正式资产与序列化事务 | `editor, asset-authoring, asset-database, prefab, prefab-override, serialized-object, serialized-property, undo, dirty, save, transaction, scene-builder, backup` | `es-editor-tooling`, `es-api-contract-review`, `es-entity-prefab-validation`, `es-test-fixture-authoring` |
| Unity 序列化、渲染与图集 | `unity, serialization, asset-guid, local-file-id, serialize-reference, rendering, shader, material, shader-keyword, shader-variant, material-variant, sprite-atlas, ui-canvas, canvas-sorting, ui-batching, draw-call, frame-debugger, srp-batcher, material-property-block, mask, stencil, batch-break` | `es-editor-tooling`, `es-api-contract-review`, `es-performance-budgeting`, `es-observability-evidence`, `es-unity-compile`, `es-release-acceptance` |
| Fixture 与视觉证据 | `fixture, test-fixture, deterministic, editmode, playmode, screenshot, resolution, visual-qa, visual-evidence, gpu-capture` | `es-test-fixture-authoring`, `es-observability-evidence`, `es-release-acceptance` |
| 模块审计与会话 | `audit, lifecycle, session, handover` | `es-module-lifecycle`, `es-codex-session-bootstrap` |
| 飞书外部协作适配器 | `feishu, lark, external-adapter, dry-run, task-monitor, task-dispatch, task-transition, virtual-team, identity-claim, bot-ownership, onboarding, message-send, notification` | `es-feishu-cli`, `es-use-ai-command`, `es-automation-worker-authoring` |
| Knowledge 输出、验证与条目治理 | `knowledge, knowledge-quality, knowledge-output, validation, source-ref, content-hash, hash, routing, route-probe, misroute, canonical-entry, dedup, evidence, evidence-boundary, permission-boundary, bounded-output, stale` | `es-knowledge-creator`, `es-knowledge-validator`, `es-ai-knowledge-curation`, `es-aibrain-route-authoring` |
| 任务读取一致性与解析投影基础设施 | `task, read, snapshot, consistency, hash, stale` | `es-task-read-snapshot` |
| 长运行 AI 会话的 Skill 增量发现与能力刷新 | `skill, session, refresh, capability, delta, stale, routing` | `es-skill-session-refresh`, `es-task-read-snapshot` |
| Skill 理解刷新与增量能力发现 | `understanding-drift, skill-understanding-refresh, capability-refresh, incremental-discovery` | `es-skill-session-refresh`, `es-task-read-snapshot` |
| AIWarnings 领域治理 | `aiwarnings, p0, architecture, runtime, editor, validation, handover, archive` | `es-aiwarning-authoring`, `es-aibrain-route-authoring`, `es-ai-knowledge-curation` |

路由歧义收口：`skill-performance`、`execution-cost`、`fast-path`、`deep-path` 和 `cache` 只在任务明确讨论 **Skill 调用、启动或执行流程** 时归入治理功能区。运行时缓存、GC、帧预算或 Unity 性能问题仍按运行时性能和 `$es-performance-budgeting` 路由；AIBrain 不得只因出现 `cache` 一词就改变领域。`consistency` 只表示文件读取基础设施，必须同时出现文件、读取、解析、哈希、清单或投影语义；Graph Bake Snapshot、Story Definition Snapshot 等领域快照不得仅因 `Snapshot` 一词误命中任务读取基础设施。

目录是导航投影，不授予权限；最终权限仍由 AICommand、TaskContract 和用户授权决定。

Skill 发现资格还必须读取 `.agents/SKILL_DISCOVERY_POLICY.json`：`candidate` 只能作为能力候选展示，`operational-candidate` 只能进入计划并保持运行证据未证明，只有 `operational` 才能进入正式能力面；任何状态都不能替代 AICommand、TaskContract 或开发者授权。`.agents/SKILL_REGISTRY.manifest.json` 的元数据代际不一致时，AIBrain 必须把相关计划标为 stale 并重新规划。

## 受治理核心 Skills

以下 Skill 使用 AIBrain 可执行校验的独立权威轴。`project-gate` 是跨项目门禁，`core-governed` 是核心治理流程；二者都必须经过 `planTask`、禁止直接执行，并且不扩大写权限。基础工程 Skill 可以由 AIBrain 按任务形态自动附加，但仍必须经过同样的 Skill、AICommand、TaskContract 和证据门禁。

- `project-gate`: `es-skill-governance`, `es-use-ai-command`, `es-feishu-cli`, `es-utf8-guard`, `es-release-acceptance`
- `core-governed`: `es-skill-creator`, `es-worktree-audit`, `es-task-read-snapshot`

它们是高权威门禁/编排能力，不等于拥有源码、Unity、Git 或发布权限。

## 扩展 Skill 族

资源组合索引位于 `.agents/SKILL_RESOURCE_INDEX.yaml`，分类与生命周期快照位于 `.agents/SKILL_CATALOG.yaml`。每个直接 Skill 目录必须有且仅有一条 Catalog 记录；记录包含 family、routeKeys、状态、首次注册、最近修改/复核时间与治理哈希。Catalog 只负责发现和陈列，不授予权限；AIBrain 仍以 AIWarnings、AICommand 和 `governance.json` 为执行门禁。需要脚本或 MCP 时，先读取资源索引、Catalog 和对应合同，再检查连接状态与证据，不把 MCP 可见性当作授权。
