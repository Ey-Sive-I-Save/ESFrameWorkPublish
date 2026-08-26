# ES EditorWindow 休眠覆盖表

状态：静态代码合同与编译门禁已补齐；Unity ReloadDomain、PlayMode、交互和 Profiler 仍为 `runtime-not-run`。

## 声明式窗口合同

直接继承 `EditorWindow`/`OdinEditorWindow` 的 ES 生产窗口必须显式声明合同；缺少
`ESWindowSleepContract` 的 Unity/第三方窗口会在 VisualTree 修改前 fail-closed。
合同同时声明休眠能力和界面语义：Workspace、Inspector、Preview 使用
`ESWindowSleepContract(ESWindowSleepMode.Full, surfaceKind)`；Popup、Dialog、Utility 使用
`ESWindowSleepContract(ESWindowSleepMode.Transient, surfaceKind, reason)` 并通过
`BindTransient` 接入。核心层在运行时拒绝 mode 与 SurfaceKind 不一致、Unknown 分类，
以及任何并非 `ESAdvancedDialogWindow` 的 Dialog surface。SurfaceKind 不得从类型名推断。
长期 ES 窗口通过 `BindFullSleep` 或标准 System Host 接入。
MenuTree、SinglePage 和 Workbench 基类窗口继续由基类提供完整休眠默认值。

当前 49 项权威库存逐类型登记 SurfaceKind：32 个 Workspace、4 个 Inspector、
5 个 Popup、1 个 Dialog、3 个 Preview、4 个 Utility；与 39 Full / 10 Transient
形成可机械校验的二维合同。

### 直接窗口逐项判定

| 类别 | 窗口 |
| --- | --- |
| 默认 Full | `ESCmdAgentWindow`、`ESAgentArtifactCandidateReviewWindow`、`ESStableGraphViewWindow`、`ESCompositeShaderBakeWindow`、`ESCompositeESNativeMigrationWindow`、`ESTrackViewWindow`、`ESWorldDialogueEditorWindow`、`ESWorldMapSpaceEditorWindow` |
| 显式 Transient | `ESAdvancedDialogWindow`、`ESAssetReferKeyPickerWindow`、`ESProgressCenterWindow`、`ESCommandPaletteWindow`、`ESCompactChoicePopup`、`ESCreateSkillWindow`、`ESTreeMenuShower`、`ESWorkbenchPopupWindow` |

扫描范围为 `Assets/Plugins/ES/Editor` 和 `Assets/Scripts/ESLogic/Editor` 的生产源码，排除
Tests、Examples、Obsolete 和模板定义；共确认 16 个直接 ES Presentation 窗口类型：
8 个 Full、8 个 Transient。另有 33 个 ES 基类派生窗口：31 个 Full、2 个 Transient。
合计 49 个生产 ES 窗口，即 39 个 Full、10 个 Transient。Unity 原生窗口与第三方窗口不在接入范围内，且不得调用
`ESWindowFoundation`。

## 完整休眠

以下窗口是长生命周期 ES 工具，绑定正常流 System 宿主，并获得立即休眠、唤醒和恢复控制；
允许、自动/固定、全局策略统一收纳在“系统”菜单中：

| 窗口 | 宿主方式 | 生命周期入口 |
| --- | --- | --- |
| `ESWorldMapSpaceEditorWindow` | 页面 shell 的显式三域宿主 | `CreateGUI` / `Suspend` / `Close` |
| `ESWorldDialogueEditorWindow` | `EnsureStandardSystemActionBar` | `OnEnable` / `Suspend` / `Close` |
| `ESCompositeShaderBakeWindow` | `EnsureStandardSystemActionBar` | `OnEnable` / `Suspend` / `Close` |
| `ESCompositeESNativeMigrationWindow` | `EnsureStandardSystemActionBar` | `OnEnable` / `Suspend` / `Close` |
| `ESAgentArtifactCandidateReviewWindow` | `EnsureStandardSystemActionBar` | `OnEnable` / `Suspend` / `Close` |
| `ESEditorFeedbackSoundSchemeWindow` | 单页窗口基类标准 System 宿主 | 基类生命周期 |
| `ESStableGraphViewWindow` | CreateGUI 中显式 System host | `CreateGUI` / `Suspend` / `Close` |
| `ESCmdAgentWindow` | `headerActionHosts` 显式宿主 | `CreateGUI` / `Suspend` / `Close` |
| `ESTrackViewWindow` | Odin toolbar 的显式 System host | `CreateGUI` / `Suspend` / `Close` |
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

