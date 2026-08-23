---
name: es-skill-governance
description: "Classify, design, upgrade, review, validate, and retire ESFramework project Skills using SmallTool, Workflow, and Engineering tiers. Use when creating a Skill, deciding how much automation or evidence it needs, adding scripts or references, reviewing permissions and failure recovery, assessing readiness to scale, or diagnosing Skill execution cost, slow startup, repeated scans/hashing, caching, Fast Path, and Deep Path acceptance. Discovery aliases: skill-performance, execution-cost, fast-path, deep-path, cache."
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# ES Skill Governance

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- Read [the Runtime authorization contract](references/runtime-authorization-contract.md) before any runtime proposal; validate a supplied manifest with `scripts/Test-ESRuntimeAuthorization.ps1`. The machine-readable contract is `ES/Automation/Contracts/es-runtime-authorization.schema.json`; the validator remains the semantic authority for path containment, source hashes, expiry, and budget relationships.
- Run [the static/runtime semantics audit](scripts/Test-ESSkillVerificationSemantics.ps1) after a batch upgrade; it reports Skills whose claims are runtime-heavy but lack explicit verification profiles.
- MCP is optional and deny-by-default; capability visibility never grants permission. Use AIBrain `planTask`, the matching AICommand, and the current TaskContract before any write or external operation.

Use this Skill to turn a proposed project Skill into a bounded, testable and maintainable capability. It governs scope, workflow, resources, evidence, risk, ownership and maturity; it never grants permission to edit source, Git, Unity state, release outputs or external systems.

## Authority and boundaries

1. Read `.agents/README.md`, the AIWarnings entry, `CurrentStatus`, `RuleIndex`, and the P0/domain rules matched by the Skill's work.
2. Treat `.agents/skills/<name>/SKILL.md` as the Skill workflow authority, AIWarnings as long-lived constraints, and AICommands as the per-task authorization contract.
3. Keep three axes separate: **Tier** (`SmallTool`, `Workflow`, `Engineering`), **Maturity** (`Proposed` through `Archived`), and **Delivery** (`Designed`, `Implemented-Unverified`, `Blocked`, `Failed`, `Accepted`, `Released`).
4. Use the project's S0-S6 evidence levels. Never describe a Skill as production-ready from frontmatter validation alone.
5. Preserve the direct-child `es-*` layout under `.agents/skills`. Do not copy AIWarnings, AICommands, session history, Unity assemblies, generated artifacts, or hidden binaries into a Skill.
6. For a governed Skill, keep `governance.json` beside `SKILL.md`. It is a declaration consumed by validation and, when present, by AIBrain; it is not an authorization token.

## Tier decision

Classify from the highest true condition:

```text
One bounded action, one domain, low side effect, no child tools? -> SmallTool
Cross-module workflow, staged checks/recovery, reusable scripts or child tools? -> Workflow
Full project lifecycle, architecture/risk/performance/release governance? -> Engineering
```

Read [references/tier-matrix.md](references/tier-matrix.md) before choosing a tier. A higher tier increases design, evidence and recovery obligations; it does not increase authorization.

Read [references/verification-semantics.md](references/verification-semantics.md) for the project-wide distinction between source-level (`Static`) and external-execution (`Runtime`) evidence. Never convert `runtime-not-run` into a static failure; select the verification profile that matches the claim.

The default is `StaticDeepReplay` first: complete static simulation and boundary analysis before any Runtime proposal. Runtime is opt-in only and requires explicit developer approval, AIBrain plan, matching AICommand/TaskContract, and a bounded evidence budget.

Every Skill-local `Test-ESSkillEvidence.ps1` is required to delegate to `scripts/Test-ESStrictEvidenceReceipt.ps1` (or implement an equivalent strict contract). Local receipt validation must not stop at field presence; it must verify project-relative paths, source hashes, PlanHash, tool identity, capture time, and freshness.

