---
name: es-skill-session-refresh
description: Incrementally refresh an active ES AI session when the user says the current project understanding is outdated, asks to refresh or re-understand Skill capabilities, or when queued work, session resume, Skill, governance, Catalog, Knowledge route, or resource changes may cause drift. Use without requiring the user to name this Skill; compare hashes and current task routes first, read only selected changed material, and never reread the whole Skill portfolio into context.
---

# ES Skill Session Refresh

Maintain a task-scoped capability snapshot for a long-running AI window. This Skill detects Skill and routing drift, identifies the smallest affected capability set, and asks the caller to read only the changed material required by the current objective.

## Skill 使用披露

使用本 Skill 时，在首次用户可见进度更新中说明它用于刷新长运行 ES AI 会话的 Skill 能力与路由快照；最终答复只列出本轮实际执行或实际影响工作的 Skill。该披露不代表授权、执行或验收证据。项目级披露规范见 `AGENTS.md` 与 `.agents/README.md`。

## Boundaries

- Read-only by default. It may write only its own project-relative snapshot/report evidence paths; it never edits business Assets, grants authority, runs a Worker, starts Unity, or replaces `planTask`/`TaskContract` authorization.
- Compare metadata and hashes first; do not reread every Skill document on every queue update.
- A cache hit proves unchanged bytes only. It does not prove that the current objective is authorized or that old conclusions remain valid.
- Any change to a Skill's `SKILL.md`, `governance.json`, `agents/openai.yaml`, `static-replay.manifest.json`, route index, Catalog, or required reference invalidates the affected session binding.

## Workflow

1. Treat user phrases such as “你的理解已经过时”, “刷新一下技能理解”, “重新理解当前项目提供的 Skill” or their English equivalents as an explicit refresh intent. Establish a project-relative session identity and objective; record the current AIBrain `planHash` when one exists, and never use a session snapshot as permission.
2. Run `scripts/Invoke-ESSkillSessionRefresh.ps1 -Mode Build` to create a compact snapshot. The snapshot hashes the Resource Index, Catalog, Knowledge Index, and Skill metadata/resources without loading all documents into the model context.
3. On a queue update, resume, or explicit refresh, run `-Mode Compare -BaselinePath <snapshot>`. Classify results as `unchanged`, `added`, `removed`, `metadata-changed`, `resource-changed`, `route-changed`, or `index-changed`.
4. Pass objective route keys with `-RouteKeys skill,session,...` when available. The script intersects changed Skills with each Skill governance `routeKeys` and the discovery policy. Use `-DiscoveryMode Operational` for an active task, `CapabilityIndex` for candidate capability discovery, and `Audit` only for governance inspection. Without route keys, the compare result is `blocked` with `nextAction=replan`; it never selects the whole portfolio.
5. Re-check Knowledge `requiredReads` and source hashes for any newly selected or changed Skill. Use `es-task-read-snapshot` when multiple files must be consumed consistently.
6. Mark the prior plan or conclusion `stale` when a bound Skill, governance hash, command contract, Knowledge source, or task read snapshot changed. Request a fresh plan; do not silently merge new rules into an old authorization.
7. Produce a refresh receipt containing the baseline hash, current snapshot hash, changed items, selected items, ignored items, and stale decisions. A receipt is evidence of discovery, not evidence that the new Skill was understood or executed.

## Change policy

| Change | Session action |
| --- | --- |
| New Skill matching current route | Read its metadata and required references, then re-plan if it affects the task |
| New Skill unrelated to current route | Record `out-of-scope`; do not load it |
| `SKILL.md` or governance change | Mark the affected binding stale and re-plan before execution |
| Route/Knowledge index change | Re-run route discovery and verify selected `requiredReads` |
| Resource script or manifest change | Re-run the Skill's static replay before relying on its result |
| Removed Skill or missing required file | Block the affected route; never fall back silently |

## Required output

Return structured data with:

```text
sessionId
baselineSnapshotHash
currentSnapshotHash
status: unchanged | refreshed | stale | blocked
changes[]
selectedSkills[]
ignoredChanges[]
invalidatedBindings[]
nextAction: none | read-selected | replan | blocked
```

Do not claim that a Skill was consumed merely because its hash was observed. Do not claim a task remains valid after a bound Skill or contract changed.

## Engineering controls

- Identity: bind every snapshot and refresh receipt to the project root, session identity, task ID, and PlanHash when supplied.
- Authority: this Skill is read-only and never grants permission; changed bindings require a fresh AIBrain plan before execution.
- Risk: reject path expansion outside the project and classify stale, missing, or contradictory metadata explicitly.
- Observability: record baseline hash, current hash, changed items, selected items, ignored items, and stale decisions.
- Recovery: compare operations are deterministic and can be repeated without mutating the baseline or project assets.
- Performance: hash metadata first and read only the objective-relevant changed Skill resources.
- Lifecycle: candidate, blocked, deprecated and hidden Skills are not selected by Operational refresh; CapabilityIndex returns metadata only.
- Compatibility: preserve existing ES AICommand, Knowledge, Catalog, and TaskContract contracts.
- Supply-chain: source paths and hashes are project-relative and must be verified before they influence routing.

The static replay cases are explicitly bounded: `normal-input`, `invalid-input`, `denied-expansion`, `repeat-idempotency`, `hash-change-cache-invalidation`, `interruption-recovery`, and `deterministic-output`. The Skill has a read-only write scope, zero change budget, and a stop condition on invalid authority, path expansion, or stale binding. Its bounded output policy returns only the session delta and required reads, never unrestricted project content.

## Resources

- `scripts/Invoke-ESSkillSessionRefresh.ps1`: deterministic Build/Compare snapshot and delta report.
- `scripts/Test-es-skill-session-refresh-StaticReplay.ps1`: project StaticDeepReplay adapter.
- `references/session-refresh-contract.md`: snapshot fields, invalidation rules, and receipt semantics.
