# Codex Tool Rewrite Context

> Role: this Codex pass is responsible for ES editor-tool rewrite support and commercial-grade validation, not for redefining the whole gameplay architecture.
> Purpose: give future AI collaborators a dense, verifiable starting point before modifying or rewriting ES Framework tools.
> Scope: observations from the local project at `F:\aaProject\ESFrameWorkPublish` on 2026-07-17. Treat this as a tool-rewrite context map, not as product documentation.

## Responsibility Boundary

- Primary responsibility: validate, harden, and when requested rewrite small ES tools to a commercial standard.
- Main working surface: `Assets/Plugins/ES/Editor`, tool-related code in `Assets/Plugins/ES/0_Stand`, `Assets/Plugins/ES/1_Design`, and tool-facing data/assets they directly read or write.
- Secondary responsibility: identify architecture assumptions that affect tools, then verify them locally before changing behavior.
- Out of default scope: broad player runtime redesign, GameManager domain redesign, StateMachine/IK/Buff rewrites, or generated-data redesign unless the requested tool depends on them.
- Collaboration rule: if a tool rewrite touches runtime systems, first read the relevant `AIWarnings` note and the source code it names; do not infer from this file alone.

## Project Baseline

- Unity version is `2022.3.57f1c1`, from `ProjectSettings/ProjectVersion.txt`.
- Primary plugin root is `Assets/Plugins/ES`.
- Third-party plugins under `Assets/Plugins` include DOTween, Easy Save 3, RootMotion, and Sirenix/Odin Inspector.
- Main ES folders:
  - `0_Stand`: base framework layer, value types, containers, SO support, editor-safe utilities, AssemblyStream.
  - `1_Design`: design/runtime abstractions, input system definitions/services, domain/link/runtime mode tools.
  - `2_Feature`: 已迁空；不要在这里新增项目功能。`ESCommandPlay` 已迁到 `Assets/Scripts/ESLogic/Runtime/Features/ESCommandPlay`。
  - `Editor`: commercial validation focus; contains installer, menu-tree windows, drawers, GraphView, TrackView, resource and SO-data tools.
  - `3_Examples`: examples and test scenarios. Do not treat examples as production behavior without checking references.
  - `Generated`: generated Luban outputs.
  - `Obsolete`: legacy/preview code. Read only when current code references it or the user explicitly asks.

## Assembly Boundaries

- `Assets/Plugins/ES/0_Stand/ES_Stand.asmdef` has name `ES_Stand`.
- `Assets/Plugins/ES/1_Design/ES_Design.asmdef` references `ES_Stand`, `Sirenix`, and `Unity.InputSystem` by asmdef/GUID/package reference.
- `ES_Feature.asmdef` 已删除。`ESCommandPlay` 当前随 `ES_Logic` 编译。
- `Assets/Plugins/ES/Editor/Installer/ESInstaller.asmdef` is Editor-only and `autoReferenced=false`; verify actual Unity compilation/availability before assuming it can be used from other assemblies.
- Many editor scripts are not in a dedicated visible asmdef under `Assets/Plugins/ES/Editor`; expect Unity's default editor assembly behavior unless a nearby asmdef is found.

## Menu and Window Entry Points

- Main menu path constants live in `Assets/Plugins/ES/0_Stand/Stand_Tools/OnlyEditor/MenuItemPathDefine.cs`.
- The root menu is `【ES】`.
- Core editor windows observed through `MenuItem` search:
  - `Editor/ESMenuTreeWindow/ResWindow/ESResWindow.cs`: `【资源管理】窗口`.
  - `Editor/ESMenuTreeWindow/SODataInfoWindow/ESSODataInfoWindow.cs`: `【SO】数据窗口`.
  - `Editor/ESMenuTreeWindow/SimpleToolsWindow/SimpleToolsWindow.cs`: `简单工具集成`.
  - `Editor/ESGraphView/Graphview-Define/ESGraphViewWindow.cs`: `【图】编辑器`.
  - `Editor/Installer/ESInstaller.cs`: dependency/install manager and dependency check menu items.
- Shared Odin menu-window base is `Editor/ESMenuTreeWindow/-Templates/-ESMenuTreeWindow.cs`.

## Current Worktree Warning

- The repository already had many modified, deleted, and untracked files before this note was written.
- Important touched areas observed in `git status --short` include:
  - `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/PackGroupInfo/EditorOnly/InfoType/*`
  - `Assets/Plugins/ES/1_Design/Input/*`
  - `Assets/Plugins/ES/Editor/ESDrawer/Normal/*`
  - `Assets/Plugins/ES/Editor/ESMenuTreeWindow/*`
  - `Assets/Scripts/ESLogic/*`
  - deleted `Assets/Plugins/ES/2_Feature/*`
- Do not revert, clean, or normalize these changes unless the user explicitly requests it.
- Before editing a file in a dirty area, inspect the file and its diff first. Assume changes belong to the user or another AI.

## Encoding Warning

- Use UTF-8 when reading source files with Chinese comments or menu strings.
- PowerShell default output can show mojibake for these files. Example: `Get-Content -Encoding UTF8`.
- Do not "fix" readable Chinese strings based only on garbled terminal output.

## Commercial Validation Priorities

For small-tool validation, prioritize in this order:

1. Compile and assembly availability: missing references, Editor-only leakage into runtime assemblies, asmdef dependency mistakes.
2. Tool entry reliability: `MenuItem` paths, window creation, initialization order, null static state, stale singleton/window references.
3. Data safety: asset writes, generated files, `.meta` preservation, destructive batch actions, path assumptions, dirty asset persistence.
4. Unity lifecycle: `InitializeOnLoad`, `delayCall`, `OnDestroy`, `OnDisable`, domain reload, play mode transition.
5. UX correctness for production use: clear errors, undo support, progress/cancel behavior, selection handling, disabled states, no silent partial success.
6. Dependency handling: Package Manager async request status, class-existence checks, optional vs required packages, offline/network failure behavior.

## Do Not Assume

- Do not assume `Obsolete` code is inactive; confirm references before deleting or ignoring behavior.
- Do not assume `Generated/Luban` files are hand-editable.
- Do not assume menu strings are duplicated bugs; some paths may intentionally expose legacy or test entries.
- Do not assume Odin is optional; several editor windows inherit Odin editor classes.
- Do not assume Easy Save 3 is part of ES core; it is a third-party plugin newly present in this workspace.

## Suggested First Checks For Any Tool

- Search exact class and menu path with `rg`.
- Read nearby `.asmdef` files and `using UnityEditor` placement.
- Check `git status --short -- <path>` before edits.
- If the tool writes assets, identify every path it can write before running it.
- Prefer narrow validation artifacts: one focused note, one focused test, or one small guard per issue.
