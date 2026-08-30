---
name: es-web-generation-round-03-task-context
description: Create one ES TaskContextRuntime task from a confirmed Round 02 FocusContext, binding immutable GoalRevision, RoutePlan, acceptance profile, source scope, and CAS identity before Knowledge or WebPageStudio generation.
---

# ES Web Generation Round 03 — TaskContext

## Purpose

Round 03 turns a confirmed FocusContext into one platform-owned TaskContextRuntime task. It freezes task identity, GoalRevision, RoutePlan, acceptance profile, source scope, evidence claims, and idempotency before any Knowledge routing, SubAgent/ABCD design, HTML generation, Runtime, network, Unity, Git, or release action.

## SmallTool controls

- Read only the confirmed Round 02 receipt plus explicit GoalRevision, RoutePlan, contract, and source-scope files.
- Invoke the platform TaskContextRuntime API; never reproduce its state machine or write event files directly.
- Require stable TaskId, route/goal hashes, acceptance profile, and idempotency key; reject path escape, stale FocusContext, scope expansion, and CAS conflicts.

## Required reads

Read project `AGENTS.md`, `ES/AISpace/README.md`, the Round 02 FocusContext receipt, `ES/Automation/TaskContextRuntime/ESTaskContextRuntime.psm1`, `es-task-context-runtime` Skill, and [`references/round-03-task-context-contract.json`](references/round-03-task-context-contract.json). Read the RoutePlan and GoalRevision contracts referenced by the invocation.

## Workflow

1. Verify Round 02 is `accepted`, `status=accepted`, and contains FocusContext identity plus valid SHA-256 hashes.
2. Verify GoalRevision and RoutePlan are immutable, project-relative, and match the requested task scope; do not synthesize a route or goal from a template.
3. Bind the FocusContext identity (`focusContextId`, revision, proposal hash, scope hash) to `New-ESTaskContextTask`.
4. Create exactly one task through the platform-owned API with an idempotency key. A repeat must return the original state, not append a duplicate event.
5. Emit a Round 03 receipt containing task identity, revision/context version, route/goal/source hashes, AI analysis, execution result, and non-claims.
6. Stop. Round 04 may begin only after this receipt is read; Knowledge, design, SubAgents, ABCD, and page generation are forbidden here.

## Hard controls

- A pending or rejected FocusContext cannot create a task.
- Focus, GoalRevision, RoutePlan, acceptance profile, and source scope are independently hash-bound; mismatch is a hard object-level block.
- `TaskContextRuntime` completion is not implied by task creation; `Active+Live` is only task initialization evidence.
- No automatic chaining, runtime lease, network request, Unity process, Git write, release, or deletion is performed.

## Engineering controls

- Platform owner: `ESTaskContextRuntime.psm1`; this Skill is only an invocation adapter.
- Change boundary: one TaskId, one create event, one bounded receipt; all mutations use TaskRevision/ContextVersion CAS.
- Recovery: repeat idempotency returns the original state; orphan receipts and detached output are non-authoritative.
- Supply chain: RoutePlan, GoalRevision, verifier registry, and evidence contracts are reread and hash-bound by the platform.
- Compatibility: preserve existing TaskContextRuntime entry points and status meanings; do not fork the state machine.

## Return contract

Return `recordType=TaskContextCreationReceipt`, `roundId`, `stageId`, `status`, `taskId`, `taskRevision`, `contextVersion`, `focusContextId`, `focusRevision`, `focusProposalHash`, `focusScopeHash`, `goalRevisionHash`, `routePlanHash`, `requestedSourceScope`, `sourceScopeHash` (null until the separate VerifySources step), `acceptanceProfileId`, `idempotencyKey`, `aiAnalysis`, `execution`, `decision`, `returnReceipt`, and `nonClaims`.

## Expected use in the complete workflow

Round 03 is the first platform task boundary. Later rounds use its stable task identity and CAS pair to route Knowledge, plan design, submit evidence, and close static artifacts. It prevents downstream stages from presenting an untracked plan or a detached receipt as authoritative task state.

## Skill 使用披露

遵循项目根 `AGENTS.md` 和 `.agents/README.md` 的 Skill 使用披露规范。使用本 Skill 不授予 Runtime、网络、Unity、Git、删除或发布权限。
