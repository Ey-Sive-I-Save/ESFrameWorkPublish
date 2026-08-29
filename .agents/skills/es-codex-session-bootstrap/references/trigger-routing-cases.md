# ES Codex session trigger routing cases

Use these cases to validate semantic routing. They are behavior examples, not a keyword-only matcher.

## Must trigger this Skill

| User intent | Expected operation |
|---|---|
| “把当前任务交给一个新窗口” | `New` |
| “完成交接，然后开一个新的 Codex 对话” | `New` |
| “接手这个 ES 项目” | Read-only bootstrap, then the bounded task |
| “继续昨天的工程验收窗口” | Resolve one exact SessionId, then exact `Resume`; if identity is unknown, list/query candidates and stop for confirmation |
| “从资源管线旧对话分叉一个新会话” | Exact `Fork` after identity confirmation |
| “关闭 AITest 页签” | Exact `Close` |
| “列出当前 ES 会话和异常状态” | Read-only `Status` |
| “查询当前会话的唯一 ID” | `Query -Current`; return record and session routing IDs without guessing |
| “查询资源职责对应的可用 AI” | `Resolve -ResponsibilityKey resource-pipeline` |
| “把当前 AI 绑定为资源验收职责” | Exact `BindResponsibility`; reject a live conflicting owner |
| “把当前任务绑定到工程验收窗口，完成后自动发起验收” | `BindAcceptance -Current -AcceptanceResponsibilityKey engineering-acceptance`; explicit opt-in, self-binding rejected, disable is reversible |
| “等 AITest AI 空闲后再处理” | `Wait` for declared readiness; do not claim this sends a message |
| “等资源 AI 空闲后给它发消息，并让我查询结果” | `Wait`, then `QueueMessage`, then `MessageStatus`; report cooperative mailbox state exactly and do not claim direct TUI injection |
| “给资源职责 AI 发这条消息” | Prefer high-level `SendMessage`; return the exact delivery plan, message ID, wake requirement, and receipt state |
| “我完成后自动发给验收窗口，等它不忙并等返回” | `RequestAcceptance`; resolve the bound acceptance responsibility, continuously query route state while Busy/Pending, send a correlated acceptance request, then wait for a bounded reply |
| “回复刚才的验收请求” | `ReplyAcceptance` with the exact request message ID; target the requester record and preserve correlation metadata |
| “查询验收请求有没有返回” | `AcceptanceStatus` with the exact request message ID; report request and correlated reply states separately |
| “资源 AI 忙完后自动处理这条消息” | Queue it while the target is active; a trusted/reloaded `Stop` hook may claim it at turn completion. Report hook trust/load as unverified unless observed |
| “检查消息桥到底支持什么” | `BrokerStatus -ProbeAppServer`; distinguish stdio handshake, hook delivery, daemon support, and spontaneous idle wake |
| “快速体检一下现在缺什么” | `Doctor`; return stable issue codes, safe commands, and explicit authorization requirements without mutation |
| “隔离跑一遍职责、等待和消息闭环” | `SmokeTest`; use a unique temporary state root, perform real writes there, and never mutate the authoritative local registry |
| “一键检查会话功能是否达到商业运行条件” | `SelfTest -RunSelfTests -ProbeAppServer`; keep code readiness, cooperative-mailbox commercial baseline, fleet hook coverage, and managed direct delivery separate |
| “清理过期消息” | `MessageRepair` DryRun first; require explicit `-Apply`, and require `-DeleteTerminalMessages` before deleting retained terminal pairs |
| “检查并修复 pending、残留和孤儿会话状态” | `Repair` plan first; use `-Apply` only after explicit authorization |
| “恢复最近 24 小时使用的 AI 独立窗口并按职责命名” | `RestoreRecent -RecentHours 24` |
| “只读读取旧对话，然后开新窗口，不要 Resume” | `ReadOnlyRestore`; create a redacted read-only packet and launch a fresh `New` session |
| “当前页签清掉后重建对话，再初始化” | `CurrentTabRecycle`; frozen semantics are same-window new-tab then exact-source close (`physicalTabReused=false`), never physical in-place same-tab reuse |
| “同时开多个职责窗口/批量启动多个 AI” | `Invoke-ESCodexMultiLaunch.ps1 -PlanPath`; explicit per-target `New`/`Handoff`/`Reissue`, strict drift gate, bounded waves, per-target evidence; never collapse partial failure into one success |
| “把 ES 对话都放进一个项目窗口的不同页签” | `ProjectWindow` terminal mode |
| “开一个辅助窗口，和你保持同页签” | `CurrentWindow` terminal mode; target the caller's inherited `WT_SESSION` with `-w 0` |
| “用交接 Skill 重新交接到新窗口” | Read the project `es-codex-session-bootstrap` Skill and perform the requested real session operation; do not search only global Skill roots |
| “不要所有窗口共用同一个交接文件，分别搞” | Create one immutable handoff snapshot directory per launch; envelopes validate their own snapshot and treat later source drift as informational |
| “信封没了但窗口已经读完交接，应该继续” | Allow only that previously accepted conversation to continue from accepted transcript/context; require a new envelope for any New, Resume, or Fork |

## Must not substitute another mechanism

- Do not return only a copyable prompt when the user asked to open a real conversation.
- Do not spawn a subagent and describe it as a new Codex UI conversation.
- Do not handwrite a parallel launcher when the project launcher is available.
- Do not choose a fuzzy resume candidate automatically.
- Do not use the official Resume/Fork picker for managed ES task delivery; it cannot carry the mandatory initialization prompt.
- Do not report a visible terminal, process ID, `launched=true`, or a history token as proof that the task was accepted. Require `contextAccepted=true` and its exact receipt.
- Do not use `ForceNew` for parameter repair or ordinary retries.

## Must not launch automatically

| User intent | Expected handling |
|---|---|
| “帮我写一份交接文档，但先不要开窗口” | Create/review the document only under current authorization |
| “解释一下 Codex resume 的作用” | Explain only; do not resume |
| “就在当前对话继续实现” | Continue the current task; do not create a new session |
| “列出可能的历史窗口” | Read-only candidate listing; do not select or resume automatically |

## Failure reporting

If the Skill is expected but unavailable, report the missing discovery capability explicitly. Do not claim that prose, a subagent, or a raw shell command completed the requested UI/session operation.

The project path `.agents/skills/es-codex-session-bootstrap/SKILL.md` is a required fallback. Global Codex skill roots and plugin caches are not authoritative evidence that this project Skill is absent.
