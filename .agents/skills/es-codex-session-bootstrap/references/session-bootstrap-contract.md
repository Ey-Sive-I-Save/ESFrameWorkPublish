# ES Codex Session Bootstrap Contract

## Capability boundary

- `codex` starts a new interactive terminal session.
- `codex resume` can continue a saved interactive session by exact ID or through the official picker, but managed ES delivery supports only the exact-ID form because picker selection cannot append the mandatory initialization prompt.
- `codex fork` creates a new conversation from a saved session transcript while preserving the original.
- Windows Terminal groups ESFramework conversations into a named project window; each Codex conversation remains an independent session even when tabs share one terminal window.
- A project Skill cannot create a new UI thread by prose alone. It must invoke a supported Codex surface or give the user the exact command.

## Project terminal workspace

- `TerminalMode Auto` uses the named `ESFramework` Windows Terminal window when `wt.exe` is available and otherwise falls back to a standalone CMD window.
- `TerminalMode CurrentWindow` uses Windows Terminal target `-w 0` and requires an inherited `WT_SESSION`; use it when the user explicitly asks for another tab beside the current conversation.
- `TerminalMode ProjectWindow` always targets the named project window and creates one new tab.
- `TerminalMode NewWindow` creates a separate Windows Terminal window.
- `TerminalMode PlainCmd` preserves the legacy standalone `cmd.exe` behavior.
- A tab title is restored from exact session metadata, explicitly supplied with `-TabTitle`, or generated from `-ResponsibilityKey`. Titles are sanitized before entering a CMD command line.
- Sharing a Windows Terminal window is presentation grouping only. It does not merge Codex transcripts, permissions, worktrees, or task ownership.
- A named project window is not equivalent to the caller's current window. Multiple windows may share routing history or names, so “和当前窗口同页签组” must use `CurrentWindow`, not `ProjectWindow`.
- `Mode Close` resolves only registered ES tabs by exact selectors and invokes the matched Windows Terminal tab's own close button. Killing a child process without closing the visual tab is not considered successful tab closure.
- Ambiguous close selectors are a hard failure unless the caller explicitly passes `-AllMatches`.
- `Mode RestoreRecent` reads the authoritative registry first and uses local rollout JSONL only for unregistered fallback classification. It excludes known smoke tasks by default, groups candidates by stable responsibility, and resumes only the latest session in each group. If one session in a responsibility group is live, the whole group is skipped to prevent an older duplicate from reopening. Time proximity never grants archive ownership or source authority.
- Automatic recent recovery may use deterministic responsibility rules for tab presentation. An unclassified session is not resumed unless `-IncludeUnclassified` is explicit; it receives a neutral timestamp title rather than an invented project responsibility.

## Fixed project root

Always launch with:

```text
F:\aaProject\ESFrameWorkPublish
```

The launcher also derives the same root from its installed project location and fails when the requested override resolves elsewhere.

## Initialization scope

The initial prompt may authorize only read-only project initialization:

1. Read AIWarnings `README.md`.
2. Read `当前状态（CurrentStatus）.md`.
3. Read `规则索引（RuleIndex）.md`.
4. Inspect branch, HEAD, and worktree status.
5. Wait for or execute the user's actual task under the matching rules.

It does not authorize source edits, Git operations, Unity execution, history maintenance, audit-state writes, publishing, or deletion.

## Timeline completion gate

When a user authorizes history creation, recovery, completion, or handover, the responsible Skill/Command must preserve every independent user message as a `Txxx` node. `Stage`/`阶段` headings are containers only. Before reporting completion, it must run:

```powershell
& '.\\ES\\AI协作历程（Codex）\\Tools\\Test-ESCodexTimelineCoverage.ps1' `
  -SessionPath '<confirmed rollout JSONL>' `
  -ArchivePath '<window archive Markdown>'
```

Non-zero exit is a hard failure. The agent must report the mismatch and continue repair or mark the history incomplete; it may not substitute a stage summary, recent-message sample, or verbal assurance.

## Task delivery

The launcher may receive one bounded `-TaskPrompt` or a task that points to a handoff file. It is appended after the read-only initialization instructions and does not authorize history writes, audit-state writes, Git operations, publishing, deletion, or Unity execution unless the task prompt explicitly grants the corresponding scope. The default visible shell is `cmd.exe` using the npm `codex.cmd` entry. The new session must report the initialization result before acting on the delivered task.

### Prompt payload discipline

