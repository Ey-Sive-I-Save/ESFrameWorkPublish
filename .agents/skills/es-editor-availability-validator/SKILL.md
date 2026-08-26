---
name: es-editor-availability-validator
description: Validate whether an ESFramework Unity EditorWindow or editor extension is actually usable, including structural safety, compile evidence, domain-reload recovery, interaction, visual layout, cleanup, performance, and freshness of evidence. Use when deciding if an editor window, drawer, inspector, menu tool, preview tool, workbench, or editor-only service is Ready, Degraded, Blocked, or Unavailable.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# Editor Availability Validator

This is a read-only availability裁决器. It separates source-level signals from Unity evidence and never treats a file existing or a window opening once as production readiness.

Use the global Static/Runtime semantics in `.agents/skills/es-skill-governance/references/verification-semantics.md`: source layout and lifecycle checks report static support; Unity geometry, DPI, panel rebuild and clickability report runtime evidence. `runtime-not-run` is not a static failure.

Layout and ES framework integration are critical dimensions, each with weight `3`. Missing layout bounds or missing ES foundation integration blocks availability even when other source checks pass.

When validating lifecycle or user-visible state behavior, require the `es-ai-interaction-governance` intent contract and report any observed or unproven violation of its `mustPreserve` or `forbiddenTransitions` fields separately from compile/layout evidence. Static availability must not be upgraded by a self-authored contract; the contract only bounds the claim and required evidence.

The validator is target-kind aware: `-TargetKind Auto|EditorWindow|Workbench|AdvancedDropdown|TransientPopup|PreviewWindow|InspectorDrawer|ShaderGUI|MenuAction|PreviewImport|BackgroundService` selects the responsibility matrix in `references/availability-matrix.md`. EditorWindow/Workbench targets additionally receive one result for each stable rule in `references/editor-rule-registry.json` (`EW-01` through `EW-20`); AdvancedDropdown, TransientPopup, PreviewWindow, InspectorDrawer, and ShaderGUI use separate contracts and are not misclassified as full EditorWindows. `not-applicable` is a scoped result, not a failure.

The script supports `StaticReview`, `Development`, `Acceptance`, and `Release` validation modes. `StaticReview` is the default and performs source-level deep replay without starting Unity. Development mode may return `Degraded` for non-critical missing evidence; Acceptance/Release require explicitly authorized runtime evidence. Critical source defects produce `StaticBlocked`; missing external evidence produces `RuntimeRequiredForSelectedProfile`/`runtime-blocked`, not a claim that the source is defective.

## Specialized static acceptance

- Guidance: `references/static-specialized-acceptance.md`
- Acceptance ID: `editor-availability-static`
- Required cases: `min-max-window-contract, narrow-wide-layout, dpi-boundary, reload-unbind, runtime-escalation-scope`
- Static assertions: minSize; maxSize; narrow; DPI; ReloadDomain; Unbind
- This contract is responsibility-specific and remains distinct from Runtime proof.

## Responsibility-specific static acceptance

