# Editor availability matrix

The validator resolves `TargetKind` before loading evidence. Use `-TargetKind` when inference is ambiguous; `Auto` records that the kind was inferred.

| TargetKind | Critical static gates |
|---|---|
| EditorWindow | framework-integration, visual |
| Workbench | framework-integration, visual |
| AdvancedDropdown | advanced-dropdown-contract |
| TransientPopup | transient-popup-contract |
| PreviewWindow | preview-window-contract |
| InspectorDrawer | inspector-drawer-contract, visual |
| ShaderGUI | shader-gui-contract, visual |
| MenuAction | none |
| PreviewImport | none |
| BackgroundService | none |

Every EditorWindow/Workbench result also includes the 20-rule registry (`EW-01` through `EW-20`). A `not-applicable` rule is scoped out, not failed. Runtime evidence remains a separate claim set.

| Target | Required rows before `Ready` |
|---|---|
| EditorWindow/workbench | framework-integration, compile, reloadDomain, interaction, visual, recovery, performance |
| AdvancedDropdown | advanced-dropdown-contract, compile, reloadDomain, interaction, visual, recovery, performance |
| TransientPopup | transient-popup-contract, compile, reloadDomain, interaction, visual, recovery, performance |
| PreviewWindow | preview-window-contract, compile, reloadDomain, interaction, visual, recovery, performance |
| Inspector/drawer/property UI | compile, serialization, multiSelection, undo, prefabOverride, visual |
| ShaderGUI/material Inspector | shader-gui-contract, compile, serialization, multiSelection, undo, prefabOverride, visual |
| Menu/action tool | compile, menuReachability, invalidInput, boundary, repeatIdempotency |
| Asset preview/import tool | compile, previewLifecycle, missingAsset, cleanup, reloadDomain, performance |
| Background editor service | compile, startupScope, cancellation, cleanup, reloadDomain, performance |

Static checks may add blockers, but cannot satisfy Unity evidence rows. `framework-integration` and `visual` are critical, weight-3 rows. Every EditorWindow must expose a bounded minimum and either a fixed maximum or an approved adaptive strategy: `adaptive-resolve`, `content-adaptive`, `host-bounded`, or `unbounded-flexible`. Visual evidence must use structured `bounds` and `viewports` fields and exercise `narrow`, `wide`, `high-dpi`, and `extreme-resolution` cases with no clipping or overlap. `Ready` requires every required row marked `passed` with fresh, hash-bound evidence. In `Development` mode, missing non-critical evidence may produce `Degraded`; `Acceptance` and `Release` treat it as `Blocked`. Critical framework/layout evidence, structural failures, and boundary failures always remain `Blocked`, including in Development mode.
