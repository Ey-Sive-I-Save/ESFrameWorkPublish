# 编辑器交付体验与下一步可发现性：保真 Knowledge

`KnowledgeId`: `es.aiwarning.p0.editor-delivery-next-step-discoverability.v1`  
`Authority`: `AIWarnings` 与当前 Editor/UI/产物交付合同  
`RouteKeys`: `aiwarnings`, `p0`, `editor`, `delivery`, `discoverability`, `next-step`, `recovery`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `ed295037c2083fae5eb691bb88e5eac8463713d196e06ab6950e784b68541a03`  
`SourceSetHash`: `ed295037c2083fae5eb691bb88e5eac8463713d196e06ab6950e784b68541a03`  
`EntryBodyHash`: `8e0eb90bebde860c5e5ae251755ba43f4b351936e12c724e32f5d8a0c16ccc3d`  
`StaleWhen`: UI 入口、产物路径策略、交互状态、恢复协议或任一 SourceRef 哈希变化。

## 迁移范围

Warning 保留五阶段交互、下一步入口、路径安全和恢复 P0；本条目承载详细状态、信息密度、无 UI 交付和验收细则。Knowledge 不授予 UI、文件或外部程序操作权限。

## 五阶段交互合同

配置阶段要有安全可解释默认值、高风险影响/权限/回滚说明、就近入口和最小补全动作；使用阶段支持目标/名称/搜索/结果进入、快捷动作、危险操作确认或 dry-run、跳步/返回/取消/重试，以及空/等待/失败状态。查询阶段支持稳定 ID、名称、路径或任务 ID，区分主结果、日志、证据、临时文件和失败详情，并说明搜索范围与下一种查询方式。

交付阶段的主产物必须提供快速打开、项目窗口定位、打开报告、复制路径或等价动作；CI/无 UI/安全敏感产物可提供稳定路径、机器索引或受控访问。恢复阶段覆盖窗口重开、域重载、编辑器重启和任务中断，并显示状态过期、依赖漂移和最小修复动作。

## 信息密度与路径安全

首屏按“标题/状态 → 关键结论 → 操作入口 → 证据/详情”排列，日志、长 JSON、堆栈、哈希和 SessionId 默认折叠但可完整复制；表格列名/单位/状态/排序明确，不只靠颜色。窄屏、高 DPI、不滚动首屏和无横向滚动仍保持主路径。路径必须与文件名、任务/报告 ID、窗口标题对应；打开/定位只允许任务声明输出根或明确项目内安全路径，禁止任意路径、凭据、系统目录、外网、临时缓存或不稳定时间戳作为唯一入口。

失败、部分成功、待输入、权限不足和不可用状态必须包含原因、影响、当前状态和恢复动作；禁止只在 Console/聊天/日志打印路径，或用自动弹窗、抢焦点、外部程序和作者内部目录替代入口。纯后台协议可用机器索引，但其展示层仍遵守本合同。

## 验收

验证五阶段均有状态/下一步；首屏、详情、长文本、窄屏和高 DPI 可读；主产物/报告/证据有打开或定位入口且经过路径策略；常用目标支持快速定位、跳步、返回、取消、重试；域重载、窗口重开、重复执行仍指向当前结果；记录窗口尺寸、缩放、状态场景和截图证据。Unity 视觉与交互运行证据本轮未执行。

## 原文快照

迁移前台账快照：123 行、8795 字节，原始 SHA-256 `a8f5938cafe174b2a9052136decb4e660100b3e04819f48da5749bc4cd72bcbe`。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）/项目最高警告_P0_编辑器交付体验与下一步可发现性_AI协作警告.md` (`7d08260bc02b1839a812196e0f108fb13476dcd3d2b03f2b4f7b5972d52d623f`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`6421aa7250ffc316d45d0f3fafd773c9ec62cf2b6b36bf4ccdeed9aed8ab8a8c`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-p0-editor-delivery-next-step-discoverability.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）/项目最高警告_P0_编辑器交付体验与下一步可发现性_AI协作警告.md`
