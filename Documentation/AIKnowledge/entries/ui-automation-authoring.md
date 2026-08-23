# ScreenSpec v3 游戏 UI 自动化装配

`KnowledgeId`: `es.project.ui-automation-authoring.v1`  
`Authority`: `Source + Skill contract + Unity evidence`  
`RouteKeys`: `ui-automation`, `screen-spec-v3`, `ui`, `prefab`, `fixture-scene`, `layout`, `responsive`, `visual-qa`, `asset-fallback`, `fixture`  
`ContentHash`: `57d43a405578e54ac71611eb79b03508d9e3cc080e3c4208c619ac7c56124428`

## Purpose

This entry routes AI tasks that assemble high-fidelity Unity game UI from a brief or reference
image. It covers visual Prefabs, Fixture Scenes, responsive layout and evidence. It does not own
runtime Window, Presenter, inventory, combat, economy, navigation or input-domain logic.

## Authority layers

| Layer | Authority | Boundary |
|---|---|---|
| Knowledge | routing, constraints, anti-patterns, source and stale policy | never stores drifting asset facts |
| Registry | component/template capability, layout recipe and fallback policy | never writes Unity assets or declares visual acceptance |
| ScreenSpec v3 | candidate semantic component tree, profiles, states and intent | candidate is not write authorization |
| Materializer | deterministic Prefab/Fixture serialization | never re-interprets design or owns business facts |
| Evidence gate | structure snapshots, GPU PNGs and fixture coverage | static checks cannot impersonate Unity runtime evidence |

## Required route

1. Classify the request into a registered screen family. Return `blocked` or request missing
   information when confidence is insufficient; do not silently choose HUD, collection or menu.
2. Select a registered recipe, component set and layout policy. Record the decision inputs,
   priority and conflict resolution rather than embedding them in prose.
3. Emit a candidate ScreenSpec v3 with AssetManifest, LayoutPlan, BehaviorSpec and profile/state
   matrix. Validate it before any Unity write.
4. Generate deterministic Fixture states, then invoke the authorized Unity materializer through
   the project execution boundary. The candidate spec does not itself grant Prefab or Scene write
   permission.
5. Read structural snapshots and fresh GPU screenshots. Reject missing content, invalid anchors,
   safe-area overflow, unsupported states, unresolved required assets and blank evidence.

## Registry boundary

Component records must use stable IDs and declare input slots, state variants, minimum size,
supported profiles, resource dependencies and fallback. AssetManifest entries are per-screen
facts and must carry source, hash and provenance; registry fallback policy is not proof that an
asset exists. A new component or recipe requires a registry entry, validator coverage and a
materializer implementation in the same change.

## SourceRefs

- `.agents/skills/es-ui-prefab-authoring/SKILL.md` (`3a9f45d41d00437f7484438ee0215440012f0de8b6660a1fefe2120fc429096e`)
- `.agents/skills/es-ui-prefab-authoring/governance.json` (`12a8bf9e0a80ec25889b1d7df90b8c0ffafab7a96ae534e2a734ff428ee9d331`)
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-component-registry.json` (`e67d3ba3bb5af3f93a2071de611bcd98d7ea35e48d6fd2b6f343490271548f09`)
- `.agents/skills/es-ui-prefab-authoring/references/game-ui-materializer-contract.md` (`69fd14142f1a859f1c25cffd0bd56d86633c17943396913f6558d3b673c433ff`)
- `Assets/Scripts/ESLogic/Editor/UI/ESUIGameScreenMaterializer.cs` (`92def4cdbd7a83f9ae93764cf6f49019e6bfdedf260af3f0fb453ce610eb6541`)
- `Documentation/ES_UI_AUTHORING_WORKFLOW.md` (`8e1fe9d3736ad07de9ae953dd628d3f512dc94713ea07a9aee32208570746aa4`)

`EvidenceLevel`: `S2` (protocol, registry and source boundary; Unity evidence must be supplied by the current run).  
`StaleWhen`: ScreenSpec schema, component registry, Materializer, Prefab/Fixture output or visual evidence contract changes.
