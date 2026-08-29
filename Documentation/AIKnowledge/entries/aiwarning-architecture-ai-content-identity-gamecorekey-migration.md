# AI 内容身份与 GameCoreKey 迁移边界

`KnowledgeId`: `es.aiwarning.arch.ai-content-identity-gamecorekey-migration.v1`  
`Authority`: `AIWarnings + current ConfigKey/RuntimeData source`  
`RouteKeys`: `aiwarnings`, `architecture`, `identity`, `gamecore`, `configkey`, `migration`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `32d6d0657eed8d7f9492ef516bfa4e49fd1fd8b1e254d89e26f1794f56dec3ed`  
`SourceSetHash`: `32d6d0657eed8d7f9492ef516bfa4e49fd1fd8b1e254d89e26f1794f56dec3ed`  
`EntryBodyHash`: `5ef778dda7e550ee831c427e8001cf9139c1868251f619507f77c750f9c76b33`  
`StaleWhen`: ConfigKey/Catalog/RuntimeData/Provider、Action/Skill/Weapon 身份合同或迁移格式变化。

## 迁移范围

原 Warning 85 行、6,948 UTF-8 字节；现 Warning 保留稳定身份、类型化引用、运行时解析、SkillTrack/State 裁决、禁止事项和证据边界。当前设计、已迁移字段、开放裁决、风险和垂直切片计划迁入本条目。

## 当前事实

- `ESConfigKey` 提供稳定身份基座；Action 已有 `ESActionConfigKey`/Group/注入入口/RuntimeTable，但 `comboTransitions.targetActionId` 与 `cancelRules.targetActionId` 仍是字符串，跨 Action 迁移未完成。
- Weapon 已部分使用 `ESShotConfigKey` 和 `ESAssetReferPrefabConfigKey`；字段类型化不等于 Catalog、Provider、Scope 和发布闭环验收完成。
- `ESSkillTrackConfigKey` 当前主要是身份/元数据脚手架，真实轨道定义、独立 Consumer 和 RuntimeData 绑定尚未形成；`SkillDefinitionDataInfo` 仍直接持有 Track/State 对象，`linkedSkills` 与 `tags` 仍有对象/字符串引用。
- 只有轨道需要独立复用、查询、版本化、迁移或 AI 单独生成时，才建立完整 Track Definition/Group/Consumer/RuntimeData；否则使用 Owner Key + 稳定局部 ID。State 同理必须先裁决独立定义资格。
- 正确迁移顺序是冻结格式、建立旧值→新 Key 映射、提供失败策略，再按 Action/Weapon/Skill/Audio/Tag/资源 Provider 垂直切片验收。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/AI自动化内容身份与GameCoreKey迁移_AI协作警告.md` (`efad4b628b85820da047b070b6ac9d5f5c6a8c2c9140b7ebd0674e2fc52ab8f5`)
- `Assets/Plugins/ES/1_Design/ConfigKey/ESConfigKey.cs` (`08c4fda0e5ec09db552834ff2137314aec6244709ea7d40c9c0e276a9987c33e`)
- `Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ActionTemplateDataInfo.cs` (`ae5aca0a9db8851ae9e1683f4d2a82546addc39d267b1181e4120216c684b3cd`)
- `Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Action/ESActionConfigKeyData.cs` (`40ef787e68cf089d0c2054d971cf8a8873e4cfa4c879dd5daa066b51ca705e06`)
- `Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Action/ESSkillTrackConfigKeyData.cs` (`2fa57fbd044656ed289f703edb026aa0902e3ae90aa68af08b62e36601991466`)
- `Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/SkillDefinitionDataInfo.cs` (`97acfb3ee6f83e0ca64b028bf99adaa593c113edd84fbd51b1e8c56c25675755`)
- `Assets/Scripts/ESLogic/Data/GameCoreConfigKey/Skill/ESSkillConfigKeyData.cs` (`5060c086894c30142fcb4e20975dca29e2b8649c07825934c9e4adc3bac03cae`)

## EvidenceRefs

- `Assets/Plugins/ES/1_Design/ConfigKey/ESConfigKey.cs`
- `runtime-not-run`
