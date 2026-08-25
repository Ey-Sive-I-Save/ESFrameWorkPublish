# AIBrain 功能区路由知识

状态：现行路由投影；项目动作授权仅来自当前用户明确指令，AICommand 与 TaskContract 只约束选中的受管通道。

`KnowledgeId`: `es.function-area-routing.v1`
`EntryMode`: `SharedRouteProjection`
`Authority`: `Derived`
`EvidenceLevel`: `S1`
`RequiredReads`: `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`
`RouteKeys`: `aibrain`, `governance`, `planning`, `authority`, `skill-performance`, `execution-cost`, `fast-path`, `deep-path`, `cache`, `analysis`, `design`, `root-cause`, `review`, `risk`, `change-budget`, `rollback`, `migration`, `compatibility`, `worktree`, `utf8`, `validation`, `repository`, `discovery`, `architecture`, `evidence`, `security`, `input`, `mcp`, `cli`, `path-boundary`, `unity`, `compile`, `test`, `release`, `diagnosis`, `verification`, `estest`, `aitest`, `prompt`, `worker`, `profiler`, `gamecore`, `config`, `identity`, `dependency`, `assembly`, `package`, `resource`, `asset`, `manifest`, `provider`, `entity`, `command`, `runtime`, `editor`, `graph`, `agent-authoring`, `api`, `contract`, `audit`, `lifecycle`, `session`, `handover`, `authorization`, `schema`, `action`, `communication`, `integration`, `knowledge-search`, `pipeline`, `plan`, `preservation`, `reload`, `route`, `runrecord`, `screen-family`, `stable-graph`, `static-replay`, `deep-replay`
`StaleWhen`: AIBrain 路由、功能区绑定或任一 SourceRef 哈希变化。

`SourceRefs`：

- `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs` (`edc33e609c4fefd2dbaf832043dade36e7ca07beedab1db09703947fa7cb9a19`)
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md` (`72425a0e2703081f46d7f15c963f79ae24ebf2152ba1e3b61d2dbe3fb96fc6b4`)

`ContentHash`: `15d653e6c6a1a9bb0b2b7597019e9cad09ff2074e9c3105f57ca851be6265739`

## RouteProjections

- `es.function-area.governance.v1`: `aibrain`, `governance`, `planning`, `authority`, `skill-performance`, `execution-cost`, `fast-path`, `deep-path`, `cache`, `analysis`, `design`, `root-cause`, `review`, `risk`, `change-budget`, `rollback`, `migration`, `compatibility`
- `es.function-area.worktree.v1`: `worktree`, `utf8`, `validation`, `repository`, `discovery`, `architecture`, `evidence`, `security`, `input`, `mcp`, `cli`, `path-boundary`
- `es.function-area.release.v1`: `unity`, `compile`, `test`, `release`, `evidence`, `diagnosis`, `verification`, `estest`, `aitest`, `prompt`, `worker`, `profiler`
- `es.function-area.gamecore.v1`: `gamecore`, `config`, `identity`, `dependency`, `assembly`, `package`, `architecture`
- `es.function-area.resource.v1`: `resource`, `asset`, `manifest`, `provider`
- `es.function-area.entity.v1`: `entity`, `input`, `command`, `runtime`
- `es.function-area.editor-agent.v1`: `editor`, `graph`, `agent-authoring`, `api`, `contract`, `compatibility`, `review`
- `es.function-area.lifecycle.v1`: `audit`, `lifecycle`, `session`, `handover`, `command`, `contract`, `authorization`, `schema`, `action`, `communication`, `integration`, `knowledge-search`, `pipeline`, `plan`, `preservation`, `reload`, `route`, `runrecord`, `screen-family`, `stable-graph`, `static-replay`, `deep-replay`

## 路由原则

`listCapabilities` 用于发现完整生产力面；`planTask` 必须携带功能区 `routeKeys`。KnowledgeIndex 中每个功能区条目只绑定本区首选 Skills，避免把整个 Skill 集合注入每个任务。

AIBrain 负责从任务目标补充基础路由和已注册的领域限定路由，调用方不需要知道内部 Skill 名称。`consistency` 只在多文件读取、读取清单、源文件哈希、Parser/Projection、二进制解析或带明确文件上下文的 Snapshot 信号出现时附加。Graph、Story 等领域 Snapshot 必须优先进入所属领域，不能仅凭 `Snapshot` 一词加载任务读取基础设施。

## Scope and decision boundary

- 本投影只负责把功能区 routeKeys 映射到首选 Knowledge/Skills；不拥有任何源码、Unity、Graph、Worker 或发布事实。
- 自然语言无法推导 routeKeys 时返回 `NoKnowledgeRoute`，读取 Start/CurrentStatus/RuleIndex 并记录覆盖缺口；不得借相邻功能区继续。
- 命中超过三个候选时按重叠键数量、重叠比例和 KnowledgeId 稳定排序，只加载前三个；不得递归读取全部条目。
- 命中条目的 SourceRef 或 ContentHash 漂移时停止使用该投影结果并重新规划。

## Failure recovery and non-claims

- 零命中：报告自然语言、推导键和缺失领域，不伪造 routeKey。
- 误命中：收紧领域触发词并增加相邻领域负向探针，不通过增加宽泛同义词掩盖冲突。
- 本投影通过只证明静态路由闭包，不证明 Skill 可执行、AICommand 已授权、Unity 已运行或发布已验收。

详细的人类导航见 `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`。
