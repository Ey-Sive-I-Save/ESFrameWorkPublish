# AI 交付声明与责任契约：保真 Knowledge

`KnowledgeId`: `es.aiwarning.p0.ai-delivery-contract.v1`  
`Authority`: `AIWarnings` 原文与当前交付证据规则  
`RouteKeys`: `aiwarnings`, `p0`, `delivery-evidence`, `acceptance`, `disclosure`  
`HashSchema`: `v2`  
`ContentHash`: `1563a304e0aa5f8c0d1c29efda58831d0e14909a4d4de09a43870e200e5d498e`  
`SourceSetHash`: `1563a304e0aa5f8c0d1c29efda58831d0e14909a4d4de09a43870e200e5d498e`  
`EntryBodyHash`: `a6439b7a279ea2c846bec30c9d3f5a79c302cc84cb63452eba0439c661030809`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`StaleWhen`: Warning、证据等级、交付状态、验证合同或任一 SourceRef 哈希变化。

## 迁移说明

Warning 本体保留长期证据边界、权限边界和主动披露要求；本条目承载详细证据等级、报告合同、性能附加合同、证据替换禁令、状态语义、正式资产门禁以及迁移前原文快照。Knowledge 不授予 Runtime、发布、Git 或其他执行权限。

## 详细规则

### 证据等级

| 等级 | 允许声明 | 禁止越级 |
|---|---|---|
| S0 设计 | 方案、约束、计划 | 已实现、可用、已验收 |
| S1 源码 | 源码修改、路径和入口存在 | 编译、Unity、Runtime 可用 |
| S2 静态编译 | 指定 csproj/静态检查通过 | Unity 编译、域重载、Runtime |
| S3 Unity 导入 | 导入、域重载、Console 通过 | 真实交互、PlayMode、发布 |
| S4 编辑器交互 | 指定窗口和操作真实完成 | PlayMode、Player、性能、发布 |
| S5 运行时 | EditMode/PlayMode/运行观察 | Player、IL2CPP、发布 |
| S6 发布 | Player/IL2CPP、资源、性能和发布链路按范围通过 | 超出范围的商业级/全平台结论 |

### 强制交付报告

任何完成类结论都必须写明：目标、实际修改、当前等级、逐项已验证、未验证、阻断原因、影响范围和最小下一步；未验证项为空也要显式写“无”。

### 性能附加合同

涉及排序、筛选、批处理、缓存、容器复用或低 GC/0 GC 时，必须说明结果身份、是否原地修改、所有权、首次/稳态/扩容/异常分配阶段、并发/重入边界和源码/分配计数/Profiler/Player/目标平台证据。仅编译或没有显式 `new` 不足以证明 0 GC。

### 证据替换禁令

ES 作者态资产不等于 Unity 正式资产；PreviewScene 不等于正式 Scene/Prefab；Heightfield 不等于 TerrainData；`.csproj` 不等于 Unity 编译；按钮可见不等于交互闭环；代码路径不等于 Runtime、碰撞、导航、资源收集或发布；单元测试不等于真实 Unity、PlayMode、Profiler 或 Player。临时对象、缓存、截图、日志和模拟数据只证明自身范围。

### 主动披露与状态

目标未完全达成时必须说明尚未完成的责任、原因、实际影响和所需条件；禁止“基本完成”“应该可以”等模糊措辞。交付状态使用 `Designed`、`Implemented-Unverified`、`Blocked`、`Failed`、`Accepted`、`Released`；不得将未验证状态压平为完成。模块成熟度另用 Proposed 至 Archived 序列，不能与 S0–S6 或交付状态混淆。

### AIWarnings 与正式资产边界

AIWarnings 不是编译日志或 Warning 台账，只保存长期规则、证据等级、适用范围、验证入口和可复用失败机制；不复制瞬时 Console、错误码或 Warning 数量。地图、地形、场景和资源声明必须分别确认作者态、数据源、正式 Unity 资产、Scene/Prefab、Runtime/碰撞/导航/发布产物，并写入后重读目标对象。

## 原文快照

迁移前 Warning 的完整 126 行、6864 字节内容由以下不可变 SourceRef 指向；本条目保留其全部规则语义，上述章节是按原章节拆出的可路由版本：

`Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md`（迁移前 SHA-256：`d8404c32f25ea889401f0f8c63a969d8fb7e377533200d0d92a8b269d43c2629`）

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md` (`a6e1424e0d2f4ece7c51869f7cf8e41c5d6e5e9ef5f37a26ccdf258229c0de42`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`cdb18529048494da609a69a14275d133b33352412a0265e68ecbbc612b49516e`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-p0-ai-delivery-contract.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
