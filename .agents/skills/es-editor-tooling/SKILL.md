---
name: es-editor-tooling
description: Build or review ESFramework Unity Editor windows, inspectors, drawers, ESEditorSection navigation, SO tables, menu tools, asset preview tools, serialized-property migrations, and ReloadDomain-safe caches. Use for files under Assets/Plugins/ES/Editor or any ES editor initialization, scanning, preview, menu, drawer, or tooling task.
---

# Build ES Editor Tooling

Keep editor tools explicit, user-driven, serialized-property safe, and resilient across domain reload without adding startup-wide scanning.

## Workflow

1. Read the AIWarnings start files and `references/project-map.md`.
2. Select the closest AICommand for the target window, preview, ReloadDomain, or SO tool. Confirm whether it authorizes edits.
3. Classify the tool as window, drawer, attribute processor, menu action, asset preview, SO table, or background service.
4. Inspect current ES examples and identify state ownership across serialized assets, `SessionState`, `EditorPrefs`, static caches, and live editor objects.
5. Use `SerializedObject` and `SerializedProperty` for multi-object and undo-sensitive editing. Preserve mixed values and prefab override behavior.
6. Keep scans behind explicit user action or a proven incremental invalidation path. Dispose callbacks, previews, PropertyTrees, and temporary resources.
7. Use `$es-unity-compile` to import scripts, wait for ReloadDomain, read Console, reopen the tool, and exercise the changed interaction.
8. Run `$es-utf8-guard` and report untested Unity-version, multi-object, prefab, or reload cases.

## Required boundaries

- Do not add broad `InitializeOnLoad` scanning, eager reflection catalogs, or asset enumeration without explicit P0 approval.
- Do not mutate targets directly when serialized editing, Undo, prefab overrides, or multi-object support is required.
- Do not persist live Unity object references across domain reload.
- Do not treat a window drawing once as proof that reload, multi-selection, nested serialization, and disposal are correct.
- Do not use files under `Obsolete` as current implementation authority.

## Delivery

Report the tool category, state model, serialization and Undo behavior, scan trigger, cleanup path, ReloadDomain evidence, and interaction gaps.
