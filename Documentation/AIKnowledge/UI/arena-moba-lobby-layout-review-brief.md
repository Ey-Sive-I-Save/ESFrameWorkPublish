# Arena MOBA Lobby Static Authoring Review

`DocumentId`: `arena-moba-lobby-layout-review.static.v10`
`DocumentStatus`: `static-partial / runtime-not-run`
`GeneratedFromSpec`: `Assets/UI/Contracts/arena-moba-lobby-layout-review.screen-spec.v3.json`
`GeneratedFromSpecSha256`: `70e3789c8597b0b9db713c8a19c356e2d4e3ea0b5a59506d1b1aa0ed7f34ccc1`
`StaticReplayPlanHash`: `f500e2d71848e9882fa332b87214ac4ca4b16b2ea83b23f8d59740e543da3124`
`StaticReplayStatus`: `passed`
`StaticReplayEvidenceLevel`: `S1`
`StaticReplayGeneratedUtc`: `2026-08-26T00:29:28.1776232Z`
`EvidencePolicy`: static receipts do not prove Unity materialization, GPU rendering, input behavior or commercial acceptance.

This is a deterministic static hand-off for review by another AI. It describes the current
ScreenSpec and the results of the specialized static evaluators. It is not a generated Prefab,
Fixture Scene or screenshot package.

## Capability Covered

The packet exercises the six-layer UI authoring contract:

1. `ScreenSpec`: navigation screen, semantic component tree, two responsive profiles and six
   deterministic states.
2. `AssetManifest`: ten declared visual slots, path/fallback/provenance decisions and resolver
   evidence.
3. `LayoutPlan`: safe-area rectangles, profile-specific reflow, layout-group ownership, minimum
   sizes and sibling-overlap checks.
4. `BehaviorSpec`: visual intents for selection, disabled, loading, error and long-content states;
   business links remain reserved IDs only.
5. `Fixture Driver`: repeatable `wide`/`narrow` and six-state fixture matrix.
6. `Materializer`: the declared Unity entry point is available, but it has not been invoked in
   this static run.
7. `Reference Measurement`: the deterministic image ingestor is available for source-image hash,
   pixel bounds and candidate anchor hints; it does not recover semantic hierarchy or responsive
   behavior and every candidate remains review-required.

## Screen Contract

| Field | Static value |
|---|---|
| Screen family | `navigation` |
| Recursive components | 61 |
| Profiles | `wide` 1920x1080; `narrow` 1080x1920 |
| States | `default`, `selected`, `disabled`, `loading`, `error`, `long-content` |
| Main intent | `start-ranked-match` |
| Intent availability | 12 wide; 10 narrow |
| Feedback bindings | `UI-FB-001` through `UI-FB-005` |
| Production-ready | false |
| Commercial acceptance | deferred |

The narrow profile is a deliberate reflow, not a uniform scale of the wide profile. Mail is moved
to a notification drawer and invite is deferred to party detail; this is recorded as responsive
availability, not treated as an accidental missing control.

## Static Evidence

| Gate | Result | Receipt | Meaning |
|---|---|---|---|
| ScreenSpec schema, registry, feedback and quality gates | PASS | validator JSON output | IDs, states, bindings and required fields are valid |
| Layout resolver | PASS | `ES/UIEvidence/arena-moba-lobby/layout-review-plan.receipt.json` | safe-area geometry, minimum sizes and overlap checks pass for both profiles |
| Asset manifest resolver | PASS | `ES/UIEvidence/arena-moba-lobby/layout-review-asset-manifest.receipt.json` | 10/10 candidates resolve with path, hash, GUID and dimensions; commercial acceptance remains deferred |
| Token and contrast evaluator | PASS | `ES/UIEvidence/arena-moba-lobby/token-evaluation.receipt.json` | role mapping, consumer trace and WCAG checks pass |
| Typography/glyph evaluator | BLOCKED | `ES/UIEvidence/arena-moba-lobby/typography-evaluation.receipt.json` | fixture glyph coverage and fallback chain are incomplete |
| Platform self-test | PASS | command output | inventory, combat HUD, dialogue and main-menu registry slices pass |
| Reference measurement executor | PASS (candidate-only) | `scripts/ingest_ui_reference.py` + self-test | source identity and bounded candidate geometry are available; semantic/parent/interaction review remains required |
| Skill StaticDeepReplay | PASS | `ES/Output/StaticReplay/es-ui-prefab-authoring.json` | seven deterministic static cases and five specialized acceptance checks pass; runtime escalation remains required |

### Layout result

The resolver reports zero issues for `wide` and `narrow`. Layout-group children are resolved once
in pixel space; they are not scaled a second time from normalized bounds. This prevents the former
false compression of mode cards and navigation tabs.

Representative resolver rectangles (not Unity `RectTransform` values):

| Profile | Component | Size |
|---|---|---:|
| wide | `wide-tab-home` | 336.922 x 184.880 px |
| narrow | `narrow-tab-home` | 175.622 x 261.600 px |

`wide-mode-list`, `wide-navigation` and `narrow-navigation` use `parent-layout-group` ownership;
their children do not independently control final Unity layout-group geometry.

### Token result

The packet explicitly declares `surfaceRaised`, `onAccent` and `onDanger`, and the materializer
contract consumes those semantic roles. Contrast checks pass the configured 4.5:1 minimum:

- `onAccent` on `accent`: `10.5303`
- `onDanger` on `danger`: `6.2154`
- background/text, surface/text and muted-text checks also pass.

This proves static token resolution only; it does not prove Unity color assignment or GPU output.

### Typography blocker

The primary TMP asset is hash-bound:

`Assets/UI/Fonts/ESBrandSansSC SDF.asset`

