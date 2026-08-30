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
- Capability visibility never grants AI-initiated authority. Apply [the user-directed action authority](references/user-directed-action-authority.md): the current user's explicit request directly authorizes its bounded action, while AIBrain, AICommand and TaskContract are protocol inputs only when their managed channel is selected. Reject inferred expansion, not user-directed paths.

Use this Skill to turn a proposed project Skill into a bounded, testable and maintainable capability. It governs scope, workflow, resources, evidence, risk, ownership and maturity. It never grants AI permission to invent work beyond the current user request, and it never narrows work the user has already authorized.

## Authority and boundaries

1. Read `.agents/README.md`, the AIWarnings entry, `CurrentStatus`, `RuleIndex`, and the P0/domain rules matched by the Skill's work.
2. Treat `.agents/skills/<name>/SKILL.md` as the Skill workflow authority, AIWarnings as long-lived constraints, the current user instruction as action authority, and AICommands as managed-channel task contracts.
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

Read [references/es-preservation-refactor-contract.md](references/es-preservation-refactor-contract.md) before refactoring an existing ES subsystem. Preserve ES entry points and default behavior; add commercial controls at boundaries and require migration evidence for breaking changes.

The default is `StaticDeepReplay` first. Runtime is opt-in only: the current user must explicitly request the Runtime action and a bounded stop condition. When Runtime is executed through AIBrain, its plan, AICommand and TaskContract remain mandatory transport inputs, but they are not a second user approval.

For project work, the current explicit user instruction authorizes the named goal and strictly necessary project-local changes, including source, Assets, governance/control-plane, settings, documentation, tests and generated evidence. `UserDirectedLowRisk` is now a compatibility name for a scope-closure validator, not a low-risk allowlist. Path classes and size thresholds are review signals only. Delete, rename, Git, Runtime, external-process, network, release and credential actions must be explicitly named; once named, they require no additional project approval.

Every Skill-local `Test-ESSkillEvidence.ps1` is required to delegate to `scripts/Test-ESStrictEvidenceReceipt.ps1` (or implement an equivalent strict contract). Local receipt validation must not stop at field presence; it must verify project-relative paths, source hashes, PlanHash, tool identity, capture time, and freshness.

Read [references/commercial-controls.md](references/commercial-controls.md) for the required controls around identity, ownership, risk, data, supply chain, observability, performance, compatibility and incident recovery. Read [references/performance-controls.md](references/performance-controls.md) for the mandatory fast-path/deep-path execution limits. Read [references/aibrain-contract.md](references/aibrain-contract.md) for the AIBrain planning and execution boundary. Read [references/resource-index-contract.md](references/resource-index-contract.md) and `.agents/SKILL_RESOURCE_INDEX.yaml` when composing a Skill from references, scripts, MCP capabilities or evidence packs.

## Discovery architecture

`governance.json` 的 maturity/delivery 不能直接当作可执行资格。读取 `.agents/SKILL_DISCOVERY_POLICY.json`，区分 `discoveryState`、`planEligibility` 和 `runtimeEligibility`；再使用 `.agents/SKILL_REGISTRY.manifest.json` 检查 Catalog、Resource Index、Knowledge 和 AICommand Catalog 的元数据哈希是否属于同一代。组织层检查使用 `scripts/Test-ESSkillArchitecture.ps1`，更新索引后使用 `scripts/Build-ESSkillRegistryManifest.ps1` 原子重建清单。

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

Before declaring an existing Skill upgrade complete, run `scripts/Get-ESSkillChangeImpact.ps1` for the target Skill. A `medium` or `major` result is a derived revalidation gate: report the impact to the user and complete its `requiredStages` before claiming `Accepted`; it does not request a second authorization. The rule contract is `references/skill-change-impact-contract.md` and the machine rules are `references/skill-change-impact-rules.json`.

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

## Responsibility-specific static acceptance

