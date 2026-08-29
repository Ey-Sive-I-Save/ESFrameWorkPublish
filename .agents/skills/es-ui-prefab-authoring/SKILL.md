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

### Knowledge preflight (mandatory for high-risk UI work)

Before interpreting a brief, selecting a layout or producing a ScreenSpec, read the shared
contract at `.agents/skills/es-skill-governance/references/ui-knowledge-preflight-contract.md`.
This Skill's work is high risk whenever it touches reference images, ScreenSpec, AssetManifest,
Canvas/anchors/safe area/responsive profiles, fonts/fallbacks, BehaviorSpec/focus, Fixture,
Prefab/Scene, Materializer, screenshots or visual QA. Resolve `AIBRAIN_ENTRY.md ->
KnowledgeIndex.yaml -> canonical Knowledge owner`, read every selected `requiredReads` and
SourceRef, and verify current SHA-256/stale state before planning or writing. Record
`selectedKnowledgeIds`, `requiredReads`, `sourceRefs`, `staleCheck`, `authority`,
`evidenceLevel`, `nonClaims` and `decision` in the plan/receipt. Missing route, unread
Knowledge, index mismatch or stale SourceRef must fail closed as `NoKnowledgeRoute`,
`KnowledgeReadRequired`, `KnowledgeIndexMismatch` or `KnowledgeStale`. Only an explicit
user statement that Knowledge is not applicable can produce `exempted`, with exact scope
and reason recorded; the AI must not infer an exemption. Purely read-only low-risk questions
may bypass the full preflight but do not bypass project authority or evidence boundaries.

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

## Failure feedback gate (mandatory)

Read `references/ui-failure-feedback-rules.md` before every authoring or iteration run. Treat the
previous batch's weakest visual or structural finding as an input contract, not as commentary.
Before writing a new ScreenSpec, record:

- the prior evidence batch and its failing rule ID;
- the exact ScreenSpec, registry, validator or materializer field that will change;
- the expected observable effect in wide and narrow captures;
- the evidence check that can falsify the change.

If no artifact or executable check changes, stop with `feedback-not-incorporated`; do not create
another screenshot batch. `Completed`, file existence, static validation or non-empty PNGs never
override `visualAcceptance: not-claimed` when a feedback rule is still failing.

The first visual pass must establish screen family, focal subject, primary action, zone hierarchy,
profile-specific reflow and asset provenance before micro-styling. A materializer patch that only
changes colors, cache IDs or decoration is not a capability upgrade and cannot close a failed run.

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
- `Documentation/AIKnowledge/UI/game-ui-open-source-automation-source-snapshot.md`
- `Documentation/AIKnowledge/entries/game-ui-capability-readiness.md`

Use `references/commercial-ui-patterns.md`, `references/high-fidelity-ui-recipes.md` and
`references/ai-visual-brief.md` as visual knowledge, not as executable authority.

## AI production loop

### 1. Measure and interpret the brief or reference

Identify screen family, visual hierarchy, major regions, content roles, typography, color roles,
spacing scale, asset slots, safe-area requirements and uncertainty. For a reference image, record
measured image hashes and distinguish observations from assumptions. Never infer interaction or
responsive behavior from pixels alone.

When a reference image is available, run `scripts/ingest_ui_reference.py` before authoring the
ScreenSpec. The receipt records the immutable image hash, source dimensions, border-background
estimate, conservative connected-region bounds and candidate anchor hints. Its regions are
measurement candidates, not semantic truth: labels, parent ownership, OCR, interaction and
responsive behavior still require an explicit design decision or source IR. A missing receipt,
zero-region result or unreadable image blocks reference-driven materialization.

### 2. Author ScreenSpec v3

Start from `references/game-ui-screen-spec.v3.template.json`. The spec must contain:

- `schemaVersion: 3`, stable lowercase `screenId` and a registered `template`;
- an `intentContract` whose requested screen family matches `screenType`, whose primary intent
  is present in component interactions, and whose `visualTarget`, `fidelityMode`, reference
  policy and product boundary are explicit. A request for a named/reference-driven style must
  fail closed when no reference source is supplied; it must never silently become an original
  generic screen.
