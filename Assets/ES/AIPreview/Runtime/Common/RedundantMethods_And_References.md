# 可复用冗余方法草案与引用位置（预览）

> 说明：本文件不改动任何现有源码，仅总结未来可以抽取为通用 API 的模式，并给出大致引用例子。

## 1. UnityEngine.Object 判空模式

- 典型出现位置：
  - Link 容器相关文件（接收者列表中经常需要判断目标是否已销毁）；
  - 各类 EditorWindow 中对选中对象的检查。
- 建议抽取的方法：
  - 见 [Assets/ES/AIPreview/Common/CommonUtilityPreview.cs](Assets/ES/AIPreview/Common/CommonUtilityPreview.cs)
    - `CommonUtilityPreview.IsUnityObjectAlive(object obj)`
- 未来替换方向：
  - 将散落的 `if (obj == null || (obj is UnityEngine.Object uo && uo == null))` 统一替换为该方法，提高可读性并减少错误使用。

## 2. 调试日志输出模式

- 典型出现位置：
  - [Assets/Plugins/ES/Editor/ESTrackView/-TrackView-Define/ESTrackViewWindow.cs](Assets/Plugins/ES/Editor/ESTrackView/-TrackView-Define/ESTrackViewWindow.cs)
    - 多处使用 `Debug.Log(...)` 进行流程调试（平移/选择/编辑操作）。
  - DevManagement 系列窗口（日志/任务操作成功后统一使用 Debug + Dialog）。
- 建议抽取的方法：
  - 见 [Assets/ES/AIPreview/Common/CommonUtilityPreview.cs](Assets/ES/AIPreview/Common/CommonUtilityPreview.cs)
    - `CommonUtilityPreview.Log(string category, string message)`
- 未来替换方向：
  - 将 Debug.Log 调用改为 `CommonUtilityPreview.Log("TrackView", "开始平移")` 这类形式，便于统一过滤与重定向日志输出。

## 3. Safe 列表/容器的遍历与刷新

- 典型出现位置：
  - Link 容器（LinkReceiveList / LinkFlagReceiveList / LinkReceiveChannelList 等）在遍历过程中需要 ApplyBuffers + Remove 已失效对象；
- 建议抽象：
  - 未来可以在 AIPreview 中增加一个 `SafeListUtility`，内部封装：
    - BeginIter / EndIter 模式；
    - 标记待移除对象并在遍历后统一清理。
- 当前状态：
  - 仅在文档层面提出，暂未写具体代码，以避免与现有实现产生冲突。

## 4. 总结

- 上述通用方法目前仅在 AIPreview 中以草案形式存在，**不会影响现有逻辑**；
- 当你准备重构时，可以从 CommonUtilityPreview 开始，将最常见的模式（判空/日志/容器遍历）抽取出来，并逐步替换旧代码。