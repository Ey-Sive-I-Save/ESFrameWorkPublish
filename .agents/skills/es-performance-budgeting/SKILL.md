---
name: es-performance-budgeting
description: Define measurable ESFramework runtime, editor, memory, allocation, asset, and build performance budgets. Use before claiming low GC, 0 GC, fast reload, or release readiness.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# ES Performance Budgeting

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and capability visibility never grants AI-initiated authority. The current explicit user request authorizes its bounded action under `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`; AIBrain, AICommand and TaskContract are protocol inputs only when their managed channel is selected. Reject inferred expansion, not user-directed paths.

把性能目标拆成场景、平台、预算、测量、阈值和回归动作；不从源码直觉推导性能结论。

## Workflow

1. 读取命中 P0、目标平台、热路径/编辑器生命周期和现有 Profiler 入口。
2. 建立 budget matrix：metric、scope、first-run、steady-state、peak、concurrency、threshold、tool、owner。
3. 先记录基线，再设计预热/批处理/回收/降级策略；无 Profiler 时只能输出设计或静态证据。
   使用 [性能预算验证器](scripts/Test-ESPerformanceBudget.ps1) 检查阈值、阶段、基线、输入规模、预热、证据产物和证据等级一致性。
4. 运行相同输入回放，比较阈值；失败时隔离回归并保留原始数据。

## Responsibility-specific static acceptance

- Profile: `engineering`
- Custom checks: `input-boundary, recovery-cache, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- 不修改代码或宣称商业级性能；实现必须另行授权。
- 必须区分编译、Unity、运行时、Profiler、Player/IL2CPP 和发布证据。
- 记录数据量、批次、首次/稳态成本、内存峰值、分配、并发和回滚。

## Resources

- `references/performance-budget-contract.md`
- `scripts/Test-ESPerformanceBudget.ps1`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）`