The immutable envelope carries a concise task packet, not a second copy of the project knowledge base. A managed Editor sender may include the responsibility, current user task, a de-duplicated project/scene/selection summary, and the required AIWarnings entry list. Each AIWarnings entry must carry its project-relative path, absolute path, SHA-256, and `Required Read` marker, but must not embed the full README, CurrentStatus, RuleIndex, P0, or domain-rule body. The sender captures every entry through bounded double-read verification, then rechecks the complete chain before it creates the task packet; a file that keeps changing exhausts the bounded retry budget and rejects the send. The receiving session validates the envelope, then reads those current source files in UTF-8 and recomputes their SHA-256 before claiming that it has read them. A later source mismatch is `sourceDrift`: report it and route from the current authority, never claim the send-time reference was freshly verified.

Explicit material that the user deliberately attaches remains an attachment and is budgeted separately. Automatic context must avoid duplicate project roots, active-selection/page identity, and large MCP tool catalogs; it is a navigation summary, not a substitute for UnityMCP observation or source verification.

Repeated `New` calls with the same `TaskKey` (or the same derived task fingerprint) are idempotent: if the marker's process is alive, the launcher returns `alreadyRunning` and does not open another window. `-ForceNew` is an explicit override and must not be used for ordinary parameter repair. `terminalStarted` proves only visible terminal creation. A launch token in history upgrades evidence to `promptObserved`; only a valid exact acceptance receipt upgrades it to `contextAccepted`. `sessionId=pending`, a visible process, or a timeout is an evidence gap, not permission to claim delivery or start another duplicate.

Managed `Resume` and `Fork` require an exact SessionId before any envelope or terminal is created. If responsibility resolution does not produce exactly one ID, the launcher fails and directs the caller to Status/Query/Find. The official picker remains a CLI capability for manual use, but it is outside the managed handoff contract because it cannot receive the ES initialization prompt after selection.

### Existing external CMD connection

The Agent workbench may discover candidates only below the currently or most-recently foreground `cmd.exe` / Windows Terminal host. Discovery is asynchronous and observational: title, PID and ancestry help a user choose a shell but never prove a Codex conversation identity. The UI must not ask a user to type `SessionId`, `RecordId` or PID.

Choosing a candidate creates a unique external-binding identity and one short-lived command. The workbench may show the command for manual execution, or after one explicit confirmation write that fixed command into the selected CMD's console input buffer. Automatic submission revalidates the claim, candidate PID, UTC process-start identity and a zero-active-child Shell state immediately before writing; it refuses a CMD that is running Codex or any other child process and never falls back to title matching, window keystrokes or a different PID. It then waits only for the matching one-time response and finalizes through the same Claim mutex.

The responder verifies both the selected CMD PID and its process start time before the registry accepts a `ClaimedExternal` record. A response from another CMD, an expired candidate or a reused PID is rejected. A connected external CMD remains process/liveness observation only after onboarding. It has no Codex `SessionId`, and cannot be advertised as resumable, focusable, closable, messageable or continuously input-injectable. Existing schema-v1 SessionId claims remain recoverable only to finish or cancel claims that were already created.

## Immutable launch envelope

Before a real launch, the launcher writes one create-only JSON envelope under `%LOCALAPPDATA%\ESFramework\CodexSessions\envelopes`. It records:

- the unique launch token, task key/fingerprint, responsibility key, and short tab title;
- mode, requested exact session ID, bounded task prompt, and fixed project root;
- branch and HEAD observed at launch time;
- every per-launch handoff snapshot path, source path, length, and SHA-256;
- the authorization boundary carried into the new session.

Schema v2 is the only accepted schema. Each launch first creates a unique directory under `%LOCALAPPDATA%\ESFramework\CodexSessions\handoff-snapshots` and copies every declared source into it with create-only semantics. The snapshot is private to that launch; later edits to `CODEX_TASK_CONTEXT.md`, a life-history work file, or any other source cannot invalidate another window. The validator treats schema v1, snapshot loss, or snapshot SHA-256 drift as a hard non-zero failure. Source-file changes after snapshot creation are reported as `sourceDrift` only and are never permission to switch context silently.

Legacy v1 envelopes are never rewritten. `Convert-ESCodexStateToV2.ps1` moves them into a recoverable local `legacy-v1` quarantine, updates registry and launch-state references, and marks affected records `requiresV2Resume`. Such a live process is not accepted as an idempotent running task. The exact session must be closed and resumed to generate a new v2 envelope and private snapshot.

Snapshot creation, envelope creation, launch-state registration, and process launch remain inside the per-task mutex. A source that changes while being copied fails snapshot creation instead of producing a mixed handoff. The receiving session consumes the snapshot path recorded in its envelope, never the mutable source path.

### Creation and collision rule

