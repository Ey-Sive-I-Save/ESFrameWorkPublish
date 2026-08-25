# Editor Preview 生命周期与临时对象边界导航

`KnowledgeId`: `es.editor.preview-lifecycle.v1`
`Authority`: `Current source + AIWarnings`
`RouteKeys`: `editor`, `preview`, `preview-scene`, `preview-object`, `rendertexture`, `resource-scope`, `reload-domain`, `playmode`, `cleanup`, `editor-lifecycle`
`ContentHash`: `5682062c6a48449382552760c6a738d8985dcfadd4565c37b637d736fe3c2747`

## Canonical facts

- 新增编辑器预览优先使用 `ESEditorPreviewRenderContext`、`ESEditorPreviewModelHandle`、`ESEditorPreviewResourceScope` 和 `ESEditorPreviewLifecycleHub`；业务窗口不重复实现 Camera、Light、RT、PreviewScene 或全局清理。
- 普通窗口 `Dispose` 只释放自己拥有的 context/model/scope；全局 `CleanupAll` 只属于全局事件、显式菜单或确实需要清理全部 ES 预览残留的边界。
- 临时 GameObject 必须有预览标记、`HideAndDontSave` 和确定性 Destroy/Dispose；RenderTexture 销毁前必须 Release；静态缓存清理必须销毁自己创建的 Unity native object。
- ReloadDomain、PlayMode 切换、窗口禁用/关闭和编辑器退出都属于预览释放边界。缓存帧、AssetPreview 返回纹理和业务自建纹理的所有权不能混用。

## Failure prevention

| 失败面 | 预防检查 | 正确恢复 | 未证明 |
|---|---|---|---|
| 每个窗口再造一套 PreviewScene/RT | 检查是否接入公共 preview context/scope | 收口到公共底层，保留业务采样逻辑 | 所有旧窗口迁移完成 |
| 窗口关闭后残留对象/RT | 检查 Dispose、ReloadDomain、PlayMode 和退出钩子 | 释放 context/scope/handle，必要时由全局 Hub 清理 | Unity 内存/场景残留 |
| 普通 Dispose 误清其他窗口 | 检查是否调用全局 CleanupAll | 只释放本窗口 owner | 多窗口并发实机行为 |

## Route boundary

本条目拥有通用预览资源和生命周期边界；AssetPackage、World、Entity 等条目只描述业务适配和迁移状态。它不证明任何窗口已经完成 Runtime、内存或视觉验收。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/预览与生命周期（PreviewLifecycle）/编辑器预览系统生命周期_AI协作警告.md` (`f47b625e802478c67336fc12cfbd42fdb29e71832f7c5dc796343a36b4cf7d1c`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/预览与生命周期（PreviewLifecycle）/内存泄露与编辑器生命周期_AI协作警告.md` (`b2fdf355777d58b2037407ef9211925eec6c39632ff66e18053799f23033e866`)
- `Assets/Scripts/ESLogic/Runtime/EditorPreview/ESEditorPreviewCore.cs` (`3d0b58271163258289e89a408028f89cf4971cf0186590d6c331c8eb28598a74`)
- `Assets/Scripts/ESLogic/Runtime/EditorPreview/ESEditorPreviewResourceScope.cs` (`37eb3cfdf66e11f0123536c6dbe529736ab47fedcd2045596e264a7a298e9769`)
- `Assets/Scripts/ESLogic/Runtime/EditorPreview/ESEditorPreviewUtility.cs` (`b62962881b5ccc6105caa2e78cac42b6fb8a99671c6b5702ca6a7cbbc00ec30a`)
- `Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchPreviewScene.cs` (`0c8df64d8d944118c65f97b0012ea391f1beb46a0f5c8b8d911553bfab734d26`)

`EvidenceLevel`: `S1`（源码与 AIWarnings；Unity 生命周期和内存验收未运行）
`StaleWhen`: EditorPreview context/hub/scope、PreviewScene ownership、ReloadDomain/PlayMode cleanup 或任一 SourceRef 哈希变化。