Read [references/commercial-controls.md](references/commercial-controls.md) for the required controls around identity, ownership, risk, data, supply chain, observability, performance, compatibility and incident recovery. Read [references/performance-controls.md](references/performance-controls.md) for the mandatory fast-path/deep-path execution limits. Read [references/aibrain-contract.md](references/aibrain-contract.md) for the AIBrain planning and execution boundary. Read [references/resource-index-contract.md](references/resource-index-contract.md) and `.agents/SKILL_RESOURCE_INDEX.yaml` when composing a Skill from references, scripts, MCP capabilities or evidence packs.

## Creation and upgrade workflow

1. Frame the outcome, trigger phrases, inputs, outputs, side effects, non-goals and failure modes.
2. Audit the worktree, existing Skills and AICommands. Mark `NoMatchingCommand` when no command matches; never borrow an unrelated command.
3. Choose the lowest tier that satisfies the real scope, and record upgrade conditions.
4. Keep routing and mandatory steps in `SKILL.md`; put stable details in one-level `references/`; use `scripts/` for deterministic repeated checks.
5. Declare read/write paths, dry-run, confirmation, cancellation, idempotency, concurrency, recovery and cleanup. Fail closed when context or authorization is missing.
6. Use `.agents/skills/es-skill-creator/scripts/init_skill.py` and `generate_openai_yaml.py` for new Skills and metadata. Use explicit UTF-8 and preserve unrelated changes.
7. Run `quick_validate.py`, the Skill's scripts, the UTF-8 guard and positive, denial and recovery cases. Read [references/evidence-and-acceptance.md](references/evidence-and-acceptance.md).
8. Report target, changes, tier/maturity/delivery, evidence, verified and unverified behavior, blockers, impact and next action.
9. If `governance.json` is present, include its hash in the acceptance evidence. AIBrain must bind that hash into the plan; changing governance metadata requires a new plan.

## Tier operating rules

- **SmallTool**: one obvious entry point and narrow blast radius; prefer read-only or dry-run; one deterministic script is enough when it removes repeated errors. No child Skills or unrelated scans.
- **Workflow**: phases with checkpoints, dry-run, prerequisite validation, machine-readable evidence, retry/rollback boundaries and a recovery path. Child tools may exist, each single-purpose with an explicit interface.
- **Engineering**: model architecture, dependencies, permissions, performance, compatibility, migration, release and rollback. Require a risk register, evidence matrix, staged execution and acceptance owner. Engineering is not a “万能权限” tier.

## Resource rules

- Add references only for stable facts or repeated rediscovery; link directly from `SKILL.md`.
- Add scripts only for deterministic repeated work; document parameters, output, write scope and exit codes.
- Add assets only for reusable output templates or media. Never bundle project binaries or generated Unity output.
- Do not add `README.md`, installation guides, changelogs or copied AIWarnings.

## Minimum release gate

A Skill may be called `Stable` only when its trigger is precise, write scope is explicit, scripts pass representative runs, denial fails closed, and evidence matches the claim. Workflow and Engineering tiers also require recovery and scale/performance notes. Missing Unity, Player, Profiler, IL2CPP or release evidence remains explicitly unverified.

## Specialized static acceptance

- Guidance: `references/static-specialized-acceptance.md`
- Acceptance ID: `governance-contract`
- Required cases: `metadata-completeness, authority-ref-closure, permission-denial, profile-weight, stale-governance-hash`
- Static assertions: authority refs are closed; runtime hard gate; StaticDeepReplay-first; permission expansion denied; governance hash
- This contract is responsibility-specific and remains distinct from Runtime proof.

## Responsibility-specific static acceptance

