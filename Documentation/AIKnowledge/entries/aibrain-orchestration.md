# AIBrain 编排核心知识条目

状态：AIBrain 第一阶段生产力面已接入；仅有源码与静态构建证据，Graph 仍为可选实验适配。

`KnowledgeId`: `es.aibrain.orchestration.v1`
`Authority`: `Derived`
`RouteKeys`: `aibrain`, `orchestration`, `task-routing`, `evidence`
`EvidenceLevel`: `S1`
`ContentHash`: `66b1821d6ea2df4213f149bff970ffb24fac39f8dbf5db0a3d3c016193c46964`

`SourceRefs`:

- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationCenter.cs` (`d1027d9905a34bc9c10215df61150eb1f4bfbb71c33fd5f83b90e9956aac296e`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationFacade.cs` (`7f38d24a53d8f2e382821085c8d711d6fd6ba086d0493bd7d33fb355cf2d12bd`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAgentGraphAutomationEndpoint.cs` (`0bc253ebba46f4deb28cc4820677ee02a7233a070d57b682bbe91242640fd13c`)
- `Assets/Plugins/ES/Editor/ESGraphViewV2/ESAISkillExecutionWorkflow.cs` (`7b17d81c0dc4bbf04d2e91df2c8e47e46c9b811de622dfef03c7f408476a192e`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAutomationCenter与受管Worker治理_AI协作警告.md` (`a33c17c739c6394096b8892bd3eb2497ff4f02b2ecd17fd86e14b4d7ce8c3306`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs` (`5dedd6837210742df5fc4dd252ff7153a5b63e99a936f7b601819d5dd6aec205`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs` (`71d48b7f46fd6068a76193fa6e158c79836594aa5d9e33d69d736032224e0062`)

`EvidenceRefs`: 当前源码入口和本条目的静态哈希；没有 Unity 运行证据。

`StaleWhen`: 任一 SourceRef 哈希变化，或 AIBrain/TaskContract/ESAutomationFacade 权威边界变化。

## 结论

当前实现中，AIBrain 第一阶段是 AI 协作生产力控制面：发现正式 Project Skills、AICommand、AIWarnings、AIKnowledge、受管 CLI Worker 与 UnityMCP 宿主能力；按 routeKeys 建立只读计划，再向 AutomationFacade 签发一次性执行许可。对存在 `governance.json` 的 Skill，AIBrain 会严格读取并把治理元数据和哈希纳入生产力面与计划哈希；它不直接启动进程或写入 Assets，Graph 仅作为可选实验适配器。

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
- `runTask`：只接受已通过 AIBrain 的计划，并消费一次性授权。
- `getRun` / `cancelRun` / `submitInput`：观察、取消和继续受管任务。

`planTask` 和 `runTask` 的 `skillNames` 可省略；省略时 AIBrain 先根据目标文本和显式 `routeKeys` 形成最小路由，再从命中 Knowledge 的 `relatedSkills` 中选择当前项目中同时具备 `SKILL.md` 与 `agents/openai.yaml` 的正式 Skill。多文件一致性、快照、源文件哈希、Parser/Projection 或文件读取缓存漂移会自动追加 `consistency` 基础路由，因此用户不需要知道 `es-task-read-snapshot` 这个内部名称。没有完整 Skill 时计划阻断，不把候选目录或摘要当作能力。

治理级 Skill 还必须提供合法的 `governance.json`。状态、风险、证据等级或 `allowDirectExecution` 不合法时，AIBrain 阻断该 Skill；Skill、治理元数据、Knowledge、AICommand 或 TaskContract 任一哈希漂移后，旧 PlanHash 不得继续执行。

`authorityClass` 是独立于 Tier 的门禁轴：`standard`、`core-governed`、`project-gate`。后两者必须绑定 AIBrain 计划；`project-gate` 至少需要 S2 证据。权威级只影响路由和阻断，不授予源码、Unity、Git、发布、删除或网络权限。

候选 Skill 目录不会被发现为正式能力；候选必须经过 Unity Diff Review 和人工批准后才能进入 `.agents/skills`。

## 权威边界

- AIWarnings：P0、禁止事项和证据门禁的不可绕过权威。
- AICommand：当前任务的权限合同。
- Skill：可复用工作流，不扩大权限。
- ESAutomationCenter：任务注册、路径策略、受管进程和 RunRecord 权威。
- AIKnowledge：定向索引与摘要，不拥有源事实。
- Feishu：外部协作出口，不是项目事实源。

## 失败条件

- AIBrain 直接调用 ProcessRunner。
- AIBrain 直接写入 `Assets/`。
- Knowledge 条目没有 SourceRefs 或 ContentHash。
- Skill 绕过 AICommand 或 AutomationFacade。
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
