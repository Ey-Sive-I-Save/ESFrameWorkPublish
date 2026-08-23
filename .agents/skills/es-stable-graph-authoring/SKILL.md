---
name: es-stable-graph-authoring
description: Author and validate Stable Graph V2 assets, identities, edges, snapshots, Agent artifacts, and execution workflows without restoring Legacy GraphView or mutable runners.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# ES Stable Graph Authoring

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and deny-by-default; capability visibility never grants permission. Use AIBrain `planTask`, the matching AICommand, and the current TaskContract before any write or external operation.

维护 Stable Graph V2 的稳定身份、Undo、迁移、烘焙和消费者专属产物。

## Workflow

1. 读取 Graph P0、ESAutomation/TaskContract、目标 consumer 和现有 asset；确认 Legacy 路径禁止恢复。
2. 定义 graphId/version/nodeId/edge.order、输入输出、Branch/FanOut/Join、snapshot 和 migration。
3. 通过 Editor 白名单与 Undo/dry-run 修改；候选 Agent 产物只能进入隔离 Candidates。
4. 运行结构、迁移、失败恢复和性能门禁；未执行真实闭环时保持 Verifying。
   使用 [Stable Graph V2 验证器](scripts/Test-ESStableGraphPacket.ps1) 检查稳定节点、边序、消费者快照，并拒绝 Legacy GraphView/NodeRunner。

## Specialized static acceptance

- Guidance: `references/static-specialized-acceptance.md`
- Acceptance ID: `stable-graph-contract`
- Required cases: `graph-identity, node-id-stability, edge-closure, duplicate-node-rejection, packet-hash`
- Static assertions: stable graph; node ID; edge; duplicate node; packet hash
- This contract is responsibility-specific and remains distinct from Runtime proof.

## Responsibility-specific static acceptance

- Profile: `authoring`
- Custom checks: `change-boundary, resource-projection, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- 不直接写 Assets、不绕过 AICommand/Facade、不把图存在宣称为执行成功。
- 记录节点/边规模、烘焙时间、内存、并发、取消、回滚和 consumer 兼容性。
- 覆盖 malformed graph、重复 identity、非法 edge、拒绝 Legacy、部分失败和恢复。

## Resources

- `references/graph-contract.md`
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）`
- `scripts/Test-ESStableGraphPacket.ps1`


## Specialized static acceptance

Acceptance ID: `stable-graph-contract`

Responsibility-specific static assertions (these are source-level requirements, not Runtime claims):
- stable graph
- node ID
- edge
- duplicate node
- packet hash

Required specialized cases: `graph-identity, node-id-stability, edge-closure, duplicate-node-rejection, packet-hash`
Guidance: `references/static-specialized-acceptance.md`
