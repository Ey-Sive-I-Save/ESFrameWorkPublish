# 编辑器窗口迁移：ESMenuTree、SinglePage 与 Odin 兼容外壳

> 状态：现行迁移约束；源码接入持续实施，Unity 交互与性能待分项验收。
>
> 最后核对：2026-08-16。
>
> 历史说明：文件名因既有引用暂时保留 `ESMenuTreeWindowAB` 字样；活跃源码已不存在该类型，禁止按文件名恢复旧基类。

职责：记录 ES 编辑器窗口迁移到新版 Toolkit 菜单树、单页外壳、IMGUI 桥接和 Odin 兼容外壳的当前事实、边界和风险。它不是全项目 UI 总纲，也不要求为了数量重写成熟业务交互。

## 当前源码确认路径

- 统一窗口基类：`Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/-ESMenuTreeWindow.cs`
  - `ESMenuTreeWindow<T>`：新版 UI Toolkit 菜单树与页面上下文权威。
  - `ESSinglePageWindow<T>`：无导航的 Toolkit 单页外壳。
  - `ESSinglePageIMGUIWindow<T>`：保留既有 IMGUI 内容的单页迁移外壳。
  - `ESOdinMenuTreeWindow<T>`：仅为仍依赖 Odin PropertyTree/序列化的兼容外壳。
- 独立检查器基类：`Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/ESIndependentInspectorWindow.cs`
- 安装器：`Assets/Plugins/ES/Editor/Installer/ESInstaller.cs`
- 交互运行时面板：`Assets/Scripts/ESLogic/Editor/EntityBasicInteractionDebugWindow.cs`，当前因程序集边界保留为普通 `EditorWindow`
- Solver 示例窗口目录：`Assets/Plugins/ES/3_Examples/2_Editor/Example_EditorTools/Example_ForMustEditorSolvers`
- TrackView 临时检查器：`Assets/Plugins/ES/Editor/ESTrackView/-TrackView-Define/ESTrackTemporaryInspectorWindow.cs`
- TrackView 临时编辑调用点：
  - `Assets/Plugins/ES/Editor/ESTrackView/-TrackView-Define/ESTrackViewWindow.cs`
  - `Assets/Plugins/ES/Editor/ESTrackView/-TrackView-Define/ESTrackTimerToolbar.cs`

## 当前迁移事实

以下窗口已迁入 `ESSinglePageIMGUIWindow<T>`，原业务 IMGUI 绘制逻辑基本保留：

- `ESInstaller`
- `ESExample_AreaDragAtSolverWindow`
- `ESExample_DropZoneSolverWindow`
- `ESExample_ContextMenuSolverWindow`
- `ESExample_TreeViewSolverWindow`
- `ESForMustEditorSolversSampleWindow`
- `ESExample_RecordListSolverWindow`

迁移方式：旧 `OnGUI` 入口改为 `ESWindow_DrawIMGUI(ESMenuTreePageContext)` 或内部绘制方法，由单页外壳提供状态栏、动作区、错误隔离、激活动效、局部重建和确定性释放。不要为套壳重写业务逻辑，也不要把原窗口逻辑机械拆成大量一次性脚本。

其他已确认事实：

- `ESAssetPackageBakeWindow` 使用 `ESMenuTreeWindow<ESAssetPackageBakeWindow>` 新版菜单树外壳。
- `EntityBasicInteractionDebugWindow` 已使用 `ESSinglePageIMGUIWindow<EntityBasicInteractionDebugWindow>`；旧“程序集不可见、不能迁移”结论失效。
- `ESInputActionImportWindow` 与 `ESInputActionBindingImportWindow` 已使用 `ESSinglePageIMGUIWindow<T>`；它们仍属于短生命周期输入面，必须显式关闭半休眠。
- TrackView、Stable Graph、Agent 保留自身 UI Toolkit 主体，但已通过 `ESWindowFoundation.Bind` 与显式 `ESWindowActionHosts` 接入共享 Presentation；这属于标准外壳接入，不要求改造成菜单树。

