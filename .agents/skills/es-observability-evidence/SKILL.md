---
name: es-observability-evidence
description: Design and audit reproducible evidence, RunRecord, logs, traces, receipts, and failure reports for ESFramework automation and runtime workflows.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# ES Observability Evidence

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and capability visibility never grants AI-initiated authority. The current explicit user request authorizes its bounded action under `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`; AIBrain, AICommand and TaskContract are protocol inputs only when their managed channel is selected. Reject inferred expansion, not user-directed paths.

让每次任务回答谁、何时、用什么 Skill/Command/PlanHash、读写什么、结果和恢复方式。

## Workflow

1. 定义 task identity、actor、PlanHash、inputs、outputs、events、artifact hashes 和 retention。
2. 绑定 AIWarnings、AICommand、Knowledge、Skill、MCP/Worker、Unity/Test/Profiler evidence。
3. 检查成功、拒绝、取消、超时、崩溃、Domain Reload 和部分失败是否可重放。
4. 输出 receipt 与 evidence matrix；缺少可重读路径时降级或 Blocked。

## Responsibility-specific static acceptance

- Profile: `engineering`
- Custom checks: `input-boundary, recovery-cache, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- 证据采集默认只读；禁止把瞬时 Console 或聊天摘要写成长期事实。
- 记录规模、事件量、保留期、并发、脱敏和失败恢复；不收集凭据。
- 覆盖正向、非法输入、拒绝扩权、重复 invocation 和中断恢复。

## Resources

- `references/evidence-receipt-contract.md`
- `Documentation/AIKnowledge/entries/aibrain-orchestration.md`
