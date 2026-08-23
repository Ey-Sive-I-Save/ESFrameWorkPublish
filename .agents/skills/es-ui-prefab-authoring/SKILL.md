---
name: es-ui-prefab-authoring
description: AI-driven generation of high-fidelity Unity game UI prefabs and fixture scenes from a visual brief or reference image. Use for HUD, navigation, modal, conversation, collection, combat, world and system screens. The output is visual UI plus deterministic evidence; runtime Window, Presenter, domain and input systems are out of scope.
---

# ES Game UI Prefab Authoring

This Skill has one production protocol: `ScreenSpec v3`. The AI owns visual interpretation and
semantic layout decisions; Unity owns deterministic hierarchy creation, serialization and GPU
evidence. There is no alternate contract, manual workbench, linear operation list or hidden
business implementation.

Static acceptance treats the component registry as authoritative metadata and is read-only: it validates and plans bounded outputs without writing Unity assets. Runtime materialization and GPU evidence remain separate claims.

## Skill 使用披露

使用本 Skill 时，AI 必须在首次用户可见进度更新中说明正在使用
`es-ui-prefab-authoring`，并说明它负责 ScreenSpec v3、Prefab、Fixture Scene
和视觉证据生成。最终答复必须列出本轮实际影响工作的 Skill 及其作用。

本 Skill 遵守项目根 `AGENTS.md` 和 `.agents/README.md` 的 Skill 使用披露、
知识路由、权限边界和验证规范。披露不等于授权，也不等于运行时或 Unity 验收证据。

## Capability model

Every generated screen is composed of six explicit layers:

1. **ScreenSpec**: semantic component tree, screen family, profiles, states and visual intent.
2. **AssetManifest**: declared asset slots with source, hash, provenance and fallback status.
3. **LayoutPlan**: bounds, anchors, pivots, size rules, safe-area policy, Canvas ownership and
   responsive variants. Anchor and Canvas governance are separate checks.
4. **BehaviorSpec**: visual interaction intent and state transitions such as selected, disabled,
   loading, empty, error and long-content. It reserves links only; it does not implement gameplay.
5. **Fixture Driver**: deterministic profile/state values used for scene generation and captures.
6. **Materializer**: `ESUIGameScreenMaterializer`, the only Unity production entrypoint. It emits a
   Prefab, Fixture Scene, semantic snapshots and GPU-backed PNG evidence.

The registry in `references/game-ui-component-registry.json` is the knowledge base for screen
families, component types, required zones and visual variants. Extend the registry before adding
a new reusable component. Do not add inventory, combat, economy, navigation or Window-specific
branches to the materializer.

## Scope boundary

Allowed: high-fidelity UGUI composition, reusable visual components, responsive layout variants,
mock state fixtures, asset fallbacks, screenshots and structural/visual evidence.

Not allowed: `ESUIWindowDefinition`, `ESUIWindowCatalog`, `ESUIRootCoordinator`, Presenter logic,
runtime data mutation, inventory/combat/economy ownership, resource publishing or release claims.
Business integration is represented by stable reserved IDs in a future bridge owned by another
Skill; this Skill never executes those links.

## Required context

Before authoring, read:

- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Documentation/ES_UI_AUTHORING_WORKFLOW.md`
- `references/game-ui-component-registry.json`
- `references/game-ui-screen-spec.v3.template.json`
- `references/game-ui-materializer-contract.md`

Use `references/commercial-ui-patterns.md`, `references/high-fidelity-ui-recipes.md` and
`references/ai-visual-brief.md` as visual knowledge, not as executable authority.

## AI production loop

### 1. Interpret the brief or reference

Identify screen family, visual hierarchy, major regions, content roles, typography, color roles,
spacing scale, asset slots, safe-area requirements and uncertainty. For a reference image, record
measured image hashes and distinguish observations from assumptions. Never infer interaction or
responsive behavior from pixels alone.

### 2. Author ScreenSpec v3

Start from `references/game-ui-screen-spec.v3.template.json`. The spec must contain:

- `schemaVersion: 3`, stable lowercase `screenId` and a registered `template`;
- one or more positive viewport `profiles` and deterministic `states`;
- an `assets` manifest with source classification (`project-sprite`, `ai-generated` or
  `generated-placeholder`), fallback and hash/provenance fields;
- a recursive `components` tree where every node has `type`, `zone`, `layout`, content/asset
  intent and `stateVariants`;
- `behaviors` that describe visual input intent without calling runtime systems;
- design evidence for reference regions, visual decisions, responsive decisions and assumptions.

Validate before touching Unity:

```powershell
$env:PYTHONUTF8 = '1'
python .agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py `
  Assets/UI/Contracts/<screen>.screen-spec.v3.json
```

### 3. Materialize deterministically

Compute the spec SHA-256 and invoke the fixed Unity batch entrypoint:
`ES.Editor.ESUIGameScreenMaterializer.RegenerateFromSpecBatchMode`.
Pass project-relative `Assets/UI/` spec and generated Prefab/Scene paths, an evidence root under
`ES/UIEvidence/`, the spec hash, profile matrix and state matrix. The materializer rejects every
non-v3 input and every undeclared field. It must be safe to rerun with identical inputs.

The generated hierarchy must preserve semantic IDs, use explicit anchors/pivots and layout groups,
keep one root Canvas/CanvasScaler by default, and create nested Canvas boundaries only when the
spec declares a sorting or rebuild-isolation reason. It must not add runtime business components.

### 4. Inspect and iterate

Read the generated Prefab/Scene snapshots and PNGs for every declared profile/state pair. Check:

- anchor, pivot, parent, sibling order and normalized bounds;
- root/nested Canvas roles, scaler ownership, safe-area containment and rebuild scope;
- text wrapping, minimum interaction target, overlap and clipping;
- asset hash/provenance/fallback status;
- state visibility and visual variant changes;
- non-empty GPU evidence at the declared resolution.

When a mismatch is found, revise the ScreenSpec or registry entry, rerun validation and rematerialize.
Never patch generated YAML by hand and never treat a static pass as visual proof.

### 5. Acceptance evidence

A completed visual authoring run reports the input spec hash, Unity version, profile/state matrix,
Prefab path, Fixture Scene path, semantic snapshot paths, PNG paths and unresolved placeholders.
Acceptance requires both structural evidence and fresh GPU screenshots. A successful headless
process alone is not acceptance. Runtime input, player usability and release performance require
separate project-owned tests.

## Reference files and scripts

- `references/game-ui-component-registry.json`: reusable component/template knowledge base.
- `references/game-ui-screen-spec.v3.template.json`: starting protocol shape.
- `references/game-ui-materializer-contract.md`: materializer behavior, fallback and evidence rules.
- `scripts/validate_game_ui_screen_spec.py`: ScreenSpec v3 and registry validator.
- `scripts/screen_spec_adapter.py`: in-memory v3-to-Unity semantic adapter; it writes no files.
- `scripts/self_test_game_ui_platform.py`: deterministic registry self-test across four screen families.

The three scripts above are the only Skill production scripts. Any removed or unlisted script is not
part of this protocol and must not be recreated as a parallel authority.

## Safety and recovery

Keep all paths project-relative and preserve existing `.meta` files. Limit writes to the declared
generated UI and evidence roots. On interruption, keep the last known-good Prefab/Scene and rerun
from the same spec hash. Do not overwrite a visual baseline unless the user explicitly approves a
new reference. External AI vision calls are optional, HTTPS-only, bounded and recorded as input
evidence; provider output is never allowed to write Unity assets directly.

## Current status

The platform protocol and main-menu vertical slice are implemented and verified. Generic screen
families are registry/self-test coverage, not proof that every family has been materialized in Unity.
Commercial completion, runtime integration and player usability remain unclaimed until their own
Unity evidence exists.
