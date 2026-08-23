---
name: es-codex-session-bootstrap
description: >-
  Manage ESFramework Codex sessions and terminal workspace: initialize or take over the project, New, Resume, Fork, Focus or Close exact tabs, list and route sessions, publish Busy/Idle presence, wait for readiness, queue cross-window messages, bind responsibilities, repair stale state, restore recent independent sessions, assign responsibility titles, prevent duplicate launches, and validate immutable handoffs. Use for requests such as “交给新窗口”, “打开/关闭 Codex”, “恢复/继续旧会话”, “分叉会话”, “查询会话 ID”, “绑定职责”, “等待某 AI 空闲”, “给某职责 AI 留消息”, “查看会话状态”, “修复残留会话状态”, “接手项目”, or “恢复最近 N 小时的独立 AI 窗口”. Do not trigger merely to write a handoff document, explain sessions, or continue in the current conversation without a window/session operation.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# Bootstrap ES Codex Sessions

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and deny-by-default; capability visibility never grants permission. Use AIBrain `planTask`, the matching AICommand, and the current TaskContract before any write or external operation.

## Workflow controls

- Scope and authority are checked before execution; stale or missing evidence blocks the task.
- Execute only through AIBrain planTask and the matching AICommand; direct execution is denied.
- Record evidence for positive, invalid-input, denied-expansion, repeat-idempotency, and interruption-recovery cases.
- The short-lived `ES_CODEX_LAUNCH_TOKEN` is a non-secret session marker, never a credential; it carries only the minimum launch capability and is not persisted as user authentication.
- External process boundaries use an exact executable allowlist and one-time argument envelope; a process name or visible terminal alone is never execution proof.

Use `ES/AI协作历程（Codex）/Tools/Complete-ESCodexHandoff.ps1` as the only real handoff orchestrator when the user asks to hand work to a new window. It owns timeline coverage, source-session/archive binding, full-history responsibility assessment, bootstrap validation, private per-launch snapshots, immutable handoff receipts, and the final `Start-ESCodexSession.ps1` launch. `Start-ESCodexSession.ps1 -Mode New` remains valid for an ordinary new session, but a New request containing handoff intent or any `-HandoffPath` is hard-blocked unless it is invoked by the orchestrator with its short-lived authorization capability. `-HandoffMode` is not a user-facing bypass switch.

Do not use an action as the receiving window's responsibility. `handoff`, `handover`, `resume`, `fork`, `bootstrap`, `交接`, `恢复`, `分叉`, and `启动` are operation names, not stable content responsibilities. The orchestrator assesses all formal timeline nodes and rejects a supplied key that does not match the dominant content responsibility; ambiguous history is blocked instead of guessed. Pass a stable content key such as `es-editor-foundation-governance`, `es-session-bootstrap-maintenance`, or `es-aibrain-architecture`; omit `-TabTitle` unless a content-specific title is required. The launcher rejects action-shaped keys and titles for orchestrated handoffs.

Do not pretend that a prompt inside the current thread created a new conversation.

Before acting, classify the request semantically rather than waiting for the exact Skill name. Read [references/trigger-routing-cases.md](references/trigger-routing-cases.md) when changing, diagnosing, or validating trigger behavior.

This project copy lives at `.agents/skills/es-codex-session-bootstrap/SKILL.md`. If a session-operation request matches but the injected Skill list omits it, report a list-injection gap and read this project copy directly. Never search only global Codex skill or plugin-cache directories and conclude that the ES project Skill does not exist; never ask the user to provide a path already fixed by this project.

## Workflow

1. Run `scripts/Start-ESCodexSession.ps1 -Mode Validate -DryRun` before the first launch in an environment. Report missing CLI, project root, start files, or history paths.
   使用 [会话引导包验证器](scripts/Test-ESSessionBootstrapPacket.ps1) 检查 schema v2、唯一 sessions.json 权威、不可变快照哈希和 contextAccepted 证据。
