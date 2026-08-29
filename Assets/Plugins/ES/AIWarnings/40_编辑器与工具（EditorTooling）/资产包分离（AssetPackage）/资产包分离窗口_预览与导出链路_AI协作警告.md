# 资产包分离窗口：预览与导出边界

Status: current
StableId: es.aiwarnings.editor.asset-package-preview-export-boundary
Authority: AIWarnings
RouteKeys: aiwarnings, editor, asset-package, preview, export
Applicability: 修改资产包烘焙窗口、资源预览、分类/搜索/排序、导出或回退链路时。
EvidenceRef: ES/Tools/Validation/Test-ESMenuArchitecture.ps1 -RouteId es.aiwarnings.editor.asset-package-preview-export-boundary
Owner: ES Editor/Tooling
StaleWhen: AssetPackage 数据/窗口、PreviewWorkflow、导出事务、缓存策略或回退合同变化。
Knowledge: es.aiwarning.editor.asset-package-preview-export-boundary.v1

长期约束：
- 这是编辑器资源治理工具，不是运行时资源加载系统；不得让 ESInput、ESCommand、GameManager、RuntimeMode 或 Interaction 直接依赖窗口。
- 所有大/动态预览必须经 `ESAssetPackagePreviewWorkflow` 及专职下游，复用上下文、相机、灯光、隔离层、缓存和清理生命周期；不得新增第三套 Preview 底层。
- 小格子动画使用缓存帧，大预览使用实时渲染；缓存不得写入 `Assets/`，临时实例不得写回源 Prefab、材质、VFX 或正式资产。
- 普通 Dispose 只释放本窗口持有的资源；不得调用全局清理破坏其他预览窗口，也不得新增页面级 ReloadDomain/全局缓存规则。
- 导出必须经过依赖通报、源 GUID→目标路径链路、漂移确认、事务/回退与链路记录；默认不重复导出，不以目标文件存在 alone 判定有效。
- 修改 UI 或预览时保留 ES Presentation 统一出口、单一滚动容器和明确失败/重试/取消状态；不得把静态检查冒充 Unity 实机或商业级验收。

静态证据仅覆盖源码、配置和验证脚本；Unity 交互、Domain Reload、Profiler、Player/发布行为仍未证实。
