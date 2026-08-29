# Game UI Materializer Contract

The production path is headless and deterministic:

```text
AI brief/reference
-> ScreenSpec v3
-> validate_game_ui_screen_spec.py
-> screen_spec_adapter.py / ESUIScreenSpecAdapter
-> ESUIGameScreenMaterializer
-> Prefab + Fixture Scene + profile/state evidence
```

## Semantic preservation

Every v3 component becomes a Unity object with `ESUIComponentSemantic`. The component
type, visual variant, asset slot ids, and numeric value remain available in the prefab
and in `editor.snapshot` / `ui.snapshot`. Do not infer these fields from pixels.

The materializer has dedicated visual recipes for:

- `item-slot` / `item-card`: surface, icon region, rarity, quantity, selected outline;
- `progress` / `bar`: background, normalized fill, optional value label;
- `counter` / `badge` / `status-badge` / `input-hint`: colored badge and readable label;
- `cooldown`: icon surface, radial-like overlay, remaining value;
- `tooltip`: title and description regions;
- `loading` / `error-state` / `empty-state`: explicit state message panel;
- `focus-ring`: keyboard/controller focus outline.

Unrecognized types still use the registered primitive mapping. Adding a new semantic
type requires a registry entry, validator fixture, adapter mapping, and materializer
recipe in the same change.

The adapter preserves the production contracts needed by the materializer. A declared
`qualityGates.typographyPolicy` is resolved to its TMP Font Asset and any distinct
`fallbackFontAssets` records, checked against file hashes and the union of required glyph sets,
and applied to all generated `TMP_Text` nodes. A
`responsivePolicy` configures the CanvasScaler; when `safeAreaPolicy` is
`profile-safe-area-inset`, each authored profile's safe-area rectangle owns the profile
root. A production-ready `assetPolicy` must resolve imported Sprites and verify the
declared source/hash/provenance fields. Missing planned procedural assets remain a
deterministic fallback and cannot be promoted to commercial art.

Asset resolution is performed before Unity materialization by
`scripts/resolve_ui_asset_manifest.py`. Its receipt is an input-side identity record, not a
visual acceptance result: it proves only that the selected project file, `.meta` GUID, dimensions
and declared hash are stable. Generated procedural assets remain `commercialAcceptance: deferred`
until an authorized source/license and GPU visual review are recorded.

Geometry resolution is performed before Unity materialization by
`scripts/resolve_ui_layout_plan.py`. It is the static owner of profile pixel rectangles, safe-area
containment, layout-group child placement and conflict diagnostics. When a `list`, `flow` or `grid`
declares `childGeometryOwner: parent-layout-group`, the receipt's child rectangles are authoritative
for static review and the authored child bounds are semantic hints only. This receipt does not prove
Unity `RectTransform` serialization, rebuild order, CanvasScaler runtime behavior or visual quality.

Token and typography gates run before materialization. `scripts/evaluate_ui_tokens.py` must produce
an explicit consumer trace and pass the configured contrast threshold; `accent` and `danger`
backgrounds require separate `onAccent` and `onDanger` foreground tokens. The materializer consumes
those foregrounds for button/badge labels instead of assuming the general `text` token is readable on
every surface. `scripts/evaluate_ui_typography.py` must pass the declared TMP Font Asset hash and
verified fallback metadata and every rendered fixture character across the fallback union, or
materialization remains blocked. A dynamic atlas mode without a fresh glyph/render receipt is not a
pass.

## Fixture states

The fixture driver applies `default`, `selected`, `disabled`, `empty`, `loading`,
`error`, and `long-content` without owning inventory, combat, economy, or navigation
facts. It only changes visual state and interaction affordance for evidence capture.