- one or more positive viewport `profiles` and deterministic `states`;
- an `assets` manifest with source classification (`project-sprite`, `ai-generated`,
  `generated-procedural` or `generated-placeholder`), fallback and hash/provenance fields;
- a recursive `components` tree where every node has `type`, `zone`, `layout`, content/asset
  intent and `stateVariants`;
- every component must bind an explicit `layout.anchor` (`strategy`, `edge`, `pivot`,
  `safeArea`), `colorToken`, `typographyRole`, `layerRole` and deterministic `siblingOrder`.
  Adapter defaults are compatibility fallbacks for legacy packets only; they are not valid
  design decisions for a quality-gated packet.
- strict validation checks geometric meaning, not merely field presence: `edge-docked` bounds
  must reach their declared local edge, `center` must use a centered axis with a center pivot,
  and `stretch` must span a local axis. Per responsive profile, sibling orders must be unique
  and non-decreasing by declared layer role; the requested primary action must use the
  `primaryAction` color token.
- high-fidelity packets also declare `designContract.advancedComposition` and run
  `--require-advanced-composition`: exactly one primary action mapped per profile, an explicit
  focal-subject decision, protected focal/action separation when a subject exists, key alignment
  and clearance relationships, responsive semantic equivalences, focal-subject asset/crop
  policies, and post-layout interaction-density groups. Focal asset policies bind every subject
  slot to one AssetManifest crop policy, normalized focal point, positive finite
  `sourceAspectRatio` and `atlasRotationPolicy: disallow-rotation`; density groups validate the
  resolved LayoutGroup target sizes and pairwise gaps. `contentRequirements` must additionally
  declare per-profile minimum component/text/interaction counts, required component grammar and
  required zones; `requiredTokenConsumers` prevents declared color roles from becoming unused
  decoration; `stateTokenBindings` ties selected/error signals to semantic focus/danger tokens;
  `spacingScalePx` rejects one-off gap and padding values. Also require `visualHierarchy` with
  ranked component bands and an explicit primary-action band; `interactionContract` with a
  per-profile focus order whose first target carries the requested primary intent;
  `stateImpactPolicy` with a per-profile affected-component budget; and `anchorContract` with
  final numeric Unity `anchorMin`, `anchorMax` and `pivot` values for key components. ScreenSpec
  bounds use top-left coordinates, but the anchor contract uses Unity bottom-left RectTransform
  coordinates; convert the bounds Y axis and preserve the explicitly authored Unity pivot.
  LayoutGroup-managed children cannot claim authored final anchors. Disabled and loading states
  must set every profile-specific primary action to `interactable: false`. These constraints make
  composition, focus, state scope and anchor projection falsifiable; they do not turn static
  geometry into Unity, GPU or visual acceptance.
- `behaviors` that describe visual input intent without calling runtime systems;
- design evidence for reference regions, visual decisions, responsive decisions and assumptions.

For strict feedback-gated authoring, also provide:

- `stateSemantics` for every declared state. Use `default`, `selected`, `empty`, `loading`,
  `disabled`, `error`, `long-content` and, when artwork can be unavailable, `missing-art`.
  Each state needs concrete `fixtureData`,
  `affectedComponentIds`, `visualChanges`, `interactionChanges`, a `geometryPolicy`, and one
  `effects` entry for every affected component. Each effect declares a component ID plus executable
  changes from `visible`, `interactable`, `graphicAlpha`, `graphicColor`, `wrapText`, `text`, or
  `outline`; prose alone is not state evidence.
  A state name or empty `stateVariants` object is not state evidence. In strict packets,
  the binding is bidirectional: every affected component must declare that state variant,
  and every non-`default` component state variant must appear in that state's
  `affectedComponentIds`. `default` is the baseline exception.
- Every fixture string intended for a text component uses an explicit
  `fixtureTextBindings` record: `componentId`, `fixtureDataKey`, `overflowPolicy`, positive
  `maxLines`, `contentInsetsPx`, and `reserveActionClearancePx`. Never guess a text target from a
  fixture-data key or component name. Long-content binds every affected textual component; bindings
  cannot target controls, compete with `effects[].changes.text`, or use `scroll` before a
  registered scroll-container recipe exists.
