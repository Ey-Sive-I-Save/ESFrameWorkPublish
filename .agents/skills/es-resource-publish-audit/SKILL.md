---
name: es-resource-publish-audit
description: Audit ESFramework AssetPackage, ResourcePlan, Manifest, Provider, Scope, download, verification, and rollback evidence before resource release.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# ES Resource Publish Audit

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and capability visibility never grants AI-initiated authority. The current explicit user request authorizes its bounded action under `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`; AIBrain, AICommand and TaskContract are protocol inputs only when their managed channel is selected. Reject inferred expansion, not user-directed paths.

验证资源从定义到发布、下载、加载和回滚的完整链路，不把生成 Manifest 当成发布成功。

## Workflow

1. 读取资源 P0、AssetPackage、ResourcePlan、Manifest、Provider、Scope、目标平台和 release contract。
2. 建立资产身份/依赖/分类/产物/哈希/Provider/下载/加载/释放/回滚矩阵。
3. 运行只读预览、重复导出检测、未使用/缺失依赖和 artifact hash 检查。
4. 逐项记录 Unity/Player/Provider/下载/加载证据；缺行标记 Blocked。
   使用 [资源审计包验证器](scripts/Test-ESResourceAuditPacket.ps1) 检查 AssetId、产物哈希、Provider、Lease/Scope、回滚和发布证据。

## Responsibility-specific static acceptance

- Profile: `authoring`
- Custom checks: `change-boundary, resource-projection, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- 不写资源、不上传、不删除旧包；正式导出必须由当前用户明确点名并先完成 dry-run。只有选择 `ManagedAIBrain/Worker` 时才额外校验对应 AICommand 和 TaskContract，它们不是二次批准。
- 记录资产数、包大小、并发下载、缓存、首次/稳态、失败隔离和 rollback。
- 覆盖缺依赖、重复导出、错误 Scope、校验失败、重复审计和中断恢复。

## Resources

- `references/resource-audit-contract.md`
- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）`
- `scripts/Test-ESResourceAuditPacket.ps1`