`ESAssetReferKeyPickerWindow` 已迁入 `ES_Editor`；`ES_Stand` 只保留无 Editor 依赖的桥接委托。
该窗口使用 `GetWindow` 单实例、显式 Transient 合同以及
`BindTransient` / `Suspend` / `Close`，上下文在 ReloadDomain 后丢失时安全关闭。

## 明确不支持独立休眠

短生命周期或具有原生模态/下拉语义的 ES 窗口必须显式使用 `BindTransient` 或基类
`ESWindow_SupportsSemiSleep => false`，避免休眠状态机接管它们：

- `ESAdvancedDialogWindow`、`ESAssetReferKeyPickerWindow`、`ESProgressCenterWindow`
- `ESCommandPaletteWindow`、`ESCompactChoicePopup`
- `ESCreateSkillWindow`、`ESInputActionImportWindow`、`ESInputActionBindingImportWindow`
- `ESWorkbenchPopupWindow`、`ESTreeMenuShower`

这些窗口仍接入 ES 生命周期，但显式使用 `BindTransient` 或基类的
`ESWindow_SupportsSemiSleep => false`，因此不会出现独立
休眠按钮、自动休眠状态或跨窗口持久化休眠几何。当前已锁定的短窗口包括：
`ESAdvancedDialogWindow`、`ESAssetReferKeyPickerWindow`、`ESProgressCenterWindow`、`ESCommandPaletteWindow`、
`ESCompactChoicePopup`、`ESCreateSkillWindow`、`ESWorkbenchPopupWindow`、
`ESTreeMenuShower`。文本输入已统一进入 `ESDialogService.TryShowTextInputModal`，不再保留独立 `EditorInputDialog`。

两个 Input 导入窗口是基类级别的显式例外：它们承载 Unity 原生
InputAction 导入器，生命周期由调用方控制，不创建独立休眠槽。

`ESProgressCenterWindow` 是跨任务全局进度聚合面，按 P0 合同保持不参与自动半休眠；
它仍接入 ES 生命周期清理，但不生成独立休眠控制。

当前权威库存由 `ESWindowSleepLifetimeTests.ProductionWindowInventoryHasExplicitOrInheritedContracts`
从源码重算并锁定。`Artifacts/editor-window-*.txt` 是历史快照，不作为本轮当前性证据。

## ES 窗口类型接入矩阵

统计口径固定为：直接窗口 `16 = 8 Full + 8 Transient`，基类派生窗口
`33 = 31 Full + 2 Transient`，总库存 `49 = 39 Full + 10 Transient`。下表中的
`FollowOwner` 6 个窗口已经计入 39 个 Full，不是额外库存。

