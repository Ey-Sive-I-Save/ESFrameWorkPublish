# UGC 工作台：UI Toolkit 作者底座与草稿提交边界 AI 协作警告

状态：现行专项约束；当前工作副本 `Implemented-Unverified`。

最后核对：2026-08-16。

适用范围：`ESWorkbenchWindowBase`、`ESWorkbenchUIToolkitHost`、World/关卡/剧情/对话/Graph 等 UGC 作者工作台，以及资源面板、层级、2D/3D 视口、Inspector、工具轨、底部问题区、拖放、锁定、Undo、Draft、提交和恢复。

## 产品目标

UGC 工作台不是“把若干 IMGUI 表单塞进一个窗口”，而是让用户在一个稳定作者会话里完成：发现内容、选择目标、在视口中操作、编辑属性、发现问题、预览结果、撤销、保存草稿和显式提交正式资产。

标准首屏结构：

```text
顶部：文档/地图选择、状态、保存/提交、撤销/重做、验证、运行或构建
左侧：资源库与作者层级，可搜索、过滤、锁定、拖入视口
中心：2D/3D/游戏预览视口 + 固定尺寸工具轨 + 视口模式/吸附/坐标控制
右侧：当前稳定选择的 Inspector、问题与就近修复动作
底部：问题、活动、构建/验证、性能或操作结果抽屉
```

窄窗口可以折叠左右栏和底部抽屉，但不得删除功能或把关键动作藏到横向滚动之外。布局变化不能改变当前选择、编辑目标、草稿或 Undo 归属。

## 入口文件

```text
Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchWindowBase.cs
Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs
Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchAuthoringContracts.cs
Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchContributionRegistry.cs
Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchPersistenceContract.cs
Assets/Scripts/ESLogic/Editor/World/ESWorldBuilderWorkbenchWindow.cs
Assets/Scripts/ESLogic/Editor/World/ESWorldEditSession.cs
Assets/Scripts/ESLogic/Editor/World/ESWorldAuthoringViewport.cs
```

## 当前源码事实

- 当前工作副本中存在 UI Toolkit 工作台外壳、Contribution Registry、页面/视口/资源/层级/Inspector/工具/命令/问题源合同、稳定选择、拖放、锁定、Undo 目标、快捷键隔离和底部抽屉。
- World 工作台存在 `ESWorldEditSession`：正式 `Source` 与 `HideAndDontSave` 的 `Draft` 分离，使用基线 Hash、ChangeSet、SessionState 恢复、外部漂移阻断和显式 `TryCommit()`。
- World 作者操作已有地形、区域、POI、Prefab 放置等入口，并要求适配器提供 Undo 目标；提交失败会尝试恢复正式 Source。
- `Assets/Scripts/ESLogic/Editor/Workbench` 与 `Assets/Scripts/ESLogic/Editor/World` 当前是未跟踪目录。它们不是正式 Git 基线，本轮也没有 Unity 实机、Domain Reload、PlayMode、Profiler 或发布证据。
- `ESWorkbenchWindowBase.OnWorkbenchUndoRedo()` 当前只更新 `SerializedObject` 并刷新工作台。领域 Draft 的恢复快照是否随 Undo/Redo 同步，不能由界面刷新推导；World 需要显式同步 `draftHash`、ChangeSet 与 SessionState，或提供等价领域回调并验证。

## 过时理解，禁止继续传播

- [过时] “二维网格加属性表单就是完整 UGC 工作台。”
  商业作者工具还必须有资源发现、层级、视口工具、稳定选择、拖放、锁定、Undo、问题反馈、Draft、冲突与提交闭环。
- [过时] “UI Toolkit 外壳画出来就证明工作台底座可用。”
  外壳不能替代真实交互、目标正确性、Reload、恢复和性能证据。
- [过时] “直接修改正式资产并依靠 Undo 就等于 Draft。”
  Draft 必须与 Source、Baseline、ChangeSet、外部漂移和提交事务明确分层。
- [过时] “保存 ESWorldMapAsset 就代表 TerrainData、Scene 和运行时地图完成。”
  五层权威对象必须分别写入、读取和验收。

## 权威状态模型

每个作者工作台必须明确以下对象，禁止混用：

