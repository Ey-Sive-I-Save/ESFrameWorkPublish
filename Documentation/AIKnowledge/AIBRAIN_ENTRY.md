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

## GraphView 与 AI 协作内核边界

当前 `ESGraphViewV2` / `AISkill Graph` 是 GraphView 上的作者、烘焙和固定流程执行工具，不是已经验证的真实 AI 协作工作流内核。它可以表达稳定的 AISkill 输入、Task、Skill Call、Branch、ForEach、Approval、FanOut、Join、输出和验证关系，但其编辑效率、动态任务拆解能力、并行调度性能、跨 Agent 上下文隔离与执行准确性尚未完成独立验证。

因此，在多子 Agent 机制研究和方案设计阶段，禁止把 GraphView 的节点存在、FanOut/Join 拓扑或静态 Bake 结果描述为真实并行 Agent 能力，也不得以 GraphView 作为外部 Agent 机制的唯一复刻载体。动态协作应优先沿用 AIBrain、TaskContextRuntime、TaskFocusContext、AutomationCenter 和受管 TaskContract 的现有协作流；GraphView 只作为可选的固定 AISkill 工作流作者/验证/可视化投影。只有在性能、准确性、上下文隔离和运行时证据分别通过后，才可讨论把某类协作流程固化接入 AISkill Graph。

## 子 Agent 并行方案冻结（v1）

本版本冻结的子 Agent 机制是“父子任务投影/适配器”，不是新增的第五套 Agent 生命周期、身份系统或授权内核。动态并行的唯一执行链为：`AIBrain → GoalRevision/RoutePlan → TaskFocusContext → TaskContextRuntime → AutomationCenter/TaskContract → EvidenceSet/Receipt → ParentAggregator → completionDecision`。

冻结的职责边界如下：

- `CollaborationPlan` 只表达父任务的拆解、依赖、并发预算和聚合策略，复用 `GoalRevision` 与 `RoutePlan`，不拥有任务生命周期、权限或执行权。
- `ChildTaskRegistry` 只作为 TaskContext 管理的父子索引，不建立第二个状态机；子任务的状态、CAS、RunId 和幂等语义仍由现有 TaskContext/Automation 权威负责。
- `Lease/CAS` 只用于短期 Worker 认领和过时保护；租约不拥有业务完成权。租约过期、取消后或 CAS 过时的迟到结果必须隔离，不能回写父任务。
- `TaskFocusContext` 只描述本次 Agent 的关注范围、允许读取范围、禁止扩展范围及其版本/回执引用，不拥有 Goal、RoutePlan 或 Knowledge 内容权威，也不复制完整内容。TaskContext 只冻结 `focusContextId`、`focusRevision`、`focusProposalHash`、`focusReceiptHash` 和必要的 `focusScopeHash`。
- `ResultEnvelope` 只提交结构化输出、Evidence 引用、哈希、尝试号和错误状态，不得自报 `Accepted` 或直接把结果写成 `Completed`。
- `ParentAggregator` 只按确定性排序和显式策略聚合子结果；冲突、过时、重复、部分失败和未验证结果不得使用 last-write-wins，最终完成仍由 `completionDecision` 决定。

冻结的最低竞态语义：相同幂等指纹返回原结果；相同 key 但输入/上下文/计划哈希不同则拒绝；CAS 过时必须重读当前状态；迟到和取消后的结果保留为隔离证据；父任务取消级联请求子任务取消，但子任务终态 Receipt 不可变。

冻结的实现顺序：先修正 TaskFocus 的自动确认与真实确认模式，并完成 Focus 身份到 TaskContext 的只读绑定；再定义 Parent/Child、Lease/CAS、ResultEnvelope 和 Aggregator 的静态合同与竞态回放；最后才允许选择单一外部机制做隔离 PoC。GraphView 在本版本中只能作为固定流程作者/验证/可视化投影，不能作为动态子 Agent 调度器或并行能力证据。

## Two-phase execution and capability drift

