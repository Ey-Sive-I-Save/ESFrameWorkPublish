---
name: es-automation-worker-authoring
description: Design and validate ESFramework AutomationCenter workers, TaskContracts, ProcessRunner boundaries, RunRecords, cancellation, input, artifacts, and recovery.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# ES Automation Worker Authoring

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and capability visibility never grants AI-initiated authority. The current explicit user request authorizes its bounded action under `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`; AIBrain, AICommand and TaskContract are protocol inputs only when their managed channel is selected. Reject inferred expansion, not user-directed paths.

建立受管 Worker 的输入、权限、生命周期、证据和恢复合同；AIBrain 不是 ProcessRunner。

## Workflow

1. 读取 AutomationCenter/Facade/AIBrain contract、TaskContract schemas、worker registration 和 target command。
2. 定义 worker identity/version、allowed root、arguments、environment、timeout、concurrency、artifact、secret policy。
3. 设计 plan/run/cancel/submitInput、heartbeat、RunRecord、partial failure、cleanup 和 retry；禁止任意命令入口。
4. 用 malformed input、拒绝路径/参数、超时、取消、重复 invocation 和重启恢复 fixture 验证。
   使用 [Worker 合同验证器](scripts/Test-ESWorkerContractPacket.ps1) 检查 PlanHash、AllowedRoots、Secret、超时、取消和恢复门禁。

## Responsibility-specific static acceptance

- Profile: `authoring`
- Custom checks: `change-boundary, resource-projection, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- Worker 自主执行仍由 AIBrain PlanHash、AICommand、TaskContract 和 Facade 收口；这只约束 Worker 通道，不阻止当前用户授权下的直接 Assets 修改。
- 记录进程/网络/文件权限、数据脱敏、资源预算、并发和 release artifact ownership。
- 缺 capability、hash、TaskContract 或证据时 fail closed，不自动换 Worker。

## Resources

- `references/worker-contract.md`
- `Documentation/AIKnowledge/entries/aibrain-orchestration.md`
- `scripts/Test-ESWorkerContractPacket.ps1`