```text
正式来源 Source
  -> 会话打开时的 Baseline + Hash
  -> 隔离 Draft
  -> ChangeSet / Dirty Flags / Revision
  -> Validation Result
  -> Commit Plan
  -> 正式提交或明确取消
```

- UI、预览和工具默认只修改 Draft；正式 Source 只有在显式提交事务内可变。
- 稳定选择保存 Stable ID、Kind、资产 GUID 或领域 Key，不持久化 `UnityEngine.Object`、`SerializedObject`、InstanceId、视口实例或托管 Payload。
- Draft 的恢复身份必须绑定正式资产稳定身份和 Schema/Revision；外部 Source 漂移时必须锁定提交并要求重新加载或人工合并。
- Dirty、ChangeSet、恢复快照和 UI 状态是不同数据。任何一项更新不得假设其他项已经自动同步。
- World 必须继续区分 ES 作者态资产、Heightfield、Unity TerrainData、正式 Scene/Prefab 和运行时/发布产物；保存 Draft 或 `ESWorldMapAsset` 不等于其他层已保存。

## 作者交互合同

### 选择与 Inspector

- 资源、层级、视口和 Inspector 必须引用同一个稳定选择服务；对象替换时即使 Stable ID 相同也要刷新对象代际。
- 删除、外部替换或 Domain Reload 后无法解析目标时，显示原因、影响和恢复动作，禁止继续编辑旧引用或猜测同名对象。
- Inspector 必须使用 SerializedObject/SerializedProperty，支持 Undo、Prefab override、多对象和 mixed value 时按目标领域明确声明；自定义直接写对象只能用于有显式事务和回滚的非序列化状态。

### 拖放与视口

- 拖放分为“可接受性预检”和“正式作者操作”。预检不得改数据；执行必须解析精确视口坐标、目标层级、权限、锁定、预算和 Undo 目标。
- **P0 事件路由硬合同**：外部拖放事件必须由 Workbench Host 在子视口/`IMGUIContainer` 之前接收；`DragUpdatedEvent`、`DragPerformEvent`、`DragLeaveEvent` 在稳定宿主节点注册 `TrickleDown.TrickleDown`，注销时必须使用完全相同的节点、回调和阶段。`DragExitedEvent`、窗口失焦、根宿主 `PointerCaptureOutEvent`、根宿主 `DetachFromPanelEvent` 和拖放离开都必须幂等调用 `CancelWorkbenchDrag(true)`；任何路径不得只清反馈而保留 owner、session token 或外部拖放状态。该合同证明的是源代码路由意图，实际 UI 行为仍需 Unity 交互证据。
- 2D/3D/游戏预览是同一作者数据的不同投影，不得各自维护第二份业务状态。
- 工具轨尺寸、按钮、吸附值和视口覆盖层必须有稳定约束，不能因标签、Hover 或动态状态改变视口布局。
- 视口临时对象、Terrain、Camera、RT 和 PreviewScene 必须受预览生命周期管理；预览成功不代表正式 Scene 已修改。

### 锁定、Undo 与快捷键

- 锁定必须在作者操作执行前阻断，不能只禁用一个按钮；拖放、快捷键、Context Menu、脚本命令和批量操作都走同一门禁。
- 每个 mutation 必须提供明确 Undo 目标。没有 Undo 目标时拒绝执行，不允许“先改了再补记录”。
- Undo/Redo 后必须重新计算领域 Dirty/Hash/ChangeSet，持久化恢复快照，刷新选择/Inspector/视口，并重新检查外部漂移；只重绘界面不算闭环。
- 文本输入、搜索、ObjectField、Popup 和 IMGUIContainer 获得焦点时，删除、复制、撤销、重做等工作台快捷键不得抢占输入事件。

## 提交事务

推荐顺序：

```text
冻结当前 Draft/Revision
  -> Validate
  -> 检查 Source Baseline 漂移
  -> 建立 Undo Group 与提交前快照
  -> 写入唯一正式 Source
  -> 调用领域保存后端
  -> 重新读取并核对目标
  -> 更新 Baseline/Hash/ChangeSet/恢复快照
  -> 发布 UI 刷新
```

