---
name: es-gamecore-integration
description: Integrate features into ESFramework GameCore root ScriptableObjects, ESGameCoreRuntimeData, global indexes, static ESGameManager modules, and reinjection lifecycles. Use when adding GameCore data, registering a global service or asset index, changing RuntimeData initialization, or reviewing whether data belongs at the GameCore root.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# Integrate ES GameCore

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

Preserve the root ownership and stable-runtime contracts. Do not use a key, nested field, or convenience wrapper to imitate a real GameCore root registration.

## Workflow

1. Read the AIWarnings start files and `references/project-map.md`.
2. Select exactly one matching AICommand as the authorization contract. Prefer the root-SO command for new root data and the global-index command for indexes.
3. Trace the current path from authoring SO to GameCore root field, injection entry, stable runtime object, and consuming module before editing.
4. Decide whether the feature is root data, retained RuntimeData, a static GameManager module, a resource index, or ordinary nested configuration. Do not collapse these roles.
5. Preserve stable object identity during reinjection. Replace internal tables or providers transactionally instead of invalidating cached service references.
   使用 [GameCore 集成包验证器](scripts/Test-ESGameCoreIntegrationPacket.ps1) 检查根所有权、显式注入、同实例重注入、事务回滚和禁止的隐式注册。
6. Add or update focused tests for ownership, duplicate registration, reinjection, and lifecycle behavior.
7. Use `$es-unity-compile` for Unity import, Console, domain reload, and test evidence. Keep generated-project builds separate.
8. Run `$es-utf8-guard` and record remaining untested layers.

## Required boundaries

- Keep GameCore root injection explicit and source-visible.
- Do not disguise core data behind ConfigKey, a nested payload, reflection-only discovery, or editor scan side effects.
- Do not rebuild stable RuntimeData service objects when the contract requires reinjection into the existing instance.
- Do not introduce global access through a parallel singleton when `ESGameManager` or the root SO is authoritative.
- Preserve existing dirty-worktree changes and do not edit generated project files.
- 持久化身份不得使用 RuntimeKey/RuntimeId。

## Delivery

Report the ownership category, authoritative root, injection and reinjection path, consumers, tests, Unity evidence, and any lifecycle or release gaps.
