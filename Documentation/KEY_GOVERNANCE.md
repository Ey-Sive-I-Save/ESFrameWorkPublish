# Project Key Governance

状态：现行规范。最后验证：2026-07-31，ES_Design 编译通过；GameCore Bake、Unity Test Runner 与审计报告重生成仍按发布门禁执行。

## Core Rule

`EnumKey` and `StringKey` are equal stable identities. A definition may expose either one or both. If both are present, a catalog must bind them to the same definition. `RuntimeKey` is an in-process acceleration index only: it must never be written to configuration, save data, a release manifest, or network packets.

`EnumKey` is preferred for editor discovery, generated code, rename safety, and constrained authoring. It is not more authoritative than a registered `StringKey`. A StringKey is a first-class identity for authored extensions, DLC, mod content, and business data.

## Classification

| System | Identity Contract | Runtime Form | Governance |
| --- | --- | --- | --- |
| GameCore ConfigKey (Buff, Skill, Actor, Shot, Weapon) | EnumKey and/or StringKey | scoped ConfigKeyTable RuntimeKey | stable scope, dual-alias validation, schema handshake |
| Asset business reference | EnumKey and/or StringKey plus Asset GUID/local file ID | scoped asset-table RuntimeKey | ConfigKey selects business use; GUID/local ID locates the Unity asset |
| GameTag | EnumKey and/or StringKey | HotSlot `ulong` mask or sparse RuntimeKey count | stable identities are equal; storage is a performance choice; RuntimeKey is process-local and conditions bind SchemaHash plus layout hash |
| Super Attribute | EnumKey and/or StringKey, value type and definition schema | catalog RuntimeKey, HotSlot or sparse storage | value type/default/range/formula/migration belong to the catalog |
| Input configuration, schemes, actions, and rebind binding ids | `Input.Config` StringKey; built-in schemes and fixed actions use EnumKey + StringKey; extensions use StringKey | legacy action HotSlot plus `Input.Scheme` / `Input.Action` Catalog RuntimeKey | binding profiles persist ConfigKey, Scheme SchemaHash, Action SchemaHash, and stable binding ids; runtime rejects an incompatible profile |
| Runtime mode, operation/expression type registries | fixed enum/type name unless externally authored | domain-local lookup | promote to a catalog before they enter save, network, DLC, or mod contracts |
| State default parameters | fixed enum plus display/name alias | typed field or domain-local array | current state context accepts typed enums only; promote to a scoped catalog before name-based persistence, mods, or network payloads are introduced |
| State names, ContextPool keys, transform maps, pool group names, save-section names | local container key by default | local dictionary/index | not a stable business ID unless explicitly promoted and cataloged |
| Unity GUID/local file ID, Address, AssetBundle hash, InstanceID, pool handle, generated context ID | resource/object/lifetime identity | backend-specific | do not force into business Key catalogs |

## Required Lifecycle

1. Declare the key in a catalog with scope, kind, value type, storage policy, default/range/formula, migration information, and owner.
2. Build deterministically. Sorting stable identity, never registration order, determines dense catalog RuntimeKeys and SchemaHash.
3. Serialize only the stable identity. On process start, resolve it through the active catalog before it reaches a hot path.
4. Cache RuntimeKey only inside the current process and invalidate it on catalog rebuild/resource transition.
5. Exchange catalog name and SchemaHash before a multiplayer session accepts catalog-indexed payloads. Never assume both processes assigned equal RuntimeKeys.
6. Record declaration/read/write provenance in editor diagnostics. A declared but unread/unwritten key is a review signal; conflicting type/schema declarations are build errors.

## Storage Policy

`HotSlot` and `Sparse` only decide memory and access strategy. They do not change identity, persistence, networking, or validation.

