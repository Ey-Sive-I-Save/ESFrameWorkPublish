# Editor Workbench 事件路由与作者边界导航

`KnowledgeId`: `es.project.editor-workbench-authoring.v1`  
`Authority`: `Source + AIWarnings`  
`RouteKeys`: `editor`, `workbench`, `ui-toolkit`, `drag-routing`, `event-phase`, `draft`, `undo`, `session`, `world`, `acceptance`
`ContentHash`: `f8c5e3b554edbd2f50fa86b913a81af794df3260425efed05146b5c9adeb46bd`

## 只保留的导航事实

- Workbench 外部拖放的权威实现入口是 `ESWorkbenchUIToolkitHost`；事件阶段、owner 释放和失败边界以 AIWarnings P0 合同为准，不在 Knowledge 中复制规则正文。
- 当前 Host 将 `DragUpdatedEvent`、`DragPerformEvent`、`DragLeaveEvent` 在中心宿主以 `TrickleDown.TrickleDown` 注册并以相同阶段注销；`DragExitedEvent`、失焦、捕获丢失和 Panel 脱离统一进入 `CancelWorkbenchDrag(true)`。
- 静态验证器的 `EW-21` 只证明上述源码路由模式存在；它不能证明 Unity 面板实际收到事件、3D 选择命中或运行时拖放成功。
- Scene 正式对象选择必须由稳定身份映射和正式 Scene 所有权证明；PreviewScene/临时对象只能作为预览证据，不能投影为正式选择。

## 路由入口

- 规则正文：`Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/UGC工作台_UI Toolkit作者底座与草稿提交边界_AI协作警告.md`
- 源码：`Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs`
- 静态检查：`.agents/skills/es-editor-availability-validator/scripts/Invoke-ESEditorAvailability.ps1` 与 `references/editor-rule-registry.json` 的 `EW-21`

## 非声明

本条目不声明 Unity Compile、Domain Reload、视觉布局、拖放交互、3D SelectionChanged 或正式 Scene 提交已经运行通过；这些结论必须回到源码、测试和本次真实回执。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/UGC工作台_UI Toolkit作者底座与草稿提交边界_AI协作警告.md` (`bda2011a12df8424e091a5e6d1cd9cbb8c8297dc0ea64c9ee49927df66f23177`)
- `Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs` (`1cbf739532885237235191b830c44b941336a59d9395c7e5ef90aa9ed982456f`)
- `.agents/skills/es-editor-availability-validator/scripts/Invoke-ESEditorAvailability.ps1` (`85f14f990509c87415794fd6628ca2c0b53d9608036a931a5539516ea6b13618`)
- `.agents/skills/es-editor-availability-validator/references/editor-rule-registry.json` (`3033ad086d2dff84cf4ad5f9b8c891b2dfd939eb93ba9211e061bfddc5c29247`)

`EvidenceLevel`: `S1`（源码与规则导航；未运行 Unity 交互验收）
`StaleWhen`: Workbench Host、事件路由合同、EW-21、正式 Scene 稳定身份映射或任一 SourceRef 哈希变化。
