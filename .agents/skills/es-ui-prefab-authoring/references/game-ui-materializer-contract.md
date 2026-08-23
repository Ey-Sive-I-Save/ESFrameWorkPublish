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

## Fixture states

The fixture driver applies `default`, `selected`, `disabled`, `empty`, `loading`,
`error`, and `long-content` without owning inventory, combat, economy, or navigation
facts. It only changes visual state and interaction affordance for evidence capture.

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