- `geometryPolicy.preserveBounds: true` for every state. State variants only declare
  Fixture participation: they may not contain bounds, anchors, pivots, layout modes,
  sizes, safe-area, parent or sibling-order changes. Executable effects are visual or
  interaction changes only. The Python Validator, standalone Layout Resolver and Unity
  Materializer all reject a state-local geometry mutation, so long-content must wrap,
  ellipsize or scroll inside its authored rectangle instead of moving action targets.
- `bindings` entries for every `designEvidence.feedback.ruleIds`. Each binding names the
  affected components, profiles, states, evidence requirements and the next artifact fields that
  must change after a failure.
- `layout.childGeometryOwner` on every `grid`, `list` or `flow` container with children.
  `parent-layout-group` means Unity's layout group owns child geometry; `child-bounds` means
  explicit child bounds own it. The Python adapter and `ESUIScreenSpecAdapter` must emit the same
  layoutSpec for either choice.
- `profileAvailability` when responsive variants omit an input intent. Record the omission reason;
  do not leave other agents to infer whether a control was accidentally lost.
- `qualityGates` for production-oriented packets: `assetPolicy`, `responsivePolicy`, `colorPolicy`
  and `typographyPolicy`. `productionReady: false` is valid for a planned fixture, but it must
  explicitly remain `commercialAcceptance: deferred`; production-ready packets require resolved
  asset provenance/license/import/crop/atlas fields and a concrete TMP Font Asset/fallback chain.
  Distinct fallback IDs must also carry `fallbackFontAssets` records with project-relative path,
  hash and license; an ID without a verified file or glyph coverage is not a fallback.

Validate before touching Unity:

```powershell
$env:PYTHONUTF8 = '1'
python .agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py `
  Assets/UI/Contracts/<screen>.screen-spec.v3.json `
  --require-feedback --require-quality-gates --require-advanced-composition
```

With a project-root path available, the quality gate also reads the declared Font Asset and
production asset files, verifies they remain inside the project, and recomputes their SHA-256.
Changing a hash string without changing the file is rejected; a valid static result still does
not prove that Unity imported the Sprite or rendered the glyphs on a target device.

Resolve the authored geometry before materialization with
`scripts/resolve_ui_layout_plan.py`. The resolver evaluates each declared profile inside its
safe-area rectangle, converts normalized bounds to pixel rectangles, applies `list`/`flow`/`grid`
geometry ownership, and reports minimum-size, containment and sibling-overlap conflicts with
repair suggestions. A layout-group child rectangle is already resolved in pixels and must not be
scaled again from its authored bounds. A passed layout receipt is static geometry evidence only;
it does not prove Unity `RectTransform` values, layout rebuild order or GPU composition.
`safeArea: ignore` is not a generic escape hatch: it is valid only for a top-level stretch
background and resolves against the full profile rectangle. Content, actions and nested nodes must
resolve inside or inherit their parent safe area.
The receipt also emits `textFit` evidence per profile/state binding: text hash, resolved pixel
rectangle, conservative mixed-script line estimate, available lines, overflow policy, explicit
ellipsis truncation, and action-clearance diagnostics. A wrapping binding that exceeds capacity
blocks; ellipsis may pass only with `truncated: true` recorded.
It also emits `interactionDensity` evidence after list/flow/grid resolution: active target count,
resolved target dimensions, minimum observed pairwise gap and the declared group threshold.
For every `focal-cover`, it emits `focalCropFeasibility`: resolved target aspect, declared source
aspect, projected normalized cover UV and whether the protected safe-crop region can fit. An
impossible protected region blocks static layout instead of waiting for a screenshot. This remains
deterministic static geometry evidence, not a Unity layout-rebuild, Sprite import or device usability
claim.

Evaluate design tokens before materialization with `scripts/evaluate_ui_tokens.py`. It maps semantic
roles to concrete tokens, computes WCAG contrast for text/action/feedback surfaces, and records which
components consume each token. Accent and danger surfaces must declare readable `onAccent` and
`onDanger` foregrounds; a raw hex value or a visual variant without a consumer trace is not a
commercial color decision.

