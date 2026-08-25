# UI Knowledge Preflight Contract

This contract is the shared gate for `es-ui-intent-authoring` and
`es-ui-prefab-authoring`. It is a read/decision contract; it does not grant write,
Unity, runtime or release authority.

## Risk classification

The task is **high risk** when it includes any of the following:

- creating or changing a Prefab, Scene, Fixture, screenshot or visual baseline;
- converting a reference image or visual brief into ScreenSpec, AssetManifest,
  LayoutPlan or BehaviorSpec;
- choosing Canvas ownership, anchors, pivots, safe area, responsive profiles,
  clipping, typography, fonts, sprites, fallbacks or Atlas policy;
- declaring UI focus, navigation, input modality, state transitions or a runtime
  handoff;
- invoking a Materializer, Unity process, GPU capture, visual QA or evidence gate.

Purely read-only explanation, a local text typo, or a bounded schema lookup is
low risk unless the user asks it to produce or validate one of the artifacts above.

## Required preflight

For a high-risk task, the Skill must stop before planning or writing and perform this
ordered lookup:

```text
AGENTS.md
  -> Documentation/AIKnowledge/AIBRAIN_ENTRY.md
  -> Documentation/AIKnowledge/KnowledgeIndex.yaml
  -> route-key match
  -> canonical Knowledge owner(s)
  -> requiredReads and SourceRefs
  -> source hash/stale check
  -> bounded plan or fail-closed result
```

The route must select the canonical owner for each fact. Read at most three owners by
default; a larger set requires an explicit reason in the receipt. A route hit is not a
read receipt: every selected `requiredReads` path must be read and its current
SHA-256 must match the index/entry declaration.

The preflight result is `ready` only when all of these are true:

- at least one route matches the task and every selected owner is canonical;
- `requiredReads` resolve below the project root and are actually read;
- SourceRef hashes, `ContentHash`/v2 source-set and body hashes, and stale conditions
  are current;
- the plan records non-claims and the evidence level that remains unproven.

Otherwise the Skill must fail closed with one of:
`NoKnowledgeRoute`, `KnowledgeReadRequired`, `KnowledgeIndexMismatch`,
`KnowledgeStale`, or `KnowledgeExemptionRequired`.

## Explicit exemption

Only the user may exempt a task, using an explicit statement that the Knowledge
library is not applicable. The receipt must preserve the exact scope and a non-empty
reason. The AI must not infer an exemption from time pressure, a familiar task, a
small diff, or missing route. An exemption never removes AIWarnings, Skill contract,
permission, path, or evidence boundaries.

## Read receipt

Every high-risk run must retain a machine-readable receipt (or an equivalent plan
record) containing:

```text
schemaVersion: 1
taskRisk: high
routeKeys: [...]
selectedKnowledgeIds: [...]
requiredReads: [{path, sha256, read: true}]
sourceRefs: [{path, sha256, current: true}]
staleCheck: passed | stale | blocked
authority: [...]
evidenceLevel: S0-S6
nonClaims: [...]
decision: ready | blocked | exempted
exemption: {explicitUserStatement, scope, reason} | null
```

The receipt proves only Knowledge preflight. It does not prove Unity materialization,
Prefab/Scene serialization, GPU pixels, runtime input, player usability or release.

## Replay requirements

Static validation must cover at least: matching route, unread Knowledge, no route,
stale SourceRef, explicit scoped exemption, and a low-risk bypass. A deterministic
replay must produce the same normalized decision and input hash for identical inputs.
