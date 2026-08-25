---
name: es-resource-pipeline
description: Implement, diagnose, or audit the ESFramework resource pipeline across ESAssetLibrary, ESAssetBook, catalogs, ResourcePlan, manifests, runtime providers, ESAssetScope, downloading, and release output. Use for asset collection, preview, export, dependency analysis, runtime loading, provider changes, or resource publishing tasks.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# Work on the ES Resource Pipeline

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and capability visibility never grants AI-initiated authority. The current explicit user request authorizes its bounded action under `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`; AIBrain, AICommand and TaskContract are protocol inputs only when their managed channel is selected. Reject inferred expansion, not user-directed paths.

## Specialized static acceptance

- Guidance: `references/static-specialized-acceptance.md`
- Acceptance ID: `resource-pipeline-static`
- Required cases: `stage-manifest, provider-identity, dependency-closure, duplicate-resource, recovery-boundary`
- Static assertions: manifest; provider; dependency; duplicate; recovery
- This contract is responsibility-specific and remains distinct from Runtime proof.

## Responsibility-specific static acceptance

- Profile: `authoring`
- Custom checks: `change-boundary, resource-projection, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- Scope and authority are checked before execution; stale or missing evidence blocks the task.
- A current user request directly authorizes bounded resource source and Assets changes. Pipeline execution, external, destructive, release and Git actions must be explicitly named; managed-channel plans and commands are protocol inputs only.
- Record evidence for positive, invalid-input, denied-expansion, repeat-idempotency, and interruption-recovery cases.

Treat editor authoring, generated plans, release artifacts, runtime providers, and scope ownership as separate layers.

## Workflow

1. Read the AIWarnings start files and `references/project-map.md`.
2. For direct work, treat the current explicit user request as edit authority. Select a matching AICommand only for `ManagedAIBrain/Worker`; a managed read-only contract still cannot be used as that channel's write protocol.
3. Identify the affected stage: library/book authoring, catalog, reference graph, build plan, bundle manifest, release manifest, provider, downloader, or scope.
4. Trace identifiers and hashes end to end. Verify which file or object is authoritative at each stage.
5. Make the smallest coherent change without creating a second export, preview, manifest, or loading path.
6. Validate dependency closure, duplicate inclusion, unused assets, stable identities, scope disposal, cancellation, and provider replacement as applicable.
7. Use `$es-unity-compile` for editor import and tests. Run real export, Player, IL2CPP, network, or release checks only when required and available.
8. Label every result by its actual evidence layer and run `$es-utf8-guard` for changed text.

## Required boundaries

- Keep `ESAssetLibrary` and `ESAssetBook` as authoring structures, not runtime provider substitutes.
- Keep Library editor-owned and Manifest/Table runtime-owned where the current warnings require that split.
- Do not make runtime code scan the AssetDatabase, editor libraries, or project folders.
- Do not leak `ESAssetScope`; ownership and disposal must be explicit.
- Do not invent fallback loads that bypass the provider or release manifest.
- Do not claim release success from preview, catalog generation, `.csproj` compilation, or Editor-only tests.

## Delivery

Report the affected pipeline stage, authority chain, changed artifacts, dependency and scope checks, evidence labels, and untested publishing layers.


## Specialized static acceptance

Acceptance ID: `resource-pipeline-static`

Responsibility-specific static assertions (these are source-level requirements, not Runtime claims):
- manifest
- provider
- dependency
- duplicate
- recovery

Required specialized cases: `stage-manifest, provider-identity, dependency-closure, duplicate-resource, recovery-boundary`
Guidance: `references/static-specialized-acceptance.md`