`planTask` returns an immutable `planHash`. External `runTask` must submit it as `approvedPlanHash`; the coordinator compares the current bindings and rejects stale plans, `NeedsReview` Skills, or Skills without `authorized-only` runtime eligibility before issuing an invocation-bound authorization. Policy v5 stores grants in schema 3 under a permanent cross-process lock and atomically persists `Active / Exhausted / Expired` state. The authorization expires after 15 minutes; a trusted in-process host that binds the full request and current user-instruction SHA-256 may receive at most 20 uses for an L1 local plan, L1/L2 `candidate-only` plans receive at most 5, and L3 or other plans 1. Every reusable use requires a fresh non-empty `idempotencyKey`. External Bridge JSON cannot self-assert `userDirected`. The user-enabled local Bridge remains `ManagedAIBrain`, and only the exact `ui.materialize-screen` + `es.ui.materialize-screen@1` + `L2` + `scoped-write` + `MaterializeUI` combination receives an internally request-bound UI runtime exception; all other `not-proven` Skills remain blocked.

The ES host emits bounded capability-drift signals for queue updates and session resume, and polls Catalog, governance, Knowledge-route, and command metadata hashes. A signal contains only a trigger, generation, and metadata fingerprint. It never loads the full Skill portfolio, grants permission, or substitutes for route-scoped comparison and re-planning.

## Knowledge 使用最小规则

本页只负责发现，不复制各合同的完整规则。使用 Knowledge 时：

1. 从 `KnowledgeIndex.yaml` 按对象、动作、风险和版本选择 1～3 个条目，再读取其 `requiredReads`、正文和 `SourceRefs`；禁止递归加载全库。
2. SourceRef 缺失、Hash 漂移、索引不一致或 `StaleWhen` 命中时，标记相关条目/计划 `stale` 并回读权威来源。
3. Knowledge 只能支持其 `Authority`/`EvidenceLevel` 覆盖的结论；静态证据不能声称 Unity、Runtime、Profiler、Player、IL2CPP、视觉、性能或发布已通过。
4. 一次性读取回执只记录 `selectedKnowledgeIds`、`requiredReads`、来源哈希、`authorityDecision`、`evidenceLevel`、`staleCheck` 和 `nonClaims`；权限和完整证据分别遵循 AGENTS、AICommand/TaskContract 与验证器合同。
5. 理解过时或 Skill 刷新意图统一路由到 `es-skill-session-refresh`；只做增量哈希与路由筛选，不自动读取全量 Skill。

权威分工：`AGENTS.md` 定义稳定治理边界；`KnowledgeIndex.yaml` 负责知识选择；对应 `SKILL.md`、AIWarnings、AICommand、TaskContract 和 Receipt 负责具体规则与证明。

## 组件如何找到 AIBrain

### 会话交接意图别名

以下表达都归入 `session + handover` 路由，不要求用户知道 Skill 名称：`交接窗口`、`窗口交接`、`让新 AI 接手`、`交给新窗口`、`准备交接`、`写入 AI 历程`、`保存会话历程`、`handoff`、`handover`、`resume`、`fork`。命中后首选 `es-codex-session-bootstrap`；“理解过时/刷新 Skill”才路由到 `es-skill-session-refresh`。涉及真实新窗口交接时必须使用该 Skill 的 `Complete-ESCodexHandoff.ps1` 编排器，不得直接调用普通 New 入口替代。

所有直接项目 Skill 的中文自然语言别名统一登记在 `.agents/SKILL_ROUTE_ALIASES.zh-CN.json`；使用 `.agents/skills/es-skill-governance/scripts/Test-ESChineseSkillRouteCoverage.ps1` 检查覆盖、孤儿别名和歧义。别名只负责发现，不授予权限。

