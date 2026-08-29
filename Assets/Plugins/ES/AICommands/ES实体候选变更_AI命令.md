---
name: es-entity-candidate-change
description: Generate an isolated ES entity or prefab change candidate while preserving existing entity ownership and lifecycle.
---

## Direct effect contract

Command type: candidate content generation. It requires AIBrain `planTask` and writes only `ES/Automation/Candidates/EntityAuthoring/<request-id>/candidate/`.
命令类型：候选内容生成：ES 实体候选变更。
默认改文件：仅允许 `ES/Automation/Candidates/EntityAuthoring/<request-id>/candidate/`。
风险等级：L2。

## 必须先读

- `.agents/skills/es-entity-authoring/SKILL.md`
- `Documentation/CHARACTER_PREFAB_CONTRACT.md`
- `Documentation/ES_GENERIC_LIFE.md`
- `.agents/skills/es-skill-governance/references/es-preservation-refactor-contract.md`

## 交付格式

```ContractCompleteness
commandId: entity.change.candidate
cancellation: before commit; cancel leaves no formal entity or prefab
recovery: isolated candidate cleanup; NeedsReissue on uncertain state; no replay
validation: candidate schema, content hash, and isolated-path checks
evidenceRef: candidate path, SHA-256, receipt, and Static/Runtime status
allowRoots: ES/Automation/Candidates/EntityAuthoring/<request-id>/candidate/ only
denyPaths: .agents/skills, Assets/Plugins/ES/AICommands, Assets, Runtime, Git, release
deny-overrides: true
```

Produce an entity category, authoritative prefab/DataInfo entry, ownership and lifecycle table, compatibility diff, StaticDeepReplay results, and runtime claims not proven. Preserve ES registration, pooling, control arbitration, and serialization ownership.

## Prohibitions

No direct Unity asset mutation, hidden scene scan, duplicate entity root, runtime launch, or claim that a prefab is usable without runtime evidence.
