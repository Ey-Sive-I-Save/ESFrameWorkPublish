# AIWarnings / AIKnowledge 日粒度新鲜度筛查

`KnowledgeId`: `es.knowledge.freshness.v1`  
`Authority`: `AIKnowledge freshness contract and deterministic local scripts`  
`RouteKeys`: `knowledge`, `freshness`, `last-modified`, `stale`, `source-ref`, `content-hash`, `aiwarnings`, `screening`  
`HashSchema`: `v2`  
`ContentHash`: `aa8715bc60cf3a29a3ca52810e560b94dcae979c6fd848aa3d4b060eb541ff93`  
`SourceSetHash`: `aa8715bc60cf3a29a3ca52810e560b94dcae979c6fd848aa3d4b060eb541ff93`  
`EntryBodyHash`: `568c4ef348100f651f8aaab29dfc5507159c9170e003e1ed5d60315ad1bf59e4`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`StaleWhen`: 新鲜度合同、快照 schema、刷新/筛查脚本、KnowledgeIndex、AIWarnings Start 链或任一 SourceRef 哈希变化。

## 机制

`Documentation/AIKnowledge/AIKnowledgeFreshness.json` 是集中式、可重放的文件快照，覆盖 AIWarnings Markdown、AIKnowledge 条目及 Knowledge/AIBrain 导航文件。每项保存原始字节 SHA-256 与日粒度 `lastModifiedDate`；首次建立时的文件系统日期仅是 `filesystem-bootstrap` 估计，不能解释为已完成语义审查。

刷新器在新增文件或内容哈希变化时将日期推进到 `asOfDate`，哈希未变时保留既有日期，因此重复运行幂等。筛查器按 `ageDays = asOfDate - lastModifiedDate` 计算，只有 `ageDays > 7` 才标记 `stale`；当前哈希不匹配快照时标记 `drift` 并退出结构校验失败。

## 使用边界

- `stale` 是优先回读信号，不是自动删除、降权或事实否定。
- `drift` 要求刷新快照并重新运行 Knowledge Validator；不能沿用旧 SourceRef 或旧计划。
- 日期快照不替代 `StaleWhen`、Knowledge `ContentHash`/`EntryBodyHash`、AIWarnings P0、Unity/Player/Profiler/IL2CPP 或发布证据。
- `LastReviewedAt`（语义审查日）若未来引入，必须由真实审查动作记录，不能由刷新器批量伪造。

## SourceRefs

- `Documentation/AIKnowledge/AIKnowledgeFreshnessContract.md` (`3edd33528f2b1e7073ae8c3c67d730fcc631ddc1984b5d12137a858314f1a5ac`)
- `Documentation/AIKnowledge/README.md` (`cad0601de945374689dbcbceb88f180042f00317373d985e00f8953f116e1bfc`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `.agents/skills/es-ai-knowledge-curation/scripts/Update-ESAIKnowledgeFreshness.ps1` (`1caad0eddad46ca6fec4003c6fcfa270b8b31ce68df2ca648496e936a6d084b4`)
- `.agents/skills/es-ai-knowledge-curation/scripts/Test-ESAIKnowledgeFreshness.ps1` (`c956cbf2590531ab4b47c3c1a9470d06f37458592859031dbb8b7aeced2fd9c5`)

## EvidenceRefs

- `.agents/skills/es-ai-knowledge-curation/scripts/Update-ESAIKnowledgeFreshness.ps1 -ProjectRoot . -AsOfDate 2026-08-27`
- `.agents/skills/es-ai-knowledge-curation/scripts/Test-ESAIKnowledgeFreshness.ps1 -ProjectRoot . -AsOfDate 2026-08-27`
- `runtime-not-run`
