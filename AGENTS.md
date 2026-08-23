# ESFramework Codex 项目指令

## PowerShell 与编码

- PowerShell 读取中文或编码未知的项目文件时必须显式使用 UTF-8。
- 修改项目文本优先使用 `apply_patch`，禁止默认代码页覆写或机械转码。

## AIKnowledge 强制发现门禁

1. 只要任务涉及本项目的源码、配置、架构、Skill、AICommand、测试或发布事实，AI 在分析或搜索实现前必须先读取 `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`，再从 `Documentation/AIKnowledge/KnowledgeIndex.yaml` 按任务语义匹配 `routeKeys`。
2. 只加载命中的最小知识集：通常选择 1～3 个条目，读取其 `requiredReads` 和条目正文；禁止为“建立上下文”递归读取全部 `entries/`。
3. AIKnowledge 是导航和可追溯摘要，不替代当前源码、AIWarnings P0、AICommand 权限合同或真实验证证据。条目的 `SourceRefs` 缺失或哈希漂移时，必须把条目标记为 stale，回读权威来源并重新规划。
4. 找不到匹配路由时，不得跳过知识入口自行猜测；应回到 AIWarnings Start 链与当前源码，并明确报告 Knowledge 覆盖缺口。
5. `AIBRAIN_ENTRY.md` 或 `KnowledgeIndex.yaml` 缺失、不可解析或无法读取时，属于 AI 上下文发现失败；必须向用户报告，不能声称已经读取项目知识。
6. 简单寒暄、与项目无关的通用问答及纯文本格式调整不强制加载项目知识。

## Skill 路由门禁

1. 在首次工具调用前，必须根据用户真实意图对当轮可用 Skill 的 `name` 与 `description` 做语义匹配；用户不需要记忆 Skill 名称、路径或精确触发词。
2. 当用户要求交给新窗口、打开或关闭 Codex 对话/页签、恢复或继续历史会话、分叉会话、接手项目、按职责恢复窗口、恢复最近一段时间使用的独立窗口时，必须先读取并使用 `$es-codex-session-bootstrap`。
3. 命中 `$es-codex-session-bootstrap` 后，禁止用普通提示词、手写临时启动脚本、子代理或普通文本答复冒充真实的 New、Resume、Fork、Close 或 RestoreRecent 操作。
4. “写交接文档但不打开窗口”“解释会话机制”“在当前对话继续工作”不自动授权启动或恢复窗口；必须按语义和用户要求区分。
5. 当系统已经提供可用 Skill 清单时，以该清单为首选，不得每轮机械扫描 `.agents/skills`。只有疑似发现失败或 Skill 文件刚发生变化时，才允许扫描目录诊断或要求刷新会话。
6. 发现失败时必须先检查项目根 `F:\aaProject\ESFrameWorkPublish\.agents\skills\*\SKILL.md`；只扫描 `C:\Users\asus\.codex\skills` 或插件缓存不能作为“项目 Skill 不存在”的证据。对交接、New、Resume、Fork、Close、RestoreRecent 或接手项目语义，必须直接核对 `.agents/skills/es-codex-session-bootstrap/SKILL.md`。
7. 如果任务明显匹配项目 Skill 但该 Skill 未出现在当轮可用清单中，必须报告“清单注入缺口”，再读取项目内已确认的 `SKILL.md` 执行安全范围内的工作流；不得要求用户提供本项目已存在的 Skill 名称或路径，也不得假装已经执行 Skill。
8. Skill 只提供工作流，不扩大源码、Git、Unity、历史、审计状态、删除或发布权限。

## Skill 使用披露

1. 当本轮实际使用一个或多个 Skill 时，首次面向用户的进度更新必须声明 Skill 名称及其与当前任务的直接关系；不要列出只因环境注入而未使用的 Skill。
2. 最终答复必须单列“本轮使用的 Skill”，简要说明每个实际使用的 Skill 如何影响了结论、设计、修改或验证；纯文本回复且未使用 Skill 时不添加该段。
3. 技能披露是可观察性要求，不是授权、验收或执行证据。不得因为已披露 Skill 就声称已执行其脚本、获得 AICommand 权限，或完成 Runtime 验收。

## 会话窗口语义

- “开新对话”“交给新窗口”默认映射为 `New`。
- “继续旧窗口”“恢复对话”默认映射为 `Resume`。
- “从旧对话另开分支”默认映射为 `Fork`。
- 受管 `Resume`/`Fork` 必须先解析出精确 `SessionId`；官方选择器不能附加 ES 初始化消息，因此不得用于交接启动或冒充消息已送达。
- 新窗口返回 `terminalStarted` 只证明终端已创建；只有 `contextAccepted=true` 和精确接收回执才能报告初始化/任务已送达。`promptObserved`、超时或仅创建窗口都不得冒充执行已开始。
- “关闭对话/页签”默认映射为 `Close`，必须使用精确 selector；歧义时停止。
- “恢复最近 N 小时使用的独立窗口并按职责命名”默认映射为 `RestoreRecent -RecentHours N`。
- 禁止未经用户明确要求自动 `ForceNew`。