## TrackView 临时弹窗迁移事实

TrackView 主窗口保留时间轴主体，但它内部的临时编辑窗口已经改为 ES 独立检查器外壳：

- 轨道项目编辑：`ESTrackItemTemporaryInspectorWindow`
- 片段编辑：`ESTrackClipTemporaryInspectorWindow`
- 技能配置编辑：`ESTrackSkillDataTemporaryInspectorWindow`

统一外壳是：

```text
ESTrackTemporaryInspectorWindow<TWindow>
  : ESIndependentInspectorWindow<TWindow>
  : ESOdinMenuTreeWindow<TWindow>
```

它替代了原来的 `OdinEditorWindow.InspectObject(...)` 直接弹窗。关闭时仍然走原来的保存/刷新逻辑：清理 `drawerData`、刷新 Track/Clip、`SaveContainerChanges()`、`SetDirty` 等。

重要：临时弹窗可以迁移，但不应塞进主菜单树。它们应保持“由业务窗口按需打开”的使用方式。

## 自定义主体窗口的接入边界

- `ESTrackViewWindow`
  - 强 UIElements / 时间轴交互 / 多状态缓存窗口。
  - 保留主体交互，只接共享 Presentation、显式系统动作宿主、父窗口 key 和休眠生命周期；不得为了菜单树重写播放、选择、拖拽、焦点片段或预览状态。
- `ESStableGraphViewWindow`
  - Stable Graph V2/UIElements 主体窗口。
  - 保留 Graph 主体，只接共享 Presentation 和显式系统动作宿主；不得把画布强塞入菜单树页面。
- `ESTreeMenuShower`
  - 弹出式快捷菜单。
  - 它的正确形态更接近弹出菜单，不是常驻工具窗口。
- `EditorInputDialog`
  - 仍是一次性输入弹窗；新调用优先迁往 ES 权威 Dialog 请求/回调入口，不得继续扩大同步返回值用法。
- `EntityBasicInteractionDebugWindow`
  - 当前已在 `ES_Logic.Editor` 依赖边界内接入 `ESSinglePageIMGUIWindow<T>`；后续保持该程序集引用，不得恢复不存在的 `ESMenuTreeWindowAB` 或复制第二套外壳。

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
- 当前 MenuTree/SinglePage/Odin 兼容外壳的 `OpenWindow()` 都不会默认最大化；首开窗口按主编辑器工作区、最小尺寸和默认尺寸居中放置。临时弹窗仍应使用自己的 `OpenFor(...)` / `Open(owner)` 明确尺寸、owner 和关闭语义。
- 页面类可以内嵌在窗口类里，避免为每个小窗口新增很多脚本。
- `QuickBuildRootMenu<T>` / `RegisterAndAddPage(...)` 只属于 `ESOdinMenuTreeWindow<T>` 兼容路径；新版 Toolkit 页面使用 `ESMenuTreeBuilder`、稳定页面 ID、`ESMenuTreePageDefinition` 和页面上下文。
- 标准 MenuTree/SinglePage 基类负责创建 System、Global、Window、Page 四层动作行；派生窗口只追加动作。自定义主体窗口必须显式提供 `ESWindowActionHosts`，不得依赖未知 Toolbar 或右上绝对定位回退。
- 半休眠与父子关系统一遵守编辑器常识第 11.10、11.11 节；窗口尺寸不能作为状态推断，子窗口必须使用显式 owner、稳定 ownerKey 和 `PendingFollowOwner`。

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

## 当前证据边界

本文件本次更新只复核当前源码类型、接入点和测试合同，没有重新运行 Unity Test Runner、四边页签交互矩阵或 20/50/100 窗口 Profiler。后续 AI 不得把“文档已同步”“源码存在”或 `.csproj` 编译写成 Unity 交互、ReloadDomain、性能或商业级验收通过。
