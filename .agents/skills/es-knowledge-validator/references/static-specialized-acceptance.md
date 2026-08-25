# Knowledge validation integrity

## Purpose

This responsibility-specific static acceptance plan supplements the seven common StaticDeepReplay cases for `es-knowledge-validator`.

## Static scope

- Profile: `knowledge`
- Acceptance id: `knowledge-validation-integrity`
- Runtime boundary: Source, hash, index and route closure are static; Runtime claims remain unproven.
- Static assertions: strict UTF-8, contained paths, current SourceRef hashes, recomputed ContentHash, unique KnowledgeId, route/read/Skill closure, deterministic findings.

## Required specialized cases

- `source-hash-valid`: accept a contained entry whose declared source and content hashes are current.
- `source-hash-drift`: block after a SourceRef changes without an entry refresh.
- `content-hash-mismatch`: block a declared ContentHash that does not match the sorted source hashes.
- `duplicate-id`: block non-unique KnowledgeId index identities.
- `denied-path-expansion`: block rooted, traversing, or out-of-scope paths.
- `deterministic-repeat`: return the same result over unchanged inputs.
- `stable-refresh-boundary`: preview SourceRef refreshes by default, classify unstable sources as wait-for-source-stability, bind the plan with a SHA-256 `planHash`, reject sources changed between plan and apply, and allow `-Apply` to update only stable entry/index evidence with atomic writes.

## Evidence artifacts

- `SKILL.md`
- `references/knowledge-validation-contract.md`
- `scripts/Invoke-ESKnowledgeValidation.ps1`
- `.agents/tests/Test-ESKnowledgeValidatorRegression.ps1`
- `scripts/Test-ESSkillEvidence.ps1`
- `scripts/Export-ESKnowledgeRefreshPlan.ps1`
- `scripts/Invoke-ESKnowledgeStableRefresh.ps1`

All specialized cases must be statically accounted for. Missing Runtime evidence is reported separately and does not erase a completed static result.