Evaluate typography with `scripts/evaluate_ui_typography.py`. It checks the declared TMP Font Asset
path/hash, serialized Unicode table, rendered component and state fixture strings, locale fixture
declarations, distinct fallback metadata and the union of primary/fallback glyph tables. Missing
glyphs, an unverified fallback file or a chain that still cannot cover the fixture blocks the static
gate; dynamic TMP population is a candidate, not proof of rendering. The resolver discovers the
Unity project root for nested generated packets; `--project-root` remains available when a
workspace contains multiple Unity projects.

### 3. Materialize deterministically

Before invoking Unity, resolve every declared asset slot with
`scripts/resolve_ui_asset_manifest.py`. The resolver is read-only against the ScreenSpec and
emits a manifest receipt containing the selected project path, Unity GUID, dimensions, raw file
hash and provenance decision. An auto-discovered generated asset is still
`commercialAcceptance: deferred`; it cannot be promoted by the resolver. A declared hash
mismatch, missing `.meta` GUID or missing provenance for a project/AI asset blocks the run.

Compute the spec SHA-256 and invoke the fixed Unity batch entrypoint:
`ES.Editor.ESUIGameScreenMaterializer.RegenerateFromSpecBatchMode`.
Pass project-relative `Assets/UI/` spec and generated Prefab/Scene paths, an evidence root under
`ES/UIEvidence/`, the spec hash, profile matrix and state matrix. The materializer rejects every
non-v3 input and every undeclared field. It must be safe to rerun with identical inputs.

The Unity adapter preserves `profiles`, `states`, `qualityGates` and asset provenance instead
of dropping them at the JSON boundary. `qualityGates.typographyPolicy` selects the declared TMP
Font Asset, verifies its file hash and required glyphs, and applies its material to every generated
text node; a missing or mismatched font blocks materialization. `assetPolicy` verifies production
asset source/path/hash/provenance before allowing a production-ready packet. `responsivePolicy`
configures the CanvasScaler and `profile.safeArea` constrains each Wide/Narrow profile root when
the policy is `profile-safe-area-inset`. Planned procedural assets remain explicit fallbacks.

The generated hierarchy must preserve semantic IDs, use explicit anchors/pivots and layout groups,
keep one root Canvas/CanvasScaler by default, and create nested Canvas boundaries only when the
spec declares a sorting or rebuild-isolation reason. For `focal-cover`, the Materializer rejects
SpriteAtlas rotation, then compares the declared `sourceAspectRatio` to the resolved Sprite UV
aspect (1% tolerance) before serializing evidence. It must not add runtime business components.

After Unity produces snapshots, run `scripts/validate_ui_snapshot_evidence.py` against the exact
ScreenSpec hash and evidence root. It rejects a missing editor/UI profile-state pair, mismatched
panel/run/spec identity, empty semantic lists, missing focal-crop records for declared focal assets,
or a serialized `safeCropSatisfied: false`. For every matching profile/state pair it also requires the
same non-empty root path and complete Canvas metadata (`renderMode`, `uiScaleMode`, positive
`referenceResolution`, `screenMatchMode`, and finite `match` in `[0, 1]`), exact profile viewport and runtime screen dimensions,
the same unique semantic path set rooted under that root, matching boolean active state, matching
`parentPath`, `siblingIndex`, `anchorMin`, `anchorMax` and `pivot`, and matching finite non-negative
editor `screenRect` versus UI `screenX/Y/Width/Height` (0.01-pixel tolerance). Every runtime rectangle
must remain inside its profile viewport. The paired snapshots also serialize and compare active
`LayoutGroup`/`ContentSizeFitter` axis ownership; a parent group and child fitter may not actively
control the same width or height axis. Active Buttons with a declared `interactionTarget` must meet the
declared resolved width and height. Every pair also compares `visibility` and `inputReachability`:
Mask/RectMask2D ancestry, RectMask2D visible intersection/fraction, CanvasGroup filtering and a
conservative same-parent opaque raycast blocker. An active, interactable target must retain its minimum
size in the visible rectangle and report no blocker. A non-rectangular Mask over a target is deliberately
unproven and requires runtime raycast evidence. This does not impose parent containment: overlays,
tooltips and intentional clipped effects may exceed parent bounds. This is a snapshot-structure and
cross-channel geometry gate only: it cannot prove that the Unity
process ran, that the Prefab/Scene persisted, that UGUI performed its final layout rebuild, or that
PNG pixels are visually valid.

