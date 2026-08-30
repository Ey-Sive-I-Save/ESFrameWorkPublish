# Step 00 — Authority baseline and resource binding

## AI analysis

Before any business step, the AI must distinguish authority from navigation. The current user supplies goal and permissions; TaskContext owns lifecycle/CAS; TaskFocus owns scope; AIBrain/Knowledge navigates facts; ABCD/ABCC supplies bounded capability and recovery semantics; SubAgent contracts describe child work but never self-authorize; RoutePlan freezes order. The complete binding is `authoritative-step-baseline.v1.json`.

## Execution

Read every `requiredResources` entry in the baseline, compute input hashes, create a task-scoped baseline receipt, and validate that stage ordinals are contiguous. Do not run a later stage, invent a receipt, or expand scope when a resource is missing or stale.

## Return

Return `AuthorityBaselineReceipt` with `baselineId`, resource hashes, ordered stage IDs, `skipPolicy`, authority decisions and explicit non-claims. A missing resource returns `blocked.missing-required-read`; an invalid order returns `blocked.baseline.order`; a scope mismatch returns `blocked.scope-expansion`.

## Frozen invocation timing

`RequirementIntake` first records the raw user request and authorization boundary. `TaskFocus` is then proposed exactly once from that intake before intent locking and before any Knowledge read. Once accepted, it freezes focus scope and forbidden scope. `TaskContext` is created exactly once after the focus and intent lock pass, and before Knowledge routing; its GoalRevision, RoutePlan, sourceScope, AcceptanceProfile and ContextVersion become immutable inputs for all later stages. Neither may be silently recreated or replaced. Only an explicit user GoalRevision or platform Reopen may cause a new Focus/TaskContext pair. Completion is evaluated only after evidence closeout by the platform evaluator; Skills, SubAgents and Workers cannot self-accept.
