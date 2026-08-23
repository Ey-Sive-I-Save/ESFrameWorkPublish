# Feishu task lifecycle contract

Authority: target contract bound to the registered Feishu Task v2 TaskContracts and current source.
Scope: task monitoring, dispatch, virtual-team test fixtures and controlled transitions.
StaleWhen: AICommand, TaskContract, input Schema, Worker, Task v2 SDK/API or permission model changes.
Evidence: current source plus fresh Static and Runtime receipts; source presence is not connectivity proof.

## Fixed bindings

| Command | TaskContract | Operations | Remote write |
|---|---|---|---:|
| `feishu.task.monitor` | `es.feishu.task.monitor@1` | `tasklist-list`, `tasklist-get`, `task-list`, `task-get` | No |
| `feishu.task.mutate` | `es.feishu.task.dispatch@1` | `tasklist-create`, `task-create`, `virtual-team-fixture-create` | Yes |
| `feishu.task.mutate` | `es.feishu.task.transition@1` | `task-update`, `task-complete`, `task-reopen`, `members-add`, `members-remove`, `reminder-add`, `reminder-remove` | Yes |

All contracts use a 60-second host timeout, a 15-second per-request target, at most two bounded transient retries inside the host budget, a 4 MiB output ceiling, a 20-member limit and a 20-task batch ceiling. Live behavior remains unverified until Unity-managed receipts exist.

## Lifecycle semantics

ES planning uses `Draft -> PendingApproval -> Ready -> InProgress -> Review -> Verified -> Completed`, with `Blocked`, `Cancelled` and `Reopened` branches. Feishu Task v2 remains the remote record; ES state is a normalized projection using completion time and explicitly authorized Agent status/progress fields. Never overwrite free-form descriptions to simulate a state machine.

A transition must include the task GUID and a fresh `expectedUpdatedAt`. The Worker reads the current task before mutation and rejects mismatches as `REMOTE_VERSION_CONFLICT`. This reduces lost updates but does not create an atomic server ETag where Feishu does not expose one.

Task creation uses the official `client_token` derived from stable RunId/work-item identity. Task-list creation lacks that token, so the managed Worker adds an `[ES:<hash>]` suffix and searches the exact name before creating, allowing conservative recovery after an uncertain response.

## Virtual-team fixture

`virtual-team-fixture-create` creates one isolated list named with `[ES-TEST:<hash>]` and, by default, five role tasks: Product Owner, Technical Lead, Developer, QA and Release Owner. It creates no tenant users, departments or chats. Optional members must be current-tenant IDs supplied through an approved bounded request.

The fixture never auto-deletes. Return the list/task IDs and URLs for inspection; archive or deletion needs a future separate command and is deliberately outside this contract.

## Progress and evidence

- Human Feishu changes are remote facts with remote update time.
- RunRecord, commit, compile and test receipts may support progress but cannot alone mark a task completed.
- AI may propose a transition; only the authorized transition contract writes it.
- Completion requires the task's declared acceptance evidence and a fresh remote version.
- Monitoring reports deltas, stale tasks, overdue tasks, blockers and missing evidence without rewriting source facts.

## Runtime authorization

Bind tenant/app hashes, credential source, task list/task IDs, actor, operation, normalized input hash, PlanHash, command/task/worker/schema/governance hashes, API request budget, 60-second timeout and stop conditions. Credentials remain only in managed environment or an approved Credential Manager broker.

Stop on first unclassified write failure, permission error, remote version conflict, hash drift, uncertain cancellation, sensitive leakage or unreconciled partial success. No external-write receipt may be accepted from DryRun, static compilation or a nonterminal RunRecord.
