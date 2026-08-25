---
name: es-change-risk-register
description: Create and validate a bounded risk register for ESFramework changes. Use before cross-module edits, migrations, release work, destructive operations, or tasks with unclear ownership and rollback.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# ES Change Risk Register

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and capability visibility never grants AI-initiated authority. The current explicit user request authorizes its bounded action under `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`; AIBrain, AICommand and TaskContract are protocol inputs only when their managed channel is selected. Reject inferred expansion, not user-directed paths.

把风险转成可执行的预防、检测、隔离、恢复和验收条目。

## Workflow

1. 读取目标 AIWarnings、AICommand、Skill Resource Index 和工作树事实。
2. 为每个影响面登记 owner、权限、变更预算、依赖、兼容性、性能、证据、停止条件和 rollback。
3. 按 `ReadOnly / StateChanging / AssetWriting / Destructive` 与 `None / Confirm / PreviewThenConfirm / ExplicitPhrase` 分离风险和确认。
4. 将每项风险绑定检测命令、证据路径和恢复动作；缺少 owner 或 evidence 时标为 Blocked。
5. 变更完成后回放风险清单，关闭已验证项，保留未完成责任。

## Responsibility-specific static acceptance

- Profile: `engineering`
- Custom checks: `input-boundary, recovery-cache, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- 不授予权限，不替代 AICommand；风险登记是计划输入和验收证据。
- 支持正向、非法输入、拒绝扩权、重复回放和中断恢复案例。
- 变更预算必须含路径、对象数、重试、并发、超时和停止条件。
- 不以“风险低”跳过 Unity、Profiler、Player、IL2CPP 或发布证据。

## Resources

- `references/risk-register-contract.md`
- `.agents/skills/es-skill-governance/references/commercial-controls.md`
