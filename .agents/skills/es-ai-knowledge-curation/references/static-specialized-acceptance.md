# AIKnowledge curation and discovery

## Purpose

This is a responsibility-specific static acceptance plan for **es-ai-knowledge-curation**. It supplements the seven common StaticDeepReplay cases; it is not interchangeable with another Skill's plan.

## Static scope

- Profile: `knowledge`
- Acceptance id: `knowledge-curation`
- Runtime boundary: Runtime is not required to prove index routing or source closure.
- Static assertions: KnowledgeIndex; minimal route; stale; duplicate reads; bounded batch

## Required specialized cases

- `route-minimality`: replay the route-minimality contract from source/configuration and record pass or blocked evidence.
- `source-closure`: replay the source-closure contract from source/configuration and record pass or blocked evidence.
- `stale-index`: replay the stale-index contract from source/configuration and record pass or blocked evidence.
- `duplicate-read-prevention`: replay the duplicate-read-prevention contract from source/configuration and record pass or blocked evidence.
- `bounded-batch`: replay the bounded-batch contract from source/configuration and record pass or blocked evidence.

## Evidence artifacts

- `SKILL.md`
- `references/knowledge-entry-contract.md`
- `scripts/Test-ESAIKnowledgeDiscovery.ps1`
- `scripts/Build-ESAIWarningsInventory.ps1`
- `references/evidence-receipt-contract.md`

## Acceptance rule

All listed specialized cases and source assertions must be statically accounted for. Missing or stale evidence is `static-blocked`; `runtime-not-run` is reported separately and does not erase a completed static result.