| 类别（数量） | 实例策略 | 合同 | typed owner | ownerKey | Bind | Suspend | Close | ReloadDomain |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 直接 Full 独立窗口（7） | 普通打开入口使用 `GetWindow`；普通生产窗口禁止直接 `CreateInstance` | `Full + Independent` | 无 | 无 | `BindFullSleep` 或标准 System Host | `OnDisable -> Suspend` | `OnDestroy -> Close` | 保留标量偏好；panel 就绪后重建 action host 和视觉层 |
| 基类 Full 独立窗口（26） | 基类 `OpenWindow` / `GetWindow` 单实例；多实例必须声明稳定 coordinator | 基类默认 `Full + Independent` | 无 | 无 | MenuTree、SinglePage 或 Workbench 基类统一绑定 | 基类 `OnDisable -> Suspend` | 基类 `OnDestroy -> Close` | 基类恢复标量状态、窗口身份和标准 System Host |
| Track 临时 Inspector（3） | 每个具体类型由 `GetWindow<T>(true, ...)` 复用并重新绑定目标；普通打开在 `OpenIndependent` 前拒绝 null owner | `Full + FollowOwner` | 是：`OpenFor(..., ESTrackViewWindow owner)`，不可省略 | `ES.TrackView.Window` | Inspector 基类完整绑定；打开后提交显式 owner | 基类 `Suspend` | 基类 `Close` | 以稳定目标 key 恢复；仅 ReloadDomain 恢复时允许父未就绪登记 Pending；目标丢失则安全关闭 |
| 世界对话编辑器（1：`ESWorldDialogueEditorWindow`） | `GetWindow` 单实例；无 owner 重载与必需 owner 重载分离 | `Full`；`OpenFor(asset)` 为 `Independent`，`OpenFor(asset, ESWorldBuilderWorkbenchWindow owner)` 为 `FollowOwner` | `FollowOwner` 重载必须传 `ESWorldBuilderWorkbenchWindow`，null 在创建窗口前 fail-closed | `ES.WorldBuilder.Window` | 显式标准 System Host / Full 绑定 | `OnDisable -> Suspend` | `OnDestroy -> Close` | 序列化 ownerKey；仅 ReloadDomain 恢复时允许父未就绪登记 Pending；真实父关闭后保持脱离 |
| Full Preview（2：`ESCameraTrackPreviewWindow`、`ESAssetPackageRecordPreviewWindow`） | `GetWindow` 单实例并替换当前预览上下文；普通打开在 `GetWindow` 前拒绝 null owner | `Full + FollowOwner` | 是：不可省略的 `ESTrackViewWindow` / `ESAssetPackageBakeWindow` | `ES.TrackView.Window` / `ES.AssetPackageBake.Window` | SinglePage 基类完整绑定 | 基类 `Suspend`，同时停止临时预览活动 | 基类 `Close`，同时释放预览资源 | owner 先恢复则立即解析，子先恢复仅在 ReloadDomain 路径登记 Pending；上下文失效安全关闭 |
| ES Dialog（1） | 仅 `ESDialogService` 可按 operation 创建受控多实例 | `Transient` | `request.owner` 必须显式且存活；无 owner 时只能显式启用主工作区 fallback | 不使用休眠 ownerKey；使用稳定 `dialogId` | `BindTransient` | `OnDisable -> Suspend` | service 取消/完成关闭，`OnDestroy -> Close` | 活动、排队和异步请求确定性取消，不恢复模态 operation |
| 资源 Key Picker（1） | Editor bridge 进入，`GetWindow<ESAssetReferKeyPickerWindow>` 单实例 | `Transient` | 无休眠 owner；仅持有本次选择上下文 | 无 | `BindTransient` | `OnDisable -> Suspend` | `OnDestroy -> Close` | 委托上下文无法跨域恢复，检测丢失后安全关闭 |
| 进度中心（1） | `GetWindow<ESProgressCenterWindow>` 单实例聚合 | `Transient` | 无 | 无 | `BindTransient` | `OnDisable -> Suspend` | `OnDestroy -> Close` | 不恢复窗口 operation；由进度服务当前快照重新投影 |
| 其他直接 Transient（5） | 命令面板、创建窗口、Tree Popup 使用 `GetWindow`；两个受控 Popup 工厂维持唯一 active instance | `Transient` | Popup 只接收显式 host 做 interaction hold，不建立休眠 owner 关系 | 无 | `BindTransient` | `OnDisable -> Suspend` | `OnDestroy -> Close` | 只恢复允许的标量状态；回调、anchor 或上下文丢失时安全关闭 |
| InputAction 导入窗口（2） | 基类 `GetWindow` utility 单实例，重开时替换临时导入上下文 | 显式 `Transient`，`ESWindow_SupportsSemiSleep => false` | 无窗口 owner；调用方提供 `SerializedObject` 上下文 | 无 | 基类按声明进入 `BindTransient` | 基类 `Suspend` | 基类 `Close` | 不持久化原生导入上下文；上下文失效时不恢复独立窗口状态 |

