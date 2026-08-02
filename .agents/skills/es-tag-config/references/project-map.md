# ES tag and configuration project map

## Authorization command

- `Assets/Plugins/ES/AICommands/新增GameTag_AI命令.md`

## Mandatory rule areas

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/配置与稳定身份（IdentityConfig）`
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/Buff标签与数值（BuffTagValue）`
- Read the GameCore boundary rules when ConfigKey participates in root data or RuntimeData injection.

## Source and tests

- `Assets/Plugins/ES/1_Design/Tag/ESGameTag.cs`
- `Assets/Plugins/ES/1_Design/Tag/ESTag.cs`
- `Assets/Plugins/ES/1_Design/Tag/ESTagBakeTable.cs`
- `Assets/Plugins/ES/1_Design/Tag/ESTagRuntimeCatalog.cs`
- `Assets/Plugins/ES/1_Design/ConfigKey/ESConfigKey.cs`
- `Assets/Plugins/ES/1_Design/Tests/ESTagCatalogRuntimeTests.cs`
- `Assets/Plugins/ES/1_Design/Tests/ESConfigKeyTableTests.cs`
- Editor drawers: `Assets/Plugins/ES/Editor/ESDrawer/Normal/ESTagStableReferenceDrawer.cs` and `ESAssetConfigKeyDrawer.cs`
