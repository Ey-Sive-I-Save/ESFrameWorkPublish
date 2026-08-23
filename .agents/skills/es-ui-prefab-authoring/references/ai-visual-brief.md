# AI Visual Brief for ES UI Panels

Use this brief before asking an AI to create or refine a high-fidelity Prefab. It is deliberately
visual and fixture-oriented; it does not describe runtime windows, presenters, or business state.

## Brief

```text
Panel identity:
  panelId:
  user goal:
  target Prefab:
  fixture Scene:

Profiles:
  - id / width / height / orientation / safe-area insets / CanvasScaler reference:

Visual grammar:
  tone:
  color roles:
  typography roles and maximum lines:
  spacing scale:
  corner/border/shadow roles:
  image/icon crop rules:

Composition:
  root bounds:
  header/content/action zones:
  normalized bounds per profile:
  max hierarchy depth:
  required sibling/layer order:

Components and variants:
  component path:
  visual variant:
  selected/disabled/loading behavior:
  interaction minimum:
  raycast policy:

Fixtures:
  default:
  loading:
  empty:
  error:
  disabled:
  selected:
  long-content/localized:
  missing-art:

Evidence and limits:
  baseline policy:
  pixel threshold:
  max iterations:
  allowed changed paths:
  stop condition:
```

## AI handoff rules

- Read the project UI workflow, AIWarnings, contract, and existing visual conventions before editing.
- Reuse token roles and existing components; introduce a new token only with a reason and a stable name.
- Produce or update the contract before creating a screenshot. Every state and supported profile must be explicit.
- Correct one responsible cause at a time. Anchor problems are fixed at the parent/anchor level, not by
  scattering child offsets.
- Compare the same profile/state against the immutable baseline after each revision. Do not replace the
  baseline to make a mismatch disappear.
- Report placeholders, missing fonts/icons, unsupported safe-area data, and ambiguous visual causes as
  blockers. Do not silently substitute a portrait layout, a fallback font, or a runtime system.

## Acceptance language

Use factual outcomes:

- `PASS`: contract, Editor geometry, runtime geometry, artifact identity, and visual batch all pass.
- `REVISE`: a bounded finding identifies a responsible visual path and the iteration budget remains.
- `BLOCKED`: Unity/adapter evidence, required asset, safe-area/text data, or cause attribution is missing.

“Looks good” and a single screenshot are not acceptance evidence.
