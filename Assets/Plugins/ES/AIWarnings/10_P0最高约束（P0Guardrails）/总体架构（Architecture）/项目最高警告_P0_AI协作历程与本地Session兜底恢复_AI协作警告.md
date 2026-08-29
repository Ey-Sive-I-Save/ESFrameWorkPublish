# 项目最高警告：AI 协作历程与本地 Session 兜底恢复

Status: current
StableId: es.aiwarning.p0.ai-collaboration-history-session-recovery.v1
Authority: AIWarnings（长期 P0 约束）；详细协议与恢复规则见 Knowledge
RouteKeys: aiwarnings, p0, codex, session, history, handoff, recovery, timeline, privacy
Applicability: AI 协作历程、Session 定位、窗口恢复、交接档案和时间线审计
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-p0-ai-collaboration-history-session-recovery.md
StaleWhen: session bootstrap、handoff envelope、timeline coverage 工具、档案格式或 SourceRef 哈希变化。

## P0 长期约束

- 协作历程只回答真实窗口按时间顺序做了什么、证据到哪一级、哪里失败或被纠正；不是阶段总结、成果宣传、源码验收或运行时通过的替代品。
- 一个真实窗口只能对应一个唯一档案；候选定位、归属确认、逐轮恢复和技术验收必须分开。`history.jsonl` 只做候选索引，`rollout-*.jsonl` 才是逐轮恢复证据。
- 普通问答、代码修改、读取、构建和测试不得自动写历程；只有用户明确要求创建、更新、恢复、整理或交接时才获得对应范围授权。
- 交接必须使用项目既有 bootstrap/handoff 路由：先解析 Session、档案和 TaskKey，完成覆盖校验与 Bootstrap Validate；目标窗口返回 `ContextAccepted=true` 前不得关闭源窗口，不得用聊天摘要冒充交接。
- 新窗口必须依据 immutable launch envelope、私有 handoff snapshot 和最新工作树重新确认；交接授权不扩大源码、Git、Unity、发布、删除或外部发送权限。
- 每条独立用户消息、纠正、失败、重试和交付复核都要有连续唯一时间线节点；`task_complete` 不等于实现或验收，失败/中止/否决必须保留。
- 必须脱敏凭据和隐私、排除 system/developer/reasoning 等无关内容；禁止手改 session JSONL、自动合并候选或在覆盖脚本失败后声称恢复完成。

## Knowledge 导航

术语、路由、恢复流水线、候选分数、时间线完整性、隐私和工具入口见 `es.aiwarning.p0.ai-collaboration-history-session-recovery.v1`。本 Warning 不授予历程写入、交接启动、源码、Git、运行时或发布权限。
