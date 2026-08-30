---
name: es-ai-abc-core
description: >-
  Design and validate the independent ES AI ABC Adapter Core (ABCC): translate
  A intent into B capability offers, normalize evidence back to A, and preserve
  every ABCD Dynamic kernel capability. Use for semantic adapter contracts,
  core/part boundaries, capability negotiation, evidence-gated completion, or
  reviewing whether a domain part can safely consume the core.
---

# ES AI ABC Core (ABCC)

## Overview

ABCC is an independent semantic adapter core. It is not a section of
`es-agent-mechanism-replication`, and it does not replace `ABCD (Dynamic)`.
The research Skill and its Knowledge remain provenance only; the files listed
below are the authority for ABCC contracts.

## Formal modes

- **ABCD (Dynamic)** is the original independent, broad, adaptive system.
- **ABCC (Core)** is an independent A↔B semantic adapter that exposes all six
  ABCD kernel capabilities through stable contracts.
- **ABCP (Part)** is a bounded domain part that references ABCC by IDs and
  contracts; it does not copy the Core text.

ABCC+ABCP is a focused profile, not a hidden mutation of ABCD. A Part may
fallback to ABCD only through an explicit fallback contract.

The generation core exposes three explicit objectives: **three generation
objectives** (`creative-divergence`, `engineering`, `stable`). **Visible creative
divergence** is required: creative candidates are not silently hidden before
selection; they are ranked by an explicit player-delight score and expose a
default recommendation. **Generation-before-audit separation** keeps
`core-high-risk` as a final audit profile rather than an early creative-pruning
rule. Graph/Workbench remains candidate-only and is never final authority.

ABCD is creation-first for redesign work: when the goal is a rework, the
first deliverable must be newly generated mechanism candidates, not a summary
of the existing design and not an audit-only verdict. The collaborator may
provide constraints, preferences and forbidden changes, but the Skill owns
seed-axis generation and selection; the host AI must not silently choose the
player's starting concept. Audit, patch planning and implementation are
downstream stages applied to the newly created candidate.

Static acceptance markers: three generation objectives; visible creative
divergence; generation-before-audit separation.
The exact marker `visible creative divergence` is required for acceptance.

## Dynamic InnovationRun (mandatory after requirement lock)

ABCD must create a task-scoped `InnovationRun` after the requirement is
confirmed; this is an executable orchestration contract, not a guidance note
or a permanent experience document. The run is bound to the current
TaskContext/goal revision and must expose its state and receipts.

The run must materialize at least these stages:

1. requirement-facts (hard constraints, editable goals, forbidden substitutions, unknowns)
2. player-outcomes (0-10s, 10-60s, 1-3m and long-term signals)
3. lexical-deanchor (at least five mechanism interpretations for ambiguous verbs)
4. seed-divergence (ABCD must generate and select at least three independent mechanism axes; external seed hints are candidates/constraints only)
5. tree-expansion (2-4 concrete children per selected parent)
6. global-convergence (re-score the whole candidate, not only the new branch)
7. interaction-graph (record cross-mechanic links, redundancy and dead mechanics)
8. adaptive-weighting (derive next-branch weights from measured gaps)
9. player-replay (first-use and repeated-use scenarios)
10. counterplay-audit (warnings, interrupts, escapes and punish windows)
11. complexity-prune (remove mechanisms without a distinct player decision)
12. candidate-tournament (retain diverse Pareto winners and rejected reasons)
13. final-decision (scope, residual risk, non-claims and evidence references)

During tree expansion, every round must carry a parent candidate, concrete
change, player-acceptability observation, interaction delta, keep/discard
decision and discard reason. The next round must consume the retained
candidate content; a synthetic round counter or repeated template text is not
iteration evidence. `nextBranchWeights` must be recomputed after global
convergence and must identify the gap that caused each weight.

The machine output is task-scoped and must include `stagePlan`,
`divergenceTree`, `convergenceHistory`, `interactionGraph`, `branchWeights`,
`rejectedBranches`, and `finalDecision`. Static generation may prove contract
shape and deterministic replay only; it may not claim that an external model,
Unity or players executed the stages.