Run `scripts/validate_ui_gpu_evidence.py` only after the paired snapshots and GPU PNGs exist. It
recomputes every PNG's SHA-256, byte length, viewport dimensions, alpha coverage, sampled color
buckets and pixel-edge transitions, then cross-checks those values against both snapshots. It rejects
mixed runs, old PNGs, wrong-size captures, transparent frames and uniform/zero-edge frames. A pass is
pixel-integrity evidence only, never a claim that composition, brand fidelity, accessibility or player
usability passed visual review. When a non-default `stateSemantics` entry declares `visualChanges` or
effects, it also compares that profile's PNG with `default`: an identical or below-threshold delta is
rejected as `state-pixel-undifferentiated`. The default editor snapshot maps every
`affectedComponentIds` entry to its unique profile-local `screenRect`, preferring an active node but
retaining a unique default-hidden node for a `visible: true` state; at least 80% of changed pixels must
fall inside their union after a four-pixel outline/shadow tolerance. Missing or ambiguous semantic rectangles, or a
change concentrated outside them, is rejected as `state-locality-*` or
`state-pixel-outside-affected-components`. This proves coarse correlation with declared components, not
exact rendering correctness, composition or commercial design.
The same capture also replays every executable `stateSemantics.effects` field against the UI snapshot.
`visible`, `interactable`, `graphicAlpha`, `graphicColor`, `wrapText`, `text` and `outline` must match
the materialized target value; effects that require Button, Graphic or TMP_Text require the corresponding
`hasButton`, direct `hasGraphic`, descendant `hasDescendantGraphic` or `hasText` snapshot capability. `graphicAlpha`
uses the Materializer's descendant-Graphic scope and must equal `descendantGraphicAlpha`; every traced
`descendantGraphicAlphas[].alpha` must also match, with unique paths rooted at the target component. `wrapText`
and `text` require every traced `descendantTextStates[]` value and path to match the same scope. `graphicColor`
and `outline` require a direct Graphic. A visually
different PNG with an unchanged or
unsupported declared effect is rejected as `state-effect-snapshot-mismatch` or `state-effect-evidence-*`.
This is semantic serialization evidence, not a substitute for a Unity interaction or design-quality review.

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

The static Validator also rejects same-profile sibling overlaps, child bounds that cannot satisfy
their declared minimum size, and duplicate semantic IDs across different branches. Overlay nodes
and children owned by a declared parent layout group are the only intentional exceptions.

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
- `scripts/ingest_ui_reference.py`: deterministic reference-image hash, pixel geometry and candidate
  region receipt; it does not infer semantic Unity layout or authorize writes.
- `scripts/screen_spec_adapter.py`: in-memory v3-to-Unity semantic adapter; it writes no files.
- `scripts/generate_ui_iteration_packet.py`: deterministic `rebuild` and `iterate` authoring packet generator; it writes only declared ScreenSpec and receipt outputs.
- `scripts/resolve_ui_asset_manifest.py`: project-local asset resolver and provenance/hash receipt generator; it never mutates the ScreenSpec.
- `scripts/resolve_ui_layout_plan.py`: profile/safe-area geometry resolver and deterministic layout conflict receipt; it never mutates the ScreenSpec.
- `scripts/evaluate_ui_tokens.py`: semantic token consumer, WCAG contrast and state-signal receipt generator; it never mutates the ScreenSpec.
- `scripts/evaluate_ui_typography.py`: TMP Font Asset hash/glyph/fixture/fallback receipt generator; it never mutates the ScreenSpec.
- `scripts/validate_ui_snapshot_evidence.py`: paired semantic snapshot identity and focal-crop safety validator; it never grants GPU visual acceptance.
- `scripts/validate_ui_gpu_evidence.py`: PNG/snapshot identity and pixel-integrity validator; it never grants design or commercial visual acceptance.
- `scripts/self_test_game_ui_platform.py`: deterministic registry self-test across four screen families.

The scripts above are the Skill production scripts. Any removed or unlisted script is not
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
