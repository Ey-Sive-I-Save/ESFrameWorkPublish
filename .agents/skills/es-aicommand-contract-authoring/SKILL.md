---
name: es-aicommand-contract-authoring
description: Design and validate ESFramework AICommand contracts with bounded permissions, exact inputs, required reads, dry-run, cancellation, recovery, and acceptance evidence.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# ES AICommand Contract Authoring

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and capability visibility never grants AI-initiated authority. The current explicit user request authorizes its bounded action under `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`; AIBrain, AICommand and TaskContract are protocol inputs only when their managed channel is selected. Reject inferred expansion, not user-directed paths.

把一次任务的授权边界写成可发现、可验签、可拒绝扩权的合同。

## Workflow

1. 读取 AICommand README、AIWarnings 命中规则、Resource Index 和目标 Skill；确认 NoMatchingCommand 状态。
2. 定义稳定 id、命令类型、风险、默认改文件、输入 schema、读取链、写范围、dry-run、确认、取消、重试和回滚。
3. 更新 catalog、正文和引用，验证路径、哈希、导航角色和 AICommand/Skill/AIBrain 边界。
4. 用正向、缺输入、拒绝越界、重复 invocation 和中断回放验证；用户未明确要求正式写入的自主内容只能进入候选目录。

## Specialized static acceptance

- Guidance: `references/static-specialized-acceptance.md`
- Acceptance ID: `aicommand-contract`
- Required cases: `command-id-closure, task-contract-binding, write-scope-denial, risk-level-consistency, command-hash-stale`
- Static assertions: AICommand; TaskContract; write scope; risk level; command hash
- This contract is responsibility-specific and remains distinct from Runtime proof.

## Responsibility-specific static acceptance

- Profile: `governance`
- Custom checks: `authority-routing, permission-boundary, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- 合同提供受管通道协议但不覆盖 P0、当前用户授权或 AIBrain 计划；不创建“万能命令”。
- 禁止直接写正式 AICommands，除非用户明确授权并完成 Diff Review；候选生成走隔离目录。
- 覆盖 malformed schema、旧 hash、错误路径、越权写入、重复和恢复。

## Resources

- `references/command-contract.md`
- `Assets/Plugins/ES/AICommands/README.md`


## Specialized static acceptance

Acceptance ID: `aicommand-contract`

Responsibility-specific static assertions (these are source-level requirements, not Runtime claims):
- AICommand
- TaskContract
- write scope
- risk level
- command hash

Required specialized cases: `command-id-closure, task-contract-binding, write-scope-denial, risk-level-consistency, command-hash-stale`
Guidance: `references/static-specialized-acceptance.md`
