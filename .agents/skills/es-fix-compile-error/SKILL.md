---
name: es-fix-compile-error
description: Diagnose, minimally fix, and verify exactly one explicit ESFramework C# or Unity compilation error. Use when the user supplies a concrete compiler error, file and line, or asks to fix a single current compile failure without broad cleanup.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# Fix One ES Compilation Error

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and capability visibility never grants AI-initiated authority. The current explicit user request authorizes its bounded action under `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`; AIBrain, AICommand and TaskContract are protocol inputs only when their managed channel is selected. Reject inferred expansion, not user-directed paths.

## Responsibility-specific static acceptance

- Profile: `engineering`
- Custom checks: `input-boundary, recovery-cache, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- Scope and authority are checked before execution; stale or missing evidence blocks the task.
- A current user request directly authorizes the bounded source fix. Unity compilation/runtime, external, destructive and Git actions must be explicitly named; managed-channel plans and commands are protocol inputs only.
- Record evidence for positive, invalid-input, denied-expansion, repeat-idempotency, and interruption-recovery cases.

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
   使用 [编译修复包验证器](scripts/Test-ESCompileRepairPacket.ps1) 检查单错误范围、根因、禁止抑制、生成项目保护和原层验证。
7. Re-run the original failing layer. For Unity scripts, import/refresh, wait for domain reload, and read Console.
8. If another error remains, report it separately. Do not silently expand the task into fixing multiple failures.

## Required boundaries

- Do not treat a missing generated `.csproj` include as proof that Unity excluded the source.
- Do not treat `dotnet build` success as Unity Editor success.
- Do not suppress errors, weaken types, add broad compatibility shims, or restore obsolete APIs merely to compile.
- Do not overwrite unrelated dirty-worktree changes.
- `scripts/Test-ESCompileRepairPacket.ps1` 通过前不得把修复描述升级为已验证。
- Run the UTF-8 guard for every changed text file.

## Delivery

Report the original error, root cause, exact patch, changed files, original-layer verification, other remaining errors, and untested layers.
