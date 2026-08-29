# 内存泄露与编辑器生命周期_AI协作警告

Status: current
StableId: es.aiwarning.editor.memory-leak-lifecycle-boundary.v1
Authority: AIWarnings；详细事实见 Knowledge
RouteKeys: aiwarnings, editor, lifecycle, memory, dispose, domain-reload
Applicability: Unity Editor 预览窗口、资产包预览、编辑器进程面板和相关缓存
Owner: ESFramework EditorTooling 维护者
EvidenceRef: Documentation/AIKnowledge/entries/aiwarning-editor-memory-leak-lifecycle-boundary.md
StaleWhen: 预览生命周期底层、EditorInvoker、资源释放实现或任一 SourceRef 变化。

## 长期约束

- 编辑器窗口关闭不等于资源自然释放；回调必须成对解绑，`RenderTexture` 先 `Release()` 再销毁，`Texture2D`、`Material`、`PreviewRenderUtility`、隐藏对象、`PlayableGraph`、`HumanPoseHandler`、临时模型、静态缓存和外部 `Process` 都必须有确定性清理入口。
- 优先复用 Editor-only 预览底层和 `ESEditorPreviewLifecycleHub`；业务窗口只释放自己的 context/scope/handle，不得随意调用全局 `CleanupAll()` 或重复实现相机、灯光、RT、隐藏对象清理。
- `EditorApplication.update`、Domain Reload、重编译、PlayMode 切换、OnDisable/OnDestroy 必须覆盖可证明的停止、解绑、Dispose/Destroy 路径；`HideAndDontSave` 仅防止保存污染，不替代释放。
- 静态缓存清理必须区分自己创建的 Unity native object 与 `AssetPreview` 返回对象；前者销毁，后者不得手动销毁。`Process` 必须在窗口禁用/关闭时 Stop/Kill/Dispose，并限制输出队列。
- 小格子批量预览优先使用持久化缓存帧；实时大预览必须绑定 context 生命周期。第三方插件默认不改源码。上述约束不等同于 Unity/Runtime 验收。

## Knowledge 导航

详细的已修正底线、组件清单、风险分级、逐项后续规则、历史编译记录和原文快照见 `es.aiwarning.editor.memory-leak-lifecycle-boundary.v1`。Knowledge 只提供可追溯事实，不授予修改或运行权限。
