---
name: es-release-notes-evidence
description: Produce evidence-linked ESFramework release notes and change summaries without promoting unverified claims, transient logs, or AI summaries into release facts.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# ES Release Notes Evidence

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and capability visibility never grants AI-initiated authority. The current explicit user request authorizes its bounded action under `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`; AIBrain, AICommand and TaskContract are protocol inputs only when their managed channel is selected. Reject inferred expansion, not user-directed paths.

将变更、证据、已知风险和未完成责任整理成可审计发布说明。

## Workflow

1. 读取 branch/HEAD/worktree、AIWarnings、AICommand、receipts、测试/构建/Profiler/Player/发布产物。
2. 区分 changed/fixed/verified/blocked/unverified，给每项绑定 hash、平台、命令、时间和 owner。
3. 检查版本兼容、迁移、回滚、已知问题和支持范围；拒绝无证据的“完成/商业级”措辞。
4. 输出草稿和 evidence matrix；发布动作必须由 release owner 和独立合同执行。

## Responsibility-specific static acceptance

- Profile: `release`
- Custom checks: `evidence-contract, runtime-escalation, compatibility-boundary, deterministic-replay`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- 只读生成草稿；不发布、不上传、不改版本或删除历史。
- 记录范围、工件、保留期、证据层级和未验证项；重跑需重新绑定 HEAD/PlanHash。
- 覆盖正向、缺证据、拒绝扩权、重复生成和中断恢复。

## Resources

- `references/release-note-contract.md`
- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）`
