---
name: es-entity-prefab-validation
description: Validate ESFramework Entity, player/weapon Prefab, DataInfo, parts, control, motion, pooling, and runtime ownership contracts before integration.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# ES Entity Prefab Validation

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and deny-by-default; capability visibility never grants permission. Use AIBrain `planTask`, the matching AICommand, and the current TaskContract before any write or external operation.

验证实体从定义、Prefab 装配到运行时消费和池化生命周期的闭环。

## Workflow

1. 读取 Entity/Prefab/Pool/实际可玩闭环 P0、目标 builder、DataInfo、parts、input/control 和 runtime consumer。
2. 建立层级、组件、stable identity、所有权、初始化、请求仲裁、运动、池化、资源和清理矩阵。
3. 用官方 builder、dry-run、Prefab override 审计和 fixture 验证；不手改正式模板绕过 builder。
4. 运行 EditMode/PlayMode/Profiler 需要的证据行，分开报告静态、实机、运行和发布结论。
   使用 [实体 Prefab 验证器](scripts/Test-ESEntityPrefabPacket.ps1) 检查稳定身份、DataInfo、Parts、Pool、ResourceScope 和运行证据边界。

## Responsibility-specific static acceptance

- Profile: `authoring`
- Custom checks: `change-boundary, resource-projection, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- 任何 Prefab/Scene 写入需独立 AICommand；Skill 默认只读。
- 记录实体数、层级深度、池容量、首次/稳态成本、并发、回滚和残留清理。
- 覆盖缺组件、坏 DataInfo、错误挂点、非法所有权、重复 builder 和中断恢复。

## Resources

- `references/entity-prefab-contract.md`
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Entity与世界（EntityWorld）`
- `scripts/Test-ESEntityPrefabPacket.ps1`
