---
name: es-gamecore-config-authoring
description: Author and validate ESFramework GameCore root SO, RuntimeData, ConfigKey, global indexes, stable identities, and transaction/reinjection boundaries.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# ES GameCore Config Authoring

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and capability visibility never grants AI-initiated authority. The current explicit user request authorizes its bounded action under `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`; AIBrain, AICommand and TaskContract are protocol inputs only when their managed channel is selected. Reject inferred expansion, not user-directed paths.

维护 GameCore 内容层唯一入口和稳定配置身份，不让运行时反向依赖内容对象。

## Workflow

1. 读取 GameCore/Identity P0、现有 root SO、RuntimeData、ConfigKey、Catalog 和 consumer。
2. 定义 stable id、owner、serialization、initialization、RuntimeData reinjection、transaction 和 rollback。
3. 用 Editor/事务白名单和 dry-run 生成差异；禁止手改生成资产或跨层引用。
4. 运行结构、序列化、重注入、重复提交和目标运行证据；缺 Unity/运行证据不得 Accepted。

## Responsibility-specific static acceptance

- Profile: `authoring`
- Custom checks: `change-boundary, resource-projection, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- Skill 自主运行只计划/验证且不得扩大范围；当前用户明确要求的正式资产写入不以 AICommand 为前置条件。
- 记录条目数、索引重建成本、并发/重入、兼容迁移和恢复。
- 覆盖缺 root、重复 Key、反向引用、坏序列化、重复事务和中断恢复。
- 使用 [稳定身份 Manifest 验证器](scripts/Test-ESStableIdentityManifest.ps1) 检查 Scope、稳定序列化值、SchemaHash、确定性顺序和冲突拒绝；任何持久化 `RuntimeKey`/`RuntimeId` 都必须阻断。

## Resources

- `references/gamecore-config-contract.md`
- `scripts/Test-ESStableIdentityManifest.ps1`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）`