AI 自发现入口：对用户目标先调用 `.agents/skills/es-skill-governance/scripts/Resolve-ESChineseSkillRoute.ps1` 做中文别名匹配；唯一命中后读取项目内对应 `SKILL.md` 与 `governance.json`。多命中时按对象、动作和风险消歧；无命中时报告 `NoSkillRoute`，回到本页和 `KnowledgeIndex.yaml`，不得猜测或全量扫描。

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
| 公开 Agent 机制复刻与 ES 适配 | `agent-mechanism-replication, research-to-contract, es-adaptation, failure-surface, external-authority` | `es-agent-mechanism-replication`, `es-first-principles-analysis`, `es-adversarial-review`, `es-aibrain-route-authoring`, `es-knowledge-creator`, `es-knowledge-validator` |
| ES AI ABC 语义适配核心（ABCC） | `ai-abc, abc-core, semantic-adapter, evidence, closed-loop, route-stage` | `es-ai-abc-core`, `es-aibrain-route-authoring`, `es-knowledge-creator`, `es-knowledge-validator` |
| 武器 ABC 部件（ABCP） | `ai-abc, abc-part, weapon, weapon-definition, prefab, input, evidence` | `es-weapon-abc-part`, `es-ai-abc-core`, `es-knowledge-creator`, `es-knowledge-validator` |
| AIBrain 编排与 Automation 任务路由 | `orchestration, task-routing, automation, task-contract, worker, automation-run-record, agent-execution-graph, aicommand, mcp` | `es-aibrain-route-authoring`, `es-use-ai-command`, `es-automation-worker-authoring`, `es-aicommand-contract-authoring` |
| 任务与上下文平台生命周期 | `task-context-runtime, task-lifecycle, context-lifecycle, goal-revision, route-plan, completion-decision, delivery-acceptance, evidence-set, evidence-verifier, source-scope, receipt, cas, reopen` | `es-task-context-runtime`, `es-aibrain-route-authoring`, `es-task-read-snapshot`, `es-observability-evidence` |
| AI 用户交互与任务收尾治理 | `interaction, conversation, prompt, objective, verification, uncertainty, next-step, behavior-tree, context-collection, numeric-selection, next-step-dispatch, goal-drift, handover, closeout, evaluation, dialogue-quality` | `es-ai-interaction-governance`, `es-codex-session-bootstrap`, `es-skill-session-refresh` |
| ES AI 协作菜单与制作/迭代引导 | `menu, collaboration-menu, guidance, creation, iteration, framework-governance, evidence, context-discovery, session-coordination` | `es-ai-collaboration-menu` |
| AI 生成内容空间与 Local/Public 放置治理 | `governance, ai-space, folder-organization, file-placement, local-public, generated-content, stale` | `es-ai-space-organization` |
| Skill 验证与质量门禁 | `skill, validation, security, catalog, evidence, evidence-pending, portfolio, static-replay, deep-replay, deterministic, static-boundary, external-side-effect, blocking-layer` | `es-skill-validator`, `es-skill-creator`, `es-static-deep-replay` |
| 商业一致性与交付证据 | `commercial-coherence, delivery-tracking, evidence-receipt, report-hash, source-freshness, plan-hash, static-review, runtime-not-run` | `es-skill-governance`, `es-knowledge-validator`, `es-release-acceptance` |
| 工作树与编码 | `worktree, utf8, validation` | `es-worktree-audit`, `es-utf8-guard` |
| Unity 编译、MonoBehaviour 生命周期与验收 | `unity, compile, monobehaviour, lifecycle, static-state, domain-reload, scene-reload, enter-play-mode, script-execution-order, player, il2cpp, aot, test, release, evidence, unity-build-identity, artifact-provenance, build-fingerprint, build-input-snapshot, build-output-hash, build-receipt, artifact-freshness, player-provenance, hybridclr-input-hash, build-reproducibility` | `es-unity-compile`, `es-release-acceptance`, `es-editor-availability-validator`, `es-observability-evidence`, `es-worktree-audit` |
| Unity 编辑态执行与 Prefab Stage | `unity, execute-always, execute-in-edit-mode, edit-mode, prefab-stage, prefab-mode, prefab-auto-save, application-is-playing, playing-world` | `es-editor-tooling`, `es-unity-compile` |
| 游戏 UI 玩家目标与 IntentSpec | `ui-automation, player-intent, player-goal, intent-spec, primary-action, ui-intent-clarification, business-bridge` | `es-ui-intent-authoring`, `es-ui-prefab-authoring` |
| 游戏 UI 自动化装配 | `ui-automation, screen-spec-v3, ui-prefab, ui-fixture-scene, ui-layout, responsive, visual-qa, asset-fallback` | `es-ui-prefab-authoring`, `es-unity-compile` |
| Unity UI AI 防错适配 | `ui-automation, ui-ai-failure-prevention, ui-system-selection, ui-layout, responsive, ui-clipping, ui-interaction, ui-rendering, ui-input, ui-toolkit, visual-evidence, evidence-boundary` | `es-knowledge-creator`, `es-ai-knowledge-curation`, `es-ui-prefab-authoring` |
| 游戏 UI 外部方案与规范化适配 | `ui-automation, ui-workflow, normalized-adapter, canonical-owner, knowledge-deduplication, schema-adapter, ai-error-prevention, open-source-ui, design-to-unity, intermediate-representation, source-map, readiness-report, visual-diff, conformance, ui-flow, known-loss, ui-mcp` | `es-knowledge-creator`, `es-ai-knowledge-curation`, `es-ui-prefab-authoring` |
| 游戏 UI 屏幕族与信息架构 | `game-ui-screen-family, commercial-ui, hud-ui, inventory-ui, shop-ui, dialogue-ui, map-ui, progression-ui, result-ui, settings-ui, ui-information-architecture` | `es-ui-intent-authoring`, `es-ui-prefab-authoring` |
| 游戏 UI 视觉设计与 Token | `ui-visual-design, visual-design, design-token, color-role, typography-role, spacing-token, visual-hierarchy, information-density, rarity-visual, ui-material` | `es-ui-prefab-authoring`, `es-editor-tooling` |
| 游戏 UI 参考图与输入证据 | `ui-reference-evidence, design-evidence, reference-image, reference-provenance, source-region, vision-review, observation-assumption` | `es-ui-prefab-authoring`, `es-knowledge-creator` |
| 游戏 UI AssetManifest 与素材解析 | `ui-asset-manifest, asset-manifest, asset-provenance, asset-license, asset-fallback, sprite-atlas, crop-policy, asset-resolver` | `es-ui-prefab-authoring`, `es-resource-pipeline`, `es-resource-publish-audit` |
| 游戏 UI 行为、焦点与导航 | `ui-behavior-spec, behavior-spec, ui-binding, ui-interaction-intent, ui-focus, ui-navigation, input-modality, input-system-ui` | `es-ui-prefab-authoring`, `es-input-action` |
| 游戏 UI 文本与本地化韧性 | `ui-text-resilience, ui-localization, long-content, text-wrapping, bidi, rtl, glyph-coverage, font-fallback, line-breaking` | `es-ui-prefab-authoring`, `es-editor-tooling` |
| 测试场景构建与备份权威 | `scene-builder, prefab-override, scene-fixture, scene-layout, scene-backup, backup-manifest` | `es-test-fixture-authoring`, `es-editor-tooling` |
| 测试场景验收与发布证据 | `scene-validation, scene-guide, acceptance, release, evidence, receipt, profiler, unity` | `es-release-acceptance`, `es-observability-evidence`, `es-worktree-audit` |
| GameCore 与稳定身份 | `gamecore, config, identity, config-key, runtime-key, catalog, root-so, runtime-data, content-registration` | `es-gamecore-integration`, `es-gamecore-config-authoring`, `es-tag-config` |
| 资源与发布链 | `resource, asset, manifest, provider, resource-plan, owner-scope, temporary-scope, lease, provider-transition` | `es-resource-pipeline`, `es-resource-publish-audit` |
| Entity、输入与命令 | `entity, input, input-action, runtime-mode, control, command, runner, runner-tick, lifecycle` | `es-entity-authoring`, `es-entity-prefab-validation`, `es-input-action`, `es-command-authoring` |
| 运行时生命周期、Pool 与仲裁 | `runtime, lifecycle, generic-life, pool, operation, lease, request, arbitration, commit, executor` | `es-entity-authoring`, `es-performance-budgeting`, `es-test-fixture-authoring` |
| 热路径、托管分配与内存容量证据 | `performance, runtime-hot-container, container-warmup, steady-state-gc, capacity-growth, pool, prewarm, profiler, run-record, zero-gc, managed-allocation, allocation-static-audit, boxing, closure, delegate, foreach, iterator, yield, async, linq, gc, false-positive, hot-path, memory-budget, resident-memory, capacity-budget, high-water-mark, pool-size, cache-size, trim, retention, memory-profiler, gc-tradeoff` | `es-performance-budgeting`, `es-observability-evidence`, `es-ai-knowledge-curation`, `es-knowledge-validator` |
| 编辑器、Graph 与 Agent 产物 | `editor, graph, agent-authoring, editor-window, editor-extension, inspector, drawer, dialog, popup, workbench, layout, responsive, high-dpi, single-axis-scroll, owner-lifecycle, reload-domain, undo-dirty, preview-lifecycle, editor-performance, window-production-standard` | `es-editor-tooling`, `es-editor-availability-validator`, `es-generate-agent-artifacts` |
| Stable Graph V2 作者与烘焙边界 | `graph, stable-graph-v2, graph-identity, graph-undo, graph-migration, edge-order, graph-snapshot, graph-bake, legacy-graph` | `es-stable-graph-authoring`, `es-editor-tooling` |
| 编辑器正式资产与序列化事务 | `editor, asset-authoring, asset-database, prefab, prefab-override, serialized-object, serialized-property, undo, dirty, save, transaction, scene-builder, backup` | `es-editor-tooling`, `es-api-contract-review`, `es-entity-prefab-validation`, `es-test-fixture-authoring` |
| Unity 序列化、渲染与图集 | `unity, serialization, asset-guid, local-file-id, serialize-reference, rendering, shader, material, shader-keyword, shader-variant, material-variant, sprite-atlas, ui-canvas, canvas-sorting, ui-batching, draw-call, frame-debugger, srp-batcher, material-property-block, mask, stencil, batch-break` | `es-editor-tooling`, `es-api-contract-review`, `es-performance-budgeting`, `es-observability-evidence`, `es-unity-compile`, `es-release-acceptance` |
| Fixture 与视觉证据 | `fixture, test-fixture, deterministic, editmode, playmode, screenshot, resolution, visual-qa, visual-evidence, gpu-capture` | `es-test-fixture-authoring`, `es-observability-evidence`, `es-release-acceptance` |
| 模块审计与会话 | `audit, lifecycle, session, handover` | `es-module-lifecycle`, `es-codex-session-bootstrap` |
| 跨域协作路由与证据基础设施 | `action, communication, integration, knowledge-search, pipeline, plan, preservation, reload, route, runrecord, screen-family, stable-graph, static-replay, deep-replay` | `es-aibrain-route-authoring`, `es-task-read-snapshot`, `es-observability-evidence`, `es-static-deep-replay`, `es-editor-tooling`, `es-stable-graph-authoring` |
| 飞书外部协作适配器 | `feishu, lark, external-adapter, dry-run, task-monitor, task-dispatch, task-transition, virtual-team, identity-claim, bot-ownership, onboarding, message-send, notification` | `es-feishu-cli`, `es-use-ai-command`, `es-automation-worker-authoring` |
| Knowledge 输出、验证、维护事务与条目治理 | `knowledge, knowledge-quality, knowledge-output, validation, source-ref, content-hash, hash, routing, route-probe, misroute, canonical-entry, dedup, maintenance-transaction, refresh-plan, stable-refresh, cas, concurrent-update, atomic-projection, recovery, evidence, evidence-boundary, permission-boundary, bounded-output, stale` | `es-knowledge-creator`, `es-knowledge-validator`, `es-ai-knowledge-curation`, `es-aibrain-route-authoring`, `es-worktree-audit`, `es-task-read-snapshot`, `es-utf8-guard` |
| 任务读取一致性与解析投影基础设施 | `task, read, snapshot, consistency, hash, stale` | `es-task-read-snapshot` |
| 长运行 AI 会话的 Skill 增量发现与能力刷新 | `skill, session, refresh, capability, delta, stale, routing` | `es-skill-session-refresh`, `es-task-read-snapshot` |
| Skill 理解刷新与增量能力发现 | `understanding-drift, skill-understanding-refresh, capability-refresh, incremental-discovery` | `es-skill-session-refresh`, `es-task-read-snapshot` |
| AIWarnings 领域治理 | `aiwarnings, p0, architecture, runtime, editor, validation, handover, archive` | `es-aiwarning-authoring`, `es-aibrain-route-authoring`, `es-ai-knowledge-curation` |