- 验证失败、外部漂移和写入失败必须保持 Draft 可恢复，不能清空用户进度。
- 正式写入失败时只有“正式 Source 已恢复且重新读取一致”才能声明回滚成功。
- mutation 已提交后，若 Inspector/索引/预览等后处理失败，必须报告“作者数据已提交，但提交后同步失败”；不得谎报整个操作已经回滚。
- 保存一个 `ESWorldMapAsset` 不能描述成保存 `TerrainData`、正式 Scene、碰撞、导航或运行时内容。

## Domain Reload 与并发

- Reload 前释放视口、回调、PropertyTree、SerializedObject、PreviewScene、RT 和临时对象；只持久化稳定身份、布局、Baseline/Draft 文本、Hash、ChangeSet 与必要版本。
- Reload 后先验证正式 Source 身份和外部漂移，再恢复 Draft 和稳定选择；禁止根据窗口标题、最近 Selection 或 InstanceId 猜测。
- 多窗口编辑同一正式资产时，每个窗口拥有自己的 Draft 和 Baseline；后提交者遇到漂移必须阻断，不能后写覆盖先写。
- 贡献目录与运行实例分离：注册元数据可以稳定保存，真实视口、回调和对象只属于当前窗口会话，移除贡献时必须 Dispose。

## ES 界面标准

- 外壳、Surface、Toolbar、Header、状态、间距、字体和图标统一复用 `ESEditorPresentation`/`ESWindowPresentation`；业务域可以定义语义色和专业布局，不能复制一套品牌系统。
- 第一视图无需滚动即可看到当前文档、Dirty/冲突状态、主要操作和中心作者视口。
- 宽屏保持左/中/右 + 底部抽屉；窄屏使用明确的面板切换或折叠，不创建嵌套横向 ScrollView。
- 错误视图必须包含原因、影响、恢复动作；长路径、GUID、Hash、Revision 和日志要可完整复制，并提供定位/打开入口。
- UI Toolkit 外壳中嵌入 IMGUI/Odin 只能称视觉适配；它们不得创建第二个滚动、选择、Undo 或主题生命周期。

## 验收门禁

达到 `Accepted` 前至少完成：

1. Unity Editor Compile、Domain Reload 和 Console 无目标错误；
2. 宽屏、窄屏、高 DPI、深浅主题和长中文下布局无重叠、裁切或横向滚动；
3. 资源搜索、层级选择、视口框选/拖放、工具轨、Inspector 编辑、锁定、Undo/Redo、保存和回退真实可用；
4. Reload 前后 Draft、Dirty、ChangeSet、稳定选择和布局正确恢复，且不会覆盖外部 Source 变化；
5. 失败注入覆盖验证失败、无 Undo 目标、外部漂移、正式保存失败和提交后同步失败；
6. 多窗口同资产冲突不会丢进度或后写覆盖；
7. Profiler/Memory 证明无每帧 AssetDatabase 扫描、无界 Repaint、重复 PropertyTree/SerializedObject、预览资源泄漏或大规模 GC；
8. 对 World 分别验证作者态、Heightfield、TerrainData、正式 Scene/Prefab 与运行时/发布层，禁止证据替换。

源码、测试源码、`.csproj` 编译、UTF-8 Guard、`git diff --check` 或单张截图都不能单独替代上述 Unity 交互矩阵。

## 禁止事项

- 禁止为每个 UGC 领域复制一套选择、拖放、Undo、Draft、提交、布局和恢复底座。
- 禁止把工作台外壳存在、二维网格显示或属性表单绘制一次描述成 UGC 核心能力完成。
- 禁止直接编辑正式 Source 来获得“即时预览”，再依赖 Undo 充当草稿系统。
- 禁止在未跟踪目录、未导入脚本或未跑 Unity 时宣称正式基线、商业级完成或已验收。
- 禁止为了视觉风格牺牲事件隔离、目标正确性、单一滚动容器、进度恢复和性能。

## 下一步

1. 先补公共 Undo/Redo 领域回调与 World Draft 恢复快照同步测试。
2. 再完成真实 Unity 窗口的布局、拖放、锁定、Reload、冲突和失败注入矩阵。
3. 最后再推广到其他 UGC 领域；底座未验收前不得把示例工作台包装为商业级通用方案。
