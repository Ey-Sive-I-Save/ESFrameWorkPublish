# Knowledge output policy

## Modes

| Mode | Default budget | Allowed output |
|---|---:|---|
| `index` | 10 rows | KnowledgeId, topic, routeKeys, authority, evidenceLevel, staleWhen |
| `route-pack` | 1–3 entries | matched entries, requiredReads, relatedSkills, source/evidence boundary |
| `detailed-entry` | 1 domain | one source-grounded entry with hashes and non-claims |
| `full-audit` | explicit user request | batched inventory with uncovered domains and evidence gaps |

The budget limits context and output, not the truth standard. If a task needs more than the budget, split it into named batches and preserve a handoff list.

## Authority decisions

Knowledge is a derived navigation layer. It may summarize a source but never override it. If a Knowledge entry conflicts with current source, current source wins and the entry becomes stale. If AIWarnings conflict with an aspirational design document, the current P0/source fact wins unless a newer authority is explicitly identified.

## Required non-claims

Every detailed output must state what it does not prove. Typical gaps include Unity compilation, PlayMode lifecycle, Profiler allocation, Player/IL2CPP, platform backend, migration replay, and release acceptance.

## Anti-expansion cases

Reject requests that ask the Skill to silently include all project files, all AIWarnings, unrelated source domains, or unsupported runtime claims. Return a bounded first batch and route the next batch through `KnowledgeIndex.yaml`.
