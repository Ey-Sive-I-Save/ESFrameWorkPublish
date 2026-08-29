# ES Task Collaboration Contracts

This folder contains the static Parent/Child collaboration contracts frozen in
`Documentation/AIKnowledge/AIBRAIN_ENTRY.md`. It is an adapter layer over the
existing `TaskContextRuntime` and `AutomationCenter`; it is not a scheduler,
new Agent lifecycle, or completion authority.

The contracts are deliberately split:

- `es-task-collaboration-plan-v1.schema.json` describes a parent decomposition,
  dependencies, concurrency budget, and deterministic aggregation strategy.
- `es-task-child-task-registry-v1.schema.json` is only a parent/child index.
  Child lifecycle, CAS, RunId, and idempotency remain owned by TaskContext and
  Automation contracts.
- `es-task-lease-cas-v1.schema.json` records a short-lived worker claim and a
  read-only CAS observation. A stale or expired observation sets
  `canSubmitResult=false`; it never changes business task state.
- `es-task-result-envelope-v1.schema.json` carries candidate output and evidence
  references. It has no `Accepted` or `Completed` field and cannot self-approve.
- `es-task-parent-aggregation-v1.schema.json` records deterministic child
  observations, conflicts, quarantined attempts, and a required handoff to
  `completionDecision`. It never declares task completion.
- `es-aitalk-message-v1.schema.json` defines the structured projection of an
  AITalk Markdown message. It binds conversation identity, task/context
  revisions, visibility, payload hash, and external evidence references without
  granting execution or completion authority.

`ESTaskCollaborationContracts.psm1` provides pure contract constructors and a
deterministic aggregator. Lease/CAS admission is a prerequisite to submitting a
ResultEnvelope; late, cancelled, stale, or expired results remain isolated.

`ESAITalkAggregation.psm1` provides deterministic AITalk de-duplication,
conflict/quarantine handling, private-view filtering, stale binding isolation,
and an explicit projection into candidate `ResultEnvelope` values when a valid
Lease/CAS claim is supplied. It never writes TaskContext state.

`Invoke-ESAITalkSessionAggregation.ps1` is the practical entrypoint for an
existing `Assets/Plugins/ES/AITalk/Sessions/<id>` folder. It reads all Markdown
messages, derives stable structured identities, and prints the aggregation JSON.
Pass `-WriteConsensus` only when the caller explicitly wants a candidate
projection written to `Consensus/当前共同意见.md`; the projection is never a
completion decision.

`Test-ESAITalkStaticPerformance.ps1` runs one warmup plus a bounded number of
steady-state host replays and checks the measured baseline against
`ES/Output/Performance/aitalk-performance-budget.json`. This is an honest
PowerShell/file-system baseline; it does not claim Unity, Player, IL2CPP, or
zero-GC performance.

`Invoke-ESAITalkHumanLightFlow.ps1` is the low-friction interaction entrypoint.
It performs the project aggregation automatically and returns
`humanActionRequired=false` for a normal candidate. It emits only a short
`actionItems` list for conflicts, review states, user-decision points, or a
five-round interruption; it never selects a business outcome on the user's
behalf.

`Invoke-ESAITalkProjectAggregation.ps1` scans the whole AITalk Sessions root,
indexes every valid session, and returns one deterministic project-level
aggregation (`es-aitalk-project-aggregation-v1`). Use `-MaxSessions` to keep the
scan bounded; the result exposes `discoveredSessionCount` and
`sessionLimitReached`, and a session missing `Messages/` is surfaced as
`needs-review` instead of being silently skipped. `Invoke-ESAITalkRoundGate` enforces the collaboration limit: an
explicit consensus stops early, a user-decision request interrupts immediately,
and five rounds without either result in `interrupted/MAX_ROUNDS_EXCEEDED`.

Run `Test-ESTaskCollaborationContracts.ps1` for the bounded static replay. The
replay also checks `ABCD.Dynamic` parity against the six registered ABCC kernel
capabilities. It does not start Unity, Worker, MCP, external Agent Runtime, or
real multi-process concurrency. Run `Test-ESAITalkAggregation.ps1` for the
AITalk schema, idempotency, ordering, privacy, stale-context, conflict, and
non-completion negative cases.
