# AIBrain route and capability discovery

## Purpose

This is a responsibility-specific static acceptance plan for **es-aibrain-route-authoring**. It supplements the seven common StaticDeepReplay cases; it is not interchangeable with another Skill's plan.

## Static scope

- Profile: `governance`
- Acceptance id: `aibrain-route-contract`
- Runtime boundary: Only Runtime can prove listCapabilities behavior; static checks prove route declarations and closure.
- Static assertions: AIBRAIN_ENTRY; routeKeys; KnowledgeIndex; collision; stale; RoutePlan; GoalRevision; depthReasonCode; executionEnabled

## Required specialized cases

- `route-key-overlap`: replay the route-key-overlap contract from source/configuration and record pass or blocked evidence.
- `skill-discovery`: replay the skill-discovery contract from source/configuration and record pass or blocked evidence.
- `knowledge-binding`: replay the knowledge-binding contract from source/configuration and record pass or blocked evidence.
- `route-collision`: replay the route-collision contract from source/configuration and record pass or blocked evidence.
- `stale-route-hash`: replay a real RoutePlan artifact and reject forged routePlan/source hashes, missing Goal/Registry SourceRefs, Git/artifact drift, unregistered stages, Profile/routeKey mismatch, and unauthorized depth while keeping failures scoped to that RoutePlan.

## Evidence artifacts

- `SKILL.md`
- `references/route-contract.md`
- `scripts/Test-ESAIBrainRoute.ps1`
- `scripts/Test-ESRoutePlanContract.ps1`
- `../../../../ES/Automation/RoutePlan/ESRoutePlanContract.psm1`
- `references/evidence-receipt-contract.md`
- `governance.json`

## Acceptance rule

All listed specialized cases and source assertions must be statically accounted for. Missing or stale evidence is `static-blocked`; `runtime-not-run` is reported separately and does not erase a completed static result.