### 机器可读 Knowledge 路由探针

`es.knowledge.routing-quality.v1` 是路由探针的 canonical owner。`Documentation/AIKnowledge/RouteProbeRegistry.json` 是唯一数据集，`ES/Automation/Contracts/es-knowledge-route-probe-registry.schema.json` 是结构合同；其他 Knowledge 只引用，不复制探针事实。

- 命令行静态验证：`powershell -NoProfile -File Documentation/AIKnowledge/tools/Test-ESKnowledgeRouteProbeRegistry.ps1 -ProjectRoot <absolute-project-root>`
- Unity 自然语言推导验证：`ESAIBrainKnowledgeRoutingTests.Plan_RouteProbeRegistry_MatchesFixedCrossDomainExpectations`
- AI Bridge 只读操作：`runKnowledgeRouteProbes`；`listCapabilities` 通过 `diagnostic.knowledge-route-probes` 发现该能力。
- `operational-static` 只表示注册、结构、当前索引排名、Top-3、禁止命中、requiredReads 和确定性重放静态闭合；Unity Test Runner 未执行时仍为 `runtime-not-run`。
- 新领域先向注册表增加唯一 `probeId`，再运行 CLI；涉及自然语言推导变化时还必须运行 Unity 测试消费者。不得在领域 Knowledge 中另建平行探针表。

