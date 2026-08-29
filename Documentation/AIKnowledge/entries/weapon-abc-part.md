# 武器 ABC 部件（ABCP）

`KnowledgeId`: `es.weapon.ai-abc-part.v1`
`Authority`: `Weapon ABCP contract plus ABCC Core contract and current weapon P0`
`RouteKeys`: `ai-abc`, `abc-part`, `weapon`, `weapon-definition`, `prefab`, `input`, `evidence`, `route-stage`, `static-replay`, `knowledge`
`HashSchema`: `v2`
`ContentHash`: `9c780327bdad50e67d57981cbd8503be703b66298dcac26272245bfe63bc9850`
`SourceSetHash`: `9c780327bdad50e67d57981cbd8503be703b66298dcac26272245bfe63bc9850`
`EntryBodyHash`: `ac302d24c1f20add590100ab0a9b42dcc65c6259ab48960126ebf658d9661151`
`EvidenceLevel`: `S1`
`StaleWhen`: `ABCC/ABCP contracts, WeaponDefinition ownership, route stages, AIBrain route, Skill, aliases or any SourceRef hash changes.`

## Scope

Weapon ABCP is a bounded domain part with independent Skill and Knowledge. It
references `es.ai-abc.core.v1`, selects the capabilities needed by a weapon
task, and keeps the Part as canonical owner. It never copies ABCC text.

## Formal naming

The shared `ABC` vocabulary is owned by the mode registry's `namingAuthority`:
`Agent–Behavior–Collaborator`. A is the weapon intent/process side, B is the
independent weapon mechanism/capability side, and C is the human or AI
collaborator. `ABCP.Part` means a bounded domain part; it does not rename or
replace `ABCC.Core`, and any `ABCD.Dynamic` fallback remains explicit-only.

## A/B/C mapping

- A expresses attack lifecycle, damage balance, Prefab/DataInfo binding and
  resource/cooldown intent.
- B offers WeaponDefinition/GameCore, Entity/Prefab, Input, Command and task
  capabilities with schemas, preconditions, effects, failure codes and
  evidence.
- C is a human designer, test AI or collaborator who supplies authorization
  and acceptance.

Legacy and new tracks may coexist only through an explicit versioned adapter;
there is no silent merge. Dynamic fallback is explicit-only.

## Authority and non-claims

Reusable weapon definitions remain in `ItemWeaponSharedData`/
`ESWeaponRuntimeData`, instance state remains in
`ItemWeaponVariableData`/`WeaponRuntimeState`, and Combat executes rather than
defines new weapon parameters. This entry is static navigation only; it does
not prove Unity Prefab import, firing, collision, damage, input, performance,
Player, IL2CPP or release behavior.

## SourceRefs

- `ES/Automation/Contracts/es-ai-abc-part-v1.schema.json` (`5123da41566b0e1eee80b428060dfaaef661284d553416142f292c73bb368d5a`)
- `ES/Automation/Contracts/es-ai-abc-weapon-part.v1.json` (`4d55eb38f5e04075d8b1b19650ef395739efb4fa51be1e06ed423df1ac3f075b`)
- `ES/Automation/Contracts/es-ai-abc-core-v1.json` (`20a10dc81762e61c4dc946bc6e6ea11fc830bc5f5e11cfe65def43997f613dbc`)
- `ES/Automation/Contracts/es-ai-abc-mode.registry.json` (`5950220db01715980e2456fdea26a80f8f816c5e61cb47f99c03739a8510e95e`)
- `.agents/skills/es-weapon-abc-part/SKILL.md` (`3b821c303da99f48cdf28061287e17be02f467cd7bd1deca01bff322d0077fb0`)
- `.agents/skills/es-weapon-abc-part/governance.json` (`2fc2fd5ef1cef3fe4cf7074e3c7e49b0f07956001cb2d9a8cd103acf12d12cc6`)
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/总体架构（Architecture）/项目最高警告_P0_CombatModule武器定义迁移边界与验收门禁_AI协作警告.md` (`85dffa1ae38dbfbc0d11ddd53744e229e5cd9e1421bcf7fd5fa6cc199edc105b`)
- `ES/Automation/Contracts/es-route-stage.registry.json` (`4f67cd468ef4d64c04eb219da7fcb1cbdab10a62bf0590328409470b8a0fb82d`)
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md` (`8e3f621daa078c047311f28dede7e839aae4fd34d3062a259561604fdbd2f2f4`)
