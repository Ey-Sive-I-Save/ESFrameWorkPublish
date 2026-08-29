# ES 编辑器内存泄露与生命周期边界：保真 Knowledge

`KnowledgeId`: `es.aiwarning.editor.memory-leak-lifecycle-boundary.v1`  
`Authority`: `AIWarnings` 与当前 Editor/Preview 实现  
`RouteKeys`: `aiwarnings`, `editor`, `lifecycle`, `memory`, `dispose`, `domain-reload`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `39281aff3a8db46481322cfbe245b32985b98fac86f2250ce39e9c9ad006e557`  
`SourceSetHash`: `39281aff3a8db46481322cfbe245b32985b98fac86f2250ce39e9c9ad006e557`  
`EntryBodyHash`: `0f64ff428e82fb9a0cfc7a415ecfe51186d4497a649c73c7631f448fc7508842`  
`StaleWhen`: 预览生命周期底层、EditorInvoker、资源释放实现或任一 SourceRef 变化。

## 迁移范围

Warning 保留编辑器资源确定性释放、回调解绑、权限与证据边界；本条目承载已修正底线、组件清单、风险分级、详细后续规则、历史编译记录和原文快照。Knowledge 不替代 AIWarnings、源码或运行证据。

## 已修正底线与组件边界

- `ESEditorPreviewLifecycleHub` 是全局预览生命周期入口；普通窗口/功能的 `Dispose()` 只释放自己的 context/scope/handle，不随意调用全局 `CleanupAll()`。
- `ESEditorPreviewResourceScope` 只登记局部资源，启动注册由 `EditorInvoker_Level2` 负责，不新增 `InitializeOnLoadMethod`。
- `ESEditorPreviewUtility.DestroyObject()` 对 `RenderTexture` 先 `Release()` 再销毁。
- `EditorTimelinePlayer` 接入 `EditorInvoker_Level2` 全局清理，覆盖重编译、退出编辑器和切 PlayMode 时停止、退 update、归还预览目标。
- `ESLibraryTemplate.buttonBackground`、Presentation 动态纹理等当前窗口创建的静态 Editor Texture 必须 `HideAndDontSave`，并在缓存失效、域重载或受控卸载时确定性销毁；已删除的 `ESMenuTreeWindowAB.blackTexture` 不是现行源码入口。
- 资产包窗口模型预览缓存、缓存帧和 fallback 材质走统一 Clear/Dispose，不能只清字典引用。

## 后续规则

1. 新增预览优先使用 `Assets/Scripts/ESLogic/Runtime/EditorPreview` 下的 Editor-only 底层：`ESEditorPreviewRenderContext`、`ESEditorPreviewModelHandle`、`ESEditorPreviewResourceScope`、`ESEditorPreviewUtility`、`ESEditorPreviewLifecycleHub`；目录名中的 `Runtime` 不授权进入 Player。
2. 不在业务窗口重复实现相机、灯光、RT、隐藏对象清理；小格子批量动画预览优先使用项目外持久化缓存帧，大预览绑定 context，关闭时释放。
3. `EditorApplication.update +=` 必须有成对 `-=`，并覆盖 OnDisable/OnDestroy/ReloadDomain/PlayMode 切换中的至少一个强制清理入口。
4. `Process` 在窗口禁用/关闭时 Stop/Kill/Dispose，输出队列有长度上限；静态缓存自己创建的 `Texture2D`、`Material`、`RenderTexture` 时 Clear 必须 Destroy，`AssetPreview` 返回图不得手动 Destroy。
5. `PlayableGraph` Create 后必须能证明 Stop/Dispose/OnDestroy/ReloadDomain 会 Destroy；`HumanPoseHandler` 用完 Dispose，不长期挂在静态对象；`HideAndDontSave` 不是释放。

## 当前风险与历史证据

- 低风险：`ESCmdAgentWindow` 在 OnDisable 停止进程并退 update；后台常驻需额外全局退出钩子。
- 低风险：`BasePreviewEditor<T>` 在 OnDisable 退 update 并释放 active preview elements；新增 Provider 负责自身释放。
- 低风险：`EntityStateDomain.EditorPreview` 已接底层预览 context，不恢复本地相机/RT/灯光代码。
- 中风险：`ESAssetPackageBakeWindow.cs` 仍集中资产包预览、缓存帧和导出链路，后续拆分不得改变缓存帧协议。
- 中风险：vHierarchy/vFolders/DOTween/Odin/KCC/EasySave 等第三方插件默认不改源码，除非确认版本缺陷。

2026-07-22 的 `dotnet build ES_Logic.csproj --no-restore -v:minimal` 与 `dotnet build ES_Editor.csproj --no-restore -v:minimal -p:BuildProjectReferences=false` 记录为 0 警告、0 错误；本轮未运行 Unity/Runtime。

## 原文快照

迁移前原始文件为 58 行、4653 UTF-8 字节，原始 SHA-256 为 `b2fdf355777d58b2037407ef9211925eec6c39632ff66e18053799f23033e866`。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/预览与生命周期（PreviewLifecycle）/内存泄露与编辑器生命周期_AI协作警告.md` (`8b867e93038fcf467efdf81f6803487e2e20ae4e23082c725ca2e195b9b7c95e`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`aef840920b065b91c488a52b524e0537cd429b188a95afbffc75bfed2e03a0a3`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-editor-memory-leak-lifecycle-boundary.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/预览与生命周期（PreviewLifecycle）/内存泄露与编辑器生命周期_AI协作警告.md`