路由歧义：`cache` 只有在任务明确涉及 Skill 执行/读取基础设施时才进入治理路由；运行时缓存、GC、帧预算仍走性能路由。`Snapshot` 只有同时出现文件读取、解析、哈希、清单或投影语义时才进入一致性路由。

目录和 routeKey 只负责发现，不授予权限。用户指令是动作授权；受管通道另行遵循 `AICommand`、`TaskContract`、`PlanHash` 与 `.agents/SKILL_DISCOVERY_POLICY.json`。Registry 代际漂移只使相关计划 `stale`，不扩大或缩小用户授权。

## 受治理核心 Skills

以下 Skill 使用 AIBrain 可执行校验的独立权威轴。`project-gate` 是跨项目门禁，`core-governed` 是核心治理流程；二者在 AIBrain 自主/受管通道中必须经过 `planTask` 且禁止无当前用户指令的直启。该元数据不阻止 current-user-direct 工作。基础工程 Skill 可由 AIBrain 自动附加，但只能在当前用户范围和受管通道合同内运行。

- `project-gate`: `es-skill-governance`, `es-use-ai-command`, `es-feishu-cli`, `es-utf8-guard`, `es-release-acceptance`
- `core-governed`: `es-skill-creator`, `es-worktree-audit`, `es-task-context-runtime`, `es-task-read-snapshot`

这些 Skill 只是高权威门禁/编排入口，不自动获得源码、Unity、Git 或发布权限。

## 扩展 Skill 族

资源组合索引：`.agents/SKILL_RESOURCE_INDEX.yaml`；分类与生命周期：`.agents/SKILL_CATALOG.yaml`。需要脚本或 MCP 时先读索引和对应合同，再检查连接与证据；MCP 可见性不等于授权。
