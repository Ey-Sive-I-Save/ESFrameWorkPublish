# AIWarnings 领域路由地图

状态：由 AIWarnings 当前入口和规则索引派生；本条目不取代 P0 原文。

`KnowledgeId`: `es.aiwarnings.domain-map.v1`
`Authority`: `AIWarnings`
`RouteKeys`: `aiwarnings`, `p0`, `architecture`, `runtime`, `editor`, `validation`, `handover`, `archive`
`ContentHash`: `06de948b50e15383eb320676d35d33b1ac3a7bd80e36502a2175b4e596a26b35`

`SourceRefs`:

- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`04af5af87127d069f4a5d2914ee12ce885043b804bd4d6050a3ec342721ca66b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)

## 定向路由

| routeKey | AIWarnings 领域 | 读取范围 | 相关 Skill |
|---|---|---|---|
| `p0` | P0 最高约束 | `10_P0最高约束（P0Guardrails）` | `es-aiwarning-authoring`, `es-skill-governance` |
| `architecture` | 架构现状与跨系统语义 | `20_架构现状（Architecture）` | `es-aibrain-route-authoring`, `es-api-contract-review` |
| `runtime` | 运行时专项 | `30_运行时专项（RuntimeOperations）` | `es-automation-worker-authoring`, `es-observability-evidence` |
| `editor` | 编辑器与工具 | `40_编辑器与工具（EditorTooling）` | `es-editor-tooling` |
| `validation` | 验证与发布 | `50_验证与发布（ValidationRelease）` | `es-release-acceptance`, `es-release-notes-evidence` |
| `handover` | 交接与复盘 | `80_交接与复盘（Handover）` | `es-codex-session-bootstrap`, `es-module-lifecycle` |
| `archive` | 提案与废止 | `90_提案与废止（Archive）` | `es-change-risk-register`, `es-migration-planning` |

## 门禁

- `ActionAuthority`: `CurrentExplicitUserInstruction`
- `ManagedProtocolRequiredWhen`: `ManagedAIBrain/Worker`
- `SourceDriftEffect`: `KnowledgeAndDependentPlanStale`
- `SourceDriftRequiresSecondUserApproval`: `false`

- 先读 Start/README、CurrentStatus 和 RuleIndex，再按 routeKey 读取最小领域目录。
- 在事实、长期约束和完成声明层，AIWarnings P0、禁止事项和证据要求高于 AIKnowledge 摘要、Skill 和历史
  聊天投影；这不是动作授权排序，不得覆盖当前用户明确指令。
- 当前用户明确指令是本轮有界项目动作的授权来源；AIWarnings 约束实现和完成声明，但不得把明确请求降为
  候选、只读或再次待批准。删除、Git、Unity/Runtime、网络、发布等不同类别动作仍须由当前用户明确点名。
- `AICommand`、`TaskContract` 与 AIBrain `planTask/runTask` 只在选择 ManagedAIBrain/Worker 受管通道时作为
  技术输入和回执合同；直接用户通道不因缺少这些工件而失去授权。
- 领域目录变化、入口哈希变化或规则索引变化会使本条目和依赖计划 stale；必须重读当前来源并据此重规划。
  只有继续使用受管通道时才重新形成或校验 `planTask`，漂移本身不要求用户第二次批准。
- 本条目自身不授予写入、发布、Unity、Git 或外部协作动作；动作授权来自当前用户明确指令，且不得由 AI
  自主引申到未请求类别。

`EvidenceLevel`: `S1`
`StaleWhen`: 任一 SourceRef 哈希变化、RuleIndex 规则变化或领域目录重命名。
