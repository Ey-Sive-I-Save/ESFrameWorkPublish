# Knowledge validation integrity

## Purpose

This responsibility-specific static acceptance plan supplements the seven common StaticDeepReplay cases for `es-knowledge-validator`.

## Static scope

- Profile: `knowledge`
- Acceptance id: `knowledge-validation-integrity`
- Runtime boundary: Source, hash, index and route closure are static; Runtime claims remain unproven.
- Static assertions: strict UTF-8, contained paths, current SourceRef hashes, recomputed SourceSetHash and EntryBodyHash, unique KnowledgeId, exact canonical route sets, explicit shared projections, read/Skill closure, deterministic findings.

## Required specialized cases

- `source-hash-valid`: accept a contained entry whose declared source and content hashes are current.
- `source-hash-drift`: block after a SourceRef changes without an entry refresh.
- `content-hash-mismatch`: block a declared ContentHash that does not match the sorted source hashes.
- `entry-body-hash-mismatch`: block body-only edits, including removal of stop or evidence-boundary content, while SourceRefs remain unchanged.
- `route-set-mismatch`: block missing or extra canonical routeKeys in either direction while accepting order-only differences.
- `shared-route-projection`: accept only explicit per-binding route projections and reject undeclared differences.
- `duplicate-id`: block non-unique KnowledgeId index identities.
- `denied-path-expansion`: block rooted, traversing, or out-of-scope paths.
- `deterministic-repeat`: return the same result over unchanged inputs.
- `stable-refresh-boundary`: preview SourceRef refreshes by default, classify unstable sources as wait-for-source-stability, bind current and expected Entry/Index projections plus the algorithm version with a SHA-256 `planHash`, reject sources changed between plan and apply, and allow `-Apply` to update only stable Entry/Index evidence under a fixed cooperative writer lock with per-file replacement and verified caught-exception rollback. This case must assert that Preview, WhatIf, and no-change receipts use `transactionExecuted=false` and `atomicBatch=false`; only an entered Apply transaction may use `transactionExecuted=true`, `atomicBatch=true`, and `transactionMode=locked-exception-rollback`. Every path keeps `crashSafe=false`; none proves crash-safe multi-file atomicity or reader snapshot isolation.

## Advisory effectiveness protocol checks

The optional three-condition comparison is covered by the existing `normal-input` and `denied-expansion` cases:

- `references/three-condition-comparative-evaluation.md` must exist and remain directly discoverable from `SKILL.md`.
- Routine structural validation must not automatically propose or run the comparison.
- A proposal must pause before new contexts, another model, network access, or artifact writes unless the user explicitly authorizes each requested side effect.
- A true comparison requires three fresh contexts with the same model/version, task, output schema, and fixed rubric; leaked or previously read Knowledge forces a `single-model-staged` or `counterfactual` label.
- Comparative scores remain advisory and cannot override SourceRef, ContentHash, index, route, permission, or Runtime blockers.

## Evidence artifacts

- `SKILL.md`
- `references/knowledge-validation-contract.md`
- `references/three-condition-comparative-evaluation.md`
- `scripts/Invoke-ESKnowledgeValidation.ps1`
- `.agents/tests/Test-ESKnowledgeValidatorRegression.ps1`
- `scripts/Test-ESSkillEvidence.ps1`
- `scripts/Export-ESKnowledgeRefreshPlan.ps1`
- `scripts/Invoke-ESKnowledgeStableRefresh.ps1`

All specialized cases must be statically accounted for. Missing Runtime evidence is reported separately and does not erase a completed static result.