`stateSemantics.<state>.affectedComponentIds` is the execution set: the Materializer
must not broadcast a state to unrelated nodes. In a strict packet, every member of that
set declares the same `stateVariants.<state>`, and every non-`default` variant is a
member of that set. This bidirectional contract prevents a state from being present in
the JSON but absent from Fixture execution; `default` remains the baseline exception.
Each affected component also has exactly one `stateSemantics.<state>.effects` entry.
The Materializer executes its declared `visible`, `interactable`, `graphicAlpha`,
`graphicColor`, `wrapText`, `text`, and `outline` changes after compatibility
heuristics, so the explicit ScreenSpec contract has final capture precedence. Effects
outside the execution set are invalid, and fixture reset disables any outline created
only for an earlier captured state.
Every strict state has `geometryPolicy.preserveBounds: true`. `stateVariants` only
declare membership in Fixture execution; neither they nor `effects` may carry bounds,
anchors, pivots, layout modes, sizes, safe-area, parent or sibling-order changes. The
Materializer enforces this again after normalization, so a caller that skips the Python
Validator cannot move or shrink an action target through a state payload. Long content
must wrap, ellipsize or scroll inside the resolved base LayoutPlan.
Fixture strings do not use key-name or component-name heuristics. A declared
`fixtureTextBindings` record explicitly maps `fixtureDataKey` to one non-interactive textual
`componentId`, plus its `overflowPolicy`, positive `maxLines`, pixel content insets and action
clearance reservation. The Materializer validates the mapping even when Python validation was
skipped, then applies the bound string after state effects. `wrap` enables TMP word wrapping and
is allowed only when static layout capacity passes; `ellipsis` records intentional truncation.
`scroll` is rejected until a registered scroll-container recipe exists. A binding and an effect
cannot both own the same component's text.

## Advanced composition boundary