2. Interpret “打开新 Codex”, “开启新对话”, or “初始化新会话” as `-Mode New` when this is an ordinary new session. Interpret “交接到新窗口”“让新 AI 接手”“直接交接” as `Complete-ESCodexHandoff.ps1 -OpenNew`; first resolve the exact current Session JSONL and archive, run timeline coverage, and stop if either is missing. The handoff command must carry a receiving content responsibility, not a handoff action label. Prefer `-TerminalMode ProjectWindow`, which reuses the named `ESFramework` Windows Terminal window and adds one responsibility-scoped tab. If the user explicitly asks for a tab beside the current conversation, use `-TerminalMode CurrentWindow`; a named project window is not proof that it is the caller's current window.
3. Interpret “恢复对话” or “继续旧会话” as `-Mode Resume`. Resolve and pass one exact `-SessionId`. A stable `-ResponsibilityKey` may select a session only when the local registry has exactly one match; multiple or zero matches require Status/Query/Find plus identity confirmation. Never use the official picker for a managed ES handoff: picker selection cannot append the mandatory initialization prompt, so the launcher hard-fails instead of opening an uninitialized window.
4. Interpret “分叉会话” as `-Mode Fork`. Prefer fork when the user wants the old transcript as context but does not want to continue mutating the original conversation. An exact resume or fork restores the registered responsibility and short tab title unless the caller overrides them.
5. When the user supplies only topic words, use `ES/AI协作历程（Codex）/Tools/Find-CodexSession.ps1` read-only. Show candidates and require identity confirmation before passing a session ID to the launcher.
6. In a newly initialized session, read only the start chain: AIWarnings `README`, `CurrentStatus`, and `RuleIndex`. Then load the matched P0 and domain rules for the actual task. Do not recursively read all AIWarnings.
7. Treat module audit state and AI collaboration history as optional recovery sources. Read them only when the requested task needs them. Never write or claim an old history archive without current user authorization.
8. Recheck Git branch, HEAD, staged/unstaged/untracked paths, relevant source, current rules, and evidence after resume. A transcript or checkpoint is navigation, not current truth.
9. When a history archive is created or recovered, run `ES/AI协作历程（Codex）/Tools/Test-ESCodexTimelineCoverage.ps1` against the confirmed session JSONL and archive. A count mismatch, missing stage container, non-contiguous T numbering, or missing required node fields is a hard failure; do not report the history as complete.
10. When an authorized history/recovery workflow is complete, evaluate its commercial-feasibility evidence and append the standard handoff offer once. Generate the directly copyable handoff prompt only after the user accepts; never treat the prompt as new implementation authority.
11. To deliver a bounded task into a new session, use `Complete-ESCodexHandoff.ps1` for handoff intent. It first validates the source Session JSONL against the archive with `Test-ESCodexTimelineCoverage.ps1`, then passes the archive, Bootstrap Skill, CurrentStatus, RuleIndex, and any additional handoff files through `-HandoffPath`. Every real launch copies those sources into its own create-only directory under `%LOCALAPPDATA%\ESFramework\CodexSessions\handoff-snapshots`, hashes the private copies, and records only those immutable snapshot paths in the launch envelope. Never make multiple windows validate one shared mutable handoff file. The new session must run `scripts/Test-ESCodexLaunchEnvelope.ps1` once with its exact launch token. Successful validation creates both the launch acceptance receipt and the orchestrator's immutable handoff receipt. Snapshot drift before acceptance is a hard failure; later source-file drift is informational.
12. New-session launch is idempotent by task fingerprint. The launcher checks a per-task mutex and a short-lived process marker before opening a window. Repeating the same task returns `alreadyRunning` instead of launching a duplicate. Use `-ForceNew` only when the user explicitly requests a second independent window. Treat `terminalStarted`, `promptObserved`, and `contextAccepted` as separate evidence: only `contextAccepted=true` with the exact create-only receipt proves initialization delivery. A visible window, process ID, history token, timeout, or `launched=true` alone must never be reported as task delivery or execution start.
13. Pass a stable `-ResponsibilityKey` for recurring roles. The launcher maps known keys such as `engineering-acceptance`, `aitest`, `resource-pipeline`, and `graph-audit` to short tab titles. `-TabTitle` is an explicit safe override.
14. Runtime envelopes, launch markers, and the authoritative session registry live under `%LOCALAPPDATA%\ESFramework\CodexSessions`. `sessions.json` is the unique responsibility/session authority; launch-state and Codex history are observations and recovery sources, never competing authorities or project authorization.
15. Interpret “列出会话”“查看页签/会话状态” as `-Mode Status` or `-Mode List`. Report registered identity, process liveness, visible-tab cardinality, pending registration, missing process/tab, authority gaps, and orphan artifacts. This operation is read-only and does not scan Codex JSONL when registry and launch-state are sufficient.
16. Interpret “显示终端”“聚焦受管 CMD/页签” as `-Mode Focus` with one exact `SessionId` and `RecordId`. Focus requires the registry record, live managed shell process, captured Windows Terminal host process, and one matching tab inside that exact host. It never falls back to a title-only lookup or injects text into an existing TUI.
17. Interpret “查询当前会话”“查询某职责唯一 ID” as `-Mode Query` or strict `-Mode Resolve`. Return `recordId` for authority/binding and `sessionId` for exact conversation routing. Current resolution uses launch token first, then `WT_SESSION`, then process ancestry; ambiguous results never guess.
18. Interpret “绑定职责” as `-Mode BindResponsibility` with one exact target and a new stable key. Interpret “绑定验收窗口/完成后自动请求验收” as `-Mode BindAcceptance -Current -AcceptanceResponsibilityKey engineering-acceptance`; this is an explicit opt-in on the current record, rejects self-binding, and can be disabled with `-DisableAcceptanceBinding`. Reject a key already owned by another non-terminal session. Binding changes registry metadata only; it does not rename or move the tab.
19. Interpret “标记忙碌/空闲/等待” as `-Mode SetPresence`. Presence is optional, expires by TTL, and defaults to `Unknown`; do not require ordinary windows to publish it continuously. Interpret “等待某 AI 空闲/结束” as `-Mode Wait`, capped at 60 seconds per call. Wait only returns state and never injects a message.
20. Interpret “给某职责 AI 发消息/留消息” as the high-level `-Mode SendMessage`; it resolves one target, queues idempotently, and reports `StopHookAtBusyCompletion`, `NextUserPromptHook`, `MailboxUntilSessionReturns`, or `MailboxOnlyUntilHookObserved`. Interpret “任务完成后自动请求工程验收” as `-Mode RequestAcceptance`: resolve the bound `engineering-acceptance` responsibility (or an explicit key), poll its route, keep waiting while it is `Busy`/`Pending`, queue an `acceptance-request` with a correlation ID, and wait up to 60 seconds for a correlated reply. The receiver answers with `-Mode ReplyAcceptance`; a timed-out caller uses `-Mode AcceptanceStatus`. Use `QueueMessage` only for the lower-level mailbox operation and `MessageStatus` for ordinary receipts. Requests are immutable, states are revisioned, and TTL, idempotency, exact receipts, correlation IDs, and bounded quotas are mandatory. The project `Stop` and `UserPromptSubmit` hooks may claim one queued message at a supported turn boundary after the user trusts the exact hook definition and the session reloads. `queued` is never delivery proof, hook configuration is not hook activation proof, and a completely idle standalone TUI cannot be awakened without new input.
21. Interpret “检查消息桥能力” as `-Mode BrokerStatus -ProbeAppServer`. Interpret “快速体检/现在缺什么” as read-only `-Mode Doctor`; it returns stable issue codes and separates the cooperative-mailbox commercial baseline, full fleet hook coverage, and managed direct delivery. Interpret “隔离跑一遍会话闭环” as `-Mode SmokeTest`; it writes only to a unique temporary state root, exercises responsibility, Busy-to-Idle wait, Hook delivery, completed receipt, restart reload, and Repair idempotence, then removes successful artifacts. Interpret “完整自检/商业就绪检查” as `-Mode SelfTest -RunSelfTests -ProbeAppServer`; it includes that isolated operational smoke and reports `codeReady`, `commercialBaselineReady`, `fleetOperationalReady`, and `managedDirectDeliveryReady` separately. Direct injection into an existing standalone Windows TUI is not required for the bounded cooperative-mailbox commercial profile and must never be falsely claimed. Interpret “清理/修复消息状态” as `-Mode MessageRepair`; default to DryRun. Marking TTL expiry or deleting retained terminal messages requires explicit `-Apply`, and deletion additionally requires `-DeleteTerminalMessages`.
22. Interpret “修复/对账会话状态” as `-Mode Repair` or `-Mode Reconcile`. The default is a read-only repair plan. Apply registry changes only with explicit `-Apply`; never delete orphan artifacts in the first-stage repair path.
23. Interpret “关闭对话”“关闭页签” as `-Mode Close`. Resolve the authoritative registry first. Closing requires the exact terminal host process plus one matching host-local title; ambiguity or a missing host identity is blocked unless the user explicitly resolves it by resuming the exact session. Close the Windows Terminal tab through its UI close button so the visual page disappears.
24. Interpret “恢复最近 24 小时使用的 AI 独立窗口并根据职责命名” as `-Mode RestoreRecent -RecentHours 24`. Prefer authoritative registry responsibility and title metadata. Use JSONL classification only for unregistered history fallback. Exclude smoke sessions by default, group by stable responsibility, and skip a responsibility when one of its sessions is active.
25. Treat schema v2 as the only valid launch-envelope schema. Run `scripts/Convert-ESCodexStateToV2.ps1 -DryRun` before migrating a machine with v1 state, then run it without `-DryRun` after reviewing the exact counts. It moves v1 envelopes into a recoverable local `legacy-v1` quarantine and marks affected sessions `requiresV2Resume`; never rewrite an immutable v1 envelope in place. Close and Resume exact sessions to create their first v2 envelope.
26. Treat an envelope as a one-time acceptance gate, not a continuous runtime lease. After a session validates it successfully and receives an acceptance receipt, later loss of the envelope does not stop that already-running conversation. Continue only from the accepted transcript/context, report that fresh artifact verification is unavailable, and never substitute a different handoff. Every later New, Resume, or Fork still requires a new envelope and first acceptance.
27. Report the context state with exactly one of three values: `ValidatedNow` when the validator ran now and passed; `AcceptedContext` when a prior receipt permits the current conversation to continue without the envelope; `HardFailure` when first acceptance fails or an existing envelope/private snapshot is altered. Ordinary continuation must not request the envelope again or switch handoff sources.
28. The launcher waits up to 60 seconds for delivery evidence, records pending authority before waiting, and emits `Prepared`, `TerminalStarted`, `PromptObserved`, `ContextAccepted`, or `Failed`. A Codex exit marker before acceptance is a hard startup failure; an evidence timeout remains explicitly unconfirmed and is not silently retried with `ForceNew`.

