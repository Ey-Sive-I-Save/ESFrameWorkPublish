---
name: es-editor-tooling
description: Build or review ESFramework Unity Editor windows, inspectors, drawers, ESEditorSection navigation, SO tables, menu tools, asset preview tools, serialized-property migrations, and ReloadDomain-safe caches. Use for files under Assets/Plugins/ES/Editor or any ES editor initialization, scanning, preview, menu, drawer, or tooling task.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# Build ES Editor Tooling

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and deny-by-default; capability visibility never grants permission. Use AIBrain `planTask`, the matching AICommand, and the current TaskContract before any write or external operation.

## Specialized static acceptance

- Guidance: `references/static-specialized-acceptance.md`
- Acceptance ID: `editor-tooling-boundary`
- Required cases: `host-selection, window-lifecycle, serialized-property-boundary, undo-dirty-contract, panel-rebuild`
- Static assertions: Editor Host; SerializedObject; Undo; Dirty; Panel rebuild
- This contract is responsibility-specific and remains distinct from Runtime proof.

## Responsibility-specific static acceptance

- Profile: `editor`
- Custom checks: `editor-layout-static, lifecycle-boundary, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- Scope and authority are checked before execution; stale or missing evidence blocks the task.
- Execute only through AIBrain planTask and the matching AICommand; direct execution is denied.
- Record evidence for positive, invalid-input, denied-expansion, repeat-idempotency, and interruption-recovery cases.

Keep editor tools explicit, user-driven, serialized-property safe, and resilient across domain reload without adding startup-wide scanning.

## Workflow

1. Read the AIWarnings start files and `references/project-map.md`.
2. For any window, workbench, inspector, drawer, dialog, popup, layout, DPI, scroll, owner-lifecycle, or ReloadDomain work, read `Documentation/ES_EDITOR_WINDOW_PRODUCTION_STANDARD.md` and the availability matrix before implementation.
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

Report the tool category, state model, serialization and Undo behavior, scan trigger, cleanup path, ReloadDomain evidence, interaction gaps, and information-density/layout risks. Keep the first view focused on status, conclusion, and next action; progressively disclose logs, hashes, stack traces, and raw JSON. Every generated report, log, snapshot, handoff, or other user-facing artifact must include a stable project path plus a guarded quick-open, project-locate, open-report, or copy-path action when the host has UI. If the host cannot provide such an action, state that limitation explicitly and give the shortest machine-readable or manual path; printing a path alone is not complete delivery for a user-facing main result.

For user-visible windows, inspectors, dialogs, and diagnostic panels, record whether the first view shows status/conclusion/main action without scrolling, whether critical actions avoid horizontal scrolling, whether long values can be copied in full, and whether failure views include cause/impact/recovery. Validate narrow-window and high-DPI layouts with screenshots when Unity visual evidence is required; source inspection alone is not visual acceptance.

## 受管场景修改入口

AI 或 UnityMCP 如需修改并保存场景，只能调用 ESAutomation Bridge 的 `modifyActiveScene`：

- 目标必须是当前已加载的 Active Scene；
- 请求必须携带精确 `scenePath`、白名单 `operations`、`dryRun` 和 `save`；
- 当前白名单仅为 `setActive`、`setName`、`setTag`、`setLayer`；
- 真实修改必须经过 Undo、Dirty 标记，并由 C# Editor 调用 `EditorSceneManager.SaveScene`；
- PlayMode、任意脚本、任意资产路径和直接编辑 `.unity` YAML 均禁止。

Bridge 响应只证明 Editor 主线程完成了操作；场景重新加载、Prefab 覆盖、运行时行为和发布结果仍需单独验收。


## Specialized static acceptance

Acceptance ID: `editor-tooling-boundary`

Responsibility-specific static assertions (these are source-level requirements, not Runtime claims):
- Editor Host
- SerializedObject
- Undo
- Dirty
- Panel rebuild

Required specialized cases: `host-selection, window-lifecycle, serialized-property-boundary, undo-dirty-contract, panel-rebuild`
Guidance: `references/static-specialized-acceptance.md`
