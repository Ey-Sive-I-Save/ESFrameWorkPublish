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
| AIBrain 编排与 Automation 任务路由 | `orchestration, task-routing, automation, task-contract, worker, run-record, graph, aicommand, mcp` | `es-aibrain-route-authoring`, `es-use-ai-command`, `es-automation-worker-authoring`, `es-aicommand-contract-authoring`, `es-stable-graph-authoring` |
| Skill 验证与质量门禁 | `skill, validation, security, catalog, evidence, evidence-pending, portfolio, static-replay, deep-replay, deterministic, static-boundary, external-side-effect, blocking-layer` | `es-skill-validator`, `es-skill-creator`, `es-static-deep-replay` |
| 工作树与编码 | `worktree, utf8, validation` | `es-worktree-audit`, `es-utf8-guard` |
| Unity 编译与验收 | `unity, compile, test, release, evidence` | `es-unity-compile`, `es-release-acceptance` |
| 游戏 UI 自动化装配 | `ui-automation, screen-spec-v3, ui, prefab, fixture-scene, layout, responsive, visual-qa` | `es-ui-prefab-authoring`, `es-unity-compile` |
| GameCore 与稳定身份 | `gamecore, config, identity` | `es-gamecore-integration`, `es-tag-config` |
| 资源与发布链 | `resource, asset, manifest, provider` | `es-resource-pipeline` |
| Entity、输入与命令 | `entity, input, command, runtime` | `es-entity-authoring`, `es-input-action`, `es-command-authoring` |
| 编辑器、Graph 与 Agent 产物 | `editor, graph, agent-authoring, editor-window, editor-extension, inspector, drawer, dialog, popup, workbench, layout, responsive, high-dpi, single-axis-scroll, owner-lifecycle, reload-domain, undo-dirty, preview-lifecycle, editor-performance, window-production-standard` | `es-editor-tooling`, `es-editor-availability-validator`, `es-generate-agent-artifacts` |
| 模块审计与会话 | `audit, lifecycle, session, handover` | `es-module-lifecycle`, `es-codex-session-bootstrap` |
| 外部只读适配器 | `feishu, lark, external-adapter, dry-run` | `es-feishu-cli`, `es-use-ai-command`, `es-editor-tooling` |
| Knowledge 输出与条目治理 | `knowledge, knowledge-output, source-ref, hash, routing, evidence, bounded-output` | `es-knowledge-creator`, `es-ai-knowledge-curation` |
| 任务读取一致性与解析投影基础设施 | `task, read, snapshot, consistency, hash, stale` | `es-task-read-snapshot` |
| 长运行 AI 会话的 Skill 增量发现与能力刷新 | `skill, session, refresh, capability, delta, stale, routing` | `es-skill-session-refresh`, `es-task-read-snapshot` |
| Skill 理解刷新与增量能力发现 | `understanding-drift, skill-understanding-refresh, capability-refresh, incremental-discovery` | `es-skill-session-refresh`, `es-task-read-snapshot` |
| AIWarnings 领域治理 | `aiwarnings, p0, architecture, runtime, editor, validation, handover, archive` | `es-aiwarning-authoring`, `es-aibrain-route-authoring`, `es-ai-knowledge-curation` |

路由歧义收口：`skill-performance`、`execution-cost`、`fast-path`、`deep-path` 和 `cache` 只在任务明确讨论 **Skill 调用、启动或执行流程** 时归入治理功能区。运行时缓存、GC、帧预算或 Unity 性能问题仍按运行时性能和 `$es-performance-budgeting` 路由；AIBrain 不得只因出现 `cache` 一词就改变领域。`consistency` 是文件读取基础设施信号，必须与文件、读取、解析、快照或投影语义共同出现。

目录是导航投影，不授予权限；最终权限仍由 AICommand、TaskContract 和用户授权决定。

## 受治理核心 Skills

以下 Skill 使用 AIBrain 可执行校验的独立权威轴。`project-gate` 是跨项目门禁，`core-governed` 是核心治理流程；二者都必须经过 `planTask`、禁止直接执行，并且不扩大写权限。基础工程 Skill 可以由 AIBrain 按任务形态自动附加，但仍必须经过同样的 Skill、AICommand、TaskContract 和证据门禁。

- `project-gate`: `es-skill-governance`, `es-use-ai-command`, `es-feishu-cli`, `es-utf8-guard`, `es-release-acceptance`
- `core-governed`: `es-skill-creator`, `es-worktree-audit`, `es-task-read-snapshot`

它们是高权威门禁/编排能力，不等于拥有源码、Unity、Git 或发布权限。

## 扩展 Skill 族

资源组合索引位于 `.agents/SKILL_RESOURCE_INDEX.yaml`，分类与生命周期快照位于 `.agents/SKILL_CATALOG.yaml`。每个直接 Skill 目录必须有且仅有一条 Catalog 记录；记录包含 family、routeKeys、状态、首次注册、最近修改/复核时间与治理哈希。Catalog 只负责发现和陈列，不授予权限；AIBrain 仍以 AIWarnings、AICommand 和 `governance.json` 为执行门禁。需要脚本或 MCP 时，先读取资源索引、Catalog 和对应合同，再检查连接状态与证据，不把 MCP 可见性当作授权。
