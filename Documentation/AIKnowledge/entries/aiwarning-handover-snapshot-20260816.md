# 2026-08-16 AIWarnings 历史交接快照

`KnowledgeId`: `es.aiwarning.handover.snapshot.20260816.v1`  
`Authority`: `AIWarnings historical handover snapshot + current routing rules`  
`RouteKeys`: `aiwarnings`, `handover`, `historical`, `editor`, `shader`, `graph`, `entity`, `resource`, `camera`, `evidence`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `1d0fdd9b3d73017d64baccc50d60e4a52464c1da245fc7c4428da2b5bc140a1e`  
`SourceSetHash`: `1d0fdd9b3d73017d64baccc50d60e4a52464c1da245fc7c4428da2b5bc140a1e`  
`EntryBodyHash`: `308ab36bc47d26bdac2a1ecfe2c5c2f943ec5d59d403e1e421f869b10e1ac6e5`  
`StaleWhen`: `当前源码、CurrentStatus、专项规则或后续验收证据变化。`

## 快照定位与保真

原历史快照 157 行、34,480 UTF-8 字节；现交接 Warning 保留历史身份、日期、用途、非授权边界和关键领域状态。详细日期段落、源码入口、编译记录、证据矩阵和人工验收清单由本条目承接；它们只描述当时观察，不能覆盖当前事实。

## 领域快照摘要

- Editor 窗口基础层：显式 owner/ownerKey、半休眠形态和四层动作宿主；资产包窗口已接入但仍待实机验收。
- Composite Shader/材质：四类 URP 职责分离，Renderer 参数用 MaterialPropertyBlock，UI 使用受管理材质实例；状态为 `Implemented-Unverified`。
- API/菜单治理：命名分级、六域菜单迁移和旧路径台账已记录；静态计数与局部编译不能替代 Unity 菜单/视觉验收。
- VFX、AssetPackage、UGC Workbench、Stable Graph V2/AISkill：源码骨架或局部接入存在，分别受正式输出、贡献注册、Undo/草稿、真实执行和发布证据约束，不能标 Stable。
- Entity/装备/挂点、Camera、ResourcePlan/Scope：静态链路和定向源码证据存在，但 PlayMode、Provider 重建、IL2CPP、Profiler 或发布证据按领域仍有缺口。
- 玩家控制与手感：KCC/输入/挂点链已收口为当前方向；T90、松手停止、反向完成时间和多帧率 Profiler 仍未验收。

## 证据与交接规则

快照中的 dotnet build、Unity 导入、测试源码、静态门禁和旧错误结论均只能按其日期和范围解释；不能升级为当前 Runtime/Release 通过。交接、历史恢复和台账维护必须遵循当前 Handover P0 与用户授权，普通任务不得自动写入 AI 历程、审计状态或发布状态。冲突以当前源码、P0 和最新真实回执为准。

## EvidenceRefs

- `Assets/Plugins/ES/AIWarnings/80_交接与复盘（Handover）/历史上下文（HistoricalContext）/2026-08-16_当前状态快照_活跃索引迁移前.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/80_交接与复盘（Handover）/历史上下文（HistoricalContext）/2026-08-16_当前状态快照_活跃索引迁移前.md` (`3c5ae1c332ea7bcd148f4eb65cc033a97408304938f23072dfc725929f31c755`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`2aa56abe81352fd79ad59b1364ffa7381d70b26674a1676b8439173a515d9b6c`)