- Profile: `editor`
- Custom checks: `editor-layout-static, lifecycle-boundary, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- Require an AIBrain plan and matching AICommand before any Unity or external validation operation.
- Keep the validator read-only; reports may be written only to an explicit project-relative path.
- Treat missing, stale, contradictory, or out-of-scope evidence as `blocked` or `not-run`.
- Never claim Unity, ReloadDomain, interaction, visual, performance, Player, IL2CPP, or release acceptance from source inspection alone.

## Workflow

1. Load `es-editor-tooling`, the routed AIWarnings, the matching AICommand, and the evidence contract before inspecting the target.
2. Run `scripts/Invoke-ESEditorAvailability.ps1` with a project-relative target file or directory. Use `-TargetKind` when classification is not obvious. Use the default `-ValidationMode StaticReview` for deep source replay, `Development` for bounded iteration diagnostics, and `Acceptance`/`Release` only after explicit developer authorization for runtime evidence.
3. Supply a project-relative evidence manifest when Unity, ReloadDomain, interaction, visual, recovery, or performance claims are available. Missing critical rows remain `blocked`; missing non-critical rows are `Degraded` only in Development mode and `Blocked` in Acceptance/Release.
4. Review static findings and the state matrix. Fix source defects before repeating Unity acceptance; do not turn warnings into passes by editing the report.
5. Use `references/availability-matrix.md` to decide the minimum evidence rows for the target category.

## State decision

The result is the weakest required dimension; critical dimensions are also reported with higher weight for review ordering:

```text
Ready       = scoped profile requirements passed; the report's `overallVerdict` and `scope` define what Ready means
Degraded    = core interaction works, but a non-critical dimension is unavailable
Blocked     = compatibility status for a selected profile; inspect `staticStatus` and `runtimeStatus` to distinguish source (`static-blocked`) from external evidence (`runtime-blocked`)
Unavailable = target/dependency cannot be loaded or the tool cannot start
```

`Static` is never a release claim. A source scan can identify risk and missing evidence, but only Unity evidence can establish compile/import, ReloadDomain, actual interaction, layout, cleanup, and editor performance.

## Required dimensions

- Structural: target path, editor-only placement, UTF-8, menu/window entry, and referenced assets.
- Boundary: no broad `InitializeOnLoad` scan, direct target mutation where SerializedObject/Undo is required, leaked callbacks, persisted live Unity references, or unbounded asset enumeration.
- Framework integration (critical, weight 3): standard ES window/workbench foundation, sleep contract, action hosts, and close/reload unbind lifecycle. Direct `EditorWindow` implementations must use `ESWindowFoundation` or an approved ES base; transient exceptions must be explicit.
- Compile: Unity import, Console, assembly reload, and relevant Test Runner evidence.
- ReloadDomain: close/reopen, SessionState/EditorPrefs/static-cache restoration, callback disposal, and stale reference handling.
- Interaction: first view, main action, error/recovery path, keyboard/mouse focus, multi-selection, Undo/Redo, prefab overrides, and long-value access where applicable.
- Visual (critical, weight 3): declared minimum and maximum window bounds or an approved adaptive strategy, narrow/wide/high-DPI behavior, no clipping/overlap, readable status/action hierarchy, and deterministic evidence at extreme viewports.
- Recovery: interrupted scan/preview/export, external asset drift, locked or missing asset, and repeat-idempotency.
- Performance: explicit scan trigger, bounded work, no domain-load full scan, stable repaint behavior, and measured evidence for expensive tools.

## Evidence rules

Use a manifest with one row per required dimension. Each passed row must identify the exact command or Unity test, target/version, timestamp, artifact path, and source hashes. The `visual` row must use structured `bounds` (`minimum`, `maximum`, `adaptive`, `strategy`) and `viewports` fields; approved strategies are `fixed`, `adaptive-resolve`, `content-adaptive`, `host-bounded`, and `unbounded-flexible`. A screenshot alone cannot prove interaction, serialization, resource loading, or lifecycle correctness. The validator reports missing rows; it does not invent them.

The Skill is read-only and does not open Unity, modify assets, save scenes, or claim Player/IL2CPP/release acceptance.

## Resources

- `scripts/Invoke-ESEditorAvailability.ps1`: deterministic source and evidence matrix validator.
- `references/availability-matrix.md`: category-specific required rows and status rules.
- `references/evidence-receipt-contract.md`: receipt binding requirements.


## Specialized static acceptance

Acceptance ID: `editor-availability-static`

Responsibility-specific static assertions (these are source-level requirements, not Runtime claims):
- minSize
- maxSize
- narrow
- DPI
- ReloadDomain
- Unbind

Required specialized cases: `min-max-window-contract, narrow-wide-layout, dpi-boundary, reload-unbind, runtime-escalation-scope`
Guidance: `references/static-specialized-acceptance.md`
