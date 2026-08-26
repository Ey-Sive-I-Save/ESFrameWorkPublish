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
  and `pivot`; keep every runtime screen rect inside its profile viewport; for every active Button
  with `interactionTarget`, compare the resolved runtime dimensions to the declared minimum.
- Evidence: paired editor/UI snapshots through `validate_ui_snapshot_evidence.py`, including
  negative fixtures for hierarchy, anchor/pivot, viewport and interaction-target drift.
