# AIWarnings / AIKnowledge 新鲜度记录合同

> 状态：现行治理合同。它只负责内容新鲜度筛查，不把日期或筛查结果升级为 AIWarnings、源码、Unity 或发布证据。

## 目标

`AIKnowledgeFreshness.json` 是 AIWarnings 与 AIKnowledge 的集中式、可重放快照。它为每个受管文件保存日粒度 `lastModifiedDate` 与内容哈希，供后续 AI 库筛查快速发现可能失真的条目。

## 字段语义

- `asOfDate`：本次快照/筛查的 UTC 日，格式固定为 `yyyy-MM-dd`。
- `lastModifiedDate`：由内容哈希变化观察得到的日期；首次建立快照时使用文件系统 UTC 修改日作为 `filesystem-bootstrap` 估计值，不能解释为语义审查完成。
- `lastReviewedAt`：语义审查日期（如未来需要）应单独记录；本合同不自动生成或伪造该字段。
- `generatedAtUtc`：仅表示快照文件生成时间，不参与内容新鲜度判断。
- `contentHash`：当前文件原始字节的 SHA-256，小写；用于检测漂移，不替代 Knowledge 条目的 SourceRef/ContentHash 合同。

## 失真判定

默认 `staleAfterDays: 7`，按 `ageDays = asOfDate - lastModifiedDate` 计算；只有 `ageDays > 7` 才标记 `stale`，第 7 天仍属于当前窗口。当前文件哈希与快照不一致时标记 `drift`，必须先刷新快照并重新核对来源，不能直接把旧日期当作新鲜。

## 范围

快照覆盖：

- `Assets/Plugins/ES/AIWarnings/**/*.md`
- `Documentation/AIKnowledge/entries/**/*.md`
- `Documentation/AIKnowledge/README.md`
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`
- `Documentation/AIKnowledge/KnowledgeIndex.yaml`
- `Documentation/AIKnowledge/AIWarningsDomainInventory.yaml`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/AIWarningsRouteCatalog.json`

生成型 `AIWarningsGeneratedInventory.json` 不纳入范围，因为它包含每次生成都会变化的 `generatedAtUtc`；它仍由自身脚本和 SourceRef/ContentHash 合同治理。

## 操作边界

```powershell
# 在明确的业务日期建立或刷新快照（写入唯一快照文件）
.agents/skills/es-ai-knowledge-curation/scripts/Update-ESAIKnowledgeFreshness.ps1 `
  -ProjectRoot . -AsOfDate 2026-08-27

# 只读筛查；stale 是可报告的关注项，drift/missing 才使结构校验失败
.agents/skills/es-ai-knowledge-curation/scripts/Test-ESAIKnowledgeFreshness.ps1 `
  -ProjectRoot . -AsOfDate 2026-08-27
```

刷新器只在新增文件或内容哈希变化时推进日期，哈希不变时保留原日期；重复运行幂等。筛查器不写文件、不启动 Unity/Runtime、不修改 Knowledge 条目正文或其 SourceRef 哈希。

## 非声明

该合同不能证明条目事实仍正确、语义审查已完成、Unity/Player/Profiler/IL2CPP/发布行为可用，也不能代替 `StaleWhen`、Knowledge Validator 或 AIWarnings P0。日期快照只提供“应优先回读”的筛选信号。
