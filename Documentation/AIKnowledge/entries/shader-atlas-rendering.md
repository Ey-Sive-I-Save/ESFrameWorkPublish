# Shader、Composite Shader 与图集兼容路由投影

`KnowledgeId`: `es.project.shader-atlas-rendering.v1`  
`EntryMode`: `SharedRouteProjection`
`Authority`: `Derived routing projection`
`RouteKeys`: `shader-atlas`, `rendering-routing`, `texture-routing`
`ContentHash`: `61c87baf3434772a602b14ec48d7eb8014d2e4d4c5cc8d0e299ca8fc7645b1cf`
`EvidenceLevel`: `S1`
`RelatedSkills`: `es-ai-knowledge-curation`, `es-aibrain-route-authoring`
`RequiredReads`: `Documentation/AIKnowledge/Unity/unity-rendering-material-atlas/unity-rendering-material-atlas.md`、`Documentation/AIKnowledge/ESFramework/project-composite-shader-material/composite-shader-material-contract.md`、`Documentation/AIKnowledge/ESFramework/project-dynamic-atlas-runtime/dynamic-atlas-runtime-contract.md`

## RouteProjections

- `es.project.shader-atlas-rendering.v1`: `shader-atlas`, `rendering-routing`, `texture-routing`

## Routing boundary

本条目只保留旧 KnowledgeId 的兼容发现入口，不拥有 Shader、Material、SpriteAtlas、Dynamic Atlas 或性能证据事实：

- Unity Shader、Material、SpriteAtlas、Canvas 与合批机制转到 `es.unity.rendering-material-atlas.v1`。
- ES Composite Shader 参数、材质与 MPB 合同转到 `es.project.composite-shader-material-contract.v1`。
- ESDynamicAtlas 身份、Lease、预算、Provider、隔离与恢复转到 `es.project.dynamic-atlas-runtime-contract.v1`。
- Draw Call、GPU、显存与性能结论转到 `es.engineering.rendering-batching-evidence.v1`。

只有任务同时混用 Shader 与图集概念、且无法先判断权威对象时才命中本投影。确定目标后必须读取对应 canonical 条目，不得把本投影当作领域事实或 Runtime 证据。

## Failure and evidence boundary

- 单一领域任务仍命中本投影：收紧推导 route key 或 canonical routeKeys，不能复制事实来提高排名。
- canonical 条目缺失、stale 或 SourceRef 漂移：停止使用该路由结果并回读权威来源。
- 本投影通过只证明静态发现闭包；Unity、PlayMode、Profiler、Player、IL2CPP 与发布均为 `runtime-not-run`。

`StaleWhen`: 三个 canonical 渲染条目、图集分流规则、routeKeys 或任一 SourceRef 哈希变化。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/ESCompositeShader_URP职责与材质检查器验收边界_AI协作警告.md` (`743bd3b3b031ed527bbc6d76f04111bdf985cf423a2a092458385602b498863d`)
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/UI与图集（UIAtlas）/P2_UI图标_SpriteAtlas与运行时动态图集分流_AI协作警告.md` (`207f74a74d0f5e9cdcf91c5dd23d4f5afb9f40e3899938460a6c159666d4b5c5`)
- `Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESCompositeShaderParameters.cs` (`582012f2a6554d29de98ddd24b4e1ef21b13f5df462d1ab2a78fb3886a5dfc37`)
- `Assets/Plugins/ES/Editor/ESShader/ESCompositeShaderGUI.cs` (`1bb137d0fb62ec5dec2043b7e8f58618931ae40b59fe99f3e82df63242eaf62e`)
- `Assets/Scripts/ESLogic/Runtime/Graphics/DynamicAtlas/ESDynamicAtlasContracts.cs` (`0efeef56604386ae1f9bc174561d610e0a5b3838e6206bc524c10203262ce8bb`)
- `Assets/Scripts/ESLogic/Runtime/Graphics/DynamicAtlas/ESDynamicAtlasRuntime.cs` (`4ad8fafdcc1ed9a4e2d2b8516e6bbaafa0a192d897212886bdd6b168f13b34cf`)
- `Assets/Scripts/ESLogic/Runtime/Graphics/DynamicAtlas/ESDynamicAtlasGraphic.cs` (`b7fdb5bf72de1973e3e3085d8ceb0ea1e2cbd47657e05cdd1682b43650e95d0a`)
