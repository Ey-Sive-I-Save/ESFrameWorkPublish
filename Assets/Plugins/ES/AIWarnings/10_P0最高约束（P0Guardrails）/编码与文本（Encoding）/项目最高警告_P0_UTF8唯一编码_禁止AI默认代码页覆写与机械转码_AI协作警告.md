# P0：UTF-8 唯一编码边界

`StableId`: `es.aiwarning.p0.utf8-encoding-boundary.v1`
`Status`: `current`
`Authority`: `AIWarnings`
`RouteKeys`: `aiwarnings`, `p0`, `utf8`, `encoding`, `text-integrity`, `evidence-boundary`
`Applicability`: 项目内所有源码、配置、文档、Shader、JSON、YAML、CSV 及其他文本；所有 AI、自动化脚本和人工批处理。
`EvidenceRef`: `.agents/skills/es-utf8-guard/scripts/Test-ESUtf8.ps1`；`git diff --check`；`Documentation/AIKnowledge/entries/aiwarning-p0-utf8-encoding-boundary.md`
`Owner`: ESFramework 文本完整性维护者
`StaleWhen`: UTF-8 规则、PowerShell 写入策略、验证脚本或本条目 SourceRefs 变化。

## 长期约束（P0）

- 所有文本必须保持严格 UTF-8；乱码是数据损坏，不是格式问题。
- 违反时停止当前修改、保留现场并优先恢复；不得用未经验证的整文件转码掩盖损坏。
- 禁止默认代码页、ANSI、GBK、`-Encoding Default`、隐式区域设置写入，禁止 `Get-Content | Set-Content` 直接覆写。
- 禁止把乱码扩散到源码、注释、日志或文档；无法确认原文时不得猜测或批量替换。
- 修改优先使用 `apply_patch`；其他写入方式须先确认 UTF-8 行为；不得为“统一编码”无差别重写目录。

## 证据与验收边界

修改后必须严格 UTF-8 解码、扫描 `U+FFFD`/疑似乱码、执行 `git diff --check` 并检查目标 diff；必要时再做 Unity/C# 验证。静态通过不代表 Unity、Runtime、Profiler、Player、IL2CPP 或发布通过。

详细禁令、恢复步骤、示例、历史原文和完整验收清单见 Knowledge：`es.aiwarning.p0.utf8-encoding-boundary.v1`（保真快照与 SourceRefs）。
