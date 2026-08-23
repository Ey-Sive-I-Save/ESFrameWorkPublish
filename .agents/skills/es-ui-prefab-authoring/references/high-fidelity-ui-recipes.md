# High-Fidelity Unity UI Recipes

> Authority: a visual-authoring aid built from public workflow sources and their Unity adaptation.
> Scope: composition, component grammar, material restraint, fixture coverage, and AI correction prompts.
> Not authority for runtime UI architecture, rendering package selection, or external asset licensing.

## Purpose

High fidelity is not decoration added until a screenshot looks busy. A production-facing UI is high
fidelity when its component grammar is deliberate, its hierarchy is inspectable, and its visual rules
survive every declared fixture and orientation. Use the recipes below to turn named art direction into
an auditable UGUI composition.

Use `generate_ui_authoring_packet.py <contract> <packet.md> --style <recipe>` to emit a bounded,
contract-specific AI handoff. It does not create assets or write Unity files.

## Recipe Selection

| Recipe | Strong fit | Avoid when |
|---|---|---|
| `operational-dark` | inventory, loadout, quest, social, settings, tactical overlays | the panel needs an illustrative object stage as its main signal |
| `premium-rpg` | character, item, loot, collection, reward, inspect panels | a dense operational workflow needs rapid repeated scanning |
| `luminous-sci-fi` | HUD, telemetry, cockpit, scanner, technical controls | the design has no clear data hierarchy and would become glow-heavy |
| `modern-mobile` | touch-first menus, collections, profile, commerce-like selection | desktop-only dense authoring tools require fixed multi-column scanning |

These are visual directions, not copied brand styles. Select one from the brief, state the reason, and
reuse existing project art direction whenever it conflicts with a generic recipe.

## Shared Construction Order

1. **Structure:** establish Canvas, safe area, root frame, content zones, anchors, sibling order, and
   scroll/mask boundaries.
2. **Reading order:** establish the focal region, section titles, primary action, repeated-row rhythm,
   and a limited number of type roles.
3. **State grammar:** express default, selected, disabled, loading, empty, error, long-content, and
   missing-art as component variants with fixed fixture data.
4. **Material:** apply named color, border, radius, shadow, artwork crop, and icon roles. Material must
   not conceal a structural problem.
5. **Micro-alignment:** verify icon optical centers, numeral baselines, separator alignment, 1px edges,
   truncation, and safe-area clearance at each profile.

## Stable UGUI Hierarchy

Use names that reveal visual ownership:

```text
Canvas
  PanelRoot
    Backdrop
    Frame
      Header
      ContentViewport
        Content
      ActionBar
```

Add a named `Overlay`, `TooltipAnchor`, `EmptyState`, or `LoadingState` only when its fixture requires
it. Do not create anonymous wrapper GameObjects for a few pixels of offset. A repeated card or row is a
component root with an explicit variant, not a copied subtree whose layout gradually drifts.

## Visual Quality Rules

- Use semantic tokens such as `surface.panel`, `text.primary`, `action.primary`, and `space.4`.
  Never make a near-duplicate token to solve one screen's local offset.
- Prefer value contrast, spacing, type hierarchy, and grouping before blur, glow, shadow, and decorative
  frames.
- Give every interactive icon a stable icon box and an interaction rectangle at least as large as the
  contract target. Icon pixels and hit geometry are separate requirements.
- Give artwork a crop contract: aspect mode, focal alignment, fallback art, and what remains visible on
  narrow profiles.
- Build responsive design through profile-specific anchors/layout rules, not a scaled landscape
  screenshot. Portrait receives its own composition evidence when policy is `both`.
- Treat motion as a visual cue with a static end state for screenshot evidence. Runtime animation,
  input, and lifecycle implementation remain outside this Skill until authorized.

## Advanced-Looking, Testable Details

| Detail | Stable implementation rule | Evidence |
|---|---|---|
| Layered panels | name backdrop/frame/inset/highlight roles; explain every raised layer | Editor hierarchy and profile PNG |
| Data-dense rows | fixed label/value/icon columns and repeated vertical rhythm | normalized bounds and long-content fixture |
| Object stage | fixed artwork crop plus info column, badge, and action hierarchy | default/missing-art/portrait screenshots |
| Premium material | restrained border/elevation roles, no arbitrary glow per child | token review and visual diff |
| Focus/selection | separate selected/focused semantic state from hover/enabled state | interaction trace plus selected fixture |
| Empty/loading/error | preserve page scaffold and replace only the affected content zone | fixture matrix and geometry audit |

## Public Sources and Current Verification

The following sources were network-checked on 2026-08-22. They describe methods, not prefab code to
copy into ESFramework:

- Playwright screenshot testing: https://playwright.dev/docs/test-snapshots
- Storybook interaction testing: https://storybook.js.org/docs/writing-tests/interaction-testing
- W3C Design Tokens Community Group: https://www.w3.org/community/design-tokens/
- Unity UGUI package documentation: https://docs.unity3d.com/Packages/com.unity.ugui@latest
- OneUIKit repository, MIT, publicly updated 2026-06-16: https://github.com/DevsDaddy/OneUIKit
- Unity-FlowUI repository, MIT, publicly updated 2026-08-03: https://github.com/nimritagames/Unity-FlowUI

Chromatic's documentation directory is the relevant entry point for multi-mode visual review:
https://www.chromatic.com/docs/. Do not hard-code a historical page URL into execution flow.

## Non-Transferable Lessons

- Storybook stories map to deterministic fixture states, not React components or web DOM code.
- Playwright baselines map to immutable Unity screenshot baselines, not arbitrary image generation.
- Component library assets may carry licenses and dependencies. Inspect and authorize each source asset
  before use; do not import a runtime manager merely to obtain a visual style.
- A polished screenshot is still not runtime, Player, performance, accessibility, or resource evidence.