`faa6412e55b384fb05d6e33097053cb5ce5f0ceb6a945db2f6191db04cdf1e90`

The serialized asset contains 261 Unicode entries and the evaluator inspects 47 fixture strings.
Several visible Chinese fixture characters are absent, including characters from `快速`, the
season/event labels, the error message and long-content names. `fallbackFontAssetIds` currently
points only to `es-brand-sans-sc`, so there is no distinct fallback that can satisfy the missing
glyphs. Typography remains BLOCKED until a real static font asset or distinct licensed fallback is
generated and re-evaluated. Dynamic TMP atlas population is not accepted as static proof.

### Asset result

All ten declared slots now resolve to project-local generated Texture2D/Sprite assets. Each record
has a declared path and SHA-256 matching the actual file, a Unity `.meta` GUID, dimensions and an
explicit fallback. The assets are usable prototype art, not approved commercial art: every record
remains `commercialAcceptance: deferred` because the source class is `generated-procedural`.

The Materializer now consumes a declared path first for every image source. Procedural generation by
semantic ID is retained only as a fallback when the declared asset cannot be loaded. This closes the
previous `season-hero`/`hero-banner` key mismatch that caused a real candidate to degrade to a solid
placeholder.

## State Review Contract

Each state has fixture data, affected component IDs, visual changes, interaction changes and a
preserve-bounds policy. The static packet therefore tests state semantics rather than merely
checking that state names exist:

- `default`: ranked mode selected; all declared actions enabled.
- `selected`: quick mode selected; selection does not start matchmaking.
- `disabled`: party-not-ready disables declared intents while retaining labels and bounds.
- `loading`: primary action shows `正在匹配...`; navigation remains available.
- `error`: retry copy and error message replace the seasonal status line.
- `long-content`: long Chinese, English and numeric fixtures wrap or truncate without changing
  input geometry.

## Evidence Boundary and Verdict

```text
ScreenSpec validator: PASS
Layout plan: PASS (wide, narrow)
Asset manifest: PASS (10/10 identity-verified; commercial acceptance deferred)
Token/contrast: PASS
Typography/glyphs: BLOCKED (missing fixture glyphs; no distinct fallback)
Platform self-test: PASS (4 registry slices)
Reference measurement: PASS (candidate-only; no semantic/layout truth)
StaticDeepReplay: PASS (S1; plan hash bound to current Skill static replay inputs)
Unity compilation: BLOCKED (existing editor-assembly errors outside the UI materializer)
Unity materialization: NOT RUN
Prefab/Fixture Scene: NOT PROVEN
GPU screenshots: NOT PROVEN
TMP glyph rendering: NOT PROVEN
PlayMode/input focus: NOT PROVEN
Commercial asset acceptance: DEFERRED
```

The weakest-layer verdict for this packet is `static-partial`. Existing PNGs, JSON presence,
non-empty files or a successful headless process must not be promoted to visual acceptance.

## Reproduction Commands

```powershell
$env:PYTHONDONTWRITEBYTECODE='1'
$env:PYTHONUTF8='1'

# Optional when a source screenshot is supplied; this produces candidate geometry only.
python .agents/skills/es-ui-prefab-authoring/scripts/ingest_ui_reference.py `
  <reference-image.png> `
  --out ES/UIEvidence/arena-moba-lobby/reference-ingest.receipt.json

python .agents/skills/es-ui-prefab-authoring/scripts/validate_game_ui_screen_spec.py `
  Assets/UI/Contracts/arena-moba-lobby-layout-review.screen-spec.v3.json `
  --require-feedback --require-quality-gates --json

python .agents/skills/es-ui-prefab-authoring/scripts/resolve_ui_layout_plan.py `
  Assets/UI/Contracts/arena-moba-lobby-layout-review.screen-spec.v3.json `
  --out ES/UIEvidence/arena-moba-lobby/layout-review-plan.receipt.json

python .agents/skills/es-ui-prefab-authoring/scripts/resolve_ui_asset_manifest.py `
  Assets/UI/Contracts/arena-moba-lobby-layout-review.screen-spec.v3.json `
  --project-root F:/aaProject/ESFrameWorkPublish `
  --out ES/UIEvidence/arena-moba-lobby/layout-review-asset-manifest.receipt.json

python .agents/skills/es-ui-prefab-authoring/scripts/evaluate_ui_tokens.py `
  Assets/UI/Contracts/arena-moba-lobby-layout-review.screen-spec.v3.json `
  --out ES/UIEvidence/arena-moba-lobby/token-evaluation.receipt.json

python .agents/skills/es-ui-prefab-authoring/scripts/evaluate_ui_typography.py `
  Assets/UI/Contracts/arena-moba-lobby-layout-review.screen-spec.v3.json `
  --project-root F:/aaProject/ESFrameWorkPublish `
  --out ES/UIEvidence/arena-moba-lobby/typography-evaluation.receipt.json

python .agents/skills/es-ui-prefab-authoring/scripts/self_test_game_ui_platform.py

powershell -NoProfile -File .agents/skills/es-ui-prefab-authoring/scripts/Test-es-ui-prefab-authoring-StaticReplay.ps1 `
  -ProjectRoot F:/aaProject/ESFrameWorkPublish
```

The Python commands are static checks. The Unity compile attempt reached the project compiler but
was blocked by existing errors in `ES_Editor.csproj` (for example `ESTrackViewWindow.cs`,
`ESAssetPackageBakeWindow.cs`, `ESCommandPaletteWindow.cs` and `ESPolymorphicReferenceDrawer.cs`).
Therefore no fresh Prefab/Fixture Scene or GPU evidence is attached to this revision. Unity
materialization and runtime input require a clean editor compilation and a fresh run tied to this
exact ScreenSpec hash.