- Profile: `governance`
- Custom checks: `authority-routing, permission-boundary, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- **Owners**: ESFramework AI governance maintainers own maintenance; the task requester or designated maintainer owns acceptance.
- **Permission matrix**: inspection and planning are read-only; Skill/validator writes require explicit task authorization; Git, Unity state, release, deletion, network and external AI remain separately unauthorized.
- **Capability modes**: `references/capability-mode-registry.json` is the explicit exception registry. The default is `mutating`, which requires an AICommand binding. `advisory` produces analysis/review only; `candidate` produces proposals only. Neither mode grants project writes or external execution.
- **Command binding registry**: `references/command-binding-registry.json` is an auditable bridge for existing Skills while their minified `governance.json` is migrated. It must contain the exact command ID, body hash, role, risk level and write mode; the validator resolves and rehashes it before allowing the binding.
- **Change budget**: name exact Skill paths, maximum Skill count, allowed files, retry count, timeout and stop condition before any batch upgrade.
- **Risk register**: prevent tier inflation with the tier matrix; detect permission expansion through metadata validation; isolate malformed Skills by failing closed; recover by preserving the previous files and invalidating the old PlanHash.
- **Scale and concurrency**: validate one Skill independently, batch only with a declared upper bound, and reject concurrent writes to the same Skill root. Static validation is not a performance claim.
- **Execution performance**: every Skill defaults to the fast path in [references/performance-controls.md](references/performance-controls.md). Full scans, uncached hashing, Graph Bake, Unity operations, external network calls and bulk evidence copying/hashing require an explicit deep-path objective, declared budget and phase evidence; they must never be hidden in ordinary invocation or silently skipped when required.
- **Compatibility and retirement**: schema or authority semantic changes require validator/AIBrain updates and Knowledge hash refresh. Retirement requires removing active routes only after a replacement or explicit deprecation decision.
- **Acceptance replay**: rerun the contract validator plus positive, invalid-input, denied-expansion, repeat/idempotency and interruption/recovery cases; record command, inputs, output and governance hash.
- **Evidence separation**: record static proof and runtime proof on separate axes. A Skill may be source-supported and runtime-unverified; it may not claim runtime or release acceptance from static evidence.
- **Runtime consent**: never open Unity, run a game, switch scenes, launch a Player or start an external process merely because a runtime check exists. Ask for or consume explicit developer authorization tied to the current plan and stop condition.
- **Static weight**: every profile gives StaticDeepReplay at least half of its evidence weight; Runtime is supplementary unless an explicitly authorized RuntimeAcceptance/ReleaseAcceptance profile is selected.

## AIBrain operating boundary

When the task is routed through AIBrain, use `planTask` before `runTask`. The plan must name the routed Knowledge entries, AICommand, Skill hashes, governance metadata, TaskContract and required evidence. `runTask` may consume only the one-time plan authorization and matching invocation; it must not call ProcessRunner or write `Assets/` directly. If a Skill or its `governance.json` changes after planning, discard the old plan and re-plan. See [references/aibrain-contract.md](references/aibrain-contract.md).

## Bundled resources

- [references/tier-matrix.md](references/tier-matrix.md): tier capabilities, prohibitions, resources, evidence and upgrade rules.
- [references/evidence-and-acceptance.md](references/evidence-and-acceptance.md): S0-S6 mapping, status axes, tests and reporting.
- [references/scale-patterns.md](references/scale-patterns.md): child tools, references, scripts, dependencies and maintenance.
- [references/commercial-controls.md](references/commercial-controls.md): commercial-grade controls and operating obligations.
- [references/performance-controls.md](references/performance-controls.md): universal fast-path/deep-path execution and scale limits.
- [references/aibrain-contract.md](references/aibrain-contract.md): AIBrain routing, plan hash and one-time execution contract.
- [references/verification-semantics.md](references/verification-semantics.md): Static/Runtime axes and verification profiles.
- [references/runtime-authorization-contract.md](references/runtime-authorization-contract.md): one-time runtime authorization binding.
- `scripts/Test-ESSkillContract.ps1`: read-only structural and contract checks.

Run:

```powershell
& .agents/skills/es-skill-governance/scripts/Test-ESSkillContract.ps1 -SkillPath .agents/skills/es-skill-governance
```


## Specialized static acceptance

Acceptance ID: `governance-contract`

Responsibility-specific static assertions (these are source-level requirements, not Runtime claims):
- authority refs are closed
- runtime hard gate
- StaticDeepReplay-first
- permission expansion denied
- governance hash

Required specialized cases: `metadata-completeness, authority-ref-closure, permission-denial, profile-weight, stale-governance-hash`
Guidance: `references/static-specialized-acceptance.md`
