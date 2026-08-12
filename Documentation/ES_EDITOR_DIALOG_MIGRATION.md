# ES 对话框迁移规范

## 推荐入口

新代码优先使用 `ESDialog` 的强类型入口：

```csharp
bool accepted = await ESDialog.ConfirmAsync(
    "agent.session.close",
    "关闭受管会话",
    "关闭后本地窗口将停止跟踪该会话。",
    "关闭",
    "取消",
    tone: ESDialogTone.Warning,
    host: ESDialogHost.Editor,
    cancellationToken: token);
```

- `InfoAsync`：单按钮信息提示。
- `ConfirmAsync` / `DangerAsync`：推荐的非阻塞双按钮确认。
- `ChooseAsync`：三按钮选择，返回 `ESDialogChoice`，不使用 `0/1/2`。
- `ESDialog.ShowAsync`：跨宿主结构化输入；支持文本、开关、单选、多选、推荐度和异步校验。
- `ESEditorDialog.ShowAsync`：为公共请求补充 Owner 和 Editor 尺寸等宿主选项。
- `ESDialogService.ShowAsync`：仅供 Editor 高级请求、VisualElement、ObjectField、文件选择和异步执行。
- `ESEditorDialog.ShowAdvancedModal`：只用于必须维持旧同步控制流的短确认。
- `ESProgressCenter.RunAsync`：真正异步任务。
- `ESProgressCenter.RunSteps`：把旧同步循环拆成逐帧步骤，保持 Editor 可响应。

日常入口强制提供稳定 `dialogId`，建议格式为 `<模块>.<操作>`。相同 ID 默认聚焦已有窗口，避免重复弹窗。

Runtime UI 实现不使用 AssemblyStream。由产品 UI Root 在自身生命周期显式持有注册 Lease：

```csharp
private ESDialogPresenterLease dialogLease;

void OnEnable()
{
    dialogLease = ESDialog.RegisterPresenter(runtimeDialogPresenter);
}

void OnDisable()
{
    dialogLease?.Dispose();
    dialogLease = null;
}
```

Runtime Presenter 尚未提供框架默认视觉实现；在它落地前，显式 `Runtime` 请求会返回 `HostUnavailable`，不会偷偷弹出 Editor 或 Unity 原生对话框。

## 原生 API 映射

```csharp
if (!await ESDialog.ConfirmAsync(
        "asset.delete",
        title,
        message,
        ok,
        cancel,
        host: ESDialogHost.Editor))
    return;
```

`EditorUtility.DisplayDialogComplex` 映射为 `ChooseAsync`：

```csharp
ESDialogChoice choice = await ESDialog.ChooseAsync(
    "resource.conflict",
    title,
    message,
    "覆盖",
    "保留两份",
    "取消",
    host: ESDialogHost.Editor);
```

## 禁止机械替换

- 不要把 `OpenFilePanel`、`OpenFolderPanel` 或 `SaveFilePanel` 替换成确认框。
- 不要用 `ESProgressCenter.Run` 包裹长时间主线程循环；使用 `RunSteps` 或真正的 `RunAsync`。
- 不要为了保留同步控制流把公共门面重新依赖到 `EditorWindow`；同步 Modal 只属于 Editor 高级层。
- 不要在异步 API 返回前执行资产、场景、Git、发布或删除操作。
- 需要 Undo、Dirty、Prefab Override 或多对象编辑时，确认结果只授予当前调用方继续执行；业务写入仍必须使用对应 Unity 安全 API。

## 生命周期

- 请求提交时会生成私有快照；提交后修改原请求不会影响已打开或排队窗口。
- 同一 Host 禁止无声覆盖 Presenter；注册 Lease 使用 Generation，旧 Lease 不能注销新 Presenter。
- Editor 与 Runtime Presenter 同时存在时，`Auto` 会返回 `AmbiguousHost`，关键调用应显式指定 Host。
- 每个 `ShowAsync` 调用者拥有独立等待与取消；取消观察不会关闭其他调用者共享的同 ID 对话框。
- Domain Reload 和 Unity Quit 前，活动窗口、排队请求、逐帧进度和取消令牌统一收口。
- 自定义 `VisualElement` 使用 `releaseCustomContent` 释放；未提供回调时才尝试 `IDisposable.Dispose()`，最多执行一次。
