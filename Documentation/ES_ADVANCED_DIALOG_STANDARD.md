# ESAdvancedDialog 编辑器扩展对话框标准

状态：源码实现，待 Unity 收录、编译与 Editor 交互验收。

## 定位

`ESAdvancedDialog` 是 Editor 专用的少量结构化输入窗口。它不绑定 `ESAutomationCenter`，也不执行命令、读写资产、启动 Worker 或授予权限；业务调用方在用户确认后自行消费结果。

适用：名称与说明、模式选择、少量开关、文件/目录选择、单个 Unity Object 选择等轻量输入。

不适用：多页面工作流、长时间任务、批量表格编辑、凭据输入、权限授权或任意命令行输入。这些需求必须使用专用 EditorWindow 和所属系统的门禁。

## 输入类型

- `Text`、`MultilineText`
- `Toggle`
- `Choice`
- `FolderPath`、`FilePath`
- `Object`

字段必须有窗口内稳定唯一的 `id`。`required`、Choice 选项和可选的 `request.validate` 会在确认前校验。验证回调必须快速且无副作用。

跨语言或需长期保存的选项必须使用 `AddChoiceOptions`：显示 `label` 可以本地化，`id` 才是 `GetString(...)` 返回和写入协议的稳定值。旧 `AddChoice` 仍用于显示值即业务值的本地 Editor 场景。

## 使用约定

```csharp
var request = new ESAdvancedDialogRequest
{
    title = "创建配置",
    message = "只收集输入；创建资产仍由调用方的 C# Editor 逻辑完成。",
    confirmText = "继续",
    validate = values => string.IsNullOrWhiteSpace(values.GetString("key")) ? "Key 不能为空。" : string.Empty,
    completed = result =>
    {
        if (!result.accepted) return;
        string key = result.values.GetString("key");
        // 由调用方执行自己已授权的 C# Editor 行为。
    },
};
request.AddText("key", "稳定 Key", required: true);
request.AddChoice("mode", "模式", new[] { "A", "B" });
ESAdvancedDialogWindow.Show(request);
```

```csharp
request.AddChoiceOptions("detailMode", "报告粒度", new[]
{
    new ESAdvancedDialogChoiceOption("summary", "摘要"),
    new ESAdvancedDialogChoiceOption("detailed", "详细"),
}, "summary");
// values.GetString("detailMode") 返回 summary / detailed，而非中文标签。
```

用户点击取消、关闭 Utility 窗口或确认后，`completed` 恰好回调一次。对话框本身不显示“发布成功”等业务结论；调用方应使用所属窗口或报告系统展示后续结果。

## 不可突破的边界

1. 不能将密码、AK/SK、Token 等秘密作为字段输入或回传。
2. 不能以 `FilePath`、`FolderPath` 或自由文本替代路径权限、发布确认和安全策略。
3. 不可把任意命令、Python 路径、PowerShell 内容交给该对话框执行。
4. 不可把它当作长任务进度窗口；长任务应支持取消、报告和后台生命周期。
5. Unity API 和业务动作只可由调用方在 Editor 主线程、通过本系统的正式入口执行。
