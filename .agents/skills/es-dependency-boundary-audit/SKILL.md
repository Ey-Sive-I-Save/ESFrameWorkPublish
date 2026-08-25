---
name: es-dependency-boundary-audit
description: Audit ESFramework assembly, package, editor/runtime, resource, and reverse-reference boundaries. Use before introducing dependencies, moving code, or diagnosing circular or forbidden references.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# ES Dependency Boundary Audit

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and capability visibility never grants AI-initiated authority. The current explicit user request authorizes its bounded action under `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`; AIBrain, AICommand and TaskContract are protocol inputs only when their managed channel is selected. Reject inferred expansion, not user-directed paths.

证明依赖方向、宿主边界和迁移影响，不把生成的 csproj 当作唯一事实。

## Workflow

1. 收集 asmdef、`.csproj`、Packages、Editor/Runtime 路径和实际源码引用；记录生成物与源码的区别。
2. 建立 dependency graph，标出 allowed/forbidden/reverse/optional edges、所有者和证据。
3. 检查序列化、反射、资源寻址、MCP/Worker 和构建平台的隐式边界。
4. 输出最小修复或迁移建议；当前用户要求实施时可在同一范围直接变更，并在目标平台重验。选用受管通道时才走其 AICommand。

## Responsibility-specific static acceptance

- Profile: `engineering`
- Custom checks: `input-boundary, recovery-cache, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- 只读；不修改 asmdef、csproj、Packages 或资源。
- 记录图规模、缓存/增量策略、并发读取限制、版本漂移和恢复方案。
- 覆盖缺失路径、循环边、拒绝反向依赖、重复审计和中断扫描。

## Resources

- `references/dependency-boundary-contract.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）`