Every newly created local artifact in the managed delivery chain, including operation directories, command wrappers, envelopes, snapshots, receipts, registry/message records, and editor workspace state, must have a unique stable identity before it is exposed to a reader. Creation uses create-only semantics; an existing target is a collision or a separately owned artifact, never permission to overwrite it. Mutable authoritative state additionally uses a bounded cross-process mutex and revision compare-and-swap: a stale writer rejects its update, reloads the persisted authority, and tells the user that its local change was not committed. A writer stages bytes to a unique temporary path, flushes to disk, commits atomically where replacement is intended, then re-reads the committed artifact to verify identity/version or hash. On any failure, the final target is not reported as created; temporary data is cleaned only when its ownership is proven, while ambiguous or transaction evidence is retained for diagnosis.

The initial prompt points to that envelope and to `scripts/Test-ESCodexLaunchEnvelope.ps1`. The receiving session must run the validator before consuming the handoff. Branch or HEAD drift is always reported and becomes a hard failure when `-StrictGit` is requested. Envelopes and the local session registry are navigation evidence, not source acceptance or new authority.

## Acceptance lifecycle

The envelope is a one-time acceptance gate, not a continuous runtime lease.

Launcher evidence uses `Prepared`, `TerminalStarted`, `PromptObserved`, `ContextAccepted`, and `Failed`. The launcher writes a pending registry record immediately after terminal creation, then waits up to 60 seconds for history and receipt evidence. If Codex returns before acceptance, its wrapper atomically writes an exit diagnostic and the launch becomes `Failed`. If the deadline expires, `startupTimedOut=true` preserves the strongest observed phase without claiming success or failure.

Use exactly these externally reported states:

- `ValidatedNow`: the validator ran during the current operation and the v2 envelope and private snapshots passed.
- `AcceptedContext`: first acceptance passed earlier; the current conversation continues from its receipt, accepted transcript, and already-read context because the envelope is now unavailable.
- `HardFailure`: first acceptance failed, the exact launch token mismatched, or an existing envelope/private snapshot was altered.

1. Before consuming a handoff, the launched session validates the v2 envelope, private snapshot hashes, project root, and exact launch token.
2. A successful validation creates a create-only receipt under `%LOCALAPPDATA%\ESFramework\CodexSessions\acceptance-receipts` containing the launch token, envelope path and hash, project root, and accepted snapshot hashes.
3. After acceptance, the current conversation may continue from its already accepted transcript/context if the envelope is later unavailable. It must report that the artifact is unavailable, must not substitute another handoff source, and must not claim fresh file verification.
4. If the envelope still exists but its contents or private snapshots drift, validation remains a hard failure; an old receipt cannot bless altered artifacts.
5. A new process boundary—New, Resume, or Fork—always receives a new launch token and envelope and must complete first acceptance again. An old receipt cannot authorize a new launch.

## Responsibility registry

Before process launch, the launcher has a launch token and task identity; after launch it records project, responsibility, title, process/window identity, envelope, snapshot, and a possibly pending SessionId in `%LOCALAPPDATA%\ESFramework\CodexSessions\sessions.json`. When the exact SessionId becomes available, the same launch-token record is resolved instead of creating a competing record.

- Exact `Resume` and `Fork` may restore this metadata.
- `Resume` or `Fork` with only a responsibility key may select a session when exactly one registry entry matches the fixed project and responsibility. Multiple matches are reported and require an explicit session ID.
- Caller-supplied `-ResponsibilityKey` and `-TabTitle` override restored presentation metadata.
- Registry corruption is a hard launcher error; the launcher must not guess or overwrite an unreadable registry.
- Registry identity never authorizes history writes, source edits, Git operations, Unity execution, publishing, or deletion.

### Authoritative state and observations

`sessions.json` schema v2 is the unique local authority for session, responsibility, task, process, terminal presentation, launch token, envelope, and lifecycle identity. Every real New, Resume, or Fork writes a record immediately after terminal creation, before the bounded evidence wait. Unaccepted records remain `PendingPrompt` or `PendingAcceptance`; only `ContextAccepted` may become `Registered`/`Active`. Exact launch-token and session-ID matches are merged into one record, preserving the earliest stable authority identity.

Launch-state files, visible Windows Terminal tabs, process liveness, envelopes, snapshots, and Codex history are observations. They may hydrate missing legacy registry fields through an explicit Repair action, but they never silently replace or contradict an existing authoritative identity.

`Status` is read-only and reports active, pending-prompt, pending-acceptance, launch-failed, lost, missing-process, missing-tab, ambiguous-tab, and closed states; authority gaps; orphan envelopes, snapshots, and launch-state records; and whether terminal UI observation was available. A session is routable as Active only after acceptance evidence.

