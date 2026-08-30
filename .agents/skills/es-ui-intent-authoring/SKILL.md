---
name: es-ui-intent-authoring
description: Convert a player's UI goal into a bounded IntentSpec that selects a registered game-screen family, primary and secondary actions, information priorities, layout preferences and fixture states. Use when AI must clarify or plan a game UI from requests such as inspect, equip, compare, configure, claim, navigate or respond; do not use it to implement runtime menus, gameplay logic or domain data.
---

# ES UI Intent Authoring

Use this Skill as the semantic planning layer before `es-ui-prefab-authoring`. It turns a
natural-language player goal into a deterministic candidate plan; it does not create a Prefab,
Fixture Scene, runtime Window, Presenter, input binding or business state.

## Skill 使用披露

遵循项目根 `AGENTS.md` 和 `.agents/README.md` 的 Skill 使用披露规范。首次用户可见进度
必须说明本 Skill 正在把玩家目标转换为 UI 装配计划；最终答复必须列出实际使用的 Skill。
披露不等于授权、运行或验收证据。

## Contract

### Knowledge preflight (mandatory for high-risk UI work)

Before classifying or emitting an IntentSpec for a high-risk task, read the shared
contract at `.agents/skills/es-skill-governance/references/ui-knowledge-preflight-contract.md`.
High risk includes reference-image interpretation, Canvas/anchor/responsive decisions,
AssetManifest/font/fallback choices, Fixture/visual evidence, or any Prefab/Scene/runtime
handoff. Resolve `AIBRAIN_ENTRY.md -> KnowledgeIndex.yaml -> canonical Knowledge owner`,
read every selected `requiredReads` and SourceRef, and verify current SHA-256/stale state.
Record `selectedKnowledgeIds`, `requiredReads`, `sourceRefs`, `staleCheck`, `authority`,
`evidenceLevel`, `nonClaims` and `decision` in the plan/receipt. Missing route, unread
Knowledge, index mismatch or stale SourceRef is `Blocked` (`NoKnowledgeRoute`,
`KnowledgeReadRequired`, `KnowledgeIndexMismatch` or `KnowledgeStale`). Only an explicit
user statement that Knowledge is not applicable may produce `exempted`, with scope and
reason recorded; never infer that exemption. Low-risk read-only clarification may bypass
the full preflight but still follows the normal authority and boundary rules.

Read `references/intent-spec.contract.md`, `references/player-intent-registry.json`, and
`references/aispace-output-contract.md` before authoring. Emit one JSON object with
`schemaVersion: 1`, a stable `intentId`, exactly one
`primaryAction`, zero or more registered `secondaryActions`, one or more registered
`screenFamilies`, `informationPriority`, `requiredStates`, `layoutPreferences`, and explicit
`missingInputs`, `blockedWhen`, `businessBridge`, and `visualOnly` fields.

The player intent registry is authoritative for registered actions and screen families. The
clarification gate must block unresolved or competing intent instead of guessing.

The intent is a candidate: confidence below `0.75`, multiple competing primary actions, an
unknown screen family, or missing required information must produce `status: "blocked"` or
`status: "needs-clarification"`; never silently guess. A confirmed intent may be adapted into a
ScreenSpec v3 candidate, then validated and materialized only by `es-ui-prefab-authoring`.

## Workflow

1. Classify the user's goal using the registry. Separate the player objective from nouns such as
   “inventory”, “shop” or “map”. Do not infer `equip`, `buy`, `sell` or other mutations from a
   noun alone.
2. Choose exactly one primary action. Record secondary actions only when they support that goal.
3. Select a registered screen family and information priorities. Prefer the smallest family that
   can present the goal; record uncertainty instead of inventing a new family.
4. Select wide and narrow layout preferences, required visual states, input modalities and a
   future business bridge ID. Bridge IDs are declarations only; they contain no inventory,
   economy, quest, combat or save data.
5. Validate the JSON with `scripts/validate_intent_spec.py`. This is static evidence only.
6. Pass a confirmed IntentSpec to the ScreenSpec v3 authoring flow. Do not write Unity assets
   directly from this Skill.

Canonical examples are under `references/examples/`. Use them as shape and boundary examples,
not as domain data or templates to copy blindly. `ambiguous-goal.json` is intentionally blocked
and demonstrates the clarification path.

## Boundary rules

- Allowed: classification, clarification questions, semantic intent plans and deterministic
  screen/layout/state recommendations.
- Forbidden: runtime radial/command menus, `ESUIWindowDefinition`, presenters, input systems,
  business facts, fake item/price/stat data, direct Prefab/Scene writes, asset publishing,
  screenshots and release claims.
- `visualOnly` must remain `true` for authoring plans. Runtime behavior requires a separately
  authorized runtime UI and input capability.

## Validation

```powershell
$env:PYTHONUTF8 = '1'
python .agents/skills/es-ui-intent-authoring/scripts/validate_intent_spec.py `
  path/to/intent-spec.json
```

Run the static replay after changes:

```powershell
.agents/skills/es-ui-intent-authoring/scripts/Test-es-ui-intent-authoring-StaticReplay.ps1 `
  -ProjectRoot .
```

Static replay covers normal, invalid, ambiguous, denied-expansion, repeat/idempotency and
interruption evidence. It does not prove Unity, runtime input, visual fidelity or player usability.
Deterministic replay must preserve the normalized plan and validation hash for identical inputs.

## Workflow controls

- Keep output project-relative and limited to the IntentSpec candidate and its static receipt.
- Write those task-scoped artifacts only under
  `ES/AISpace/Local/<agent-or-task>/Temp/UIIntent/`; the AISpace binding is a stable
  discovery relation, not a runtime or Unity-asset permission.
- Reject malformed, ambiguous or business-shaped input; never silently upgrade a clarification
  result to `confirmed`.
- Keep runtime UI, input, Presenter and domain facts behind separately owned capabilities.
- Rerun validation after any registry or contract hash change and preserve the last valid receipt.
