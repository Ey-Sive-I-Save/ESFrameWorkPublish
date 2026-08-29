# 编辑器预览生命周期边界

`KnowledgeId`: `es.aiwarning.editor.preview-lifecycle-boundary.v1`  
`Authority`: `AIWarnings + current EditorPreview source`  
`RouteKeys`: `aiwarnings`, `editor`, `preview`, `lifecycle`, `reload`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `551bc2c2855171422c2c6c235d4861537f18146b99d125f54c6afa3505504938`  
`SourceSetHash`: `551bc2c2855171422c2c6c235d4861537f18146b99d125f54c6afa3505504938`  
`EntryBodyHash`: `2fa7dbeff6624d5554b9e7c469760d1af8e970243245d083585f961f741c6c35`  
`StaleWhen`: PreviewCore、ResourceScope、全局生命周期钩子、缓存路径或 Editor asmdef 边界变化。

## 迁移范围

原 Warning 114 行、6,575 UTF-8 字节；现 Warning 17 行，保留公共预览底层、局部/全局清理、临时对象、缓存目录、程序集和证据边界。核心类型职责、应用层边界、AssetPackage 接入、过时方案和验收标准迁入本条目。

## 当前事实

- `ESEditorPreviewCore.cs` 集中提供预览上下文、生命周期中心和模型句柄；`ESEditorPreviewUtility.cs` 处理 HideFlags、Layer、RenderTexture、截图、Renderer 状态与残留标记；`ESEditorPreviewResourceScope.cs` 仅管理业务侧临时资源。
- 应用层负责选择对象、采样时间、播放/暂停/停止及自身资源 Dispose，不负责创建 Camera/Light/RenderTexture/PreviewScene、分配隔离点、递归设置标记或注册全局清理。
- 预览临时对象必须带 `EditorPreviewGameObjectSign` 并使用 `HideAndDontSave`；缓存帧默认位于 `Library/ESPreviewFrames`，避免 AssetDatabase 污染。目录中的 Runtime 是组织结果，不是 Player 许可。
- AssetPackage 播放器应持有公共 RenderContext；业务层可以保留采样、分类和导出逻辑，但不得新增独立 PreviewScene 或第二套生命周期。
- ReloadDomain、PlayMode、编辑器退出和手动清理应由全局生命周期中心处理；普通窗口 Dispose 不能误伤其他预览窗口。

## 过时方案与验收边界

已废弃每窗口自建预览底层、Dispose 时全局 `CleanupAll`、业务类重复注册 Reload 钩子、每帧实时批量渲染和临时 AnimatorController。源码搜索、UTF-8、`.csproj` 或 `git diff --check` 不能替代 Unity Editor 导入、Reload、交互、Profiler/Memory 和发布证据。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/预览与生命周期（PreviewLifecycle）/编辑器预览系统生命周期_AI协作警告.md` (`da41da7f309bdc11783f6febe99ae824cfb7858ebcb09f12ea797e3b5a9bddfc`)
- `Assets/Scripts/ESLogic/Runtime/EditorPreview/ESEditorPreviewCore.cs` (`b1042b43ebb0c600b8e42f080c0c60b85e24b8e942c5f99f50f5c0165ff851de`)
- `Assets/Scripts/ESLogic/Runtime/EditorPreview/ESEditorPreviewUtility.cs` (`a3670b59f6a6924f9353ad6f7d95e232076eac0dd6b616558ae02d86bfbd44a1`)
- `Assets/Scripts/ESLogic/Runtime/EditorPreview/ESEditorPreviewResourceScope.cs` (`08e7c9afec2949bda66c578b5d472230955105e70bf9af0c702d1558504b68d3`)
- `Assets/Scripts/ESLogic/Editor/Preview/README.md` (`2f0f9fe63b98d9ce16c9a70bc21d1fdc8cf60eb711b64bafff5dbec4ca83a0de`)

## EvidenceRefs

- `Assets/Scripts/ESLogic/Runtime/EditorPreview/ESEditorPreviewCore.cs`
- `runtime-not-run`