`designContract.advancedComposition` is required by the high-fidelity static gate. It maps the
single requested primary action across profiles, records whether the screen has a focal subject,
requires an intentional no-subject reason otherwise, and records key alignment, clearance and
responsive-equivalence constraints. A focal subject has a stable logical id and an
`focalAssetPolicies` record. That record must cover exactly its AssetManifest slots and agrees
with the manifest's `cropPolicy` and `focalPoint`; `focal-cover` also declares normalized safe
crop insets, a positive finite AssetManifest `sourceAspectRatio`, and
`atlasRotationPolicy: disallow-rotation`. The standalone Layout Resolver
projects the cover UV after final static profile geometry and blocks an impossible protected crop.
At Unity materialization, `ESUIFocalCropRawImage` recomputes the source ratio from the resolved
Sprite UV and rejects a rotated SpriteAtlas Sprite or a manifest mismatch above 1%.
`interactionDensity.groups` declares the controls, profile, maximum target count and
minimum gap to measure after list/flow/grid placement. The Python validator checks authored
geometry where child bounds own geometry; the resolver performs the density measurement from final
static rectangles and refuses to treat LayoutGroup child hints as layout truth. The Materializer
rejects malformed primary/focal/profile, crop-policy and density declarations before
serialization, but Unity LayoutGroup resolution, SpriteAtlas rotation/packing, TMP layout rebuild and rendered composition
remain runtime evidence.
`scripts/validate_ui_snapshot_evidence.py` is the post-materialization structural gate. It requires
every declared profile/state to have matching `editor.snapshot` and `ui.snapshot` identities for the
same spec hash, run and scene generation. It also rejects an absent or unsafe `focalCrop` record for
each declared focal asset. Each pair must declare the same non-empty `rootPath`, equal complete Canvas metadata
(`renderMode`, `scaler.uiScaleMode`, positive `scaler.referenceResolution`, `scaler.screenMatchMode`, and
finite `scaler.match` in `[0, 1]`),
the exact profile `viewport`, matching runtime `screenWidth/screenHeight`, and an equal set of unique
semantic paths below the declared root. For every shared path, both snapshots must declare boolean
`active` values that agree, matching `parentPath`, `siblingIndex`, `anchorMin`, `anchorMax` and `pivot`,
and editor `screenRect` must equal UI `screenX`, `screenY`, `screenWidth` and `screenHeight` within
0.01 pixels. Every element serializes matching `layoutGroup`/`contentSizeFitter` controller state;
the gate rejects a parent LayoutGroup and child ContentSizeFitter that actively control the same
width or height axis. Every runtime rectangle must remain inside the declared profile viewport. An active
`hasButton` node with an `interactionTarget` must meet that declared minimum width and height in its
resolved runtime rectangle. Every element also serializes matching `visibility` and
`inputReachability`: the active Mask/RectMask2D ancestor path, RectMask2D-visible intersection and
fraction, non-rectangular Mask uncertainty, CanvasGroup chain, and a conservative same-parent opaque
Graphic blocker. An active, interactable Button with an interaction target must retain that target's
minimum dimensions in the visible RectMask2D intersection, pass its CanvasGroup chain, and have no
same-parent raycast blocker. A decorative Graphic with `raycastTarget: false` is not a blocker.
An active non-rectangular Mask ancestor is intentionally `runtime-not-proven`, because snapshot
geometry cannot prove its stencil pixels or EventSystem hit result. Parent containment is not universal:
tooltips, overlays and intentional clipped effects may exceed their parent bounds when their own
viewport constraint still holds. Missing, non-finite or negative geometry blocks the pair. Its receipt
validates serialized JSON consistency only; it does not prove the Unity process,
it does not prove the Unity process,
persisted assets, final UGUI rebuild, PNG pixels or rendered visual quality. Axis ownership is a
serialized controller check, not proof of a stable final UGUI rebuild.
`scripts/validate_ui_gpu_evidence.py` then re-reads each PNG and requires it to match both snapshot
`capture` objects: file name, SHA-256, byte length, dimensions, alpha coverage, sampled color buckets,
edge transitions and RGBA extrema. Transparent, uniform and zero-edge frames are blocked. This is an
anti-corruption and non-blank pixel gate; it does not establish composition, target-style fidelity,
accessibility, runtime input or commercial visual acceptance. For every non-default state with declared
`visualChanges` or visual effects, the validator also compares its PNG with the same profile's `default`
PNG. An identical or below-threshold changed-pixel result blocks the evidence as
`state-pixel-undifferentiated`. The validator resolves each declared `affectedComponentIds` member from
the default editor snapshot's `path` and unique profile-local `screenRect`, preferring an active node but
retaining a unique default-hidden node for a `visible: true` state. It expands the region by four pixels
for outline/shadow bleed, then requires at least 80% of changed pixels to occur inside the affected-rectangle union. Missing,
ambiguous or invalid semantic rectangles block as `state-locality-*`; a delta concentrated elsewhere
blocks as `state-pixel-outside-affected-components`. This is coarse semantic-region correlation only: it
does not prove exact component rendering or approve the resulting design.
For the same profile/state, the UI snapshot also serializes target capability and result fields:
`hasButton`, direct `hasGraphic`, descendant `hasDescendantGraphic`, `hasText`, `active`, `interactable`,
direct `graphicAlpha`, descendant `descendantGraphicAlpha`, per-node `descendantGraphicAlphas`, RGBA
`graphicColor`, `outline`, `wrapText`, `text` and per-node `descendantTextStates`. The GPU evidence gate replays each executable state effect against these
fields and rejects missing/ambiguous targets, missing required capability or a mismatched actual value as
`state-effect-evidence-*` or `state-effect-snapshot-mismatch`. This proves the serialized result of the
declared effect, not that Unity input, layout rebuild or commercial visual review passed. `graphicAlpha` is
applied to all descendant Graphics and is checked against their common serialized alpha and every traced node;
`graphicColor` and `outline` only target the component's direct Graphic. Text and wrapping are serialized only
when all descendant TMP_Text targets agree, and every traced text node is checked independently. Every trace path
must be unique and rooted at the target component, so an ambiguous, selectively stale or cross-component value
injection fails closed.
The active state is recorded in the evidence snapshot, not painted as an undeclared
debug badge over a strict ScreenSpec capture. A legacy packet without state semantics
may retain the compatibility marker, but it cannot be used as high-fidelity evidence.

## Asset boundary

`assetSlots` are semantic ids validated against ScreenSpec `assets`. A project sprite or
AI-generated bitmap can be resolved by a future AssetManifest resolver. When no sprite
is available, the materializer uses the deterministic `Assets/UI/Generated/` white
fallback so layout and state evidence remain renderable. A fallback is not commercial
art and must be reported as placeholder evidence.

## Evidence levels

- `S2`: protocol/adapter/self-test, no Unity import;
- `S3`: Unity batch materialization, prefab/scene/snapshots;
- `S3-visual`: GPU-enabled batch capture with non-empty PNG pixels;
- `S4`: editor or PlayMode interaction evidence, required before claiming runtime input.

`-nographics` can prove structure but produces no valid visual baseline in this project.
Use a GPU-enabled batch invocation for PNG review and record the command/log beside the
evidence. Never promote a non-empty file whose pixel extrema are a single clear color.
