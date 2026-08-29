---
name: es-ai-abc-core
description: >-
  Design and validate the independent ES AI ABC Adapter Core (ABCC): translate
  A intent into B capability offers, normalize evidence back to A, and preserve
  every ABCD Dynamic kernel capability. Use for semantic adapter contracts,
  core/part boundaries, capability negotiation, evidence-gated completion, or
  reviewing whether a domain part can safely consume the core.
---

# ES AI ABC Core (ABCC)

## Overview

ABCC is an independent semantic adapter core. It is not a section of
`es-agent-mechanism-replication`, and it does not replace `ABCD (Dynamic)`.
The research Skill and its Knowledge remain provenance only; the files listed
below are the authority for ABCC contracts.

## Formal modes

- **ABCD (Dynamic)** is the original independent, broad, adaptive system.
- **ABCC (Core)** is an independent A↔B semantic adapter that exposes all six
  ABCD kernel capabilities through stable contracts.
- **ABCP (Part)** is a bounded domain part that references ABCC by IDs and
  contracts; it does not copy the Core text.

ABCC+ABCP is a focused profile, not a hidden mutation of ABCD. A Part may
fallback to ABCD only through an explicit fallback contract.

## A↔B protocol

1. C (human or AI collaborator) supplies the goal, constraints, evidence
   expectations and current authorization.
2. A emits an `aIntentEnvelope` with a goal revision and source snapshot.
3. ABCC matches requested semantics to a versioned B capability offer.
4. B declares schemas, preconditions, effects, evidence and failure codes;
   mismatches cause `replan`, not a silent reinterpretation.
5. ABCC maps the result back to A as a normalized result with an evidence set
   and immutable receipt reference.
6. Audit and completion are separate from C's final acceptance.
7. Missing capability blocks; missing evidence caps the claim; unauthorized
   effects block; observable failure enters the recovery path.

The machine contract is
`ES/Automation/Contracts/es-ai-abc-interface-v1.schema.json`; the Core instance
and parity declaration are in `es-ai-abc-core-v1.json`.

Static markers: **ABCC independent**, **ABCD parity**, **A-to-B**,
**normalized evidence**, **explicit-only**, and **deterministic-replay**.

## ABCD parity requirement

ABCC must provide all six kernel capabilities, while selection remains
predicate-driven (not every task executes all six):

1. `bounded-tool-action` — bounded, authorized action with change evidence.
2. `failure-recovery` — observable failure, revision and bounded retry/stop.
3. `branch-evaluation` — finite candidates, criteria and a ranked decision.
4. `state-transition-guard` — legal lifecycle/ownership transitions only.
5. `environment-trust-gate` — environment identity and trust before external
   tools or runtime claims.
6. `audit-evidence-chain` — source hashes, receipts, non-claims and completion.

The `parityContract.requiredCapabilities` list is a hard closure check. A
missing or semantically incompatible capability is `blocked`/`replan`; it is
never hidden by a Part or by the old research mapping.

## Workflow

1. Read AIBrain and the one or two routed Knowledge entries, then read this
   Skill and the Core contract files.
2. Freeze the C authorization and goal revision; do not infer Runtime or write
   authority from a Skill, route, catalog or Knowledge entry.
3. Validate A intent, negotiate B offers, and record field-level mappings and
   loss policy.
4. Require evidence for each accepted output and report non-claims.
5. Run bounded static replay for positive, invalid, denied-expansion,
   idempotency, hash invalidation, interruption recovery and deterministic
   output cases.
6. Use `es-adversarial-review` after modifications. Unity/Runtime acceptance is
   a separate explicitly authorized operation.

`KnowledgeIndex` and each Knowledge `SourceRef`/`ContentHash` are navigation
inputs only; a hash or route drift makes the selected entry stale and requires
re-planning.

## Boundaries and references

- Core contract: `ES/Automation/Contracts/es-ai-abc-core-v1.json`
- Interface schema: `ES/Automation/Contracts/es-ai-abc-interface-v1.schema.json`
- Mode registry: `ES/Automation/Contracts/es-ai-abc-mode.registry.json`
- Route stages: `ES/Automation/Contracts/es-route-stage.registry.json`
- Independent Knowledge: `Documentation/AIKnowledge/entries/ai-abc-core.md`
- Research provenance (not Core authority):
  `.agents/skills/es-agent-mechanism-replication/`

This Skill is read-only by default. It does not start Unity, Player, host
processes, network access or release actions, and it does not grant permission
to write project files.

## Engineering controls

Identity, authority, risk, observability, recovery, performance, compatibility
and supply-chain controls are declared in `governance.json`. StaticDeepReplay
is the first verification path; Runtime requires fresh, explicit authorization.

## Skill 使用披露

遵循项目根 `AGENTS.md` 和 `.agents/README.md` 的 Skill 披露规则；披露本
Skill 不等于获得授权或产生运行时证据。