## Launch commands

```powershell
# Validate without opening a terminal
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode Validate -DryRun

# Standard handoff to a new window: timeline coverage + private snapshot + receipt
& '.\ES\AI协作历程（Codex）\Tools\Complete-ESCodexHandoff.ps1' `
  -SessionPath '<confirmed-rollout-jsonl>' `
  -ArchivePath '<window-archive.md>' `
  -TaskKey 'aibrain-protocol-audit' `
  -ResponsibilityKey 'aibrain-protocol-audit' `
  -TabTitle 'ES·AIBrain协议审计' `
  -TaskPrompt '<bounded task>' `
  -OpenNew

# Open a new initialized Codex conversation
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode New

# Open a new session and deliver a bounded task after initialization
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode New -TaskPrompt '<task text>'

# Add a responsibility tab to the shared ESFramework Windows Terminal window
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' `
  -Mode New `
  -TaskKey 'engineering-acceptance' `
  -ResponsibilityKey 'engineering-acceptance' `
  -TerminalMode ProjectWindow `
  -HandoffPath @('ES/Automation/Handoffs/工程验收职责交接_20260805.md', 'ES/Automation/Handoffs/CODEX_TASK_CONTEXT.md') `
  -TaskPrompt '<task text>'

# Add a responsibility tab beside the current conversation (requires WT_SESSION)
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' `
  -Mode New `
  -TerminalMode CurrentWindow `
  -TaskKey '<stable-key>' `
  -ResponsibilityKey '<responsibility-key>' `
  -TabTitle '<short-title>' `
  -TaskPrompt '<task text>'

# Stable task identity; repeated calls do not open another window
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode New -TaskKey 'aitest-initial-slice' -TaskPrompt '<task text>'

# Resume or fork an exact confirmed session
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode Resume -SessionId '<uuid>'
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode Fork -SessionId '<uuid>'

# Resume an exact session into the shared project window and restore its registered tab title
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' `
  -Mode Resume -SessionId '<uuid>' -TerminalMode ProjectWindow

