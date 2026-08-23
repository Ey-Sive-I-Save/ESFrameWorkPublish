# Knowledge 条目合同

每个条目必须能回答：它是什么、来自哪里、何时可信、何时失效、哪些 Skill 会引用它。

必备字段：

- `KnowledgeId`：稳定身份。
- `Authority`：Source、AIWarnings、AICommand、Evidence 或 Derived。
- `RouteKeys`：AIBrain 检索键。
- `RequiredReads`、`RelatedSkills`：最小读取和工作流关联。
- `SourceRefs`、`EvidenceRefs`、`ContentHash`：可回读和校验。
- `EvidenceLevel`、`StaleWhen`：证据层级和失效条件。

摘要不能提高证据等级。外部缓存、zread 和 Feishu 内容默认只能标记为 Derived/ExternalCache。
