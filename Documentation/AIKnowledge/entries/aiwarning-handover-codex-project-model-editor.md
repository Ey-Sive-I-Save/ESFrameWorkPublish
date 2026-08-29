# 历史交接：Codex 项目模型重构与编辑器稳定

`KnowledgeId`: `es.aiwarning.handover.codex-project-model-editor.v1`  
`Authority`: `AIWarnings project handover + current source`  
`RouteKeys`: `aiwarnings`, `handover`, `historical`, `entity-model`, `editor`, `preview`, `lifecycle`, `aicommands`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `768f15fd2b65b7eaab808441364d06cffd5f9b484b8ec195e54eda52cea5f17c`  
`SourceSetHash`: `768f15fd2b65b7eaab808441364d06cffd5f9b484b8ec195e54eda52cea5f17c`  
`EntryBodyHash`: `e984eb102af5313d7529122c173f5310917b1e40b17c19603d657c202a960ffd`  
`StaleWhen`: `项目模型、编辑器预览底层、生命周期合同或 SourceRefs 变化。`

## 保真迁移

原 Warning 256 行、13,357 UTF-8 字节；现 Warning 保留历史性质、模型/编辑器不变量和证据边界。详细上下文、废止方案、预览踩坑、资源导出与生命周期清单迁移至本条目。

## 模型与资源导出

- 角色模型面向玩家、NPC、怪物、召唤物、载具、切换角色、剧情和网络代理；统一 Root/Entity、Domain/Module、Control Request、View/Animator/VFX/Audio、Sensors/Target 分层。控制请求来源可为输入、AI、剧情、网络、回放，但实际执行复用正式入口。
- 资源包导出必须建立源 GUID→目标路径链路、依赖摘要、分类和可回退状态；默认不重复导出或自动改名，不能只检查目标文件存在。BakeData/配置应持久化，运行时与 Editor Library/AssetPackage 分层。
- 已废止临时 AnimatorController 预览、每格实时 PreviewRenderUtility、重复导出自动唯一名、运行时全量扫描和把 Transform 天然当性能瓶颈等旧方向；小格预览优先缓存帧、可见页队列和统一 Preview 底层。

## 编辑器与生命周期

- 预览对象使用 HideAndDontSave，统一管理 Camera/RT/Light/Model/Animator，覆盖窗口关闭、配置/模型切换、重建、异常和 ReloadDomain 释放；不可污染场景或复用状态导致 T Pose。
- 长生命周期事件使用命名方法，注册前 `-=` 再 `+=`；OnDisable/Dispose/Reload 解绑并释放 RT、PlayableGraph、PreviewRenderUtility。AssemblyStream 只做轻量 Editor 元数据注册，不全盘扫描或加载重对象。
- AICommands 声明权限、必读、验证和交付；AIWarnings 保存长期事实/禁止事项；Persona/AITalk 不得替代它们。中文文件必须严格 UTF-8，工作树检查只读且不回滚并行变更。

## 证据边界

需要单独验证模型切换、资产导出/依赖、预览帧一致性、Inspector、域重载、内存/GC、PlayMode、Profiler、Player、IL2CPP 和发布；本历史条目不提供这些运行证据。

## EvidenceRefs

- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md`
- `Assets/Plugins/ES/Editor/EditorTools/ESWindowLauncher.cs`
- `runtime-not-run`

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/80_交接与复盘（Handover）/项目总交接（ProjectHandover）/Codex_当前项目总交接_模型重构与编辑器稳定_AI协作警告.md` (`3e8beeaf530b5ca541491f37a35bab85f991b3fa3e0efd9a6c8212dc1f8671d1`)
- `Assets/Plugins/ES/AIWarnings/40_编辑器与工具（EditorTooling）/菜单与窗口（MenuWindow）/编辑器扩展AI常识_EditorExtensionCommonSense_AI协作警告.md` (`f8b5dd538e5747a9fe5914fa30df168801db051911082dbfc397ddf767a439ce`)
- `Assets/Plugins/ES/Editor/EditorTools/ESWindowLauncher.cs` (`7074fe9d43701194ecfbbe3226ba7d4ad5d7982144c7b92ca9406cf4e6dcf22a`)
