---
name: es-weapon-abc-part
description: >-
  Build and validate the bounded Weapon ABCP domain part on top of the
  independent ES AI ABC Core. Use for weapon intent-to-capability mappings,
  WeaponDefinition/Prefab/Input/Command adapters, dual-track migration, or
  weapon-specific evidence and acceptance planning.
---

# ES Weapon ABC Part (ABCP)

## Overview

This is the first domain-specific ABCP. It owns weapon semantics and routing,
but is **ABCC-backed** through stable IDs and schemas. It has its own Skill,
Knowledge and route stages; it is not a renamed copy of the Core or the old
mechanism-replication research Skill.

## Responsibilities

- **A** expresses weapon goals such as attack lifecycle, damage balance,
  Prefab/DataInfo binding and resource/cooldown behavior.
- **B** offers weapon capabilities backed by the existing ES ownership model:
  WeaponDefinition/GameCore, Entity/Prefab, Input, Command and TaskContext
  providers.
- **C** is the human designer, test AI or collaborator who supplies
  authorization, constraints and acceptance.

The canonical mapping is in
`ES/Automation/Contracts/es-ai-abc-weapon-part.v1.json`. It references
`es.ai-abc.core.v1`; do not paste Core contract text into this Part.

Static markers: **coreRef** binds the Part to ABCC; **ABCD.Dynamic** fallback
is explicit-only; static evidence remains **runtime-not-run**.

`KnowledgeIndex` and each Knowledge `SourceRef`/`ContentHash` are navigation
inputs only; drift makes the selected entry stale and requires re-planning.

## Part workflow

1. Freeze `weaponKey`, goal revision, scope and evidence expectations.
2. Emit an A intent and bind it to the ABCC contract.
3. Select only the Core capabilities needed by the weapon task. The Core still
   owns the complete ABCD parity set.
4. Accept a B offer only when schemas, preconditions, effects and failure codes
   match; otherwise replan or block.
5. Normalize evidence and ask C for acceptance. Static evidence cannot prove
   Unity import, firing, collision, performance or release behavior.

## Dual-track compatibility

Legacy adapters may coexist temporarily, but the Part is the canonical owner;
there is no silent merge. Any legacy-to-Part conversion must be explicit,
versioned and reversible. Fallback to `ABCD (Dynamic)` is explicit-only via
`es.ai-abc.dynamic-fallback.v1`; a failed Part cannot silently become Dynamic.

When weapon implementation is later changed, retain the ES boundary that
`ItemWeaponSharedData`/`ESWeaponRuntimeData` own reusable weapon definitions,
`ItemWeaponVariableData` owns instance state, and Combat executes rather than
defines new weapon parameters.

## Static and runtime boundaries

Use this Skill's static replay for deterministic contract, route and evidence
checks. Do not claim Runtime/Unity acceptance until the user explicitly asks
for it and fresh receipts exist.

## ABCP authoring toolchain

Use the project-local toolchain when creating a new Part instead of copying the
Weapon Part contract by hand:

1. Copy `references/abc-part-authoring-request.template.json` and edit only the
   domain request fields. The request is validated against
   `ES/Automation/Contracts/es-ai-abc-part-authoring-request-v1.schema.json`.
2. Run `scripts/New-ESAbcPartContract.ps1` with the project-relative request and
   output paths. It reads the authoritative ABCC Core contract, rejects
   unbound capabilities or duplicate mappings, and refuses to overwrite a
   different existing output unless `-Force` is explicitly supplied.
3. Run `scripts/Test-ESAbcPartContract.ps1` on the generated contract. The
   validator first checks Part semantics and RouteStage data-flow, then invokes
   both the ABCC Core and Weapon ABCP StaticDeepReplay receipts. A Part is not
   statically accepted when the ABCC replay, mapping closure, or route-stage
   closure fails.

The toolchain only produces static evidence. It never starts Unity, Player,
network or a host process, and it does not infer write or Runtime authority.

## Engineering controls

Identity, authority, risk, observability, recovery, performance, compatibility
and supply-chain controls are declared in `governance.json`. StaticDeepReplay
is the first verification path; Runtime requires fresh, explicit authorization.

## References

- Part contract: `ES/Automation/Contracts/es-ai-abc-part-v1.schema.json`
- Weapon instance: `ES/Automation/Contracts/es-ai-abc-weapon-part.v1.json`
- Core contract: `ES/Automation/Contracts/es-ai-abc-core-v1.json`
- Independent Knowledge: `Documentation/AIKnowledge/entries/weapon-abc-part.md`
- Weapon P0: `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_CombatModule武器定义迁移边界与验收门禁_AI协作警告.md`

## Skill 使用披露

遵循项目根 `AGENTS.md` 和 `.agents/README.md` 的 Skill 披露规则；本 Skill
只提供工作流，不扩大用户授权或产生运行时证据。
