# Entity 插件依赖与适配层边界

`KnowledgeId`: `es.aiwarning.arch.entity-plugin-dependency-boundary.v1`  
`Authority`: `AIWarnings + Packages/asmdef/current Entity source`  
`RouteKeys`: `aiwarnings`, `architecture`, `entity`, `dependency`, `asmdef`, `runtime`, `editor`, `adapter`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `9ada7729fd0f24393a6e6f43cbec67da0c34d8265d0f0a8189eaf10de2f83703`  
`SourceSetHash`: `9ada7729fd0f24393a6e6f43cbec67da0c34d8265d0f0a8189eaf10de2f83703`  
`EntryBodyHash`: `0a729a136070437395b6b65a87e34babd45cb7ff131382f5c57068d8a26fb61e`  
`StaleWhen`: Packages/manifest、asmdef、Entity 五域或第三方插件引用变化。

## 迁移范围

原 Warning 164 行、11,719 UTF-8 字节；现 Warning 保留 Entity 根、五域、依赖方向和适配层长期约束。本条目承接插件/包地图、当前程序集引用、角色模板分层、第三方耦合风险、过时点与源码入口。

## 当前依赖事实

- 角色主线位于 `Assets/Scripts/ESLogic/Runtime/Entity`，沿用 `Entity + EntityCharacterIdentity + Core → Domain → Module` 与 Basic/AI/Buff/Equipment/State 五域；`ESPlayer` 仅是历史/空壳程序集。
- `ES_Design`/`ES_Logic` 的 asmdef 引用必须显式可审计。KCC 是运动硬依赖但只能位于 Movement Adapter；Input System 只提供可替换输入源；FinalIK/Cinemachine 属表现/目标适配；EasySave 只消费快照；DOTween 为遗留隔离项。
- 纯协议层不得引用 Unity 第三方组件；领域层不得把插件对象作为移动、战斗、剧情、网络或存档权威；表现和工具层不得反向污染运行时核心。
- 角色输入、AI、网络、剧情和回放必须汇入统一 Intent/ControlAuthority；相机只绑定稳定目标点，存档只接收快照，Tween 不驱动 KCC/网络权威位移。
- 新插件或 asmdef 变更需同时审计激活、反向引用、默认初始化、Prefab/Scene 和发布链；不得通过删除第三方目录或恢复旧 GameCore 路径规避依赖问题。

## EvidenceRefs

### evidence

- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Entity与世界（EntityWorld）/模型重构_插件依赖边界_AI协作说明.md` (`a97ad637f95a0af8565a43aa54df53d55cd07616db61b31c40366be31515f6e1`)
- `Packages/manifest.json` (`d447378a6e35e070c3fa8df645a5829a703eb4b488f8ae8132cd894ab19d016d`)
- `Assets/Scripts/ESLogic/ES_Logic.asmdef` (`a05b333b2391766b924a7cb4312f4dd93642f9057ea5716a09248484290565bd`)
- `Assets/Plugins/ES/1_Design/ES_Design.asmdef` (`ad58874939f26f5a5b72dc972b8c6d235aa0c5261d45fea51a18787f4853fbd4`)
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs` (`5d1f0225e27a8b04d219917fc15da5026675bc8d7b10024a17e91c8682c9751e`)
