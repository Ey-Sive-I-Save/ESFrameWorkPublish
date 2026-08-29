# 编辑器 AssemblyStream 初始化边界：保真 Knowledge

`KnowledgeId`: `es.aiwarning.p0.editor-assembly-stream-initialization.v1`  
`Authority`: `AIWarnings` 与当前 Editor AssemblyStream/生命周期实现  
`RouteKeys`: `aiwarnings`, `p0`, `editor`, `initialization`, `assembly-stream`, `domain-reload`, `delay-call`  
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`HashSchema`: `v2`  
`ContentHash`: `ca4fc46db910f3f8717bc6c7567af3443d6db41d8b78e83cc48ccc90dc74a6f1`  
`SourceSetHash`: `ca4fc46db910f3f8717bc6c7567af3443d6db41d8b78e83cc48ccc90dc74a6f1`  
`EntryBodyHash`: `a367f6152cb3955aa25aec8ce4b7f39fef9a850a1d1b9c4e99aca97aa28c4d2a`
`StaleWhen`: AssemblyStream、EditorInvoker/Register、域重载、ESSO 预加载或任一 SourceRef 哈希变化。

## 迁移范围

Warning 保留原生入口例外、轻量可重复初始化、订阅生命周期和 ESSO 预加载 P0；本条目承载注册器类型、根入口特例、延迟回调边界、测量解释和验收细节。Knowledge 不授予 Unity 执行或资产修改权限。

## 注册路径与例外

普通编辑器自动初始化优先使用 `EditorInvoker_Level0/1/2/50`、`EditorRegister_FOR_Singleton<T>`、`EditorRegister_FOR_AsSubclass<T>`、`EditorRegister_FOR_ClassAttribute<TAttribute>`、`EditorRegister_FOR_FieldAttribute<TAttribute>`、`EditorRegister_FOR_PropertyAttribute<TAttribute>`、`EditorRegister_FOR_MethodAttribute<TAttribute>`。`[InitializeOnLoad]` / `[InitializeOnLoadMethod]` 仅限 AssemblyStream 根引导或 Unity/第三方强制的极少数全局桥接，必须给出不能使用 AssemblyStream 的理由。

自动入口须可重复、去重、轻量且无不受控副作用；普通工具/示例安装器/窗口辅助类/RuntimeWatch/临时测试不得接入原生域重载。静态构造器不得无条件 `EditorApplication.delayCall += ...`；域重载入口不得创建场景对象、扫全项目资产、刷新/打开窗口、写 EditorPrefs 或 MarkSceneDirty。`EditorApplication.update` 订阅必须有状态门控、异常保护和对称退订。

`delayCall`/`update` 可用于用户按钮后的 UI 刷新、窗口打开期间、预览/拖拽、异步包管理等有明确开始/结束的任务；不可用于静态全局常驻、域重载后无条件扫描/创建或无退订条件的监听。

## ESSO 预加载与性能证据

`[ESSOEditorPreLoad]` 先由程序集流在 Level0 登记，再由 `SoEditorIniter` 消费；仅当编辑器启动即需且收益可证明时使用。当前允许类型为 `ESSceneGlobalData`、`ESGlobalProjectAssetGuideData`、`ESGlobalEditorLocation`、`ESGlobalEditorDefaultConfi`；普通 GameCore、资源库、示例、诊断 SO 不得因方便加入。新增前需说明按需加载不可行、预期资产量、全项目扫描风险和域重载重复安全。

性能报告必须分开“程序集流/类型登记”和“Unity 资产反序列化”。历史样本为 45 GUID、45 路径、86 ESSO、约 376ms，其中 `AssetDatabase.LoadAllAssetsAtPath` 约 362ms；这说明主要耗时在 SO 加载，不能写成 `SoEditorIniter` 注册耗时。

## 根入口与验收

`Assets/Plugins/ES/0_Stand/Stand_Tools/AssemblyStream/-ESAssemblyStream.cs` 是根引导特例；`Assets/Plugins/ES/Editor/Out/ToolbarExtender.cs` 是 Toolbar 桥接特例，二者都不是普通工具模板。验收应扫描新增原生入口、检查重复执行/对称退订/副作用和预加载类型，并通过 UTF-8、U+FFFD/乱码与 `git diff --check`；Unity 域重载行为本轮未执行。

## 原文快照

迁移前台账快照：118 行、5607 字节，原始 SHA-256 `63054f018470f0c3a07ae63b78879cb6c24c39bcc982689890a7cab7990e9af5`。

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）/项目最高警告_禁止滥用InitializeOnLoad_优先程序集流注册器_AI协作警告.md` (`b7c986c498ce3f25a03afdd3c5dbd684e5913382f45d49012d3eae5195ccad28`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`9045c175129cd63b94bc22695b9a6f5b6d30f5d9b5e06103f4a7462e57cf246a`)

## EvidenceRefs

- `.agents/skills/es-aiwarning-authoring/scripts/Test-ESAIWarningRoute.ps1`
- `.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeValidation.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-p0-editor-assembly-stream-initialization.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/编辑器启动与生命周期（EditorLifecycle）/项目最高警告_禁止滥用InitializeOnLoad_优先程序集流注册器_AI协作警告.md`