# Resume by stable responsibility only when the local registry has one exact match
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' `
  -Mode Resume -ResponsibilityKey 'engineering-acceptance' -TerminalMode ProjectWindow

# Force a standalone legacy CMD window only when Windows Terminal grouping is not wanted
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' `
  -Mode New -TerminalMode PlainCmd -TaskKey '<stable-key>' -TaskPrompt '<task text>'

# Recheck an immutable envelope after a session starts or resumes
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Test-ESCodexLaunchEnvelope.ps1' `
  -EnvelopePath '<absolute-envelope-path>' `
  -LaunchToken '<exact-launch-token>'

# Close one exact conversation tab, including the visible Windows Terminal page
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' `
  -Mode Close -SessionId '<uuid>'

# Close all tabs registered to one responsibility after explicit confirmation
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' `
  -Mode Close -ResponsibilityKey 'launcher-smoke' -AllMatches

# Read authoritative session state and current observations
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode Status

# Bring one proven managed terminal tab or Plain CMD window to the foreground
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' `
  -Mode Focus -SessionId '<uuid>' -RecordId '<registry-record-id>'

# Resolve current or responsibility-scoped routing IDs
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode Query -Current
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode Resolve -ResponsibilityKey 'resource-pipeline'

# Bind one exact session and optionally publish queryable availability
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode BindResponsibility -SessionId '<uuid>' -BindResponsibilityKey '<stable-key>'
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode SetPresence -Current -Availability Waiting -ActivityKey '<activity>'

