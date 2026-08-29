# 模块成熟度与未完成实现治理

`KnowledgeId`: `es.aiwarning.arch.module-maturity-incomplete-governance.v1`  
`Authority`: `AIWarnings + module status source + evidence contracts`  
`RouteKeys`: `aiwarnings`, `architecture`, `module`, `maturity`, `evidence`, `blocked`, `lifecycle`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `ae9065aec1523a15ec252454a96e716d2395d92250936662b50aa944de4b6487`  
`SourceSetHash`: `ae9065aec1523a15ec252454a96e716d2395d92250936662b50aa944de4b6487`  
`EntryBodyHash`: `e708e0ded1a9aa53abfcf3367b79a1dbc9adae6793fe5cff29a39d3f357a91f3`  
`StaleWhen`: 模块状态入口、证据等级、成熟度合同或续接协议变化。

## 迁移范围

原 Warning 169 行、11,268 UTF-8 字节；现 Warning 保留状态三轴、半成品隔离、唯一状态入口、续接权限和证据分层。本条目承接完整状态枚举、跃迁门禁、审计矩阵、续接检查点、失败模式与验收清单。

## 核心语义

- `ModuleMaturity` 使用 Proposed、Scaffolded、Experimental、Implementing、Integrating、Verifying、Stable、Deprecated、Archived；`EvidenceLevel` 使用 S0-S6；`DeliveryVerdict` 使用 Designed、Implemented-Unverified、Accepted、Released。三者不得压平。
- 状态必须绑定模块唯一权威入口，并记录承诺范围、默认激活、依赖/消费者、当前证据、缺失证据、退出条件和失效条件。提案、空接口、测试替身或 Editor-only 路径不能自动升级。
- 未完成模块不得默认注册、渗透稳定模块或进入发布；必须可编译、可隔离、可失败、可退出。删除/中断需同步清理注册、序列化、配置、资源、菜单、测试和文档引用。
- 审计续接固定写入 `ES/Documentation/Status/MODULE_AUDIT_STATE.md` 对象块，记录 Git 基线、依赖、消费者、渗透、证据和恢复入口；检查点不授予未来写入授权，事实变化后必须 stale。
- 低层证据不能冒充高层证据：源码不是 Unity 编译，`.csproj` 不是 PlayMode，静态检查不是 Profiler、Player、IL2CPP 或发布通过。

## EvidenceRefs

### evidence

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/模块成熟度与未完成实现治理_AI协作警告.md`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/模块成熟度与未完成实现治理_AI协作警告.md` (`b9289ed941f167c16441a89fead23dab77c79e7fd1737c21141f28859b9d8d91`)
- `Assets/Plugins/ES/AICommands/检查_模块成熟度与半成品影响_AI命令.md` (`2293889badef79194853ff5980a434ee07a7c81253db7fd3b6c3785df083dc2d`)
- `.agents/skills/es-module-lifecycle/SKILL.md` (`b2fb86785ee5661f979c020b0b2e95c7a6ad53e4f97c391ea1dd0518ed985f26`)
