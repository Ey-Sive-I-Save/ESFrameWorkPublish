# AIWarnings 领域路由地图

状态：由 AIWarnings 当前入口和规则索引派生；本条目不取代 P0 原文。

`KnowledgeId`: `es.aiwarnings.domain-map.v1`
`Authority`: `AIWarnings`
`RouteKeys`: `aiwarnings`, `p0`, `architecture`, `runtime`, `editor`, `validation`, `handover`, `archive`
`ContentHash`: `32db49cc160b3638d381f9f002359373d72b134fa9e171c3502451eba90b3667`

`SourceRefs`:

- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`b59233c67b4e86f2c85b96e975af76f633a1a4b0dbe6e6796ca8ef26df826863`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`c5359cb022ebc2902c4400ad44429da36d1a2dcfa44803586f8f91aaca0d704f`)

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

- 先读 Start/README、CurrentStatus 和 RuleIndex，再按 routeKey 读取最小领域目录。
- AIWarnings P0、禁止事项和证据要求高于 AIKnowledge 摘要、Skill 和聊天上下文。
- 领域目录变化、入口哈希变化或规则索引变化会使本条目 stale；必须重新抽取并重新 planTask。
- 本条目不授权写入、发布、Unity 操作、Git 操作或外部协作。

`EvidenceLevel`: `S1`
`StaleWhen`: 任一 SourceRef 哈希变化、RuleIndex 规则变化或领域目录重命名。
