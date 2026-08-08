# ESFramework Codex 项目指令

## PowerShell 与编码

- PowerShell 读取中文或编码未知的项目文件时必须显式使用 UTF-8。
- 修改项目文本优先使用 `apply_patch`，禁止默认代码页覆写或机械转码。

## Skill 路由门禁

1. 在首次工具调用前，必须根据用户真实意图对当轮可用 Skill 的 `name` 与 `description` 做语义匹配；用户不需要记忆 Skill 名称、路径或精确触发词。
2. 当用户要求交给新窗口、打开或关闭 Codex 对话/页签、恢复或继续历史会话、分叉会话、接手项目、按职责恢复窗口、恢复最近一段时间使用的独立窗口时，必须先读取并使用 `$es-codex-session-bootstrap`。
3. 命中 `$es-codex-session-bootstrap` 后，禁止用普通提示词、手写临时启动脚本、子代理或普通文本答复冒充真实的 New、Resume、Fork、Close 或 RestoreRecent 操作。
4. “写交接文档但不打开窗口”“解释会话机制”“在当前对话继续工作”不自动授权启动或恢复窗口；必须按语义和用户要求区分。
5. 当系统已经提供可用 Skill 清单时，以该清单为首选，不得每轮机械扫描 `.agents/skills`。只有疑似发现失败或 Skill 文件刚发生变化时，才允许扫描目录诊断或要求刷新会话。
6. 发现失败时必须先检查项目根 `F:\aaProject\ESFrameWorkPublish\.agents\skills\*\SKILL.md`；只扫描 `C:\Users\asus\.codex\skills` 或插件缓存不能作为“项目 Skill 不存在”的证据。对交接、New、Resume、Fork、Close、RestoreRecent 或接手项目语义，必须直接核对 `.agents/skills/es-codex-session-bootstrap/SKILL.md`。
7. 如果任务明显匹配项目 Skill 但该 Skill 未出现在当轮可用清单中，必须报告“清单注入缺口”，再读取项目内已确认的 `SKILL.md` 执行安全范围内的工作流；不得要求用户提供本项目已存在的 Skill 名称或路径，也不得假装已经执行 Skill。
8. Skill 只提供工作流，不扩大源码、Git、Unity、历史、审计状态、删除或发布权限。

## 会话窗口语义

- “开新对话”“交给新窗口”默认映射为 `New`。
- “继续旧窗口”“恢复对话”默认映射为 `Resume`。
- “从旧对话另开分支”默认映射为 `Fork`。
- 受管 `Resume`/`Fork` 必须先解析出精确 `SessionId`；官方选择器不能附加 ES 初始化消息，因此不得用于交接启动或冒充消息已送达。
- 新窗口返回 `terminalStarted` 只证明终端已创建；只有 `contextAccepted=true` 和精确接收回执才能报告初始化/任务已送达。`promptObserved`、超时或仅创建窗口都不得冒充执行已开始。
- “关闭对话/页签”默认映射为 `Close`，必须使用精确 selector；歧义时停止。
- “恢复最近 N 小时使用的独立窗口并按职责命名”默认映射为 `RestoreRecent -RecentHours N`。
- 禁止未经用户明确要求自动 `ForceNew`。
