---
name: es-knowledge-validator
description: Validate ESFramework AIKnowledge entries and KnowledgeIndex bindings without modifying them. Use when checking knowledge freshness, SourceRef or ContentHash drift, duplicate KnowledgeId values, route and required-read closure, evidence-level boundaries, stale entries, or deciding whether generated knowledge can be accepted.
---

## Verification boundary

- **Static**: AIKnowledge text, index bindings, source paths, hashes, route closure, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, serialization, Player, or release behavior.
- `runtime-not-run` is not a static failure. It means this validator cannot prove a runtime claim.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`.

# ES Knowledge Validator

## Engineering controls

This is a read-only, plan-authorized validation Skill. It may inspect declared Knowledge files and hashes only within the project root. It may not write entries, change Catalog or routes, access credentials, use the network, launch Unity, or treat a model claim as evidence. Any repair or registration is a separate governed command.

Validate knowledge independently from the workflow that created it. Treat AIKnowledge as a derived navigation layer: a green result proves only that the selected static contracts close against current files.

## Skill 使用披露

遵循项目根 `AGENTS.md` 和 `.agents/README.md` 的“Skill 使用披露”规范。实际使用本 Skill 时，首次用户可见的进度更新必须说明该 Skill 与任务的关系；最终答复必须列出本轮实际影响工作的 Skill 与作用。不要列出仅可用、未使用的 Skill，也不得把披露视为授权、执行或验收证据。

## Responsibility boundary

- `$es-knowledge-creator` produces or updates bounded candidate knowledge.
- `$es-ai-knowledge-curation` maintains the discovery and curation workflow.
- This Skill reads and judges existing entries and index bindings. It does not repair, rewrite, register, delete, or promote them.
- A validation report is evidence, not permission. Fixes require the user's separate write authorization and the applicable Knowledge workflow.

## Validation modes

Use `scripts/Invoke-ESKnowledgeValidation.ps1` with one of these modes:

- `Entry`: validate one project-relative Markdown entry and its unique `KnowledgeIndex.yaml` binding.
- `Index`: validate index structure, unique IDs, paths, route lists, required reads, related Skills, and entry ContentHash bindings.
- `All`: run `Index`, then validate every uniquely indexed entry. This is a bounded repository-local static scan, not a Runtime audit.

Read [the validation contract](references/knowledge-validation-contract.md) before interpreting blockers. Use [the evidence receipt contract](references/evidence-receipt-contract.md) only when a formal receipt is supplied.

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
- `scripts/Invoke-ESKnowledgeValidation.ps1`: read-only validator.
- `scripts/Export-ESKnowledgeRefreshPlan.ps1`: read-only SourceRef drift plan; it never edits source or knowledge files.
- `scripts/Invoke-ESKnowledgeStableRefresh.ps1`: explicit stable-only refresh helper; preview is default, plans carry a SHA-256 `planHash`, and `-Apply` refuses sources changed after planning before updating SourceRefs plus the matching ContentHash/index binding atomically.
- `.agents/tests/Test-ESKnowledgeValidatorRegression.ps1`: deterministic positive and negative fixtures; test-only, outside the executable Skill surface.
- `scripts/Test-ESSkillEvidence.ps1`: strict receipt validator delegate.
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`: discovery authority.
- `Documentation/AIKnowledge/KnowledgeIndex.yaml`: knowledge binding index.
