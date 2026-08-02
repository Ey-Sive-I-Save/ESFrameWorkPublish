---
name: es-entity-authoring
description: Author or review ESFramework entities, character prefabs, DataInfo entry points, components, attachment points, motion, control, tags, pooling, and world integration. Use when creating a player, NPC, vehicle, projectile host, character template, entity component, or changing an entity prefab hierarchy or lifecycle.
---

# Author ES Entities

Build entities from the current character, prefab, DataInfo, control, and pooling contracts. Do not treat archived proposals as implemented architecture.

## Workflow

1. Read the AIWarnings start files and `references/project-map.md`.
2. Classify the target as player, NPC, vehicle, projectile host, pooled generic object, or scene-only actor.
3. Select one matching AICommand when available. Use informational or plan commands only for their stated read-only purpose.
4. Inspect an existing current-source example with the same role. Confirm the authoritative DataInfo, prefab entry, component ownership, tags, attachment points, motion, and control path.
5. Define spawn, enable, disable, despawn, pool return, and destruction responsibilities before editing.
6. Keep content data, runtime state, scene references, and reusable services in their proper owners.
7. Validate prefab hierarchy, missing bindings, pooling callbacks, control arbitration, tag identity, and runtime allocations as applicable.
8. Use `$es-unity-compile` for import, Console, EditMode/PlayMode, and runtime evidence; run `$es-utf8-guard` for text changes.

## Required boundaries

- Treat `Documentation/CHARACTER_PREFAB_CONTRACT.md` as required for character and player prefabs.
- Treat `Documentation/ES_GENERIC_LIFE.md` as required for pooled lifecycle work.
- Do not promote a file under `90_提案与废止（Archive）` into current fact without source verification.
- Do not store per-instance mutable state in shared authoring assets.
- Do not bypass active-request arbitration for control, camera, UI focus, or similar ownership.
- Do not add parallel entity roots or hidden scene scans when an existing registration path is authoritative.

## Delivery

Report the entity category, authoritative data and prefab entry, component ownership, lifecycle table, control/tag/pool integrations, Unity evidence, and missing runtime checks.