FollowOwner 当前覆盖 6 个窗口：3 个 Track 临时 Inspector、
`ESCameraTrackPreviewWindow`、`ESAssetPackageRecordPreviewWindow`、
`ESWorldDialogueEditorWindow`。对应 ownerKey 分别为 `ES.TrackView.Window`、
`ES.AssetPackageBake.Window` 和 `ES.WorldBuilder.Window`；公开打开 API 只接受不可省略的具体 ES owner
类型，并在 `GetWindow` / `OpenIndependent` 前拒绝 null。`RegisterPendingSleepOwner` 只服务于
ReloadDomain 后子窗口先恢复、父窗口尚未完成绑定的恢复路径，不是普通打开的 owner fallback。

此矩阵只治理 ES 自有窗口。普通 Unity `EditorWindow`、Unity 内置窗口与第三方窗口既不要求
支持 ES 休眠，也禁止绑定 `ESWindowFoundation`、注册 ES ownerKey 或被基础层按类型名注入控件。

## Section identity 稳定恢复证据

`ESWindowSleepLifetimeTests.SectionIdentityUsesDurableGlobalIdsAndExplicitTransientFallback` 锁定
持久 Unity 对象使用 `GlobalObjectId`、多选 identity 排序后组合，managed target 使用域内弱表 token，
并且 transient、managed 和 mixed identity 均不能生成持久 `SessionState` key。

`ESWindowSleepLifetimeTests.SectionIdentityRestoresAcrossPropertyTreeRebuildsWithoutTransientLeakage`
使用三个临时持久 Probe Asset 和真实 Odin `PropertyTree` 验证：销毁并重建 Tree 后恢复同一 section；
同一持久多选集合交换顺序仍得到同一 `MultiGlobal` identity；mixed / managed target 不生成 key，
也不会覆盖持久对象已经保存的 section。该证据已进入编辑器测试程序集编译门禁；Unity Test Runner
执行状态仍为 `runtime-not-run`。

## 核心安全合同

### Owner 关系进入/退出矩阵

| `ESWindowSleepLinkMode` | `owner` | 结果 |
| --- | --- | --- |
| `Independent` | `null` | 接受；清除当前关系和该子窗口的 Pending 记录，恢复为独立窗口 |
| `Independent` | 非空 | 拒绝且不改写已有关系；独立模式不得保留隐藏 owner 引用 |
| `FollowOwner` | child 与非空 owner 均为 `Full + Workspace/Inspector/Preview` | 接受；同步 owner 的休眠/唤醒状态 |
| `FollowOwner` | child 或 owner 为 Transient、`null`、自身或非法 Surface | fail-closed；验证失败前不得创建 binding 或改写 Pending |
| `OwnedSurface` | child 与非空 owner 均为 `Full + Workspace/Inspector/Preview` | 接受；临时隐藏子窗口的独立休眠能力 |
| `OwnedSurface` | child 或 owner 为 Transient、`null`、自身或非法 Surface | fail-closed；不得让无宿主内容静默失去休眠能力 |
| 未知枚举值 | 任意 | 拒绝且不改写已有关系 |

真实 owner `Close` 必须先对当前子关系做快照，再把 Core 状态全部降为
`Independent`；`OwnedSurface` 同时恢复进入关系前的类型能力和用户允许值。完成 Core
状态后才通知 `IESWindowSleepRelationshipState`，每个通知独立隔离异常。回调即使抛错，或在
回调内关闭另一个子窗口，也不得中断 owner 自身和其他子关系的安全退出。

`ResolvePendingSleepOwners` 会登记唯一的活动 `ownerKey`。因此 ReloadDomain 后父窗口先恢复时，
后登记的子窗口会立即解析；子窗口先恢复时则保留 Pending，直到父窗口完成 Foundation 绑定。
普通公开打开入口不得登记 Pending 或以 null owner 创建子窗口。重复 key 由第二个活动 owner
fail-closed，真实 `Close` 释放 key 并清除尚未解析的残留 Pending。