Weights are execution inputs, not report decoration. Before seed expansion and
before every subsequent model round, the Skill must recompute weights from the
observed player, novelty, counterplay and complexity gaps, persist a
`weightHistory` entry, and pass the current weights plus `nextBranchesPerRound`
to the model invoker. The allocated fan-out must affect the actual number of
requested children (bounded to 2–4); a run that computes weights but always
requests the same fan-out is non-compliant.

Static acceptance markers: task-scoped InnovationRun; global convergence;
adaptive branch weighting; player-first-use gate.

### External invocation is mandatory

Any external caller that claims to use ABCD, including the `shallow-fast`,
`core-high-risk`, and `full-depth` acceptance profiles, must invoke this Skill
and the `InnovationRun` state machine. A host may not emulate ABCD by copying
its report template, calling a raw model, or emitting a synthetic passed
receipt. The invocation must produce a task-scoped receipt containing the
resolved Skill name/version, mode profile, run id, executed stage ids, stage
usage, state-machine transition evidence, and final claim level. Missing or
synthetic invocation evidence is `ABCD_SKILL_INVOCATION_UNPROVEN` and caps the
result at `unverifiable`; it cannot be reported as ABCD acceptance.

The three profiles differ in budget and audit depth, not in whether ABCD runs:
each profile must execute its registered ABCD stages, obey stage order, emit
non-empty stage evidence, and pass through the same authority, evidence and
completion gates. A fast profile may be bounded, but it is never a bypass.

Every profile must execute the same minimum ABCD skeleton: multi-branch
divergence, explicit counter-argument, developer/strategy/test regression,
mode-specific gate, and complete base evaluation. The profile selects depth and
budget; it does not remove these stages. Every base dimension must be scored
independently and show real high/low separation. Uniform defaults, missing
dimension masking, or allowing mode/ABCP scores to hide a failed base layer is
non-compliant and caps the claim.

### Discoverability authority

ABCD discoverability has one route chain: resolve a single stable route through
`.agents/SKILL_ROUTE_ALIASES.zh-CN.json` and
`.agents/scripts/Resolve-ESChineseSkillRoute.ps1`, then execute the project
Skill at `.agents/skills/es-ai-abc-core/SKILL.md` and its registered state
machine. The mode registry owns stable mode identities and the Core contract
owns capability semantics. Menus, catalogs, caches, UI labels and global Skill
directories are navigation inputs only; they cannot authorize execution or
replace the project Skill. Ambiguous routes require replan, and a missing route
is `NoSkillRoute` rather than a guessed fallback.

The executable contract is `ES/Automation/Contracts/es-ai-abc-innovation-run-v1.schema.json` and the state machine is `ES/Automation/ABCD/ESABCInnovationRun.psm1`. A compliant run must use the state machine; emitting the fields without executing legal transitions is non-compliant. The state machine owns stage order, branch parent integrity, model/evaluation budgets, convergence, adaptive weights and final-decision prerequisites.

No stage may be advanced with an empty output. `Move-ESABCInnovationRun` must
reject `INNOVATION_RUN_STAGE_OUTPUT_REQUIRED` unless the current stage has a
non-empty evidence payload or its executable artifact is present (for example,
the divergence tree, convergence history, or computed weights). Reaching the
final stage therefore proves that every preceding stage produced an observable
result; a loop that only increments `stageIndex` is non-compliant.

Seed authority is explicit: callers may provide `SeedBranches` as optional
ideas and `SeedConstraints` as boundaries, but may not treat them as the
selected axes. During `seed-divergence`, the Skill must invoke its model
selector, select 3–7 axes, record `selectionAuthority=ABCD`, and only then
materialize seed branches. If the selector is absent or returns fewer than
three axes, the run fails with `INNOVATION_RUN_SEED_SELECTION_COUNT_INVALID`.
This prevents the host AI from silently making the player's design choice.

### Stage resource allocation is executable

The run must carry `resourceBudget.stageBudgets` and `resourceBudget.stageUsage`,
not only a global call limit. Deep stages receive explicit independent quotas:
`tree-expansion`, `global-convergence`, `player-replay`, `counterplay-audit`,
and `candidate-tournament`. Every model call and evaluation is charged to both
its stage and the global budget; exceeding either is a hard run failure with
reason code `INNOVATION_RUN_STAGE_RESOURCE_EXHAUSTED`. A host may not claim the
stage completed by emitting an output object when its stage quota was never
consumed.

