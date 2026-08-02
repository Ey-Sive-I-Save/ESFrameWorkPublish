---
name: es-fix-compile-error
description: Diagnose, minimally fix, and verify exactly one explicit ESFramework C# or Unity compilation error. Use when the user supplies a concrete compiler error, file and line, or asks to fix a single current compile failure without broad cleanup.
---

# Fix One ES Compilation Error

Keep the repair scoped to one explicit error and its direct cause.

## Workflow

1. Read `Assets/Plugins/ES/AICommands/执行_修复单个编译错误_AI命令.md` completely.
2. Read the AIWarnings start files and all rules routed for the affected subsystem.
3. Inspect Git status and the target file diff before editing. Preserve overlapping user changes.
4. Reproduce the exact error in its authoritative layer:
   - Unity error: use UnityMCP Console and current project instance.
   - Generated-project error: run the exact `.csproj` build.
5. Inspect the failing symbol, declaration, assembly boundary, and recent related diff. Identify the root cause before editing.
6. Apply the smallest coherent patch. Do not format the whole file, rename unrelated APIs, repair neighboring warnings, or modify generated project files.
7. Re-run the original failing layer. For Unity scripts, import/refresh, wait for domain reload, and read Console.
8. If another error remains, report it separately. Do not silently expand the task into fixing multiple failures.

## Required boundaries

- Do not treat a missing generated `.csproj` include as proof that Unity excluded the source.
- Do not treat `dotnet build` success as Unity Editor success.
- Do not suppress errors, weaken types, add broad compatibility shims, or restore obsolete APIs merely to compile.
- Do not overwrite unrelated dirty-worktree changes.
- Run the UTF-8 guard for every changed text file.

## Delivery

Report the original error, root cause, exact patch, changed files, original-layer verification, other remaining errors, and untested layers.
