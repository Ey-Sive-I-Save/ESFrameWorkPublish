---
name: es-knowledge-validator
description: Validate ESFramework AIKnowledge entries and KnowledgeIndex bindings without modifying them. Use when checking knowledge freshness, SourceRef or ContentHash drift, duplicate KnowledgeId values, route and required-read closure, evidence-level boundaries, stale entries, deciding whether generated knowledge can be accepted, or evaluating whether Knowledge materially improves AI decisions through a consent-gated three-condition comparison.
---

## Verification boundary

- **Static**: AIKnowledge text, index bindings, source paths, hashes, route closure, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, serialization, Player, or release behavior.
- `runtime-not-run` is not a static failure. It means this validator cannot prove a runtime claim.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`.

# ES Knowledge Validator

## Engineering controls

This is a read-only validation Skill. Its deterministic validator may inspect declared Knowledge files and hashes only within the project root. It may not write entries, change Catalog or routes, access credentials, use the network, launch Unity, or treat a model claim as evidence. A current explicit user request may authorize the separate maintenance helper described below; AIBrain plans and TaskContracts are required only when that managed channel is selected.

The optional effectiveness comparison is an advisory workflow, not a new mode of `Invoke-ESKnowledgeValidation.ps1`. It may propose isolated AI evaluations and an external-authority condition, but it must pause for the user's explicit approval before starting new contexts, using another model, or accessing the network. Its qualitative results never replace the deterministic static result.

Validate knowledge independently from the workflow that created it. Treat AIKnowledge as a derived navigation layer: a green result proves only that the selected static contracts close against current files.

## Skill 使用披露

遵循项目根 `AGENTS.md` 和 `.agents/README.md` 的“Skill 使用披露”规范。实际使用本 Skill 时，首次用户可见的进度更新必须说明该 Skill 与任务的关系；最终答复必须列出本轮实际影响工作的 Skill 与作用。不要列出仅可用、未使用的 Skill，也不得把披露视为授权、执行或验收证据。

## Responsibility boundary

- `$es-knowledge-creator` produces or updates bounded candidate knowledge.
- `$es-ai-knowledge-curation` maintains the discovery and curation workflow.
- This Skill's validation modes read and judge existing entries and index bindings. They do not repair, rewrite, register, delete, or promote them.
- `Export-ESKnowledgeRefreshPlan.ps1` and `Invoke-ESKnowledgeStableRefresh.ps1` are co-located maintenance helpers, not validator modes. Export is read-only; explicit `-Apply` is a separately authorized write transaction and must be followed by the independent validator.
- A validation report is evidence, not permission. Fixes require the user's separate write authorization and the applicable Knowledge workflow.

## Validation modes

Use `scripts/Invoke-ESKnowledgeValidation.ps1` with one of these modes:

- `Entry`: validate one project-relative Markdown entry and its unique `KnowledgeIndex.yaml` binding.
- `Index`: validate index structure, unique IDs, paths, route lists, required reads, related Skills, and entry ContentHash bindings.
- `All`: run `Index`, then validate every uniquely indexed entry. This is a bounded repository-local static scan, not a Runtime audit.

Use the advisory `EffectivenessComparison` workflow when the question is whether a Knowledge entry actually reduces AI mistakes, improves decisions, or adds value beyond general model knowledge. Read [the three-condition evaluation protocol](references/three-condition-comparative-evaluation.md) before proposing or running it. Do not present it as a PowerShell mode.

Read [the validation contract](references/knowledge-validation-contract.md) before interpreting blockers. Use [the evidence receipt contract](references/evidence-receipt-contract.md) only when a formal receipt is supplied.

## Proactive effectiveness proposal

Propose the three-condition comparison only when at least one of these is true:

- the user asks whether a Knowledge entry genuinely lowers error rate or changes the next action;
- a version-sensitive API, lifecycle, permission, recovery, or evidence claim needs to be distinguished from generic model advice;
- static closure passes but practical decision value remains uncertain.

Do not interrupt routine SourceRef, ContentHash, index, UTF-8, duplicate-ID, or route-closure validation with this proposal.

Use this user-facing proposal, adapted only for the target entry and scenario:

```text
我建议对这条 Knowledge 做一次真正的三情况对比：
A. 隔离的通用 AI，只给任务场景；
B. 同模型的新隔离上下文，再加入目标 Knowledge 与 requiredReads；
C. 同模型的新隔离上下文，在 B 的基础上加入当前版本的外部权威资料。
三组使用同一任务、输出结构和评分标准，互不读取前组答案。C 需要你明确同意联网范围；创建新会话、调用外部模型或写入实验工件也分别需要你明确同意。要现在执行吗？
```

After proposing, stop the comparison until the user answers. A request to validate Knowledge does not by itself authorize network access, another model, a new session, or persisted experiment artifacts.

For a true comparison, require three fresh contexts, the same model/version and task prompt, no answer leakage, and a scoring rubric fixed before outputs are viewed. If isolation is unavailable or the current model has already read the target Knowledge, offer a `single-model staged comparison` and state that it is counterfactual, not blind or independent.

Compare decision quality rather than prose volume. At minimum score prerequisite discovery, API or mechanism correctness, stop conditions, failure recovery, idempotency, authority boundaries, evidence honesty, unsupported claims, and actionable next steps. Attribute each improvement or regression to the added evidence condition; retain `runtime-not-run` unless authoritative Runtime evidence was actually supplied.

## Required checks

For an entry:

1. Read strict UTF-8 and require `KnowledgeId`, `Authority`, `RouteKeys`, `ContentHash`, `SourceRefs`, `EvidenceLevel`, and `StaleWhen`.
2. Reject placeholders, rooted paths, traversal, reparse points, missing sources, duplicate SourceRefs, and non-lowercase SHA-256 values.
3. Rehash every SourceRef and recompute ContentHash from the ordinal-sorted source hashes.
4. Require exactly one index binding with the same file, KnowledgeId, ContentHash, and at least one overlapping route key.
5. Keep unsupported Runtime statements as non-claims unless authoritative Runtime evidence is separately bound.

For the index:

1. Require a single `entries` collection and unique `knowledgeId` values.
2. Require exactly one value for each entry field defined by the contract.
3. Resolve each entry file and each `requiredReads` path inside the project root.
4. Resolve every `relatedSkills` item to a direct `.agents/skills/<name>` directory containing both `SKILL.md` and `agents/openai.yaml`.
5. Compare the index ContentHash to the selected entry's declared ContentHash.

## Decision model

- `passed`: every requested static check passed.
- `blocked`: at least one structural, source, hash, route, path, or evidence-boundary contract failed.
- `runtime-not-run`: always reported separately; this validator never starts Unity or external systems.

Sort findings by code and path so repeated runs over unchanged inputs are deterministic. Do not hide later findings after the first failure.

## Failure and recovery

- On changing input, discard the prior result and rerun; source or index hash drift invalidates the old result.
- On malformed UTF-8 or YAML-like structure, return a blocker without trying to rewrite the file.
- On a concurrent change, rerun from current files. Do not merge or repair during validation.
- If `ReportPath` is supplied, require a project-relative path below `ES/Output`; otherwise remain fully read-only.

## Static acceptance

- Responsibility profile: `knowledge`.
- Specialized acceptance: `knowledge-validation-integrity`.
- Required cases: source-hash-valid, source-hash-drift, content-hash-mismatch, duplicate-id, denied-path-expansion, deterministic-repeat.
- Runtime boundary: static closure does not prove Unity, Player, Profiler, IL2CPP, network, or release behavior.

## Resources

- `references/knowledge-validation-contract.md`: fields, decisions, and finding codes.
- `references/static-specialized-acceptance.md`: responsibility-specific replay cases.
- `references/three-condition-comparative-evaluation.md`: consent, isolation, fairness, scoring, and reporting contract for optional Knowledge effectiveness evaluation.
- `scripts/Invoke-ESKnowledgeValidation.ps1`: read-only validator.
- `scripts/Export-ESKnowledgeRefreshPlan.ps1`: read-only schema v3 SourceRef drift plan; it records complete `entrySnapshots`, the expected Entry and Index hash projections, the refresh algorithm version, the full declared source sets, findings, and `indexHash` in `planHash`. It exits blocked for malformed, missing, duplicate, unsafe, or unresolved SourceRefs.
- `scripts/Invoke-ESKnowledgeStableRefresh.ps1`: explicit stable-only maintenance helper; preview is default, it rejects older plan schemas, recomputes every plan-bound result projection, and validates complete Entry/Index/SourceRef CAS state. `-Apply` uses a fixed per-project cooperative cross-process lock, repeats final CAS while holding the lock, and performs verified per-file replacement with reverse-order rollback for caught failures. Only a receipt that actually entered that lock transaction declares `transactionExecuted=true`, `atomicBatch=true`, and `transactionMode=locked-exception-rollback`; Preview, WhatIf, and no-change paths declare that no transaction ran. `crashSafe=false` always excludes non-cooperating writers, reader snapshot consistency, process termination, machine failure, and power loss.
- `.agents/tests/Test-ESKnowledgeValidatorRegression.ps1`: deterministic positive and negative fixtures; test-only, outside the executable Skill surface.
- `scripts/Test-ESSkillEvidence.ps1`: strict receipt validator delegate.
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`: discovery authority.
- `Documentation/AIKnowledge/KnowledgeIndex.yaml`: knowledge binding index.