`Repair` defaults to DryRun semantics. `-Apply` may resolve an exact pending SessionId, hydrate missing authority fields from its matching launch token/session/task fingerprint, or mark a dead process lost. It must not delete orphan artifacts, guess tab ownership, or perform source, Git, or history writes.

### Close identity

The preferred composite identity is `sessionId + processId + terminalWindowProcessId + WT_SESSION + windowKey + tabTitle`. SessionId and the authoritative registry select ownership; the shell's Windows Terminal host process narrows the UI window before the tab title is considered. Until Windows Terminal exposes a durable public Tab/Pane ID, title matching remains a degraded locator within one proven terminal host:

- one authoritative record plus one visible title match inside its recorded terminal host may focus or close;
- multiple registry records require an exact selector or explicit bounded `-AllMatches`;
- a missing terminal host identity or multiple visible matches inside that host never focus or close automatically;
- an exact SessionId with a missing host identity or non-unique host-local title match always fails rather than guessing.

### Routing query, responsibility binding, and presence

Query output separates two IDs:

- `recordId` / `bindingTargetId`: authoritative local registry identity used to bind responsibility or publish presence;
- `sessionId` / `messageTargetId`: exact Codex conversation identity suitable for a future supported message/resume bridge.

`Query` may return zero, one, or multiple candidates. `Resolve` requires exactly one candidate and fails on ambiguity. Current-session resolution uses this evidence order: explicit or inherited launch token, exact `WT_SESSION`, then current process ancestry. It never selects by title alone.

Responsibility binding requires one exact record and rejects a responsibility key already owned by another non-terminal record. It changes registry metadata only and does not rename, move, resume, close, or message a tab.

Presence is an optional declaration with `Unknown`, `Busy`, `Idle`, or `Waiting`, an update time, and a TTL. Expired presence is queried as `Unknown`. Process liveness is not evidence that an AI turn is idle. Windows that do not publish presence continue working normally and are reported as `UnknownAvailability`.

`Wait` polls query state for at most 60 seconds per call and returns `completed`, `timedOut`, the last route, and a suggested next poll. It does not send or inject text into another independent Codex process. Cross-window delivery requires a separate supported message bridge; routing IDs alone must never be reported as successful delivery.

Registry mutations accept an optional expected revision. The revision is checked again while holding the registry mutex; stale writers fail instead of overwriting newer responsibility or presence state. Responsibility uniqueness is also rechecked inside that critical section.

### Cooperative message mailbox

`QueueMessage` resolves one authoritative target, verifies the registry revision under a publisher mutex, and creates an immutable request plus a separately revisioned state file. An idempotency key prevents duplicate requests for the same target. States are `queued`, `accepted`, `turn_started`, `steered`, `completed`, `failed`, and `expired`.

`SendMessage` is the preferred user-facing wrapper. It does not wait for Idle before queuing a message that a trusted `Stop` hook can consume; waiting first would miss the supported busy-completion boundary. It reports one exact plan: `StopHookAtBusyCompletion`, `NextUserPromptHook`, `MailboxUntilSessionReturns`, or `MailboxOnlyUntilHookObserved`, plus whether external wake/input remains required. It never attempts UI injection or spontaneous idle wake.

This mailbox is not direct TUI injection. `queued` means stored locally only; it is not delivered, displayed, accepted, or executed. The target conversation or a future supported App Server broker must explicitly consume the request and advance its state with CAS. Direct injection remains unsupported until a managed Codex App Server control connection has real `thread/read`, `turn/start` or `turn/steer`, and completion-receipt evidence. Never use UI keystrokes, mutate Codex JSONL, or start a competing `codex resume` process to simulate delivery.

The project may configure trusted `Stop` and `UserPromptSubmit` hooks to consume one queued message at supported turn boundaries. `Stop` can request an automatic continuation after a busy turn finishes; `UserPromptSubmit` can attach a queued message to the next user turn. Hooks claim with message-state CAS and include the message ID plus an instruction to publish `completed` or `failed` afterward. A repeated `Stop` with `stop_hook_active=true` never consumes another message, preventing continuation loops.

Hook files are non-managed project hooks. Codex requires the user to review and trust their exact hash through `/hooks`, and existing sessions may require reload. Configuration presence therefore proves only `Configured`, not `Trusted`, `Loaded`, or `Delivered`. Hooks cannot spontaneously wake a completely idle standalone TUI without a new prompt or an in-flight turn reaching `Stop`.

