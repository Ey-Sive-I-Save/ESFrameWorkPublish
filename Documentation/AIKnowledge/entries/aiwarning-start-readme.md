# ES AIWarnings 启动入口与最小读取链

`KnowledgeId`: `es.aiwarning.start.readme.v1`  
`Authority`: `AIWarnings startup route + AIBRAIN_ENTRY`  
`RouteKeys`: `aiwarnings`, `start`, `current-status`, `rule-index`, `p0`, `knowledge`, `skills`, `evidence`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `33eec77f0d4ab0918885ecf27e91027edcc35e60a2a6f4b7d4d01405e995e4d7`  
`SourceSetHash`: `33eec77f0d4ab0918885ecf27e91027edcc35e60a2a6f4b7d4d01405e995e4d7`  
`EntryBodyHash`: `9c1a88de0986fb6b225e13bef344c2cccdce471a384a47a8d755cb465afb1119`  
`StaleWhen`: `Start 链、AIBRAIN_ENTRY、RuleIndex 或 AIWarnings 路由协议变化。`

## 保真迁移

原 README 100 行、12,748 UTF-8 字节；现入口 Warning 保留最小读取顺序、上下文边界、目录分层、长期约束和 AIWarnings/AICommands/Skills 协作边界。详细导航解释与约束映射迁移至本条目，README 仍是每个任务的人工起点。

## 启动协议

最小链路为：README → CurrentStatus（短活跃索引）→ RuleIndex → 命中的 P0 原文 → 任务专项原文 → 必要时直接关联的 Handover/Archive → 当前源码与工作树 → 风险匹配的编译、Unity 或 Player 验证。命中项目 Skill 时读取对应 `SKILL.md`；Skill 只提供工作流，不扩大用户或 AICommand 授权。

禁止把开始任务解释为递归加载全部 95 份 Warning。按 RuleIndex、明确 AICommand 或任务命中关系分批读取；摘要、搜索片段、Catalog 或其他 AI 转述不能替代 P0、现行状态和专项原文。`CurrentStatus` 不记录编译日志、Console、错误码或 Warning 数量；Handover/Archive 默认不全量加载。

## 目录与权威

`10_P0最高约束` 是最高优先级长期约束；`20_架构现状` 保存职责边界；`30_运行时专项`、`40_编辑器与工具` 按任务读取；`50_验证与发布` 是验收必读；`80_交接与复盘` 仅历史参考；`90_提案与废止` 不是现行事实。`AIWarningsRouteCatalog.json` 只是机器路由投影，`reserved` 路由不表示模块、API 或授权已存在。

## 长期边界索引

- 文本严格 UTF-8；RuntimeKey 不持久化；Tag 使用 ESTagCollection 的 Host/Lease 所有权；资源寻址以 Manifest/Table/Bundle Index 为准。
- 资源生命周期区分 Resident、Owner Scope、ResourcePlan、Temporary 引用和独立 Lease；普通任务只释放自己的 Lease。GameManager Module 优先 `TryGetModule<T>()`。
- GameCore 不反向引用 Prefab/GameObject/Scene；编辑器初始化优先 AssemblyStream，禁止域重载全盘扫描；热路径须先做 Prepare、静态门禁和分配清单，无 Profiler 不宣称 0 GC。
- 测试场景导视复用 ESSceneValidationGuide，官方构建器是场景布局权威；Pool 遵守 IESGameObjectPoolLifecycle，ContextPool 不替代跨对象 Lease。
- ESCommand PlayerRunner、Stable Graph V2、模块成熟度、菜单根和 AI 协作历程各有独立规则，不能用相邻文档或摘要替代。AI 协作历程只有用户明确要求时维护。

## 协作分工

AIWarnings 保存长期事实、边界、禁止事项和验收规则；AICommands 保存受管任务协议而非用户授权；Agent Skills 保存可复用工作流；AIKnowledge 负责导航和可回溯 SourceRefs；AITalk 只记录过程共识。维护入口时保留状态、StableId、Authority、RouteKeys 和 EvidenceRef，冲突以 P0、当前源码和最新验收证据为准。

## EvidenceRefs

- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`
- `Assets/Plugins/ES/AICommands/README.md`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`c1fc2f3dd03713d0bedf4c12c4e95190613033af55cc28eb79b075976501c31b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md` (`8e3f621daa078c047311f28dede7e839aae4fd34d3062a259561604fdbd2f2f4`)
- `Assets/Plugins/ES/AICommands/README.md` (`4af02fd8d89c7e85191027262afb869a6bb1e8e3ca4a362f571758a68a24e651`)
