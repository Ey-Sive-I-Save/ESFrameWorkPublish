# Commercial UI Patterns for Prefab Authoring

> Authority: external reference distilled for ES UI authoring; project AIWarnings and source outrank it.
> Scope: commercial/open-source visual workflow patterns and their Unity adaptation.
> Stale when: project UI boundaries, renderer choices, or linked sources materially change.
> Evidence: source links and the current project workflow document; not runtime acceptance.

This reference is a design and production guide, not a runtime system contract. Sources were accessed on 2026-08-21 and are references only; current project source and AIWarnings remain authoritative. Re-read the linked sources when their details matter.

## Network verification notes (2026-08-21)

- Playwright's visual comparison guidance uses `expect(...).toHaveScreenshot()` to create a
  reference on the first run and compare later runs; it also warns that screenshot baselines need
  a stable environment. The Unity equivalent is an immutable baseline per contract/profile/state,
  plus recorded Unity version, Git head, viewport, and fixture generation.
- Chromatic's current testing documentation groups visual tests with interaction, accessibility, and
  viewport modes. The Unity equivalent is to keep pixel diff, UI snapshot geometry, Editor
  RectTransform export, and any interaction evidence as separate required layers instead of
  treating one screenshot as a full acceptance result.
- The W3C Design Tokens Community Group publishes a vendor-neutral, interoperability-oriented token
  format. The Skill therefore treats token roles as stable semantic names and keeps the Unity
  mapping explicit; it does not import a web token file as an unreviewed runtime dependency.
- Unity's public UI-system page redirects to versioned `com.unity.ugui` package documentation. The
  project contract therefore treats the installed Unity package and current project source as the
  authority for Canvas/CanvasScaler behavior, while web workflow sources remain advisory.

## Patterns worth adopting

### Tokens before screens

Commercial design systems centralize color roles, type scale, spacing, radii, elevation, and component states. The practical benefit is not visual fashion; it gives an AI agent named choices to reuse instead of inventing near-duplicates. In Unity, keep these decisions in a small, explicit presentation layer or documented prefab conventions. Do not scatter magic colors and offsets through scene YAML.

### Component plus variants

Treat Button, Tab, Card, Badge, ListRow, Header, and ModalFrame as reusable visual components. Model `Default`, `Selected`, `Disabled`, `Loading`, `Error`, and `Empty` as variants or fixtures. Prefer composition and shallow, inspectable hierarchies over a deep inheritance tree.

### State matrix as the design source

A screenshot describes one state. A state matrix describes the panel's visual surface. Keep fixture data for long text, zero results, errors, loading, disabled actions, and missing art. This makes AI output testable and prevents the polished default state from hiding broken edge states.

### Context-rich design-to-code

Figma's MCP guidance emphasizes that screenshots alone are insufficient. Components, variables, styles, code paths, and interaction notes give an AI agent the design intent and the existing vocabulary. For Unity, the equivalents are prefab paths, component names, token names, target resolutions, and fixture states.

### Preview, inspect, refine

Figma Make and v0 both use a short loop: describe or import a frame, generate an editable prototype, preview it, inspect the result, and iterate. For Unity, the preview is a dedicated scene fixture and a fixed set of screenshots. Treat generated output as a draft until it matches the visual contract and project conventions.

### Stories and visual regression

Storybook treats each rendered component state as a story and reuses those states for interaction, accessibility, visual, and CI tests. Unity does not need Storybook itself for this pattern: use a fixture scene, deterministic mock data, named state toggles, and screenshot records. Add PlayMode only when behavior is in scope.

### Design tokens as an interchange layer

The W3C Design Tokens Community Group describes tokens as a cross-tool source for colors, typography, spacing, and related decisions. Do not force a Web token file into Unity without a consumer. First establish stable roles and a mapping that can later feed Unity materials, fonts, sprites, or prefab conventions.

## Open-source Unity patterns

### Unity Dragon Crashers UI Toolkit sample

The sample is useful as a scene-composition reference: a complete game-facing UI can be studied as a set of screens and reusable visual pieces rather than as one giant prefab. Borrow the idea of a dedicated sample scene and inspectable screen composition. Do not assume UI Toolkit is the correct renderer for an existing UGUI project.

Source: https://github.com/eungyukm/UnityDragonCrashers

### OneUIKit

