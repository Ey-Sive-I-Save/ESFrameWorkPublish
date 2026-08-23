# ES Editor Window Production Standard

This is the source-of-truth entry for editor-window static acceptance. It is intentionally split from Unity runtime acceptance.

## Required order

1. Classify the target as `EditorWindow`, `Workbench`, `InspectorDrawer`, `MenuAction`, `PreviewImport`, or `BackgroundService`.
2. Run `Invoke-ESEditorAvailability.ps1` with the explicit `-TargetKind` when inference is uncertain.
3. Read the target-kind matrix and inspect the `EW-01` through `EW-20` result set.
4. Treat `StaticCompleteRuntimePending` as a completed source/configuration review, not as proof of Unity behavior.
5. Request bounded runtime authorization only for claims that static analysis cannot prove.

## Rule contract

The stable rule definitions are in `.agents/skills/es-editor-availability-validator/references/editor-rule-registry.json`. Each rule declares its scope, static checks, runtime checks, severity, and required evidence boundary. A rule outside the target kind is `not-applicable`; it must not be converted into a false failure.

## Static acceptance minimum

Static review must cover source scope/UTF-8, ES lifecycle integration, mutation boundaries, deterministic state/recovery paths, size strategy, and all applicable EW rules. Static evidence may prove implementation structure and deterministic replay, but cannot claim actual DPI geometry, Unity panel mounting, reload behavior, or profiler measurements.

## Runtime boundary

Runtime is required only for the selected acceptance/release profile and must use a one-time authorization bound to the task, PlanHash, command, target paths, time budget, timeout, and stop condition. Missing runtime evidence is reported as `runtime-not-run` or `Degraded` in development diagnostics; it is not a reason to rewrite source that already passed static review.

## Human/AI route

For creating, refactoring, or accepting an ES editor tool: read this standard, then `es-editor-tooling`, then `es-editor-availability-validator`, and finally `es-release-acceptance` for release claims.
