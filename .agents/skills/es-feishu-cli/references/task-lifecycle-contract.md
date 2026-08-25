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

All contracts use a 60-second host timeout, a 15-second per-request target, a 20-attempt network ceiling, a 4 MiB output ceiling, a 20-member limit and a 20-task batch ceiling. Reads and `client_token` task creation may use at most two transient retries. Writes without a server idempotency token do not retry; a lost response becomes `UNCERTAIN_REMOTE_RESULT`. Live behavior remains unverified until Unity-managed receipts exist.

## Lifecycle semantics

ES planning uses `Draft -> PendingApproval -> Ready -> InProgress -> Review -> Verified -> Completed`, with `Blocked`, `Cancelled` and `Reopened` branches. Feishu Task v2 remains the remote record; ES state is a normalized projection using completion time and explicitly authorized Agent status/progress fields. Never overwrite free-form descriptions to simulate a state machine.

A transition must include the task GUID and a fresh `expectedUpdatedAt`. The Worker reads the current task before mutation and rejects mismatches as `REMOTE_VERSION_CONFLICT`. This reduces lost updates but does not create an atomic server ETag where Feishu does not expose one.

Every live dispatch or transition uses a new InvocationId and includes `dryRunEvidenceRunId`. The referenced run must be an accepted DryRun from the same TaskContract, Worker and Schema, no older than 30 minutes, with the same normalized operation input and intact request/output hashes. A DryRun cannot authorize a changed target, payload or source hash.

Task creation, task-list membership and member transitions may use `claimedRoles` instead of raw `members`. Each entry contains only a stable local `roleId` and the requested Feishu task role. C# resolves it from the same-AppId local identity store only when the owner enabled `allowTaskAssignment`; callers may not mix the two identity sources. Resolution is part of the normalized DryRun input, so a changed/released role invalidates prior evidence.

Task creation uses the official `client_token` derived from stable RunId/work-item identity. Task-list creation lacks that token, so the managed Worker adds an `[ES:<hash>]` suffix and searches the exact name before creating, allowing conservative recovery after an uncertain response.

## Virtual-team fixture

`virtual-team-fixture-create` creates one isolated list named with `[ES-TEST:<hash>]` and, by default, five role tasks: Product Owner, Technical Lead, Developer, QA and Release Owner. These are role-labelled work items, not identities. It creates no tenant users, departments, chats or member assignments.

The fixture never auto-deletes. Return the list/task IDs and URLs for inspection; archive or deletion needs a future separate command and is deliberately outside this contract. Creating role-labelled tasks remains distinct from assigning locally claimed people to those tasks.

## Progress and evidence

- Human Feishu changes are remote facts with remote update time.
- RunRecord, commit, compile and test receipts may support progress but cannot alone mark a task completed.
- AI may propose a transition; only the authorized transition contract writes it.
- Completion requires the task's declared acceptance evidence and a fresh remote version.
- Monitoring reports deltas, stale tasks, overdue tasks, blockers and missing evidence without rewriting source facts.
- `task-list` supports `includeDetails=true` with `pageSize<=10` to retrieve per-task status/progress within the request budget; otherwise it returns the paged summary projection and continuation token.

## Runtime authorization

Bind tenant/app hashes, credential source, task list/task IDs, actor, operation, normalized input hash, PlanHash, command/task/worker/schema/governance hashes, API request budget, 60-second timeout and stop conditions. Credentials remain only in managed environment or an approved Credential Manager broker.

Stop on first unclassified write failure, permission error, remote version conflict, hash drift, uncertain cancellation, sensitive leakage or unreconciled partial success. No external-write receipt may be accepted from DryRun, static compilation or a nonterminal RunRecord.
