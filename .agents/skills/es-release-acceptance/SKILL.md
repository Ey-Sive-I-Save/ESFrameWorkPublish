---
name: es-release-acceptance
description: Plan, execute, and report ESFramework acceptance across source checks, generated .csproj builds, Unity Editor compilation, ReloadDomain, EditMode and PlayMode tests, runtime observation, Profiler, Player and IL2CPP builds, resource providers, manifests, downloading, and real release workflows. Use when deciding whether an ES change is actually ready to publish or when assembling a release evidence matrix.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# Run ES Release Acceptance

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- Runtime authorization manifests must conform to `ES/Automation/Contracts/es-runtime-authorization.schema.json` and be semantically checked by `.agents/skills/es-skill-governance/scripts/Test-ESRuntimeAuthorization.ps1`; a receipt or approval note alone never grants execution authority.
- MCP is optional and capability visibility never grants AI-initiated authority. The current explicit user request authorizes its bounded action under `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`; AIBrain, AICommand and TaskContract are protocol inputs only when their managed channel is selected. Reject inferred expansion, not user-directed paths.

Build an evidence matrix from the actual risk surface. Never upgrade a lower evidence layer into a higher release claim.

## Workflow

1. Read the AIWarnings start files and `references/evidence-matrix.md`.
2. Inspect the changed paths and classify affected assemblies, runtime systems, editor tools, resources, providers, platforms, and release artifacts.
3. Define the required evidence rows before running checks. Mark unavailable rows as blocked or not run, never implicit pass.
4. Capture the existing Unity Console and editor state before clearing or changing anything.
5. Run static and generated-project checks, then Unity import and domain reload, then focused EditMode/PlayMode tests.
6. Run runtime observation, Profiler, Player/IL2CPP, resource plan, provider, download, and release checks only where the change requires them.
7. Archive exact commands, Unity jobs, test names, target platform, logs, outputs, hashes, and timestamps needed to reproduce the conclusion.
8. Separate task-related failures from unrelated existing failures. Stop release approval when a required row fails or lacks evidence.
9. Run `$es-utf8-guard` and update the local documentation ledger without advancing HTML before `ready-for-html`.

## Decision rule

Approve only the explicitly requested scope whose required evidence rows passed. Use `conditional` when accepted gaps are documented. Use `blocked` when required Unity, platform, provider, or publishing evidence could not run.

## Required boundaries

- Source presence is not compilation.
- `.csproj` build is not Unity Editor compilation.
- Unity Console success is not Test Runner or PlayMode success.
- PlayMode observation is not Profiler, Player, IL2CPP, provider, or release success.
- A generated manifest is not a successfully downloaded, verified, loaded, and rolled-back release.
- An external AI report is not local evidence unless independently reproduced.

## Specialized static acceptance

- Guidance: `references/static-specialized-acceptance.md`
- Acceptance ID: `release-evidence-static`
- Required cases: `matrix-completeness, missing-evidence, hash-freshness, compatibility-boundary, runtime-gate-scope`
- Static assertions: acceptance matrix; fresh hash; compatibility; missing evidence; release gate
- This contract is responsibility-specific and remains distinct from Runtime proof.

## Responsibility-specific static acceptance

- Profile: `release`
- Custom checks: `evidence-contract, runtime-escalation, compatibility-boundary, deterministic-replay`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- **Owners**: ESFramework release maintainers own the workflow; a designated release owner alone accepts or releases the requested scope.
- **Permission matrix**: evidence planning and log inspection are read-only; Unity state changes, Player builds, uploads, publishing, deletion and rollback each require explicit current-user authorization. A matching TaskContract is additionally required only when the selected execution path is ManagedAIBrain/Worker; it is not a second approval for direct user work.
- **Change budget**: declare platforms, scenes, test assemblies, build targets, providers, artifact paths, maximum retries, timeout and stop conditions before execution.
- **Risk register**: prevent evidence-layer substitution and accidental release; detect stale inputs, unrelated failures and partial artifacts; isolate failed rows; recover through preserved logs/artifacts and the target release rollback procedure.
- **Scale/performance**: state asset/test counts, batching, first-run versus steady-state cost, expected disk/memory pressure, concurrency and bottlenecks. Never infer Profiler or platform performance from static checks.
- **Compatibility**: record Unity/package/toolchain versions, input/output formats and target platforms. Version drift invalidates affected evidence.
- **Acceptance replay**: another agent must be able to rerun the matrix from recorded commands, task identity, PlanHash, hashes, platforms and artifacts. Include positive, invalid-input, denied-expansion, repeat/idempotency and interruption/recovery cases.

## Delivery

Return the evidence matrix, decision, exact passed scope, failures, blocked rows, artifacts, and the smallest next action required for approval.


## Specialized static acceptance

Acceptance ID: `release-evidence-static`

Responsibility-specific static assertions (these are source-level requirements, not Runtime claims):
- acceptance matrix
- fresh hash
- compatibility
- missing evidence
- release gate

Required specialized cases: `matrix-completeness, missing-evidence, hash-freshness, compatibility-boundary, runtime-gate-scope`
Guidance: `references/static-specialized-acceptance.md`
