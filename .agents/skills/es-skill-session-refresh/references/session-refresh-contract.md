# Skill session refresh contract

## Authority and scope

This contract governs incremental discovery for one AI session. It is derived navigation evidence. It does not grant execution authority and does not replace AIWarnings, AICommand, TaskContract, or AIBrain.

## Snapshot contents

The deterministic snapshot contains:

- `sessionId` and normalized project root;
- SHA-256 for `.agents/SKILL_RESOURCE_INDEX.yaml`, `.agents/SKILL_CATALOG.yaml`, `Documentation/AIKnowledge/KnowledgeIndex.yaml`, AIBrain entry routing, the capability-mode/command-binding registries, and the AICommand catalog;
- one record per direct Skill with hashes for `SKILL.md`, `governance.json`, `agents/openai.yaml`, `static-replay.manifest.json`, and directly bundled `references/` and `scripts/` files;
- a stable snapshot hash over sorted paths and hashes.

The script hashes files; it does not place their contents in the model context.

## Invalidation

Invalidate the session binding when:

1. a selected Skill metadata or resource hash changes;
2. a selected route index, Catalog record, Knowledge entry, required read, AICommand, or TaskContract hash changes;
3. a selected Skill disappears or a required file becomes unreadable;
4. the task read snapshot or source set changes;
5. the current plan hash no longer includes the same selected bindings.

An unrelated Skill change is reported but does not invalidate the current task unless route discovery selects it.

## Receipt semantics

`unchanged` means the observed metadata set is byte-identical to the baseline. `refreshed` means changes were found and selected metadata was re-read. `stale` means a previous plan or conclusion is no longer safe to reuse. `blocked` means the selected route has a missing, unreadable, or contradictory binding.

A refresh receipt must include source hashes and timestamps. It must not contain a model-authored claim that a Skill was understood, executed, or accepted.
