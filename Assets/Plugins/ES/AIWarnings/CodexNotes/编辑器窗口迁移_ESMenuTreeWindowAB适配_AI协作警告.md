# 编辑器窗口迁移 ESMenuTreeWindowAB 适配 AI 协作警告

Last updated: 2026-07-19

职责：本文件属于 Codex 工具重写 / 商业级验证职责范围，记录 ES 编辑器窗口迁移到 `ESMenuTreeWindowAB` 的当前事实、边界和风险。它不是全项目 UI 总纲，也不是要求所有窗口立刻迁移。

## 当前已验证路径

- 统一窗口基类：`Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/-ESMenuTreeWindow.cs`
- 安装器：`Assets/Plugins/ES/Editor/Installer/ESInstaller.cs`
- 交互运行时面板：`Assets/Scripts/ESLogic/Editor/EntityBasicInteractionDebugWindow.cs`，当前因程序集边界保留为普通 `EditorWindow`
- Solver 示例窗口目录：`Assets/Plugins/ES/3_Examples/2_Editor/Example_EditorTools/Example_ForMustEditorSolvers`
- TrackView 临时检查器：`Assets/Plugins/ES/Editor/ESTrackView/-TrackView-Define/ESTrackTemporaryInspectorWindow.cs`
- TrackView 临时编辑调用点：
  - `Assets/Plugins/ES/Editor/ESTrackView/-TrackView-Define/ESTrackViewWindow.cs`
  - `Assets/Plugins/ES/Editor/ESTrackView/-TrackView-Define/ESTrackTimerToolbar.cs`

## 已迁移窗口

这些窗口已改为 `ESMenuTreeWindowAB` 外壳，原业务绘制逻辑基本保留：

- `ESInstaller`
- `ESExample_AreaDragAtSolverWindow`
- `ESExample_DropZoneSolverWindow`
- `ESExample_ContextMenuSolverWindow`
- `ESExample_TreeViewSolverWindow`
- `ESForMustEditorSolversSampleWindow`
- `ESExample_RecordListSolverWindow`

迁移方式：旧 `OnGUI` 入口改成内部绘制方法，例如 `DrawExampleGUI`、`DrawInstallerGUI`、`DrawRuntimePanelGUI`；再用内嵌 `ESWindowPageBase` 页面通过 `[OnInspectorGUI]` 调用。不要把原窗口逻辑拆成一堆独立脚本，除非确实存在复用价值。

## TrackView 临时弹窗迁移事实

TrackView 主窗口本身暂未迁移，但它内部的临时编辑窗口已经改为 ES 临时检查器外壳：

- 轨道项目编辑：`ESTrackItemTemporaryInspectorWindow`
- 片段编辑：`ESTrackClipTemporaryInspectorWindow`
- 技能配置编辑：`ESTrackSkillDataTemporaryInspectorWindow`

统一外壳是：

```text
ESTrackTemporaryInspectorWindow<TWindow> : ESMenuTreeWindowAB<TWindow>
```

它替代了原来的 `OdinEditorWindow.InspectObject(...)` 直接弹窗。关闭时仍然走原来的保存/刷新逻辑：清理 `drawerData`、刷新 Track/Clip、`SaveContainerChanges()`、`SetDirty` 等。

重要：临时弹窗可以迁移，但不应塞进主菜单树。它们应保持“由业务窗口按需打开”的使用方式。

## 哪些暂时不要硬迁移

- `ESTrackViewWindow`
  - 强 UIElements / 时间轴交互 / 多状态缓存窗口。
  - 主窗口迁移会影响播放、选择、拖拽、焦点片段、临时预览状态，不应在窗口适配小任务里硬改。
- `ESGraphViewWindow`
  - GraphView/UIElements 主体窗口。
  - 这类窗口的核心不是菜单树页面，强迁可能破坏 GraphView 生命周期。
- `ESTreeMenuShower`
  - 弹出式快捷菜单。
  - 它的正确形态更接近弹出菜单，不是常驻工具窗口。
- `EditorInputDialog`、`ESInputActionBindingImportWindow`
  - 一次性输入/导入弹窗。
  - 可以在必要时换成统一临时弹窗外壳，但不要为了数量迁移而改变交互方式。
- `EntityBasicInteractionDebugWindow`
  - 位于 `Assets/Scripts/ESLogic/Editor`，当前程序集不能直接看到 `Assets/Plugins/ES/Editor/ESMenuTreeWindow` 下的 `ESMenuTreeWindowAB`。
  - 不要在原地继承 `ESMenuTreeWindowAB`；若要迁移，应先移动到合适的 Editor 程序集或调整 asmdef 边界。

## 迁移判断规则

- 适合迁移：
  - 普通 `EditorWindow`，主要靠 IMGUI 绘制。
  - 单页或少量页面工具。
  - 示例窗口、管理窗口、调试面板。
  - 业务窗口内按需打开的临时 Odin 检查器窗口。
- 谨慎迁移：
  - 使用大量 UIElements、GraphView、Timeline 风格自绘的主窗口。
  - 有复杂播放状态、选择状态、拖拽状态、焦点状态的主窗口。
  - 弹出式菜单和一次性输入对话框。
- 不要迁移：
  - 只是为了“看起来统一”而迁移。
  - 迁移后必须重写业务逻辑、重写交互模型、重写持久化流程的窗口。

## 必须保留的行为

- 原窗口的保存逻辑、关闭逻辑、Undo/Dirty 行为不能丢。
- 原窗口的菜单路径不要随手改；如果发现 diff 里已有菜单路径变化，先确认是不是其他 AI 或用户已改，不要擅自回滚。
- `ESMenuTreeWindowAB.OpenWindow()` 默认会最大化窗口；临时弹窗不适合直接用它，应像 `ESTrackTemporaryInspectorWindow.OpenFor(...)` 一样自己设置尺寸和 `Show()`。
- 页面类可以内嵌在窗口类里，避免为每个小窗口新增很多脚本。
- `QuickBuildRootMenu<T>` 要求页面类型有无参构造；如果页面需要传对象，可用 `RegisterAndAddPage(...)` 传已创建实例。

## 后续 AI 修改前检查

迁移窗口前先查：

```powershell
rg "class .*: (EditorWindow|OdinEditorWindow)|: EditorWindow|: OdinEditorWindow" Assets/Plugins/ES/Editor Assets/Plugins/ES/3_Examples Assets/Scripts/ESLogic -g "*.cs" -n
rg "InspectObject\\(|private void OnGUI|protected override void OnImGUI|DrawEditors\\(" Assets/Plugins/ES Assets/Scripts/ESLogic -g "*.cs" -n
```

改完至少查：

```powershell
git diff --check -- <changed-files>
rg "InspectObject\\(" Assets/Plugins/ES/Editor/ESTrackView/-TrackView-Define -n
rg "class .*: EditorWindow|private void OnGUI\\(" <changed-folder> -n
```

如果能进 Unity，必须确认：

- 菜单入口能打开。
- 页面树能显示。
- 原按钮、拖拽、右键菜单还能用。
- 关闭临时窗口会触发原保存/清理。
- 域重载或关闭主窗口时不会留下脏引用。

## 当前未完成验证

本次只做了静态检查和 `git diff --check`。没有在 Unity 编辑器里完成编译和手动打开验证。后续 AI 不要把“静态检查通过”写成“Unity 已验证通过”。
