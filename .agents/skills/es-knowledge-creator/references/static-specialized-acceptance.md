# Knowledge source references and bounded output

## Purpose

This is a responsibility-specific static acceptance plan for **es-knowledge-creator**. It supplements the seven common StaticDeepReplay cases; it is not interchangeable with another Skill's plan.

## Static scope

- Profile: `knowledge`
- Acceptance id: `knowledge-output-governance`
- Runtime boundary: Runtime is not required for source/hash/output governance; runtime claims remain non-claims.
- Static assertions: SourceRef; ContentHash; bounded output; stale; unsupported runtime claims

## Required specialized cases

- `source-ref-hash`: replay the source-ref-hash contract from source/configuration and record pass or blocked evidence.
- `content-hash-recompute`: replay the content-hash-recompute contract from source/configuration and record pass or blocked evidence.
- `bounded-output`: replay the bounded-output contract from source/configuration and record pass or blocked evidence.
- `stale-entry-detection`: replay the stale-entry-detection contract from source/configuration and record pass or blocked evidence.
- `unsupported-claim-rejection`: replay the unsupported-claim-rejection contract from source/configuration and record pass or blocked evidence.

## Evidence artifacts

- `SKILL.md`
- `references/knowledge-entry-contract.md`
- `references/output-policy.md`
- `scripts/Test-ESKnowledgeEntry.ps1`
- `scripts/Test-ESSkillEvidence.ps1`

## Acceptance rule

All listed specialized cases and source assertions must be statically accounted for. Missing or stale evidence is `static-blocked`; `runtime-not-run` is reported separately and does not erase a completed static result.
