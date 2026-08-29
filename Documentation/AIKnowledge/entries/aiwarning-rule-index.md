# AIWarnings 规则索引路由合同

`KnowledgeId`: `es.aiwarning.rule-index.v1`  
`Authority`: `AIWarnings routing index + CurrentStatus/RouteCatalog`  
`RouteKeys`: `aiwarnings`, `rule-index`, `p0`, `architecture`, `runtime`, `editor`, `validation`, `handover`, `skills`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `4a2be6cf7db6d96da7962f4bee1cbc18bc0b3a29ea12b01b430d2544983abea3`  
`SourceSetHash`: `4a2be6cf7db6d96da7962f4bee1cbc18bc0b3a29ea12b01b430d2544983abea3`  
`EntryBodyHash`: `f783c373be39bc012db1d8cc99c536c065af0bed6eabc2b0a21f38844c1e59ec`  
`StaleWhen`: `AIWarnings 文件、RouteCatalog、P0/专项目录或 Skill 路由变化。`

## 保真迁移

原 RuleIndex 106 行、28,877 UTF-8 字节；现路由 Warning 保留加载规则、路由状态、主要分组、特殊入口、Skill 快速路由和禁止递归加载边界。逐项任务→必读路径矩阵与预留路由细节迁移至本条目；RuleIndex 仍是人工命中入口，不能由 Knowledge 摘要取代。

## 加载合同

所有文本/源码修改先读 UTF-8 P0，再按任务命中读取 P0 与专项原文；多领域任务分批读取。Handover 只在决策背景、失败复盘或交接需要时读取，Archive 只在提案/废止评审时读取。上下文预算是建议，不得跳过命中的 P0、CurrentStatus 或专项原文。

## 路由矩阵

- GameCore/Identity/内容注册：GameCore、ConfigKey、RuntimeData、稳定 Key、事务、Info/Group/Pack。
- 运行时资源/性能/战斗：ResourcePlan、Scope、Manifest、Provider、Pool、Item、Shot、Tag、Buff、State/IK、Input、Audio、VFX、Profile。
- 编辑器/工具/资产：EditorLifecycle、菜单/窗口、SimpleTools、SO Table、Shader/Material、Workbench、AssetPackage、Graph V2、序列化和第三方包。
- 验证/交付/协作：实际可玩闭环、AI 交付声明、Runtime/Profiler/Player/IL2CPP/Release、AICommand/Skill、模块成熟度、测试场景和 Codex 会话。

实施、审计、交接、运行或发布任务必须同时回读对应 P0、专项原文、当前源码和匹配 Skill；无匹配 AICommand 标记 `NoMatchingCommand`，不得套用无关合同。

## 预留与机器投影

普通路由为 `current`；`reserved` 只定义未来必读边界，不代表模块、API、AICommand、默认注册或授权存在。`AIWarningsRouteCatalog.json` 是机器投影，Markdown RuleIndex 和命中的 Warning 原文仍是人工权威。

## Skill 路由

GameCore→`es-gamecore-integration`；资源→`es-resource-pipeline`；Tag→`es-tag-config`；Entity→`es-entity-authoring`；Input→`es-input-action`；Command→`es-command-authoring`；Editor→`es-editor-tooling`；Unity/Release→`es-release-acceptance`；模块审计→`es-module-lifecycle`；Codex→`es-codex-session-bootstrap`。Skill 只提供工作流，不扩大授权。

## EvidenceRefs

- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/AIWarningsRouteCatalog.json`
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`
- `Assets/Plugins/ES/AICommands/README.md`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`2aa56abe81352fd79ad59b1364ffa7381d70b26674a1676b8439173a515d9b6c`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0fc33af09f1343bee0d2cfb19abb1034d9d906e4f80ccd9695eab597c6856ebe`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/AIWarningsRouteCatalog.json` (`f340924035d800f3b485a75f868ed9184bbe00634cb624e2d09f986536ae12d3`)
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md` (`8e3f621daa078c047311f28dede7e839aae4fd34d3062a259561604fdbd2f2f4`)
- `Assets/Plugins/ES/AICommands/README.md` (`4af02fd8d89c7e85191027262afb869a6bb1e8e3ca4a362f571758a68a24e651`)
