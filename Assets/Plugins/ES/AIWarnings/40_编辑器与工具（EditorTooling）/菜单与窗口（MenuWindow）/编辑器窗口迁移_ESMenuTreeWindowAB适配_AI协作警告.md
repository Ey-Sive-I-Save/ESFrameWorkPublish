# 编辑器窗口迁移：ESMenuTree、SinglePage 与 Odin 兼容外壳

Status: current
StableId: es.aiwarnings.editor.window-shell-migration-boundary
Authority: AIWarnings
RouteKeys: aiwarnings, editor, window, menutree, singlepage, odin
Applicability: 修改 ES 编辑器窗口基类、Toolkit/IMGUI/Odin 适配、临时检查器或窗口迁移时。
EvidenceRef: Assets/Plugins/ES/Editor/ESMenuTreeWindow/-Templates/-ESMenuTreeWindow.cs -RouteId es.aiwarnings.editor.window-shell-migration-boundary
Owner: ES Editor/Window
StaleWhen: MenuTree/SinglePage/Odin 外壳、页面上下文、Presentation 或窗口程序集边界变化。
Knowledge: es.aiwarning.editor.window-shell-migration-boundary.v1

长期约束：
- `ESMenuTreeWindow<T>`、`ESSinglePageWindow<T>`、`ESSinglePageIMGUIWindow<T>` 和 `ESOdinMenuTreeWindow<T>` 是不同适配层；按窗口真实交互选择，不为视觉统一强迁移。
- 旧文件名 `ESMenuTreeWindowAB` 不代表仍存在旧类型；禁止按名称恢复旧基类或复制第二套窗口外壳。
- 迁移必须保留原保存、关闭、Undo/Dirty、菜单、焦点、拖拽和临时窗口 owner 语义；业务逻辑不因套壳重写。
- Toolkit 页面使用稳定页面 ID、页面上下文和 `ESMenuTreeBuilder`；`QuickBuildRootMenu` 等仅属于 Odin 兼容路径，不得混用。
- 标准外壳负责 System/Global/Window/Page 动作层；自定义主体窗口必须显式提供 `ESWindowActionHosts`，不得依赖未知 Toolbar 或绝对定位回退。
- 临时检查器按需打开，不塞入主菜单树；父子窗口使用显式 owner、稳定 ownerKey 和半休眠规则。
- 源码/静态检查不能证明 Unity 菜单、页面、交互、ReloadDomain、性能或商业级迁移完成。