When a trusted hook actually runs for one exact registered session, it writes a bounded activation receipt under `hook-activations`. The receipt binds `recordId`, `sessionId`, event time, the current `hooks.json` SHA-256, and the receiver-script SHA-256. Query/Status may report `LoadedAndObserved` only while identity and both hashes still match and the receipt is recent. Configuration or script drift invalidates the receipt automatically; the user must review the new hook hash and reload before automatic delivery is reported active again.

The mailbox enforces bounded input and local quotas: 8,000 characters per message, 100 pending messages per target, 1,000 total requests, and 16 MiB total message storage. `MessageRepair` is read-only by default. `-Apply` may persist TTL expiry; retained terminal-message deletion additionally requires `-DeleteTerminalMessages`, validates every exact path remains below the message root, and never deletes non-terminal messages.

### Commercial readiness profiles

`Doctor` is a read-only, versioned operational report. Its issue codes are stable automation inputs; human-readable text is explanatory only. It separates four claims:

- `codeReady`: manifests, PowerShell parsing, and Hook configuration are valid.
- `commercialBaselineReady`: the supported Windows environment, schema-v2 authority, zero applicable safe repairs, and cooperative mailbox are ready.
- `fleetOperationalReady`: every eligible registered session has recent identity-and-hash-bound Hook activation evidence.
- `managedDirectDeliveryReady`: a managed App Server path has proven identity and delivery receipts.

The commercial baseline is deliberately the bounded cooperative mailbox. Unsupported direct injection into an existing standalone Windows TUI and spontaneous idle wake are host limitations, not hidden requirements for that profile. They remain explicit informational boundaries and must never be advertised as available. `SelfTest` adds the complete Pester suite and App Server probe before allowing a commercial-baseline claim.

`SmokeTest` provides real-write evidence without using `%LOCALAPPDATA%\ESFramework\CodexSessions`. It creates a unique directory below the operating-system temporary root, executes registry creation, responsibility binding, Busy-to-Idle waiting, cooperative message enqueue, Stop-Hook acceptance, completed receipt, restart reload, and Repair idempotence, and removes the directory only after every stage passes. A failed run retains its isolated artifacts and reports their exact path for diagnosis. This mode never launches, resumes, closes, renames, or messages a real Codex window.

### Acceptance request/reply protocol

`RequestAcceptance` is the bounded completion handoff. It resolves the requester and exactly one acceptance responsibility, polls route state, and does not publish while the target is `Busy` or `Pending`; each poll is observable in the result. After enqueueing, it waits no more than 60 seconds per call for a message with `inReplyToMessageId` equal to the request. `ReplyAcceptance` creates that correlated response targeted at the original requester. A timeout is not a failure or delivery claim; it returns the request ID and a resumable `AcceptanceStatus` command. This protocol does not wake an idle TUI or inject keystrokes.

`BindAcceptance` is the explicit opt-in for automatic completion handoff. On a `Stop` event, the trusted project Hook may create one idempotent acceptance request for the bound responsibility, but it skips auto-send when the current Turn is already consuming another queued message to prevent recursive chains. Disabling the binding is metadata-only and reversible. Automatic Hook execution remains subject to the same trust, reload, Busy/Pending wait, and cooperative-mailbox limits as manual requests.

`BrokerStatus -ProbeAppServer` performs a bounded read-only stdio handshake (`initialize`, `initialized`, `thread/loaded/list`) and reports capabilities. On Windows, a successful stdio probe does not imply daemon lifecycle support or safe attachment to standalone TUI processes. Keep `directExistingTuiInjectionSupported=false` until both identity and delivery receipts are proven in the actual host mode.

Registry writes pass update data explicitly into the mutex-held callback. Never rely on dynamic scriptblock scope for launch records, responsibility, presence, close, or repair mutations. New records with no launch token, session ID, task fingerprint/key, or tab title are rejected before write. `Repair` can detect duplicate or single identity-empty placeholders, remains read-only by default, and creates a persistent create-only backup before an explicitly authorized removal. Duplicate identity-bearing authority remains manual review and is never auto-merged.

## Recovery safety

- Prefer an exact session ID supplied by Codex or confirmed by the user.
- When only topic words are known, use `Find-CodexSession.ps1`; search scores rank candidates but never authorize resume or archive writes.
- When identity is not known, use Status/Query/Find and obtain one exact SessionId. Do not use the official picker for managed delivery and do not silently choose `--last` in a multi-project environment.
- After resume or fork, refresh current repository facts. Old conversation context, archive files, and checkpoints may be stale.
- A restored short title describes responsibility only; it does not prove the resumed session still owns current files or that its old evidence remains valid.
- Never modify `%USERPROFILE%\.codex\history.jsonl` or session JSONL files.
