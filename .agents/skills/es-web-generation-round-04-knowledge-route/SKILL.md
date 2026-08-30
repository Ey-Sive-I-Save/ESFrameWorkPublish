---
name: es-web-generation-round-04-knowledge-route
description: Route a confirmed Round 03 TaskContext to the smallest set of current AIKnowledge entries, verify SourceRefs and ContentHash bindings, and emit a stale-aware Knowledge route receipt before design or generation.
---

# ES Web Generation Round 04 — Knowledge Route

## Purpose

Round 04 selects only the Knowledge entries required by the frozen TaskContext and proves their static source closure. It does not rewrite Knowledge, repair stale entries, create design, invoke SubAgents/ABCD, generate HTML, or run Runtime/network/Unity.

## SmallTool controls

- Read the accepted Round 03 receipt, `Documentation/AIKnowledge/KnowledgeIndex.yaml`, selected entries, and required SourceRefs only.
- Invoke the read-only Knowledge validator; never silently refresh hashes or substitute an adjacent entry.
- Write only the explicit route receipt. SourceRefs/ContentHash freshness findings produce `partial` with the project-relative path and parsed usable content; unsafe paths, missing entries, duplicate identity, or unparseable structure still block.

## Required reads

Read project `AGENTS.md`, `ES/AISpace/README.md`, the Round 03 TaskContext receipt, `es-ai-knowledge-curation`, `es-knowledge-validator`, and [`references/round-04-knowledge-route-contract.json`](references/round-04-knowledge-route-contract.json). Read `Documentation/AIKnowledge/AIBRAIN_ENTRY.md` and the index before selecting entries.

## Workflow

Knowledge 路由必须消费当前 AI 证据（`AiEvidencePath`），并验证 `taskContextHash`、`sourceScopeHash` 与 `entryPaths`；不能使用其他任务或旧轮次的路由分析替代。

1. Verify the TaskContext receipt is accepted and its task identity/hash fields are present.
2. Select a bounded list of Knowledge entry paths/IDs from the index based on the frozen task focus; do not load the whole library.
3. Run `Invoke-ESKnowledgeValidation.ps1 -Mode Entry` for each selected entry and preserve every finding.
4. Accept the route only when every entry has one index binding, valid SourceRefs, current ContentHash, overlapping RouteKeys, and closed requiredReads. If only freshness drifts remain, return `partial` and do not treat the content as authoritative.
5. Emit a `KnowledgeRouteReceipt` with selected IDs, entry hashes, validator evidence, stale status, AI analysis, and non-claims.
6. Stop. Round 05 may begin only after this receipt is read; no design or generation is chained automatically.

## Hard controls

- Knowledge is navigation evidence, not authority or user acceptance.
- `stale`, `ContentHash mismatch`, missing SourceRef, duplicate ID, and unsafe path are object-level blockers for the selected route only.
- A passing static route does not prove runtime, visual, network, Unity, or release behavior.
- Never modify KnowledgeIndex or entries in this round; use the separate curation workflow for authorized repair.

## Engineering controls

- Minimal route and bounded batch are mandatory; unrelated entries cannot become blockers.
- SourceRef and ContentHash are re-read at validation time and recorded in the receipt.
- Repeat runs over unchanged files are deterministic; changed source invalidates the prior receipt.
- Preserve compatibility with the existing Knowledge validator and index schema.

## Return contract

Return `recordType=KnowledgeRouteReceipt`, `roundId`, `stageId`, `status`, `taskId`, `taskRevision`, `contextVersion`, `taskContextHash`, `selectedEntries`, `entryValidationReceipts`, `routeHash`, `aiAnalysis`, `execution`, `decision`, `returnReceipt`, and `nonClaims`.

## Expected use

Round 04 prevents generic or stale Knowledge from silently steering later design. Round 05 consumes this receipt to produce an evidence-bound capability/design brief.

## Skill 使用披露

遵循项目根 `AGENTS.md` 和 `.agents/README.md` 的 Skill 使用披露规范。使用本 Skill 不授予 Runtime、网络、Unity、Git、删除或发布权限。
