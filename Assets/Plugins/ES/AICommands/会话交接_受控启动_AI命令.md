# 会话交接：受控启动 AI 命令

本命令只允许通过 `ES/AI协作历程（Codex）/Tools/Complete-ESCodexHandoff.ps1` 完成一次明确职责的会话交接或受控启动。

命令类型：安全执行。
默认改文件：否；仅允许当前任务声明的会话注册、一次性授权包、私有快照和交接 Receipt；不得直接修改项目源代码、Assets、Git 或发布状态。
风险等级：L3。

## 必须先读

执行前必须读取本文件全文、`.agents/skills/es-codex-session-bootstrap/SKILL.md`、会话交接合同、当前 AIWarnings 启动链和 TaskContract，并取得绑定当前任务的 AIBrain PlanHash、目标职责、精确会话身份、超时和停止条件。

只允许一次性授权的 `New/Resume/Fork/Handoff` 操作。禁止标题猜测、重复启动、直接注入已有 TUI、绕过交接编排器或把 `launched=true` 冒充 `ContextAccepted`。缺少新鲜授权、快照 Hash、目标路径或收据时必须 blocked。

本命令不代表 Runtime 或 Unity 验收；窗口可见、进程存在和提示词发送都不能替代 `ContextAccepted` Receipt。

命令 ID：`session.handoff.execute`

## 取消（cancellation）

一次性授权包提交前可取消；启动后仅通过 `Complete-ESCodexHandoff.ps1` 的受支持停止入口取消并等待终态。取消或超时返回 `Cancelled` 或 `RecoveryRequired`，不得报告 `ContextAccepted`。

## 恢复（recovery）

仅从已验证的 per-launch snapshot 与 Receipt 恢复；丢失、Hash 漂移或身份冲突返回 `NeedsReissue`，重新发放一次性授权包。禁止改用 mutable `sourceAbsolutePath`、另一 handoff 或重复启动；重试必须使用新 InvocationId/idempotencyKey。

## 验证（validation）

执行 `Test-ESCodexLaunchEnvelope.ps1` 与 Receipt 校验，确认 LaunchToken、envelope Hash、branch/HEAD/worktree 绑定和 `ContextAccepted`；`launched=true`、进程存在或窗口可见均不足以替代验收。

## evidenceRef

回执必须包含 per-launch snapshot 绝对路径及 SHA-256、源/接收会话身份、planHash、InvocationId、停止原因和验证输出；不得写入 Git、历史或审计状态。

## 交付格式

返回一次性授权包 Hash、目标职责、源会话和接收会话身份、快照 Hash、ContextAccepted 状态、交接 Receipt 和停止原因；未完成初始化不得报告成功。
