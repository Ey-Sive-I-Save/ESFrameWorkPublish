---
name: es-aiwarning-authoring
description: Author, reconcile, validate, and retire ESFramework AIWarnings without copying summaries, losing history, or weakening P0 and evidence boundaries.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# ES AIWarnings Authoring

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and capability visibility never grants AI-initiated authority. The current explicit user request authorizes its bounded action under `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`; AIBrain, AICommand and TaskContract are protocol inputs only when their managed channel is selected. Reject inferred expansion, not user-directed paths.

维护长期规则、路由、当前状态和历史边界；AIWarnings 不替代源码或运行证据。

## Workflow

1. 读取 Start/CurrentStatus/RuleIndex、目标 P0/领域规则、现有 inbound links 和工作树。
2. 判定 keep/normalize/merge/split/archive/deprecate/defer，先建立 preservation ledger。
3. 为现行规则定义 id、authority、routeKeys、applicability、evidenceRef、owner 和 staleWhen。
4. 更新人工索引与机器路由投影，运行结构/链接/UTF-8 检查；历史不得覆盖当前事实。

## Specialized static acceptance

- Guidance: `references/static-specialized-acceptance.md`
- Acceptance ID: `aiwarning-route-governance`
- Required cases: `route-identity, p0-priority, rule-index-closure, duplicate-route, archive-transition`
- Static assertions: AIWarnings; P0; RuleIndex; route identity; archive
- This contract is responsibility-specific and remains distinct from Runtime proof.

## Responsibility-specific static acceptance

- Profile: `governance`
- Custom checks: `authority-routing, permission-boundary, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- 不把源码、构建日志、临时错误、外部摘要或 AIKnowledge 当作 P0 权威。
- 不删除、静默移动或覆盖已有交接/状态；冲突保留并标记 Blocked/Deferred。
- 覆盖坏链接、重复 authority、过长 CurrentStatus、编码错误和中断恢复。

## Resources

- `references/aiwarning-contract.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`


## Specialized static acceptance

Acceptance ID: `aiwarning-route-governance`

Responsibility-specific static assertions (these are source-level requirements, not Runtime claims):
- AIWarnings
- P0
- RuleIndex
- route identity
- archive

Required specialized cases: `route-identity, p0-priority, rule-index-closure, duplicate-route, archive-transition`
Guidance: `references/static-specialized-acceptance.md`
