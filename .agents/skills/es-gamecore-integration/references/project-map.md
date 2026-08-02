# ES GameCore project map

Read these live files; this map is navigation, not a substitute for source inspection.

## Authorization commands

- `Assets/Plugins/ES/AICommands/执行_GameCore根SO接入_强约束_AI命令.md`
- `Assets/Plugins/ES/AICommands/执行_新增GameCore或Asset全局索引_强约束_AI命令.md`
- `Assets/Plugins/ES/AICommands/检查_GameCoreRuntimeData重注入闭环_AI命令.md`
- `Assets/Plugins/ES/AICommands/GameManager模块接入_检查_AI命令.md`
- `Assets/Plugins/ES/AICommands/检查_GameManager模块生命周期_AI命令.md`

## Mandatory rule areas

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/GameCore边界（GameCore）`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）`
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/GameCoreGlobalData与AICommands_AI协作警告.md`
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/GameManager与存档（GameManagerSave）`

## Source and tests

- Search `Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/SO/GlobalEditorData` for root authoring data.
- Inspect `Assets/Plugins/ES/1_Design/ConfigKey/ESConfigKey.cs` for retained RuntimeData and ConfigKey contracts.
- Inspect `Assets/Plugins/ES/1_Design/Tests/ESGameManagerStaticModuleTests.cs` for static module behavior.
- Search the exact consumer symbol before changing a root field or injection API.