- ReloadDomain 和 PlayMode 首次通知只保存一次休眠快照；重复通知不得用临时 Awake 几何覆盖快照。
- Suspend 期间修改允许休眠、自动/固定、短标题或停靠落点时，只合并这些稳定配置；首次保存的休眠状态与 Awake 几何必须保持不变，成功恢复后下一轮 Suspend 才能捕获新快照。
- 生命周期暂停只拆 ES 视觉层，保留绑定槽；退出 PlayMode 前清理已经销毁的窗口，避免僵尸引用。
- 内容重建只允许 `Unbind`，`OnDisable` 只允许 `Suspend`，`OnDestroy` 只允许 `Close`；只有真实 `Close` 才永久解除 FollowOwner。
- 首次进入或生命周期恢复都必须等待新 `rootVisualElement.panel` 就绪后再注册回调、schedule 和休眠控件；detached root 只允许建立逻辑 binding、owner 与单实例状态，短暂未挂 panel 只允许有限次延迟重试。
- 有限恢复重试耗尽后禁止转成永久轮询；`ESWindowPresentationHealthSnapshot.ResumeRetryExhausted` 必须暴露该状态，下一次显式 Bind、panel attach 或生命周期恢复才能重置预算并重试。
- 主题失效、编译失败或面板重建不能把生命周期中的绑定槽直接清空；对话框在进入 PlayMode 时取消活动请求和队列，避免模态循环、异步校验与 owner 交互跨模式残留。
- 所有完整休眠窗口必须有稳定短标题、单实例协调身份或明确的多实例合同。
- owner/follow 关系只接受显式窗口或稳定 owner key，不扫描全部 `EditorWindow` 猜测归属。

## Dialog 迁移边界

新建 ES Dialog、任意命名的第二个 `SurfaceKind.Dialog` EditorWindow 和兼容层
`ESAdvancedDialogWindow.Show*` 的生产调用均被门禁禁止，统一入口是 `ESDialogService`。
`ESConfirmPromptWindow`、`ESQuestionSheet` 等不带 Dialog 后缀的名称也不能绕过分类合同。
Popup / Utility 的 `Transient` 声明不构成模态能力授权。生产 ES Editor 源码中的
`EditorWindow.ShowModalUtility` 调用、方法组和转发全部被静态门禁拒绝；唯一引用必须是
`ESAdvancedDialogWindow.Internal_OpenFromDialogService(bool modal)` 内的一次直接调用；该入口的
唯一调用点同时锁定为同文件 `ESDialogService.OpenNow`。
`ES_EDITOR_NATIVE_DIALOG_BASELINE.txt` 仍冻结 82 个
历史文件中的 462 次 `EditorUtility.DisplayDialog*` 文本匹配；这是待逐项迁移的同步调用债务，
只能证明未新增路径和未增长数量，不能证明旧入口已经统一。新增或修改对话交互不得进入该基线。
源码发现必须按严格 UTF-8 处理 BOM，并拒绝用类型别名、派生静态 receiver、方法组或字符串/插值
表达式隐藏窗口创建入口；原生 Dialog 的逐文件计数与调用点指纹必须同时匹配。

## 后续新工具研发强制检查清单

- [ ] 先确认窗口属于 ES 自有生产工具；Unity 内置、普通 Unity 或第三方窗口不得接入 ES Foundation。
- [ ] 直接继承 `EditorWindow` / `OdinEditorWindow` 时显式声明 `ESWindowSleepContract(mode, surfaceKind, reason)`；基类派生窗口也要在 49 项权威库存的后继清单中逐类型登记并确认继承结果符合用途。
- [ ] 明确选择 `Full` 或带具体原因的 `Transient`，同时选择 Workspace、Inspector、Popup、Dialog、Preview、Utility 之一；禁止 Unknown、mode-kind 错配、运行时把 Transient 提升成 Full，或用类型名白名单绕过合同。
- [ ] 普通窗口使用 `GetWindow` / 基类打开入口；需要多实例时实现稳定 coordinator 合同。`CreateInstance` 只允许由受控 Dialog/Popup 工厂集中创建并清理。
- [ ] 后台配置读取和依赖检查必须进入无窗口依赖服务；不得为了复用逻辑创建隐藏窗口、临时窗口 Lease 或第二套窗口生命周期。
- [ ] Full 窗口使用 `BindFullSleep` 或正常流标准 System Host；Transient 使用 `BindTransient`，不得创建绝对定位 fallback action host。
- [ ] 任何 `rootVisualElement.Clear()` 或整树替换前先 `Unbind`，完成后重新 Bind；`OnDisable` 使用 `Suspend`，`OnDestroy` 使用 `Close`；不得新增旧式 `Unbind(window, bool)` 调用。
- [ ] `FollowOwner` 的公开打开 API 接收不可省略的具体 ES owner 类型，在创建窗口前拒绝 null，并声明稳定 ownerKey；只有 ReloadDomain 恢复可登记 Pending，父窗口在 `ESWindow_OnFoundationBound` 解析 Pending，真实关闭释放 key。
- [ ] ReloadDomain 只序列化可恢复的稳定标量、资产 identity 和 ownerKey；委托、anchor、临时对象或原生上下文丢失时必须安全关闭。
- [ ] Dialog 仅走 `ESDialogService`，且生产 `SurfaceKind.Dialog` 只能是 `ESAdvancedDialogWindow`；提供稳定 `dialogId`，显式传 owner 或显式声明主工作区 fallback；首个终态获胜，父关闭先取消活动与排队子请求；不得直接调用兼容 `Show*`、以 Popup/Utility 分类调用 `EditorWindow.ShowModalUtility`，或扩大原生 Dialog 基线。
- [ ] 新增 section 导航必须证明 PropertyTree 重建稳定、多选顺序无关，以及 mixed / managed target 不写持久 SessionState。
- [ ] 更新生产窗口库存、Transient 集合、FollowOwner 数量和本文矩阵；运行静态门禁、目标程序集编译及 UTF-8 / `git diff --check`。
- [ ] 获准启动 Unity 后完成下方运行验收；Preview 或高频窗口还必须提供 Profiler 和资源释放证据。

