# ES 预览编辑器入口

本目录保留 Inspector/窗口层的预览入口，例如：

```text
BasePreviewEditor<T>
CorePreviewEditor
MonoBehaviourPreviewEditor
```

真正的相机、灯光、RT、模型组、100m 隔离、缓存帧路径、ReloadDomain 清理等底层能力已经迁移到：

```text
Assets/Scripts/ESLogic/Editor/Preview
```

迁移原因：`State`、技能预览、资产包窗口都需要同一套底层，而 `EntityStateDomain.EditorPreview` 属于 `ES_Logic` 编译域，不能反向依赖 `ES_Editor`。

## 维护规则

1. 本目录负责“编辑器 UI 入口”和 `IPreviewElement` 生命周期收集。
2. 不要在本目录新增重复的相机、RT、截图、预览对象清理核心代码。
3. 新预览功能需要相机/灯光/RT/模型组时，直接使用 `ESEditorPreviewRenderContext`。
4. 新预览功能只需要静态缩略图、截图、HideFlags、Layer、清理时，直接使用 `ESEditorPreviewUtility`。
