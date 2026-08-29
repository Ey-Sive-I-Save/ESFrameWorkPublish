# ES 编辑器底层能力地图

状态：源码整理完成；Runtime/Unity 验收未运行（`runtime-not-run`）。
范围：ES 自有 Editor、ESLogic Editor/EditorPreview；不把 Unity 原生或第三方窗口计入 ES 底座。

## 总体结论

ES 已形成窗口生命周期、Presentation、公共 Preview、Workbench、Inspector/Drawer 和专业工具几条底层链路。当前主要问题不是缺少窗口数量，而是公共底座的接入成熟度不一致、AssetPackage 仍保留专用 PreviewScene 链路，以及 Runtime 证据尚未覆盖主路径。

## 能力分层

| 层级 | 当前能力 | 权威入口 | 状态 |
|---|---|---|---|
| 公共窗口底座 | SleepContract、Full/Transient、Owner/Pending、Suspend/Close/Unbind、标准 System Host | `Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs`、`Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/-ESMenuTreeWindow.cs` | 静态已成形，Runtime 未验收 |
| 公共预览底座 | Camera、Light、RT、PreviewScene、模型组、HideFlags/Layer、全局清理、资源 Scope | `Assets/Scripts/ESLogic/Runtime/EditorPreview/` | 公共底座存在；AssetPackage 尚未完全迁入 |
| 通用窗口壳 | MenuTree、OdinMenuTree、SinglePage、IMGUI、UI Toolkit Workbench | `Assets/Plugins/ES/Editor/ESMenuTreeWindow/`、`Assets/Scripts/ESLogic/Editor/Workbench/` | 功能完整度较高 |
| 作者工作台 | 资源、层级、稳定选择、2D/3D 视口、拖放、锁定、Inspector、Draft、Commit、问题抽屉 | `ESWorkbenchUIToolkitHost.cs`、`ESWorkbenchAuthoringContracts.cs` | 静态通过，Runtime 未验收 |
| Inspector/Drawer | ConfigKey、Input、Tag、Asset、Enum/String、Collection、Section、Serialized Mutation | `Assets/Plugins/ES/Editor/ESDrawer/` | 覆盖广，需逐类验证 Undo/Prefab/Multi-object |
| 交互公共块 | Command Palette、SearchDropdown、Popup、Dialog Service、反馈音效、Developer Cockpit | `Assets/Plugins/ES/Editor/EditorTools/` | 已存在，接入一致性需继续检查 |
| 专业工具 | AssetPackage、Resource、Shader、Stable Graph、Camera、Audio、Particle、Agent/Automation | 对应 `ESResPipeline`、`ESShader`、`ESGraphViewV2`、`ESAutomation` 等目录 | 业务能力存在，证据等级不一致 |

## 需要优先强化的共性

1. 统一 Preview 资源所有权，逐步收口 AssetPackage 的专用 Camera/Light/PreviewScene/RT。
2. 为每个生产窗口补齐 SurfaceKind、尺寸策略、首屏主动作、Owner、ReloadDomain、取消和清理证据。
3. 将 Draft、ChangeSet、Dirty、Undo/Redo、SessionState 和稳定 Selection 明确分层，禁止只刷新界面就认为领域状态已恢复。
4. 将窄窗口、高 DPI、ReloadDomain、PlayMode、重复打开、关闭重开和多窗口并行加入统一验收矩阵。
5. 对超大文件按职责拆分，但保持现有公共类型和入口兼容：Presentation Core、AssetPackage Bake Window、Workbench UI Toolkit Host、生命周期测试。

## 当前非声明

- 本地图不声明 Unity 编译、ReloadDomain、视觉布局、拖放、Profiler、内存或发布通过。
- 源码存在和静态测试存在不等于窗口实际可用。
- AssetPackage Preview 仍是待迁移的专用实现，不应被当成第二套可扩展公共标准。