- Profile: `governance`
- Custom checks: `authority-routing, permission-boundary, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- **Owners**: ESFramework AI governance maintainers own maintenance; the task requester or designated maintainer owns acceptance.
- **Permission matrix**: inspection is read-only unless the current user requests a change. That current instruction authorizes its bounded goal; Git, Unity/Runtime, release, deletion/rename, network, external process and credentials require action-specific wording, not a second approval.
- **Capability modes**: `references/capability-mode-registry.json` classifies unattended or AIBrain-orchestrated Skill behavior. `advisory` and `candidate` limit what the Skill may initiate by itself; they do not force an explicitly user-directed implementation into proposal-only mode. `mutating` uses an AICommand binding only when the managed channel is selected.
- **Command binding registry**: `references/command-binding-registry.json` is an auditable bridge for existing Skills while their minified `governance.json` is migrated. It must contain the exact command ID, body hash, role, risk level and write mode; the validator resolves and rehashes it before allowing the binding.
- **Change budget**: name exact Skill paths, maximum Skill count, allowed files, retry count, timeout and stop condition before any batch upgrade.
- **Risk register**: prevent tier inflation with the tier matrix; detect permission expansion through metadata validation; isolate malformed Skills by failing closed; recover by preserving the previous files and invalidating the old PlanHash.
- **Scale and concurrency**: validate one Skill independently, batch only with a declared upper bound, and reject concurrent writes to the same Skill root. Static validation is not a performance claim.
- **Execution performance**: every Skill defaults to the fast path in [references/performance-controls.md](references/performance-controls.md). Full scans, uncached hashing, Graph Bake, Unity operations, external network calls and bulk evidence copying/hashing require an explicit deep-path objective, declared budget and phase evidence; they must never be hidden in ordinary invocation or silently skipped when required.
- **Compatibility and retirement**: schema or authority semantic changes require validator/AIBrain updates and Knowledge hash refresh. Retirement requires removing active routes only after a replacement or explicit deprecation decision.
- **Acceptance replay**: rerun the contract validator plus positive, invalid-input, denied-expansion, repeat/idempotency and interruption/recovery cases; record command, inputs, output and governance hash.
- **Evidence separation**: record static proof and runtime proof on separate axes. A Skill may be source-supported and runtime-unverified; it may not claim runtime or release acceptance from static evidence.
- **Runtime consent**: never open Unity, run a game, switch scenes, launch a Player or start an external process merely because a runtime check exists. A current user request that explicitly names the Runtime/external action and a bounded stop condition is sufficient; do not ask for a second project approval.
- **Static weight**: every profile gives StaticDeepReplay at least half of its evidence weight; Runtime is supplementary unless an explicitly authorized RuntimeAcceptance/ReleaseAcceptance profile is selected.

## AIBrain operating boundary

When the task is routed through AIBrain, use `planTask` before `runTask`. The plan must name the routed Knowledge entries, AICommand, Skill hashes, governance metadata, TaskContract and required evidence. `runTask` may consume only the bounded plan token and matching invocation; it must not call ProcessRunner or write paths outside that plan. These are managed-channel invariants, not prerequisites for direct user-directed work. If a Skill or its `governance.json` changes after planning, discard the old plan and re-plan. See [references/aibrain-contract.md](references/aibrain-contract.md).

## Bundled resources

- [references/tier-matrix.md](references/tier-matrix.md): tier capabilities, prohibitions, resources, evidence and upgrade rules.
- [references/evidence-and-acceptance.md](references/evidence-and-acceptance.md): S0-S6 mapping, status axes, tests and reporting.
- [references/scale-patterns.md](references/scale-patterns.md): child tools, references, scripts, dependencies and maintenance.
- [references/commercial-controls.md](references/commercial-controls.md): commercial-grade controls and operating obligations.
- [references/performance-controls.md](references/performance-controls.md): universal fast-path/deep-path execution and scale limits.
- [references/aibrain-contract.md](references/aibrain-contract.md): AIBrain routing, plan hash and invocation-bound, time-and-use-limited execution contract.
- [references/verification-semantics.md](references/verification-semantics.md): Static/Runtime axes and verification profiles.
- [references/runtime-authorization-contract.md](references/runtime-authorization-contract.md): one-time runtime authorization binding.
- `scripts/Test-ESSkillContract.ps1`: read-only structural and contract checks.
- `scripts/Test-ESSkillArchitecture.ps1`: lifecycle, route-scope and registry-manifest closure checks.
- `scripts/Test-ESCommercialCoherence.ps1`: read-only aggregate gate for Skill, AICommand, ES Automation compatibility, and AIKnowledge static surfaces; it never starts Runtime.
  The aggregate gate also records before/after governance-surface hashes and blocks when the audit spans multiple source generations.
- Read `references/commercial-coherence-contract.md` before changing the aggregate gate or interpreting `static-coherent`.
- Run `scripts/Test-ESStaticAcceptanceCoverage.ps1` when changing the Skill portfolio; it verifies every Skill has a responsibility-specific static acceptance plan and discoverable evidence artifacts.
- [references/user-directed-action-authority.md](references/user-directed-action-authority.md), `references/user-directed-low-risk-policy.json` and `scripts/Test-ESUserDirectedLowRiskPolicy.ps1`: current-user direct authority plus deterministic declared-scope checks; the old `UserDirectedLowRisk` name is compatibility-only and contains no path denylist.
- `scripts/Build-ESSkillRegistryManifest.ps1`: deterministic, project-relative registry snapshot builder.
- `scripts/Build-ESSkillRelationRegistry.py`: projects each direct Skill's relationships to the
  Catalog, Resource Index, Registry Manifest, AIBrain, Knowledge, AICommand, evidence contracts,
  authority references, Chinese aliases and AISpace output bindings
  into `ES/AISpace/Public/Skills/registry.json`; use `--write` only for an explicitly requested
  registration update and `--check` for read-only drift detection.
- `scripts/Test-ESSkillRelationRegistry.py`: read-only closure, uniqueness, project-relative path
  and freshness validator for the AISpace relationship projection.
- `scripts/Test-ESSkillAISpaceBindings.py`: read-only validator for stable Skill output/cache
  bindings and their reverse references in the AISpace projection.
- `scripts/Register-ESSkillAISpaceBinding.py`: bounded explicit-write registration helper;
  it enforces Skill existence, canonical roots, unique IDs and atomic update before projection.

The governance acceptance also includes **Skill relationship registry closure**: every direct
Skill must resolve exactly once across these navigation relations before the projection is
treated as current.

### AISpace output binding at registration

Generation/cache-capable Skills must register stable output bindings in the project authority
`.agents/SKILL_AISPACE_BINDINGS.json` before Catalog/Manifest/relationship rebuild. Each entry
names the Skill, points to its `governance.json`, and declares a stable `bindingId`, purpose,
storage class, project-relative path template, lifecycle, artifact kinds and write policy.
`private-temp`/`private-content` uses `ES/AISpace/Local/<category>/<YYYYMMDD>/<agent-or-task>/`;
`public-index`/`public-content` uses `ES/AISpace/Public/<category>/<YYYYMMDD>/<topic-or-task>/`;
Unity-facing formal assets use `Assets/ES/AISpace/Public/<category>/<YYYYMMDD>/<domain>/` after
the authorized reference migration.

`Build-ESSkillRelationRegistry.py` projects these declarations as the `aispace` relation with
both `registryPath` and `skillContractPath`. `Test-ESSkillAISpaceBindings.py` proves the
forward declaration and reverse projection agree. This is a discoverability and placement
contract only; it does not redirect arbitrary writers or expand Skill permissions.
- `.agents/SKILL_ROUTE_ALIASES.zh-CN.json` and `scripts/Test-ESChineseSkillRouteCoverage.ps1`: authoritative Chinese discovery aliases for every direct Skill; missing or ambiguous aliases block route coverage only, never grant permission.

Run:

```powershell
& .agents/skills/es-skill-governance/scripts/Test-ESSkillContract.ps1 -SkillPath .agents/skills/es-skill-governance
```

## Skill 使用披露

使用本 Skill 时，按项目根 `AGENTS.md` 与 `.agents/README.md` 的 Skill 使用披露规范，
在首次用户可见的进度更新中说明本 Skill 与当前任务的治理关系，并在最终答复列出本轮
实际使用的 Skill。披露不等于授权、执行或验收证据。


## Specialized static acceptance

Acceptance ID: `governance-contract`

Guidance: `references/static-specialized-acceptance.md`

Responsibility-specific static assertions (these are source-level requirements, not Runtime claims):
- authority refs are closed
- runtime hard gate
- StaticDeepReplay-first
- permission expansion denied
- governance hash
- ES entry compatibility
- commercial coherence snapshot stability
- knowledge source freshness classification

Required specialized cases: `metadata-completeness, authority-ref-closure, permission-denial, profile-weight, stale-governance-hash, es-entry-compatibility, commercial-coherence-snapshot`
Guidance: `references/static-specialized-acceptance.md`
