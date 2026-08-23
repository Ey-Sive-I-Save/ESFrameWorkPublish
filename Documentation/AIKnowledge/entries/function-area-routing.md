# AIBrain 功能区路由知识

状态：现行路由投影；权限仍由 AICommand、TaskContract 和用户授权决定。

`KnowledgeId`: `es.function-area-routing.v1`
`Authority`: `Derived`
`EvidenceLevel`: `S1`
`RouteKeys`: `aibrain`, `governance`, `planning`, `authority`, `skill-performance`, `execution-cost`, `fast-path`, `deep-path`, `cache`
`StaleWhen`: AIBrain 路由、功能区绑定或任一 SourceRef 哈希变化。

`SourceRefs`：

- `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs` (`9735b55bf6b2df8758050f2b84b053aabc0438ddf633c3c61ba43e4d684349d9`)
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md` (`a9d171a938841e2fab4409814b9dbcba98261269d30fef7a16304425c59ee316`)

`ContentHash`: `c98d04d36f5094ebc0ee9d67c807fbc4f6f5e575d5f05b249609eda711c6ea39`

## 路由原则

`listCapabilities` 用于发现完整生产力面；`planTask` 必须携带功能区 `routeKeys`。KnowledgeIndex 中每个功能区条目只绑定本区首选 Skills，避免把整个 Skill 集合注入每个任务。

AIBrain 负责从任务目标补充跨领域基础路由，调用方不需要知道内部 Skill 名称。当前 `consistency` 基础路由只在多文件一致性、快照、源文件哈希、Parser/Projection、二进制解析或文件读取缓存漂移信号出现时附加；业务领域 routeKeys 仍由调用方或上游任务分类提供。

详细的人类导航见 `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`。
