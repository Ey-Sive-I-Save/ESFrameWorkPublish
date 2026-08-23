---
name: es-api-contract-review
description: Review public C#, editor, runtime, JSON, YAML, MCP, and automation APIs for stable identity, ownership, compatibility, and evidence. Use before adding or changing a public contract.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# ES API Contract Review

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and deny-by-default; capability visibility never grants permission. Use AIBrain `planTask`, the matching AICommand, and the current TaskContract before any write or external operation.

把 API 变更转成稳定身份、输入输出、生命周期、权限和兼容性证据。

## Workflow

1. 定位现有 API、消费者、序列化/反射入口、AICommand 和对应 P0；禁止从命名推断权威。
2. 建立 contract matrix：身份、输入约束、输出、错误、生命周期、线程/主线程、权限、版本和消费者。
3. 检查迁移策略、旧数据、默认值、兼容窗口、回滚和测试 fixture。
4. 输出 review，区分源码缺陷、设计风险和缺失证据；不直接改代码。

## Responsibility-specific static acceptance

- Profile: `engineering`
- Custom checks: `input-boundary, recovery-cache, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- 只读审查；任何修改必须由匹配 AICommand 单独授权。
- 必须有 owner、acceptance owner、risk register、boundary matrix、compatibility evidence 和 replay。
- 覆盖正向、非法输入、拒绝扩权、重复调用和中断/版本漂移案例。

## Resources

- `references/api-contract-matrix.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）`