- `HotSlot`: dense fixed storage for proven high-frequency fields such as character movement and input-critical permits.
- `Sparse`: allocate only when an instance actually owns a value or modifier. This is the normal choice for optional equipment, role, quest, DLC, and extension attributes.
- A catalog may contain both; public APIs resolve stable key to RuntimeKey before selecting storage.
- Buff modifier bindings may contain EnumKey and/or StringKey, but target resolution is rejected unless the bound entity attribute Catalog declares the exact identity and value type. A string can no longer create an undeclared sparse attribute implicitly.

## Current Enforcement Points

- `ESKeyCatalog` provides deterministic dense RuntimeKey assignment, SchemaHash, peer validation, and declaration/read/write diagnostics.
- `ESConfigKeyTable` has a stable scope, deterministic StringKey RuntimeKey generation, strict Enum/String alias checks, and a table schema handshake.
- Runtime GameCore and asset tables each declare an explicit scope. Asset identity resolution validates both aliases instead of preferring EnumKey blindly.
- `ESTagBakeTable` resolves EnumKey and/or StringKey declarations into validated RuntimeKeys. `HotSlot` entries occupy RuntimeKey `1-63`; `Sparse` entries start at `64`. Neither range implies a stable identity type. Each entry declares `Runtime`/`EditorOnly`/`Deprecated` availability, an optional explicit replacement for a deprecated stable identity, and `SaveGame`/`Network` transfer compatibility; these declarations participate in the SchemaHash.
- `ESTagCollection` is the generic runtime container. It keeps aggregate HotSlot counts and sparse `RuntimeKey -> Count` entries only. Lease objects own their source reference and release exactly one increment; the collection does not retain source objects. `Clear()` advances a collection generation, so an earlier Lease is invalid and can never release a later holder of the same Tag. `GetDebugSnapshot()` is diagnostic-only and stays off the hot path. Receivers subscribe through the shared Link protocol; a Host must not create a second C# event relay. Entity and Item are current Hosts; a runtime object creates a Collection only when its own facts need cross-system composition queries.
- `ESTagConditionConfig` is the only authored condition shape: `required`, `requiredAny`, and `forbidden` each contain unified `ESTagStableReference` values. The Picker reads the sole formal Catalog and never asks authors to type a StringKey. Gameplay code calls `ESTagCollection.Matches(conditionConfig)` or `TryMatches(conditionConfig, out matches, out error)` and never handles RuntimeKeys, SchemaHash, or compilation. `ESTagConditionRuntime` is an infrastructure/performance API; it validates SchemaHash and RuntimeLayoutHash before sparse RuntimeKey evaluation. The common HotSlot-only path only performs mask checks.
- Tag writing has three deliberate levels. A Host changes its own idempotent fact with `Tags.SetTag(tag, active)` and keeps no handle; this owns exactly one Host contribution and cannot remove an external contribution. Buffs, equipped weapons, StateSupport projections, trigger zones, tasks, and other external lifecycles own one `ESTagLeaseSet`, call `TryApply`, and release only that set. Code uses `Tags.Acquire(...)` only when one independently released public Lease is genuinely required. `TryApply` uses internal value tokens, preserves the prior ownership state if validation or candidate acquisition fails, and creates no managed Lease per configured Tag; it is not an event-atomic switch, so Link receivers can observe candidate additions before old ownership releases. Direct aggregate count mutation and `RemoveAll` are not business APIs.
- Tag writer configuration lives directly on the real owner as `List<ESTagStableReference> tags`. `ActorDataInfo`, `MonsterDataInfo`, `NpcDataInfo`, and `ItemDataInfo` are the sole authored sources for their corresponding Host's intrinsic Tags. `ESTagGrantConfig` has been removed and must not be recreated as a compatibility layer.
- Skill, State, AI input dispatch, Interaction, and Buff target gates consume `ESTagConditionConfig`. `ESHitTagEligibility` is the Tag-only contract for a HitResolver: it may allow or deny a resolved attacker/target pair, but it does not own physics candidates, damage, faction, friendly fire, or hit-location rules. `ItemShotModule` continues to produce candidates only.
- `ESTagStableSnapshot` serializes only unified stable references and SchemaHash. It excludes RuntimeKeys, counts, HotMask values, and source handles. Restore requires a matching SchemaHash plus an explicit `ESTagLeaseSet` ownership boundary; transient Tag writers must rebuild their own state rather than being implicitly persisted.
- `ESGameManager.LocalControl` is the sole owner of Entity-to-RuntimeMode projection. Explicit player-control ownership can be claimed by `EntityPlayerInputWriteModule`; possession/spawn systems replace the controlled entity through `SetControlledEntity`, which releases old RuntimeMode handles before binding the new entity.
- `ESSuperAttributeCatalog` compiles value type, storage policy, defaults/range/formula/migration schema from generic attribute definitions. `GameCoreEditorGlobalData.characterAttributes` is the only editable source. A Character `HotSlot` with `fixedApiName` additionally projects a deterministic typed API into `ESCharacterAttributeCatalog.generated.cs`; its Enum/String identity remains authoritative, while the generated zero-based ID is only a compiled array slot. Only this fixed API structure is staged before Bake: access name, Float/Permit kind, stable identity and deterministic slot order. Authored values, ranges, display text, formulas and migrations go directly through Bake. Ordinary Character HotSlot, Sparse and Item attributes remain Catalog-only; optional modifiers use sparse `RuntimeKey` maps.
- `ESInputSchemeCatalog` validates built-in scheme Enum/String aliases and registered extension schemes. Scheme RuntimeKeys are process-local only; player profiles keep the stable scheme StringKey.
- `ESInputActionCatalog` validates input action aliases, declared scheme references, and globally unique rebind binding ids. The existing zero-based `ESInputActionId` remains a HotSlot index; Catalog uses a reversible non-zero stable EnumKey, so `Move(0)` cannot be confused with an absent key.
- `ESInputBindingProfile` persists its `Input.Config` StringKey plus Scheme/Action SchemaHash. A profile from another config or schema is rejected. A legacy profile without those fields is upgraded only when every enabled override still resolves to the exact current binding/action/scheme; it never receives a guessed remap.
- `【ES】/项目设置/GameCore/审计项目稳定Key治理` writes `Documentation/KEY_AUDIT_REPORT.md`. It lists loaded GameCore, Attribute, and Tag catalogs structurally, then reports direct source literals and project-owned string dictionaries as either known local containers or review candidates.

