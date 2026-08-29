# 编辑器预览系统生命周期边界

Status: current
StableId: es.aiwarnings.editor.preview-lifecycle-boundary
Authority: AIWarnings
RouteKeys: aiwarnings, editor, preview, lifecycle, reload
Applicability: 新增或修改 ES 编辑器模型/材质/动画/特效预览、缓存、临时对象或生命周期清理时。
EvidenceRef: Assets/Scripts/ESLogic/Runtime/EditorPreview/ESEditorPreviewCore.cs -RouteId es.aiwarnings.editor.preview-lifecycle-boundary
Owner: ES Editor/Preview
StaleWhen: PreviewCore、ResourceScope、全局生命周期钩子、缓存路径或 Editor asmdef 边界变化。
Knowledge: es.aiwarning.editor.preview-lifecycle-boundary.v1

长期约束：
- 编辑器预览统一经 `ESEditorPreviewRenderContext`、`ESEditorPreviewLifecycleHub`、`ESEditorPreviewUtility`、`ESEditorPreviewResourceScope`；业务层不得复制 Camera、Light、RenderTexture、PreviewScene、隔离组或全局清理。
- 普通 Dispose 只释放本窗口的 context、model handle、scope；只有全局事件、手动清理菜单或确需清除全部残留时才调用 `CleanupAll`。
- 临时对象使用 `HideAndDontSave` 与预览标记；小格子动画用项目外缓存帧，不写入 `Assets/`；禁止临时 AnimatorController 和每帧完整 PreviewRenderUtility。
- 不要在业务类新增全局 ReloadDomain 注册；全局钩子归公共生命周期中心，应用层只负责选择、采样、播放和自身资源释放。
- 该目录处于 Runtime 目录不代表可进入 Player；Editor 条件和程序集边界必须保持。静态证据不能证明 Unity、Reload、交互、内存或发布验收。
