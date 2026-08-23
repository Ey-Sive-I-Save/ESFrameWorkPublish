# ES EditorWindow 休眠覆盖表

状态：代码合同已补齐；Unity ReloadDomain、PlayMode 和视觉验收仍需在无 `Temp/UnityLockfile` 时执行。

## 声明式窗口合同

直接继承 `EditorWindow`/`OdinEditorWindow` 的 ES 生产窗口默认获得完整休眠。
只有确实不应独立休眠的短生命周期窗口才声明
`ESWindowSleepContract(ESWindowSleepMode.Transient, reason)`，并通过
`allowSemiSleep:false`/`BindTransient` 接入；核心层在运行时拒绝合同与绑定模式不一致。
`Full` 枚举和 `BindFullSleep` 仍作为显式兼容入口保留，但不要求普通生产窗口重复声明。
MenuTree、SinglePage 和 Workbench 基类窗口继续由基类提供完整休眠默认值。

### 直接窗口逐项判定

| 类别 | 窗口 |
| --- | --- |
| 默认 Full | `ESCmdAgentWindow`、`ESAgentArtifactCandidateReviewWindow`、`ESStableGraphViewWindow`、`ESCompositeShaderBakeWindow`、`ESCompositeSSUMigrationWindow`、`ESTrackViewWindow`、`ESWorldDialogueEditorWindow`、`ESWorldMapSpaceEditorWindow` |
| 显式 Transient | `ESAdvancedDialogWindow`、`ESProgressCenterWindow`、`ESCommandPaletteWindow`、`ESCompactChoicePopup`、`ESCreateSkillWindow`、`ESTreeMenuShower`、`EditorInputDialog`、`ESWorkbenchPopupWindow` |

扫描范围为 `Assets/Plugins/ES/Editor` 和 `Assets/Scripts/ESLogic/Editor` 的生产源码，排除
Tests、Examples、Obsolete 和模板定义；共确认 16 个直接 ES Presentation 窗口类型。
基类派生窗口另由下方的默认覆盖表统一接入。`ES_Stand` 中的原生 Key Picker 单独列为
Presentation 边界外的明确排除项，不计入这 16 个窗口。

## 完整休眠

以下窗口是长生命周期 ES 工具，绑定正常流 System 宿主，并获得立即休眠、唤醒和恢复控制；
允许、自动/固定、全局策略统一收纳在“系统”菜单中：

| 窗口 | 宿主方式 | 生命周期入口 |
| --- | --- | --- |
| `ESWorldMapSpaceEditorWindow` | 页面 shell 的显式三域宿主 | `CreateGUI` / `OnDisable` |
| `ESWorldDialogueEditorWindow` | `EnsureStandardSystemActionBar` | `OnEnable` / `OnDisable` |
| `ESCompositeShaderBakeWindow` | `EnsureStandardSystemActionBar` | `OnEnable` / `OnDisable` |
| `ESCompositeSSUMigrationWindow` | `EnsureStandardSystemActionBar` | `OnEnable` / `OnDisable` |
| `ESAgentArtifactCandidateReviewWindow` | `EnsureStandardSystemActionBar` | `OnEnable` / `OnDisable` |
| `ESEditorFeedbackSoundSchemeWindow` | 单页窗口基类标准 System 宿主 | 基类生命周期 |
| `ESStableGraphViewWindow` | CreateGUI 中显式 System host | `CreateGUI` / `OnDisable` |
| `ESCmdAgentWindow` | `headerActionHosts` 显式宿主 | `CreateGUI` / `OnDisable` |
| `ESTrackViewWindow` | Odin toolbar 的显式 System host | `CreateGUI` / `OnDisable` |
| `ESMenuTreeWindow` / `ESOdinMenuTreeWindow` 派生窗口 | 基类标准 System host | 基类生命周期 |

### 基类自动覆盖的生产窗口

`ESMenuTreeWindow`、`ESOdinMenuTreeWindow`、`ESSinglePageWindow` 和
`ESSinglePageIMGUIWindow` 的生产派生窗口统一继承基类的 System 宿主、休眠按钮、
持久化偏好、单实例诊断和 ReloadDomain/PlayMode 恢复。当前源码扫描确认的窗口包括：

| 窗口族 | 生产窗口 |
| --- | --- |
| 资源与工具 | `ESAssetPackageBakeWindow`、`ESAssetPackageRecordPreviewWindow`、`ESResWindow`、`ESSODataInfoWindow`、`ESFontToolsWindow`、`ESLocalizationToolsWindow`、`SimpleToolsWindow` |
| 编辑器治理 | `ESInstaller`、`ESWindowLauncher`、`ESEditorHealthWindow`、`ESEditorThemeWindow`、`ESEditorFeedbackSoundSchemeWindow`、`ESDeveloperCockpitWindow` |
| 资源管线与自动化 | `ESAssetReleaseUploadWindow`、`ESResourceCollectionWorkflowWindow`、`ESResourceRuntimeMonitorWindow`、`ESAutomationCenterWindow`、`ESAIBrainWindow` |
| 内容与调试 | `EntityStatDebugWindow`、`EntityBasicInteractionDebugWindow`、`ESDynamicAtlasMonitorWindow`、`ESAudioCueTrimPreviewWindow`、`ESUIRiskAuditWindow`、`ESWorkbenchCaseStudyWindow`、`ESCameraTrackPreviewWindow`、`ESGameCoreDefinitionEditorWindow` |
| 工作台与临时检查器 | `ESWorkbenchIntegrationTestWindow`、`ESWorldBuilderWorkbenchWindow`、`ESTrackItemTemporaryInspectorWindow`、`ESTrackClipTemporaryInspectorWindow`、`ESTrackSkillDataTemporaryInspectorWindow` |