## Explicit Local-Key Boundaries

- `StateMachineContext._sharedData` and runtime flags are scoped to one generated context and are discarded with it. They must not be serialized as a cross-system gameplay contract.
- State-machine name maps are indexes over a registered machine, not a project-wide state identity. A state name becomes a stable key only when another asset, save, or packet refers to it independently of that machine.
- State default Int/Bool parameters retain their explicit stable enum values and names, while every `StateMachineContext` resolves them to deterministic dense RuntimeKeys before indexing its arrays. Do not use the enum's sparse numeric value as per-instance storage layout.
- `EntityTransformMapping.dynamicMap` is a per-entity binding map. A key that is only meaningful inside that prefab remains local; a key shared by authored equipment, skills, DLC, or external data must be declared in an appropriate scoped catalog before use.
- Object-pool group keys are runtime ownership names. Prefab business identity belongs to the asset ConfigKey; a pool does not create a second persistent identity.
- GUIDs, addresses, bundle names/hashes, paths, Unity instance IDs, and generated context IDs are locator or lifetime data. They must remain separate from business stable keys.

## Migration Gate

New code may not add a string dictionary as a persistence, network, content-extension, or cross-system business contract. It must first declare whether the key is a local container key or a stable catalog key. When it is stable, add the declaration, a migration policy, validation, SchemaHash/handshake requirements, and an editor usage owner before consumers are added. Run the project audit before review; every `UNCLASSIFIED` result needs either Catalog promotion or an explicit local-boundary decision.
