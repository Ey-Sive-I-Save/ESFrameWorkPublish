---
name: es-entity-authoring
description: Author or review ESFramework entities, character prefabs, DataInfo entry points, components, attachment points, motion, control, tags, pooling, and world integration. Use when creating a player, NPC, vehicle, projectile host, character template, entity component, or changing an entity prefab hierarchy or lifecycle.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# Author ES Entities

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
- A current user request directly authorizes bounded entity source and Prefab/Assets changes. Runtime, external, destructive and Git actions must be explicitly named; managed-channel plans and commands are protocol inputs only.
- Record evidence for positive, invalid-input, denied-expansion, repeat-idempotency, and interruption-recovery cases.

Build entities from the current character, prefab, DataInfo, control, and pooling contracts. Do not treat archived proposals as implemented architecture.

## Workflow

1. Read the AIWarnings start files and `references/project-map.md`.
2. Classify the target as player, NPC, vehicle, projectile host, pooled generic object, or scene-only actor.
3. Select one matching AICommand when available. Use informational or plan commands only for their stated read-only purpose.
4. Inspect an existing current-source example with the same role. Confirm the authoritative DataInfo, prefab entry, component ownership, tags, attachment points, motion, and control path.
5. Define spawn, enable, disable, despawn, pool return, and destruction responsibilities before editing.
6. Keep content data, runtime state, scene references, and reusable services in their proper owners.
7. Validate prefab hierarchy, missing bindings, pooling callbacks, control arbitration, tag identity, and runtime allocations as applicable. For Shot, Projectile, HitScan, Beam, or Weapon Fire work, run `scripts/Test-ESProjectileWeaponHotPath.ps1` before reporting the path as complete.
   使用 [实体合同验证器](scripts/Test-ESEntityContractPacket.ps1) 检查稳定 Prefab 身份、Parts 所有权、生命周期、控制、运动和清理。
8. Use `$es-unity-compile` for import, Console, EditMode/PlayMode, and runtime evidence; run `$es-utf8-guard` for text changes.

## Required boundaries

- Treat `Documentation/CHARACTER_PREFAB_CONTRACT.md` as required for character and player prefabs.
- Treat `Documentation/ES_GENERIC_LIFE.md` as required for pooled lifecycle work.
- Do not promote a file under `90_提案与废止（Archive）` into current fact without source verification.
- Do not store per-instance mutable state in shared authoring assets.
- Do not bypass active-request arbitration for control, camera, UI focus, or similar ownership.
- Do not add parallel entity roots or hidden scene scans when an existing registration path is authoritative.

## Delivery

Report the entity category, authoritative data and prefab entry, component ownership, lifecycle table, control/tag/pool integrations, Unity evidence, and missing runtime checks.
