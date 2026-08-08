# ESAdvancedDialog 通用编辑器输入边界 AI 协作警告

> 状态：源码实现，待 Unity 收录、编译与 Editor 交互验收。
> 适用范围：`Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog`。

`ESAdvancedDialog` 是独立的 Editor 轻量输入基础设施，不属于 `ESAutomationCenter`，也不具备业务权限。

它只做：少量字段输入、同步无副作用校验、确认/取消与结构化回传。

跨语言或持久化选项必须使用稳定 OptionId；显示标签可本地化，但不得作为协议值。`AddChoiceOptions` 返回稳定 ID，旧 `AddChoice` 仅适合显示值即业务值的本地输入。

它绝不做：

1. 启动 Python、PowerShell、CLI 或任意进程。
2. 读写 Unity Assets、发布物、设置或凭据。
3. 授予删除、上传、发布等能力。
4. 承担长任务进度、异步生命周期或业务结果通知。
5. 接收密码、Token、AK/SK 等机密。

调用方必须在 `completed` 回调后，经过各自系统的正式 C# Editor 入口执行已授权业务行为；不可把自由文本、路径选择或对话框确认误当作权限校验。
