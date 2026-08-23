---
name: es-tag-config
description: Add or modify ESFramework ESGameTag, ESTag stable references, ConfigKey definitions, catalogs, bake tables, and runtime-key mappings. Use for new tags, enum or string configuration identities, catalog generation, ConfigKey injection, duplicate-key failures, or stable-identity migration work.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# Maintain ES Tags and Configuration

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and deny-by-default; capability visibility never grants permission. Use AIBrain `planTask`, the matching AICommand, and the current TaskContract before any write or external operation.

## Responsibility-specific static acceptance

- Profile: `authoring`
- Custom checks: `change-boundary, resource-projection, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- Scope and authority are checked before execution; stale or missing evidence blocks the task.
- Execute only through AIBrain planTask and the matching AICommand; direct execution is denied.
- Record evidence for positive, invalid-input, denied-expansion, repeat-idempotency, and interruption-recovery cases.

Preserve stable identity from authoring through baking and runtime lookup. Names and inspector labels are not runtime identity.

## Workflow

1. Read the AIWarnings start files and `references/project-map.md`.
2. For a new GameTag, select `新增GameTag_AI命令.md`; for other changes select the closest command without borrowing write permission from an informational command.
3. Determine whether the requested identity is an enum-backed key, string-backed key, stable tag reference, catalog entry, or runtime key.
4. Inspect the current definition, bake path, collision rules, serialization shape, and all consumers before editing.
5. Preserve numeric and serialized identity. Add migration only when the old-to-new mapping is provable.
6. Validate duplicate definitions, unset values, unknown references, hash collisions, retained table behavior, and deterministic rebuilds.
   Run [the stable identity manifest validator](scripts/Test-ESStableIdentityManifest.ps1) for persisted identity evidence; it rejects process-local `RuntimeKey`/`RuntimeId` fields.
7. Run focused ConfigKey or tag catalog tests and use `$es-unity-compile` for Unity evidence.
8. Run `$es-utf8-guard`, especially when editing Chinese display names or documentation.

## Required boundaries

- Do not use display text, enum names, list positions, or transient instance IDs as stable runtime identity.
- Do not renumber existing enum-backed values casually.
- Do not let a ConfigKey masquerade as a GameCore root object.
- Do not accept duplicate or ambiguous mappings by silently taking the first match.
- Do not regenerate catalogs by hidden editor startup scans.
- Persist stable tag/config identities and schema hashes only; resolve RuntimeKey after the current catalog is loaded.

## Delivery

Report the identity type, old and new serialized values, bake/runtime mapping, collision checks, consumers, tests, and migration gaps.
