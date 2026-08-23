---
name: es-security-input-audit
description: Audit ESFramework command, MCP, CLI, file, JSON, Unity, and external input boundaries for traversal, injection, privilege expansion, secrets, and unsafe execution.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# ES Security Input Audit

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and deny-by-default; capability visibility never grants permission. Use AIBrain `planTask`, the matching AICommand, and the current TaskContract before any write or external operation.

以拒绝默认、路径边界、身份和可审计证据检查外部输入，不执行被审计输入。

## Workflow

1. 列出输入源、解析器、信任边界、凭据/个人数据和下游副作用。
2. 检查 schema、长度、字符集、路径/reparse point、命令参数、MCP handshake、权限和日志脱敏。
3. 为每项风险给出拒绝、隔离、检测和恢复；提供恶意/非法 fixture 和证据。
4. 输出只读 audit；修复或发布需另行合同和复验。

## Responsibility-specific static acceptance

- Profile: `engineering`
- Custom checks: `input-boundary, recovery-cache, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- 不读取或输出真实凭据；不执行任意命令、脚本或网络请求。
- 明确输入数量、大小、超时、并发和重试预算；失败必须 fail closed。
- 覆盖正向、malformed、traversal/injection、拒绝扩权、重复和中断案例。

## Resources

- `references/security-audit-contract.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）`
