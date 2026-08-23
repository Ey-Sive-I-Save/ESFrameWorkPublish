---
name: es-ai-knowledge-curation
description: Curate ESFramework AIKnowledge from current source, AIWarnings, AICommands, Skills, tests, and evidence while preserving SourceRefs, hashes, authority, and staleWhen semantics.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# ES AIKnowledge Curation

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and deny-by-default; capability visibility never grants permission. Use AIBrain `planTask`, the matching AICommand, and the current TaskContract before any write or external operation.

把项目事实整合成可定向检索的 Knowledge 条目，不把摘要升级为权威事实。

## Workflow

1. 读取 AIWarnings Start 链、目标源码/测试/合同和 Resource Index；建立 source/target preservation ledger。
2. 为条目定义 KnowledgeId、RouteKeys、Authority、SourceRefs、ContentHash、RequiredReads、EvidenceLevel 和 StaleWhen。
3. 按功能区拆分条目，避免全项目摘要；校验所有路径、UTF-8、SourceRef SHA-256 和 relatedSkills。
4. 更新 KnowledgeIndex 与条目原子一致；任何 source hash 漂移都使旧计划 stale。

## Specialized static acceptance

- Guidance: `references/static-specialized-acceptance.md`
- Acceptance ID: `knowledge-curation`
- Required cases: `route-minimality, source-closure, stale-index, duplicate-read-prevention, bounded-batch`
- Static assertions: KnowledgeIndex; minimal route; stale; duplicate reads; bounded batch
- This contract is responsibility-specific and remains distinct from Runtime proof.

## Responsibility-specific static acceptance

- Profile: `knowledge`
- Custom checks: `knowledge-boundary, bounded-output, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- 默认只读分析；写 Knowledge/Index 需要明确授权，禁止修改源码事实来适配摘要。
- 保留旧条目与来源，禁止删除冲突事实；缺证据标记 Deferred/Blocked。
- 覆盖正向、缺 SourceRef、坏 hash、拒绝扩权、重复更新和中断恢复。

## Resources

- `references/knowledge-entry-contract.md`
- `Documentation/AIKnowledge/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`


## Specialized static acceptance

Acceptance ID: `knowledge-curation`

Responsibility-specific static assertions (these are source-level requirements, not Runtime claims):
- KnowledgeIndex
- minimal route
- stale
- duplicate reads
- bounded batch

Required specialized cases: `route-minimality, source-closure, stale-index, duplicate-read-prevention, bounded-batch`
Guidance: `references/static-specialized-acceptance.md`
