# AIBrain 编排核心知识条目

状态：AIBrain 第一阶段生产力面已接入；仅有源码与静态构建证据，Graph 仍为可选实验适配。

`KnowledgeId`: `es.aibrain.orchestration.v1`
`Authority`: `Derived`
`RouteKeys`: `aibrain`, `orchestration`, `task-routing`, `evidence`
`EvidenceLevel`: `S1`
`ContentHash`: `bbd42d2ed32462bcbaffe51efc9a16c75976f0bb7e67120d0903dc8a9c43a450`

`SourceRefs`:

- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationCenter.cs` (`6fc627e16930d541b1275bb5d687e1fdad8d96b751616002dbf2fbdbfa38fbc3`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationFacade.cs` (`e61a58d14237555a09207cf3e3c596b48e0ee2de6188d10584b950c62606d4d2`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAgentGraphAutomationEndpoint.cs` (`0bc253ebba46f4deb28cc4820677ee02a7233a070d57b682bbe91242640fd13c`)
- `Assets/Plugins/ES/Editor/ESGraphViewV2/ESAISkillExecutionWorkflow.cs` (`7b17d81c0dc4bbf04d2e91df2c8e47e46c9b811de622dfef03c7f408476a192e`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAutomationCenter与受管Worker治理_AI协作警告.md` (`a33c17c739c6394096b8892bd3eb2497ff4f02b2ecd17fd86e14b4d7ce8c3306`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs` (`edc33e609c4fefd2dbaf832043dade36e7ca07beedab1db09703947fa7cb9a19`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs` (`20b63b3db889b705ae740d366fa234b8ae49b50a60bf72056cd2a96b86db9b57`)

`EvidenceRefs`: 当前源码入口和本条目的静态哈希；没有 Unity 运行证据。

`StaleWhen`: 任一 SourceRef 哈希变化，或 AIBrain/TaskContract/ESAutomationFacade 权威边界变化。

## 结论

当前实现中，AIBrain 第一阶段是 AI 协作生产力控制面：发现正式 Project Skills、AICommand、AIWarnings、AIKnowledge、受管 CLI Worker 与 UnityMCP 宿主能力；按 routeKeys 建立只读计划，再向 AutomationFacade 签发绑定 Invocation 的限时限次受管通道执行授权。Policy v5 使用 schema 3 持久 Store、永久跨进程锁、原子替换和 `Active / Exhausted / Expired` 终态；授权有效期为 15 分钟。只有用不可序列化 proof 绑定完整请求与当前用户指令 SHA-256 的受信进程内宿主，L1 本地计划才最多使用 20 次；L1/L2 `candidate-only` 最多 5 次，L3 或其他计划 1 次。当前文件 Bridge 通过可选的 `userDirectedRuntime` 分支绑定 `CurrentUserDirect` proof；该分支必须携带当前用户指令 SHA-256，并仍受固定 command/task allowlist 与低风险计划门禁约束，不能把字段存在或布尔值传入误读成无条件授权。可复用授权的每次调用都必须提供新的非空 `idempotencyKey`。对存在 `governance.json` 的 Skill，AIBrain 会严格读取并把治理元数据和哈希纳入生产力面与计划哈希；它不直接启动进程或写入 Assets，Graph 仅作为可选实验适配器。该流程只描述受管通道，不构成当前用户直接工作的开工门禁。

当前源码已进一步收紧计划到执行的闭环：`Plan`/`Run` 先复制并冻结请求快照；受管 `runTask` 必须提交 `planTask` 返回的 `approvedPlanHash`、稳定 N 格式 `invocationId` 和格式受限的 `idempotencyKey`。`TryApprovePlan` 只为 `fromAi` 请求签发仍可运行的 canonical 计划，并把计划、命令、TaskContract、输入和调用身份绑定到授权；`ExecutionSnapshot` 还必须匹配 `brainPlanHash`、`commandHash` 与 `taskContractHash`。Facade 在执行前做一次不消费的授权预检，并在 Endpoint/合同/能力/快照/路径/AI/PlayMode 门禁全部通过后立即再次校验并消费，避免并发消费竞态。

授权实现为 Policy v5 / Store schema 3：跨进程永久锁、受管原子替换、PlanHash/InvocationId 双唯一、终态墓碑和未知代际 fail-closed；Policy v4/schema 2 只在新 Invocation 注册时迁移。能力元数据现在由 Resource Index、Skill Catalog、Discovery Policy、KnowledgeIndex、AIBRAIN_ENTRY 和 AICommand Catalog 组成指纹，Bridge 在队列更新、会话恢复和周期轮询时发出有界 `CapabilityDriftSignal`，但信号只提示刷新，不替代重新路由或重新规划。生产力面新增静态 `runKnowledgeRouteProbes` 与有界脱敏 `getFailureTelemetry`；失败分类包含错误路由、RequiredReads 溢出、任务执行失败和 `ClaimDowngraded`。

## 固定数据流

```text
BrainContext
  -> AIWarnings P0 Gate
  -> AIKnowledge Query
  -> Project Skill Selection
  -> AICommand TaskContract
  -> CLI / MCP Capability Check
  -> ESAutomationFacade
  -> 受管 Worker 或 Unity 主线程桥接
  -> RunRecord + Evidence
```

## 第一阶段入口

- `listCapabilities`：只读发现正式 Skills、AICommands、AIWarnings、Knowledge、受管 CLI 和 MCP 宿主能力。
- `planTask`：按目标、routeKeys、Command、Skill 和 TaskContract 建立只读计划；不会产生任务副作用。
- `runTask`：只接受已通过 AIBrain 的计划，并消费与当前 Invocation、TTL、剩余次数和幂等键闭合的授权使用次数。
- `getRun` / `cancelRun` / `submitInput`：观察、取消和继续受管任务。

`planTask` 和 `runTask` 的 `skillNames` 可省略；省略时 AIBrain 先根据目标文本和显式 `routeKeys` 形成最小路由，再从命中 Knowledge 的 `relatedSkills` 中选择当前项目中同时具备 `SKILL.md` 与 `agents/openai.yaml` 的正式 Skill。多文件一致性、快照、源文件哈希、Parser/Projection 或文件读取缓存漂移会自动追加 `consistency` 基础路由，因此用户不需要知道 `es-task-read-snapshot` 这个内部名称。没有完整 Skill 时只阻断对应的 AIBrain 受管计划，不把候选目录或摘要当作能力；当前用户直接请求仍可在其明确范围内实现。

治理级 Skill 还必须提供合法的 `governance.json`。状态、风险、证据等级或 `allowDirectExecution` 不合法时，AIBrain 阻断该受管 Skill 计划；Skill、治理元数据、Knowledge、AICommand 或 TaskContract 任一哈希漂移后，旧 PlanHash 不得继续执行。这些字段不否决当前用户直接通道。

`authorityClass` 是独立于 Tier 的受管门禁轴：`standard`、`core-governed`、`project-gate`。在 `ManagedAIBrain` lane 中，后两者必须绑定 AIBrain 计划，且 `project-gate` 至少需要 S2 证据。权威级只影响该通道的路由和证据，不授予 AI 自行扩张，也不缩小当前用户明确范围。

候选 Skill 目录不会被 AIBrain 发现为正式受管能力；AI 自主候选必须经过 Diff Review 才能进入 `.agents/skills`。当前用户明确要求正式创建或登记 Skill 时可直接实施，不再要求项目内部二次批准。

## 权威边界

- AIWarnings：P0、禁止事项和证据门禁的不可绕过权威。
- AICommand：`ManagedAIBrain`/Worker 通道的当前任务协议，不是用户授权来源。
- Skill：可复用工作流，不扩大权限。
- ESAutomationCenter：任务注册、路径策略、受管进程和 RunRecord 权威。
- AIKnowledge：定向索引与摘要，不拥有源事实。
- Feishu：外部协作出口，不是项目事实源。

## 失败条件

- AIBrain 直接调用 ProcessRunner。
- AIBrain 直接写入 `Assets/`。
- Knowledge 条目没有 SourceRefs 或 ContentHash。
- `ManagedAIBrain`/Worker 中的 Skill 绕过 AICommand 或 AutomationFacade；这不适用于当前用户直接通道。
- 用摘要、zread 或 Feishu 缓存替代源码/Unity/Profiler 证据。

## 相关实现入口

- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationCenter.cs`
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationFacade.cs`
- `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs`
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs`
- `Assets/Plugins/ES/Editor/ESAutomation/ESAgentGraphAutomationEndpoint.cs`
- `Assets/Plugins/ES/Editor/ESGraphViewV2/ESAISkillExecutionWorkflow.cs`

## 证据边界

当前已取得源码与 `ES_Editor.csproj` 静态构建事实；Unity Editor 导入、ReloadDomain、Test Runner、受管 Worker 闭环、Profiler、Player/IL2CPP 仍需单独验收。
