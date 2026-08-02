# ES resource pipeline project map

## Authorization commands

- `Assets/Plugins/ES/AICommands/信息_资源治理上下文_AI命令.md`
- `Assets/Plugins/ES/AICommands/方案_资源分离工作流_AI命令.md`
- `Assets/Plugins/ES/AICommands/执行_资产包预览小修复_AI命令.md`
- `Assets/Plugins/ES/AICommands/执行_资产包导出链路小修复_AI命令.md`
- `Assets/Plugins/ES/AICommands/资产包分离_预览工作流安全检查_AI命令.md`
- `Assets/Plugins/ES/AICommands/资产包分离_导出链路体检_AI命令.md`
- `Assets/Plugins/ES/AICommands/资源依赖与未使用资产_分析_AI命令.md`

## Mandatory rule areas

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/资源运行时与发布（RuntimeAssets）`
- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）`
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）`

## Source map

- Authoring: `Assets/Plugins/ES/0_Stand/_Res/Master/Shared/SoSupport/ESAssetLibrary.cs`
- Books/pages: `Assets/Plugins/ES/0_Stand/_Res/Master/Shared/SoSupport/ESAssetBookAndPage.cs`
- Scope: `Assets/Plugins/ES/0_Stand/_Res/Runtime/ESAssetScope.cs`
- Runtime loader/provider: search `Assets/Plugins/ES/0_Stand/_Res/Runtime` for `ESRuntimeAssetLoader` and `ESRuntimeAssetProvider`.
- Editor contracts: `Assets/Plugins/ES/Editor/ESResPipeline/ESAssetPipelineContracts.cs`
- Resource plan and baking: `Assets/Plugins/ES/Editor/ESResPipeline/ResourcePlan`
- Runtime monitoring: `Assets/Plugins/ES/Editor/ESResPipeline/Windows/ESResourceRuntimeMonitorWindow.cs`
