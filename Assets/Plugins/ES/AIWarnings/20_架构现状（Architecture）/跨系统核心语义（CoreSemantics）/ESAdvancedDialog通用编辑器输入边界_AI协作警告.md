# ESAdvancedDialog 通用编辑器输入边界 AI 协作警告

> 状态：源码实现，待 Unity 收录、编译与 Editor 交互验收。
> 适用范围：`Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog`。

`ESAdvancedDialog` 是独立的 Editor 通用交互外壳，不属于 `ESAutomationCenter`，也不具备业务权限。它可以承载结构化字段、稳定选项、自定义 `VisualElement` 内容、同步或异步校验、同步或异步辅助动作、进度与取消反馈、重复窗口策略、多种定位模式以及确认/取消回传；这些 UI 能力不能被解释为对业务行为的授权。

复制文本、切换说明、预览等不改变业务权威数据的辅助动作可以直接放在对话框内。会修改资产、设置、发布物或外部状态的动作，即使通过 `AddAuxiliaryAction` / `AddAuxiliaryActionAsync` 接入，也必须由调用方先完成权限、目标和前置条件检查，再进入对应系统的正式 C# Editor 入口。

跨语言或持久化选项必须使用稳定 OptionId；显示标签可本地化，但不得作为协议值。`AddChoiceOptions` 返回稳定 ID，旧 `AddChoice` 仅适合显示值即业务值的本地输入。

它自身绝不自动做：

1. 启动 Python、PowerShell、CLI 或任意进程。
2. 绕过调用方的正式入口读写 Unity Assets、发布物、设置或凭据。
3. 因为用户点击确认或辅助按钮就授予删除、上传、发布等能力。
4. 把窗口存活、进度显示或取消按钮冒充后台任务的权威生命周期与最终结果。
5. 接收密码、Token、AK/SK 等机密。

调用方必须在 `completed` 回调或已声明的辅助动作中，经过各自系统的正式 C# Editor 入口执行已授权业务行为；资产修改仍须遵守目标校验、`Undo`、Dirty、保存和失败回滚合同。不可把自由文本、路径选择、对话框确认、进度显示或异步回调误当作权限校验与任务完成证据。
