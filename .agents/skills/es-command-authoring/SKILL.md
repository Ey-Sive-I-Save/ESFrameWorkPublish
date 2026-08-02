---
name: es-command-authoring
description: Add or review ESFramework ESCommand runtime command types, categories, serializable context, ESCommandPlayer and Runner execution, virtual-input commands, and Start/Stop lifecycle behavior. Use when creating an ESCommand, changing command context or categories, wiring command events, or diagnosing command execution and lifecycle issues.
---

# Author ESCommands

Create commands that follow the existing runtime execution contract instead of adding an unrelated command bus or hiding state in the command asset.

## Workflow

1. Read the AIWarnings start files, `references/project-map.md`, and `ESCommand_STANDARD.md` completely.
2. Select `执行_新增ESCommand运行时命令_强约束_AI命令.md` for implementation. Informational commands do not grant write permission.
3. Inspect similar current commands, their category registration, serialized fields, context acquisition, runner path, and lifecycle.
4. Define input, output, ownership, reentrancy, cancellation, and Start/Stop semantics before editing.
5. Keep per-execution mutable state in the execution context or runtime instance, not shared serialized command data.
6. Register the category and editor discoverability through the existing path only when required.
7. Add focused tests or a reproducible runner case. Use `$es-unity-compile` for import, Console, and runtime evidence.
8. Run `$es-utf8-guard` and document other commands or runners not exercised.

## Required boundaries

- Do not create a second command abstraction beside `ESCommand` for the same responsibility.
- Do not assume every Operation or command has Stop; follow the declared lifecycle contract.
- Do not cache scene objects, player instances, or per-run state in shared assets.
- Do not bypass `ESCommandPlayer` or the authoritative Runner when the command belongs to that execution frame.
- Do not use obsolete ESVMCP command code as the implementation model.

## Delivery

Report the command type, category, serialized contract, runtime context, execution lifecycle, editor discoverability, tests, and unverified runners.
