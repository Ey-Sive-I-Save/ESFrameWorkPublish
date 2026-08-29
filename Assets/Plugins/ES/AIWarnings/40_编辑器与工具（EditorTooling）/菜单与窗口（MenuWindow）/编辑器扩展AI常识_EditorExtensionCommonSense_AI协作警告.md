# 编辑器扩展 AI 常识（项目权威规范）

Status: current
StableId: es.aiwarnings.editor.extension-common-sense.v1
Authority: ESFramework AIWarnings
RouteKeys: aiwarnings, editor, editor-window, imgui, uitoolkit, inspector, preview, lifecycle, performance
Applicability: EditorWindow、EditorGUI、Inspector、菜单、预览及 Editor-only UI
EvidenceRef: `Documentation/AIKnowledge/entries/aiwarning-editor-extension-common-sense.md`
StaleWhen: Editor UI 源码、Unity 事件行为、GUI 生命周期或 Knowledge SourceRefs 变化
Knowledge: `es.aiwarning.editor-extension-common-sense.v1`

## P0 长期约束

- GUI/GUILayout/EditorGUILayout/GUIStyle 只能在 OnGUI；OnEnable、静态初始化和 AssemblyStream 不得访问 GUI。Layout/Repaint 控件数量和顺序必须一致；中文按钮按内容计算宽度。
- 窗口定位使用主窗口、显式 Owner 或捕获 GUI 坐标并夹取显示区域，禁止用 Screen.currentResolution 推导 Editor 主窗口。多显示器、DPI、首开和重开行为需 Unity 实机证据。
- Editor 临时 Unity 对象必须缓存、HideAndDontSave、幂等 DestroyImmediate；不持久化 UnityEngine.Object、SerializedObject、InstanceId 或 RuntimeHandle，域重载和 PlayMode 前恢复临时状态。
- Inspector 事件不得穿透按钮、文本框、ObjectField、ScrollView 或 IMGUIContainer 清除选择/删除/关闭；Track/Clip 目标必须单一、同代、可追踪。滚动容器必须单一且方向明确。
- OnGUI/预览热路径禁止每帧创建资源、AssetDatabase、全量校验或重建；字段变更分离 Undo/Dirty、轻量投影、空闲合并预览和明确落盘。GUI Scope 必须异常安全恢复。
- 命令面板同一目标默认一条命令；打开/定位/复制使用快捷键或明确动作前缀。资源设置页不得因布局重构隐藏模式、平台、目录和关键入口。
- 静态编译不能证明 Editor 交互、保存 YAML、Prefab、Profiler、PlayMode 或发布行为；缺证据必须降级结论。

## 证据边界

详细窗口定位、Odin/UI Toolkit、Inspector 宿主识别、预览/音频、菜单去重、资源设置和历史问题迁移至 Knowledge；执行前必须回读当前源码。
