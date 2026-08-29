# ESAdvancedDialog 通用编辑器输入边界：保真 Knowledge
`KnowledgeId`: `es.aiwarning.editor.es-advanced-dialog-input-boundary.v1`  
`Authority`: `AIWarnings` 与当前 ESAdvancedDialog Editor 实现  
`RouteKeys`: `aiwarnings`, `editor`, `esadvanceddialog`, `input`, `authorization`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `eca732c5c2e932adba3b7b0d2db255a6d16e34b757549e7c90c55b51d1b3e64b`  
`SourceSetHash`: `eca732c5c2e932adba3b7b0d2db255a6d16e34b757549e7c90c55b51d1b3e64b`  
`EntryBodyHash`: `971ee751aa126b28f27cdb99fc1f5277864a7008d95587bf2694053d9b47d342`  
`StaleWhen`: ESAdvancedDialog 实现或任一 SourceRef 哈希变化。

## 迁移范围
Warning 保留 UI 外壳无业务权限、稳定选项、进程/资产/机密禁止事项及正式入口授权边界；本条目承载可提供的交互能力、辅助动作和回调验收细节。Knowledge 不授予执行权限。

## 能力与选项
`ESAdvancedDialog` 可承载结构化字段、稳定选项、自定义 `VisualElement`、同步/异步校验、辅助动作、进度/取消反馈、重复窗口策略、定位模式及确认/取消回传，但这些 UI 能力不能授权业务。复制文本、切换说明、预览等不改变权威数据的动作可直接放入窗口。跨语言或持久化选项必须使用稳定 OptionId；`AddChoiceOptions` 返回稳定 ID，旧 `AddChoice` 只适用于显示值即业务值。

## 权限与生命周期边界
`AddAuxiliaryAction`/`AddAuxiliaryActionAsync` 若修改资产、设置、发布物或外部状态，调用方必须先做权限、目标和前置检查，再进入正式 C# Editor 入口。对话框不得自动启动 Python、PowerShell、CLI 或任意进程，不得绕过入口读写 Unity Assets、发布物、设置或凭据，不因确认/辅助按钮授予删除、上传、发布能力，不接收密码、Token、AK/SK 等机密。窗口存活、进度、取消和异步回调不是后台任务权威生命周期或最终结果。

## 调用方合同
调用方在 `completed` 回调或已声明辅助动作中执行已授权业务；资产修改仍须目标校验、`Undo`、Dirty、保存和失败回滚。自由文本、路径选择、确认、进度或异步回调都不能替代权限校验与完成证据。当前条目状态为源码实现，Unity 收录、编译及 Editor 交互仍待验收。

## 原文快照与证据
迁移前台账：21 行、1983 字节，原始 SHA-256 `322ac01c53b0eeb5d03962a0a8973c24880707bc2a9a81ca806bba378220033c`；本轮未运行 Unity/Runtime。

## SourceRefs
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAdvancedDialog通用编辑器输入边界_AI协作警告.md` (`cb8da4bd465c56291bd88c6ec5477a3252666f7bd0b3bbd219a7326b161d1935`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`90a4eec18e9952e4c13bdf8d6f1ebf3a4a88412f93c7d5403b3925afae1b0e9e`)

## EvidenceRefs
- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads
- `Documentation/AIKnowledge/entries/aiwarning-editor-es-advanced-dialog-input-boundary.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAdvancedDialog通用编辑器输入边界_AI协作警告.md`
