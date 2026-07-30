# 编辑器预览系统生命周期_AI协作警告

职责：指导后来的 AI 正确接入 ESFramework 的编辑器预览底层，避免重复写相机、RT、临时对象、ReloadDomain 清理和小格子缓存逻辑。

## 当前结论

ES 编辑器预览底层已经收口到：

```text
Assets/Scripts/ESLogic/Editor/Preview
```

核心类型：

```text
ESEditorPreviewLifecycleHub
全局生命周期中心。只负责 ReloadDomain、PlayMode 切换、编辑器退出、手动菜单时的全局 Dispose 和残留清理。

ESEditorPreviewRenderContext
一次预览上下文。负责相机、灯光、RenderTexture、PreviewScene 或隐藏对象场景、100m 隔离点、模型组创建和渲染。

ESEditorPreviewModelHandle
单个预览模型实例句柄。负责模型实例释放、Bounds、稳定视角中心。

ESEditorPreviewUtility
底层工具。负责 HideFlags、Layer、标记、RT 创建释放、截图、AssetPreview 兜底、Renderer 状态复制、运行时 Behaviour 禁用、残留标记对象清理。

ESEditorPreviewResourceScope
业务侧临时资源作用域。只管本作用域资源 Dispose，不再作为全局清理中心。
```

## 必须遵守

1. 新增大预览，不要自己写 Camera、Light、RenderTexture、PreviewScene、模型隔离组，直接用 `ESEditorPreviewRenderContext`。
2. 新增预览模型，不要自己递归设置 HideFlags/Layer，不要自己复制 Renderer 状态，使用 `CreateModelGroup` 或 `ESEditorPreviewUtility`。
3. 业务窗口的普通 `Dispose` 只释放自己持有的 context、model handle、scope，禁止随手调用 `ESEditorPreviewLifecycleHub.CleanupAll`。
4. 只有全局事件、手动清理菜单、确实需要清所有 ES 预览残留时，才能调用 `ESEditorPreviewLifecycleHub.CleanupAll`。
5. 临时 GameObject 必须带 `EditorPreviewGameObjectSign`，优先通过 `ESEditorPreviewUtility.TryMarkPreviewObject` 或 `RenderContext.CreateModelGroup` 间接标记。
6. 临时对象必须使用 `HideAndDontSave`；需要 `AnimationMode.SampleAnimationClip` 的采样对象才使用采样安全 HideFlags。
7. 小格子批量动画预览不要实时跑完整 PreviewRenderUtility，多数情况下应使用项目外缓存帧，路径由 `ESEditorPreviewPersistentFramePaths` 生成。
8. 缓存帧不要写入 `Assets/`，默认放 `Library/ESPreviewFrames`，避免污染资源库和版本控制。
9. 禁止临时生成 `AnimatorController` 做 Humanoid 预览；该方案已触发过 `UnityEditor.Graphs.Edge.WakeUp` 异常。
10. 不要在业务类里新增 `[InitializeOnLoadMethod]` 注册预览清理。项目要求优先走 `EditorInvoker_Level0/1/2/50`，当前预览生命周期由 `ESEditorPreviewResourceScopeInitializer` 调用 `ESEditorPreviewLifecycleHub.RegisterGlobalHooks()`。

## 应用层边界

应用层只应该负责：

```text
选哪个对象
采样哪个时间
播放/暂停/停止
绘制业务按钮和调试信息
持有并 Dispose 自己创建的 context/scope/handle
```

应用层不应该负责：

```text
创建预览相机
创建预览灯光
创建 RenderTexture
分配 100m 隔离点
递归设置 HideFlags/Layer
全局扫描残留对象
ReloadDomain 清理注册
PlayMode 切换清理注册
```

## 已纠正的旧理解

- [过时] “每个窗口自己写一套预览相机、RT、截图和清理。”
  现在统一交给 `ESEditorPreviewRenderContext` 和 `ESEditorPreviewUtility`。

- [过时] “业务 Dispose 时顺手全局 CleanupAll 更安全。”
  这是危险做法，会误伤其他打开的预览窗口。普通 Dispose 只释放自己，全局清理只用于全局事件或菜单。

- [过时] “ResourceScope 也可以注册 ReloadDomain 全局清理。”
  现在全局生命周期只归 `ESEditorPreviewLifecycleHub`。`ResourceScope` 是业务局部资源容器。

- [过时] “State、Skill、TrackView 的 Sampler 要强行统一。”
  不需要。Sampler 属于业务推进逻辑，可以保持 Skill 专属、State 专属、TrackView 专属；公共底层只负责把采样后的对象稳定渲染出来。

## 后续迁移优先级

1. `State`、技能预览、TrackView 大预览继续使用 `ESEditorPreviewRenderContext`。
2. 资产包窗口中仍然存在的 PreviewRenderUtility 小块逻辑，后续逐步改为底层 context 或缓存帧工作流。
3. 编辑器窗口里的 update 订阅后续可继续收敛，但不要一次性重写所有窗口。
4. Obsolete 目录不要优先迁移，除非它仍参与编译或被当前工具调用。

## 判断标准

如果一个预览改动完成后满足以下条件，才算生命周期合格：

```text
关闭窗口不会留下预览对象
ReloadDomain 前会释放 RT、Texture2D、PreviewScene、隐藏 GameObject
进入/退出 PlayMode 不污染场景
一个窗口 Dispose 不会清掉另一个窗口的预览
业务层没有重复相机/灯光/RT/全局清理代码
编译 ES_Logic 和 ES_Editor 均通过
```
