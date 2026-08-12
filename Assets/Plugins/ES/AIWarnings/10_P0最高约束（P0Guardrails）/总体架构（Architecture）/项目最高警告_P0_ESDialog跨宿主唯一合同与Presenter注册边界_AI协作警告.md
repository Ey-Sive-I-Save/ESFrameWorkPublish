# P0：ESDialog 跨宿主唯一合同与 Presenter 注册边界

> 状态：现行架构决策，源码迁移实施中。不得将规则写入视为 Runtime Presenter、Unity 实机或 Player 验收完成。
>
> 适用范围：`ESDialog`、通用 Request/Result/Values、`IESDialogPresenter`、Editor Presenter、未来 Runtime Presenter 及其生命周期。

## 唯一权威

- `ESDialog`、`ESDialogRequest`、`ESDialogResult`、`ESDialogValues`、`ESDialogHost`、`IESDialogPresenter` 的唯一权威位于 `ES_Stand`。
- 禁止在 `ES_Editor`、具体 Runtime UI 或业务程序集平行定义同名门面、第二套队列总线或兼容别名。
- `ES_Stand` 禁止引用 `UnityEditor`、EditorWindow、EditorPrefs、具体 Runtime UI、场景 UI Root 或业务模块。
- `ESAdvancedDialogRequest`、ObjectField、文件选择、VisualElement 插槽、Owner EditorWindow 与同步 Modal 保留为 Editor 高级能力，不伪装为跨宿主合同。

## Presenter 注册

- 注册必须按 `ESDialogHost.Editor` 与 `ESDialogHost.Runtime` 隔离；Presenter 禁止使用 `Auto` 注册。
- Editor Presenter 只通过 Editor AssemblyStream 轻量注册。不得新增普通 `[InitializeOnLoad]`、静态构造器或资产扫描入口。
- Runtime Presenter 只允许由 Runtime UI Root、GameCore 或产品明确 Bootstrap 显式注册。禁止恢复 Runtime AssemblyStream、运行时程序集扫描或反射发现。
- 每次成功注册返回带单调递增 Generation 的 Lease。旧 Lease 释放不得注销更新一代 Presenter。
- 同一 Host 已注册时必须明确拒绝，禁止无声覆盖。
- Presenter 注销必须确定性结束其活动请求和等待队列；不得留下永不完成的 Task、静态窗口引用或取消令牌。

## Host 路由

- 显式 Host 只路由到对应 Presenter；缺失时返回 `HostUnavailable`，禁止回退 Unity 原生对话框或其他 Host。
- `Auto` 仅在当前恰好存在一个 Presenter 时可用。
- Editor 与 Runtime Presenter 同时存在时，`Auto` 必须返回 `AmbiguousHost`；禁止使用 `Application.isPlaying`、焦点窗口或调用程序集猜测。
- Play Mode 中需要显示游戏内 UI 的调用必须显式选择 Runtime；编辑器工具必须显式选择 Editor。

## 请求与结果

- 请求提交时必须形成深快照；调用方后续修改标题、字段、选项或集合不得改变已提交请求。
- Presenter 必须声明能力。缺失 Text、Choice、MultiChoice、Recommendation、AsyncValidation 等能力时明确返回 `CapabilityUnavailable`，禁止静默丢字段或降级语义。
- StableId、FieldId、OptionId 是协议身份；显示文本不得作为业务稳定键。
- 对话框确认只表示用户选择。它不授予删除、发布、Git、写资产、保存场景、释放资源或其他业务权限。
- 业务调用方仍必须执行 Undo、Dirty、Prefab Override、发布门禁、权限和取消检查。

## 当前实施事实

- `ES_Stand` 已建立通用合同、Host 路由、Presenter Lease、Generation、能力门禁与请求快照。
- `ES_Editor` 已提供映射现有 `ESAdvancedDialogWindow` 的 Editor Presenter，并通过 AssemblyStream 注册。
- 现有高级 Editor 队列、重复 ID、异步校验、进度、窗口动画与 Domain Reload 清理继续由 Editor 实现负责。
- Runtime Presenter、Runtime UI Root、输入焦点、暂停策略、场景切换和 Player 实机证据尚未完成，不得宣称跨宿主 UI 已全部交付。

## 验收

- 全项目只有一个 `ES.ESDialog`。
- `ES_Stand` 不出现 `UnityEditor` 或具体 UI 引用。
- 重复 Host 注册失败；旧 Lease 不影响新 Generation；双 Host 的 Auto 返回歧义。
- Presenter 停止后活动与等待任务全部终止。
- Stand、目标 Editor 与独立合同测试程序集定向编译；Unity 导入、ReloadDomain、EditMode、PlayMode 和 Player 证据按实际分层报告。

