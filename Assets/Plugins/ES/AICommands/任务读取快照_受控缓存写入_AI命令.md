# 任务读取快照：受控缓存写入 AI 命令

本命令只允许为当前 TaskContract 建立或更新任务读取快照和投影缓存。它不授权修改 Assets、源代码、Git、Unity 状态、外部网络或发布状态。

命令类型：安全执行。
默认改文件：是，仅允许 `ES/Output/TaskReadSnapshots/`、`ES/Output/FileProjectionCache/` 下与当前 TaskId 和内容 Hash 匹配的清单、投影及临时文件。
风险等级：L2。

## 必须先读

执行前必须读取本文件全文、`.agents/skills/es-task-read-snapshot/SKILL.md`、`references/task-read-snapshot-contract.md`，取得当前任务绑定的 AIBrain PlanHash、TaskContract、输入 ReadSet、预算和停止条件。

必须先 Build/Verify 快照，再按源文件 Hash、Parser 版本和 Projection Hash 命中缓存。源漂移、路径越界、重复路径、超限、损坏或 Hash 不匹配必须返回 blocked；不得刷新缓存后继续假装命中。

严禁把本命令当作源文件写入、命令执行、Unity/MCP 授权或 Runtime 证据。所有写入必须可由 receipt 逐项对账并支持幂等重试。

## 交付格式

返回 TaskId、快照 Hash、缓存命中/失效计数、实际写入路径清单、Receipt 路径和失败/恢复状态；缺少任何绑定时返回 blocked。
