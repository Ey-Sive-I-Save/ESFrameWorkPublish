# ES 编辑器预览底层

这里存放 ESFramework 可被 `ES_Logic` 与 `ES_Editor` 共同引用的编辑器预览底层。

## 当前职责

```text
ESEditorPreviewCore
- ESEditorPreviewRenderContext：统一管理相机、灯光、RenderTexture、PreviewScene/隐藏对象场景、100m 隔离点、模型组。
- ESEditorPreviewModelHandle：统一管理预览模型实例、HideFlags、Layer、Renderer 状态复制、Bounds。
- ESEditorPreviewLifecycleHub：统一处理 ReloadDomain、PlayMode 切换、编辑器退出时的预览清理。
- ESEditorPreviewPersistentFramePaths：统一项目外缓存帧路径，默认 Library/ESPreviewFrames，不写入 Assets。

ESEditorPreviewUtility
- 临时对象创建、HideFlags、Layer、URP CameraData、RenderTexture、截图、AssetPreview 兜底、Renderer 状态复制、运行时组件禁用、残留对象清理。

ESEditorPreviewResourceScope
- 业务侧临时资源作用域。State、Skill、TrackView、资产预览等如果自己创建临时资源，必须登记到这里或交给 RenderContext。

EditorRememberedEntityTarget
- 编辑器预览目标记忆。用于 State/技能预览在切换 Inspector 或重建时保持目标。
```

## 接入规则

1. 业务预览不再自己创建相机、灯光、RT、模型隔离组，统一交给 `ESEditorPreviewRenderContext`。
2. 业务只负责采样、播放、时间、配置和调试信息。
3. 大预览优先实时渲染，小格子批量动画预览优先使用项目外缓存帧。
4. 禁止临时生成 `AnimatorController` 做 Humanoid 预览，已验证会引发 `UnityEditor.Graphs` 风险。
5. 临时对象必须 `HideAndDontSave` 或采样安全 HideFlags，并接入清理标记。
6. 缓存帧不得写入 `Assets/`，避免污染项目资源库。
