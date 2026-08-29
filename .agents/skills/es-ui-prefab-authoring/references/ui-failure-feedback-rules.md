# UI Failure Feedback Rules

These rules are mandatory feedback from the v18/v19 game-lobby evidence batches. They are
reusable authoring constraints, not a visual acceptance claim.

## UI-FB-001: authored art must remain identifiable

- Trigger: hero, portrait, map, or other declared art is darkened, flattened, mirrored, cropped
  beyond recognition, or visually indistinguishable from a procedural surface.
- Root cause: applying a token color or placeholder treatment to authored art; stale generated
  assets; or no explicit focal-subject brief.
- Block: do not accept the ScreenSpec or continue to another state.
- Required next input: declare `visualVariant: none` for authored art, record a stable focal
  subject, intended orientation, a `focalAssetPolicies` crop/focal-point/safe-crop record that
  matches AssetManifest, contrast target, and increment the generated-art build ID.
- Evidence: fresh GPU captures for wide and narrow default states plus a source/spec hash match.

## UI-FB-002: one primary action and a visible hierarchy

- Trigger: all controls have equal emphasis, the main action is not immediately visible, or the
  result reads as a generic panel collage.
- Root cause: choosing component types before deciding screen family, user intent, zones and
  action hierarchy.
- Block: do not claim commercial visual readiness.
- Required next input: record one primary action, secondary actions, hero/content/support zones,
  hierarchy order, interaction-density groups after LayoutGroup resolution, and the design reason
  for each surface/token.
- Evidence: the default capture must be reviewed at native resolution; selected/disabled states
  must preserve the same hierarchy rather than recolor every control.

## UI-FB-003: wide and narrow profiles are separate layout decisions

- Trigger: wide layouts leave large dead regions, narrow layouts squeeze two columns, text is
  clipped, or controls overlap safe-area boundaries.
- Root cause: scaling one normalized composition instead of authoring profile-specific constraints.
- Block: any missing or clipped profile is a failed batch, even when the wide screenshot passes.
- Required next input: declare profile-specific zones, min sizes, reflow/stack rules, safe-area
  policy and long-content behavior before materialization.
- Evidence: wide and narrow default, selected, disabled, loading, error and long-content captures.

## UI-FB-004: evidence status follows the weakest layer

- Trigger: `Completed`, a non-empty PNG, or a static validator pass is reported as visual acceptance.
- Root cause: mixing ScreenSpec/static, Unity materialization, GPU evidence and runtime claims.
- Block: missing Unity or GPU evidence forces `runtime-not-run` or `visualAcceptance: not-claimed`.
- Required next input: report Static, Materialization, GPU Visual and Runtime layers separately;
  bind every capture to spec hash, profile, state and build ID.

## UI-FB-005: every failure must change the next artifact

- Trigger: a rerun changes only colors, filenames or cache keys while the diagnosed composition,
  asset or responsive failure remains.
- Root cause: recording a review without converting it into a ScreenSpec, registry, validator or
  materializer change.
- Block: stop the iteration loop and return a `feedback-not-incorporated` finding.
- Required next input: name the changed rule/field, expected visual effect, validator/evidence
  that will detect it, and the old evidence batch that caused the change.

## UI-FB-006: request intent must survive classification

- Trigger: the requested screen family, primary user intent or named visual target is replaced
  by a generic screen, or a reference-driven request is authored with no reference source.
- Root cause: the AI validates internal geometry and tokens without preserving the user's task
  contract; `screenType` is treated as a free design choice instead of a classified requirement.
- Block: reject the ScreenSpec before layout, materialization or visual acceptance.
- Required next input: declare `intentContract.requestedScreenFamily`, `requestedPrimaryIntent`,
  `visualTarget`, `fidelityMode`, `referencePolicy`, `referenceSources` and `productBoundary`.
- Evidence: classifier decision, reference/source receipt when required, primary interaction trace,
  and a validator result proving the intent contract matches the ScreenSpec.

## UI-FB-007: matching rectangles are not enough to prove one layout

- Trigger: editor and UI snapshots share a screen rectangle but disagree on parent hierarchy,
  sibling order, anchors, pivot, viewport containment, or an active button's declared target size.
- Root cause: geometry is compared without proving the RectTransform structure that owns it, or a
  visual target is treated as an input target.
- Block: do not pass the snapshot pair or let it enter GPU evidence validation.
- Required next input: serialize and compare `parentPath`, `siblingIndex`, `anchorMin`, `anchorMax`
  and `pivot`; resolve every non-root parent path within the semantic path set and require sibling
  indices to be unique under each parent; keep every runtime screen rect inside its profile viewport; for every active Button
  with `interactionTarget`, compare the resolved runtime dimensions to the declared minimum.
- Evidence: paired editor/UI snapshots through `validate_ui_snapshot_evidence.py`, including
  negative fixtures for hierarchy, anchor/pivot, viewport and interaction-target drift.

## UI-FB-008: each UGUI size axis has one active owner

- Trigger: a parent `LayoutGroup` controls a child's width or height while that child has an
  active `ContentSizeFitter` controlling the same axis, or a disabled controller claims ownership.
- Root cause: treating matching final rectangles as proof of a stable layout, while two UGUI
  controllers can race during rebuild and produce resolution- or frame-dependent geometry.
- Block: do not accept the snapshot pair or claim layout stability for the affected profile/state.
- Required next input: split responsibility across hierarchy levels, disable one controller on the
  contested axis, or use a declared fixed/parent-owned axis. Record the expected owner in the
  LayoutPlan rather than relying on a post-rebuild rectangle.