以下窗口也属于基类默认 Full，按用途补列，避免只依赖“窗口族”描述而漏查具体类型：

`ESAssetPackageRecordPreviewWindow`、`ESResWindow`、`ESSODataInfoWindow`、`ESFontToolsWindow`、
`ESLocalizationToolsWindow`、`SimpleToolsWindow`、`ESInstaller`、`ESWindowLauncher`、
`ESEditorHealthWindow`、`ESEditorThemeWindow`、`ESEditorFeedbackSoundSchemeWindow`、
`ESDeveloperCockpitWindow`、`ESAssetReleaseUploadWindow`、`ESResourceCollectionWorkflowWindow`、
`ESResourceRuntimeMonitorWindow`、`ESAutomationCenterWindow`、`ESAIBrainWindow`、
`EntityStatDebugWindow`、`EntityBasicInteractionDebugWindow`、`ESDynamicAtlasMonitorWindow`、
`ESAudioCueTrimPreviewWindow`、`ESUIRiskAuditWindow`、`ESWorkbenchCaseStudyWindow`、
`ESCameraTrackPreviewWindow`、`ESGameCoreDefinitionEditorWindow`、`ESWorkbenchIntegrationTestWindow`、
`ESWorldBuilderWorkbenchWindow`、`ESTrackItemTemporaryInspectorWindow`、
`ESTrackClipTemporaryInspectorWindow`、`ESTrackSkillDataTemporaryInspectorWindow`。

这份清单与当前源码扫描的 39 个 Full 类型对应；新增生产基类窗口仍继承默认值，只有显式
覆写/绑定为 Transient 才能关闭休眠。

新增生产派生窗口默认获得完整休眠；只有明确的 `OwnedSurface`/短生命周期例外才允许关闭，
并必须在源码合同或基类测试中登记原因。

类型级能力与运行时策略分开处理：`Transient` 表示该窗口类型不支持独立休眠，必须显式声明原因；
普通 Full 窗口仍可由用户在“系统”菜单中临时关闭“允许参与休眠”，但这不会把窗口类型改成
Transient。`OwnedSurface` 先继承窗口类型的默认 Full 能力，再由父子关系临时关闭独立休眠；
解除关系后恢复原类型能力，避免永久退化为不支持休眠。

`ESWindowFoundation.Bind` 的兼容调用现在也只创建正常流标准宿主，不再创建右上角绝对定位 fallback。窗口根节点被替换时，休眠核心会校验宿主所有权并重建正常流宿主。

`ESAssetReferKeyPickerWindow` 位于 `ES_Stand`，是原生 IMGUI `ShowAuxWindow` Key 选择器，
明确属于 ES Presentation 边界外的原生 Popup，不接入 ES Presentation；它不能被基础层按类型名
猜测并注入休眠控件，关闭由自身上下文失效流程治理。若未来要让它参与休眠，必须先把它迁入
Editor Presentation 宿主并为此类窗口单独设计 Popup 生命周期合同，不能在基础层按类型名强行绑定。

## 明确不支持独立休眠

短生命周期或具有原生模态/下拉语义的窗口必须显式使用 `allowSemiSleep:false`，避免休眠状态机接管它们：

- `ESAdvancedDialogWindow`、`ESProgressCenterWindow`
- `ESCommandPaletteWindow`、`ESCompactChoicePopup`
- `ESCreateSkillWindow`、`ESInputActionImportWindow`、`ESInputActionBindingImportWindow`
- `ESWorkbenchPopupWindow`、`ESTreeMenuShower`、临时输入对话框

这些窗口仍接入 ES 生命周期解绑，但显式使用 `allowSemiSleep:false` 或基类的
`ESWindow_SupportsSemiSleep => false`，因此不会出现独立
休眠按钮、自动休眠状态或跨窗口持久化休眠几何。当前已锁定的短窗口包括：
`ESAdvancedDialogWindow`、`ESCommandPaletteWindow`、
`ESCompactChoicePopup`、`ESCreateSkillWindow`、`ESWorkbenchPopupWindow`、
`ESTreeMenuShower` 和 `EditorInputDialog`。

两个 Input 导入窗口是基类级别的显式例外：它们承载 Unity 原生
InputAction 导入器，生命周期由调用方控制，不创建独立休眠槽。

`ESProgressCenterWindow` 是跨任务全局进度聚合面，按 P0 合同保持不参与自动半休眠；
它仍接入 ES 生命周期清理，但不生成独立休眠控制。

源码扫描收据：`Artifacts/editor-window-all-classes.txt`、
`Artifacts/editor-window-binding-scan.txt`、`Artifacts/es-base-derived-window-scan.txt`。

## 核心安全合同

- ReloadDomain 和 PlayMode 首次通知只保存一次休眠快照；重复通知不得用临时 Awake 几何覆盖快照。
- 生命周期暂停只拆 ES 视觉层，保留绑定槽；退出 PlayMode 前清理已经销毁的窗口，避免僵尸引用。
- 生命周期恢复必须等待新 `rootVisualElement.panel` 就绪后再注册回调和创建休眠控件；短暂未挂 panel 只允许有限次延迟重试，不能在空 panel 上重建。
- 主题失效、编译失败或面板重建不能把生命周期中的绑定槽直接清空；对话框在进入 PlayMode 时取消活动请求和队列，避免模态循环、异步校验与 owner 交互跨模式残留。
- 所有完整休眠窗口必须有稳定短标题、单实例协调身份或明确的多实例合同。
- owner/follow 关系只接受显式窗口或稳定 owner key，不扫描全部 `EditorWindow` 猜测归属。
