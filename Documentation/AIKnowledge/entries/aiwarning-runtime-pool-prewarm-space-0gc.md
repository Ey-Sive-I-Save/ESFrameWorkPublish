# 对象池预热、Space 与 0GC：保真 Knowledge
`KnowledgeId`: `es.aiwarning.runtime.pool-prewarm-space-0gc.v1`  
`Authority`: `AIWarnings` 与当前 GameObject 对象池实现  
`RouteKeys`: `aiwarnings`, `runtime`, `pool`, `prewarm`, `space`, `gc`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `43e91d81795e3b73f1e5b54cfbb3dc81c59bc3e2a099520285d1be5072189815`  
`SourceSetHash`: `43e91d81795e3b73f1e5b54cfbb3dc81c59bc3e2a099520285d1be5072189815`  
`EntryBodyHash`: `131d140fa2c05c810446e9df989de0e2637feaeab07259095abbf43937189d09`  
`StaleWhen`: 对象池模块、PrefabPrewarmData 或任一 SourceRef 哈希变化。

## 迁移范围
Warning 保留对象池职责、预热作用域和热路径 0GC 边界；本条目承载源码入口、配置字段、Scene/Space 生命周期、重复判定及验收细节。Knowledge 不授予运行时修改权限。

## 当前实现与入口
核心实现为 `MODULE_ESGameObjectPoolModule.cs`，配置数据为 `PrefabPrewarmDataInfo`、`PrefabPrewarmDataGroup`、`PrefabPrewarmDataPack`。模块入口包括 `prewarmSources`、`loadPrewarmOnStart`、`autoLoadOnSceneLoaded`、`unloadPrewarmOnSceneUnloaded`、`currentSpaceName`；数据支持 `supportAllScenes/supportedScenes`、`supportAllSpaces/supportedSpaces` 与 `entries`。

## 作用域与调用
- 关卡/玩法开始前，将高频 Prefab 放入 `PrefabPrewarmDataInfo` 并挂入 `ESGameObjectPoolModule.prewarmSources`。
- `RegisterPrewarmSource(dataInfo, loadImmediately: true)` 立即载入；登记后再用 `LoadConfiguredPrewarmForCurrentScene()` 统一载入。
- `sceneLoaded` 按 Scene + `currentSpaceName` 尝试载入，`sceneUnloaded` 释放；外部 Space 管理器通过 `NotifySpaceChanged(spaceName[, unloadOldSpace])` 通知。
- 重复预热键为 `PrefabPrewarmDataInfo + sceneName + spaceName`，不能退化为只按场景名。

## 热路径与职责边界
`GetInPool/PushToPool` 不遍历预热配置、不检查 Scene/Space、不拼业务字符串；管理路径才允许少量字典/HashSet 初始化。高频访问优先使用 Prefab 或已注册 key，禁止 Tick 临时拼 string、首次建池或集合扩容；查询组件应复用 List。对象池不承载伤害、命中、Buff、VFX 或 Shot 飞行语义。

## 原文快照与验收
迁移前台账：80 行、2773 字节，原始 SHA-256 `f88f17a86b2703c968ba19aefafacfc36b79c26c0b20d567dd0e69d10b7c25a3`。验收需覆盖场景/Space 预热与释放、重复判定、借还复用和稳态无分配；本轮未运行 Unity/Runtime。

## SourceRefs
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/对象池（Pool）/对象池预热_Space与0GC_AI协作警告.md` (`8c2c59d6d08a738eae2e073c4668f9946214fb0a77470d224ce611a1b52c348c`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`082a1d815125d2d997fe0ce49148b66d2d388fea6bbdffd70f9650678dfd4270`)

## EvidenceRefs
- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads
- `Documentation/AIKnowledge/entries/aiwarning-runtime-pool-prewarm-space-0gc.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/30_运行时专项（RuntimeOperations）/对象池（Pool）/对象池预热_Space与0GC_AI协作警告.md`
