# Editor 事件与输入 Owner 导航

`KnowledgeId`: `es.editor.editor-event-ownership.v1`
`Authority`: `Current source + AIWarnings`
`RouteKeys`: `editor`, `editor-event`, `event-phase`, `input-owner`, `pointer-capture`, `drag-routing`, `focus`, `interaction`
`ContentHash`: `80bbc2d09ff55ecca6f32cb33c036016831fd64a950e4f99cd96b0d6234f1aa4`

## Canonical facts

- Editor 事件的接收节点、传播阶段和注销阶段属于宿主合同；不能因为子节点是 IMGUIContainer、视口或控件就把路由责任下放给子节点。
- 一个交互只能有一个明确 owner。Pointer capture 丢失、FocusOut、Cancel、DragLeave、DragExited 和宿主脱离必须进入对应 owner 的幂等终态；清理视觉反馈不等于释放 owner。
- 外部拖放必须区分预检与正式执行；预检不得修改作者数据，执行必须经过目标、权限、锁定、坐标和 Undo 门禁。
- `ESWorkbenchUIToolkitHost` 的 `EW-21` 是 Workbench 对本通用合同的静态投影；规则正文仍以 AIWarnings 和当前源码为准。

## Failure prevention

| 失败面 | 预防检查 | 正确恢复 | 未证明 |
|---|---|---|---|
| 子视口/IMGUI 消费拖放 | 检查宿主注册节点和 `TrickleDown` 对称注销 | 回到 Host owner 路由并清理当前会话 | Unity 实际事件顺序 |
| 捕获或焦点丢失后手势卡死 | 检查 CaptureOut/FocusOut/Cancel 的幂等终态 | 调用 owner 的 cancel/release，再允许新手势 | 跨窗口焦点切换体验 |
| 预检误写作者数据 | 检查 DragUpdated 不调用 mutation/commit | 丢弃预检状态，保留 Draft | 全部非法输入运行矩阵 |

## Route boundary

本条目只拥有通用事件与 owner 事实；Workbench、World、Graph 等条目只描述各自如何接入，不得复制本节规则。静态通过不证明 Unity 交互、视觉、Domain Reload 或 Runtime 行为。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md` (`0e7523fd7806a9be00a2bde8edb97a6b9f8e22c1830e1319a89a96e5ead0e00f`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/UGC工作台_UI Toolkit作者底座与草稿提交边界_AI协作警告.md` (`bda2011a12df8424e091a5e6d1cd9cbb8c8297dc0ea64c9ee49927df66f23177`)
- `Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs` (`1cbf739532885237235191b830c44b941336a59d9395c7e5ef90aa9ed982456f`)
- `Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchViewportFoundation.cs` (`c00ab4fa184c272a14bc78fa9f2338121ad35c33948ea5a4e7471473acdbcc89`)

`EvidenceLevel`: `S1`（源码与 AIWarnings；未运行 Unity 交互验收）
`StaleWhen`: Editor 事件合同、Input/Pointer owner、Workbench Host、Viewport foundation 或任一 SourceRef 哈希变化。