- Evidence: paired snapshots serialize matching `layoutGroup`, `contentSizeFitter` and effective
  child/self axis-control fields; `validate_ui_snapshot_evidence.py` must pass negative fixtures
  for both cross-channel drift and parent-layout-group/self-content-size-fitter axis conflict.

## UI-FB-009: an active action must remain visible and reachable

- Trigger: an active interactive Button has a legal RectTransform and target size but is fully or
  partly clipped below its target dimensions, hidden by a transparent CanvasGroup, blocked by an
  ancestor CanvasGroup, or covered by a same-parent opaque raycast-enabled Graphic.
- Root cause: treating authored bounds as player reachability while omitting clipping ancestry,
  CanvasGroup filtering and draw-order overlays from snapshot evidence.
- Block: do not accept the snapshot pair or let it enter GPU evidence validation for that
  profile/state. A non-rectangular `Mask` ancestor over an active target remains unproven until
  runtime raycast evidence exists.
- Required next input: keep decorative overlays `raycastTarget: false`; make loading/disabled
  blockers disable the covered Button; avoid covering primary actions; declare the target's
  required visible dimensions and preserve its profile/state geometry.
- Evidence: paired editor/UI snapshots serialize matching `visibility.clipAncestors`, RectMask2D
  visible intersection/fraction, CanvasGroup chain and conservative same-parent blocker. Negative
  fixtures cover fully/partly clipped targets, raycast blockers, CanvasGroup blocking and
  cross-channel reachability drift.

## UI-FB-010: content density and zone coverage must be explicit

- Trigger: a screen passes because it has a few valid components, while a required header,
  content or navigation zone is empty, text is absent, or the composition is visually skeletal.
- Root cause: treating component existence as proof of a complete commercial screen and leaving
  profile-specific content expectations in prose.
- Block: do not accept the ScreenSpec for authoring or visual review when a declared profile is
  below its content contract.
- Required next input: declare `contentRequirements.profiles` with minimum component, text and
  interactive counts, required component types and per-zone minimums; bind the rule to those
  fields and record the prior evidence batch that exposed the omission.
- Evidence: deterministic profile counts and zone coverage from the normalized component tree,
  plus a visual review that confirms the required regions are not placeholder-only.

## UI-FB-011: declared visual tokens must have executable consumers

- Trigger: a palette, state color or spacing scale is declared but no component consumes it, or
  selected/error states only name a token without an executable visual effect.
- Root cause: writing design-token vocabulary without connecting it to components and state
  semantics, allowing decorative or one-off values to drift between profiles.
- Block: do not accept the visual design contract while any required token, state-token binding
  or spacing value is unconsumed or unverifiable.
- Required next input: declare `requiredTokenConsumers`, `stateTokenBindings` and
  `spacingScalePx`; bind each state signal to affected components and concrete effects that
  resolve to the declared token.
- Evidence: validator output proving consumer counts, state effect resolution and spacing-scale
  membership; Unity/GPU evidence remains a separate, later layer.

## UI-FB-012: visual hierarchy must be complete and ranked

- Trigger: components are valid individually but the screen reads as a flat collage, or the
  primary action is absent from the strongest declared visual band.
- Root cause: hierarchy exists only in prose, token names or sibling order and has no component
  membership contract.
- Block: do not accept a high-fidelity ScreenSpec with unassigned required components, duplicate
  band membership, non-increasing emphasis, or a primary action outside the action band.
- Required next input: declare `visualHierarchy` bands, ranks, emphasis, coverage policy and the
  primary-action band. Use complete non-background coverage or an explicit key-component set.
- Evidence: deterministic band membership and primary-action trace; GPU review remains separate.

## UI-FB-013: focus and unavailable-action behavior must be explicit

- Trigger: keyboard/gamepad order is inferred from hierarchy, focus starts on a secondary action,
  or loading/disabled primary actions still accept repeated activation.
- Root cause: pointer layout is mistaken for an input-navigation graph and state prose is not
  bound to executable interaction effects.
- Block: do not accept the interaction contract until every profile has a complete focus order
  and the primary action sets `interactable: false` in disabled and loading states.
- Required next input: declare input modes, explicit focus order, default focus intent and
  disabled/loading intent policies; bind both state effects to profile-specific primary actions.
- Evidence: static focus-order trace and state-effect resolution, followed by Unity input evidence.

## UI-FB-014: state changes must have a bounded blast radius

- Trigger: selected, loading, error or another local state recolors, hides or mutates most of a
  profile even though only a small semantic region owns that state.
- Root cause: no profile-relative limit connects `affectedComponentIds` to the screen composition.
- Block: reject any state whose active affected-component ratio exceeds its declared budget.
- Required next input: declare `stateImpactPolicy.maxAffectedComponentRatio` for every state and
  reduce the affected set or explicitly redesign the state as a screen-wide mode.
- Evidence: per-profile active-component and affected-component counts.

## UI-FB-015: key RectTransform anchors must be numeric and materializer-aligned

- Trigger: anchor words such as top, center or stretch exist, but final `anchorMin`, `anchorMax`
  and `pivot` are absent, contradictory or differ from the Materializer projection.
- Root cause: mixing screenshot top-left bounds with Unity bottom-left RectTransform coordinates,
  or treating a parent-layout-owned child as authored anchor truth.
- Block: do not accept a high-fidelity LayoutPlan with missing, out-of-range or mismatched key
  anchor records. LayoutGroup-managed children cannot claim deterministic authored anchors.
- Required next input: declare `anchorContract` in `unity-rect-transform-bottom-left`, map key
  components per profile and derive anchor Y from top-left bounds while preserving the authored
  Unity pivot.
- Evidence: Validator projection checks followed by paired Unity RectTransform snapshots.
