# Editor 扩展交互、生命周期与绘制边界

`KnowledgeId`: `es.aiwarning.editor-extension-common-sense.v1`  
`Authority`: `AIWarnings + current Editor source`  
`RouteKeys`: `aiwarnings`, `editor`, `editor-window`, `imgui`, `uitoolkit`, `inspector`, `preview`, `lifecycle`, `performance`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `dd58dcc012ceb29ed0f783c382f1294929eb7689deeeaa9992e2768c358c8bdc`  
`SourceSetHash`: `dd58dcc012ceb29ed0f783c382f1294929eb7689deeeaa9992e2768c358c8bdc`  
`EntryBodyHash`: `a97ae4b4bae85dabf3c606a13a191542498a4bf2b498cc6a133628d5c03f8fa8`  
`StaleWhen`: `EditorWindow/Inspector/Preview 源码、Unity 事件行为、GUI 生命周期或 SourceRefs 变化。`

## 迁移说明

原 Warning 447 行、42,334 UTF-8 字节；现 Warning 保留 Editor/Runtime 隔离、OnGUI 事件、资源生命周期、目标绑定和验证边界。详细窗口定位、Odin/UI Toolkit 滚动、Inspector 事件路由、预览与菜单规则迁移至本条目。

## 绘制与窗口

- GUI/GUILayout/EditorGUILayout/GUIStyle 只能在 OnGUI 使用；OnEnable、静态初始化和 AssemblyStream 只做注册/引用维护。样式/纹理按主题缓存，在 Disable/Destroy/域重载安全点幂等释放。
- EditorWindow 定位使用主窗口、显式 Owner 或已捕获 GUI 坐标并夹取显示区域；禁止用 Screen.currentResolution 推导主窗口位置。Layout/Repaint 控件数量和顺序必须一致，中文按钮按内容计算宽度。
- Odin 外层滚动与自定义滚动必须区分；目标页只能有一个权威容器，禁止用 `UseScrollView=false` 冒充仅关闭横向滚动。Unity 实机才可证明滚动、DPI、多显示器和首开稳定。

## 生命周期、Inspector 与预览

- 不持久化 UnityEngine.Object/SerializedObject/InstanceId/RuntimeHandle；AssemblyStream 禁止全量扫盘和加载大资源。Editor 临时对象使用 HideAndDontSave，统一 DestroyImmediate，切换主题/域重载先释放旧资源。
- Inspector 事件必须先判断受保护子树，不能让时间轴根节点清选择/删除/关闭穿透按钮、文本框、ObjectField、ScrollView。Track/Clip 目标单一、同代、可追踪，主动关闭与失焦/域重载/目标失效分开处理。
- OnGUI/IMGUIContainer 禁止每帧创建资源、AssetDatabase、预览重建或全量校验；字段变更按 Undo/Dirty、轻量投影、空闲合并预览和明确落盘分层。GUI.color、labelWidth、wideMode 等必须用可恢复 Scope。
- 命令面板同一目标默认一条命令，打开/定位/复制用快捷键或明确动作前缀；资源设置页不得因布局重构隐藏模式、平台、目录和关键入口。

## EvidenceRefs

- `Assets/Plugins/ES/Editor/ESDrawer/Normal/ESInputActionDefineDrawer.cs`
- `Assets/Plugins/ES/Editor/EditorTools/ESWindowLauncher.cs`
- `Assets/Plugins/ES/Editor/EditorTools/ESCommandPalette/ESCommandPaletteRegistry.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md` (`f8b5dd538e5747a9fe5914fa30df168801db051911082dbfc397ddf767a439ce`)
- `Assets/Plugins/ES/Editor/ESDrawer/Normal/ESInputActionDefineDrawer.cs` (`c7503e27f57251bdb64658b110411cf99fd66346ecc8919fc9834549aba419ab`)
- `Assets/Plugins/ES/Editor/EditorTools/ESCommandPalette/ESCommandPaletteRegistry.cs` (`bfd5adc468a7a633ec9a0d68646eb049f6eae5840eb95f78d2902b81055dccd1`)
