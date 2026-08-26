# ES Editor Window Production Standard

This is the source-of-truth entry for editor-window static acceptance. It is intentionally split from Unity runtime acceptance.

## Required order

1. Classify the target as `EditorWindow`, `Workbench`, `InspectorDrawer`, `MenuAction`, `PreviewImport`, or `BackgroundService`.
2. Run `Invoke-ESEditorAvailability.ps1` with the explicit `-TargetKind` when inference is uncertain.
3. Read the target-kind matrix and inspect the `EW-01` through `EW-20` result set.
4. Treat `StaticCompleteRuntimePending` as a completed source/configuration review, not as proof of Unity behavior.
5. Request bounded runtime authorization only for claims that static analysis cannot prove.

## Rule contract

The stable rule definitions are in `.agents/skills/es-editor-availability-validator/references/editor-rule-registry.json`. Each rule declares its scope, static checks, runtime checks, severity, and required evidence boundary. A rule outside the target kind is `not-applicable`; it must not be converted into a false failure.

## Static acceptance minimum

Static review must cover source scope/UTF-8, ES lifecycle integration, mutation boundaries, deterministic state/recovery paths, size strategy, and all applicable EW rules. Static evidence may prove implementation structure and deterministic replay, but cannot claim actual DPI geometry, Unity panel mounting, reload behavior, or profiler measurements.

## Runtime boundary

Runtime is required only for the selected acceptance/release profile and must use a one-time authorization bound to the task, PlanHash, command, target paths, time budget, timeout, and stop condition. Missing runtime evidence is reported as `runtime-not-run` or `Degraded` in development diagnostics; it is not a reason to rewrite source that already passed static review.

## Human/AI route

For creating, refactoring, or accepting an ES editor tool: read this standard, then `es-editor-tooling`, then `es-editor-availability-validator`, and finally `es-release-acceptance` for release claims.

## 新工具研发强制接入提醒

后续新增或改造 ES 编辑器工具必须先归类为 ES 主窗口、ES 子窗口/临时 Inspector、Popup、Dialog、Preview 或 Utility。普通 Unity/第三方窗口不得调用 `ESWindowFoundation`；ES 窗口必须显式声明或继承 `[ESWindowSleepContract(mode, surfaceKind, reason)]`，缺少合同或使用 `Unknown` SurfaceKind 的窗口会在任何 VisualTree 改写前拒绝接入。SurfaceKind 是显式语义，不得从类名、后缀或打开方式推断。

- Workspace、Inspector、Preview 声明 `Full`，进入只调用 `BindFullSleep` 或标准 System Host 入口。
- Popup、Dialog、Utility 声明带原因的 `Transient`，进入只调用 `BindTransient`。
- 标准 System Host、`FollowOwner`、`OwnedSurface` 和 Pending child 只对 `Full + Workspace/Inspector/Preview` 开放；Transient 即使 mode-kind 合法也必须在 VisualTree、binding、Pending 和全局 hook 变更前 fail-closed。`Independent`、`ClearSleepOwner` 和 Pending 清理入口必须始终可用于安全退出。
- 内容/VisualTree 重建只调用 `Unbind(window)`；任何 `rootVisualElement.Clear()` 或整树替换都必须先解绑，重建完成后再按合同重新 Bind。`OnDisable` 只调用可恢复的 `Suspend(window)`；`OnDestroy` 只调用永久退出的 `Close(window)`。禁止再用 `Unbind(window, bool)` 猜测关闭语义，三条路径都必须可重复执行。
- `rootVisualElement.panel` 尚未挂载时只允许建立逻辑 binding、owner 和单实例状态；不得提前创建回调、schedule、overlay 或系统控件。Resume 只有在 overlay、Transient/Full 对应宿主以及 Full 系统控件都真实挂载后才算成功。Suspend 期间修改稳定偏好必须合并到首次快照，不得覆盖休眠状态和 Awake 几何。
- FollowOwner 窗口的公开打开 API 必须接受具体 ES owner 类型，禁止暴露任意 `EditorWindow owner`；同一工具同时支持 Independent 与 FollowOwner 时必须使用不同重载表达，无 owner 重载只进入 Independent，带 owner 重载的 owner 不得可选且必须在创建窗口前拒绝 null。首次打开后立即 `SetSleepOwner`，同时序列化稳定 `ownerKey`。ReloadDomain 后由子窗口 `RegisterPendingSleepOwner`、父窗口 `ResolvePendingSleepOwners` 恢复，父窗口真实关闭必须永久脱离。当前 6 个生产 FollowOwner 为 3 个 Track 临时 Inspector、`ESCameraTrackPreviewWindow`、`ESAssetPackageRecordPreviewWindow` 和 `ESWorldDialogueEditorWindow`。
- 对话框只从 `ESDialogService` 进入；生产库存中 `SurfaceKind.Dialog` 只能对应 `ESAdvancedDialogWindow`，无论类型名是否包含 Dialog 都不得自建第二个 Dialog surface，也禁止直接调用兼容层 `ESAdvancedDialogWindow.Show*`。`SurfaceKind.Popup` / `Utility` 只是生命周期分类，不授予原生模态能力；生产 ES Editor 源码不得调用或缓存 `EditorWindow.ShowModalUtility`，唯一合法引用固定在 `ESAdvancedDialogWindow.Internal_OpenFromDialogService(bool modal)`，并且该真实内部入口只能由同文件 `ESDialogService.OpenNow` 调用。排队、替换、owner 失效、取消、ReloadDomain 和关闭必须由同一 operation 完成一次全部 callback/Task subscriber；首个终态不可被后续取消覆盖，父对话框关闭前必须先收敛活动与排队中的子请求。
- 生产主窗口使用 `GetWindow`/既有 ES 单实例协调器；`CreateInstance` 只允许集中服务、受控 Popup 和显式压力测试例外。

每个工具交付前必须明确：稳定身份（持久 Unity 对象使用 `GlobalObjectId`；managed、未保存对象和 mixed selection 只能保留当前域内状态，不得写入跨 Reload 的 `SessionState`）、ownerKey 与安全退出路径、ReloadDomain 恢复、重复打开策略、取消与回调清理、静态性能边界，以及尚未取得的 Unity/Profiler 证据。对应静态门禁位于 `ESWindowSleepLifetimeTests`；缺少 Runtime 证据时只能标记为 `RuntimePending`，不能宣称 Unity 交互或 Profiler 已通过。

`ES_EDITOR_NATIVE_DIALOG_BASELINE.txt` 只冻结历史 `EditorUtility.DisplayDialog*` 迁移债务，不是统一生命周期的验收证明。新工具和任何新增/修改的对话交互必须直接进入 `ESDialogService`，不得通过增加基线额度完成交付。
