# 核心热路径缺失依赖与判空边界：保真 Knowledge
`KnowledgeId`: `es.aiwarning.p0.hotpath-missing-dependency-null-check.v1`  
`Authority`: `AIWarnings` 与当前运行时实现  
`RouteKeys`: `aiwarnings`, `p0`, `runtime-performance`, `hotpath`, `dependency`, `null-check`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `8137f49c8fa013e09b65a452d0303a7648a640fdee14e0940b93f3b10922399e`  
`SourceSetHash`: `8137f49c8fa013e09b65a452d0303a7648a640fdee14e0940b93f3b10922399e`  
`EntryBodyHash`: `677a70c716e2375718dac0ef3d5657d2bfe4e9e79604aca8ccfbad1980a5647c`  
`StaleWhen`: 核心依赖初始化、预热、配置验证或任一 SourceRef 变化。

## 迁移范围
Warning 只保留长期性能与错误暴露边界；本条目承载适用系统、判断标准、初始化处理和禁止误操作。Knowledge 不授权绕过初始化验证，也不授权删除任意判空。

## 核心原则
对缺失后必然无法正确推进的核心依赖，应在初始化、绑定、预热或配置验证阶段保证存在，并以断言、错误日志、初始化失败返回或编辑器验证暴露问题。初始化成功后，正式热路径可以信任该结果，不应把初始化错误转嫁给每帧 `Update`、KCC 回调、IK 求解或 StateMachine Evaluate。

## 依赖分类与处理
- 核心依赖：缺失意味着系统必然不能正确运行；初始化阶段严格验证，热路径不重复判空。
- 可选能力：缺失或未启用是正常业务状态；在任务入口做轻量判断并快速返回。
- 诊断、日志和编辑器监控不能污染正式运行热路径。

## 禁止误操作
不要在高频回调中重复核心判空、每帧链式查找可缓存对象，或以“安全”为名让依赖缺失的系统半死不活地继续运行。热路径不得引入 LINQ、反射、字符串拼接、临时集合或 Unity 查找。看到判空不能盲删，必须先确认它保护的是核心依赖还是可选能力。

## 结论
ESFramework 的性能口径是：初始化严格，热路径信任初始化结果；可选能力快速失败，核心缺失直接暴露；不得把防御式编程误用成每帧判空、查找或分配。

## 原文快照
迁移前原始文件为 47 行、2107 UTF-8 字节，原始 SHA-256 为 `02ba0a6d00e9a15ac0d6d4aec2c4689d5652ad284f169742b8c904e1c552440b`。本条目保留其全部语义；未运行 Unity/Runtime。

## SourceRefs
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_核心热路径缺失依赖不判空_AI协作警告.md` (`ee23d930bb006f56c6c6517072e556f2f9942368bd5dd8e235c65bb00d390b9a`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`0ac8415c00f6695a8c8ed48386d40fd8b6203e3c55b6c1650868957152806b82`)

## EvidenceRefs
- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads
- `Documentation/AIKnowledge/entries/aiwarning-p0-hotpath-missing-dependency-null-check.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_核心热路径缺失依赖不判空_AI协作警告.md`
