---
name: es-migration-planning
description: Plan reversible ESFramework migrations across code, assets, AIWarnings, AICommands, Knowledge, Skills, packages, and release artifacts with ownership and compatibility evidence.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# ES Migration Planning

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and deny-by-default; capability visibility never grants permission. Use AIBrain `planTask`, the matching AICommand, and the current TaskContract before any write or external operation.

先保护旧事实，再设计可回滚、可分批、可验证的迁移。

## Workflow

1. 建立 source/target baseline、preservation ledger、inbound links 和 source-of-truth 决策。
2. 分类 adopt/adapt/rewrite/exclude/defer；定义 schema、identity、dry-run、批次、停止和回滚。
3. 生成差异报告和迁移序列；缺少 target evidence 或 owner 时保持 Blocked。
   使用 [迁移计划验证器](scripts/Test-ESMigrationPlan.ps1) 检查 preservation ledger、dry-run、批次、重试、回滚和兼容窗口。
4. 分批执行并记录 receipts，验证兼容窗口、旧入口 redirect、Knowledge/PlanHash stale 和发布恢复。

## Responsibility-specific static acceptance

- Profile: `engineering`
- Custom checks: `input-boundary, recovery-cache, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- 不自动删除、覆盖、发布或改变基础设施；正式写入必须匹配 AICommand。
- 风险登记覆盖数据丢失、链接漂移、权限扩大、部分失败、并发和版本兼容。
- 覆盖 dry-run、非法输入、拒绝越界、重复批次和中断恢复。

## Resources

- `references/migration-contract.md`
- `Documentation/AIKnowledge/authority-reconciliation.md`
- `scripts/Test-ESMigrationPlan.ps1`
