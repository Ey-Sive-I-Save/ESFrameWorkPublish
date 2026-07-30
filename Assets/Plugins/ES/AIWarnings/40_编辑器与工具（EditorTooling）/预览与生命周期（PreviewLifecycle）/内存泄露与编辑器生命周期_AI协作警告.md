# 内存泄露与编辑器生命周期_AI协作警告

作者职责：Codex，负责编辑器预览底层、资产包预览工作流、玩家/生命体模型重构协作中的工程风险审计。

更新时间：2026-07-22，中国时间。

## 当前结论

ESFramework 现在不能再把“编辑器窗口关闭时会自然释放”当成安全假设。Unity 编辑器里最容易泄露的是：

1. `EditorApplication.update +=` 后没有稳定 `-=`。
2. `RenderTexture` 只 `DestroyImmediate`，没有先 `Release()`。
3. `Texture2D`、`Material`、`PreviewRenderUtility`、隐藏预览对象没有 `HideAndDontSave` 和清理入口。
4. `PlayableGraph`、`HumanPoseHandler`、临时预览模型在 Domain Reload、切 PlayMode、退出编辑器前没有强制清理。
5. 静态 Dictionary/List 缓存没有上限，或者 Clear 时没有销毁自己创建的 Unity native object。
6. 外部 `Process` 面板只靠用户手动关闭，没有在窗口禁用时 Kill/Dispose。

## 已修正的底线

- `ESEditorPreviewLifecycleHub` 是全局预览生命周期入口，普通窗口/功能自己的 `Dispose()` 只释放自己的 context/scope/handle，不要随手调用全局 `CleanupAll()`。
- `ESEditorPreviewResourceScope` 只做局部资源登记，启动注册交给 `EditorInvoker_Level2`，不要新增 `InitializeOnLoadMethod`。
- `ESEditorPreviewUtility.DestroyObject()` 对 `RenderTexture` 已做防御释放：销毁前先 `Release()`。
- `EditorTimelinePlayer` 已接入 `EditorInvoker_Level2` 全局清理：重编译、退出编辑器、切 PlayMode 时会 Stop、退 update、归还预览目标。
- `ESMenuTreeWindowAB.blackTexture` 和 `ESLibraryTemplate.buttonBackground` 这类静态编辑器 Texture 必须 `HideAndDontSave`，不要写入场景/资产。
- 资产包窗口的模型预览缓存、缓存帧、fallback 材质必须走统一 Clear/Dispose，不允许只清字典引用。

## 后续 AI 必须遵守

1. 新增编辑器预览功能时，优先使用 `Assets/Scripts/ESLogic/Editor/Preview` 下的底层：
   - `ESEditorPreviewRenderContext`
   - `ESEditorPreviewModelHandle`
   - `ESEditorPreviewResourceScope`
   - `ESEditorPreviewUtility`
   - `ESEditorPreviewLifecycleHub`
2. 不要在业务窗口里重复写相机、灯光、RT、隐藏对象清理核心逻辑。
3. 小格子批量动画预览不要实时多开 PreviewRenderUtility，优先使用项目外持久化缓存帧。
4. 大预览窗口可以实时渲染，但必须绑定 context 生命周期，关闭窗口必须释放。
5. `EditorApplication.update +=` 必须有成对 `-=`，并且要覆盖 OnDisable/OnDestroy/ReloadDomain/PlayMode 切换中至少一个强制清理入口。
6. `Process` 必须在窗口禁用/关闭时 Stop/Kill/Dispose，输出队列必须有长度上限。
7. 静态缓存如果保存的是自己 new 出来的 `Texture2D`、`Material`、`RenderTexture`，Clear 时必须 Destroy；如果是 Unity 的 `AssetPreview` 返回图，不要手动 Destroy。
8. `PlayableGraph` 只要 Create，就必须能证明 Stop/Dispose/OnDestroy/ReloadDomain 会 Destroy。
9. `HumanPoseHandler` 是托管 Dispose 对象，用完必须 Dispose，不要长期挂在静态对象里。
10. `HideAndDontSave` 不是释放，它只防止污染保存；最终仍然要 Destroy/Dispose。

## 当前风险清单

- [低风险] `ESCmdAgentWindow` 已在 `OnDisable` 停止进程并退 update；若后续改成后台常驻，需要额外全局退出钩子。
- [低风险] `BasePreviewEditor<T>` 已在 `OnDisable` 退 update 并释放 active preview elements；新增 Provider 必须实现自己的释放。
- [低风险] `EntityStateDomain.EditorPreview` 已接底层预览 context；不要再恢复本地相机/RT/灯光代码。
- [中风险] `ESAssetPackageBakeWindow.cs` 仍然很大，资产包预览、缓存帧、导出链路集中在一个文件，后续应拆成 workflow/service，但拆分时不能改变缓存帧协议。
- [中风险] 第三方插件如 vHierarchy/vFolders/DOTween/Odin/KCC/EasySave 有自己的编辑器缓存和 update 链路，默认不改插件源码；只在确认插件版本 bug 时再处理。

## 编译验证记录

2026-07-22 已验证：

- `dotnet build ES_Logic.csproj --no-restore -v:minimal`：通过，0 警告，0 错误。
- `dotnet build ES_Editor.csproj --no-restore -v:minimal -p:BuildProjectReferences=false`：通过，0 警告，0 错误。

