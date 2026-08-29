# 生成产物快速打开入口：保真 Knowledge

`KnowledgeId`: `es.aiwarning.editor-generated-artifact-quick-open.v1`  
`Authority`: `AIWarnings` 与当前 Editor/AI/Worker 交付合同  
`RouteKeys`: `aiwarnings`, `editor`, `delivery`, `artifact`, `quick-open`, `path-safety`, `recovery`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `a009664fe9780ed1ddd791c60ee04374dffa1755ef01509cab9de6d72e95c040`  
`SourceSetHash`: `a009664fe9780ed1ddd791c60ee04374dffa1755ef01509cab9de6d72e95c040`  
`EntryBodyHash`: `feb148adbfdc49ac2c7ecf6fe6c65f74c78f2561c40a4ae22e9004aa5fefbc13`  
`StaleWhen`: 产物交付入口、路径策略、宿主能力或任一 SourceRef 哈希变化。

## 迁移范围

Warning 保留快速打开、路径安全和失败下一步的最小契约；本条目承载不同宿主的交付例外、状态表达和验收细节。Knowledge 不授予文件、外部程序或系统目录访问权限。

## 交付合同

AI、C#、Python、PowerShell、Worker 或 Unity 工具生成报告、日志、配置、快照、审计状态、交接文案等用户需继续处理的文件，交付必须同时说明产物名称/用途、稳定项目相对路径或受控绝对路径、`快速打开`/`在项目窗口定位`/`打开报告`/`复制路径` 至少一个直接入口，以及失败或部分成功的下一步。只打印 Console、聊天或日志路径不算完成。

快速打开只能访问本次任务声明的输出根或明确安全的项目内路径，不得访问任意系统目录、凭据、外部网络、未声明路径或绕过 ES 路径策略；路径、文件名、任务/报告 ID 应能相互对应。不得用临时目录或不稳定时间戳路径作为唯一入口。

无 UI、CI 或安全敏感宿主可用稳定路径、机器可读索引或受控访问替代按钮；若当前宿主确实无法提供按钮，必须明确写“快速打开入口不可用”，给出完整可复制路径和最短手工动作，不能假装已提供入口。失败、部分成功、等待输入和权限不足必须显示状态、原因、影响范围和恢复动作。

## 与 P0 交付原则的关系

本条目是编辑器交付体验 P0 的具体落地，不取代五阶段状态/下一步、恢复、首屏可读性、长文本复制和窄屏布局要求。验收应检查主产物/报告/证据均可打开或定位、入口经过路径策略、重复执行/域重载/窗口重开仍指向当前结果，并保留宿主、状态场景和路径证据；本轮未运行 Unity 视觉或交互验证。

## 原文快照

迁移前台账快照：17 行、1161 字节，原始 SHA-256 `956af92e6fd746c5da99fc66e53f9681a8b061f6bacddb9bc559b84f31f9d20a`。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/生成产物快速打开入口_AI协作警告.md` (`d480f3019fe6944fadc580adfa0c79cb9dfbbf26d5e798aecbd8c2a639682140`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）/项目最高警告_P0_编辑器交付体验与下一步可发现性_AI协作警告.md` (`7d08260bc02b1839a812196e0f108fb13476dcd3d2b03f2b4f7b5972d52d623f`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`1b1868dc4834a1ea76c28cbd9b786ce26446a4177ea4d0c73d5ccb66bda69430`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-editor-generated-artifact-quick-open.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/生成产物快速打开入口_AI协作警告.md`
