---
name: es-module-lifecycle
description: Classify and govern ESFramework modules that are proposed, scaffolded, experimental, partially implemented, integrating, awaiting verification, stable, deprecated, or archived. Use only when the current user explicitly requests a formal module audit, audit recording, audit continuation, a maturity matrix, or an audit checkpoint. Do not trigger for ordinary technical review questions such as "合理吗", "商业级吗", "需要修改吗", or "评价这份结论"; handle those as review-only unless formal audit intent is explicit.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# Govern ES Module Lifecycle

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and deny-by-default; capability visibility never grants permission. Use AIBrain `planTask`, the matching AICommand, and the current TaskContract before any write or external operation.

## Responsibility-specific static acceptance

- Profile: `session`
- Custom checks: `consistency-cache, change-boundary, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- Scope and authority are checked before execution; stale or missing evidence blocks the task.
- Execute only through AIBrain planTask and the matching AICommand; direct execution is denied.
- Record evidence for positive, invalid-input, denied-expansion, repeat-idempotency, and interruption-recovery cases.

Classify the module from current implementation and evidence. Never infer completion from directories, type names, TODO counts, documentation claims, or an external report.

## Workflow

1. Read the AIWarnings start files, the module-lifecycle warning, and the target domain rules selected by `RuleIndex`.
2. Select `检查_模块成熟度与半成品影响_AI命令.md` when the user has not granted a narrower execution command. Treat it as read-only.
3. Define the module boundary from its authority, entry points, registrations, consumers, and release path. Do not equate a folder with a module.
4. Inspect source, configuration, serialized assets, initialization, tests, documentation, generated artifacts, and release integration relevant to that boundary.
5. Assign exactly one maturity state: `Proposed`, `Scaffolded`, `Experimental`, `Implementing`, `Integrating`, `Verifying`, `Stable`, `Deprecated`, or `Archived`.
6. Record `Blocked` separately with the exact missing authority, dependency, decision, tool, platform, or evidence.
7. Detect unfinished-code leakage: default registration, automatic initialization, stable-module dependencies, serialized references, public compatibility claims, empty success paths, swallowed errors, and unrecoverable migrations.
8. Build an evidence matrix without upgrading `.csproj`, Console, Test Runner, PlayMode, Profiler, Player, IL2CPP, provider, or release evidence into another layer.
9. Recommend the smallest reversible action that satisfies the next transition gate. Do not implement, delete, migrate, stage, or publish without matching user and AICommand authority.
10. If authorized changes are made, preserve unrelated work, run `$es-utf8-guard`, and invoke `$es-unity-compile` or `$es-release-acceptance` only for evidence actually required by the target state.
11. Establish `currentUserExplicitlyRequestedFormalAudit` from the current user message before loading this Skill. It is true only for explicit requests such as “审计”, “审计并记录”, “继续审计”, “输出模块成熟度矩阵”, or “写入/恢复审计检查点”. A review, explanation, feasibility judgment, or critique is never formal audit intent by itself.
12. Treat ordinary review-only requests (“合理吗”, “商业级吗”, “需要修改吗”, “评价这份结论”, “还缺什么”) as technical review. In review-only mode do not write audit state, do not emit C0–C3 governance grades, do not create or refresh checkpoints, do not ask about handoff, and do not trigger any session-operation Skill.
13. Interpret an explicit “审计” as `audit-only` when the target module is clear. Deliver the full maturity/evidence matrix, but do not write state or ask to record it unless the user explicitly says “审计并记录” or separately authorizes a checkpoint.
14. Interpret “审计并记录” as authorization to update the target module block in `ES/Documentation/Status/MODULE_AUDIT_STATE.md`; do not ask for a path or region. Interpret “继续审计” as `resume` from that same file. Ask only for the module scope when context and stored blocks cannot identify it.
15. For a checkpoint, derive a stable module key, read [references/audit-state-contract.md](references/audit-state-contract.md), inspect overlap, and update only that module block. Never write audit continuation state elsewhere.
16. On resume, treat the checkpoint as navigation rather than current truth. Recheck branch, HEAD, relevant worktree paths, latest AIWarnings, authority entry, activation, dependencies, and evidence before continuing.
17. For a completed full audit workflow, evaluate the collaboration workflow against `AI协作历程与模块审计_商业可行性验收标准.md`. Do not call it commercially viable from source or one successful audit alone.
18. Offer handoff only when `currentUserExplicitlyRequestedFormalAudit=true` and the complete audit matrix has actually been delivered. Append the standard handoff offer exactly once. A Skill-trigger decision, long answer, maturity state, blocked item, or suggested next step never satisfies this gate. If the user accepts, generate a directly copyable new-AI prompt; do not generate it automatically.

## Decision rules

- Keep an unstarted direction in `Proposed`; do not create empty runtime structure merely to show progress.
- Require `Scaffolded`, `Experimental`, and `Implementing` code to remain compilable and explicitly isolated from default production activation.
- Do not mark `Integrating` until the main path and failure, cancellation, teardown, or rollback paths exist.
- Do not mark `Verifying` while feature scope is still expanding.
- Mark `Stable` only for the exact scope, platform, and evidence layers that passed.
- Downgrade or block a previously stable scope when current evidence reveals a regression or incomplete migration.
- Prevent stable modules from depending on experimental modules unless an explicit reviewed boundary isolates the dependency.
- A checkpoint write authorizes only the named state document region. It does not authorize source implementation, migration, deletion, staging, commit, push, Unity execution, or release operations.
- Mark a checkpoint stale when its branch/HEAD, relevant worktree fingerprint, authority entry, activation path, dependency boundary, or evidence layer no longer matches current facts.

## Continuation modes

- `review-only`: answer the technical question only; no audit state, maturity matrix, checkpoint, handoff offer, or session operation.
- `audit-only` / explicit “审计”: return the full findings and evidence matrix; write nothing and do not ask about recording unless separately authorized.
- `audit+checkpoint` / “审计并记录”: write the fixed state file's target module block without asking for a path.
- `resume` / “继续审计”: read the fixed state file, report stale fields, refresh facts from source, then continue only within the current user and AICommand authority.

## Required output

Return the module boundary, maturity state, blocked reason, committed scope, authority entry, activation mode, upstream dependencies, downstream consumers, unfinished-code leakage, evidence matrix, smallest next transition action, checkpoint status (`not-requested`, `offered`, `written`, `stale`, or `refused`), commercial-feasibility level/evidence, and the exact resume entry when one exists. When the full workflow is complete, end with the one-time handoff offer.
