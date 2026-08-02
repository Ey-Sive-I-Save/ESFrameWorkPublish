---
name: es-use-ai-command
description: Select, validate, and execute one ESFramework AICommand as the task authorization contract. Use when the user provides an Assets/Plugins/ES/AICommands path, asks to choose or run an AICommand, or requests an ES project task that should follow an existing command template.
---

# Use an ES AICommand

Treat the selected AICommand as the project-specific task contract. Do not infer permission from the file name.

## Workflow

1. Resolve the Git repository root and confirm it is the ESFramework project.
2. Run `scripts/Test-ESAICommands.ps1 -ProjectRoot <root>` before relying on the command library.
3. Read these live project files with explicit UTF-8:
   - `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
   - `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
   - `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
4. If the user supplied a command path, read that file completely. Otherwise inspect `命令合集索引_AI命令.md` and select exactly one command whose scope matches the request.
5. Restate the command type, write permission, risk level, required inputs, required reading, affected paths, and verification contract.
6. Inspect the worktree before editing. Preserve unrelated user or agent changes.
7. Apply only the intersection of the user's request and the command's authorization. Ask before proceeding when a required parameter is genuinely missing.
8. Run the verification required by the command. Keep `.csproj` compilation, Unity Editor compilation, Test Runner, PlayMode, Profiler, IL2CPP, and release evidence distinct.

## Rules

- Never execute multiple commands as a combined permission grant.
- Never let an AICommand override P0 AIWarnings or current source facts.
- Never modify files when the command is read-only unless the user separately authorizes the change.
- Never write or restore AI collaboration history unless the user explicitly requests it.
- Report missing command references as a command-library defect; do not silently guess replacements.

## Delivery

Report: selected command, rules read, work performed, changed files, validation evidence, and remaining risks.

## Script

`scripts/Test-ESAICommands.ps1` validates UTF-8, required metadata, and project-relative references for every command. It does not modify files.
