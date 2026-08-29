# 预备案提案：UnityMCP、AI 工程验收代理与自动化路线图

`KnowledgeId`: `es.aiwarning.proposal.unitymcp-engineering-agent-roadmap.v1`  
`Authority`: `AIWarnings proposal + project governance route`  
`RouteKeys`: `aiwarnings`, `proposal`, `unitymcp`, `agent`, `validation`, `automation`, `evidence`, `release`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `89637ca312b129e40df43dc26516eb206c19e81a70da9e0be418a59d578c2b88`  
`SourceSetHash`: `89637ca312b129e40df43dc26516eb206c19e81a70da9e0be418a59d578c2b88`  
`EntryBodyHash`: `e198a0438ebbf16203d0e788b21a1fe106b404f06ede0fd723f84fea2a88246b`  
`StaleWhen`: `UnityMCP/AICommands/验收合同、CurrentStatus 或路线图发生变化。`

## 保真迁移

原预备案 116 行、6,474 UTF-8 字节；现 Warning 保留 proposed/未实现声明、授权边界、分层证据边界和禁止事项。候选能力、阶段路线、ES 专项切入点与未来验收要求迁移至本条目，不能把路线图当作交付计划或用户授权。

## 目标链路与候选能力

目标链路：用户目标 → AICommand 权限 → RuleIndex/P0 → Skill 最小上下文 → 安全事务 → Unity 真实证据 → Test Runner/PlayMode/Profiler/Player 分层验收 → 结构化证据包 → 本地台账。候选能力包括 Unity 验收编排、序列化健康审计、任务上下文采集、Prefab 契约、资源发布、性能回归、ReloadDomain 泄漏、语义 Diff、安全回滚和证据追踪图。

## 分阶段约束

- 第一阶段只读采证：上下文、序列化扫描、编译/Console/Test Runner 证据，禁止自动改场景、Prefab、SO 或发布资源。
- 第二阶段受控验收：每项须有明确输入、超时、取消、失败状态和机器可读输出，不能以低层成功替代高层证据。
- 第三阶段高风险自动化：资产写入、长时运行、远端或发布必须有隔离、授权、精确回滚和恢复证据。

## 进入实施与禁止事项

实施前须重读最新入口、P0、源码和匹配 AICommand，并取得本次明确授权；默认只读。不得绕过 AICommand/P0/Undo/工作树/文档门禁，不得递归吞入全库，不得把截图、Console 清洁或一次 PlayMode 写成 Profiler、IL2CPP 或发布通过。每项落地至少需要最小真实任务成功/失败样例、UTF-8/工作树/取消超时验证、Unity 实跑证据和 CurrentStatus 真实登记。

## EvidenceRefs

- `Assets/Plugins/ES/AIWarnings/90_提案与废止（Archive）/待验收提案（Proposals）/UnityMCP_AI工程验收代理与自动化能力路线图_预备案提案.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AICommands/README.md`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/90_提案与废止（Archive）/待验收提案（Proposals）/UnityMCP_AI工程验收代理与自动化能力路线图_预备案提案.md` (`368b625bad7e7dee513f5fedcc1eac22c8bc9b389f953d32492a45b47ad609cb`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Assets/Plugins/ES/AICommands/README.md` (`4af02fd8d89c7e85191027262afb869a6bb1e8e3ca4a362f571758a68a24e651`)
- `.agents/skills/es-aiwarning-authoring/SKILL.md` (`8a7a97afb5b825f450118798d2f2f36b4d27cdfb0912852d27a72ba90707d2b3`)
- `.agents/skills/es-aibrain-route-authoring/SKILL.md` (`823e01fd1e84a7a5a163716bdd4047c9fe5cf63ed479c79debbb56a4f6ebc378`)