OneUIKit packages ready-to-use components, icons, effects, example screens, navigation/binding examples, and mobile-oriented presentation. The reusable lesson for this Skill is to ship a visual component catalog and a demo scene so a creator can copy a known-good composition. Its runtime framework and event binding are outside this Skill's scope.

Source: https://github.com/DevsDaddy/OneUIKit

### Unity-FlowUI

FlowUI focuses on fluent builders plus editor search/filter, hierarchy inspection, naming assistance, and missing-reference detection. For the current prefab-only phase, borrow the authoring ergonomics: predictable paths, searchable hierarchy, stable names, and early broken-reference checks. Do not import its centralized `UIManager` premise into ESFramework.

Source: https://github.com/nimritagames/Unity-FlowUI

## Sources

- Figma Make design-to-code: https://www.figma.com/solutions/design-to-code/
- Figma Dev Mode MCP context: https://www.figma.com/blog/introducing-figma-mcp-server/
- v0 overview and prompt-to-prototype workflow: https://v0.dev/docs
- Storybook stories: https://storybook.js.org/docs/writing-stories
- Storybook UI testing: https://storybook.js.org/docs/writing-tests
- Chromatic visual, interaction, accessibility, and viewport testing: https://www.chromatic.com/docs/
- W3C Design Tokens Community Group: https://www.w3.org/community/design-tokens/
- Unity Dragon Crashers UI Toolkit sample: https://github.com/eungyukm/UnityDragonCrashers
- OneUIKit: https://github.com/DevsDaddy/OneUIKit
- Unity-FlowUI: https://github.com/nimritagames/Unity-FlowUI

## Do not copy blindly

- Web-oriented generated React/HTML/CSS is a visual reference, not Unity code.
- A screenshot is not a component contract and cannot prove behavior.
- A design-token format is not automatically a Unity runtime dependency.
- Automated visual comparison cannot prove ES resource ownership, input authority, or gameplay correctness.

## Production pipeline synthesis

The useful common denominator across Figma/Dev Mode, Storybook, Chromatic, Playwright, and Unity
sample projects is a chain of explicit identities rather than a single clever prompt:

```text
brief + tokens
  -> component variants
  -> story/state fixtures
  -> viewport/profile matrix
  -> deterministic render
  -> structural + interaction audit
  -> visual diff review
  -> bounded change with an immutable baseline
```

Apply these invariants to every ES panel:

1. **Intent is named.** Every visual decision has a role (`surface.primary`, `text.muted`,
   `space.3`, `action.primary`) or a component variant. Avoid one-off values that an AI cannot
   recognize or safely reuse.
2. **A state is a fixture, not a screenshot annotation.** Loading, empty, error, disabled,
   selected, long-content, and missing-art states each have stable fixture data and a matrix entry.
3. **A viewport is a test input.** Width, height, orientation, CanvasScaler reference, safe area,
   and crop rules are recorded per profile. Portrait is never inferred from a landscape image.
4. **Structure is tested before pixels.** Anchors, pivots, sibling order, required hierarchy paths,
   target dimensions, raycast behavior, and safe-area bounds are audited before visual comparison.
5. **Diff review is bounded.** A diff ratio is a signal, not an automatic approval. Keep one focused
   correction per iteration, inspect the changed region, preserve the last passing baseline, and
   stop when the cause is ambiguous.
6. **Evidence has one identity.** Contract hash, panel/profile/state `captureKey`, N-format RunId,
   capture attempt, Unity version, Git head, artifact hashes, and baseline hash travel together. A stale report is worse than a failed
   report because it can make an AI revise the wrong asset.

### AI visual quality heuristics

Use the following order when asking an AI to refine a panel:

1. **Composition:** frame bounds, safe area, major zones, reading order, and responsive profile.
2. **Hierarchy:** title/body/action emphasis, repeated component grammar, and layer/sibling order.
3. **Geometry:** anchors, pivot, min target size, list rhythm, text container width, and clipping.
4. **State parity:** selected/disabled/loading/empty/error/long-content must retain the same grammar.
5. **Material:** color roles, typography, image crop, border, radius, shadow, and opacity.
6. **Micro-details:** icon optical alignment, 1px edges, baseline alignment, and localized copy.

Do not ask the AI to “make it prettier” without naming the affected profile/state, allowed Prefab or
Scene paths, evidence it must reread, and the stop condition. The skill's iteration planner is the
machine-readable version of that discipline.
