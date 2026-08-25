---
name: es-command-authoring
description: Add or review ESFramework ESCommand runtime command types, categories, serializable context, ESCommandPlayer and Runner execution, virtual-input commands, and Start/Stop lifecycle behavior. Use when creating an ESCommand, changing command context or categories, wiring command events, or diagnosing command execution and lifecycle issues.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# Author ESCommands

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and capability visibility never grants AI-initiated authority. The current explicit user request authorizes its bounded action under `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`; AIBrain, AICommand and TaskContract are protocol inputs only when their managed channel is selected. Reject inferred expansion, not user-directed paths.

## Responsibility-specific static acceptance

- Profile: `authoring`
- Custom checks: `change-boundary, resource-projection, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- Scope and authority are checked before execution; stale or missing evidence blocks the task.
- A current user request directly authorizes its bounded command, source, Assets and governance changes. Runtime, external, destructive and Git actions must be explicitly named; managed-channel plans and commands are protocol inputs only.
- Record evidence for positive, invalid-input, denied-expansion, repeat-idempotency, and interruption-recovery cases.

Create commands that follow the existing runtime execution contract instead of adding an unrelated command bus or hiding state in the command asset.

## Workflow

1. Read the AIWarnings start files, `references/project-map.md`, and `ESCommand_STANDARD.md` completely.
2. Select `执行_新增ESCommand运行时命令_强约束_AI命令.md` for implementation. Informational commands do not grant write permission.
3. Inspect similar current commands, their category registration, serialized fields, context acquisition, runner path, and lifecycle.
4. Define input, output, ownership, reentrancy, cancellation, and Start/Stop semantics before editing.
5. Keep per-execution mutable state in the execution context or runtime instance, not shared serialized command data.
   使用 [ESCommand 合同验证器](scripts/Test-ESCommandContractPacket.ps1) 检查权威 Runner、上下文、生命周期、重入和禁止的平行总线。
6. Register the category and editor discoverability through the existing path only when required.
7. Add focused tests or a reproducible runner case. Use `$es-unity-compile` for import, Console, and runtime evidence.
8. Run `$es-utf8-guard` and document other commands or runners not exercised.

## Required boundaries

- Do not create a second command abstraction beside `ESCommand` for the same responsibility.
- Do not assume every Operation or command has Stop; follow the declared lifecycle contract.
- Do not cache scene objects, player instances, or per-run state in shared assets.
- Do not bypass `ESCommandPlayer` or the authoritative Runner when the command belongs to that execution frame.
- Do not use obsolete ESVMCP command code as the implementation model.
- 不得把 RuntimeKey 或共享序列化资产状态当作每次执行身份。

## Delivery

Report the command type, category, serialized contract, runtime context, execution lifecycle, editor discoverability, tests, and unverified runners.