# Opt in the current session to automatically request the bound acceptance window at Stop
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode BindAcceptance -Current -AcceptanceResponsibilityKey 'engineering-acceptance'

# Wait for declared readiness; this does not send a message
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode Wait -ResponsibilityKey '<stable-key>' -WaitFor Ready -WaitSeconds 30

# Queue a durable cooperative message after readiness is observed
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode QueueMessage -ResponsibilityKey '<stable-key>' -MessageBody '<bounded-message>' -IdempotencyKey '<retry-safe-key>' -RequireReady

# Preferred simple entry: resolve, queue, and report the exact delivery plan
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode SendMessage -ResponsibilityKey '<stable-key>' -MessageBody '<bounded-message>' -IdempotencyKey '<retry-safe-key>'

# On task completion: wait for the bound engineering-acceptance AI, request a result, and wait for its correlated reply
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode RequestAcceptance -Current -AcceptanceResponsibilityKey 'engineering-acceptance' -MessageBody '任务已完成，请执行工程验收并回复结论、证据和阻断项。'

# Receiver-side correlated reply
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode ReplyAcceptance -Current -MessageId '<request-message-id>' -MessageBody '验收结论：通过/阻断；证据：...'

# Query a timed-out acceptance request and its reply
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode AcceptanceStatus -MessageId '<request-message-id>'

# Query the exact queued/accepted/completed/failed/expired state
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode MessageStatus -MessageId '<message-uuid>'

# Probe the actual local Broker/App Server boundary without creating a turn
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode BrokerStatus -ProbeAppServer

# Fast read-only health report with stable issue codes and exact authorization boundaries
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode Doctor

# Isolated real-write acceptance; never touches the authoritative local session root
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode SmokeTest

# One-command code and operational readiness audit
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode SelfTest -RunSelfTests -ProbeAppServer

# Plan message expiry/retention reconciliation; Apply is always explicit
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode MessageRepair
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode MessageRepair -Apply

# Deletion requires both Apply and the bounded terminal-message selector
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode MessageRepair -MessageRetentionDays 30 -DeleteTerminalMessages -Apply

# Generate a repair plan; apply only after explicit authorization
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode Repair
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' -Mode Repair -Apply

# Restore the most recent inactive session for each responsibility from the last 24 hours
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1' `
  -Mode RestoreRecent -RecentHours 24

# Inventory and quarantine legacy schema v1 local state
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Convert-ESCodexStateToV2.ps1' -DryRun
& '.\.agents\skills\es-codex-session-bootstrap\scripts\Convert-ESCodexStateToV2.ps1'
```

Read [references/session-bootstrap-contract.md](references/session-bootstrap-contract.md) before changing launch behavior or recovery authority.

## Required output

Report the selected mode, fixed project root, effective terminal mode/window/tab title, responsibility key, launch-envelope path, per-launch handoff snapshot directory, `terminalStarted`, `promptObserved`, `contextAccepted`, startup failure/timeout, exact acceptance-receipt or diagnostic path, initialization scope, recovery coverage result, commercial-feasibility evidence, and any recovery evidence that remains unconfirmed. Never describe terminal creation as task delivery. After a completed history/recovery workflow, end with the one-time handoff offer.