The Provider boundary must reject a candidate that lacks a validated
12-round `iterationTrace`; this is the admission gate before audit or patch
planning. Host integration must preserve the run id, stage usage and final
decision as separate evidence fields; flattening them into a generic
`accepted` flag is non-compliant.

## A↔B protocol

1. C (human or AI collaborator) supplies the goal, constraints, evidence
   expectations and current authorization.
2. A emits an `aIntentEnvelope` with a goal revision and source snapshot.
3. ABCC matches requested semantics to a versioned B capability offer.
4. B declares schemas, preconditions, effects, evidence and failure codes;
   mismatches cause `replan`, not a silent reinterpretation.
5. ABCC maps the result back to A as a normalized result with an evidence set
   and immutable receipt reference.
6. Audit and completion are separate from C's final acceptance.
7. Missing capability blocks; missing evidence caps the claim; unauthorized
   effects block; observable failure enters the recovery path.

The machine contract is
`ES/Automation/Contracts/es-ai-abc-interface-v1.schema.json`; the Core instance
and parity declaration are in `es-ai-abc-core-v1.json`.

Static markers: **ABCC independent**, **ABCD parity**, **A-to-B**,
**normalized evidence**, **explicit-only**, and **deterministic-replay**.

## ABCD parity requirement

ABCC must provide all six kernel capabilities, while selection remains
predicate-driven (not every task executes all six):

1. `bounded-tool-action` — bounded, authorized action with change evidence.
2. `failure-recovery` — observable failure, revision and bounded retry/stop.
3. `branch-evaluation` — finite candidates, criteria and a ranked decision.
4. `state-transition-guard` — legal lifecycle/ownership transitions only.
5. `environment-trust-gate` — environment identity and trust before external
   tools or runtime claims.
6. `audit-evidence-chain` — source hashes, receipts, non-claims and completion.

The `parityContract.requiredCapabilities` list is a hard closure check. A
missing or semantically incompatible capability is `blocked`/`replan`; it is
never hidden by a Part or by the old research mapping.

## Workflow

1. Read AIBrain and the one or two routed Knowledge entries, then read this
   Skill and the Core contract files.
2. Freeze the C authorization and goal revision; do not infer Runtime or write
   authority from a Skill, route, catalog or Knowledge entry.
3. Validate A intent, negotiate B offers, and record field-level mappings and
   loss policy.
4. Require evidence for each accepted output and report non-claims.
5. Run bounded static replay for positive, invalid, denied-expansion,
   idempotency, hash invalidation, interruption recovery and deterministic
   output cases.
6. Use `es-adversarial-review` after modifications. Unity/Runtime acceptance is
   a separate explicitly authorized operation.

`KnowledgeIndex` and each Knowledge `SourceRef`/`ContentHash` are navigation
inputs only; a hash or route drift makes the selected entry stale and requires
re-planning.

## Boundaries and references

- Core contract: `ES/Automation/Contracts/es-ai-abc-core-v1.json`
- InnovationRun contract: `ES/Automation/Contracts/es-ai-abc-innovation-run-v1.schema.json`
- InnovationRun executor: `ES/Automation/ABCD/ESABCInnovationRun.psm1`
- Interface schema: `ES/Automation/Contracts/es-ai-abc-interface-v1.schema.json`
- Mode registry: `ES/Automation/Contracts/es-ai-abc-mode.registry.json`
- Route stages: `ES/Automation/Contracts/es-route-stage.registry.json`
- Independent Knowledge: `Documentation/AIKnowledge/entries/ai-abc-core.md`
- Research provenance (not Core authority):
  `.agents/skills/es-agent-mechanism-replication/`

This Skill is read-only by default. It does not start Unity, Player, host
processes, network access or release actions, and it does not grant permission
to write project files.

## Engineering controls

Identity, authority, risk, observability, recovery, performance, compatibility
and supply-chain controls are declared in `governance.json`. StaticDeepReplay
is the first verification path; Runtime requires fresh, explicit authorization.

## Skill 使用披露

遵循项目根 `AGENTS.md` 和 `.agents/README.md` 的 Skill 披露规则；披露本
Skill 不等于获得授权或产生运行时证据。
