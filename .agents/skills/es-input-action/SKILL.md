---
name: es-input-action
description: Add, bind, diagnose, or migrate ESFramework input actions across ESInputActionId, action metadata, Input System or virtual bindings, profiles, RuntimeMode filtering, services, player control requests, and self-tests. Use for missing bindings, new controls, rebinding, runtime-mode blocks, virtual input, or player input routing.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# Work on ES Input Actions

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and capability visibility never grants AI-initiated authority. The current explicit user request authorizes its bounded action under `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`; AIBrain, AICommand and TaskContract are protocol inputs only when their managed channel is selected. Reject inferred expansion, not user-directed paths.

## Responsibility-specific static acceptance

- Profile: `authoring`
- Custom checks: `change-boundary, resource-projection, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- Scope and authority are checked before execution; stale or missing evidence blocks the task.
- A current user request directly authorizes bounded input source, Assets and governance changes. Runtime, external, destructive and Git actions must be explicitly named; managed-channel plans and commands are protocol inputs only.
- Record evidence for positive, invalid-input, denied-expansion, repeat-idempotency, and interruption-recovery cases.

Maintain one traceable path from stable action identity through binding and profile resolution to runtime consumption.

## Workflow

1. Read the AIWarnings start files and `references/project-map.md`.
2. Select the strong write command for adding an action, or a read-only diagnostic command for inspection.
3. Trace the action through ID, metadata, binding definition, source type, profile baking, scheme resolution, runtime service, RuntimeMode filter, and player consumer.
4. Preserve stable action IDs and binding keys. Determine whether the source is Unity Input System or an ES virtual control.
5. Update all required catalogs and defaults without creating a parallel direct-input path.
6. Validate missing bindings, duplicate IDs, value type, trigger policy, runtime-mode allow/block behavior, rebinding persistence, and player control arbitration.
   Run [the stable identity manifest validator](scripts/Test-ESStableIdentityManifest.ps1) for persisted identity evidence; it rejects process-local `RuntimeKey`/`RuntimeId` fields.
7. Run the built-in ES input self-tests and relevant Unity tests. Use `$es-unity-compile` for authoritative editor evidence.
8. Run `$es-utf8-guard` and report untested devices, schemes, or PlayMode behavior.

## Required boundaries

- Do not read Unity input directly from gameplay code when `ESInputService` is the intended authority.
- Do not assign or renumber stable action IDs without verifying serialized and runtime consumers.
- Do not bypass RuntimeMode filtering or active control-request arbitration.
- Do not treat editor drawer display success as runtime binding success.
- Keep Input System bindings and virtual-control bindings explicit.
- Persist stable action/binding identities and schema hashes only; resolve RuntimeKey after the current catalog is loaded.

## Delivery

Report the action ID, metadata, binding source, profile and RuntimeMode path, consumer chain, self-test and Unity results, and unsupported devices or schemes.
