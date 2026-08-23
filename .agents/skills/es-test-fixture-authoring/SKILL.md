---
name: es-test-fixture-authoring
description: Design deterministic ESFramework test fixtures, scenes, assets, and malformed cases with ownership and cleanup boundaries. Use when a test needs new data, a reproducible failure, or a safe acceptance fixture.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# ES Test Fixture Authoring

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and deny-by-default; capability visibility never grants permission. Use AIBrain `planTask`, the matching AICommand, and the current TaskContract before any write or external operation.

创建可重复、可清理、不会污染正式资产的测试夹具。

## Workflow

1. 读取目标测试合同、AIWarnings 场景/资源规则和现有 fixture builder；确认测试程序集与平台。
2. 先定义 fixture identity、来源、生命周期、写入目录、对象上限、清理和 rollback。
3. 优先使用官方 builder、临时目录和 dry-run；禁止直接改正式 Prefab/Scene/ScriptableObject。
4. 生成正向、非法输入、拒绝扩权、重复运行和中断恢复夹具，并保存可重读 manifest。
5. 运行目标测试后清理临时对象；清理失败必须阻断交付并报告残留路径。

## Responsibility-specific static acceptance

- Profile: `engineering`
- Custom checks: `input-boundary, recovery-cache, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- 夹具不是产品内容，不得进入发布链；正式资产变更必须另有 AICommand。
- 记录平台、Unity 版本、测试程序集、fixture hash、创建者和清理结果。
- 明确首次/稳态成本、数量上限、并发隔离、超时和失败恢复。

## Resources

- `references/fixture-contract.md`
- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）`