## Unity 与 Profiler 运行验收矩阵

以下项目尚未在 Unity Editor 内执行，状态统一为 `runtime-not-run`。执行前需确认可独占目标项目，
约定测试预算、单项超时和失败停止条件；结果应记录 Unity 版本、测试时间、日志和 Profiler capture。

| 场景 | 覆盖对象 | 操作与通过条件 | 当前状态 |
| --- | --- | --- | --- |
| 重复打开 | Full 主窗口、3 个临时 Inspector、Popup、Dialog、Preview、Key Picker | 连续触发打开入口；单实例窗口只聚焦/重配既有实例，受控多实例按 coordinator 或 operation 去重，无额外 Foundation 槽和孤立窗口 | `runtime-not-run` |
| 关闭再打开 | 上述全部 ES 窗口类别 | 关闭后确认 binding、interaction hold、ownerKey 和资源已释放；重开可重新绑定且不继承已关闭实例的 owner/上下文 | `runtime-not-run` |
| ReloadDomain | Independent Full、6 个 FollowOwner、所有 Transient、section navigator | 分别覆盖父先/子先恢复；稳定状态和 section 恢复，Pending 最终清空；不可恢复的 transient 上下文确定性关闭 | `runtime-not-run` |
| 父子关闭顺序 | 6 个 FollowOwner 及受 owner 约束的 Popup/Dialog | 分别执行子先关、父先关、父回调中关闭另一子窗口；关系全部降为 Independent 或关闭，通知异常不阻断其余清理 | `runtime-not-run` |
| Dialog 取消与异常 | 活动、排队、同步模态、异步校验、父子 Dialog | 覆盖取消 token、Escape、父关闭、验证异常、关闭异常；每个 operation 只完成一次，队列可继续推进，现场无 live orphan | `runtime-not-run` |
| PlayMode 往返 | Full、Transient、Dialog、Preview | 进入/退出时快照只保存一次；不会以临时 Awake 几何覆盖，Dialog 不跨模式残留，预览临时资源被释放或按合同重建 | `runtime-not-run` |
| Profiler 空闲基线 | 打开的 Full / FollowOwner / Transient 窗口 | 记录 EditorLoop、Update、Repaint、GC Alloc；休眠核心空闲时无持续分配、无全窗口扫描，ownerKey 查询与解析不产生随窗口数增长的热路径回归 | `runtime-not-run` |
| Profiler 交互与资源 | 频繁打开/关闭 Popup、Dialog、Preview，切换休眠和 section | 对比操作前后实例数、binding 数、GC Alloc 与预览资源；关闭后回到稳定基线，无累计回调、RenderTexture、Preview 或任务引用 | `runtime-not-run` |
